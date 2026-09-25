using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // A weapon case in the start hall: an open briefcase with striped sides, light shooting
    // up out of it and its star weapon floating above. Walk up and press E to open it: a
    // strip of possible drops spins past and slows to a stop on what you got (Void 6% of the
    // time, otherwise Mythic), which is then equipped.
    //
    // The model is built when the scene loads (and in the editor), not saved into the scene.
    [ExecuteAlways]
    public class CaseStation : MonoBehaviour
    {
        public string title = "KNIFE CASE";
        public bool sniperCase;
        public bool gloveCase;
        [Tooltip("Which skin floats above the case")]
        public int showcaseSkin = 1;
        public Color stripeA = new(0.08f, 0.06f, 0.12f), stripeB = new(0.55f, 0.25f, 1f), rayColor = new(1f, 0.75f, 0.25f);
        [Tooltip("Any URP Lit material; the case's materials are made from it")]
        public Material template;
        public float useRange = 2.8f;

        Transform model, showcaseSpin;
        WeaponParts showcase;
        readonly List<(Transform t, float height, float phase)> rays = new();
        readonly List<Material> materials = new();
        Texture2D stripes;

        static readonly Vector3 ShowcaseSpot = new(0f, 1.75f, -0.25f); // above the case, in front of the lid

        Skins.Skin[] Pool => gloveCase ? Skins.Gloves : sniperCase ? Skins.Snipers : Skins.Knives;

        void OnEnable() => Build();

        void Start()
        {
            if (!Application.isPlaying) return;
            var at = new GameObject("Sparkle Spot").transform;
            at.SetParent(transform, false);
            at.localPosition = new Vector3(0f, 1.3f, 0f);
            FxLibrary.Sparkle(at, rayColor, 0.9f, 6f);
        }

        void OnDisable()
        {
            Clear();
            if (opening) ViewModel.InputBlocked = false;
        }

        // ------------------------------------------------------------------ model

        void Build()
        {
            Clear();
            if (!template) return;
            var b = new WeaponBuilder(template, gameObject.layer, true, materials);
            model = new GameObject("Case Model").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * 1.35f;

            stripes = StripeTexture(stripeA, stripeB);
            Material shell = b.Mat(Color.white, 0.55f, 0.3f);
            shell.SetTexture("_BaseMap", stripes);
            Material trim = b.Mat(new Color(0.75f, 0.77f, 0.82f), 0.85f, 0.9f);
            Material foam = b.Mat(new Color(0.05f, 0.05f, 0.07f), 0.1f, 0f);
            Material glow = b.Glow(rayColor, 2.2f, null);
            Material ray = b.Glow(rayColor, 2f, null);

            const float w = 1.6f, d = 1f, h = 0.42f, wall = 0.06f;
            var t = model;
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(0f, 0.025f, 0f), new Vector3(w, 0.05f, d));
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(0f, h / 2f, -d / 2f + wall / 2f), new Vector3(w, h, wall));
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(0f, h / 2f, d / 2f - wall / 2f), new Vector3(w, h, wall));
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(-w / 2f + wall / 2f, h / 2f, 0f), new Vector3(wall, h, d));
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(w / 2f - wall / 2f, h / 2f, 0f), new Vector3(wall, h, d));
            // Metal rim around the top and corners
            b.Part(t, PrimitiveType.Cube, trim, new Vector3(0f, h, -d / 2f + wall / 2f), new Vector3(w + 0.02f, 0.03f, wall + 0.02f));
            b.Part(t, PrimitiveType.Cube, trim, new Vector3(0f, h, d / 2f - wall / 2f), new Vector3(w + 0.02f, 0.03f, wall + 0.02f));
            b.Part(t, PrimitiveType.Cube, trim, new Vector3(-w / 2f + wall / 2f, h, 0f), new Vector3(wall + 0.02f, 0.03f, d + 0.02f));
            b.Part(t, PrimitiveType.Cube, trim, new Vector3(w / 2f - wall / 2f, h, 0f), new Vector3(wall + 0.02f, 0.03f, d + 0.02f));
            foreach (float x in new[] { -w / 2f, w / 2f })
            foreach (float z in new[] { -d / 2f, d / 2f })
                b.Part(t, PrimitiveType.Cube, trim, new Vector3(x, h / 2f, z), new Vector3(0.07f, h + 0.02f, 0.07f));
            // Handle and latches on the front
            b.Rod(t, trim, new Vector3(-0.22f, h * 0.55f, -d / 2f - 0.07f), new Vector3(0.22f, h * 0.55f, -d / 2f - 0.07f), 0.035f);
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                b.Part(t, PrimitiveType.Cube, trim, new Vector3(x, h * 0.55f, -d / 2f - 0.035f), new Vector3(0.04f, 0.04f, 0.07f));
                b.Part(t, PrimitiveType.Cube, trim, new Vector3(x * 2.4f, h * 0.8f, -d / 2f - 0.01f), new Vector3(0.1f, 0.07f, 0.03f));
            }
            // Glowing inside
            b.Part(t, PrimitiveType.Cube, foam, new Vector3(0f, 0.06f, 0f), new Vector3(w - 2f * wall, 0.03f, d - 2f * wall));
            b.Part(t, PrimitiveType.Cube, glow, new Vector3(0f, 0.08f, 0f), new Vector3(w - 0.3f, 0.01f, d - 0.3f));

            // Lid, hinged at the back and flipped open
            var lid = new GameObject("Lid").transform;
            lid.SetParent(t, false);
            lid.SetLocalPositionAndRotation(new Vector3(0f, h, d / 2f), Quaternion.Euler(105f, 0f, 0f));
            b.Part(lid, PrimitiveType.Cube, shell, new Vector3(0f, 0.06f, -d / 2f), new Vector3(w, 0.12f, d));
            b.Part(lid, PrimitiveType.Cube, foam, new Vector3(0f, -0.005f, -d / 2f), new Vector3(w - 0.1f, 0.02f, d - 0.1f));
            b.Part(lid, PrimitiveType.Cube, glow, new Vector3(0f, -0.018f, -d / 2f), new Vector3(w - 0.5f, 0.01f, 0.05f));
            b.Part(lid, PrimitiveType.Cube, trim, new Vector3(0f, 0.12f, -d / 2f), new Vector3(w + 0.02f, 0.02f, d + 0.02f));

            // Rays of light shooting up out of the case
            var random = new System.Random(title.GetHashCode());
            for (int i = 0; i < 16; i++)
            {
                float height = 0.5f + (float)random.NextDouble() * 2.2f;
                var p = new Vector3(((float)random.NextDouble() - 0.5f) * (w - 0.3f), 0.08f, ((float)random.NextDouble() - 0.5f) * (d - 0.3f));
                var r = b.Part(t, PrimitiveType.Cube, ray, p, new Vector3(0.022f, height, 0.022f));
                rays.Add((r, height, (float)random.NextDouble() * 10f));
            }

            // The star weapon, floating above at a jaunty angle
            showcaseSpin = new GameObject("Showcase").transform;
            showcaseSpin.SetParent(t, false);
            showcaseSpin.localPosition = ShowcaseSpot;
            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(showcaseSpin, false);
            tilt.localRotation = Quaternion.Euler(0f, 0f, sniperCase ? 15f : -35f);
            tilt.localScale = Vector3.one * (sniperCase ? 1.5f : gloveCase ? 5f : 6.5f);
            var offset = new GameObject("Offset").transform;
            offset.SetParent(tilt, false);
            offset.localPosition = sniperCase ? new Vector3(0f, -0.02f, -0.25f) : gloveCase ? new Vector3(0f, 0.07f, 0f) : new Vector3(0f, -0.06f, 0f);
            int skin = Mathf.Clamp(showcaseSkin, 0, Pool.Length - 1);
            showcase = gloveCase ? b.GloveModel(Pool[skin], offset) : sniperCase ? b.Rifle(Pool[skin], offset) : b.Knife(Pool[skin], offset);

            foreach (var tr in model.GetComponentsInChildren<Transform>(true)) tr.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        void Clear()
        {
            if (model) WeaponBuilder.Kill(model.gameObject);
            model = null;
            showcase = null;
            rays.Clear();
            foreach (var m in materials) WeaponBuilder.Kill(m);
            materials.Clear();
            WeaponBuilder.Kill(stripes);
        }

        // Bold diagonal bands with thin bright pinstripes between them
        static Texture2D StripeTexture(Color a, Color b)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float s = Mathf.Repeat((x + y) / (float)size * 3f, 1f);
                Color c = s < 0.45f ? a : b;
                if (Mathf.Abs(s - 0.45f) < 0.03f || s > 0.97f) c = Color.white;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ opening
        //
        // The show: the screen dims and a ray burst spins up behind the strip; tiles (each
        // showing a swatch of its finish) race past with ticks that drop in pitch as it slows,
        // the strip shaking with speed. A Void drop makes the screen throb purple as it creeps
        // to a stop. Then: a white flash, the winning weapon spinning big in front of you on
        // counter-rotating ray bursts with a glowing shockwave, confetti, and the rarity banner
        // sliding in. The sniper case locks on with closing crosshairs first. Close it and the
        // new skin is drawn into your hands.

        PlayerMovement player;
        ViewModel viewModel;
        AudioSource audioSource;
        GUIStyle promptStyle, tileTop, tileName, bigStyle, smallStyle, bannerStyle;
        bool near, opening, revealed;
        float openTime, revealTime, scrollTarget;
        int lastTick, winner;
        readonly List<int> strip = new();

        const int StripLength = 60, WinnerSlot = 52;
        const float TileWidth = 170f, TileHeight = 130f, Gap = 8f, IntroTime = 0.55f, SpinTime = 6.5f;

        static Texture2D burst, edgeFade;
        Transform stage, stageModel;
        WeaponParts stageParts;
        readonly List<Material> stageMaterials = new();
        readonly List<Renderer> stageRays = new();

        class Bit { public Vector2 p, v; public float rot, spin, size, life, age; public Color color; public bool star; }
        readonly List<Bit> confetti = new();

        float Spin => Mathf.Clamp01((Time.time - openTime - IntroTime) / SpinTime);
        bool IsVoid => Pool[winner].rarity == SkinRarity.Void;

        void Update()
        {
            float time = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;
            if (showcaseSpin) showcaseSpin.localRotation = Quaternion.Euler(0f, time * 35f, 0f);
            if (showcaseSpin) showcaseSpin.localPosition = ShowcaseSpot + Vector3.up * (Mathf.Sin(time * 1.3f) * 0.06f);
            showcase?.Animate(time, -1f);
            foreach (var (t, height, phase) in rays)
            {
                if (!t) continue;
                float k = 0.75f + 0.25f * Mathf.Sin(time * 2.2f + phase);
                t.localScale = new Vector3(0.022f, height * k, 0.022f);
                t.localPosition = new Vector3(t.localPosition.x, 0.08f + height * k * 0.5f, t.localPosition.z);
            }
            if (!Application.isPlaying) return;

            if (!player) player = FindAnyObjectByType<PlayerMovement>();
            var kb = Keyboard.current;
            bool ePressed = kb != null && kb.eKey.wasPressedThisFrame;
            if (opening)
            {
                if (!revealed)
                {
                    float s = Spin;
                    int tick = Mathf.FloorToInt(Scroll(s) / (TileWidth + Gap));
                    if (tick != lastTick)
                    {
                        lastTick = tick;
                        // Ticks drop in pitch as the strip slows
                        float speed = 1f - s;
                        Play(WeaponSounds.Tick, 0.55f, Mathf.Lerp(0.8f, 1.5f, speed * speed));
                    }
                    if (s >= 1f) Reveal();
                }
                else
                {
                    UpdateStage(Time.time - revealTime);
                    bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                    if ((ePressed || click) && Time.time - revealTime > 0.9f) Close();
                }
                UpdateConfetti(Time.deltaTime);
                return;
            }
            if (!player) return;
            Vector3 d = player.Position - transform.position;
            d.y = 0f;
            near = d.magnitude < useRange && !ViewModel.InputBlocked;
            if (near && ePressed) Open();
        }

        void Open()
        {
            // Fill the strip with random drops, then put the real one where it will stop
            strip.Clear();
            for (int i = 0; i < StripLength; i++) strip.Add(Skins.Roll(Pool));
            winner = Skins.Roll(Pool);
            strip[WinnerSlot] = winner;
            scrollTarget = WinnerSlot * (TileWidth + Gap) + TileWidth * Random.Range(0.08f, 0.92f);
            openTime = Time.time;
            lastTick = 0;
            opening = true;
            revealed = false;
            confetti.Clear();
            ViewModel.InputBlocked = true;
            Play(WeaponSounds.BoltBack, 0.8f, 0.8f);
            Play(WeaponSounds.Slash, 0.6f, 0.7f);
        }

        // How far the strip has moved at s (0..1 of the spin): fast, then a long slow-down
        float Scroll(float s) => scrollTarget * (1f - Mathf.Pow(1f - Mathf.Clamp01(s), 4.5f));

        void Reveal()
        {
            revealed = true;
            revealTime = Time.time;
            ViewModel.HideWeapons = true;
            Color color = Skins.RarityColor(Pool[winner].rarity);
            Play(WeaponSounds.Launch, 1f, 0.7f);
            Play(IsVoid ? WeaponSounds.VoidReveal : WeaponSounds.Reveal, 0.9f);
            if (IsVoid) Play(WeaponSounds.Shatter, 0.7f, 0.6f);
            BuildStage(color);
            SpawnConfetti(IsVoid ? 170 : 100, color);
            FxLibrary.Celebrate(transform.position + Vector3.up * 1.8f, color, IsVoid);
        }

        void Close()
        {
            opening = false;
            ViewModel.InputBlocked = false;
            ViewModel.HideWeapons = false;
            ClearStage();
            confetti.Clear();
            if (!viewModel) viewModel = FindAnyObjectByType<ViewModel>();
            if (viewModel)
            {
                if (gloveCase) viewModel.EquipGloveSkin(winner);
                else if (sniperCase) viewModel.EquipSniperSkin(winner);
                else viewModel.EquipKnifeSkin(winner);
            }
        }

        void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            if (!clip) return;
            if (Mathf.Approximately(pitch, 1f)) { audioSource.PlayOneShot(clip, volume); return; }
            // A pitched one-shot on its own little source, so it doesn't bend other sounds
            var one = gameObject.AddComponent<AudioSource>();
            one.spatialBlend = 0f;
            one.pitch = pitch;
            one.PlayOneShot(clip, volume);
            Destroy(one, clip.length / pitch + 0.1f);
        }

        // ------------------------------------------------------------------ 3D reveal stage

        static Texture2D Burst()
        {
            if (burst) return burst;
            const int size = 256;
            burst = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f) / (size / 2f);
                float r = d.magnitude, a = Mathf.Atan2(d.y, d.x);
                float spokes = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a * 8f)), 6f);
                float glow = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
                float alpha = Mathf.Clamp01(spokes * Mathf.Clamp01(1f - r) * 1.3f + glow * 0.6f);
                burst.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            burst.Apply();
            return burst;
        }

        static Texture2D EdgeFade()
        {
            if (edgeFade) return edgeFade;
            edgeFade = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) edgeFade.SetPixel(x, 0, new Color(0f, 0f, 0f, 1f - x / 63f));
            edgeFade.Apply();
            return edgeFade;
        }

        Material SeeThrough(Texture tex, Color color, float glow)
        {
            var m = ViewModel.MakeTransparent(new Material(template));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            if (glow > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * glow);
            }
            stageMaterials.Add(m);
            return m;
        }

        // Everything lives on the view model layer in front of the camera, drawn over the world
        void BuildStage(Color color)
        {
            ClearStage();
            var cam = Camera.main;
            if (!cam || !template) return;
            const int layer = 30;
            stage = new GameObject("Case Reveal").transform;
            stage.SetParent(cam.transform, false);
            stage.localPosition = new Vector3(0f, 0.02f, 0.7f);

            Transform Quad(string name, Material mat, Vector3 at, float size)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = name;
                WeaponBuilder.Kill(q.GetComponent<Collider>());
                q.layer = layer;
                q.transform.SetParent(stage, false);
                q.transform.localPosition = at;
                q.transform.localScale = Vector3.one * size;
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return q.transform;
            }

            // Dim the world behind it all
            var dim = Quad("Dim", SeeThrough(Texture2D.whiteTexture, new Color(0f, 0f, 0f, 0.8f), 0f), new Vector3(0f, 0f, 1.6f), 1f);
            dim.localScale = new Vector3(9f, 5f, 1f);
            // Two counter-rotating ray bursts, a soft halo and a shockwave, in the rarity color
            stageRays.Clear();
            stageRays.Add(Quad("Rays", SeeThrough(Burst(), color, 0.9f), new Vector3(0f, 0f, 0.35f), 1.5f).GetComponent<Renderer>());
            stageRays.Add(Quad("Rays 2", SeeThrough(Burst(), Color.Lerp(color, Color.white, 0.35f), 0.6f), new Vector3(0f, 0f, 0.33f), 1.1f).GetComponent<Renderer>());
            var halo = FxLibrary.Instance && FxLibrary.Instance.mote ? FxLibrary.Instance.mote.GetTexture("_BaseMap") : null;
            if (halo) stageRays.Add(Quad("Halo", SeeThrough(halo, color, 0.9f), new Vector3(0f, 0f, 0.3f), 0.7f).GetComponent<Renderer>());
            var ring = FxLibrary.Instance && FxLibrary.Instance.ring ? FxLibrary.Instance.ring.GetTexture("_BaseMap") : null;
            if (ring) stageRays.Add(Quad("Shockwave", SeeThrough(ring, color, 1.2f), new Vector3(0f, 0f, 0.2f), 0.1f).GetComponent<Renderer>());

            // The weapon, centered and sized to fit, spinning
            stageModel = new GameObject("Model").transform;
            stageModel.SetParent(stage, false);
            var holder = new GameObject("Holder").transform;
            holder.SetParent(stageModel, false);
            var builder = new WeaponBuilder(template, layer, false, stageMaterials);
            var skin = Pool[winner];
            stageParts = gloveCase ? builder.GloveModel(skin, holder) : sniperCase ? builder.Rifle(skin, holder) : builder.Knife(skin, holder);
            if (sniperCase) holder.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var bounds = new Bounds();
            bool first = true;
            // Measure from the meshes themselves (new renderers have no bounds until drawn)
            foreach (var mf in holder.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds;
                var toModel = stageModel.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = toModel.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            float big = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            holder.localPosition -= bounds.center;
            stageModel.localScale = Vector3.one * ((sniperCase ? 0.6f : 0.42f) / Mathf.Max(big, 0.01f));

            // A light so it shines
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(stage, false);
            light.transform.localPosition = new Vector3(0.15f, 0.2f, -0.3f);
            light.type = LightType.Point;
            light.range = 1.5f;
            light.intensity = 4f;
            light.color = Color.Lerp(color, Color.white, 0.6f);
            UpdateStage(0f);
        }

        void UpdateStage(float t)
        {
            if (!stage) return;
            // The weapon pops in with an overshoot, whirls, then turns slowly and bobs
            float pop = t < 0.5f ? ElasticOut(t / 0.5f) : 1f;
            stage.localScale = Vector3.one * Mathf.Max(0.01f, pop);
            float whirl = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.8f))) * 720f;
            // Knives turn round and round; a rifle sways side-on so you always see all of it
            float yaw = sniperCase ? Mathf.Sin(t * 1.2f) * 28f + whirl : t * 80f + whirl;
            stageModel.localRotation = Quaternion.Euler(sniperCase ? 8f : 12f, yaw, sniperCase ? 0f : -8f);
            stageModel.localPosition = new Vector3(0f, Mathf.Sin(t * 2f) * 0.008f, 0f);
            stageParts?.Animate(Time.time, -1f);
            // Rays spin opposite ways and breathe; the shockwave rushes out and fades
            for (int i = 0; i < stageRays.Count; i++)
            {
                var r = stageRays[i];
                if (!r) continue;
                if (r.name == "Shockwave")
                {
                    float k = Mathf.Clamp01(t / 0.7f);
                    r.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, IsVoid ? 3f : 2.2f, 1f - Mathf.Pow(1f - k, 3f));
                    var c = r.sharedMaterial.GetColor("_BaseColor");
                    c.a = 1f - k;
                    r.sharedMaterial.SetColor("_BaseColor", c);
                    continue;
                }
                if (r.name == "Halo") { r.transform.localScale = Vector3.one * (0.7f + Mathf.Sin(t * 4f) * 0.06f); continue; }
                float dir = i == 0 ? 1f : -1f, speed = IsVoid ? 45f : 28f;
                r.transform.localRotation = Quaternion.Euler(0f, 0f, dir * t * speed);
                float breathe = 1f + Mathf.Sin(t * 3f + i) * 0.05f;
                r.transform.localScale = Vector3.one * (i == 0 ? 1.5f : 1.1f) * breathe;
            }
        }

        static float ElasticOut(float x) => x >= 1f ? 1f : Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;

        void ClearStage()
        {
            if (stage) WeaponBuilder.Kill(stage.gameObject);
            stage = stageModel = null;
            stageParts = null;
            stageRays.Clear();
            foreach (var m in stageMaterials) WeaponBuilder.Kill(m);
            stageMaterials.Clear();
        }

        void OnDestroy()
        {
            ClearStage();
            if (opening) { ViewModel.InputBlocked = false; ViewModel.HideWeapons = false; }
        }

        // ------------------------------------------------------------------ confetti (GUI)

        void SpawnConfetti(int count, Color color)
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float scale = Screen.height / 1080f;
            Color[] palette = { color, Color.Lerp(color, Color.white, 0.5f), Color.white, new Color(1f, 0.85f, 0.3f), rayColor };
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), speed = Random.Range(250f, 950f) * scale;
                confetti.Add(new Bit
                {
                    p = center + Random.insideUnitCircle * 30f,
                    v = new Vector2(Mathf.Cos(a), Mathf.Sin(a) - 0.6f) * speed,
                    rot = Random.Range(0f, 360f), spin = Random.Range(-600f, 600f),
                    size = Random.Range(8f, 22f) * scale, life = Random.Range(1.6f, 3f),
                    color = palette[Random.Range(0, palette.Length)], star = Random.value < 0.45f,
                });
            }
        }

        void UpdateConfetti(float dt)
        {
            float scale = Screen.height / 1080f;
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                var b = confetti[i];
                b.age += dt;
                if (b.age > b.life) { confetti.RemoveAt(i); continue; }
                b.v.y += 900f * scale * dt;
                b.v *= 1f - 0.8f * dt;
                b.p += b.v * dt;
                b.rot += b.spin * dt;
            }
        }

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            if (!Application.isPlaying) return;
            GUI.depth = -10;
            promptStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (!opening)
            {
                if (!near) return;
                promptStyle.normal.textColor = rayColor;
                GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f), $"[E]  open  {title}", promptStyle);
                return;
            }

            tileTop ??= new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
            tileName ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft, wordWrap = true, normal = { textColor = Color.white } };
            bigStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.9f, 0.9f, 0.95f) } };
            bannerStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            var old = GUI.color;
            var oldMatrix = GUI.matrix;
            float w = Screen.width, h = Screen.height, now = Time.time;
            if (!revealed) DrawSpin(now - openTime, w, h);
            else DrawReveal(now - revealTime, w, h);

            // Confetti over everything
            var star = FxLibrary.Instance && FxLibrary.Instance.star ? FxLibrary.Instance.star.GetTexture("_BaseMap") : null;
            foreach (var b in confetti)
            {
                float fade = Mathf.Clamp01((b.life - b.age) / 0.5f);
                GUI.color = new Color(b.color.r, b.color.g, b.color.b, fade);
                GUIUtility.RotateAroundPivot(b.rot, b.p);
                if (b.star && star) GUI.DrawTexture(new Rect(b.p.x - b.size, b.p.y - b.size, b.size * 2f, b.size * 2f), star);
                else GUI.DrawTexture(new Rect(b.p.x - b.size * 0.5f, b.p.y - b.size * 0.25f, b.size, b.size * 0.5f), Texture2D.whiteTexture);
                GUI.matrix = oldMatrix;
            }
            GUI.color = old;
        }

        void DrawSpin(float t, float w, float h)
        {
            float intro = Mathf.Clamp01(t / IntroTime), s = Spin, speed = 1f - s;
            GUI.color = new Color(0f, 0f, 0f, 0.75f * intro);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);

            // A slow ray burst in the case color behind the strip
            var matrix = GUI.matrix;
            float size = h * 1.4f;
            GUI.color = new Color(rayColor.r, rayColor.g, rayColor.b, 0.35f * intro);
            GUIUtility.RotateAroundPivot(t * 25f, new Vector2(w / 2f, h / 2f));
            GUI.DrawTexture(new Rect(w / 2f - size / 2f, h / 2f - size / 2f, size, size), Burst());
            GUI.matrix = matrix;

            // A Void win makes the screen throb purple while it creeps to a stop
            if (IsVoid && s > 0.7f)
            {
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 5f)), 6f) * Mathf.InverseLerp(0.7f, 1f, s);
                GUI.color = new Color(0.55f, 0.15f, 1f, 0.35f * beat);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            }

            // The title slams in
            float slam = Mathf.Lerp(2.2f, 1f, Mathf.SmoothStep(0f, 1f, intro));
            GUI.color = new Color(1f, 1f, 1f, intro);
            promptStyle.normal.textColor = rayColor;
            GUIUtility.ScaleAroundPivot(Vector2.one * slam, new Vector2(w / 2f, h * 0.5f - TileHeight / 2f - 55f));
            GUI.Label(new Rect(0f, h * 0.5f - TileHeight / 2f - 70f, w, 30f), title, promptStyle);
            GUI.matrix = matrix;

            // The strip slides up into place and shakes with speed
            float stripWidth = Mathf.Min(w * 0.94f, 1250f);
            float rise = (1f - Mathf.SmoothStep(0f, 1f, intro)) * 80f;
            Vector2 shake = Random.insideUnitCircle * (speed * speed * 3f);
            var window = new Rect((w - stripWidth) / 2f + shake.x, h * 0.5f - TileHeight / 2f + rise + shake.y, stripWidth, TileHeight);
            GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.92f * intro);
            GUI.DrawTexture(new Rect(window.x - 6f, window.y - 6f, window.width + 12f, window.height + 12f), Texture2D.whiteTexture);
            float scroll = Scroll(s) - stripWidth / 2f;
            GUI.BeginGroup(window);
            for (int i = 0; i < strip.Count; i++)
            {
                float x = i * (TileWidth + Gap) - scroll;
                if (x > stripWidth || x + TileWidth < 0f) continue;
                DrawTile(new Rect(x, 0f, TileWidth, TileHeight), Pool[strip[i]], intro);
            }
            // Edges fade into the dark
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, 120f, TileHeight), EdgeFade());
            GUI.DrawTextureWithTexCoords(new Rect(stripWidth - 120f, 0f, 120f, TileHeight), EdgeFade(), new Rect(1f, 0f, -1f, 1f));
            GUI.EndGroup();

            // Glowing marker down the middle
            float glow = 0.6f + 0.4f * Mathf.Sin(t * 12f);
            GUI.color = new Color(rayColor.r, rayColor.g, rayColor.b, 0.3f * glow);
            GUI.DrawTexture(new Rect(w / 2f - 7f, window.y - 18f, 14f, window.height + 36f), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(rayColor, Color.white, 0.5f);
            GUI.DrawTexture(new Rect(w / 2f - 1.5f, window.y - 18f, 3f, window.height + 36f), Texture2D.whiteTexture);
        }

        void DrawReveal(float t, float w, float h)
        {
            var skin = Pool[winner];
            Color color = Skins.RarityColor(skin.rarity);
            var matrix = GUI.matrix;

            // Sniper case: crosshairs close in and lock on
            if (sniperCase && t < 0.45f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.45f), gap = Mathf.Lerp(h * 0.45f, 18f, k);
                GUI.color = rayColor;
                GUI.DrawTexture(new Rect(w / 2f - gap - 60f, h / 2f - 1.5f, 60f, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w / 2f + gap, h / 2f - 1.5f, 60f, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w / 2f - 1.5f, h / 2f - gap - 60f, 3f, 60f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w / 2f - 1.5f, h / 2f + gap, 3f, 60f), Texture2D.whiteTexture);
            }

            // A white flash, and a purple wash for Void
            float flash = 1f - Mathf.Clamp01(t / 0.35f);
            if (flash > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, flash * 0.9f);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            }
            if (IsVoid && t < 1.5f)
            {
                GUI.color = new Color(0.45f, 0.1f, 0.9f, 0.4f * (1f - t / 1.5f));
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            }

            // The rarity banner slides across
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.25f) / 0.35f));
            float by = h * 0.72f;
            GUI.color = new Color(color.r, color.g, color.b, 0.85f);
            GUI.DrawTexture(new Rect(-w + slide * w, by, w, 44f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(-w + slide * w, by, w, 44f), $"{Skins.RarityName(skin.rarity).ToUpper()}  ·  {(gloveCase ? "GLOVES" : sniperCase ? "SNIPER" : "KNIFE")} UNBOXED", bannerStyle);

            // The name pops in
            float pop = t < 0.3f ? 0f : ElasticOut(Mathf.Clamp01((t - 0.3f) / 0.5f));
            if (pop > 0.01f)
            {
                GUIUtility.ScaleAroundPivot(Vector2.one * pop, new Vector2(w / 2f, h * 0.2f));
                bigStyle.normal.textColor = color;
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, h * 0.2f - 25f, w, 50f), $"★ {skin.name}", bigStyle);
                GUI.matrix = matrix;
            }
            if (t > 0.9f)
            {
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - 0.9f) * 3f) * (0.6f + 0.4f * Mathf.Sin(t * 4f)));
                GUI.Label(new Rect(0f, by + 56f, w, 24f), "[E] or click to equip", smallStyle);
            }
        }

        void DrawTile(Rect r, Skins.Skin skin, float alpha)
        {
            Color rarity = Skins.RarityColor(skin.rarity);
            // A swatch of the finish fills the tile
            var look = KnifeFinishes.Get(skin.finish);
            Texture swatch = look.albedo ? look.albedo : look.emission;
            GUI.color = new Color(0.12f, 0.12f, 0.15f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            if (swatch)
            {
                GUI.color = skin.rarity == SkinRarity.Void ? new Color(rarity.r, rarity.g, rarity.b, alpha) : new Color(1f, 1f, 1f, alpha);
                GUI.DrawTextureWithTexCoords(r, swatch, new Rect(0.1f, 0.1f, 0.5f, 0.5f * r.height / r.width));
            }
            // Darken the bottom for the text, rarity glow along it
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height * 0.45f, r.width, r.height * 0.55f), Texture2D.whiteTexture);
            GUI.color = new Color(rarity.r, rarity.g, rarity.b, 0.35f * alpha);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 18f, r.width, 12f), Texture2D.whiteTexture);
            GUI.color = new Color(rarity.r, rarity.g, rarity.b, alpha);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 6f, r.width, 6f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            string kind = skin.model switch
            {
                var m when Skins.IsGlove(m) => m == KnifeModel.Glove ? "GLOVES" : "VOID GLOVES",
                KnifeModel.Talon => "TALON KNIFE",
                KnifeModel.Butterfly => "BUTTERFLY",
                KnifeModel.Rifle => "LONGREACH",
                KnifeModel.Reaper => "VOID SCYTHE",
                KnifeModel.Saber => "PLASMA SABER",
                KnifeModel.Shardfang => "CRYSTAL DAGGER",
                KnifeModel.Railgun => "VOID RAILGUN",
                KnifeModel.Hellfire => "VOID RIFLE",
                KnifeModel.Kukri => "VOID KUKRI",
                KnifeModel.Claws => "VOID CLAWS",
                KnifeModel.Axe => "VOID AXE",
                KnifeModel.Sai => "VOID SAI",
                KnifeModel.Spear => "VOID SPEAR",
                KnifeModel.Kris => "VOID KRIS",
                KnifeModel.Prism or KnifeModel.Bone or KnifeModel.Lance or KnifeModel.Seraph => "VOID RIFLE",
                _ => "VOID BLADE",
            };
            tileTop.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, 18f), $"★ {kind}", tileTop);
            int bar = skin.name.IndexOf('|');
            string finish = bar >= 0 ? skin.name.Substring(bar + 1).Trim() : skin.name;
            GUI.Label(new Rect(r.x + 8f, r.y + 40f, r.width - 16f, r.height - 62f), finish, tileName);
            tileTop.normal.textColor = rarity;
            GUI.Label(new Rect(r.x + 8f, r.yMax - 24f, r.width - 16f, 18f), Skins.RarityName(skin.rarity).ToUpper(), tileTop);
        }
    }
}
