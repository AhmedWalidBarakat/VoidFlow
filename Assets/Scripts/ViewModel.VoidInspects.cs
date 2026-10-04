using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // The real-model Void weapons each have an inspect and a draw of their own, made to fit
    // the weapon (a knife's spins are about its own axes: x end over end, y rolling over to
    // show the other side, z turning in the flat of the blade):
    //  - Gold Skull Glory Sword: a royal presentation, rolled over to show the skull, raised high
    //  - Desolate Devil Scythe: a reaping sweep across and back, then whirled overhead
    //  - Bloody Rose Sword: a fencer's lunge, a figure-eight flourish, a kiss of the blade
    //  - Monster Fantasy Sword: wound up and slammed down, shaken, tossed heavy and caught
    //  - Abyssal Heart: let go above the open palm, where it floats, turning, until called back
    //  - Demonic Twinblades: the blades crossed and clashed, flung wide, crossed again
    //  - Sword of Golden Blood: the left palm drawn along the blade, an oath, a twirl
    //  - Da Vinci's Sword: turned like a mechanism, a quarter at a time, ticking
    //  - Jade Sword: slow flowing arcs, balanced on the open palm
    //  - Shattered Crystal Sword: flung high tumbling two ways, caught left, thrown back
    //  - Demon Sword: whirled round the back of the hand, then pointed at you
    //  - Soulsucker: brought close, turned slowly, let float before your eyes, pulled back
    //  - Lance of the Primordials: twirled end over end like a baton, thrust, twirled again
    //  - Gradient Fantasy Sword: flicked up into three cartwheels and caught, shown off
    //  - Cyber Blade: held level at the eye, a sharp flick to clear the blade, a quick spin
    //  - Divine Reaper: raised high, turned slowly in the light, carried round behind the arm
    //  - Squid Dagger: rapid flips in the fingers, a hop, passed hand to hand
    //  - Autumn Sword: tossed up, it falls like a leaf, rocking side to side, and is caught low
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
                "03" => FencerRoutine(),
                "04" => BruteRoutine(),
                "05" => LevitateRoutine(),
                "06" => ClashRoutine(),
                "07" => OathRoutine(),
                "08" => ClockworkRoutine(),
                "09" => FlowRoutine(),
                "10" => ShatterRoutine(),
                "11" => HellspinRoutine(),
                "12" => SoulRoutine(),
                "13" => BatonRoutine(),
                "14" => CartwheelRoutine(),
                "15" => ChiburiRoutine(),
                "16" => AscensionRoutine(),
                "17" => JuggleRoutine(),
                "18" => LeafRoutine(),
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
            k = k.At(2.6f); k.spin = new(0f, 0f, 1080f); ks.Add(k);
            ks.Add(Home(k, 3.2f));
            return Done(ks, 2.2f, 1, 540f, (0.6f, WeaponSounds.Slash, 0.6f), (1.3f, WeaponSounds.Slash, 0.45f), (2.0f, WeaponSounds.Slash, 0.4f));
        }

        // Bloody Rose Sword
        static Routine FencerRoutine()
        {
            Quaternion guard = FB(-0.15f, 0.35f, 1f, 0.6f, 0.6f, -0.45f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.35f); k.rp = new(0.075f, -0.095f, 0.33f); k.rq = guard; ks.Add(k);                 // en garde
            k = k.At(0.55f); k.rp = new(0.06f, -0.09f, 0.42f); ks.Add(k);                                  // lunge
            k = k.At(0.85f); k.rp = new(0.075f, -0.095f, 0.33f); ks.Add(k);
            float[] eight = { 55f, -55f, 55f, -55f, 0f };                                                   // a figure eight
            for (int i = 0; i < eight.Length; i++)
            {
                k = k.At(1.05f + i * 0.18f); k.spin = new(i % 2 == 0 ? 15f : -15f, 0f, eight[i]); ks.Add(k);
            }
            k = k.At(2.2f); k.spin = Vector3.zero; k.rp = ShowAt; k.rq = ShowA; ks.Add(k);                  // a kiss of the blade
            k = k.At(2.6f); k.spin = new(0f, 180f, 0f); ks.Add(k);
            k = Home(k, 3.1f); k.spin = new(0f, 360f, 0f); ks.Add(k);
            return Done(ks, 0.4f, 0, 0f, (0.5f, WeaponSounds.Slash, 0.5f), (1.1f, WeaponSounds.Slash, 0.25f), (1.45f, WeaponSounds.Slash, 0.25f), (2.55f, WeaponSounds.Tick, 0.3f));
        }

        // Monster Fantasy Sword
        static Routine BruteRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.4f); k.rp = new(0.13f, -0.02f, 0.32f); k.rq = RightUp; k.lp = LeftIdle + LeftHandAway; ks.Add(k); // wound up
            k = k.At(0.55f); k.rp = new(0.05f, -0.16f, 0.36f); k.rq = SweepQ; ks.Add(k);                                  // slammed down
            k = k.At(0.65f); k.rp = new(0.055f, -0.155f, 0.36f); ks.Add(k);
            k = k.At(0.72f); k.rp = new(0.048f, -0.162f, 0.36f); ks.Add(k);                                              // shaken
            k = k.At(0.8f); k.rp = new(0.052f, -0.158f, 0.36f); ks.Add(k);
            k = k.At(1.15f); k.hold = Hold.Air; k.ap = new(0.04f, 0.0f, 0.42f); k.aq = Quaternion.identity; k.spin = new(-360f, 0f, 0f);
            k.rp = RightWide; k.rq = RightUp; ks.Add(k);                                                                 // tossed heavy
            k = k.At(1.5f); k.hold = Hold.Right; k.rp = new(0.1f, -0.04f, 0.34f); k.rq = RightUp; k.spin = new(-360f, 0f, 0f); ks.Add(k);
            k = k.At(2.05f); k.rp = new(0.12f, -0.07f, 0.3f); k.rq = FB(-0.6f, 0.8f, 0.1f, 0.3f, 0.2f, -1f); ks.Add(k);     // rested on the shoulder
            k = k.At(2.35f); k.rp = new(0.12f, -0.065f, 0.3f); ks.Add(k);
            ks.Add(Home(k, 2.9f));
            return Done(ks, 0.4f, 0, 0f, (0.5f, WeaponSounds.Slash, 0.7f), (1.05f, WeaponSounds.Slash, 0.4f), (1.5f, WeaponSounds.Tick, 0.5f));
        }

        // Abyssal Heart
        static Routine LevitateRoutine()
        {
            var above = new Vector3(0.055f, -0.07f, 0.33f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.4f, ShowA, ShowAt); k.rOpen = 0.2f; ks.Add(k);
            k = k.At(0.85f); k.hold = Hold.Air; k.ap = above; k.aq = Quaternion.identity; k.spin = new(0f, 0f, 90f); k.rOpen = 0.9f; ks.Add(k); // let go: it floats
            k = k.At(1.4f); k.ap = above + new Vector3(0f, 0.008f, 0f); k.spin = new(0f, 0f, 270f); k.lp = new(-0.03f, -0.1f, 0.31f); k.lq = LeftUp; k.lOpen = 0.9f; ks.Add(k);
            k = k.At(2.0f); k.ap = above; k.spin = new(0f, 0f, 450f); ks.Add(k);
            k = k.At(2.5f); k.ap = above + new Vector3(0f, 0.008f, 0f); k.spin = new(0f, 0f, 630f); ks.Add(k);
            k = k.At(2.85f); k.hold = Hold.Right; k.spin = new(0f, 0f, 720f); k.rOpen = 0f; k.lp = LeftIdle; k.lq = LeftIdleRotation; k.lOpen = 0f; ks.Add(k); // called back
            ks.Add(Home(k, 3.35f));
            return Done(ks, 1.4f, 1, 180f, (0.85f, WeaponSounds.Tick, 0.25f), (2.85f, WeaponSounds.Tick, 0.4f));
        }

        // Demonic Twinblades: both arms move (the left with its own blade)
        static Routine ClashRoutine()
        {
            Quaternion rIn = FB(-0.6f, 0.6f, 0.6f, 0.5f, 0.3f, -0.7f), lIn = FB(0.6f, 0.6f, 0.6f, -0.5f, 0.3f, -0.7f);
            var ks = new List<Key>();
            var k = IdleKey(false); k.lp = DualLeftIdle; k.lq = DualLeftRotation; ks.Add(k);
            k = k.At(0.35f); k.rp = new(0.035f, -0.09f, 0.33f); k.rq = rIn; k.lp = new(-0.035f, -0.09f, 0.33f); k.lq = lIn; ks.Add(k); // crossed
            k = k.At(0.45f); k.rp = new(0.025f, -0.088f, 0.33f); k.lp = new(-0.025f, -0.088f, 0.33f); ks.Add(k);                         // clash
            k = k.At(0.6f); k.rp = new(0.035f, -0.09f, 0.33f); k.lp = new(-0.035f, -0.09f, 0.33f); ks.Add(k);
            k = k.At(1.0f); k.rp = RightWide; k.rq = RightUp; k.lp = LeftWide; k.lq = LeftUp; k.spin = new(0f, 0f, 360f); ks.Add(k);     // flung wide
            k = k.At(1.6f); k.spin = new(0f, 0f, 1080f); ks.Add(k);
            k = k.At(2.0f); k.rp = new(0.035f, -0.09f, 0.33f); k.rq = rIn; k.lp = new(-0.035f, -0.09f, 0.33f); k.lq = lIn; ks.Add(k);  // crossed again
            k = k.At(2.1f); k.rp = new(0.025f, -0.088f, 0.33f); k.lp = new(-0.025f, -0.088f, 0.33f); ks.Add(k);
            k = Home(k, 2.65f); k.lp = DualLeftIdle; k.lq = DualLeftRotation; ks.Add(k);
            var r = Done(ks, 1.3f, 1, 900f, (0.45f, WeaponSounds.Tick, 0.6f), (0.9f, WeaponSounds.Slash, 0.4f), (2.1f, WeaponSounds.Tick, 0.6f));
            r.dualArms = true;
            return r;
        }

        // Sword of Golden Blood
        static Routine OathRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.4f, ShowA, ShowAt); ks.Add(k);
            k = k.At(0.75f); k.lp = new(-0.02f, -0.12f, 0.31f); k.lq = LeftCatchQ; k.lOpen = 0.8f; ks.Add(k);   // the left palm on the blade...
            k = k.At(1.45f); k.lp = new(0.005f, -0.08f, 0.33f); k.rq = ShowB; ks.Add(k);                       // ...drawn along it
            k = k.At(1.75f); k.lp = LeftIdle; k.lq = LeftIdleRotation; k.lOpen = 0f; ks.Add(k);
            k = k.At(2.15f); k.spin = new(0f, 180f, 0f); ks.Add(k);                                            // the other side
            k = k.At(2.6f); k.spin = new(0f, 180f, 360f); ks.Add(k);                                           // a twirl
            k = Home(k, 3.1f); k.spin = new(0f, 360f, 360f); ks.Add(k);
            return Done(ks, 1.45f, 0, 0f, (0.8f, WeaponSounds.Tick, 0.3f), (1.4f, WeaponSounds.Unsheathe, 0.35f), (2.5f, WeaponSounds.Slash, 0.3f));
        }

        // Da Vinci's Sword
        static Routine ClockworkRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.35f, ShowA, ShowAt); ks.Add(k);
            var sounds = new List<(float, AudioClip, float)>();
            float t = 0.55f;
            for (int i = 1; i <= 4; i++)                     // a quarter turn at a time, rolled over
            {
                k = k.At(t); ks.Add(k);
                t += 0.16f; k = k.At(t); k.spin = new(0f, 90f * i, 0f); ks.Add(k);
                sounds.Add((t - 0.02f, WeaponSounds.Tick, 0.45f));
                t += 0.12f;
            }
            for (int i = 1; i <= 2; i++)                     // then half turns in the flat
            {
                k = k.At(t); ks.Add(k);
                t += 0.2f; k = k.At(t); k.spin = new(0f, 360f, 180f * i); ks.Add(k);
                sounds.Add((t - 0.02f, WeaponSounds.Tick, 0.5f));
                t += 0.15f;
            }
            ks.Add(Home(k, t + 0.45f));
            return Done(ks, 0.55f, 0, 0f, sounds.ToArray());
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

        // Shattered Crystal Sword
        static Routine ShatterRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.4f); k.spin = new(0f, 0f, 360f); ks.Add(k);
            k = k.At(1.05f); k.hold = Hold.Air; k.ap = new(0.0f, 0.12f, 0.46f); k.aq = Quaternion.identity; k.spin = new(720f, 360f, 360f);
            k.rp = RightWide; k.rq = RightUp; k.lp = LeftWide; k.lq = LeftUp; ks.Add(k);                                          // flung high, tumbling two ways
            k = k.At(1.45f); k.hold = Hold.Left; k.lp = new(-0.11f, -0.02f, 0.33f); k.lq = LeftCatchQ; k.spin = new(720f, 360f, 360f); ks.Add(k); // caught left
            k = k.At(1.9f); k.spin = new(720f, 360f, 1080f); ks.Add(k);
            k = k.At(2.15f); k.hold = Hold.Air; k.ap = new(0.02f, 0.05f, 0.42f); k.aq = Quaternion.identity; k.spin = new(1080f, 360f, 1080f); k.lp = LeftWide; ks.Add(k);
            k = k.At(2.4f); k.hold = Hold.Right; k.rp = new(0.11f, -0.02f, 0.33f); k.rq = RightUp; k.spin = new(1080f, 720f, 1080f); k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            ks.Add(Home(k, 2.95f));
            return Done(ks, 0.4f, 1, 1080f, (0.95f, WeaponSounds.Slash, 0.45f), (1.45f, WeaponSounds.Tick, 0.4f), (2.1f, WeaponSounds.Slash, 0.4f), (2.4f, WeaponSounds.Tick, 0.45f));
        }

        // Demon Sword
        static Routine HellspinRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.3f); k.rp = new(0.1f, -0.08f, 0.32f); k.rOpen = 0.6f; k.rKeep = true; ks.Add(k);
            k = k.At(0.75f); k.orbit = 360f; ks.Add(k);                                       // round the back of the hand
            k = k.At(1.1f); k.orbit = 720f; k.rOpen = 0f; k.rKeep = false; ks.Add(k);
            k = k.At(1.45f); k.rp = new(0.06f, -0.085f, 0.28f); k.rq = ShowA; k.spin = new(-80f, 0f, 0f); ks.Add(k); // pointed at you
            k = k.At(1.85f); k.rp = new(0.06f, -0.08f, 0.275f); ks.Add(k);
            k = k.At(2.2f); k.rp = new(0.1f, -0.08f, 0.32f); k.rq = ForwardIdle; k.spin = Vector3.zero; k.rOpen = 0.6f; k.rKeep = true; ks.Add(k);
            k = k.At(2.6f); k.orbit = 1080f; ks.Add(k);
            k = Home(k, 2.95f); k.orbit = 1080f; ks.Add(k);
            return Done(ks, 0.75f, 2, 900f, (0.5f, WeaponSounds.Slash, 0.4f), (0.95f, WeaponSounds.Slash, 0.4f), (1.45f, WeaponSounds.Tick, 0.5f), (2.4f, WeaponSounds.Slash, 0.4f));
        }

        // Soulsucker
        static Routine SoulRoutine()
        {
            var near = new Vector3(0.07f, -0.06f, 0.33f);
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = Show(k, 0.45f, RaiseQ, near); ks.Add(k);                                  // brought up close
            k = k.At(0.9f); k.spin = new(0f, 0f, 25f); ks.Add(k);                        // an eerie sway
            k = k.At(1.35f); k.spin = new(0f, 0f, -25f); ks.Add(k);
            k = k.At(1.7f); k.spin = new(0f, 0f, 15f); ks.Add(k);
            k = k.At(2.05f); k.hold = Hold.Air; k.ap = new(0.03f, -0.07f, 0.33f); k.aq = Quaternion.identity; k.spin = new(0f, 0f, 90f);
            k.rp = near + new Vector3(0.01f, -0.01f, 0.04f); k.rOpen = 0.9f; ks.Add(k);  // let float before your eyes
            k = k.At(2.5f); k.ap = new(0.03f, -0.065f, 0.33f); k.spin = new(0f, 0f, 270f); ks.Add(k);
            k = k.At(2.8f); k.hold = Hold.Right; k.rp = near; k.spin = new(0f, 0f, 360f); k.rOpen = 0f; ks.Add(k); // pulled back
            ks.Add(Home(k, 3.3f));
            return Done(ks, 1.35f, 0, 0f, (0.45f, WeaponSounds.Tick, 0.25f), (2.05f, WeaponSounds.Slash, 0.2f), (2.8f, WeaponSounds.Tick, 0.45f));
        }

        // Lance of the Primordials
        static Routine BatonRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.3f); ks.Add(k);
            k = k.At(1.0f); k.spin = new(720f, 0f, 0f); ks.Add(k);                                             // baton twirls
            k = k.At(1.25f); k.rp = new(0.07f, -0.08f, 0.45f); k.rq = FB(-0.15f, 0.35f, 1f, 0.6f, 0.6f, -0.45f); ks.Add(k); // thrust
            k = k.At(1.5f); k.rp = RaiseAt; k.rq = RaiseQ; ks.Add(k);
            k = k.At(2.15f); k.spin = new(1440f, 0f, 0f); ks.Add(k);
            ks.Add(Home(k, 2.65f));
            return Done(ks, 0.7f, 0, 0f, (0.5f, WeaponSounds.Slash, 0.35f), (1.2f, WeaponSounds.Slash, 0.6f), (1.8f, WeaponSounds.Slash, 0.35f));
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

        // Cyber Blade
        static Routine ChiburiRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.3f); k.spin = new(0f, 0f, 75f); ks.Add(k);                                              // level at the eye
            k = k.At(0.8f); k.rp = RaiseAt + new Vector3(0.002f, 0.002f, 0f); ks.Add(k);
            k = k.At(0.92f); k.rp = new(0.12f, -0.1f, 0.33f); k.rq = RightUp; k.spin = new(0f, 0f, -40f); ks.Add(k);       // the flick
            k = k.At(1.3f); k.rp = new(0.118f, -0.098f, 0.33f); ks.Add(k);
            k = k.At(1.65f); k.rp = new(0.1f, -0.08f, 0.33f); k.rq = RightUp; k.spin = new(0f, 0f, 360f); ks.Add(k);   // a quick spin
            k = k.At(2.0f); k.spin = new(0f, 0f, 720f); ks.Add(k);
            k = Home(k, 2.45f); k.spin = new(0f, 0f, 720f); ks.Add(k);
            return Done(ks, 0.8f, 0, 0f, (0.3f, WeaponSounds.Tick, 0.3f), (0.88f, WeaponSounds.Slash, 0.7f), (1.6f, WeaponSounds.Slash, 0.35f));
        }

        // Divine Reaper
        static Routine AscensionRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = RaiseKey(k, 0.5f); k.rp = RaiseAt + new Vector3(-0.02f, 0.02f, 0f); ks.Add(k);   // raised high
            k = k.At(1.6f); k.spin = new(0f, 360f, 0f); ks.Add(k);                                // turned slowly in the light
            k = k.At(1.9f); k.rp = new(0.1f, -0.08f, 0.32f); k.rq = ForwardIdle; k.rOpen = 0.5f; k.rKeep = true; ks.Add(k);
            k = k.At(2.5f); k.orbit = 360f; ks.Add(k);                                             // carried round behind the arm
            k = Home(k, 3.0f); k.orbit = 360f; ks.Add(k);
            return Done(ks, 1.1f, 0, 0f, (0.5f, WeaponSounds.Tick, 0.3f), (2.2f, WeaponSounds.Slash, 0.45f));
        }

        // Squid Dagger
        static Routine JuggleRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.3f); k.rp = new(0.085f, -0.1f, 0.3f); k.rOpen = 0.4f; k.spin = new(0f, 360f, 0f); ks.Add(k);   // rapid flips
            k = k.At(0.5f); k.spin = new(0f, 720f, 0f); ks.Add(k);
            k = k.At(0.75f); k.hold = Hold.Air; k.ap = new(0.075f, -0.05f, 0.31f); k.aq = Quaternion.identity; k.spin = new(360f, 720f, 0f); k.rOpen = 0.7f; ks.Add(k); // a hop
            k = k.At(0.95f); k.hold = Hold.Right; k.rOpen = 0.3f; ks.Add(k);
            k = k.At(1.3f); k.spin = new(360f, 1080f, 0f); ks.Add(k);
            k = k.At(1.6f); k.hold = Hold.Left; k.lp = new(-0.05f, -0.09f, 0.31f); k.lq = LeftCatchQ; k.rp = new(0.06f, -0.1f, 0.31f); ks.Add(k); // to the left hand
            k = k.At(1.9f); k.spin = new(360f, 1440f, 0f); ks.Add(k);
            k = k.At(2.1f); k.hold = Hold.Air; k.ap = new(0.02f, -0.06f, 0.31f); k.aq = Quaternion.identity; k.spin = new(720f, 1440f, 0f); ks.Add(k);
            k = k.At(2.3f); k.hold = Hold.Right; k.lp = LeftIdle; k.lq = LeftIdleRotation; ks.Add(k);
            ks.Add(Home(k, 2.7f));
            return Done(ks, 0.5f, 0, 0f, (0.25f, WeaponSounds.Tick, 0.3f), (0.5f, WeaponSounds.Tick, 0.3f), (0.95f, WeaponSounds.Tick, 0.4f), (1.6f, WeaponSounds.Tick, 0.4f), (2.3f, WeaponSounds.Tick, 0.4f));
        }

        // Autumn Sword
        static Routine LeafRoutine()
        {
            var ks = new List<Key>();
            var k = IdleKey(false); ks.Add(k);
            k = k.At(0.35f); k.rp = RightIdle + new Vector3(0f, -0.012f, 0f); ks.Add(k);
            k = k.At(0.7f); k.hold = Hold.Air; k.ap = new(0.05f, 0.06f, 0.42f); k.aq = Quaternion.identity; k.spin = new(0f, 0f, 0f);
            k.rp = new(0.08f, -0.11f, 0.31f); k.rq = ShowA; k.rOpen = 0.7f; ks.Add(k);                                    // tossed up
            (Vector3 at, float rock)[] fall = { (new(-0.01f, 0.035f, 0.42f), 45f), (new(0.07f, 0.01f, 0.41f), -40f), (new(0.0f, -0.02f, 0.4f), 30f), (new(0.065f, -0.05f, 0.38f), -18f) };
            for (int i = 0; i < fall.Length; i++)                                                                          // falling like a leaf
            {
                k = k.At(1.1f + i * 0.38f); k.ap = fall[i].at; k.spin = new(0f, 0f, fall[i].rock); ks.Add(k);
            }
            k = k.At(2.75f); k.hold = Hold.Right; k.spin = Vector3.zero; k.rOpen = 0f; ks.Add(k);                          // caught low
            ks.Add(Home(k, 3.25f));
            return Done(ks, 1.5f, 0, 0f, (0.65f, WeaponSounds.Slash, 0.3f), (2.75f, WeaponSounds.Tick, 0.45f));
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
                case "04":                                                                                                  // slammed down
                {
                    float bounce = a < 0.7f ? Mathf.Lerp(0.12f, -0.02f, a / 0.7f) : Mathf.Lerp(-0.02f, 0f, (a - 0.7f) / 0.3f);
                    return (new Vector3(0f, bounce, 0f), Turn(Vector3.right, -180f), a >= 1f ? 1f : Mathf.Max(0.02f, Mathf.Min(1f, a * 2.5f)));
                }
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
            }
            return (Vector3.zero, Turn(Vector3.up, 360f), grow);
        }
    }
}
