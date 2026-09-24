using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow
{
    // First-person weapons, CS style, drawn by their own overlay camera on their own layer so
    // they never clip into ramps or walls. Built from simple shapes at startup.
    //
    // Slot 2 is the sniper (see ViewModel.Sniper.cs), slot 3 the knife; Q swaps to the last
    // weapon and the mouse wheel toggles. The held weapon sways and lags behind your mouse,
    // bobs with your speed, tilts when you strafe and breathes a little at rest. A small
    // see-through panel in the bottom right shows which weapon you're holding.
    //
    // Like CS, the weapon sets your max speed: knife 250, sniper 200, scoped 100. That feeds
    // air acceleration too, so the knife is still the one to surf with.
    [RequireComponent(typeof(Camera))]
    public partial class ViewModel : MonoBehaviour
    {
        public PlayerMovement player;
        [Tooltip("Any URP Lit material; the weapon and glove materials are made from it")]
        public Material template;
        public float fieldOfView = 58f;
        [Tooltip("Optional: a clip to use for the sniper shot instead of the generated one")]
        public AudioClip sniperShotClip;

        const int Layer = 30; // drawn only by the viewmodel camera
        public const int KnifeSlot = 0, SniperSlot = 1;

        class Weapon
        {
            public string name, rarity;
            public Color color;
            public float speed;    // max speed in Source units per second
            public float drawTime; // seconds before it can be used after switching to it
            public Transform root;
            public Vector3 restPosition;
            public Quaternion restRotation;
        }

        Weapon[] weapons;
        int current = KnifeSlot, previous = SniperSlot;
        float drawTime = 99f;

        // Resting spot of the knife hand, relative to the camera (right, down, forward)
        static readonly Vector3 KnifeRest = new(0.17f, -0.15f, 0.4f);

        Camera view, overlay;
        AudioSource audioSource;
        Transform anchor, hand;
        Texture2D panel;
        GUIStyle nameStyle, detailStyle;
        Vector3 sway, swayVelocity, tilt;
        float bobPhase, inspectTime = -1f, slashTime = -1f;
        readonly List<Material> materials = new();

        Weapon Current => weapons[current];

        void Awake()
        {
            if (anchor) return;
            view = GetComponent<Camera>();
            view.cullingMask &= ~(1 << Layer);

            // Overlay camera that draws only the viewmodel, on top of the world
            overlay = new GameObject("ViewModel Camera").AddComponent<Camera>();
            overlay.transform.SetParent(transform, false);
            overlay.cullingMask = 1 << Layer;
            overlay.fieldOfView = fieldOfView;
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 3f;
            var overlayData = overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderShadows = false;
            view.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

            if (Application.isPlaying)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }

            anchor = new GameObject("ViewModel").transform;
            anchor.SetParent(transform, false);

            weapons = new[]
            {
                new Weapon { name = "Standard Knife", rarity = "Default", color = new Color(0.7f, 0.72f, 0.78f), speed = 250f, drawTime = 0.6f },
                new Weapon { name = "Longreach", rarity = "Default", color = new Color(0.7f, 0.72f, 0.78f), speed = 200f, drawTime = 1.1f },
            };
            weapons[KnifeSlot].root = BuildKnife();
            weapons[KnifeSlot].restPosition = KnifeRest;
            weapons[KnifeSlot].restRotation = Quaternion.identity;
            weapons[SniperSlot].root = BuildSniper();
            weapons[SniperSlot].restPosition = SniperRest;
            weapons[SniperSlot].restRotation = SniperRestRotation;
            foreach (var w in weapons)
            {
                w.root.SetLocalPositionAndRotation(w.restPosition, w.restRotation);
                w.root.gameObject.SetActive(w == Current);
            }
            SetupSniper();
        }

        // Builds the viewmodel outside play mode too, so editor tools can photograph it
        public void BuildNow() => Awake();

        public void Equip(int slot)
        {
            if (weapons == null || slot == current) return;
            SetZoom(0, false);
            inspectTime = slashTime = sniperInspect = -1f;
            previous = current;
            current = slot;
            drawTime = Application.isPlaying ? 0f : 99f;
            foreach (var w in weapons) w.root.gameObject.SetActive(w == Current);
            Play(WeaponSounds.Draw, 0.6f);
        }

        void OnDestroy()
        {
            RestoreView();
            foreach (var m in materials) Kill(m);
            Kill(panel);
            Kill(dot);
            Kill(scopeTexture);
        }

        static void Kill(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void Play(AudioClip clip, float volume = 1f)
        {
            if (audioSource && clip) audioSource.PlayOneShot(clip, volume);
        }

        // The scope overlay, then the weapon panel: bottom right, translucent, weapon name
        // over rarity and a hint. Drawn behind the rest of the HUD.
        void OnGUI()
        {
            if (weapons == null) return;
            GUI.depth = 10;
            DrawScope();
            if (zoom == 0) DrawCrosshair();
            if (!panel)
            {
                panel = new Texture2D(1, 1);
                panel.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.04f, 0.45f));
                panel.Apply();
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleRight };
            }
            var weapon = Current;
            const float w = 230f, h = 58f, margin = 16f;
            var box = new Rect(Screen.width - w - margin, Screen.height - h - margin, w, h);
            GUI.DrawTexture(box, panel);
            var old = GUI.color;
            GUI.color = weapon.color;
            GUI.DrawTexture(new Rect(box.xMax - 4f, box.y, 4f, box.height), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(box.x, box.y + 6f, w - 14f, 24f), weapon.name, nameStyle);
            detailStyle.normal.textColor = weapon.color;
            string detail = current == SniperSlot ? SniperStatus() : "F inspect";
            GUI.Label(new Rect(box.x, box.y + 30f, w - 14f, 20f), $"{weapon.rarity}   ·   {detail}", detailStyle);
        }

        [Header("Crosshair")]
        public Color crosshairColor = Color.white;
        public float crosshairSize = 5f; // dot diameter in pixels at 1080p

        Texture2D dot;

        // A small round white dot with a faint dark edge so it reads on any background.
        // Hidden while scoped (the scope has its own lines).
        void DrawCrosshair()
        {
            if (!dot)
            {
                const int size = 32;
                dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                    dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - r) * size / 2f)));
                }
                dot.Apply();
            }
            float d = Mathf.Max(3f, crosshairSize * Screen.height / 1080f);
            var center = new Vector2(Screen.width, Screen.height) * 0.5f;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(center.x - d * 0.5f - 1f, center.y - d * 0.5f - 1f, d + 2f, d + 2f), dot);
            GUI.color = crosshairColor;
            GUI.DrawTexture(new Rect(center.x - d * 0.5f, center.y - d * 0.5f, d, d), dot);
            GUI.color = old;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            float dt = Time.deltaTime;

            // Weapon switching: 2 sniper, 3 knife, Q last weapon, wheel toggles
            if (kb != null)
            {
                if (kb.digit2Key.wasPressedThisFrame) Equip(SniperSlot);
                else if (kb.digit3Key.wasPressedThisFrame) Equip(KnifeSlot);
                else if (kb.qKey.wasPressedThisFrame) Equip(previous);
            }
            if (locked && mouse != null && mouse.scroll.ReadValue().y != 0f) Equip(current == KnifeSlot ? SniperSlot : KnifeSlot);
            drawTime += dt;

            // Sway: the weapon lags behind the mouse and springs back
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

            anchor.localPosition = sway + bob + Vector3.up * breathe;
            anchor.localRotation = Quaternion.Euler(tilt + new Vector3(look.y * 0.04f, 0f, 0f));

            // Draw: the new weapon comes up from below the screen
            var weapon = Current;
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(drawTime / (weapon.drawTime * 0.6f)));
            weapon.root.SetLocalPositionAndRotation(
                weapon.restPosition + new Vector3(0f, -0.16f, -0.04f) * (1f - up),
                weapon.restRotation * Quaternion.Euler(40f * (1f - up), 0f, 0f));
            bool ready = drawTime >= weapon.drawTime;

            if (current == KnifeSlot) UpdateKnife(kb, mouse, locked, ready, dt);
            else UpdateSniper(kb, mouse, locked, ready, dt);
            UpdateEffects(dt);

            if (player) player.maxSpeed = (current == SniperSlot && zoom > 0 ? 100f : weapon.speed) * PlayerMovement.SourceUnit;
        }

        void UpdateKnife(Keyboard kb, Mouse mouse, bool locked, bool ready, float dt)
        {
            if (kb != null && kb.fKey.wasPressedThisFrame && inspectTime < 0f) inspectTime = 0f;
            if (ready && locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                slashTime = 0f;
                inspectTime = -1f;
                Play(WeaponSounds.Slash, 0.7f);
            }

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

        // Keyframes: time, offset, rotation offset (degrees), smoothly blended
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
            if (t <= keys[0].t) return (keys[0].pos, keys[0].rot);
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

        Material glove, strap, sleeve;

        Transform BuildKnife()
        {
            glove = Make(new Color(0.07f, 0.07f, 0.08f), 0.35f, 0f);
            strap = Make(new Color(0.95f, 0.38f, 0.05f), 0.3f, 0f);
            sleeve = Make(new Color(0.13f, 0.14f, 0.16f), 0.15f, 0f);
            Material steel = Make(new Color(0.78f, 0.8f, 0.84f), 0.8f, 0.55f);
            Material grip = Make(new Color(0.05f, 0.05f, 0.05f), 0.25f, 0f);

            var root = new GameObject("Knife Rig").transform;
            root.SetParent(anchor, false);
            hand = new GameObject("Hand").transform;
            hand.SetParent(root, false);
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

            Fist(hand, glove);
            return root;
        }

        // A gloved fist around a handle that runs along +Y (from -0.11 to 0): palm on the +X
        // side, four fingers wrapped around, thumb over the top, a strap across the back, then
        // the cuff and sleeve. `tips` colors the fingertips (fingerless gloves show cloth).
        void Fist(Transform parent, Material tips)
        {
            Part(parent, PrimitiveType.Cube, glove, new Vector3(0.03f, -0.055f, 0.004f), new Vector3(0.034f, 0.1f, 0.075f), Quaternion.Euler(0f, 0f, -4f));
            for (int f = 0; f < 4; f++)
            {
                float y = -0.018f - f * 0.024f;
                Part(parent, PrimitiveType.Capsule, glove, new Vector3(-0.004f, y, 0.02f), new Vector3(0.024f, 0.028f, 0.024f), Quaternion.Euler(0f, 0f, 90f));
                Part(parent, PrimitiveType.Capsule, tips, new Vector3(-0.016f, y, -0.004f), new Vector3(0.022f, 0.022f, 0.022f), Quaternion.Euler(90f, 0f, 0f));
            }
            Part(parent, PrimitiveType.Capsule, tips, new Vector3(0.012f, 0.004f, -0.024f), new Vector3(0.022f, 0.03f, 0.022f), Quaternion.Euler(20f, 0f, -35f));
            Part(parent, PrimitiveType.Cube, strap, new Vector3(0.048f, -0.05f, 0.004f), new Vector3(0.004f, 0.03f, 0.078f));
            Part(parent, PrimitiveType.Cylinder, glove, new Vector3(0.04f, -0.13f, 0.004f), new Vector3(0.07f, 0.035f, 0.07f), Quaternion.Euler(0f, 0f, -8f));
            Part(parent, PrimitiveType.Cylinder, sleeve, new Vector3(0.06f, -0.34f, 0.004f), new Vector3(0.085f, 0.19f, 0.085f), Quaternion.Euler(0f, 0f, -8f));
        }

        void Part(Transform parent, PrimitiveType shape, Material mat, Vector3 position, Vector3 scale) =>
            Part(parent, shape, mat, position, scale, Quaternion.identity);

        Transform Part(Transform parent, PrimitiveType shape, Material mat, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(shape);
            Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            Finish(go.GetComponent<MeshRenderer>(), mat);
            return go.transform;
        }

        // A cylinder from one point to another
        Transform Rod(Transform parent, Material mat, Vector3 from, Vector3 to, float diameter)
        {
            Vector3 d = to - from;
            return Part(parent, PrimitiveType.Cylinder, mat, (from + to) * 0.5f, new Vector3(diameter, d.magnitude * 0.5f, diameter),
                Quaternion.FromToRotation(Vector3.up, d));
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

        Material MakeGlow(Color color, float intensity)
        {
            var m = Make(color, 0f, 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
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
