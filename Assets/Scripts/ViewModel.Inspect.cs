using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace VoidFlow
{
    // Knife handling: the resting hold, slashes, and each knife's inspect. Inspects are
    // keyframed sequences (copied from reference clips of a Roblox block-arm game): the knife
    // can be passed between hands, tossed into the air and caught, spun around the arm, or
    // drawn from and returned to its sheath. The blade leaves a white trail, and wherever the
    // knife passes behind an arm, that arm turns see-through so the knife always reads.
    //
    // Holding F keeps the "show" part going (the talon keeps spinning, the butterfly keeps
    // circling the arm, the sword stays sheathed in the samurai hold); holding it through the
    // end starts the routine again.
    public partial class ViewModel
    {
        [Tooltip("A transparent URP Lit material; see-through copies of the arms are made from it")]
        public Material fadeTemplate;

        // Resting poses in camera space: two block arms coming in from the bottom corners, the
        // left glove empty, the right one holding the knife (a talon knife comes out the far
        // side and curls up to the right)
        static readonly Vector3 RightIdle = new(0.11f, -0.1f, 0.3f);
        static readonly Vector3 LeftIdle = new(-0.125f, -0.105f, 0.31f);
        static readonly Quaternion ReverseIdle = FingersBack(new Vector3(-0.3f, 0.5f, 1f), new Vector3(0.1f, 0.6f, -0.7f));
        static readonly Quaternion ForwardIdle = FingersBack(new Vector3(-0.3f, 0.5f, 1f), new Vector3(0.6f, 0.5f, -0.6f));
        static readonly Quaternion LeftIdleRotation = FingersBack(new Vector3(0.3f, 0.5f, 1f), new Vector3(-0.1f, 0.6f, -0.7f));
        static readonly Vector3 LeftHandAway = new(-0.04f, -0.14f, -0.05f);

        // The sheath still on the left hip but brought up a little into view for the sword
        // routine (its +Y runs from the mouth back into the scabbard), and how the left hand
        // holds it at the mouth
        static readonly Vector3 AcrossMouth = new(-0.1f, -0.105f, 0.36f);
        static readonly Quaternion AcrossSheath = FingersBack(new Vector3(-0.35f, -0.3f, -0.9f), new Vector3(1f, 0f, -0.35f));
        static readonly Vector3 FromTopRight = new Vector3(-0.5f, -0.7f, 0.5f).normalized;
        static readonly Quaternion LeftOnSheath = FingersBack(new Vector3(0.25f, 0.6f, 0.8f), new Vector3(-0.2f, 0.7f, -0.6f));

        static float Ease(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        // Rises from a to b, holds until c, falls back by d
        static float Plateau(float t, float a, float b, float c, float d) =>
            t < c ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t)) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c, d, t));

        static Quaternion FB(float fx, float fy, float fz, float bx, float by, float bz) =>
            FingersBack(new Vector3(fx, fy, fz), new Vector3(bx, by, bz));

        // ------------------------------------------------------------------ sequences

        enum Hold { Right, Left, Air, Sheath }

        // One moment of a routine. Values carry over from the previous key unless changed.
        class Key
        {
            public float t;
            public Vector3 rp, lp, ap;     // right arm, left arm, and (in the air) the knife's pivot
            public Quaternion rq, lq, aq;
            public Hold hold;
            public Vector3 spin;           // knife turned about its own axes (degrees, around its pivot)
            public float orbit;            // knife carried around the right arm (degrees)
            public float flips;            // butterfly knife flips done
            public float slide;            // sword pulled this far out of the sheath
            public float across;           // sheath at the hip (0) or laid across the view (1)
            public bool rightOnHilt, leftOnSheath;
            public bool fromAbove;         // on the hilt, the arm reaches in from the top right
            public Key At(float time) { var k = (Key)MemberwiseClone(); k.t = time; return k; }
        }

        class Routine
        {
            public Key[] keys;
            public float sustainAt = -1f; // held F pauses the routine here...
            public int sustainAxis;       // ...and keeps spinning: 0 nothing, 1 the knife, 2 around the arm
            public float sustainSpeed;
            public (float t, AudioClip clip, float volume)[] sounds = System.Array.Empty<(float, AudioClip, float)>();
            public float Length => keys[^1].t;
        }

        static Key IdleKey(bool reverse) => new()
        {
            rp = RightIdle, rq = reverse ? ReverseIdle : ForwardIdle,
            lp = LeftIdle, lq = LeftIdleRotation,
            aq = Quaternion.identity,
        };

        // Arms thrown wide apart for a throw, and raised to catch
        static readonly Vector3 RightWide = new(0.175f, -0.03f, 0.3f), LeftWide = new(-0.175f, -0.035f, 0.31f);
        static readonly Quaternion RightUp = FB(-0.3f, 0.8f, 0.6f, 0.3f, 0.3f, -1f), LeftUp = FB(0.3f, 0.8f, 0.6f, -0.3f, 0.3f, -1f);

        // Talon knife: up to the right spinning on the ring, thrown across to the left hand
        // with the arms flung wide, spun up there, thrown high and caught by the right, spun
        // off the end of a level arm, home
        static Routine TalonRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(true); ks.Add(k);
            k = k.At(0.3f); k.rp = new(0.09f, -0.03f, 0.34f); k.rq = FB(-0.2f, 0.9f, 0.4f, 0.1f, 0.2f, -1f); k.spin = new(0f, 0f, -180f); ks.Add(k);
            k = k.At(0.75f); k.rp = new(0.12f, -0.035f, 0.33f); k.spin = new(0f, 0f, -1080f); ks.Add(k);
            k = k.At(1.0f); k.hold = Hold.Air; k.ap = new(0f, 0.06f, 0.4f); k.aq = Quaternion.Euler(0f, 0f, 20f); k.spin = new(-360f, 0f, -1260f);
            k.rp = RightWide; k.rq = RightUp; k.lp = LeftWide; k.lq = LeftUp; ks.Add(k);
            k = k.At(1.25f); k.hold = Hold.Left; k.lp = new(-0.13f, -0.01f, 0.33f); k.spin = new(-720f, 0f, -1440f); ks.Add(k);
            k = k.At(1.55f); k.lp = new(-0.1f, 0.01f, 0.33f); k.lq = FB(0.3f, 0.9f, 0.4f, -0.1f, 0.2f, -1f);
            k.rp = RightWide; k.spin = new(-720f, 0f, -2160f); ks.Add(k);
            k = k.At(1.85f); k.hold = Hold.Air; k.ap = new(-0.01f, 0.12f, 0.43f); k.aq = Quaternion.Euler(0f, 0f, 30f); k.spin = new(-1260f, 0f, -2160f);
            k.lp = LeftWide; k.lq = LeftUp; ks.Add(k);
            k = k.At(2.1f); k.ap = new(0.05f, 0.06f, 0.39f); k.spin = new(-1620f, 0f, -2160f); ks.Add(k);
            k = k.At(2.3f); k.hold = Hold.Right; k.rp = new(0.12f, -0.01f, 0.33f); k.rq = RightUp; k.spin = new(-1800f, 0f, -2160f);
            k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            k = k.At(2.75f); k.rp = new(0.06f, -0.05f, 0.35f); k.rq = FB(-1f, 0.15f, 0.35f, 0f, 1f, -0.2f); k.spin = new(-1800f, 0f, -2880f); ks.Add(k);
            k = k.At(3.15f); k.rp = new(0.1f, -0.07f, 0.31f); k.spin = new(-1800f, 0f, -3240f); ks.Add(k);
            k = k.At(3.55f); k.rp = RightIdle; k.rq = ReverseIdle; ks.Add(k);
            return new Routine
            {
                keys = ks.ToArray(), sustainAt = 0.55f, sustainAxis = 1, sustainSpeed = -1080f,
                sounds = new[] { (0.85f, WeaponSounds.Slash, 0.35f), (1.75f, WeaponSounds.Slash, 0.35f), (2.5f, WeaponSounds.Slash, 0.35f) },
            };
        }

        // Butterfly knife: flipped up, thrown across to the left hand with the arms flung wide,
        // flipped there, thrown high and caught by the right, carried in circles around the
        // right arm while flipping, a last throw, home
        static Routine ButterflyRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.3f); k.rp = new(0.08f, -0.02f, 0.33f); k.rq = FB(-0.2f, 0.8f, 0.6f, 0.3f, 0.2f, -1f); k.flips = 2f; ks.Add(k);
            k = k.At(0.55f); k.hold = Hold.Air; k.ap = new(0f, 0.08f, 0.4f); k.aq = Quaternion.identity; k.spin = new(0f, 0f, 300f);
            k.rp = RightWide; k.rq = RightUp; k.lp = LeftWide; k.lq = LeftUp; ks.Add(k);
            k = k.At(0.8f); k.hold = Hold.Left; k.lp = new(-0.13f, -0.01f, 0.33f); k.spin = new(0f, 0f, 720f); ks.Add(k);
            k = k.At(1.1f); k.lp = new(-0.12f, 0f, 0.34f); k.flips = 4f; ks.Add(k);
            k = k.At(1.4f); k.hold = Hold.Air; k.ap = new(0f, 0.12f, 0.44f); k.spin = new(360f, 0f, 720f); k.lp = LeftWide; ks.Add(k);
            k = k.At(1.75f); k.hold = Hold.Right; k.rp = new(0.12f, -0.01f, 0.33f); k.spin = new(720f, 0f, 720f); k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            k = k.At(2.1f); k.rp = new(0.07f, -0.03f, 0.34f); ks.Add(k);
            k = k.At(2.9f); k.orbit = 720f; k.flips = 6f; k.rp = new(0.08f, -0.02f, 0.34f); ks.Add(k);
            k = k.At(3.15f); k.hold = Hold.Air; k.ap = new(0.03f, 0.08f, 0.39f); k.aq = Quaternion.Euler(0f, 0f, -20f); k.spin = new(900f, 0f, 720f);
            k.rp = RightWide; k.lp = LeftWide; k.lq = LeftUp; ks.Add(k);
            k = k.At(3.45f); k.hold = Hold.Right; k.rp = new(0.12f, -0.02f, 0.33f); k.spin = new(1080f, 0f, 720f); k.flips = 8f; k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            k = k.At(3.9f); k.rp = RightIdle; k.rq = ForwardIdle; ks.Add(k);
            return new Routine
            {
                keys = ks.ToArray(), sustainAt = 2.5f, sustainAxis = 2, sustainSpeed = 900f,
                sounds = new[] { (0.45f, WeaponSounds.Slash, 0.35f), (1.3f, WeaponSounds.Slash, 0.35f), (3.05f, WeaponSounds.Slash, 0.3f) },
            };
        }

        // Sword: slid into the sheath at the left hip, then the right arm reaches down from the
        // top right to the hilt and eases the blade just a little way out of the sheath, and
        // holds it there, ready, for as long as F is held; then it's drawn back out
        static Routine SwordRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.3f); k.hold = Hold.Sheath; k.across = 1f; k.slide = 0.27f; k.rightOnHilt = true; k.fromAbove = true; k.leftOnSheath = true; ks.Add(k);
            k = k.At(0.6f); k.slide = 0f; ks.Add(k);
            k = k.At(0.85f); k.slide = 0.045f; ks.Add(k);
            k = k.At(1.05f); ks.Add(k);
            k = k.At(1.35f); k.slide = 0.27f; ks.Add(k);
            k = k.At(1.75f); k.hold = Hold.Right; k.rightOnHilt = false; k.fromAbove = false; k.leftOnSheath = false; k.across = 0f;
            k.rp = RightIdle; k.rq = ForwardIdle; k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            return new Routine
            {
                keys = ks.ToArray(), sustainAt = 0.95f,
                sounds = new[] { (0.58f, WeaponSounds.Sheathe, 0.8f), (0.8f, WeaponSounds.Tick, 0.6f), (1.15f, WeaponSounds.Unsheathe, 0.8f) },
            };
        }

        Routine routine;
        float sustainExtra;
        bool sustaining, sustainDone;

        Routine RoutineFor(WeaponParts parts) =>
            parts.model == KnifeModel.Talon ? TalonRoutine() : parts.model == KnifeModel.Butterfly ? ButterflyRoutine() : SwordRoutine();

        // ------------------------------------------------------------------ update

        void UpdateKnife(Keyboard kb, Mouse mouse, bool locked, bool ready, float dt)
        {
            bool held = kb != null && kb.fKey.isPressed;
            if (kb != null && kb.fKey.wasPressedThisFrame && inspectTime < 0f && ready) StartInspect();
            if (ready && locked && mouse != null && mouse.leftButton.wasPressedThisFrame && (slashTime < 0f || slashTime > 0.3f))
            {
                slashTime = 0f;
                slashSide = -slashSide;
                StopInspect();
                Play(WeaponSounds.Slash, 0.7f);
            }

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
            else if (inspectTime >= 0f) AdvanceRoutine(held, dt);
            PoseKnife(pos, rot, inspectTime);
            UpdateTrail(inspectTime >= 0f || slashTime >= 0f);
        }

        void AdvanceRoutine(bool held, float dt)
        {
            routine ??= RoutineFor(knife);
            if (sustaining)
            {
                float speed = Mathf.Abs(routine.sustainSpeed);
                if (held) sustainExtra += speed * dt;
                else if (speed > 0f && sustainExtra % 360f > 0.01f)
                    sustainExtra = Mathf.Min(sustainExtra + speed * dt, Mathf.Ceil(sustainExtra / 360f) * 360f);
                else { sustaining = false; sustainDone = true; }
                return;
            }
            float before = inspectTime;
            inspectTime += dt;
            if (!sustainDone && held && routine.sustainAt >= 0f && before <= routine.sustainAt && inspectTime >= routine.sustainAt)
            {
                inspectTime = routine.sustainAt;
                sustaining = true;
            }
            foreach (var (t, clip, volume) in routine.sounds)
                if (before < t && inspectTime >= t) Play(clip, volume);
            if (inspectTime > routine.Length)
            {
                if (held) StartInspect();
                else StopInspect();
            }
        }

        void StartInspect()
        {
            inspectTime = 0f;
            routine = RoutineFor(knife);
            sustainExtra = 0f;
            sustaining = sustainDone = false;
        }

        void StopInspect()
        {
            inspectTime = -1f;
            sustaining = false;
            sustainExtra = 0f;
        }

        // Puts the knife, sheath and arms straight back to rest (when switching weapons)
        void ResetKnifeRig()
        {
            if (knife == null || rightHand == null) return;
            StopInspect();
            slashTime = -1f;
            PoseKnife(Vector3.zero, Vector3.zero, -1f);
            trailPoints.Clear();
            if (trail) trail.enabled = false;
        }

        // ------------------------------------------------------------------ posing

        // Places the arms and knife: the resting hold plus the slash offset (camera space pos,
        // rot), or the inspect routine at `inspect` seconds
        void PoseKnife(Vector3 pos, Vector3 rot, float inspect)
        {
            bool reverse = knife.model == KnifeModel.Talon;
            SetGrip(rightHand, reverse);
            SetGrip(leftHand, reverse);
            float time = Application.isPlaying ? Time.time : 0f;

            if (inspect >= 0f)
            {
                PlayRoutine(inspect);
                return;
            }

            // Not inspecting: the knife is back in the right hand, everything solid
            if (knife.root.parent != rightHand.grip)
            {
                knife.root.SetParent(rightHand.grip, false);
                knife.root.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                UpdateSheath();
            }
            if (sheath) sheath.SetLocalPositionAndRotation(SheathMouth, SheathRotation);
            leftHand.root.gameObject.SetActive(true);
            SetLeftAlpha(1f);
            if (PoseSwordDraw())
            {
                SetArmAlpha(0.35f);
                knife.Animate(time, -1f);
                return;
            }
            hand.localPosition = RightIdle + pos;
            hand.localRotation = Quaternion.Euler(rot) * (reverse ? ReverseIdle : ForwardIdle);
            SetArmAlpha(1f);
            // The left arm dips out of the way of cuts that cross the body
            float slashAway = slashTime >= 0f && slashSide < 0f ? Plateau(slashTime, 0f, 0.1f, 0.3f, 0.45f) : 0f;
            leftHand.root.SetLocalPositionAndRotation(LeftIdle + LeftHandAway * slashAway, LeftIdleRotation);
            knife.ringSpin = 0f;
            knife.Animate(time, -1f, current == KnifeSlot ? drawTime : 99f);
        }

        struct Pose
        {
            public Vector3 p;
            public Quaternion q;
            public Pose(Vector3 p, Quaternion q) { this.p = p; this.q = q; }
            public Pose Then(Vector3 lp, Quaternion lq) => new(p + q * lp, q * lq);
            public Pose Then(Transform local) => Then(local.localPosition, local.localRotation);
            public static Pose Blend(Pose a, Pose b, float s) => new(Vector3.Lerp(a.p, b.p, s), Quaternion.Slerp(a.q, b.q, s));
        }

        Vector3 KnifePivot => knife.model switch
        {
            KnifeModel.Talon => knife.ringCenter,
            KnifeModel.Butterfly => Vector3.zero,
            _ => new Vector3(0f, -0.055f, 0f),
        };

        void PlayRoutine(float t)
        {
            routine ??= RoutineFor(knife);
            var keys = routine.keys;
            int i = 1;
            while (i < keys.Length - 1 && t > keys[i].t) i++;
            Key a = keys[i - 1], b = keys[i];
            float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.t, b.t, t));
            Transform rig = hand.parent;
            float time = Application.isPlaying ? Time.time : 0f;

            // Sheath: at the hip or laid across the view
            Pose sheathPose = default;
            if (sheath)
            {
                float across = Mathf.Lerp(a.across, b.across, s);
                sheath.SetLocalPositionAndRotation(
                    Vector3.Lerp(SheathMouth, AcrossMouth, across), Quaternion.Slerp(SheathRotation, AcrossSheath, across));
                var m = rig.worldToLocalMatrix * sheath.localToWorldMatrix;
                sheathPose = new Pose(m.GetColumn(3), m.rotation);
                sheathed.root.gameObject.SetActive(false);
            }

            Pose rightGrip = new(rightHand.grip.localPosition, rightHand.grip.localRotation);
            Pose leftGrip = new(leftHand.grip.localPosition, leftHand.grip.localRotation);
            Pose InSheath(Key k) => sheathPose.Then(new Vector3(0f, -k.slide, 0f), Quaternion.identity);
            Pose RightArm(Key k)
            {
                if (!k.rightOnHilt) return new Pose(k.rp, k.rq);
                if (k.fromAbove)
                {
                    // The arm's side runs along the handle (as in the grip) and the arm itself
                    // points down-left from the top right; the glove sits on the handle
                    var hilt = InSheath(k);
                    Vector3 x = hilt.q * Vector3.up;
                    Vector3 y = (FromTopRight - Vector3.Dot(FromTopRight, x) * x).normalized;
                    var armQ = Quaternion.LookRotation(Vector3.Cross(x, y), y);
                    Vector3 handle = hilt.p + hilt.q * new Vector3(0f, -0.055f, 0f);
                    return new Pose(handle - armQ * GripFront, armQ);
                }
                var knifePose = InSheath(k);
                var q = knifePose.q * Quaternion.Inverse(rightGrip.q);
                return new Pose(knifePose.p - q * rightGrip.p, q);
            }
            Pose LeftArm(Key k)
            {
                if (!k.leftOnSheath) return new Pose(k.lp, k.lq);
                Vector3 onSheath = sheathPose.p + sheathPose.q * new Vector3(0f, 0.035f, 0f);
                return new Pose(onSheath - LeftOnSheath * GripFront, LeftOnSheath);
            }
            var right = Pose.Blend(RightArm(a), RightArm(b), s);
            var left = Pose.Blend(LeftArm(a), LeftArm(b), s);
            Vector3 pivot = KnifePivot;
            Pose Knife(Key k) => k.hold switch
            {
                Hold.Right => right.Then(rightGrip.p, rightGrip.q),
                Hold.Left => left.Then(leftGrip.p, leftGrip.q),
                Hold.Air => new Pose(k.ap - k.aq * pivot, k.aq),
                _ => InSheath(k),
            };
            var knifePose = Pose.Blend(Knife(a), Knife(b), s);

            // Spins about the knife's pivot, and orbits around the right arm
            Vector3 spin = Vector3.Lerp(a.spin, b.spin, s);
            float orbit = Mathf.Lerp(a.orbit, b.orbit, s);
            if (routine.sustainAxis == 1) spin.z += Mathf.Sign(routine.sustainSpeed) * sustainExtra;
            if (routine.sustainAxis == 2) orbit += sustainExtra;
            Vector3 pivotAt = knifePose.p + knifePose.q * pivot;
            knifePose.q = knifePose.q * Quaternion.Euler(spin);
            knifePose.p = pivotAt - knifePose.q * pivot;
            if (orbit != 0f)
            {
                var around = Quaternion.AngleAxis(orbit, right.q * Vector3.up);
                knifePose.p = right.p + around * (knifePose.p - right.p);
                knifePose.q = around * knifePose.q;
            }

            hand.SetLocalPositionAndRotation(right.p, right.q);
            leftHand.root.gameObject.SetActive(true);
            leftHand.root.SetLocalPositionAndRotation(left.p, left.q);
            if (knife.root.parent != rig) knife.root.SetParent(rig, false);
            knife.ringSpin = 0f;
            knife.Animate(time, knife.IsSword ? t : -1f);
            knife.root.gameObject.SetActive(true);
            knife.root.SetLocalPositionAndRotation(knifePose.p, knifePose.q);

            // Butterfly handles and blade flip as the routine says
            if (knife.model == KnifeModel.Butterfly && knife.blade && knife.swingHandle)
            {
                float flips = Mathf.Lerp(a.flips, b.flips, s);
                float frac = flips - Mathf.Floor(flips);
                knife.swingHandle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(frac * Mathf.PI) * 150f);
                knife.blade.localRotation = Quaternion.Euler(0f, 0f, flips * 180f);
            }

            // Arms turn see-through wherever the knife passes behind them
            var points = new List<Vector3> { pivotAt, knifePose.p };
            if (knife.tip) points.Add(rig.InverseTransformPoint(knife.tip.position));
            // A sheathed sword is meant to sit under the hands: keep them solid then
            bool seated = a.hold == Hold.Sheath && b.hold == Hold.Sheath && Mathf.Lerp(a.slide, b.slide, s) < 0.06f;
            SetArmAlpha(seated ? 1f : OverlapAlpha(right, points));
            SetLeftAlpha(seated ? 1f : OverlapAlpha(left, points));
        }

        // How solid an arm can stay: see-through where any of the knife's points sit behind it
        // on screen
        static float OverlapAlpha(Pose arm, List<Vector3> points)
        {
            Vector3 a = arm.p + arm.q * new Vector3(0f, GloveSize.y * 0.5f, 0f);
            Vector3 b = arm.p + arm.q * new Vector3(0f, -0.07f - ArmLength, 0f);
            float alpha = 1f;
            foreach (var p in points)
            {
                if (p.z <= 0.01f) continue;
                Vector2 P = new(p.x / p.z, p.y / p.z);
                // Closest point on the arm, on screen
                float best = float.MaxValue, depth = 0f;
                for (int k = 0; k <= 8; k++)
                {
                    Vector3 q = Vector3.Lerp(a, b, k / 8f);
                    if (q.z <= 0.01f) continue;
                    float d = Vector2.Distance(P, new Vector2(q.x / q.z, q.y / q.z));
                    if (d < best) { best = d; depth = q.z; }
                }
                if (p.z < depth - 0.03f) continue; // the knife is in front of the arm
                alpha = Mathf.Min(alpha, Mathf.Lerp(0.3f, 1f, Ease(best, 0.12f, 0.3f)));
            }
            return alpha;
        }

        // Shows a moment of the knife inspect in edit mode, for photos (negative: at rest)
        public void PreviewKnifeInspect(float time)
        {
            if (weapons == null) return;
            routine = RoutineFor(knife);
            sustainExtra = 0f;
            PoseKnife(Vector3.zero, Vector3.zero, time);
            if (time < 0f) UpdateSheath();
        }

        // ------------------------------------------------------------------ blade trail

        LineRenderer trail;
        Material trailMat;
        Texture2D trailFade;
        readonly List<(float t, Vector3 p)> trailPoints = new();
        const float TrailLife = 0.14f;

        void UpdateTrail(bool active)
        {
            if (!Application.isPlaying) return;
            if (!trail)
            {
                trailFade = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < 64; x++) trailFade.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Pow(1f - x / 63f, 1.5f)));
                trailFade.Apply();
                trailMat = fadeTemplate ? new Material(fadeTemplate) : MakeTransparent(new Material(template));
                trailMat.SetTexture("_BaseMap", trailFade);
                trailMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.75f));
                trailMat.EnableKeyword("_EMISSION");
                trailMat.SetColor("_EmissionColor", Color.white * 1.2f);
                materials.Add(trailMat);
                trail = new GameObject("Blade Trail").AddComponent<LineRenderer>();
                trail.gameObject.layer = Layer;
                trail.useWorldSpace = false;
                trail.sharedMaterial = trailMat;
                trail.textureMode = LineTextureMode.Stretch;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.numCapVertices = 2;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.014f), new Keyframe(1f, 0f));
            }
            Transform rig = hand.parent;
            if (trail.transform.parent != rig) trail.transform.SetParent(rig, false);
            float now = Time.time;
            trailPoints.RemoveAll(e => now - e.t > TrailLife);
            if (active && knife.tip && knife.root.gameObject.activeInHierarchy)
                trailPoints.Insert(0, (now, rig.InverseTransformPoint(knife.tip.position)));
            trail.enabled = trailPoints.Count >= 2;
            if (!trail.enabled) return;
            trail.positionCount = trailPoints.Count;
            for (int i = 0; i < trailPoints.Count; i++) trail.SetPosition(i, trailPoints[i].p);
        }

        // ------------------------------------------------------------------ see-through arms

        class ArmPart
        {
            public MeshRenderer renderer;
            public Material solid, clear;
        }

        readonly List<ArmPart> armParts = new(), leftArmParts = new();
        float armAlpha = 1f, leftAlpha = 1f;

        // Gives both knife arms their own solid and see-through materials
        void SetupArmFade()
        {
            Setup(rightHand, armParts);
            Setup(leftHand, leftArmParts);

            void Setup(BlockArm arm, List<ArmPart> parts)
            {
                foreach (var r in arm.root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var solid = new Material(r.sharedMaterial);
                    materials.Add(solid);
                    var clear = fadeTemplate ? new Material(fadeTemplate) : MakeTransparent(new Material(template));
                    clear.SetTexture("_BaseMap", solid.GetTexture("_BaseMap"));
                    clear.SetColor("_BaseColor", solid.GetColor("_BaseColor"));
                    clear.SetFloat("_Smoothness", solid.GetFloat("_Smoothness"));
                    clear.SetFloat("_Metallic", solid.GetFloat("_Metallic"));
                    materials.Add(clear);
                    r.sharedMaterial = solid;
                    parts.Add(new ArmPart { renderer = r, solid = solid, clear = clear });
                }
            }
        }

        void SetArmAlpha(float alpha) => ApplyAlpha(armParts, ref armAlpha, alpha);
        void SetLeftAlpha(float alpha) => ApplyAlpha(leftArmParts, ref leftAlpha, alpha);

        static void ApplyAlpha(List<ArmPart> parts, ref float current, float alpha)
        {
            if (Mathf.Abs(alpha - current) < 0.001f) return;
            current = alpha;
            foreach (var part in parts)
            {
                if (alpha > 0.995f) { part.renderer.sharedMaterial = part.solid; continue; }
                Color c = part.solid.GetColor("_BaseColor");
                c.a = alpha;
                part.clear.SetColor("_BaseColor", c);
                part.renderer.sharedMaterial = part.clear;
            }
        }

        // URP Lit, switched to alpha-blended transparent
        public static Material MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }
    }
}
