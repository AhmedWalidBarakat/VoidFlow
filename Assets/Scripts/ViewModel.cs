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
        [Tooltip("Field of view of the world, horizontal like CS (the same on any screen shape)")]
        public float horizontalFov = 120f;
        [Tooltip("Field of view of the hands and weapon (vertical), kept separate so they never stretch")]
        public float fieldOfView = 58f;
        [Tooltip("Optional: a clip to use for the sniper shot instead of the generated one")]
        public AudioClip sniperShotClip;

        const int Layer = 30; // drawn only by the viewmodel camera
        public const int KnifeSlot = 0, SniperSlot = 1;

        class Weapon
        {
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

        Camera view, overlay;
        AudioSource audioSource;
        ReflectionProbe probe;
        float probeTimer;
        Transform anchor, hand;
        Texture2D panel;
        GUIStyle nameStyle, detailStyle;
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

            anchor = new GameObject("ViewModel").transform;
            anchor.SetParent(transform, false);

            knifeSkin = Skins.EquippedKnife;
            sniperSkin = Skins.EquippedSniper;
            weapons = new[]
            {
                new Weapon { speed = 250f, root = BuildKnifeRig(), drawTime = knife.IsSword ? SwordDrawTime : 0.6f, restPosition = Vector3.zero, restRotation = Quaternion.identity },
                new Weapon { speed = 200f, drawTime = 1.1f, root = BuildSniper(), restPosition = SniperRest, restRotation = SniperRestRotation },
            };
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
            UpdateSheath();
            Play(WeaponSounds.Draw, 0.6f);
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
            RestoreView();
            foreach (var m in materials) Kill(m);
            foreach (var m in knifeMaterials) Kill(m);
            foreach (var m in rifleMaterials) Kill(m);
            Kill(panel);
            Kill(dot);
            Kill(gloveTexture);
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
            if (!panel)
            {
                panel = new Texture2D(1, 1);
                panel.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.04f, 0.45f));
                panel.Apply();
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleRight };
            }
            var skin = CurrentSkin;
            Color color = Skins.RarityColor(skin.rarity);
            const float w = 260f, h = 58f, margin = 16f;
            var box = new Rect(Screen.width - w - margin, Screen.height - h - margin, w, h);
            GUI.DrawTexture(box, panel);
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(box.xMax - 4f, box.y, 4f, box.height), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(box.x, box.y + 6f, w - 14f, 24f), skin.rarity == SkinRarity.Default ? skin.name : "★ " + skin.name, nameStyle);
            detailStyle.normal.textColor = color;
            string detail = current == SniperSlot ? SniperStatus() : "F inspect";
            GUI.Label(new Rect(box.x, box.y + 30f, w - 14f, 20f), $"{Skins.RarityName(skin.rarity)}   ·   {detail}", detailStyle);
        }

        [Header("Crosshair")]
        public Color crosshairDotColor = Color.white;
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
            GUI.color = crosshairDotColor;
            GUI.DrawTexture(new Rect(center.x - d * 0.5f, center.y - d * 0.5f, d, d), dot);
            GUI.color = old;
        }

        void Update()
        {
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

            // Weapon switching: 1 knife, 2 sniper, Q last weapon, wheel toggles
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Equip(KnifeSlot);
                else if (kb.digit2Key.wasPressedThisFrame) Equip(SniperSlot);
                else if (kb.qKey.wasPressedThisFrame) Equip(previous);
            }
            if (locked && mouse != null && mouse.scroll.ReadValue().y != 0f) Equip(current == KnifeSlot ? SniperSlot : KnifeSlot);
            drawTime += dt;

            // Sway: the weapon lags behind the mouse and springs back
            Vector2 look = locked && Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
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
            if (current == KnifeSlot && knife.IsSword) up = 1f; // drawn from the sheath instead
            weapon.root.SetLocalPositionAndRotation(
                weapon.restPosition + new Vector3(0f, -0.16f, -0.04f) * (1f - up),
                weapon.restRotation * Quaternion.Euler(40f * (1f - up), 0f, 0f));
            bool ready = drawTime >= weapon.drawTime;

            UpdateSheath();
            if (current == KnifeSlot) UpdateKnife(kb, mouse, locked, ready, dt);
            else UpdateSniper(kb, mouse, locked, ready, dt);
            UpdateEffects(dt);

            if (player) player.maxSpeed = (current == SniperSlot && zoom > 0 ? 100f : weapon.speed) * PlayerMovement.SourceUnit;
        }

        void UpdateKnife(Keyboard kb, Mouse mouse, bool locked, bool ready, float dt)
        {
            if (kb != null && kb.fKey.wasPressedThisFrame && inspectTime < 0f) { inspectTime = 0f; talonSpin = 0f; talonLower = -1f; }
            if (ready && locked && mouse != null && mouse.leftButton.wasPressedThisFrame && (slashTime < 0f || slashTime > 0.3f))
            {
                slashTime = 0f;
                slashSide = -slashSide;
                inspectTime = -1f;
                talonSpin = 0f;
                talonLower = -1f;
                Play(WeaponSounds.Slash, 0.7f);
            }

            // Inspect and slash are keyframed offsets on the hand; the knife model adds its
            // own moves (ring spin, butterfly flip, Void glow burst)
            var (pos, rot) = (Vector3.zero, Vector3.zero);
            if (slashTime >= 0f)
            {
                slashTime += dt;
                (pos, rot) = Sample(SlashKeys, slashTime);
                // Every other swing mirrors, so cuts alternate right-to-left and left-to-right;
                // swords swing wider
                float reach = knife.IsSword ? 1.3f : 1f;
                pos = new Vector3(pos.x * slashSide, pos.y, pos.z) * reach;
                rot = new Vector3(rot.x, rot.y * slashSide, rot.z * slashSide) * reach;
                if (slashTime > SlashKeys[^1].t) slashTime = -1f;
            }
            else if (inspectTime >= 0f && knife.model == KnifeModel.Talon)
                UpdateTalonSpin(kb != null && kb.fKey.isPressed, dt);
            else if (inspectTime >= 0f)
            {
                inspectTime += dt;
                if (inspectTime > knife.InspectLength) inspectTime = -1f;
            }
            PoseKnife(pos, rot, inspectTime);
        }

        // Talon knife inspect, CS style: the hand comes up and the knife spins around the
        // finger ring for as long as inspect is held. Let go and it finishes the turn it's on
        // (at least one full turn), stops cleanly and the hand goes back down.
        const float TalonSpinSpeed = 1080f, TalonSpinStart = 0.35f, TalonLowerTime = 0.35f;
        float talonSpin, talonLower = -1f;

        void UpdateTalonSpin(bool held, float dt)
        {
            inspectTime += dt;
            if (talonLower >= 0f)
            {
                talonLower += dt;
                if (talonLower > TalonLowerTime) { inspectTime = -1f; talonSpin = 0f; talonLower = -1f; }
                return;
            }
            if (inspectTime < TalonSpinStart) return;
            // Spin up quickly over the first half turn, then full speed
            float speed = TalonSpinSpeed * Mathf.Clamp01(0.3f + talonSpin / 180f);
            float stopAt = held ? float.MaxValue : Mathf.Max(360f, Mathf.Ceil(talonSpin / 360f) * 360f);
            talonSpin = Mathf.Min(talonSpin + speed * dt, stopAt);
            if (talonSpin >= stopAt) talonLower = 0f;
        }

        // How far the hand is raised for an inspect (0 down, 1 up)
        float InspectRaise(float inspect)
        {
            if (inspect < 0f) return 0f;
            if (knife.model == KnifeModel.Talon)
                return Ease(inspect, 0f, 0.3f) * (talonLower >= 0f ? 1f - Ease(talonLower, 0f, TalonLowerTime) : 1f);
            float length = knife.InspectLength;
            return knife.IsSword ? Plateau(inspect, 0f, 0.35f, length - 0.5f, length) : Plateau(inspect, 0f, 0.3f, length - 0.35f, length);
        }

        // Knife arms in camera space: two block arms coming in from the bottom corners, the
        // left glove empty, the right one holding the knife (a talon knife comes out the far
        // side and curls up to the right)
        static readonly Vector3 RightIdle = new(0.11f, -0.1f, 0.3f);
        static readonly Vector3 LeftIdle = new(-0.125f, -0.105f, 0.31f);
        static readonly Quaternion ReverseIdle = FingersBack(new Vector3(-0.3f, 0.5f, 1f), new Vector3(0.1f, 0.6f, -0.7f));
        static readonly Quaternion ForwardIdle = FingersBack(new Vector3(-0.3f, 0.5f, 1f), new Vector3(0.6f, 0.5f, -0.6f));
        static readonly Quaternion LeftIdleRotation = FingersBack(new Vector3(0.3f, 0.5f, 1f), new Vector3(-0.1f, 0.6f, -0.7f));
        // Inspect, like CS2: the hand comes up to the middle and turns upright with the palm
        // toward you, turns slowly to show the knife off, then goes back down, while the left
        // hand drops away. A forward-grip blade stands up out of the fist; the talon knife rolls
        // in the fingers so it hangs below, curling out to the left.
        static readonly Vector3 InspectSpot = new(0.06f, -0.035f, 0.33f);
        static readonly Quaternion InspectRotation = FingersBack(new Vector3(-1f, 0f, 0.3f), new Vector3(0.3f, 0f, 1f));
        static readonly Vector3 LeftHandAway = new(-0.04f, -0.14f, -0.05f);
        // For inspects the hand turns its palm toward you, fingers up, and the knife moves
        // onto the palm side and spins flat in front of it, facing you. The glove and sleeve
        // stay behind the spin, so nothing ever passes through them. Swords go further out
        // and dead center so their big spin circles the middle of the screen.
        static readonly Quaternion PalmRotation = FingersBack(new Vector3(-0.25f, 1f, 0f), new Vector3(0.15f, 0f, 1f));
        static readonly Vector3 TrickSpot = new(0.045f, -0.06f, 0.46f);
        static readonly Vector3 SwordInspectSpot = new(0.03f, -0.035f, 0.6f);

        // Places the hands for the current knife: the resting hold plus the slash offset
        // (camera space pos, rot), blended into the inspect pose while inspecting (inspect >= 0)
        void PoseKnife(Vector3 pos, Vector3 rot, float inspect)
        {
            bool reverse = knife.model == KnifeModel.Talon;
            if (PoseSwordDraw()) { knife.Animate(Application.isPlaying ? Time.time : 0f, -1f); return; }
            bool sword = knife.IsSword;
            float w = InspectRaise(inspect);
            knife.ringSpin = talonSpin;
            Quaternion show = PalmRotation * InspectMove(inspect);
            hand.localPosition = Vector3.Lerp(RightIdle, sword ? SwordInspectSpot : TrickSpot, w) + pos + InspectBob(inspect);
            hand.localRotation = Quaternion.Euler(rot) * Quaternion.Slerp(reverse ? ReverseIdle : ForwardIdle, show, w);
            // The knife moves from its normal hold onto the palm side for the show
            SetGrip(rightHand, reverse);
            if (w > 0f)
            {
                var (palmPos, palmRot) = PalmGrip(knife);
                rightHand.grip.SetLocalPositionAndRotation(
                    Vector3.Lerp(rightHand.grip.localPosition, palmPos, w), Quaternion.Slerp(rightHand.grip.localRotation, palmRot, w));
            }
            // The left arm dips out of the way of inspects and of cuts that cross the body
            float slashAway = slashTime >= 0f && slashSide < 0f ? Plateau(slashTime, 0f, 0.1f, 0.3f, 0.45f) : 0f;
            leftHand.root.localPosition = LeftIdle + LeftHandAway * Mathf.Max(w, slashAway);
            // During a sword inspect the left arm disappears so it never blocks the show
            leftHand.root.gameObject.SetActive(!(knife.IsSword && inspect >= 0f));
            knife.Animate(Application.isPlaying ? Time.time : 0f, inspect, current == KnifeSlot ? drawTime : 99f);
        }

        // Each kind of knife shows itself off its own way, on top of the raised hand (hand
        // space: X runs along a forward-grip blade, Z out of the back of the hand)
        Quaternion InspectMove(float t)
        {
            if (t < 0f) return Quaternion.identity;
            switch (knife.model)
            {
                case KnifeModel.Talon:
                    // The wrist rocks a little with each turn of the knife (the spin itself is
                    // in WeaponParts)
                    return Quaternion.Euler(0f, 0f, Mathf.Sin(talonSpin * Mathf.Deg2Rad) * 4f);
                case KnifeModel.Butterfly:
                    // Counter Blox style flips: the wrist rocks with every flip
                    float flips = Plateau(t, 0.3f, 0.45f, 2f, 2.2f);
                    return Quaternion.Euler(0f, 0f, Mathf.Sin((t - 0.35f) * Mathf.PI / WeaponParts.FlipPeriod) * 9f * flips);
                default:
                    // Swords: a blade-up salute in the middle of the screen with the flat toward
                    // you, one slow even sway left and right while the edge flares, then two
                    // full spins flat in front of the palm (see WeaponParts), then still again
                    // before lowering
                    float sway = Mathf.Sin(Ease(t, 0.35f, 0.95f) * Mathf.PI * 2f) * 7f;
                    return Quaternion.Euler(0f, 0f, sway);
            }
        }

        // The butterfly arm sways side to side and dips with each flip while it shows off
        Vector3 InspectBob(float t)
        {
            if (t < 0f || knife.model != KnifeModel.Butterfly) return Vector3.zero;
            float active = Plateau(t, 0.3f, 0.45f, 2f, 2.2f);
            float beat = (t - 0.35f) * Mathf.PI / WeaponParts.FlipPeriod;
            return new Vector3(Mathf.Sin(beat * 0.5f) * 0.02f, -Mathf.Abs(Mathf.Sin(beat)) * 0.01f, 0f) * active;
        }

        static float Ease(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        // Rises from a to b, holds until c, falls back by d
        static float Plateau(float t, float a, float b, float c, float d) =>
            t < c ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t)) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c, d, t));

        // Shows a moment of the knife inspect in edit mode, for photos (negative: at rest)
        public void PreviewKnifeInspect(float time)
        {
            if (weapons == null) return;
            talonSpin = Mathf.Max(0f, time - TalonSpinStart) * TalonSpinSpeed;
            talonLower = -1f;
            PoseKnife(Vector3.zero, Vector3.zero, time);
            talonSpin = 0f;
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
            cuff = Make(new Color(0.05f, 0.05f, 0.055f), 0.35f, 0f);
            sleeve = Make(new Color(0.17f, 0.17f, 0.2f), 0.2f, 0f); // dark fabric sleeve
            sleeve.SetTexture("_BaseMap", SleeveTexture());

            var root = new GameObject("Knife Rig").transform;
            root.SetParent(anchor, false);
            rightHand = BuildBlockArm(root, "Right Arm");
            leftHand = BuildBlockArm(root, "Left Arm");
            hand = rightHand.root;
            leftHand.root.SetLocalPositionAndRotation(LeftIdle, LeftIdleRotation);
            BuildKnifeModel();
            return root;
        }

        BlockArm rightHand, leftHand;

        void BuildKnifeModel()
        {
            if (knife != null) Kill(knife.root.gameObject);
            foreach (var m in knifeMaterials) Kill(m);
            knifeMaterials.Clear();
            SetGrip(rightHand, Skins.Knives[knifeSkin].model == KnifeModel.Talon);
            var builder = new WeaponBuilder(template, Layer, false, knifeMaterials);
            knife = builder.Knife(Skins.Knives[knifeSkin], rightHand.grip);
            BuildSheath(Skins.Knives[knifeSkin], builder);
            if (weapons != null) weapons[KnifeSlot].drawTime = knife.IsSword ? SwordDrawTime : 0.6f;
            UpdateSheath();
            PoseKnife(Vector3.zero, Vector3.zero, -1f);
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
