using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // The real-model Void weapons each have an inspect and a draw of their own, made to fit
    // the weapon and show it off: few spins, slow moves that bring the blade into the light and
    // tilt it so its colours and details read (a knife's spins are about its own axes: x end
    // over end, y rolling over to show the other side, z turning in the flat of the blade):
    //  - Gold Skull Glory Sword: a royal presentation, rolled over to show the skull, raised high
    //  - Desolate Devil Scythe: a reaping sweep across and back, then whirled overhead
    //  - Bloody Rose Sword: offered like a rose, cradled in the left palm, leant in the light
    //  - Abyssal Heart: the runic heart brought up close and turned, then let rise off the palm
    //  - Demonic Twinblades: crossed slowly, drawn apart to show both flats, crossed back
    //  - Sword of Golden Blood: the left palm drawn along the blade, then raised into the light
    //  - Da Vinci's Sword: inspected like a mechanism, a few ticking degrees at a time
    //  - Jade Sword: slow flowing arcs, balanced on the open palm
    //  - Shattered Crystal Sword: held up to the light, glinting, passed across to the left
    //  - Demon Sword: raised upright, the point lowered at you, one slow cut
    //  - Soulsucker: held close and still, breathing, the green light turned to you, let hang
    //  - Lance of the Primordials: levelled and pushed out, raised against the sky, laid across
    //  - Gradient Fantasy Sword: flicked up into three cartwheels and caught, shown off
    //  - Cyber Blade: slid level across the eye so the light runs down the edge, one flick
    //  - Divine Reaper: raised so the crescent frames the view, turned, one slow sweep
    //  - Squid Dagger: flipped once over, shown close, handed across and back
    //  - Autumn Sword: held up into the light and rocked like a branch in the wind
    //  - the Void knives: a slow gust of a turn (Ice Cyclone), a glinting pass hand to hand
    //    (Arcane Crystal), the hook hung claw-like from the fist (the karambits), a lit edge slid
    //    under the eye (Cyberpunk), aimed and tossed (Kunai), weaving like smoke (Fel Whisper),
    //    fingertips down the crystal (Tidal)
    public partial class ViewModel
    {
        static readonly Vector3 ShowAt = new(0.065f, -0.135f, 0.3f), RaiseAt = new(0.09f, -0.035f, 0.35f);
        // (the fixed blades' show, leant over so these long blades stay on screen)
        static readonly Quaternion ShowA = Quaternion.Euler(0f, 0f, 32f) * FB(-0.7f, 0.3f, 0.6f, 0.594f, -0.06f, 0.723f), ShowB = Quaternion.Euler(0f, 0f, 26f) * FB(-0.7f, 0.1f, 0.7f, 0.679f, 0.175f, 0.654f);
        static readonly Quaternion RaiseQ = FB(-0.2f, 0.9f, 0.4f, 0.1f, 0.2f, -1f);
        static readonly Quaternion SweepQ = FB(-1f, 0.2f, 0.4f, 0f, 1f, -0.2f);
        static readonly Quaternion LeftCatchQ = FB(0.3f, 0.9f, 0.4f, -0.1f, 0.2f, -1f);

        Routine VoidModelRoutine(KnifeModel model)
        {
            string asset = Skins.Knives[knifeSkin].asset ?? "";
            return asset.Substring(0, Mathf.Min(2, asset.Length)) switch
            {
                "01" => RoyalRoutine(),
                "02" => ReapRoutine(),
                "03" => RoseRoutine(),
                "05" => HeartRoutine(),
                "06" => TwinShowRoutine(),
                "07" => OathRoutine(),
                "08" => MechanismRoutine(),
                "09" => FlowRoutine(),
                "10" => CrystalPassRoutine(),
                "11" => MenaceRoutine(),
                "12" => SoulRoutine(),
                "13" => SpearRoutine(),
                "14" => CartwheelRoutine(),
                "15" => EdgeRoutine(),
                "16" => HaloRoutine(),
                "17" => SmallShowRoutine(),
                "18" => BreezeRoutine(),
                "28" => CycloneRoutine(),
                "29" => GlintRoutine(),
                "30" => ClawRoutine(),
                "35" => CrimsonRoutine(),
                "31" => CircuitRoutine(),
                "32" => KunaiRoutine(),
                "33" => WhisperRoutine(),
                "34" => TideRoutine(),
                _ => model == KnifeModel.ModelScythe ? ReaperRoutine() : SaberRoutine(),
            };
        }

        static Key Show(Key k, float t, Quaternion q, Vector3 at)
        {
            k = k.At(t); k.rp = at; k.rq = q; return k;
        }

        static Routine Done(List<Key> ks, float sustainAt, int axis, float speed, params (float, AudioClip, float)[] sounds) =>
            new() { keys = ks.ToArray(), sustainAt = sustainAt, sustainAxis = axis, sustainSpeed = speed, sounds = sounds };

        static Key Home(Key k, float t)
        {
            k = k.At(t); k.hold = Hold.Right; k.rp = RightIdle; k.rq = ForwardIdle; k.lp = LeftIdle; k.lq = LeftIdleRotation;
            k.rOpen = 0f; k.lOpen = 0f; k.rKeep = false; return k;
        }

        // Gold Skull Glory Sword
        static Routine RoyalRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.4f, ShowA, ShowAt); k.rOpen = 0.1f; ks.Add(k);
            k = k.At(0.9f); k.rq = ShowB; ks.Add(k);
            k = k.At(1.35f); k.spin = new(0f, 180f, 0f); ks.Add(k);             // the skull's side
            k = k.At(1.8f); k.rp = ShowAt + new Vector3(0.005f, 0.01f, 0f); ks.Add(k);
            k = Show(k, 2.25f, RaiseQ, RaiseAt); k.spin = new(0f, 360f, 0f); k.rOpen = 0f; ks.Add(k); // raised high, presented
            k = k.At(2.6f); k.rp = RaiseAt + new Vector3(0f, 0.01f, 0f); ks.Add(k);
            ks.Add(Home(k, 3.15f));
            return Done(ks, 0.9f, 0, 0f, (0.4f, WeaponSounds.Tick, 0.35f), (1.3f, WeaponSounds.Tick, 0.3f), (2.15f, WeaponSounds.Slash, 0.35f));
        }

        // Desolate Devil Scythe
        static Routine ReapRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.35f); k.rp = new(0.16f, -0.06f, 0.32f); k.rq = RightUp; k.spin = new(0f, 0f, -60f); k.lp = LeftIdle + LeftHandAway; ks.Add(k); // drawn back
            k = k.At(0.75f); k.rp = new(0.0f, -0.08f, 0.37f); k.rq = SweepQ; k.spin = new(0f, 0f, 120f); ks.Add(k);   // the reaping sweep
            k = k.At(0.95f); k.rp = new(-0.02f, -0.085f, 0.37f); ks.Add(k);
            k = k.At(1.45f); k.rp = new(0.15f, -0.05f, 0.32f); k.rq = RightUp; k.spin = new(0f, 0f, 0f); ks.Add(k);    // and back
            k = RaiseKey(k, 1.8f); k.spin = new(0f, 0f, 360f); ks.Add(k);                                               // whirled overhead
            k = k.At(2.8f); k.spin = new(0f, 0f, 720f); ks.Add(k);
            ks.Add(Home(k, 3.4f));
            return Done(ks, 2.2f, 1, 360f, (0.6f, WeaponSounds.Slash, 0.6f), (1.3f, WeaponSounds.Slash, 0.45f), (2.0f, WeaponSounds.Slash, 0.4f));
        }

        // Jade Sword
        static Routine FlowRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            Vector3[] path = { new(0.08f, -0.1f, 0.34f), new(0.03f, -0.12f, 0.36f), new(0.06f, -0.145f, 0.33f), new(0.1f, -0.12f, 0.31f) };
            for (int i = 0; i < path.Length; i++)            // slow arcs, the blade turning with them
            {
                k = k.At(0.5f + i * 0.4f); k.rp = path[i]; k.rq = i % 2 == 0 ? ShowA : ShowB; k.spin = new(0f, 0f, 90f * (i + 1)); ks.Add(k);
            }
            k = k.At(2.4f); k.hold = Hold.Air; k.ap = new(0.06f, -0.075f, 0.33f); k.aq = Quaternion.Euler(0f, 0f, 90f); k.spin = new(0f, 0f, 360f);
            k.rp = ShowAt; k.rq = ShowA; k.rOpen = 0.9f; ks.Add(k);                           // balanced on the open palm
            k = k.At(2.8f); ks.Add(k);
            k = k.At(3.05f); k.hold = Hold.Right; k.rOpen = 0f; k.spin = new(0f, 0f, 360f); ks.Add(k);
            ks.Add(Home(k, 3.55f));
            return Done(ks, 2.6f, 0, 0f, (0.6f, WeaponSounds.Slash, 0.15f), (1.4f, WeaponSounds.Slash, 0.15f), (3.0f, WeaponSounds.Tick, 0.3f));
        }

        // Gradient Fantasy Sword
        static Routine CartwheelRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.3f); k.rp = RightIdle + new Vector3(0f, -0.015f, 0f); ks.Add(k);
            k = k.At(1.15f); k.hold = Hold.Air; k.ap = new(0.04f, 0.06f, 0.42f); k.aq = Quaternion.identity; k.spin = new(1080f, 0f, 0f);
            k.rp = new(0.1f, -0.05f, 0.33f); k.rq = RightUp; k.rOpen = 0.6f; ks.Add(k);                       // three cartwheels
            k = k.At(1.45f); k.hold = Hold.Right; k.rOpen = 0f; ks.Add(k);
            k = Show(k, 1.95f, ShowA, ShowAt); ks.Add(k);
            k = k.At(2.35f); k.rq = ShowB; k.spin = new(1080f, 180f, 0f); ks.Add(k);
            k = Home(k, 2.9f); k.spin = new(1080f, 360f, 0f); ks.Add(k);
            return Done(ks, 2.0f, 0, 0f, (0.35f, WeaponSounds.Slash, 0.4f), (1.45f, WeaponSounds.Tick, 0.5f), (2.3f, WeaponSounds.Tick, 0.25f));
        }

        // Turns a hold about the view's own axes: lean (in the screen's plane), turn (the flat
        // swings toward or away from the light), pitch (the point dips toward you or away)
        static Quaternion Lean(Quaternion q, float degrees) => Quaternion.AngleAxis(degrees, Vector3.forward) * q;
        static Quaternion TurnQ(Quaternion q, float degrees) => Quaternion.AngleAxis(degrees, Vector3.up) * q;
        static Quaternion Pitch(Quaternion q, float degrees) => Quaternion.AngleAxis(degrees, Vector3.right) * q;
        // The left hand's mirror of the show: the flat of its blade toward you
        static readonly Quaternion LeftShowA = Quaternion.Euler(0f, 0f, -32f) * FB(0.7f, 0.3f, 0.6f, -0.594f, -0.06f, 0.723f);
        static readonly Vector3 LeftShowAt = new(-0.065f, -0.135f, 0.3f);
        // Level across the view: the raised hand already holds the blade level (turned 75
        // degrees in its flat, spin z, it stands upright instead)
        static readonly Vector3 LevelSpin = Vector3.zero, UprightSpin = new(0f, 0f, 75f);

        // Bloody Rose Sword: offered like a rose, the left palm cradling the flat, held up into
        // the light and leant one way and the other so the red runs along the blade, rolled once
        // to show the other face
        static Routine RoseRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.5f, ShowA, ShowAt); k.rOpen = 0.1f; ks.Add(k);
            k = k.At(0.95f); k.lp = new(-0.01f, -0.15f, 0.31f); k.lq = LeftCatchQ; k.lOpen = 0.85f; ks.Add(k);     // cradled
            k = k.At(1.6f); k.rp = ShowAt + new Vector3(-0.005f, 0.02f, 0.01f); k.rq = Lean(ShowA, -14f); k.lp = new(-0.005f, -0.13f, 0.32f); ks.Add(k); // offered up
            k = k.At(2.15f); k.rq = Lean(TurnQ(ShowA, 18f), 6f); ks.Add(k);
            k = k.At(2.45f); k.lp = LeftIdle; k.lq = LeftIdleRotation; k.lOpen = 0f; ks.Add(k);
            k = k.At(2.95f); k.spin = new(0f, 180f, 0f); k.rq = ShowB; ks.Add(k);                                  // the other face
            k = k.At(3.3f); ks.Add(k);
            k = Home(k, 3.85f); k.spin = new(0f, 360f, 0f); ks.Add(k);
            return Done(ks, 1.6f, 0, 0f, (0.5f, WeaponSounds.Unsheathe, 0.3f), (0.95f, WeaponSounds.Tick, 0.25f), (2.9f, WeaponSounds.Tick, 0.3f));
        }

        // Abyssal Heart: brought up close so the runic heart at the guard fills the view, turned
        // slowly in the light, then let rise a moment above the open palm, still, glowing
        static Routine HeartRoutine()
        {
            var close = new Vector3(0.05f, -0.1f, 0.27f);
            var above = new Vector3(0.05f, -0.075f, 0.33f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.55f, ShowA, close); ks.Add(k);                                                          // the heart up close
            k = k.At(1.25f); k.rq = TurnQ(ShowA, 22f); ks.Add(k);
            k = k.At(1.9f); k.rq = TurnQ(Lean(ShowA, -10f), -16f); ks.Add(k);
            k = k.At(2.35f); k.hold = Hold.Air; k.ap = above; k.aq = Quaternion.Euler(0f, 0f, 8f); k.rp = ShowAt; k.rq = ShowA; k.rOpen = 0.9f; ks.Add(k); // rising off the palm
            k = k.At(2.85f); k.ap = above + new Vector3(0f, 0.012f, 0f); k.aq = Quaternion.Euler(0f, 0f, -6f); ks.Add(k);
            k = k.At(3.15f); k.hold = Hold.Right; k.rOpen = 0f; ks.Add(k);
            ks.Add(Home(k, 3.65f));
            return Done(ks, 1.9f, 0, 0f, (0.55f, WeaponSounds.Tick, 0.25f), (2.35f, WeaponSounds.Unsheathe, 0.2f), (3.15f, WeaponSounds.Tick, 0.4f));
        }

        // Demonic Twinblades: both blades brought up and crossed slowly, flats to you, then drawn
        // apart edge along edge, each hand showing its blade, and crossed back to the hold
        static Routine TwinShowRoutine()
        {
            Quaternion rIn = FB(-0.6f, 0.6f, 0.6f, 0.5f, 0.3f, -0.7f), lIn = FB(0.6f, 0.6f, 0.6f, -0.5f, 0.3f, -0.7f);
            var ks = new List<Key>();
            var k = IdleKey(false); k.lp = DualLeftIdle; k.lq = DualLeftRotation; ks.Add(k);
            k = k.At(0.55f); k.rp = new(0.035f, -0.095f, 0.33f); k.rq = rIn; k.lp = new(-0.035f, -0.095f, 0.33f); k.lq = lIn; ks.Add(k); // crossed
            k = k.At(1.0f); k.rp = new(0.03f, -0.088f, 0.33f); k.lp = new(-0.03f, -0.088f, 0.33f); ks.Add(k);
            k = k.At(1.75f); k.rp = new(0.11f, -0.115f, 0.31f); k.rq = ShowA; k.lp = new(-0.11f, -0.115f, 0.31f); k.lq = LeftShowA; ks.Add(k); // drawn apart, both shown
            k = k.At(2.35f); k.rq = Lean(ShowA, 10f); k.lq = Lean(LeftShowA, -10f); ks.Add(k);
            k = k.At(2.85f); k.rp = new(0.035f, -0.095f, 0.33f); k.rq = rIn; k.lp = new(-0.035f, -0.095f, 0.33f); k.lq = lIn; ks.Add(k);
            k = Home(k, 3.35f); k.lp = DualLeftIdle; k.lq = DualLeftRotation; ks.Add(k);
            var r = Done(ks, 1.75f, 0, 0f, (1.0f, WeaponSounds.Tick, 0.5f), (1.6f, WeaponSounds.Unsheathe, 0.35f), (2.85f, WeaponSounds.Tick, 0.5f));
            r.dualArms = true;
            return r;
        }

        // Sword of Golden Blood: the left palm drawn slowly along the blade, then raised high and
        // turned so the gold and crimson catch the light
        static Routine OathRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, ShowA, ShowAt); ks.Add(k);
            k = k.At(0.8f); k.lp = new(-0.02f, -0.15f, 0.31f); k.lq = LeftCatchQ; k.lOpen = 0.8f; ks.Add(k);     // the left palm on the blade...
            k = k.At(1.6f); k.lp = new(0.005f, -0.1f, 0.33f); k.rq = ShowB; ks.Add(k);                           // ...drawn along it
            k = k.At(1.9f); k.lp = LeftIdle; k.lq = LeftIdleRotation; k.lOpen = 0f; ks.Add(k);
            k = Show(k, 2.45f, RaiseQ, RaiseAt); ks.Add(k);                                                     // raised high
            k = k.At(2.95f); k.rq = TurnQ(RaiseQ, 25f); ks.Add(k);
            k = k.At(3.3f); k.rq = TurnQ(RaiseQ, -15f); ks.Add(k);
            ks.Add(Home(k, 3.8f));
            return Done(ks, 1.6f, 0, 0f, (0.8f, WeaponSounds.Tick, 0.3f), (1.5f, WeaponSounds.Unsheathe, 0.35f), (2.45f, WeaponSounds.Slash, 0.25f));
        }

        // Da Vinci's Sword: inspected like a mechanism: brought close and turned a few degrees at
        // a time, each turn ticking into place, a look along the edge, then a nod and away
        static Routine MechanismRoutine()
        {
            var close = new Vector3(0.055f, -0.11f, 0.28f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, ShowA, close); ks.Add(k);
            var sounds = new List<(float, AudioClip, float)>();
            float[] turns = { 14f, 28f, 14f, -8f, -22f };
            float t = 0.75f;
            foreach (float a in turns)                                     // a few degrees at a time, ticking
            {
                k = k.At(t); ks.Add(k);
                t += 0.14f; k = k.At(t); k.rq = TurnQ(ShowA, a); ks.Add(k);
                sounds.Add((t - 0.02f, WeaponSounds.Tick, 0.45f));
                t += 0.22f;
            }
            k = k.At(t + 0.4f); k.rq = Pitch(TurnQ(ShowA, 70f), -10f); ks.Add(k);   // along the edge
            k = k.At(t + 0.8f); ks.Add(k);
            ks.Add(Home(k, t + 1.35f));
            return Done(ks, 0.75f, 0, 0f, sounds.ToArray());
        }

        // Shattered Crystal Sword: held up so the light runs through the crystal, tilted to glint,
        // tossed gently across (no spin) to the left hand, which shows the other side, and back
        static Routine CrystalPassRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, RaiseQ, RaiseAt); ks.Add(k);
            k = k.At(1.0f); k.rq = Lean(RaiseQ, -12f); ks.Add(k);
            k = k.At(1.45f); k.rq = Lean(RaiseQ, 10f); ks.Add(k);
            k = k.At(1.8f); k.hold = Hold.Air; k.ap = new(0.0f, -0.04f, 0.36f); k.aq = Quaternion.Euler(0f, 0f, 20f); k.lp = new(-0.07f, -0.1f, 0.32f); k.lq = LeftCatchQ; ks.Add(k); // across
            k = k.At(2.05f); k.hold = Hold.Left; k.lp = LeftShowAt; k.lq = LeftShowA; ks.Add(k);
            k = k.At(2.6f); k.lq = Lean(LeftShowA, -12f); k.rp = RightIdle; k.rq = ForwardIdle; ks.Add(k);
            k = k.At(2.95f); k.hold = Hold.Air; k.ap = new(0.02f, -0.06f, 0.34f); k.aq = Quaternion.Euler(0f, 0f, -10f); k.rp = ShowAt; k.rq = ShowA; ks.Add(k);
            k = k.At(3.2f); k.hold = Hold.Right; k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            ks.Add(Home(k, 3.65f));
            return Done(ks, 1.0f, 0, 0f, (0.45f, WeaponSounds.Unsheathe, 0.3f), (1.8f, WeaponSounds.Slash, 0.2f), (2.05f, WeaponSounds.Tick, 0.4f), (3.2f, WeaponSounds.Tick, 0.4f));
        }

        // Demon Sword: raised slowly upright before you, then the point lowered toward you, held
        // there a breath, and a single slow cut across before it's put away
        static Routine MenaceRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.6f, RaiseQ, RaiseAt + new Vector3(-0.03f, 0f, 0f)); k.spin = UprightSpin; ks.Add(k); // upright before you
            k = k.At(1.3f); k.rq = Pitch(RaiseQ, -55f); k.rp = new(0.06f, -0.075f, 0.32f); k.spin = Vector3.zero; ks.Add(k); // the point lowered at you
            k = k.At(1.85f); k.rp = new(0.06f, -0.073f, 0.31f); ks.Add(k);
            k = k.At(2.2f); k.rp = new(0.14f, -0.06f, 0.33f); k.rq = RightUp; ks.Add(k);                       // drawn back...
            k = k.At(2.55f); k.rp = new(-0.01f, -0.11f, 0.36f); k.rq = SweepQ; ks.Add(k);                       // ...one slow cut
            k = k.At(2.8f); k.rp = new(-0.015f, -0.115f, 0.36f); ks.Add(k);
            ks.Add(Home(k, 3.35f));
            return Done(ks, 1.3f, 0, 0f, (0.6f, WeaponSounds.Unsheathe, 0.3f), (2.35f, WeaponSounds.Slash, 0.55f));
        }

        // Soulsucker: brought up close and held still, breathing, the green light in the blade
        // turned toward you, then let hang in the air a moment before it's pulled back
        static Routine SoulRoutine()
        {
            var close = new Vector3(0.07f, -0.07f, 0.31f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.55f, RaiseQ, close); ks.Add(k);
            k = k.At(1.05f); k.rp = close + new Vector3(0f, 0.004f, 0f); k.rq = TurnQ(RaiseQ, 20f); ks.Add(k);   // breathing, turned to you
            k = k.At(1.55f); k.rp = close; k.rq = TurnQ(RaiseQ, -14f); ks.Add(k);
            k = k.At(2.05f); k.hold = Hold.Air; k.ap = new(0.04f, -0.05f, 0.34f); k.aq = Quaternion.Euler(0f, 0f, 6f); k.rp = close + new Vector3(0f, -0.03f, 0f); k.rOpen = 0.9f; ks.Add(k); // let hang
            k = k.At(2.6f); k.ap = new(0.04f, -0.043f, 0.34f); k.aq = Quaternion.Euler(0f, 0f, -4f); ks.Add(k);
            k = k.At(2.9f); k.hold = Hold.Right; k.rp = close; k.rOpen = 0f; ks.Add(k);                                // pulled back
            ks.Add(Home(k, 3.4f));
            return Done(ks, 1.55f, 0, 0f, (0.55f, WeaponSounds.Tick, 0.25f), (2.05f, WeaponSounds.Unsheathe, 0.2f), (2.9f, WeaponSounds.Tick, 0.45f));
        }

        // Lance of the Primordials: levelled like a spear and pushed slowly out, then raised so the
        // head stands against the sky, and laid across the view so its whole length shows
        static Routine SpearRoutine()
        {
            Quaternion levelled = FB(-0.15f, 0.35f, 1f, 0.6f, 0.6f, -0.45f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.45f); k.rp = new(0.075f, -0.095f, 0.33f); k.rq = levelled; ks.Add(k);
            k = k.At(1.05f); k.rp = new(0.065f, -0.09f, 0.42f); ks.Add(k);                                      // pushed out
            k = k.At(1.35f); k.rp = new(0.075f, -0.095f, 0.33f); ks.Add(k);
            k = Show(k, 1.95f, RaiseQ, RaiseAt); ks.Add(k);                                                     // the head against the sky
            k = k.At(2.35f); k.rq = TurnQ(RaiseQ, 20f); ks.Add(k);
            k = k.At(2.9f); k.rp = RaiseAt + new Vector3(0.02f, -0.02f, 0f); k.spin = LevelSpin; ks.Add(k);   // laid across
            k = k.At(3.25f); ks.Add(k);
            k = Home(k, 3.75f); k.spin = Vector3.zero; ks.Add(k);
            return Done(ks, 1.05f, 0, 0f, (0.95f, WeaponSounds.Slash, 0.35f), (1.95f, WeaponSounds.Tick, 0.3f), (2.9f, WeaponSounds.Tick, 0.3f));
        }

        // Cyber Blade: drawn level across the eye and slid slowly through the view so the light
        // runs down the edge, one sharp flick to clear it, and home
        static Routine EdgeRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.4f); k.rp = RaiseAt + new Vector3(0.03f, 0f, 0f); ks.Add(k);   // level at the eye
            k = k.At(1.5f); k.rp = RaiseAt + new Vector3(-0.05f, -0.01f, 0f); ks.Add(k);                                // slid through the view
            k = k.At(1.8f); k.rp = RaiseAt + new Vector3(-0.045f, -0.008f, 0f); ks.Add(k);
            k = k.At(1.95f); k.rp = new(0.12f, -0.1f, 0.33f); k.rq = RightUp; k.spin = new(0f, 0f, -40f); ks.Add(k);   // the flick
            k = k.At(2.35f); k.rp = new(0.118f, -0.098f, 0.33f); ks.Add(k);
            k = Home(k, 2.85f); k.spin = Vector3.zero; ks.Add(k);
            return Done(ks, 1.5f, 0, 0f, (0.4f, WeaponSounds.Unsheathe, 0.3f), (1.9f, WeaponSounds.Slash, 0.7f));
        }

        // Divine Reaper: raised high so the crescent frames the view, turned slowly in the light,
        // brought down in one slow sweep low across, home
        static Routine HaloRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.6f); k.rp = RaiseAt + new Vector3(-0.02f, 0.02f, 0f); ks.Add(k);   // raised, the crescent framing the view
            k = k.At(1.3f); k.rq = TurnQ(RaiseQ, 30f); ks.Add(k);
            k = k.At(1.95f); k.rq = TurnQ(RaiseQ, -20f); ks.Add(k);
            k = k.At(2.3f); k.rp = new(0.16f, -0.06f, 0.32f); k.rq = RightUp; ks.Add(k);
            k = k.At(2.85f); k.rp = new(0.0f, -0.09f, 0.37f); k.rq = SweepQ; ks.Add(k);           // one slow sweep low
            k = k.At(3.1f); k.rp = new(-0.01f, -0.092f, 0.37f); ks.Add(k);
            ks.Add(Home(k, 3.6f));
            return Done(ks, 1.3f, 0, 0f, (0.6f, WeaponSounds.Unsheathe, 0.3f), (2.6f, WeaponSounds.Slash, 0.5f));
        }

        // Squid Dagger: flipped once over in the fingers, shown up close, handed slowly across to
        // the left hand, which tilts it to the light, and back
        static Routine SmallShowRoutine()
        {
            var close = new Vector3(0.06f, -0.115f, 0.28f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.35f); k.rp = new(0.085f, -0.1f, 0.3f); k.rOpen = 0.35f; ks.Add(k);
            k = k.At(0.65f); k.spin = new(0f, 180f, 0f); ks.Add(k);                                 // flipped once over
            k = k.At(0.85f); k.rOpen = 0f; ks.Add(k);
            k = Show(k, 1.3f, ShowA, close); ks.Add(k);
            k = k.At(1.75f); k.rq = TurnQ(ShowA, 20f); ks.Add(k);
            k = k.At(2.1f); k.hold = Hold.Left; k.lp = new(-0.03f, -0.12f, 0.3f); k.lq = LeftShowA; k.rp = RightIdle; k.rq = ForwardIdle; ks.Add(k); // to the left
            k = k.At(2.55f); k.lq = TurnQ(LeftShowA, -20f); ks.Add(k);
            k = k.At(2.9f); k.hold = Hold.Right; k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            k = Home(k, 3.3f); k.spin = new(0f, 360f, 0f); ks.Add(k);
            return Done(ks, 1.75f, 0, 0f, (0.6f, WeaponSounds.Tick, 0.35f), (2.1f, WeaponSounds.Tick, 0.35f), (2.9f, WeaponSounds.Tick, 0.35f));
        }

        // Autumn Sword: held up like into an evening sun and rocked gently, as a branch in the
        // wind, then lowered across the view, its warm colours running along it
        static Routine BreezeRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.55f, ShowA, ShowAt + new Vector3(0f, 0.015f, 0f)); ks.Add(k);
            float[] rock = { -12f, 9f, -6f, 3f };
            for (int i = 0; i < rock.Length; i++)
            {
                k = k.At(1.0f + i * 0.4f); k.rq = Lean(ShowA, rock[i]); k.rp = ShowAt + new Vector3(rock[i] * 0.0006f, 0.015f, 0f); ks.Add(k);
            }
            k = Show(k, 2.85f, RaiseQ, RaiseAt + new Vector3(0.02f, -0.02f, 0f)); k.spin = LevelSpin; ks.Add(k); // lowered across
            k = k.At(3.2f); ks.Add(k);
            k = Home(k, 3.7f); k.spin = Vector3.zero; ks.Add(k);
            return Done(ks, 1.0f, 0, 0f, (0.55f, WeaponSounds.Unsheathe, 0.25f), (2.85f, WeaponSounds.Tick, 0.3f));
        }

        // ---- the Void knives

        // Ice Cyclone Blade: held up so the frost runs down the twisted blade, then one slow turn
        // in the flat, like a gust, and a look along the twist
        static Routine CycloneRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, ShowA, ShowAt); ks.Add(k);
            k = k.At(1.0f); k.rq = TurnQ(ShowA, 18f); ks.Add(k);
            k = k.At(1.9f); k.spin = new(0f, 0f, 360f); k.rq = ShowA; ks.Add(k);                   // one slow turn, a gust
            k = k.At(2.5f); k.rq = Pitch(TurnQ(ShowA, 60f), -8f); ks.Add(k);                       // along the twist
            k = k.At(2.85f); ks.Add(k);
            k = Home(k, 3.35f); k.spin = new(0f, 0f, 360f); ks.Add(k);
            return Done(ks, 1.0f, 0, 0f, (0.45f, WeaponSounds.Tick, 0.3f), (1.2f, WeaponSounds.Slash, 0.25f));
        }

        // Arcane Crystal Dagger: turned in the light so the crystal glints, tossed low (no spin)
        // to the left hand, shown on that side, and back
        static Routine GlintRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, ShowA, ShowAt); ks.Add(k);
            k = k.At(0.9f); k.rq = TurnQ(ShowA, 25f); ks.Add(k);
            k = k.At(1.35f); k.rq = TurnQ(ShowA, -18f); ks.Add(k);
            k = k.At(1.65f); k.hold = Hold.Air; k.ap = new(0.0f, -0.07f, 0.33f); k.aq = Quaternion.Euler(0f, 0f, 15f); k.lp = new(-0.06f, -0.11f, 0.31f); k.lq = LeftCatchQ; ks.Add(k);
            k = k.At(1.9f); k.hold = Hold.Left; k.lp = LeftShowAt; k.lq = LeftShowA; k.rp = RightIdle; k.rq = ForwardIdle; ks.Add(k);
            k = k.At(2.4f); k.lq = TurnQ(LeftShowA, -22f); ks.Add(k);
            k = k.At(2.7f); k.hold = Hold.Air; k.ap = new(0.02f, -0.08f, 0.32f); k.aq = Quaternion.identity; k.rp = ShowAt; k.rq = ShowA; ks.Add(k);
            k = k.At(2.95f); k.hold = Hold.Right; k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            ks.Add(Home(k, 3.4f));
            return Done(ks, 0.9f, 0, 0f, (0.45f, WeaponSounds.Unsheathe, 0.25f), (1.9f, WeaponSounds.Tick, 0.4f), (2.95f, WeaponSounds.Tick, 0.4f));
        }

        // Karambits: the hand turns over so the curve hangs from the fist, claw-like, the colour
        // all along the hook, then rolls back, a slow pull across like a claw drawn through
        static Routine ClawRoutine()
        {
            Quaternion over = FB(-0.5f, 0.15f, 0.85f, 0.1f, 1f, 0.2f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.5f, over, new Vector3(0.075f, -0.1f, 0.3f)); ks.Add(k);                   // turned over, the hook hanging
            k = k.At(1.1f); k.rq = TurnQ(over, 25f); ks.Add(k);
            k = k.At(1.6f); k.rq = Lean(TurnQ(over, -15f), 10f); ks.Add(k);
            k = Show(k, 2.1f, ShowA, ShowAt); ks.Add(k);                                             // the flat of the hook
            k = k.At(2.5f); k.rp = new(0.13f, -0.08f, 0.33f); k.rq = RightUp; ks.Add(k);
            k = k.At(2.85f); k.rp = new(0.0f, -0.12f, 0.35f); k.rq = SweepQ; ks.Add(k);             // drawn through like a claw
            k = k.At(3.05f); ks.Add(k);
            ks.Add(Home(k, 3.5f));
            return Done(ks, 1.1f, 0, 0f, (0.5f, WeaponSounds.Tick, 0.3f), (2.65f, WeaponSounds.Slash, 0.45f));
        }

        // Cyberpunk Knife: brought close so its lit edge reads, slid along under the eye, flipped
        // once to show the circuitry on the other side
        static Routine CircuitRoutine()
        {
            var close = new Vector3(0.06f, -0.11f, 0.28f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, ShowA, close + new Vector3(0.03f, 0f, 0f)); ks.Add(k);
            k = k.At(1.3f); k.rp = close + new Vector3(-0.03f, 0.005f, 0f); k.rq = TurnQ(ShowA, 12f); ks.Add(k);   // slid along
            k = k.At(1.7f); k.spin = new(0f, 180f, 0f); ks.Add(k);                                                  // the other side
            k = k.At(2.3f); k.rp = close; k.rq = TurnQ(ShowA, -12f); ks.Add(k);
            k = Home(k, 2.85f); k.spin = new(0f, 360f, 0f); ks.Add(k);
            return Done(ks, 1.3f, 0, 0f, (0.45f, WeaponSounds.Tick, 0.3f), (1.65f, WeaponSounds.Tick, 0.35f));
        }

        // Miraigata Kunai: raised between the fingers ready to throw, aimed, tossed straight up a
        // little and caught by the ring, aimed again
        static Routine KunaiRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, RaiseQ, RaiseAt); ks.Add(k);                                          // ready to throw
            k = k.At(0.95f); k.rq = Pitch(RaiseQ, -40f); k.rp = RaiseAt + new Vector3(0f, -0.02f, 0f); ks.Add(k);   // aimed
            k = k.At(1.4f); ks.Add(k);
            k = k.At(1.75f); k.hold = Hold.Air; k.ap = new(0.07f, -0.02f, 0.35f); k.aq = Quaternion.identity; k.rp = RaiseAt + new Vector3(0f, -0.03f, 0f); k.rq = RaiseQ; k.rOpen = 0.7f; ks.Add(k); // tossed up
            k = k.At(2.05f); k.hold = Hold.Right; k.rOpen = 0f; ks.Add(k);
            k = k.At(2.45f); k.rq = Pitch(RaiseQ, -40f); ks.Add(k);
            k = k.At(2.75f); ks.Add(k);
            ks.Add(Home(k, 3.2f));
            return Done(ks, 1.4f, 0, 0f, (0.45f, WeaponSounds.Tick, 0.3f), (1.7f, WeaponSounds.Slash, 0.2f), (2.05f, WeaponSounds.Tick, 0.45f));
        }

        // Fel Whisper: the curved green blade weaving slowly side to side like smoke, turned so
        // its glow faces you, then still
        static Routine WhisperRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, ShowA, ShowAt); ks.Add(k);
            Vector3[] weave = { new(0.03f, -0.12f, 0.31f), new(0.09f, -0.125f, 0.3f), new(0.03f, -0.13f, 0.31f), new(0.07f, -0.125f, 0.3f) };
            for (int i = 0; i < weave.Length; i++)                                                     // weaving like smoke
            {
                k = k.At(0.95f + i * 0.4f); k.rp = weave[i]; k.rq = Lean(ShowA, i % 2 == 0 ? 14f : -10f); ks.Add(k);
            }
            k = k.At(2.8f); k.rp = ShowAt; k.rq = TurnQ(ShowA, 15f); ks.Add(k);
            k = k.At(3.1f); ks.Add(k);
            ks.Add(Home(k, 3.55f));
            return Done(ks, 0.95f, 0, 0f, (0.45f, WeaponSounds.Unsheathe, 0.2f), (1.35f, WeaponSounds.Slash, 0.12f), (2.15f, WeaponSounds.Slash, 0.12f));
        }

        // Tidal Crystal Dagger: presented flat, the left fingertips drawn down the crystal edge,
        // tilted like water catching light
        static Routine TideRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, ShowA, ShowAt); ks.Add(k);
            k = k.At(0.85f); k.lp = new(-0.01f, -0.1f, 0.32f); k.lq = LeftCatchQ; k.lOpen = 0.7f; ks.Add(k);
            k = k.At(1.5f); k.lp = new(0.01f, -0.14f, 0.31f); ks.Add(k);                          // fingertips down the edge
            k = k.At(1.8f); k.lp = LeftIdle; k.lq = LeftIdleRotation; k.lOpen = 0f; ks.Add(k);
            k = k.At(2.2f); k.rq = Lean(TurnQ(ShowA, 20f), -8f); ks.Add(k);                       // catching the light like water
            k = k.At(2.6f); k.rq = Lean(TurnQ(ShowA, -14f), 6f); ks.Add(k);
            ks.Add(Home(k, 3.1f));
            return Done(ks, 1.5f, 0, 0f, (0.85f, WeaponSounds.Tick, 0.25f), (2.2f, WeaponSounds.Unsheathe, 0.2f));
        }

        // Crimson Karambit: once round the back of the hand on the finger ring, caught, and shown
        // turned over, the red curve hanging from the fist
        static Routine CrimsonRoutine()
        {
            Quaternion over = FB(-0.5f, 0.15f, 0.85f, 0.1f, 1f, 0.2f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.35f); k.rp = new(0.1f, -0.08f, 0.32f); k.rOpen = 0.6f; k.rKeep = true; ks.Add(k);
            k = k.At(1.0f); k.orbit = 360f; ks.Add(k);                                               // once round the back of the hand
            k = k.At(1.2f); k.rOpen = 0f; k.rKeep = false; ks.Add(k);
            k = Show(k, 1.75f, over, new Vector3(0.075f, -0.1f, 0.3f)); ks.Add(k);                  // shown turned over
            k = k.At(2.3f); k.rq = TurnQ(over, 22f); ks.Add(k);
            k = Show(k, 2.8f, ShowA, ShowAt); ks.Add(k);
            k = Home(k, 3.3f); k.orbit = 360f; ks.Add(k);
            return Done(ks, 2.3f, 0, 0f, (0.6f, WeaponSounds.Slash, 0.35f), (1.2f, WeaponSounds.Tick, 0.45f));
        }

        // ------------------------------------------------------------------ draws

        // Each Void weapon is called out of the void its own way: the hand's path in (camera
        // space), how the weapon turns as it comes (about its own axes) and how it grows, at
        // a = 0..1 through the draw
        (Vector3 offset, Quaternion turn, float grow) VoidDrawPose(float a)
        {
            string asset = Skins.Knives[knifeSkin].asset ?? "";
            float e = 1f - Mathf.Pow(1f - a, 3f), rest = 1f - e;   // eased in, and what's left
            float grow = a >= 1f ? 1f : Mathf.Max(0.02f, BackOut(a));
            Quaternion Turn(Vector3 axis, float degrees) => Quaternion.AngleAxis(degrees * rest, axis);
            switch (asset.Length >= 2 ? asset.Substring(0, 2) : "")
            {
                case "01": return (new Vector3(0f, -0.1f, 0f) * rest, Turn(Vector3.up, 360f), grow);                       // presented, rising
                case "02": return (new Vector3(0.08f, 0f, 0f) * rest, Turn(Vector3.forward, 720f), grow);                  // swept in, whirling
                case "03": return (new Vector3(0f, 0f, 0.12f) * rest, Turn(Vector3.up, -360f), grow);                      // thrust out
                case "05": return (new Vector3(0f, -0.03f, 0f) * rest, Turn(Vector3.up, 180f), Mathf.SmoothStep(0.02f, 1f, a)); // swells slowly
                case "06": return (new Vector3(-0.03f, 0f, 0f) * rest, Turn(Vector3.forward, 360f), grow);                 // the pair crossing out
                case "07": return (new Vector3(-0.06f, 0.02f, 0f) * rest, Turn(Vector3.forward, -720f), grow);             // from the left
                case "08": return (Vector3.zero, Quaternion.AngleAxis(-90f * Mathf.Ceil(rest * 4f - 0.001f), Vector3.right), grow); // ratcheting in
                case "09":                                                                                                  // a slow arc in from the left
                {
                    float arc = rest * Mathf.PI * 0.5f;
                    return (new Vector3(-0.08f * Mathf.Sin(arc), -0.04f * Mathf.Sin(arc * 2f), 0f), Turn(Vector3.forward, 90f), Mathf.SmoothStep(0.05f, 1f, a));
                }
                case "10":                                                                                                  // assembling, shaking
                {
                    float shake = a < 0.8f ? Mathf.Sin(a * 80f) * 0.006f * (1f - a) : 0f;
                    return (new Vector3(shake, -shake, 0f), Turn(Vector3.up, 180f), grow);
                }
                case "11": return (new Vector3(0f, -0.08f, 0f) * rest, Turn(Vector3.forward, 1080f), grow);                // erupting, spinning
                case "12":                                                                                                  // rising slowly, wobbling
                {
                    float wob = Mathf.Sin(a * 14f) * 12f * rest;
                    return (new Vector3(0f, -0.07f, 0f) * rest, Quaternion.AngleAxis(wob, Vector3.forward) * Turn(Vector3.up, 270f), Mathf.SmoothStep(0.02f, 1f, a));
                }
                case "13": return (new Vector3(0.07f, -0.02f, 0f) * rest, Turn(Vector3.right, 720f), grow);                // twirled in like a baton
                case "14": return (new Vector3(-0.04f, 0.06f, 0f) * rest, Turn(Vector3.right, 360f), grow);                // cartwheeling down
                case "15":                                                                                                  // snapped out
                {
                    float snap = Mathf.Pow(1f - Mathf.Clamp01(a / 0.35f), 4f);
                    return (new Vector3(0.05f, 0f, 0f) * snap, Quaternion.AngleAxis(-360f * snap, Vector3.up), a >= 0.35f ? 1f : Mathf.Max(0.02f, a / 0.35f));
                }
                case "16": return (new Vector3(0f, 0.1f, 0f) * rest, Turn(Vector3.forward, 360f), grow);                   // descending from above
                case "17": return (Vector3.zero, Turn(Vector3.up, 1080f), grow);                                            // flipped in
                case "18":                                                                                                  // drifting down like a leaf
                {
                    float sway = Mathf.Sin(a * Mathf.PI * 3f) * 0.03f * rest;
                    return (new Vector3(sway, 0.08f * rest, 0f), Quaternion.AngleAxis(Mathf.Sin(a * Mathf.PI * 3f) * 40f * rest, Vector3.forward), grow);
                }
                case "28": return (new Vector3(0.04f, 0.03f, 0f) * rest, Turn(Vector3.forward, 360f), grow);              // a gust, swirling in
                case "29":                                                                                                  // forming, glinting
                {
                    float shake = a < 0.7f ? Mathf.Sin(a * 70f) * 0.004f * (1f - a) : 0f;
                    return (new Vector3(shake, 0f, 0f), Turn(Vector3.up, 90f), Mathf.SmoothStep(0.05f, 1f, a));
                }
                case "30":
                case "35": return (new Vector3(0.06f, -0.03f, 0f) * rest, Turn(Vector3.up, -180f), grow);                // hooked in from the right
                case "31": return (new Vector3(-0.07f, 0f, 0f) * rest, Turn(Vector3.forward, 40f), grow);                 // slid in from the left
                case "32": return (new Vector3(0f, 0.09f, 0f) * rest, Turn(Vector3.right, 180f), grow);                   // dropped into the hand
                case "33":                                                                                                  // weaving in like smoke
                {
                    float sway = Mathf.Sin(a * Mathf.PI * 2.5f) * 0.03f * rest;
                    return (new Vector3(sway, -0.04f * rest, 0f), Quaternion.AngleAxis(sway * 600f, Vector3.forward), Mathf.SmoothStep(0.05f, 1f, a));
                }
                case "34": return (new Vector3(0f, -0.06f, 0f) * rest, Turn(Vector3.up, 180f), grow);                    // rising like a wave
            }
            return (Vector3.zero, Turn(Vector3.up, 360f), grow);
        }
    }
}
