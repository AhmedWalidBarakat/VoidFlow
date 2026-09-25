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
