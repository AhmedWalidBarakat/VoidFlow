using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow
{
    // First-person weapons, CS style, drawn by their own overlay camera on their own layer so
    // they never clip into ramps or walls. Built from simple shapes at startup, with the
    // equipped skins (see Skins.cs and WeaponBuilder.cs).
    //
    // Slot 1 is the knife, slot 2 the sniper (see ViewModel.Sniper.cs); Q swaps to the last
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
        [Tooltip("Saved materials whose shader variants runtime materials use (see-through glows), so builds keep them")]
        public Material[] keepVariants;
        [Tooltip("Field of view of the world, horizontal like CS (the same on any screen shape)")]
        public float horizontalFov = 106.26f; // CS2's: 90 at 4:3, the same view at 16:9
        [Tooltip("Field of view of the hands and weapon (vertical), kept separate so they never stretch")]
        public float fieldOfView = 58f;
        [Tooltip("Optional: a clip to use for the sniper shot instead of the generated one")]
        public AudioClip sniperShotClip;
        [Tooltip("The particle effects (for the muzzle flash and scorch sprites)")]
        public FxLibrary fx;

        const int Layer = 30; // drawn only by the viewmodel camera
        public const int KnifeSlot = 0, SniperSlot = 1;

        class Weapon
        {
            public float speed;    // max speed in Source units per second
            public float drawTime; // seconds before it can be used after switching to it
            public Transform root;
            public Vector3 restPosition;
            public Quaternion restRotation;
            // Personality: how it sits and moves in the hand. The knife is light (quick draw,
            // little sway, barely any inertia); the sniper is heavy (slow swing up, more lag
            // behind the mouse, carries its momentum, softer spring)
            public float sway = 1f, swaySmooth = 0.08f, inertia = 1f, spring = 16f, holster = 0.15f, drawVisual = 0.5f;
            public (float t, Vector3 pos, Vector3 rot)[] drawKeys;
        }

        // Draws, as (fraction of the draw, offset, rotation): up from low right into frame, a
        // touch past rest, the glove re-gripping (a wrist roll), then settling
        static readonly (float t, Vector3 pos, Vector3 rot)[] KnifeDrawKeys =
        {
            (0f, new Vector3(0.05f, -0.2f, -0.04f), new Vector3(50f, -15f, 30f)),
            (0.45f, new Vector3(0f, 0.008f, 0.004f), new Vector3(-4f, 3f, -4f)),
            (0.62f, new Vector3(0.002f, -0.004f, 0f), new Vector3(2f, -1f, 7f)),
            (0.8f, new Vector3(0f, 0.002f, 0f), new Vector3(-1f, 0f, -2f)),
            (1f, Vector3.zero, Vector3.zero),
        };
        // ...the sniper swings up heavier and dips into the hands with its weight
        static readonly (float t, Vector3 pos, Vector3 rot)[] SniperDrawKeys =
        {
            (0f, new Vector3(0.1f, -0.26f, -0.1f), new Vector3(35f, -25f, 20f)),
            (0.5f, new Vector3(-0.004f, 0.012f, 0.01f), new Vector3(-5f, 2f, -3f)),
            (0.66f, new Vector3(0f, -0.01f, -0.004f), new Vector3(3f, -1f, 5f)),
            (0.82f, new Vector3(0f, 0.003f, 0f), new Vector3(-1f, 0f, -1f)),
            (1f, Vector3.zero, Vector3.zero),
        };

        Weapon[] weapons;
        int current = KnifeSlot, previous = SniperSlot;
        float drawTime = 99f;
        // Switching: the weapon in hand drops out of view before the new one comes up
        Weapon holstering;
        float holsterTime;
        // Inertia: a small spring the weapon rides on, kicked by changes in your velocity
        Vector3 inertia, inertiaVelocity, lastVelocity;
        Vector2 lookRate;

        // Resting spot of the knife hand, relative to the camera (right, down, forward)

        Camera view, overlay;
        AudioSource audioSource;
        ReflectionProbe probe;
        float probeTimer;
        Transform anchor, hand;
        Vector3 sway, swayVelocity, tilt;
        float bobPhase, inspectTime = -1f, slashTime = -1f;
        readonly List<Material> materials = new();

        int knifeSkin, sniperSkin;
        WeaponParts knife;
        readonly List<Material> knifeMaterials = new();

        Weapon Current => weapons[current];
        public Skins.Skin CurrentSkin => current == KnifeSlot ? Skins.Knives[knifeSkin] : Skins.Snipers[sniperSkin];

        // True while something else (like opening a case) has the mouse
        public static bool InputBlocked;
        // True while a case reveal is on screen: the weapons are tucked away
        public static bool HideWeapons;

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

            // Sky reflections for shiny metal: a tiny probe that sees only the sky, following
            // the camera and refreshed now and then (the sky changes with the biome)
            probe = new GameObject("Weapon Reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(transform, false);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.cullingMask = 0;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.resolution = 64;
            probe.size = Vector3.one * 5000f;
            probe.importance = 10;
            probe.RenderProbe();
            SetupStudio();

            anchor = new GameObject("ViewModel").transform;
            anchor.SetParent(transform, false);

            knifeSkin = Skins.EquippedKnife;
            sniperSkin = Skins.EquippedSniper;
            weapons = new[]
            {
                new Weapon { speed = 250f, root = BuildKnifeRig(), drawTime = KnifeDrawTime, restPosition = Vector3.zero, restRotation = Quaternion.identity,
                    sway = 0.75f, swaySmooth = 0.06f, inertia = 0.6f, spring = 20f, holster = 0.12f, drawVisual = 0.42f, drawKeys = KnifeDrawKeys },
                new Weapon { speed = 200f, drawTime = 1.1f, root = BuildSniper(), restPosition = SniperRest, restRotation = SniperRestRotation,
                    sway = 1.25f, swaySmooth = 0.13f, inertia = 1.5f, spring = 12f, holster = 0.22f, drawVisual = 0.85f, drawKeys = SniperDrawKeys },
            };
            foreach (var w in weapons)
            {
                w.root.SetLocalPositionAndRotation(w.restPosition, w.restRotation);
                w.root.gameObject.SetActive(w == Current);
            }
            SetupSniper();
            gloveSkin = Skins.EquippedGlove;
            ApplyGloves();
        }

        static float HolsterLength(Weapon w) => w.holster; // a knife is gone in a flick, the rifle takes a moment

        static void ResetPose(Weapon w) => w.root.SetLocalPositionAndRotation(w.restPosition, w.restRotation);

        // Builds the viewmodel outside play mode too, so editor tools can photograph it
        public void BuildNow() => Awake();

        public void Equip(int slot)
        {
            if (weapons == null || slot == current) return;
            SetZoom(0, false);
            if (current == KnifeSlot) ResetKnifeRig();
            inspectTime = slashTime = sniperInspect = -1f;
            previous = current;
            current = slot;
            drawTime = Application.isPlaying ? 0f : 99f;
            if (Application.isPlaying)
            {
                if (holstering == null) { holstering = weapons[previous]; holsterTime = 0f; }
                else if (holstering == Current) { ResetPose(holstering); holstering = null; } // changed your mind mid-switch
            }
            foreach (var w in weapons) w.root.gameObject.SetActive(w == (holstering ?? Current));
            UpdateSheath();
            // Putting one away rustles; the draw sound comes when the next one comes up
            if (holstering != null) Play(WeaponSounds.Holster, 0.45f);
            else Play(WeaponSounds.Draw, 0.6f);
        }

        // Puts on a knife skin (index into Skins.Knives), remembers it, and pulls it out
        public void EquipKnifeSkin(int index)
        {
            if (weapons == null) return;
            knifeSkin = Mathf.Clamp(index, 0, Skins.Knives.Length - 1);
            Skins.EquippedKnife = knifeSkin;
            BuildKnifeModel();
            if (current == KnifeSlot) { drawTime = Application.isPlaying ? 0f : 99f; inspectTime = -1f; Play(WeaponSounds.Draw, 0.6f); }
            else Equip(KnifeSlot);
        }

        void OnDestroy()
        {
            StudioDestroy();
            RestoreView();
            foreach (var m in materials) Kill(m);
            foreach (var m in knifeMaterials) Kill(m);
            foreach (var m in rifleMaterials) Kill(m);
            Kill(dot);
            Kill(gloveTexture);
            foreach (var m in gloveMaterials) Kill(m);
            Kill(trailFade);
            Kill(sleeveTexture);
            Kill(scopeTexture);
        }

        static void Kill(Object o) => WeaponBuilder.Kill(o);

        void Play(AudioClip clip, float volume = 1f)
        {
            if (audioSource && clip) audioSource.PlayOneShot(clip, volume);
        }

        // The scope overlay, crosshair, then the weapon panel: bottom right, translucent,
        // weapon name over rarity and a hint. Drawn behind the rest of the HUD.
        void OnGUI()
        {
            if (weapons == null) return;
            GUI.depth = 10;
            DrawScope();
            if (zoom == 0) DrawCrosshair();
            // The weapon box only shows in the hall: once the run starts the screen stays clear
            if (!runTimer) runTimer = FindAnyObjectByType<RunTimer>();
            if (runTimer && runTimer.Running && !AutoInspect) return;
            var skin = CurrentSkin;
            Color color = Skins.RarityColor(skin.rarity);
            var box = WeaponBox;
            float px = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            UiArt.Rounded(box, new Color(0.02f, 0.02f, 0.05f, 0.62f), 12f * px);
            UiArt.Rounded(new Rect(box.xMax - 5f * px, box.y + 10f * px, 3f * px, box.height - 20f * px), color, 1.5f * px);
            UiArt.Text(new Rect(box.x, box.y + 8f * px, box.width - 20f * px, 26f * px), skin.rarity == SkinRarity.Default ? skin.name : "★ " + skin.name, Mathf.RoundToInt(18 * px), Color.white, TextAnchor.MiddleRight);
            string detail = current == SniperSlot ? SniperStatus() : "F inspect";
            if (AutoInspect) detail = "AUTO INSPECT  ·  double tap F to stop";
            string slot = current == SniperSlot ? "PRIMARY" : "SECONDARY";
            UiArt.Text(new Rect(box.x, box.y + 36f * px, box.width - 20f * px, 20f * px), $"{slot}   ·   {Skins.RarityName(skin.rarity)}   ·   {detail}", Mathf.RoundToInt(12 * px), color, TextAnchor.MiddleRight);
        }

        // Where the weapon box sits (bottom right), so the hall's help panel can keep clear of it
        public static Rect WeaponBox
        {
            get
            {
                float px = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f), w = 360f * px, h = 64f * px, m = 16f * px;
                return new Rect(Screen.width - w - m, Screen.height - h - m, w, h);
            }
        }

        [Header("Crosshair")]
        public Color crosshairDotColor = Color.white;
        public float crosshairSize = 5f; // dot diameter in pixels at 1080p

        Texture2D dot;
        float lastShot = -99f, hitMarkerTime = -99f;
        Color hitMarkerColor = Color.white;

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
            float px = Screen.height / 1080f;
            // The dot blooms for a moment when you fire, then pulls back in
            float bloom = 1f + 0.9f * Mathf.Max(0f, 1f - (Time.time - lastShot) / 0.2f);
            float d = Mathf.Max(3f, crosshairSize * px) * bloom;
            var center = new Vector2(Screen.width, Screen.height) * 0.5f;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(center.x - d * 0.5f - 1f, center.y - d * 0.5f - 1f, d + 2f, d + 2f), dot);
            GUI.color = GameSettings.CrosshairColor;
            GUI.DrawTexture(new Rect(center.x - d * 0.5f, center.y - d * 0.5f, d, d), dot);

            // Hit marker: an X that snaps out and fades when a shot breaks a target
            float hm = Time.time - hitMarkerTime;
            if (hm < 0.35f)
            {
                float a = 1f - hm / 0.35f, gap = Mathf.Max(5f, (8f + hm * 40f) * px), len = Mathf.Max(8f, 15f * px), th = Mathf.Max(2f, 3f * px);
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, center);
                GUI.color = new Color(hitMarkerColor.r, hitMarkerColor.g, hitMarkerColor.b, a);
                GUI.DrawTexture(new Rect(center.x + gap, center.y - th / 2f, len, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - gap - len, center.y - th / 2f, len, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - th / 2f, center.y + gap, th, len), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - th / 2f, center.y - gap - len, th, len), Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            GUI.color = old;
        }

        void Update()
        {
            UpdateStudio();
            anchor.gameObject.SetActive(!HideWeapons);
            if (sheath) sheath.gameObject.SetActive(!HideWeapons);
            var kb = InputBlocked ? null : Keyboard.current;
            var mouse = InputBlocked ? null : Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            float dt = Time.deltaTime;

            ApplyFov();
            probeTimer -= dt;
            if (probeTimer <= 0f && probe)
            {
                probeTimer = 3f;
                probe.RenderProbe();
            }

            // Weapon switching: 1 primary (sniper), 2 secondary (knife), Q last weapon, wheel toggles
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Equip(SniperSlot);
                else if (kb.digit2Key.wasPressedThisFrame) Equip(KnifeSlot);
                else if (kb.qKey.wasPressedThisFrame) Equip(previous);
            }
            if (locked && mouse != null && mouse.scroll.ReadValue().y != 0f) Equip(current == KnifeSlot ? SniperSlot : KnifeSlot);
            // Switching: drop the old weapon out of view (speeding up as it goes), then draw
            if (holstering != null)
            {
                holsterTime += dt;
                float h = Mathf.Clamp01(holsterTime / HolsterLength(holstering));
                float e = h * h;
                holstering.root.SetLocalPositionAndRotation(
                    holstering.restPosition + new Vector3(0f, -0.16f, -0.04f) * e,
                    holstering.restRotation * Quaternion.Euler(40f * e, 0f, 0f));
                if (h >= 1f)
                {
                    ResetPose(holstering);
                    holstering = null;
                    foreach (var w in weapons) w.root.gameObject.SetActive(w == Current);
                    Play(WeaponSounds.Draw, 0.6f);
                }
            }
            else drawTime += dt;

            // Sway: the weapon lags behind how fast you turn. Measured per second, not per
            // frame, and smoothed, so it looks the same at any frame rate and uneven frames
            // don't make it jitter. (`look` is in per-frame-at-60fps units, what it was tuned in.)
            Vector2 rawLook = locked && Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            lookRate = Vector2.Lerp(lookRate, dt > 0f ? rawLook / dt : Vector2.zero, 1f - Mathf.Exp(-30f * dt));
            Vector2 look = lookRate / 60f;
            Vector3 swayTarget = new Vector3(-look.x, -look.y, 0f) * 0.0006f * Current.sway;
            swayTarget = Vector3.ClampMagnitude(swayTarget, 0.04f);
            sway = Vector3.SmoothDamp(sway, swayTarget, ref swayVelocity, Current.swaySmooth);

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
            tilt = Vector3.Lerp(tilt, new Vector3(0f, 0f, -sideways * 6f + Mathf.Clamp(look.x * 0.05f, -4f, 4f)), 1f - Mathf.Exp(-8f * dt));

            // Inertia: every change in your velocity (landing, a ramp turning you, a wall) nudges
            // the weapon the other way on a critically damped spring, so it dips and settles
            // without wobbling. Kicks come from the velocity change itself, so they're the same
            // at any frame rate; it's small and capped so surfing at speed stays steady.
            if (player)
            {
                Vector3 v = player.Velocity;
                Vector3 dv = transform.InverseTransformDirection(v - lastVelocity);
                lastVelocity = v;
                if (dv.sqrMagnitude < 40f * 40f) inertiaVelocity -= dv * 0.05f * Current.inertia; // bigger jumps are teleports
            }
            float spring = Current.spring;
            for (float left = dt; left > 0f; left -= 1f / 120f)
            {
                float step = Mathf.Min(left, 1f / 120f);
                inertiaVelocity += (-spring * spring * inertia - 2f * spring * inertiaVelocity) * step;
                inertia = Vector3.ClampMagnitude(inertia + inertiaVelocity * step, 0.02f);
            }

            anchor.localPosition = sway + bob + inertia + Vector3.up * breathe;
            anchor.localRotation = Quaternion.Euler(tilt + new Vector3(Mathf.Clamp(look.y * 0.04f, -3f, 3f) - inertia.y * 120f, inertia.x * 60f, inertia.x * 90f)); // mouse tilt capped so fast flicks never throw the weapon around

            // Draw: the hand brings the weapon up into frame, a touch past where it rests, the
            // glove re-grips (a wrist roll and a squeeze), and it settles into its idle
            var weapon = Current;
            var (drawPos, drawRot) = current == KnifeSlot && knife.IsSword
                ? (Vector3.zero, Vector3.zero) // drawn from the sheath instead
                : Sample(weapon.drawKeys, drawTime / weapon.drawVisual);
            weapon.root.SetLocalPositionAndRotation(weapon.restPosition + drawPos, weapon.restRotation * Quaternion.Euler(drawRot));
            float drawn = drawTime / weapon.drawVisual;
            SqueezeGloves(weapon, drawn > 0.5f && drawn < 0.85f ? Mathf.Sin((drawn - 0.5f) / 0.35f * Mathf.PI) : 0f);
            bool ready = drawTime >= weapon.drawTime;

            UpdateSheath();
            if (current == KnifeSlot) UpdateKnife(kb, mouse, locked, ready, dt);
            else UpdateSniper(kb, mouse, locked, ready, dt);
            UpdateEffects(dt);
            UpdateGloves();

            if (player) player.maxSpeed = (current == SniperSlot && zoom > 0 ? 100f : weapon.speed) * PlayerMovement.SourceUnit;
        }

        // Keyframes: time, offset, rotation offset (degrees), smoothly blended
        // A diagonal cut in camera space: wind up high on the right, sweep down through the
        // middle to the lower left, hold the follow-through a moment, recover
        static readonly (float t, Vector3 pos, Vector3 rot)[] SlashKeys =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.07f, new Vector3(0.05f, 0.05f, -0.02f), new Vector3(-20f, 15f, -25f)),
            (0.17f, new Vector3(-0.15f, -0.07f, 0.07f), new Vector3(30f, -35f, 45f)),
            (0.26f, new Vector3(-0.13f, -0.08f, 0.05f), new Vector3(26f, -30f, 40f)),
            (0.45f, Vector3.zero, Vector3.zero),
        };
        float slashSide = -1f;

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

        Material glove, cuff, sleeve;
        Texture2D gloveTexture, sleeveTexture;

        // Sleeve fabric: soft light-and-dark mottling with a faint weave
        Texture2D SleeveTexture()
        {
            const int size = 64;
            sleeveTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.25f + ((x + y) % 4 == 0 ? 0.06f : 0f);
                float v = 0.85f + n;
                sleeveTexture.SetPixel(x, y, new Color(v, v, v));
            }
            sleeveTexture.Apply();
            return sleeveTexture;
        }

        // Black glove fabric with a faint lighter web of stitched seams and a little mottling
        Texture2D GloveTexture()
        {
            const int size = 128;
            gloveTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float n = Mathf.PerlinNoise(u * 6f, v * 6f);
                float web = Mathf.Min(Mathf.Abs(Mathf.Sin((u + v * 0.5f) * 14f)), Mathf.Abs(Mathf.Sin((u - v * 0.7f) * 11f + n * 2f)));
                Color c = new Color(0.075f, 0.075f, 0.08f) * (0.85f + n * 0.3f);
                if (web < 0.12f) c = Color.Lerp(new Color(0.2f, 0.2f, 0.22f), c, web / 0.12f);
                gloveTexture.SetPixel(x, y, c);
            }
            gloveTexture.Apply();
            return gloveTexture;
        }

        Transform BuildKnifeRig()
        {
            glove = Make(Color.white, 0.35f, 0f);
            glove.SetTexture("_BaseMap", GloveTexture());
            Resources.Load<ArmRig>("Arms/RightArm")?.DressGlove(glove, true); // the baked sport glove
            cuff = Make(new Color(0.05f, 0.05f, 0.055f), 0.35f, 0f);
            sleeve = Make(new Color(0.17f, 0.17f, 0.2f), 0.2f, 0f); // dark fabric sleeve
            sleeve.SetTexture("_BaseMap", SleeveTexture());

            var root = new GameObject("Knife Rig").transform;
            root.SetParent(anchor, false);
            rightHand = BuildBlockArm(root, "Right Arm");
            leftHand = BuildBlockArm(root, "Left Arm", Relaxed);
            SetupArmFade();
            hand = rightHand.root;
            leftHand.root.SetLocalPositionAndRotation(LeftIdle, LeftIdleRotation);
            BuildKnifeModel();
            return root;
        }

        BlockArm rightHand, leftHand;
        float knifeScale = 1f;
        WeaponParts offhand; // the left hand's blade, for twin blades
        Quaternion offhandRest = Quaternion.identity; // its turn in the left fist
        RunTimer runTimer;
        static readonly Vector3 KnifeHandle = new(0f, -0.055f, 0f);

        float KnifeDrawTime => knife.IsSword ? SwordDrawTime : IsVoidModel(knife.model) ? VoidDrawTime : 0.6f;

        void BuildKnifeModel()
        {
            ResetChain();
            if (knife != null) Kill(knife.root.gameObject);
            if (offhand != null) { Kill(offhand.root.gameObject); offhand = null; }
            foreach (var m in knifeMaterials) Kill(m);
            knifeMaterials.Clear();
            if (Skins.TalonHeld(Skins.Knives[knifeSkin])) SetTalonGrip(rightHand);
            else SetGrip(rightHand, false);
            var builder = new WeaponBuilder(template, Layer, false, knifeMaterials) { pairs = false };
            knife = builder.Knife(Skins.Knives[knifeSkin], rightHand.grip);
            // Every knife is sized like the talon knife (about 0.3 long), so none reaches across
            // the middle of the screen; it's scaled about its handle, so the grip stays put
            var extent = WeaponBuilder.MeshSize(knife);
            // (a Void blade by its length alone: a wide guard or a filled-out blade mustn't shrink it)
            float length = knife.model == KnifeModel.ModelBlade && !knife.talonLike ? extent.y : extent.magnitude;
            // (twin blades shorter still, so the pair doesn't cross in the middle of the screen)
            // (scythes a little longer and smaller, so the curved head shows above the hand)
            // (Void swords a little longer than a knife, so they read as swords; Void knives a
            // knife's length)
            bool twin = knife.model == KnifeModel.ModelDual, scythe = knife.model == KnifeModel.ModelScythe;
            string asset = Skins.Knives[knifeSkin].asset;
            float wanted = twin ? 0.27f : scythe ? 0.38f : asset == null ? 0.3f : Skins.IsVoidKnife(asset) ? 0.25f : 0.34f;
            knifeScale = length > 0.01f ? Mathf.Clamp(wanted / length, twin || scythe || asset != null ? 0.3f : 0.6f, 1f) : 1f;
            // Twin blades: the second one in the left fist, sized and held the same way
            if (knife.model == KnifeModel.ModelDual)
            {
                offhand = builder.Knife(Skins.Knives[knifeSkin], leftHand.grip);
                offhand.root.localScale = Vector3.one * knifeScale;
                offhand.root.localPosition = (1f - knifeScale) * KnifeHandle;
            }
            BuildSheath(Skins.Knives[knifeSkin], builder);
            if (weapons != null) weapons[KnifeSlot].drawTime = KnifeDrawTime;
            UpdateSheath();
            PoseKnife(Vector3.zero, Vector3.zero, -1f);
            if (offhand != null) AimOffhand();
        }

        // Turns the left hand's blade, about the middle of the fist, into the mirror image of the
        // right one at rest (the left fist is posed as the right's mirror image, but its grip
        // frame isn't); it keeps that turn in the fist from then on
        void AimOffhand()
        {
            Vector3 Mirror(Vector3 world)
            {
                var v = transform.InverseTransformDirection(world);
                return transform.TransformDirection(new Vector3(-v.x, v.y, v.z));
            }
            Quaternion want = Quaternion.LookRotation(Mirror(knife.root.forward), Mirror(knife.root.up));
            offhandRest = Quaternion.Inverse(leftHand.grip.rotation) * want;
            offhand.root.SetLocalPositionAndRotation(KnifeHandle - offhandRest * (knifeScale * KnifeHandle), offhandRest);
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
    }
}
