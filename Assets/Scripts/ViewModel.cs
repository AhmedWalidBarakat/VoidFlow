using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow
{
    // The first-person knife, CS style: a gloved right hand holding a knife in the lower
    // right of the screen. It's drawn by its own overlay camera on its own layer, so it never
    // clips into ramps or walls. Built from simple shapes at startup.
    //
    // It sways and lags behind your mouse, bobs with your speed, tilts when you strafe, and
    // breathes a little at rest. F inspects the knife; left click swings it. A small
    // see-through panel in the bottom right shows which knife you're holding.
    [RequireComponent(typeof(Camera))]
    public class ViewModel : MonoBehaviour
    {
        public PlayerMovement player;
        [Tooltip("Any URP Lit material; the knife and glove materials are made from it")]
        public Material template;
        public float fieldOfView = 58f;

        [Header("Weapon shown in the HUD")]
        public string weaponName = "Standard Knife";
        public string rarity = "Default";
        public Color rarityColor = new(0.7f, 0.72f, 0.78f);

        const int Layer = 30; // drawn only by the viewmodel camera

        // Resting spot of the hand, relative to the camera (right, down, forward)
        static readonly Vector3 RestPosition = new(0.17f, -0.15f, 0.4f);

        Transform anchor, hand;
        Texture2D panel;
        GUIStyle nameStyle, detailStyle;
        Vector3 sway, swayVelocity, tilt;
        float bobPhase, inspectTime = -1f, slashTime = -1f;
        readonly List<Material> materials = new();

        void Awake()
        {
            if (anchor) return;
            var cam = GetComponent<Camera>();
            cam.cullingMask &= ~(1 << Layer);

            // Overlay camera that draws only the viewmodel, on top of the world
            var overlay = new GameObject("ViewModel Camera").AddComponent<Camera>();
            overlay.transform.SetParent(transform, false);
            overlay.cullingMask = 1 << Layer;
            overlay.fieldOfView = fieldOfView;
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 3f;
            var overlayData = overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderShadows = false;
            cam.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

            anchor = new GameObject("ViewModel").transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = RestPosition;
            BuildHandAndKnife();
        }

        // Builds the viewmodel outside play mode too, so editor tools can photograph it
        public void BuildNow() => Awake();

        void OnDestroy()
        {
            foreach (var m in materials) Kill(m);
            Kill(panel);
        }

        static void Kill(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // The weapon panel: bottom right, translucent, knife name over rarity and a hint
        void OnGUI()
        {
            if (!panel)
            {
                panel = new Texture2D(1, 1);
                panel.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.04f, 0.45f));
                panel.Apply();
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleRight };
            }
            const float w = 230f, h = 58f, margin = 16f;
            var box = new Rect(Screen.width - w - margin, Screen.height - h - margin, w, h);
            GUI.DrawTexture(box, panel);
            var old = GUI.color;
            GUI.color = rarityColor;
            GUI.DrawTexture(new Rect(box.xMax - 4f, box.y, 4f, box.height), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(box.x, box.y + 6f, w - 14f, 24f), weaponName, nameStyle);
            detailStyle.normal.textColor = rarityColor;
            GUI.Label(new Rect(box.x, box.y + 30f, w - 14f, 20f), $"{rarity}   ·   F inspect", detailStyle);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (kb != null && kb.fKey.wasPressedThisFrame && inspectTime < 0f) inspectTime = 0f;
            if (locked && mouse != null && mouse.leftButton.wasPressedThisFrame) { slashTime = 0f; inspectTime = -1f; }

            float dt = Time.deltaTime;

            // Sway: the hand lags behind the mouse and springs back
            Vector2 look = locked && mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            Vector3 swayTarget = new Vector3(-look.x, -look.y, 0f) * 0.0006f;
            swayTarget = Vector3.ClampMagnitude(swayTarget, 0.04f);
            sway = Vector3.SmoothDamp(sway, swayTarget, ref swayVelocity, 0.08f);

            // Bob with speed on the ground; strafe tilt from sideways motion
            float speed = player ? player.HorizontalSpeed : 0f;
            bool grounded = player && player.Grounded;
            bobPhase += dt * (grounded ? Mathf.Clamp(speed, 0f, 8f) * 1.6f : 0f);
            float bobAmount = grounded ? Mathf.Clamp01(speed / 7f) * 0.012f : 0f;
            Vector3 bob = new(Mathf.Sin(bobPhase) * bobAmount, -Mathf.Abs(Mathf.Cos(bobPhase)) * bobAmount, 0f);
            float breathe = Mathf.Sin(Time.time * 1.6f) * 0.003f;

            float sideways = 0f;
            if (player)
            {
                Vector3 local = transform.InverseTransformDirection(player.Velocity);
                sideways = Mathf.Clamp(local.x / 10f, -1f, 1f);
            }
            tilt = Vector3.Lerp(tilt, new Vector3(0f, 0f, -sideways * 6f + look.x * 0.05f), dt * 8f);

            anchor.localPosition = RestPosition + sway + bob + Vector3.up * breathe;
            anchor.localRotation = Quaternion.Euler(tilt + new Vector3(look.y * 0.04f, 0f, 0f));

            // Inspect and slash are keyframed offsets on the hand
            var (pos, rot) = (Vector3.zero, Vector3.zero);
            if (slashTime >= 0f)
            {
                slashTime += dt;
                (pos, rot) = Sample(SlashKeys, slashTime);
                if (slashTime > SlashKeys[^1].t) slashTime = -1f;
            }
            else if (inspectTime >= 0f)
            {
                inspectTime += dt;
                (pos, rot) = Sample(InspectKeys, inspectTime);
                if (inspectTime > InspectKeys[^1].t) inspectTime = -1f;
            }
            hand.localPosition = pos;
            hand.localRotation = HoldRotation * Quaternion.Euler(rot);
        }

        // Keyframes: time, hand offset, hand rotation offset (degrees), smoothly blended
        static readonly (float t, Vector3 pos, Vector3 rot)[] InspectKeys =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.45f, new Vector3(-0.1f, 0.06f, 0.04f), new Vector3(0f, 0f, 35f)),
            (1.1f, new Vector3(-0.1f, 0.07f, 0.04f), new Vector3(0f, 180f, 35f)),
            (1.8f, new Vector3(-0.08f, 0.09f, 0.02f), new Vector3(-50f, 180f, 60f)),
            (2.4f, new Vector3(-0.04f, 0.04f, 0.02f), new Vector3(-20f, 360f, 20f)),
            (2.9f, Vector3.zero, new Vector3(0f, 360f, 0f)),
        };

        static readonly (float t, Vector3 pos, Vector3 rot)[] SlashKeys =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.07f, new Vector3(0.04f, 0.04f, -0.02f), new Vector3(-15f, 0f, -25f)),
            (0.18f, new Vector3(-0.14f, -0.06f, 0.12f), new Vector3(45f, 0f, 70f)),
            (0.4f, Vector3.zero, Vector3.zero),
        };

        static (Vector3, Vector3) Sample((float t, Vector3 pos, Vector3 rot)[] keys, float t)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].t) continue;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(keys[i - 1].t, keys[i].t, t));
                return (Vector3.Lerp(keys[i - 1].pos, keys[i].pos, s), Vector3.Lerp(keys[i - 1].rot, keys[i].rot, s));
            }
            return (keys[^1].pos, keys[^1].rot);
        }

        // How the hand holds the knife: blade up and forward, slightly toward the center
        static readonly Quaternion HoldRotation = Quaternion.Euler(55f, -5f, 10f);

        void BuildHandAndKnife()
        {
            Material glove = Make(new Color(0.07f, 0.07f, 0.08f), 0.35f, 0f);
            Material strap = Make(new Color(0.95f, 0.38f, 0.05f), 0.3f, 0f);
            Material sleeve = Make(new Color(0.13f, 0.14f, 0.16f), 0.15f, 0f);
            Material steel = Make(new Color(0.78f, 0.8f, 0.84f), 0.8f, 0.55f);
            Material grip = Make(new Color(0.05f, 0.05f, 0.05f), 0.25f, 0f);

            hand = new GameObject("Hand").transform;
            hand.SetParent(anchor, false);
            hand.localRotation = HoldRotation;

            // Knife, in hand space: blade along +Y from the guard, edge facing -X
            var knife = new GameObject("Knife").transform;
            knife.SetParent(hand, false);
            Part(knife, PrimitiveType.Cylinder, grip, new Vector3(0f, -0.055f, 0f), new Vector3(0.024f, 0.055f, 0.024f));
            Part(knife, PrimitiveType.Cylinder, steel, new Vector3(0f, -0.114f, 0f), new Vector3(0.028f, 0.006f, 0.028f));
            Part(knife, PrimitiveType.Cube, steel, new Vector3(0f, 0.003f, 0f), new Vector3(0.056f, 0.008f, 0.022f));
            var blade = new GameObject("Blade");
            blade.transform.SetParent(knife, false);
            blade.transform.localPosition = new Vector3(0f, 0.007f, 0f);
            blade.AddComponent<MeshFilter>().sharedMesh = BladeMesh();
            Finish(blade.AddComponent<MeshRenderer>(), steel);

            // Gloved fist around the handle: palm on the spine side, four fingers wrapped
            // around, thumb over the top, a strap across the back, then the cuff and sleeve
            Part(hand, PrimitiveType.Cube, glove, new Vector3(0.03f, -0.055f, 0.004f), new Vector3(0.034f, 0.1f, 0.075f), Quaternion.Euler(0f, 0f, -4f));
            for (int f = 0; f < 4; f++)
            {
                float y = -0.018f - f * 0.024f;
                Part(hand, PrimitiveType.Capsule, glove, new Vector3(-0.004f, y, 0.02f), new Vector3(0.024f, 0.028f, 0.024f), Quaternion.Euler(0f, 0f, 90f));
                Part(hand, PrimitiveType.Capsule, glove, new Vector3(-0.016f, y, -0.004f), new Vector3(0.022f, 0.022f, 0.022f), Quaternion.Euler(90f, 0f, 0f));
            }
            Part(hand, PrimitiveType.Capsule, glove, new Vector3(0.012f, 0.004f, -0.024f), new Vector3(0.022f, 0.03f, 0.022f), Quaternion.Euler(20f, 0f, -35f));
            Part(hand, PrimitiveType.Cube, strap, new Vector3(0.048f, -0.05f, 0.004f), new Vector3(0.004f, 0.03f, 0.078f));
            Part(hand, PrimitiveType.Cylinder, glove, new Vector3(0.04f, -0.13f, 0.004f), new Vector3(0.07f, 0.035f, 0.07f), Quaternion.Euler(0f, 0f, -8f));
            Part(hand, PrimitiveType.Cylinder, sleeve, new Vector3(0.06f, -0.34f, 0.004f), new Vector3(0.085f, 0.19f, 0.085f), Quaternion.Euler(0f, 0f, -8f));
        }

        void Part(Transform parent, PrimitiveType shape, Material mat, Vector3 position, Vector3 scale) =>
            Part(parent, shape, mat, position, scale, Quaternion.identity);

        void Part(Transform parent, PrimitiveType shape, Material mat, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(shape);
            Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            Finish(go.GetComponent<MeshRenderer>(), mat);
        }

        static void Finish(MeshRenderer r, Material mat)
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.gameObject.layer = Layer;
        }

        Material Make(Color color, float smoothness, float metallic)
        {
            var m = new Material(template);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            materials.Add(m);
            return m;
        }

        // A clip-point blade: straight spine that dips to the tip, curved belly on the edge
        // side, thick at the spine and thin at the edge
        static Mesh BladeMesh()
        {
            // Outline in (across, along); edge side is -x
            var outline = new[]
            {
                new Vector2(-0.014f, 0f), new Vector2(-0.016f, 0.07f), new Vector2(-0.014f, 0.13f),
                new Vector2(-0.008f, 0.175f), new Vector2(0f, 0.2f), new Vector2(0.006f, 0.165f),
                new Vector2(0.011f, 0.12f), new Vector2(0.011f, 0f),
            };
            float Half(float x) => Mathf.Lerp(0.0006f, 0.0026f, Mathf.InverseLerp(-0.016f, 0.011f, x));

            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector2 center = Vector2.zero;
            foreach (var p in outline) center += p;
            center /= outline.Length;

            // Two faces, fanned from the middle
            foreach (float side in new[] { 1f, -1f })
            {
                int c = verts.Count;
                verts.Add(new Vector3(center.x, center.y, side * Half(center.x)));
                foreach (var p in outline) verts.Add(new Vector3(p.x, p.y, side * Half(p.x)));
                for (int i = 0; i < outline.Length; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % outline.Length;
                    if (side > 0f) { tris.Add(c); tris.Add(b); tris.Add(a); }
                    else { tris.Add(c); tris.Add(a); tris.Add(b); }
                }
            }
            // Rim joining the faces
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % outline.Length];
                int s = verts.Count;
                verts.Add(new Vector3(p.x, p.y, Half(p.x)));
                verts.Add(new Vector3(q.x, q.y, Half(q.x)));
                verts.Add(new Vector3(q.x, q.y, -Half(q.x)));
                verts.Add(new Vector3(p.x, p.y, -Half(p.x)));
                tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }

            var mesh = new Mesh { name = "Blade" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
