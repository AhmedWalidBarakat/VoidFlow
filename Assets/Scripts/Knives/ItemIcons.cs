using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VoidFlow
{
    // Pictures of every item for the case strip and the inventory. Each one is built with the
    // same WeaponBuilder as the real thing, posed (blades on a diagonal, rifles side-on, gloves
    // three-quarter), lit by its own lights, and rendered once to a small transparent texture
    // far below the world. A couple are made per frame, the ones on screen first.
    public class ItemIcons : MonoBehaviour
    {
        public const int Width = 256, Height = 176, Layer = 31;
        const ItemSlot CaseSlot = (ItemSlot)99;

        static ItemIcons instance;
        static readonly Dictionary<(ItemSlot, int), RenderTexture> icons = new();
        static readonly List<(ItemSlot, int)> queue = new();
        static Material template;
        static Transform stage;
        static Camera iconCam;
        static Light[] lights;
        static RenderTexture work;
        float born;
        bool warmed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            icons.Clear();
            queue.Clear();
            instance = null;
            template = null;
        }

        public static Texture Get(ItemSlot slot, int index)
        {
            if (icons.TryGetValue((slot, index), out var rt) && rt && rt.IsCreated()) return rt;
            Request(slot, index, true);
            return null;
        }

        public static Texture VoidCase => Get(CaseSlot, 0);

        public static void Request(ItemSlot slot, int index, bool urgent)
        {
            if (!Application.isPlaying) return;
            if (!instance) instance = new GameObject("Item Icons") { hideFlags = HideFlags.DontSave }.AddComponent<ItemIcons>();
            var key = (slot, index);
            if (icons.TryGetValue(key, out var rt) && rt && rt.IsCreated()) return;
            int at = queue.IndexOf(key);
            if (at >= 0)
            {
                if (!urgent || at == 0) return;
                queue.RemoveAt(at);
            }
            if (urgent) queue.Insert(0, key);
            else queue.Add(key);
        }

        public static void RequestAll(ItemSlot slot, bool urgent)
        {
            var pool = Skins.Pool(slot);
            for (int i = pool.Length - 1; i >= 0; i--) Request(slot, i, urgent);
        }

        // Starts making pictures of everything in the background
        public static void Warm() => Request(CaseSlot, 0, false);

        void Awake() => born = Time.unscaledTime;

        void LateUpdate()
        {
            if (!warmed && Time.unscaledTime - born > 1.5f)
            {
                warmed = true;
                foreach (ItemSlot slot in new[] { ItemSlot.Secondary, ItemSlot.Primary, ItemSlot.Hands }) RequestAll(slot, false);
            }
            if (!template)
            {
                var vm = FindAnyObjectByType<ViewModel>();
                if (vm) template = vm.template;
                if (!template) return;
            }
            for (int budget = 1; budget > 0 && queue.Count > 0; budget--) // one a frame keeps the GPU queue light
            {
                var key = queue[0];
                queue.RemoveAt(0);
                if (icons.TryGetValue(key, out var old) && old && old.IsCreated()) continue;
                icons[key] = Render(key.Item1, key.Item2, template);
            }
        }

        void OnDestroy()
        {
            foreach (var rt in icons.Values) if (rt) { rt.Release(); Destroy(rt); }
            icons.Clear();
            if (stage) Destroy(stage.gameObject);
            if (work) { work.Release(); Destroy(work); }
        }

        // ------------------------------------------------------------------ rendering

        static void SetupStage()
        {
            stage = new GameObject("Icon Stage") { hideFlags = HideFlags.HideAndDontSave }.transform;
            stage.position = new Vector3(0f, -4000f, 0f);
            iconCam = new GameObject("Icon Camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            iconCam.transform.SetParent(stage, false);
            iconCam.enabled = false;
            iconCam.orthographic = true;
            iconCam.clearFlags = CameraClearFlags.SolidColor;
            iconCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            iconCam.cullingMask = 1 << Layer;
            iconCam.nearClipPlane = 0.01f;
            iconCam.farClipPlane = 60f;
            iconCam.allowHDR = false;
            var data = iconCam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;

            // A bright key from the upper left, a cool rim from behind, a warm fill from below.
            // They're only switched on while a picture is being taken.
            lights = new[]
            {
                MakeLight(new Vector3(35f, -30f, 0f), new Color(1f, 0.97f, 0.92f), 1.5f),
                MakeLight(new Vector3(-20f, 150f, 0f), new Color(0.65f, 0.75f, 1f), 1.4f),
                MakeLight(new Vector3(-35f, 20f, 0f), new Color(1f, 0.8f, 0.7f), 0.5f),
            };
            work = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        }

        static Light MakeLight(Vector3 euler, Color color, float intensity)
        {
            var light = new GameObject("Icon Light") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
            light.transform.SetParent(stage, false);
            light.transform.localRotation = Quaternion.Euler(euler);
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << Layer;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        public static RenderTexture Render(ItemSlot slot, int index, Material template)
        {
            if (!stage || !iconCam) SetupStage();
            var mats = new List<Material>();
            var holder = new GameObject("Holder") { hideFlags = HideFlags.HideAndDontSave }.transform;
            holder.SetParent(stage, false);
            var builder = new WeaponBuilder(template, Layer, false, mats);
            WeaponParts parts;
            if (slot == CaseSlot) parts = builder.VoidCaseModel(holder);
            else
            {
                var skin = Skins.Pool(slot)[index];
                parts = slot switch
                {
                    ItemSlot.Primary => builder.Rifle(skin, holder),
                    ItemSlot.Hands => builder.GloveModel(skin, holder),
                    _ => builder.Knife(skin, holder),
                };
            }
            parts?.Animate(0.8f, -1f);

            // Pose it, then center it on the stage and frame it
            holder.localRotation = Pose(slot, MeshBounds(holder, holder));
            var b = MeshBounds(holder, stage);
            holder.localPosition -= b.center;
            float aspect = (float)Width / Height;
            iconCam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect) * (slot == CaseSlot ? 1.3f : 1.12f);
            iconCam.transform.localPosition = new Vector3(0f, slot == CaseSlot ? b.extents.y * 0.12f : 0f, -b.extents.z - 5f);

            // Flames face this camera for the picture
            WeaponFlames.Facing = iconCam;
            foreach (var f in holder.GetComponentsInChildren<WeaponFlames>())
            {
                f.brightOnly = true;
                f.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
            }
            WeaponFlames.Facing = null;

            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            foreach (var l in lights) l.enabled = true;
            iconCam.targetTexture = work;
            iconCam.Render();
            iconCam.targetTexture = null;
            foreach (var l in lights) l.enabled = false;
            RenderSettings.fog = fog;

            var rt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                useMipMap = false, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            rt.Create();
            Graphics.Blit(work, rt);

            // Tidy up at once (hidden first, so a second picture this frame can't include it)
            holder.gameObject.SetActive(false);
            foreach (var mf in holder.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh && mf.sharedMesh.name is "Blade" or "Crystal" or "Ring") WeaponBuilder.Kill(mf.sharedMesh);
            WeaponBuilder.Kill(holder.gameObject);
            foreach (var m in mats) WeaponBuilder.Kill(m);
            return rt;
        }

        // Blades on an up-right diagonal with a flat side to the camera, rifles side-on with the
        // muzzle to the right, gloves and the case turned three-quarter
        static Quaternion Pose(ItemSlot slot, Bounds b)
        {
            if (slot == CaseSlot) return Quaternion.Euler(-18f, 30f, 0f);
            if (slot == ItemSlot.Hands) return Quaternion.Euler(-10f, 205f, 0f);
            Vector3 s = b.size;
            int longest = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
            int thinnest = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
            if (thinnest == longest) thinnest = (longest + 1) % 3;
            // The far end (tip or muzzle) is on the side that reaches further from the grip
            Vector3 along = Axis(longest) * (b.center[longest] >= 0f ? 1f : -1f);
            if (slot == ItemSlot.Primary)
            {
                const float tilt = 7f * Mathf.Deg2Rad;
                var toSide = Quaternion.LookRotation(new Vector3(Mathf.Cos(tilt), Mathf.Sin(tilt), 0f), new Vector3(-Mathf.Sin(tilt), Mathf.Cos(tilt), 0f))
                             * Quaternion.Inverse(Quaternion.LookRotation(along, Vector3.up));
                return Quaternion.AngleAxis(-14f, Vector3.right) * Quaternion.AngleAxis(-16f, Vector3.up) * toSide;
            }
            const float lean = 40f * Mathf.Deg2Rad;
            var diagonal = new Vector3(Mathf.Sin(lean), Mathf.Cos(lean), 0f);
            var flat = Quaternion.LookRotation(Vector3.forward, diagonal) * Quaternion.Inverse(Quaternion.LookRotation(Axis(thinnest), along));
            return Quaternion.AngleAxis(22f, diagonal) * flat;
        }

        static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        // Bounds of the meshes under `root`, in `space`'s coordinates (renderers have no
        // bounds until drawn). Flame sprites are left out.
        static Bounds MeshBounds(Transform root, Transform space)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.name == "Flame") continue;
                var mb = mf.sharedMesh.bounds;
                var toSpace = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = toSpace.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
    }
}
