using System;
using UnityEngine;

namespace VoidFlow
{
    // Weapon sounds, synthesized in code the first time they're needed (no audio files, all
    // original). The sniper shot is built like a big-caliber rifle heard outdoors: a sharp
    // supersonic crack, a deep boom that drops in pitch, a punchy mid thump, a faint metallic
    // ring, then a long rumbling tail with echoes rolling back off distant terrain.
    public static class WeaponSounds
    {
        const int Rate = 44100;

        static AudioClip sniperShot, boltUp, boltBack, boltForward, magOut, magIn, zoom, dry, slash, draw;

        public static AudioClip SniperShot => Get(ref sniperShot, "Sniper Shot", SniperShotData);
        public static AudioClip BoltUp => Get(ref boltUp, "Bolt Up", () => Mechanism(0.12f, (0f, 1700f, 0.5f), (0.035f, 2400f, 0.25f)));
        public static AudioClip BoltBack => Get(ref boltBack, "Bolt Back", () => Mechanism(0.18f, (0f, 2100f, 0.35f), (0.075f, 2900f, 0.8f), slide: (0.005f, 0.07f, 0.25f)));
        public static AudioClip BoltForward => Get(ref boltForward, "Bolt Forward", () => Mechanism(0.22f, (0.065f, 1500f, 0.9f), (0.13f, 1250f, 0.7f), slide: (0f, 0.065f, 0.25f)));
        public static AudioClip MagOut => Get(ref magOut, "Mag Out", () => Mechanism(0.2f, (0f, 900f, 0.7f), (0.02f, 1600f, 0.3f), slide: (0.02f, 0.12f, 0.2f)));
        public static AudioClip MagIn => Get(ref magIn, "Mag In", () => Mechanism(0.15f, (0f, 1150f, 1f), (0.03f, 2200f, 0.6f)));
        public static AudioClip Zoom => Get(ref zoom, "Zoom", () => Mechanism(0.05f, (0f, 3600f, 0.35f)));
        public static AudioClip Dry => Get(ref dry, "Dry Fire", () => Mechanism(0.08f, (0f, 2000f, 0.6f)));
        public static AudioClip Draw => Get(ref draw, "Draw", () => Mechanism(0.25f, (0.16f, 1800f, 0.4f), slide: (0f, 0.16f, 0.2f)));
        public static AudioClip Slash => Get(ref slash, "Slash", SlashData);
        static AudioClip tick, reveal, voidReveal, unsheathe, launch, shatter, sheathe;
        // The blade sliding home and the guard knocking against the sheath mouth
        public static AudioClip Sheathe => Get(ref sheathe, "Sheathe", () => Mechanism(0.45f, (0.28f, 1300f, 1f), (0.3f, 2600f, 0.4f), slide: (0f, 0.28f, 0.4f)));
        // Blade scraping out of the scabbard, ending in a bright ring
        public static AudioClip Unsheathe => Get(ref unsheathe, "Unsheathe", () => Mechanism(0.6f, (0.34f, 3300f, 0.5f), (0.36f, 4700f, 0.25f), slide: (0f, 0.34f, 0.5f)));
        // Skeet: the launcher's thump and a disc shattering
        public static AudioClip Launch => Get(ref launch, "Launch", () => Mechanism(0.25f, (0f, 180f, 1f), (0.02f, 420f, 0.4f), slide: (0f, 0.12f, 0.3f)));
        public static AudioClip Shatter => Get(ref shatter, "Shatter", () => Mechanism(0.35f, (0f, 2600f, 0.8f), (0.03f, 3900f, 0.6f), slide: (0f, 0.25f, 0.6f)));
        public static AudioClip Tick => Get(ref tick, "Case Tick", () => Mechanism(0.04f, (0f, 2600f, 0.3f)));
        public static AudioClip Reveal => Get(ref reveal, "Reveal", () => Chime(1.4f, 0.09f, 523.25f, 659.25f, 783.99f, 1046.5f));
        public static AudioClip VoidReveal => Get(ref voidReveal, "Void Reveal", () => Chime(2.6f, 0.14f, 196f, 293.66f, 392f, 466.16f, 587.33f, 783.99f));

        // ------------------------------------------------------------------ movement and feedback

        // What a surface sounds like underfoot or when a bullet hits it
        public enum Surface { Wood, Metal, Stone, Ice }

        static AudioClip jump, slideLoop, windLoop, shard, boost, holster, shell, hitTick;
        static readonly AudioClip[,] steps = new AudioClip[4, 4];
        static readonly AudioClip[] lands = new AudioClip[4], impacts = new AudioClip[4];

        public static AudioClip Jump => Get(ref jump, "Jump", JumpData);
        public static AudioClip SlideLoop => Get(ref slideLoop, "Surf Slide", SlideData);
        public static AudioClip WindLoop => Get(ref windLoop, "Wind", WindData);
        public static AudioClip Shard => Get(ref shard, "Shard", () => Chime(0.7f, 0.045f, 1318.5f, 1975.5f, 2637f));
        public static AudioClip Boost => Get(ref boost, "Boost", BoostData);
        public static AudioClip Holster => Get(ref holster, "Holster", () => Whoosh(0.18f, 0.03f, 0.18f, 3));
        public static AudioClip Shell => Get(ref shell, "Shell", () => Mechanism(0.3f, (0f, 4300f, 0.45f), (0.11f, 3700f, 0.25f)));
        public static AudioClip HitTick => Get(ref hitTick, "Hit", () => Mechanism(0.07f, (0f, 3300f, 0.9f)));

        public static AudioClip Step(Surface s, int variant)
        {
            variant &= 3;
            if (steps[(int)s, variant]) return steps[(int)s, variant];
            return steps[(int)s, variant] = Clip($"Step {s} {variant}", Footfall(s, 0.2f, 0.028f, 1f, 50 + (int)s * 10 + variant));
        }

        public static AudioClip Land(Surface s) => lands[(int)s] ? lands[(int)s] : lands[(int)s] = Clip($"Land {s}", Footfall(s, 0.4f, 0.07f, 1.6f, 90 + (int)s));
        public static AudioClip Impact(Surface s) => impacts[(int)s] ? impacts[(int)s] : impacts[(int)s] = Clip($"Impact {s}", ImpactData(s));

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A foot (or both, landing) meeting a surface: a low thump, a scuff, and the surface's
        // own voice (wood knock, metal ring, stone grit, ice crunch)
        static float[] Footfall(Surface s, float length, float thumpDecay, float weight, int seed)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f, lp2 = 0f, last = 0f, hp = 0f;
            double phase = 0.0;
            float detune = 0.95f + (float)rng.NextDouble() * 0.1f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                double f = (55.0 + 40.0 * Math.Exp(-t / 0.02)) * detune;
                phase += 2.0 * Math.PI * f / Rate;
                float thump = (float)Math.Sin(phase) * Mathf.Exp(-t / thumpDecay) * Mathf.Clamp01(t / 0.002f) * weight;
                lp += (white - lp) * (s is Surface.Ice or Surface.Metal ? 0.5f : 0.25f);
                lp2 += (lp - lp2) * 0.08f;
                float scuff = (lp - lp2) * Mathf.Exp(-t / 0.03f) * 0.8f;
                hp = 0.8f * (hp + white - last);
                last = white;
                float voice = s switch
                {
                    Surface.Wood => (Mathf.Sin(2f * Mathf.PI * 230f * detune * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 410f * detune * t) * 0.25f) * Mathf.Exp(-t / 0.045f),
                    Surface.Metal => (Mathf.Sin(2f * Mathf.PI * 1100f * detune * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 1690f * detune * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 2530f * detune * t)) * Mathf.Exp(-t / 0.07f) * 0.12f,
                    Surface.Stone => hp * Mathf.Exp(-t / 0.035f) * 0.35f,
                    _ => (rng.NextDouble() < 0.006 ? (float)(rng.NextDouble() * 2.0 - 1.0) * 1.4f : 0f) * Mathf.Exp(-t / 0.06f) + hp * Mathf.Exp(-t / 0.05f) * 0.2f,
                };
                data[i] = thump + scuff + voice;
            }
            return Master(data, 1.3f, 0.85f);
        }

        // A soft cloth-and-scuff for leaving the ground
        static float[] JumpData()
        {
            var data = Whoosh(0.16f, 0.05f, 0.3f, 11);
            var rng = new System.Random(12);
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                lp += ((float)(rng.NextDouble() * 2.0 - 1.0) - lp) * 0.3f;
                data[i] += lp * Mathf.Exp(-t / 0.012f) * 0.6f;
            }
            return Master(data, 1f, 0.6f);
        }

        // Noise through a band that swells and fades (cloth, holstering)
        static float[] Whoosh(float length, float low, float high, int seed)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lpA = 0f, lpB = 0f;
            for (int i = 0; i < n; i++)
            {
                float s = (float)i / n;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lpA += (white - lpA) * high;
                lpB += (white - lpB) * low;
                data[i] = (lpA - lpB) * Mathf.Pow(Mathf.Sin(Mathf.PI * s), 2f);
            }
            return Master(data, 1f, 0.7f);
        }

        // The hiss of a body sliding down a ramp, made to loop seamlessly
        static float[] SlideData()
        {
            const float length = 2f;
            int n = (int)(Rate * length), fade = Rate / 5;
            var raw = new float[n + fade];
            var rng = new System.Random(21);
            float lpA = 0f, lpB = 0f, grind = 1f;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lpA += (white - lpA) * 0.3f;
                lpB += (white - lpB) * 0.04f;
                if (i % 600 == 0) grind = 0.8f + (float)rng.NextDouble() * 0.4f;
                raw[i] = (lpA - lpB) * grind;
            }
            return Loop(raw, n, fade, 0.7f);
        }

        // Wind rushing past: low noise breathing slowly, looping
        static float[] WindData()
        {
            const float length = 3f;
            int n = (int)(Rate * length), fade = Rate / 3;
            var raw = new float[n + fade];
            var rng = new System.Random(33);
            float lpA = 0f, lpB = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lpA += (white - lpA) * 0.06f;
                lpB += (lpA - lpB) * 0.1f;
                raw[i] = lpB * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / length * 2f));
            }
            return Loop(raw, n, fade, 0.7f);
        }

        // Crossfades the extra tail into the head so the clip loops without a click
        static float[] Loop(float[] raw, int n, int fade, float peak)
        {
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                data[i] = raw[i] * k + raw[n + i] * (1f - k);
            }
            return Master(data, 1f, peak);
        }

        // Flying through a speed ring: a rising sweep and a rush of air
        static float[] BoostData()
        {
            const float length = 0.8f;
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(44);
            float lpA = 0f, lpB = 0f;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, s = t / length;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float cutoff = Mathf.Lerp(0.04f, 0.4f, s);
                lpA += (white - lpA) * cutoff;
                lpB += (white - lpB) * cutoff * 0.25f;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t / 0.35f);
                phase += 2.0 * Math.PI * (180.0 + 700.0 * s) / Rate;
                data[i] = ((lpA - lpB) * 0.8f + (float)Math.Sin(phase) * 0.35f) * env;
            }
            return Master(data, 1.2f, 0.8f);
        }

        // A bullet hitting: a sharp crack plus the surface's answer
        static float[] ImpactData(Surface s)
        {
            const float length = 0.5f;
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(70 + (int)s);
            float last = 0f, hp = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                hp = 0.75f * (hp + white - last);
                last = white;
                lp += (white - lp) * 0.1f;
                float crack = hp * Mathf.Exp(-t / 0.004f) * 1.4f;
                float voice = s switch
                {
                    Surface.Metal => (Mathf.Sin(2f * Mathf.PI * 1650f * t) + 0.8f * Mathf.Sin(2f * Mathf.PI * 2480f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 3900f * t)) * Mathf.Exp(-t / 0.16f) * 0.35f,
                    Surface.Wood => (Mathf.Sin(2f * Mathf.PI * 320f * t) * Mathf.Exp(-t / 0.04f) + Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t / 0.07f)) * 0.8f,
                    Surface.Stone => Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t / 0.05f) * 0.8f + lp * Mathf.Exp(-t / 0.08f) * 1.5f,
                    _ => (rng.NextDouble() < 0.01 ? (float)(rng.NextDouble() * 2.0 - 1.0) : 0f) * Mathf.Exp(-t / 0.12f) + Mathf.Sin(2f * Mathf.PI * 3100f * t) * Mathf.Exp(-t / 0.05f) * 0.2f,
                };
                data[i] = crack + voice;
            }
            return Master(data, 1.3f, 0.9f);
        }

        static AudioClip Get(ref AudioClip clip, string name, Func<float[]> make)
        {
            if (clip) return clip;
            float[] data = make();
            clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] SniperShotData()
        {
            const float length = 2.4f;
            int n = (int)(Rate * length);
            var dryMix = new float[n];
            var rng = new System.Random(408);
            float lpBody = 0f, lpA = 0f, lpB = 0f, lpTail = 0f, lpTail2 = 0f, hpCrack = 0f, lastWhite = 0f;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Crack: an instant of bright, high-passed noise
                hpCrack = 0.6f * (hpCrack + white - lastWhite);
                lastWhite = white;
                float crack = hpCrack * Mathf.Exp(-t / 0.003f) * 2.2f;

                // Boom: a sine sweeping down from ~125 Hz to ~38 Hz
                float freq = 38f + 87f * Mathf.Exp(-t / 0.06f);
                phase += 2.0 * Math.PI * freq / Rate;
                float boom = (float)Math.Sin(phase) * Mathf.Exp(-t / 0.24f) * Mathf.Clamp01(t / 0.002f) * 1.2f;

                // Body and mid punch: filtered noise bursts
                lpBody += (white - lpBody) * 0.08f;
                float body = lpBody * Mathf.Exp(-t / 0.1f) * 4f;
                lpA += (white - lpA) * 0.35f;
                lpB += (white - lpB) * 0.05f;
                float mid = (lpA - lpB) * Mathf.Exp(-t / 0.05f) * 1.8f;

                // Metallic ring from the action
                float ring = (Mathf.Sin(2f * Mathf.PI * 2350f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 3180f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 4700f * t))
                             * Mathf.Exp(-t / 0.16f) * 0.05f;

                // Rumbling tail that swells in and dies away slowly
                lpTail += (white - lpTail) * 0.02f;
                lpTail2 += (lpTail - lpTail2) * 0.05f;
                float tail = lpTail2 * Mathf.Exp(-t / 0.6f) * Mathf.Clamp01(t / 0.03f) * 10f;

                dryMix[i] = crack + boom + body + mid + ring + tail;
            }

            // Echoes rolling back, each duller and quieter than the last
            var data = (float[])dryMix.Clone();
            foreach (var (delay, gain, dull) in new[] { (0.19f, 0.38f, 0.25f), (0.43f, 0.24f, 0.12f), (0.78f, 0.13f, 0.07f), (1.2f, 0.07f, 0.05f) })
            {
                int offset = (int)(delay * Rate);
                float lp = 0f;
                for (int i = 0; i + offset < n; i++)
                {
                    lp += (dryMix[i] - lp) * dull;
                    data[i + offset] += lp * gain;
                }
            }
            return Master(data, 1.5f);
        }

        // Mechanical sounds: sharp metal clicks (a noise tick plus a short ringing ping) over
        // an optional sliding scrape
        static float[] Mechanism(float length, (float at, float pitch, float gain) first, (float at, float pitch, float gain)? second = null,
            (float at, float duration, float gain)? slide = null)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(Mathf.RoundToInt(first.pitch));
            if (slide is { } sl)
            {
                float lpA = 0f, lpB = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate - sl.at;
                    if (t < 0f || t > sl.duration) continue;
                    float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lpA += (white - lpA) * 0.5f;
                    lpB += (white - lpB) * 0.1f;
                    data[i] += (lpA - lpB) * Mathf.Sin(Mathf.PI * t / sl.duration) * sl.gain;
                }
            }
            foreach (var click in second is { } s ? new[] { first, s } : new[] { first })
            {
                float last = 0f, hp = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate - click.at;
                    if (t < 0f) continue;
                    float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                    hp = 0.7f * (hp + white - last);
                    last = white;
                    float tick = hp * Mathf.Exp(-t / 0.0025f);
                    float ping = (Mathf.Sin(2f * Mathf.PI * click.pitch * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * click.pitch * 2.7f * t)) * Mathf.Exp(-t / 0.02f) * 0.5f;
                    data[i] += (tick + ping) * click.gain;
                }
            }
            return Master(data, 1f, 0.8f);
        }

        // Rising bell arpeggio (case reveals): each note a bright decaying bell tone
        static float[] Chime(float length, float step, params float[] notes)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * step, f = notes[k];
                for (int i = (int)(start * Rate); i < n; i++)
                {
                    float t = (float)i / Rate - start;
                    float env = Mathf.Exp(-t / 0.5f) * Mathf.Clamp01(t / 0.004f);
                    data[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 3.98f * t)) * env * 0.3f;
                }
            }
            return Master(data, 1f, 0.7f);
        }

        // Knife whoosh: noise through a band that sweeps up then down, swelling and fading
        static float[] SlashData()
        {
            const float length = 0.3f;
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(3);
            float lpA = 0f, lpB = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, s = t / length;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float cutoff = Mathf.Lerp(0.05f, 0.35f, Mathf.Sin(Mathf.PI * s));
                lpA += (white - lpA) * cutoff;
                lpB += (white - lpB) * cutoff * 0.3f;
                data[i] = (lpA - lpB) * Mathf.Pow(Mathf.Sin(Mathf.PI * s), 2f);
            }
            return Master(data, 1f, 0.6f);
        }

        // Soft-clip for weight, then normalize
        static float[] Master(float[] data, float drive, float peak = 0.95f)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (float)Math.Tanh(data[i] * drive);
                max = Mathf.Max(max, Mathf.Abs(data[i]));
            }
            if (max > 0f)
                for (int i = 0; i < data.Length; i++) data[i] *= peak / max;
            return data;
        }
    }
}
