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
        static readonly Vector3 RightIdle = new(0.1f, -0.105f, 0.3f);
        // As in CS2, both gloves show at rest: the left a loose fist low left of the middle
        static readonly Vector3 LeftIdle = new(-0.085f, -0.14f, 0.32f);
        static readonly Quaternion ReverseIdle = FingersBack(new Vector3(-0.3f, 0.5f, 1f), new Vector3(0.1f, 0.6f, -0.7f));
        // Talon knife at rest: low on the right, the ring out to the right of the glove and the
        // hooked blade sweeping left and curling down, flat side toward you
        // (the reverse-grip hold from the classic shooters: fist tilted in from the bottom right
        // corner, ring up by the index finger, the hooked blade curling down out of the fist)
        static readonly Vector3 TalonIdlePos = new(0.09f, -0.105f, 0.3f);
        static readonly Quaternion TalonIdle = FingersBack(new Vector3(-0.1f, 0.35f, 0.93f), new Vector3(0.1f, 0.9f, -0.4f));
        // Talon inspect: the knife hangs from the finger ring in front of the raised glove, flat
        // side on, and spins round the finger there
        static readonly Vector3 TalonShowPos = TalonIdlePos; // the arm stays put: only the hand turns
        static readonly Quaternion TalonShowArm = FingersBack(new Vector3(-0.12f, 0.9f, 0.42f), new Vector3(0.1f, -0.4f, 0.9f));
        static readonly Quaternion TalonShow = Quaternion.Euler(0f, 0f, -42f);
        static readonly Quaternion ForwardIdle = FingersBack(new Vector3(-0.3f, 0.5f, 1f), new Vector3(0.6f, 0.5f, -0.6f));
        // Butterfly knife at rest, as in CS2: the fist low right, thumb up, the knife pointing
        // forward and up to the left with the flat of the handles and blade toward you
        static readonly Quaternion ButterflyIdle = Quaternion.AngleAxis(-35f, Vector3.forward) * Quaternion.AngleAxis(45f, new Vector3(-0.6f, 0.45f, 0.65f)) * ForwardIdle;
        static readonly Quaternion LeftIdleRotation = FingersBack(new Vector3(0.3f, 0.5f, 1f), new Vector3(-0.1f, 0.6f, -0.7f));
        static readonly Vector3 LeftHandAway = new(-0.04f, -0.14f, -0.05f);

        // The sheath still on the left hip but brought up a little into view for the sword
        // routine (its +Y runs from the mouth back into the scabbard), and how the left hand
        // holds it at the mouth
        static readonly Vector3 AcrossMouth = new(-0.15f, -0.13f, 0.5f);
        static readonly Quaternion AcrossSheath = FingersBack(new Vector3(-0.35f, -0.3f, -0.9f), new Vector3(1f, 0f, -0.35f));
        static readonly Vector3 FromTopRight = new Vector3(-0.4f, 0.3f, 0.87f).normalized;
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
            public float fan;              // butterfly: the loose handle swung open this far (degrees)
            public float slide;            // sword pulled this far out of the sheath
            public float across;           // sheath at the hip (0) or laid across the view (1)
            public bool rightOnHilt, leftOnSheath;
            public bool fromAbove;         // on the hilt, the arm reaches in from the top right
            public float show;             // talon: hanging from the ring in front of the glove (0..1)
            public float onFinger;         // ring knives: hung by the ring on the index finger, to spin on it (0..1)
            public float rOpen, lOpen;     // fingers loosened (0 gripping .. 1 open)
            public bool rKeep;             // ...all but the index finger (a knife spinning in its ring)
            public Key At(float time) { var k = (Key)MemberwiseClone(); k.t = time; return k; }
        }

        class Routine
        {
            public Key[] keys;
            public float sustainAt = -1f; // held F pauses the routine here...
            public int sustainAxis;       // ...and keeps spinning: 0 nothing, 1 the knife, 2 around the arm, 3 butterfly flips
            public float sustainSpeed;
            public (float t, AudioClip clip, float volume)[] sounds = System.Array.Empty<(float, AudioClip, float)>();
            public float Length => keys[^1].t;
        }

        static Key IdleKey(bool talon) => new()
        {
            rp = talon ? TalonIdlePos : RightIdle, rq = talon ? TalonIdle : ForwardIdle,
            lp = LeftIdle, lq = LeftIdleRotation,
            aq = Quaternion.identity,
        };

        // Arms thrown wide apart for a throw, and raised to catch
        static readonly Vector3 RightWide = new(0.175f, -0.03f, 0.3f), LeftWide = new(-0.175f, -0.035f, 0.31f);
        static readonly Quaternion RightUp = FB(-0.3f, 0.8f, 0.6f, 0.3f, 0.3f, -1f), LeftUp = FB(0.3f, 0.8f, 0.6f, -0.3f, 0.3f, -1f);

        // Talon knife, after the classic karambit inspect: a flick that whirls the knife once
        // round the index finger in its ring (the other fingers open to let it go round), then
        // both hands meet low in the middle with the blade laid out to the right, then the hand
        // turns upright, palm to you, the whole curve of the blade on show, and back to the hold.
        // Held F keeps it whirling on the finger.
        static Routine TalonRoutine()
        {
            var show = new Vector3(0.1f, -0.075f, 0.3f);
            Quaternion showA = FB(-0.87f, 0.5f, 0.05f, 0f, 0f, 1f), showB = FB(-0.75f, 0.65f, 0.1f, 0.15f, 0.05f, 1f);
            var ks = new List<Key>();
            var k = IdleKey(true); ks.Add(k);
            k = k.At(0.2f); k.rp = TalonIdlePos + new Vector3(0.005f, 0.02f, 0f); k.rq = FB(-0.35f, 0.9f, 0.25f, -0.3f, -0.2f, 0.93f);
            k.rOpen = 0.85f; k.rKeep = true; ks.Add(k);
            k = k.At(0.6f); k.spin = new(0f, 0f, -360f); k.rp = TalonIdlePos + new Vector3(-0.03f, 0.01f, 0f); ks.Add(k);
            k = k.At(1.05f); k.spin = new(0f, 0f, -720f); k.rp = show; k.rq = showA; ks.Add(k);
            k = k.At(1.25f); k.rOpen = 0f; k.rKeep = false; ks.Add(k);
            k = k.At(1.95f); k.rq = showB; ks.Add(k);
            k = k.At(2.6f); k.rq = showA; k.rp = show + new Vector3(0.005f, 0.005f, 0f); ks.Add(k);
            k = k.At(3.2f); k.rp = TalonIdlePos; k.rq = TalonIdle; ks.Add(k);
            return new Routine
            {
                keys = ks.ToArray(), sustainAt = 0.45f, sustainAxis = 1, sustainSpeed = -1080f,
                sounds = new[] { (0.3f, WeaponSounds.Slash, 0.3f), (0.8f, WeaponSounds.Slash, 0.25f), (1.2f, WeaponSounds.Tick, 0.4f) },
            };
        }

        // Butterfly knife, after the classic butterfly inspect, every trick in the one hand:
        // quick flips while the wrist rolls hard one way and the other, a pop up out of the
        // fingers where it somersaults and is snatched back, a rollover right round the fist
        // still flipping, a fanned-open show with the blade standing up, then a rapid burst of
        // flips that snaps it shut. Held thumb up, blade standing out of the fist, the forearm
        // rising from the bottom right corner. Held F keeps the flips going.
        static Routine ButterflyRoutine()
        {
            var up = new Vector3(0.1f, -0.085f, 0.3f);
            var show = new Vector3(0.08f, -0.07f, 0.3f);
            Quaternion R1 = FB(-0.55f, 0.45f, 0.7f, 0.7f, 0f, 0.55f), R2 = FB(-0.85f, 0.4f, 0.3f, 0.35f, 0f, 0.95f), R3 = FB(-0.2f, 0.45f, 0.9f, 1f, 0f, 0.2f);
            Quaternion showA = Quaternion.Euler(0f, 0f, -28f) * FB(-0.5f, 0.3f, 0.8f, 0.71f, -0.31f, 0.56f), showB = Quaternion.Euler(0f, 0f, -16f) * FB(-0.6f, 0.2f, 0.75f, 0.73f, -0.13f, 0.62f);
            var ks = new List<Key>();
            var k = IdleKey(false); k.rq = ButterflyIdle; ks.Add(k);
            k = k.At(0.3f); k.rp = up; k.rq = R1; k.flips = 2f; k.rOpen = 0.25f; ks.Add(k);
            k = k.At(0.6f); k.rq = R2; k.flips = 3f; ks.Add(k);
            k = k.At(0.85f); k.rq = R3; k.flips = 4f; ks.Add(k);
            // Tossed: it tumbles end over end over the hand...
            k = k.At(1.05f); k.hold = Hold.Air; k.ap = up + new Vector3(-0.01f, 0.07f, 0.01f); k.aq = Quaternion.identity; k.spin = new(360f, 0f, 0f);
            k.rp = up + new Vector3(0f, -0.01f, 0f); k.rq = R1; ks.Add(k);
            // ...and is caught into the show, the loose handle swinging wide open
            k = k.At(1.3f); k.hold = Hold.Right; k.rp = show; k.rq = showA; k.spin = new(720f, 0f, 0f); k.rOpen = 0.1f; ks.Add(k);
            k = k.At(1.55f); k.fan = 50f; ks.Add(k); // the loose handle swung open, away from the other: a V round the fingers
            k = k.At(2.2f); k.rq = showB; ks.Add(k);
            k = k.At(2.8f); k.rq = showA; ks.Add(k);
            // Flipped shut and home
            k = k.At(3.05f); k.fan = 0f; k.flips = 6f; k.rq = R2; k.rp = up; ks.Add(k);
            k = k.At(3.4f); k.rp = RightIdle; k.rq = ButterflyIdle; k.rOpen = 0f; ks.Add(k);
            return new Routine
            {
                keys = ks.ToArray(), sustainAt = 2.2f,
                sounds = new[] { (0.3f, WeaponSounds.Tick, 0.3f), (0.6f, WeaponSounds.Tick, 0.3f), (0.85f, WeaponSounds.Tick, 0.3f), (1.0f, WeaponSounds.Slash, 0.3f),
                    (1.3f, WeaponSounds.Tick, 0.45f), (1.55f, WeaponSounds.Tick, 0.35f), (2.95f, WeaponSounds.Tick, 0.3f), (3.1f, WeaponSounds.Tick, 0.4f) },
            };
        }

        // The fixed blades (kukri, M9 bayonet, skeleton knife), after the classic shooters'
        // inspects: the hand brings the knife up and turns palm-to-you so the whole flat of the
        // blade shows, tilts it to catch the light, rolls it over in the fingers to show the
        // other side, then each does its own trick before going back to the hold: the bayonet
        // whirls once round in the hand, the kukri is tossed up tumbling and caught, and the
        // skeleton knife spins on a finger through its ring (held F keeps that one spinning)
        static Routine FixedBladeRoutine(KnifeModel model)
        {
            Quaternion showA = FB(-0.7f, 0.3f, 0.6f, 0.594f, -0.06f, 0.723f), showB = FB(-0.7f, 0.1f, 0.7f, 0.679f, 0.175f, 0.654f);
            var up = new Vector3(0.06f, -0.115f, 0.3f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.4f); k.rp = up; k.rq = showA; k.rOpen = 0.1f; ks.Add(k);
            k = k.At(0.95f); k.rq = showB; ks.Add(k);
            k = k.At(1.2f); k.rOpen = 0.35f; k.spin = new(0f, 90f, 0f); ks.Add(k);
            k = k.At(1.45f); k.rOpen = 0f; k.spin = new(0f, 180f, 0f); ks.Add(k);
            k = k.At(1.9f); k.rq = showA; ks.Add(k);
            float sustain = 0.95f;
            int axis = 0;
            float speed = 0f;
            (float, AudioClip, float)[] sounds;
            switch (model)
            {
                case KnifeModel.KukriKnife:
                    // After the CS2 kukri: no tricks, just the heavy blade turned in the fist
                    ks.RemoveRange(1, ks.Count - 1);
                    Quaternion diag = Quaternion.Euler(0f, 0f, 28f) * showA, low = Quaternion.Euler(0f, 0f, 68f) * showA;
                    k = IdleKey(false);
                    k = k.At(0.4f); k.rp = up; k.rq = diag; k.rOpen = 0.1f; ks.Add(k);
                    k = k.At(1.05f); k.rq = Quaternion.Euler(0f, 0f, 20f) * showB; ks.Add(k);
                    k = k.At(1.5f); k.rq = showA; k.spin = new(0f, 90f, 0f); ks.Add(k);
                    k = k.At(1.95f); k.spin = new(0f, 180f, 0f); ks.Add(k);
                    k = k.At(2.45f); k.rq = low; k.rp = up + new Vector3(0.01f, 0.005f, 0f); ks.Add(k);
                    k = k.At(2.9f); k.rq = diag; k.rp = up; ks.Add(k);
                    k = k.At(3.4f); k.rp = RightIdle; k.rq = ForwardIdle; k.spin = new(0f, 360f, 0f); k.rOpen = 0f; ks.Add(k);
                    sustain = 1.05f;
                    sounds = new[] { (0.3f, WeaponSounds.Slash, 0.25f), (1.5f, WeaponSounds.Tick, 0.35f), (2.4f, WeaponSounds.Tick, 0.3f) };
                    break;
                case KnifeModel.Skeleton:
                {
                    // After the CS2 skeleton: the hand opens and the knife whirls twice round the
                    // index finger through its ring, is caught standing up in the fist, shown, and put away
                    ks.RemoveRange(1, ks.Count - 1);
                    Quaternion spinHand = FB(-0.5f, 0.15f, 0.85f, 0.1f, 1f, 0.2f);
                    var spinAt = new Vector3(0.085f, -0.105f, 0.3f);
                    k = IdleKey(false);
                    k = k.At(0.3f); k.rp = spinAt; k.rq = spinHand; k.onFinger = 1f; k.rOpen = 0.9f; ks.Add(k);
                    k = k.At(1.3f); k.spin = new(0f, 0f, -720f); ks.Add(k);
                    k = k.At(1.5f); k.onFinger = 0f; k.rOpen = 0f; k.rp = up; k.rq = showA; ks.Add(k);
                    k = k.At(2.1f); k.rq = showB; ks.Add(k);
                    k = k.At(2.7f); k.rq = showA; ks.Add(k);
                    k = k.At(3.2f); k.rp = RightIdle; k.rq = ForwardIdle; ks.Add(k);
                    sustain = 0.8f; axis = 1; speed = -1080f;
                    sounds = new[] { (0.35f, WeaponSounds.Slash, 0.3f), (0.85f, WeaponSounds.Slash, 0.25f), (1.45f, WeaponSounds.Tick, 0.45f) };
                    break;
                }
                default:
                    k = k.At(2.15f); k.rOpen = 0.75f; k.rKeep = true; ks.Add(k);
                    k = k.At(2.5f); k.spin = new(0f, 180f, -360f); ks.Add(k);
                    k = k.At(2.65f); k.rOpen = 0f; k.rKeep = false; ks.Add(k);
                    k = k.At(3.3f); k.rp = RightIdle; k.rq = ForwardIdle; k.spin = new(0f, 360f, -360f); ks.Add(k);
                    sounds = new[] { (0.3f, WeaponSounds.Slash, 0.25f), (1.3f, WeaponSounds.Tick, 0.35f), (2.3f, WeaponSounds.Slash, 0.35f), (2.6f, WeaponSounds.Tick, 0.45f) };
                    break;
            }
            return new Routine { keys = ks.ToArray(), sustainAt = sustain, sustainAxis = axis, sustainSpeed = speed, sounds = sounds };
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

        // As in CS2, the empty left hand drops out of view for the inspect (it only rests on
        // screen, and comes up when a trick needs it), so nothing swings through it
        static readonly Vector3 LeftDropped = LeftIdle + new Vector3(-0.03f, -0.13f, -0.03f);

        Routine RoutineFor(WeaponParts parts)
        {
            var r = RoutineOf(parts);
            for (int i = 1; i < r.keys.Length - 1; i++)
                if (r.keys[i].lp == LeftIdle) r.keys[i].lp = LeftDropped;
            return r;
        }

        Routine RoutineOf(WeaponParts parts) => parts.model switch
        {
            KnifeModel.Talon => TalonRoutine(),
            KnifeModel.Butterfly => ButterflyRoutine(),
            KnifeModel.Bayonet or KnifeModel.Skeleton or KnifeModel.KukriKnife => FixedBladeRoutine(parts.model),
            KnifeModel.Reaper => ReaperRoutine(),
            KnifeModel.Saber => SaberRoutine(),
            KnifeModel.Shardfang or KnifeModel.Sai or KnifeModel.Kris => ShardRoutine(),
            KnifeModel.Axe or KnifeModel.Spear => ReaperRoutine(),
            KnifeModel.Kukri or KnifeModel.Claws => SaberRoutine(),
            // Swords: raised and turned to show off the blade, like the fixed blades
            KnifeModel.HollowMoon or KnifeModel.Tidebreaker or KnifeModel.Colossus => FixedBladeRoutine(parts.model),
            _ => SwordRoutine(),
        };

        // The Void specials all start the same way: the arm comes up and the weapon starts to
        // whirl in front of you, faster and faster, the flames roaring (hold F to keep it going)
        static Key RaiseKey(Key k, float t)
        {
            k = k.At(t);
            k.rp = new(0.09f, -0.035f, 0.35f);
            k.rq = FB(-0.2f, 0.9f, 0.4f, 0.1f, 0.2f, -1f);
            k.lp = LeftIdle + LeftHandAway;
            return k;
        }

        // Scythe: a slow propeller whirl, then flung high end over end and caught with the arms
        // spread wide, a last sweep, home
        static Routine ReaperRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.35f); k.spin = new(0f, 0f, 180f); ks.Add(k);
            k = k.At(1.0f); k.spin = new(0f, 0f, 900f); ks.Add(k);
            k = k.At(1.4f); k.hold = Hold.Air; k.ap = new(0.01f, 0.1f, 0.5f); k.aq = Quaternion.identity; k.spin = new(-360f, 0f, 1080f);
            k.rp = RightWide; k.rq = RightUp; k.lp = LeftWide; k.lq = LeftUp; ks.Add(k);
            k = k.At(1.9f); k.ap = new(0.03f, 0.03f, 0.42f); k.spin = new(-720f, 0f, 1080f); ks.Add(k);
            k = k.At(2.15f); k.hold = Hold.Right; k.rp = new(0.1f, -0.03f, 0.34f); k.rq = RightUp; k.spin = new(-720f, 0f, 1080f); k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            k = k.At(2.7f); k.rp = new(0.04f, -0.06f, 0.36f); k.rq = FB(-1f, 0.2f, 0.4f, 0f, 1f, -0.2f); k.spin = new(-720f, 0f, 1440f); ks.Add(k);
            k = k.At(3.2f); k.rp = RightIdle; k.rq = ForwardIdle; ks.Add(k);
            return new Routine
            {
                keys = ks.ToArray(), sustainAt = 0.9f, sustainAxis = 1, sustainSpeed = 720f,
                sounds = new[] { (0.5f, WeaponSounds.Slash, 0.5f), (1.35f, WeaponSounds.Slash, 0.6f), (2.4f, WeaponSounds.Slash, 0.5f) },
            };
        }

        // Saber: a humming spin, then flipped end over end up into the air, caught, and a
        // flourish of two quick twirls before it settles
        static Routine SaberRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.3f); k.spin = new(0f, 0f, 360f); ks.Add(k);
            k = k.At(0.9f); k.spin = new(0f, 0f, 1440f); ks.Add(k);
            k = k.At(1.25f); k.hold = Hold.Air; k.ap = new(0.02f, 0.09f, 0.45f); k.aq = Quaternion.identity; k.spin = new(540f, 0f, 1440f);
            k.rp = RightWide; k.rq = RightUp; k.lp = LeftWide; k.lq = LeftUp; ks.Add(k);
            k = k.At(1.7f); k.ap = new(0.05f, 0.02f, 0.4f); k.spin = new(1080f, 0f, 1440f); ks.Add(k);
            k = k.At(1.9f); k.hold = Hold.Right; k.rp = new(0.1f, -0.03f, 0.34f); k.rq = RightUp; k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            k = k.At(2.6f); k.spin = new(1080f, 0f, 2160f); ks.Add(k);
            k = k.At(3.1f); k.rp = RightIdle; k.rq = ForwardIdle; ks.Add(k);
            return new Routine
            {
                keys = ks.ToArray(), sustainAt = 0.8f, sustainAxis = 1, sustainSpeed = 1260f,
                sounds = new[] { (0.35f, WeaponSounds.Slash, 0.45f), (1.2f, WeaponSounds.Slash, 0.5f), (2.2f, WeaponSounds.Slash, 0.45f) },
            };
        }

        // Crystal dagger: a spin, then tossed across between the hands, arms flung wide, and
        // back again, glittering
        static Routine ShardRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.3f); k.spin = new(0f, 0f, 270f); ks.Add(k);
            k = k.At(0.85f); k.spin = new(0f, 0f, 1080f); ks.Add(k);
            k = k.At(1.1f); k.hold = Hold.Air; k.ap = new(-0.01f, 0.07f, 0.42f); k.aq = Quaternion.identity; k.spin = new(360f, 0f, 1260f);
            k.rp = RightWide; k.rq = RightUp; k.lp = LeftWide; k.lq = LeftUp; ks.Add(k);
            k = k.At(1.35f); k.hold = Hold.Left; k.lp = new(-0.12f, -0.01f, 0.33f); k.spin = new(720f, 0f, 1440f); ks.Add(k);
            k = k.At(1.75f); k.lp = new(-0.1f, 0.01f, 0.34f); k.lq = FB(0.3f, 0.9f, 0.4f, -0.1f, 0.2f, -1f); k.spin = new(720f, 0f, 2160f); ks.Add(k);
            k = k.At(2.0f); k.hold = Hold.Air; k.ap = new(0.01f, 0.08f, 0.42f); k.aq = Quaternion.identity; k.spin = new(1080f, 0f, 2340f); k.lp = LeftWide; ks.Add(k);
            k = k.At(2.25f); k.hold = Hold.Right; k.rp = new(0.12f, -0.01f, 0.33f); k.rq = RightUp; k.spin = new(1080f, 0f, 2520f); k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            k = k.At(2.8f); k.rp = RightIdle; k.rq = ForwardIdle; ks.Add(k);
            return new Routine
            {
                keys = ks.ToArray(), sustainAt = 0.75f, sustainAxis = 1, sustainSpeed = 900f,
                sounds = new[] { (1.0f, WeaponSounds.Slash, 0.4f), (1.9f, WeaponSounds.Slash, 0.4f) },
            };
        }

        // ------------------------------------------------------------------ update

        // Double tap F: the inspect keeps going by itself (as if F were held), until you double
        // tap again
        public bool AutoInspect { get; private set; }
        float lastInspectTap = -99f;

        void InspectTaps(Keyboard kb)
        {
            if (kb == null || !kb.fKey.wasPressedThisFrame) return;
            float now = Time.unscaledTime;
            if (now - lastInspectTap < 0.3f)
            {
                AutoInspect = !AutoInspect;
                lastInspectTap = -99f;
                Play(WeaponSounds.Tick, 0.5f);
            }
            else lastInspectTap = now;
        }

        void UpdateKnife(Keyboard kb, Mouse mouse, bool locked, bool ready, float dt)
        {
            InspectTaps(kb);
            bool held = (kb != null && kb.fKey.isPressed) || AutoInspect;
            if (((kb != null && kb.fKey.wasPressedThisFrame) || AutoInspect) && inspectTime < 0f && slashTime < 0f && ready) StartInspect();
            if (ready && locked && mouse != null && mouse.leftButton.wasPressedThisFrame && (slashTime < 0f || slashTime > 0.3f))
            {
                slashTime = 0f;
                slashSide = -slashSide;
                StopInspect();
                Play(WeaponSounds.Slash, 0.7f);
                SwingHit();
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
            bool talon = knife.model == KnifeModel.Talon;
            if (talon) SetTalonGrip(rightHand);
            else SetGrip(rightHand, false);
            SetGrip(leftHand, false);
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
                knife.root.SetLocalPositionAndRotation((1f - knifeScale) * KnifeHandle, Quaternion.identity);
                knife.root.localScale = Vector3.one * knifeScale;
                UpdateSheath();
            }
            if (sheath) sheath.SetLocalPositionAndRotation(SheathMouth, SheathRotation);
            if (flames) flames.gameObject.SetActive(false);
            leftHand.root.gameObject.SetActive(true);
            SetLeftAlpha(1f);
            if (PoseSwordDraw())
            {
                SetArmAlpha(0.35f);
                knife.Animate(time, -1f);
                return;
            }
            rightHand.open = 0f; rightHand.keepIndex = false;
            leftHand.open = 0.3f; // a loose fist at rest, as in CS2
            SetHandGrip(leftHand, Fist);
            hand.localPosition = (talon ? TalonIdlePos : RightIdle) + pos;
            hand.localRotation = Quaternion.Euler(rot) * (talon ? TalonIdle : knife.model == KnifeModel.Butterfly ? ButterflyIdle : ForwardIdle);
            SetArmAlpha(1f);
            // The left arm dips out of the way of cuts that cross the body
            float slashAway = slashTime >= 0f && slashSide < 0f ? Plateau(slashTime, 0f, 0.1f, 0.3f, 0.45f) : 0f;
            leftHand.root.SetLocalPositionAndRotation(LeftIdle + LeftHandAway * slashAway, LeftIdleRotation);
            knife.ringSpin = 0f;
            knife.root.localScale = Vector3.one * knifeScale;
            if (knife.model != KnifeModel.Talon) knife.root.localPosition = (1f - knifeScale) * KnifeHandle;
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
            KnifeModel.Skeleton => knife.ringCenter,
            KnifeModel.Reaper => new Vector3(0f, -0.03f, 0f),
            KnifeModel.Saber => new Vector3(0f, 0.02f, 0f),
            KnifeModel.Axe or KnifeModel.Spear => new Vector3(0f, 0.06f, 0f),
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
            // Motion flows through the keys: positions, spins and flips follow a curve through
            // the keys either side, and turns only slow to a stop where they change direction
            float u = Mathf.InverseLerp(a.t, b.t, t);
            Key before = i >= 2 ? keys[i - 2] : a, after = i + 1 < keys.Length ? keys[i + 1] : b;
            float Curve(float v0, float v1, float v2, float v3)
            {
                float m1 = Slope(v0, v1, v2, before.t, a.t, b.t), m2 = Slope(v1, v2, v3, a.t, b.t, after.t), dt = b.t - a.t;
                float u2 = u * u, u3 = u2 * u;
                return (2f * u3 - 3f * u2 + 1f) * v1 + (u3 - 2f * u2 + u) * m1 * dt + (3f * u2 - 2f * u3) * v2 + (u3 - u2) * m2 * dt;
            }
            Vector3 CurveV(System.Func<Key, Vector3> f)
            {
                Vector3 v0 = f(before), v1 = f(a), v2 = f(b), v3 = f(after);
                return new(Curve(v0.x, v1.x, v2.x, v3.x), Curve(v0.y, v1.y, v2.y, v3.y), Curve(v0.z, v1.z, v2.z, v3.z));
            }
            float TurnEase(System.Func<Key, Quaternion> f)
            {
                float m1 = Carries(f(before), f(a), f(b)) ? 0.8f : 0f, m2 = Carries(f(a), f(b), f(after)) ? 0.8f : 0f;
                float u2 = u * u, u3 = u2 * u;
                return (u3 - 2f * u2 + u) * m1 + (3f * u2 - 2f * u3) + (u3 - u2) * m2;
            }
            right = new Pose(CurveV(k => RightArm(k).p), Quaternion.Slerp(RightArm(a).q, RightArm(b).q, TurnEase(k => RightArm(k).q)));
            left = new Pose(CurveV(k => LeftArm(k).p), Quaternion.Slerp(LeftArm(a).q, LeftArm(b).q, TurnEase(k => LeftArm(k).q)));
            // Knife tricks stay low on the screen so you can still see the run: anything above
            // the resting height is pulled down (the swords' sheath moves are low already)
            bool keepLow = !knife.IsSword && knife.model != KnifeModel.Butterfly; // (the butterfly pops up for its somersault)
            if (keepLow) { right.p = Low(right.p); left.p = Low(left.p); }
            float show = Mathf.Lerp(a.show, b.show, s);
            float size = Mathf.Lerp(1f, 0.8f, show) * knifeScale; // a little smaller while it's up on show
            Vector3 pivot = KnifePivot * size;
            Pose Knife(Key k) => k.hold switch
            {
                Hold.Right => AboutHandle(right.Then(rightGrip.p, rightGrip.q), size),
                Hold.Left => AboutHandle(left.Then(leftGrip.p, leftGrip.q), size),
                Hold.Air => new Pose((keepLow ? Low(k.ap) : k.ap) - k.aq * pivot, k.aq),
                _ => AboutHandle(InSheath(k), size),
            };
            var knifePose = Pose.Blend(Knife(a), Knife(b), s);

            // Talon show: the knife hangs from the finger ring just in front of the glove
            if (show > 0f)
            {
                Vector3 ringAt = right.p + right.q * GripFront + new Vector3(-0.035f, 0.035f, -0.035f); // on the glove's top corner, spinning low right, clear of the middle
                knifePose = Pose.Blend(knifePose, new Pose(ringAt - TalonShow * pivot, TalonShow), show);
            }
            // On the finger: the knife's ring goes onto the index finger (the ring's hole along
            // the finger), blade up past the back of the hand, so a spin turns it round the finger
            float onFinger = Mathf.Lerp(a.onFinger, b.onFinger, s);
            if (onFinger > 0f && IndexFinger(rightHand) is Transform finger)
            {
                Vector3 at = right.p + right.q * hand.InverseTransformPoint(finger.position);
                Quaternion q = right.q * Quaternion.LookRotation(Vector3.up, Vector3.forward);
                knifePose = Pose.Blend(knifePose, new Pose(at - q * pivot, q), onFinger);
            }
            knife.root.localScale = Vector3.one * size;

            // Spins about the knife's pivot, and orbits around the right arm
            Vector3 spin = CurveV(k => k.spin);
            float orbit = Curve(before.orbit, a.orbit, b.orbit, after.orbit);
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

            // Fingers: the hand holding the knife grips it, loosened as the routine says (a knife
            // in the air leaves the hands open); an empty left hand stays relaxed
            float Lerp(float x, float y) => Mathf.Lerp(x, y, s);
            float rOpen = Lerp(a.rOpen, b.rOpen);
            float rFree = Lerp(a.hold == Hold.Right ? 0f : 1f, b.hold == Hold.Right ? 0f : 1f);
            rightHand.open = Mathf.Max(rOpen, 0.55f * rFree);
            rightHand.keepIndex = (a.rKeep || b.rKeep) && rFree < 0.5f;
            bool leftHolds = a.hold == Hold.Left || b.hold == Hold.Left;
            SetHandGrip(leftHand, leftHolds ? Fist : Relaxed);
            leftHand.open = Lerp(a.lOpen, b.lOpen);

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
                float flips = Curve(before.flips, a.flips, b.flips, after.flips) + (routine.sustainAxis == 3 ? sustainExtra / 360f * 2f : 0f);
                float frac = flips - Mathf.Floor(flips);
                knife.swingHandle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(frac * Mathf.PI) * 150f + Curve(before.fan, a.fan, b.fan, after.fan));
                knife.blade.localRotation = Quaternion.Euler(0f, 0f, flips * 180f);
            }

            // Arms turn see-through wherever the knife passes behind them
            var points = new List<Vector3> { pivotAt, knifePose.p };
            if (knife.tip) points.Add(rig.InverseTransformPoint(knife.tip.position));
            // A sheathed sword is meant to sit under the hands: keep them solid then
            bool seated = a.hold == Hold.Sheath && b.hold == Hold.Sheath && Mathf.Lerp(a.slide, b.slide, s) < 0.06f;
            // Both arms go see-through while the hand is on the hilt, so the sheathed sword shows
            // through them
            float onHilt = (a.rightOnHilt ? 1f - s : 0f) + (b.rightOnHilt ? s : 0f);
            SetArmAlpha(Mathf.Lerp(seated ? 1f : OverlapAlpha(right, points), 0.35f, onHilt));
            SetLeftAlpha(Mathf.Lerp(seated ? 1f : OverlapAlpha(left, points), 0.35f, onHilt));

            // Dark flames wreathe the hilt while it's held at the sheath
            UpdateFlames(knifePose.p + knifePose.q * new Vector3(0f, -0.055f, 0f), knifePose.q, onHilt, time);
        }

        // The slope a curve passes a key with: none where the value stops or turns back there
        // (so holds stay still and nothing overshoots), otherwise carried through smoothly
        static float Slope(float p0, float p1, float p2, float t0, float t1, float t2)
        {
            float d0 = (p1 - p0) / Mathf.Max(t1 - t0, 1e-4f), d1 = (p2 - p1) / Mathf.Max(t2 - t1, 1e-4f);
            if (d0 * d1 <= 0f) return 0f;
            float m = (p2 - p0) / Mathf.Max(t2 - t0, 1e-4f);
            return Mathf.Sign(m) * Mathf.Min(Mathf.Abs(m), 3f * Mathf.Min(Mathf.Abs(d0), Mathf.Abs(d1)));
        }

        // Whether a turn keeps going the same way through the middle rotation
        static bool Carries(Quaternion q0, Quaternion q1, Quaternion q2)
        {
            (q1 * Quaternion.Inverse(q0)).ToAngleAxis(out float a0, out Vector3 x0);
            (q2 * Quaternion.Inverse(q1)).ToAngleAxis(out float a1, out Vector3 x1);
            if (a0 > 180f) { a0 = 360f - a0; x0 = -x0; }
            if (a1 > 180f) { a1 = 360f - a1; x1 = -x1; }
            return a0 > 2f && a1 > 2f && Vector3.Dot(x0, x1) > 0.5f;
        }

        // The index finger's middle joint, which ring knives spin on
        static Transform IndexFinger(BlockArm arm)
        {
            for (int i = 0; i < arm.bones.Length; i++)
                if (arm.rig.boneNames[i].StartsWith("index_02")) return arm.bones[i];
            return null;
        }

        // A knife scaled about its handle: its root moves toward the handle
        static Pose AboutHandle(Pose p, float size) => new(p.p + p.q * ((1f - size) * KnifeHandle), p.q);

        // Heights above the resting line are more than halved
        static Vector3 Low(Vector3 p) => new(p.x, p.y < -0.1f ? p.y : -0.1f + (p.y + 0.1f) * 0.45f, p.z);

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
                if (p.z < depth + 0.05f) continue; // in front of the arm, or in the hand: only fade for a knife well behind it
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

        // ------------------------------------------------------------------ dark flames

        // Flame artwork: Kenney's Particle Pack (CC0), flame_01-04 are wispy licks of fire,
        // flame_05-06 are single tongues (Resources/Flames)
        Transform flames;
        Material flameMat;
        Texture2D[] flameFrames;
        MaterialPropertyBlock flameProps;
        readonly List<(MeshRenderer r, float phase, float angle, float spread, float speed, float size, bool ember)> flameBits = new();

        // Camera-facing flame sprites around the handle: each is born near the handle, rises
        // and grows while its artwork flickers through frames, then fades away, over and over.
        // Black fire with embers in the sword's own color.
        void UpdateFlames(Vector3 at, Quaternion handle, float strength, float time)
        {
            if (strength <= 0.01f)
            {
                if (flames) flames.gameObject.SetActive(false);
                return;
            }
            if (!flames)
            {
                flameFrames = new Texture2D[6];
                for (int f = 0; f < 6; f++) flameFrames[f] = Resources.Load<Texture2D>($"Flames/flame_0{f + 1}");
                flameMat = fadeTemplate ? new Material(fadeTemplate) : MakeTransparent(new Material(template));
                flameMat.EnableKeyword("_EMISSION");
                flameMat.SetFloat("_Smoothness", 0f);
                materials.Add(flameMat);
                flameProps = new MaterialPropertyBlock();
                flames = new GameObject("Dark Flames").transform;
                flames.SetParent(hand.parent, false);
                var random = new System.Random(66);
                for (int i = 0; i < 18; i++)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Kill(quad.GetComponent<Collider>());
                    quad.transform.SetParent(flames, false);
                    var r = quad.GetComponent<MeshRenderer>();
                    Finish(r, flameMat);
                    flameBits.Add((r, (float)random.NextDouble(), (float)random.NextDouble() * 360f, (float)random.NextDouble() * 2f - 1f,
                        0.9f + (float)random.NextDouble() * 0.7f, 0.04f + (float)random.NextDouble() * 0.025f, i % 3 == 0));
                }
            }
            flames.gameObject.SetActive(true);
            flames.SetLocalPositionAndRotation(at, Quaternion.identity);
            Color hue = knife.model switch
            {
                KnifeModel.HollowMoon => new Color(0.85f, 0.05f, 0.04f),
                KnifeModel.Tidebreaker => new Color(0.1f, 0.35f, 0.95f),
                _ => new Color(0.5f, 0.1f, 0.95f),
            };
            Vector3 along = handle * Vector3.up;
            Vector3 side = Vector3.Cross(along, Vector3.forward).normalized;
            Vector3 side2 = Vector3.Cross(along, side);
            foreach (var (r, phase, angle, spread, speed, size, ember) in flameBits)
            {
                float u = Mathf.Repeat(time * speed + phase, 1f);
                float a = angle * Mathf.Deg2Rad;
                Vector3 p = along * (spread * 0.04f) + (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * 0.01f + Vector3.up * (0.006f + u * 0.05f);
                // Facing the camera (the rig looks down +Z), leaning a little as it rises
                r.transform.SetLocalPositionAndRotation(p, Quaternion.Euler(0f, 0f, Mathf.Sin(time * 3f + phase * 9f) * 12f));
                float scale = size * (0.6f + u * 0.7f) * strength;
                r.transform.localScale = new Vector3(scale, scale * 1.3f, scale);
                // Frames: tongues for embers, wispy fire for the black flames, flickering along
                int frame = ember ? 4 + (int)(time * 12f + phase * 7f) % 2 : (int)(time * 10f + phase * 13f) % 4;
                float alpha = Mathf.Sin(u * Mathf.PI) * (ember ? 0.9f : 0.95f);
                flameProps.SetTexture("_BaseMap", flameFrames[frame]);
                flameProps.SetTexture("_EmissionMap", flameFrames[frame]);
                if (ember)
                {
                    flameProps.SetColor("_BaseColor", new Color(hue.r, hue.g, hue.b, alpha));
                    flameProps.SetColor("_EmissionColor", hue * 1.4f);
                }
                else
                {
                    flameProps.SetColor("_BaseColor", new Color(0.02f, 0.01f, 0.01f, alpha));
                    flameProps.SetColor("_EmissionColor", hue * 0.08f);
                }
                r.SetPropertyBlock(flameProps);
            }
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
            public Renderer renderer;
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
                foreach (var r in arm.root.GetComponentsInChildren<Renderer>(true))
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
            // Plain alpha blending, no premultiply: the same keywords a saved material keeps, so
            // builds (which strip shader variants no material uses) still have this one
            m.SetFloat("_BlendModePreserveSpecular", 0f);
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
