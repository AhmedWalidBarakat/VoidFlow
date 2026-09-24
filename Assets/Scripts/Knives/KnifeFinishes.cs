using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Blade finishes, painted in code (all original patterns). Textures map u across the
    // blade (0 at the edge, 1 at the spine) and v along it (0 at the base, 1 at the tip).
    // Blades are about three times longer than wide, so patterns use y = v * 3 to stay round.
    public static class KnifeFinishes
    {
        public struct Look
        {
            public Texture2D albedo, emission;
            public Color tint, glow; // glow is HDR (emission color times intensity)
            public float metallic, smoothness;
        }

        const int Size = 256;
        static readonly Dictionary<KnifeFinish, Look> cache = new();

        public static Look Get(KnifeFinish finish)
        {
            if (cache.TryGetValue(finish, out var look) && (look.albedo || finish == KnifeFinish.Polished)) return look;
            look = Make(finish);
            cache[finish] = look;
            return look;
        }

        static Look Make(KnifeFinish f) => f switch
        {
            KnifeFinish.Nebula => Candy(Paint(f, (u, y) => Swirl(u, y, 11f, NebulaStops))),
            KnifeFinish.EmeraldNebula => Candy(Paint(f, (u, y) => Swirl(u, y, 23f, EmeraldStops))),
            KnifeFinish.SunsetFade => Candy(Paint(f, (u, y) => Ramp(SunsetStops, Mathf.Clamp01(y / 3f * 0.9f + (1f - u) * 0.18f - 0.04f)))),
            KnifeFinish.CandySwirl => Candy(Paint(f, CandySwirl)),
            KnifeFinish.AmberStripe => Candy(Paint(f, AmberStripe)),
            KnifeFinish.RedWeb => new Look { albedo = Paint(f, RedWeb), tint = Color.white, metallic = 0.45f, smoothness = 0.75f },
            KnifeFinish.HollowMoon => new Look
            {
                albedo = Paint(f, (u, y) => Grey(0.05f + Noise(u * 6f, y * 6f, 3) * 0.03f)),
                emission = Paint(f, (u, y) => Grey(Mathf.Max(1f - Edge(0.01f, 0.08f, u), Vein(u, y, 5) * 0.2f))),
                tint = Color.white, glow = new Color(0.9f, 0.03f, 0.06f) * 1.8f, metallic = 0.7f, smoothness = 0.88f,
            },
            KnifeFinish.Tidebreaker => new Look
            {
                albedo = Paint(f, (u, y) => new Color(0.05f, 0.07f, 0.12f)),
                emission = Paint(f, Waves),
                tint = Color.white, glow = new Color(0.2f, 0.6f, 1f) * 1.6f, metallic = 0.7f, smoothness = 0.9f,
            },
            KnifeFinish.Colossus => new Look
            {
                albedo = Paint(f, (u, y) => Grey((u > 0.55f && u < 0.75f ? 0.36f : 0.58f) + (Noise(u * 3f, y * 40f, 7) - 0.5f) * 0.08f)),
                emission = Paint(f, (u, y) => Grey(1f - Edge(0.01f, 0.08f, u))),
                tint = Color.white, glow = new Color(0.6f, 0.25f, 1f) * 1.6f, metallic = 0.85f, smoothness = 0.8f,
            },
            KnifeFinish.Tempered => new Look { albedo = Paint(f, Tempered), tint = Color.white, metallic = 0.85f, smoothness = 0.93f },
            // Mirror polished steel
            _ => new Look { tint = new Color(0.93f, 0.94f, 0.97f), metallic = 0.8f, smoothness = 0.95f },
        };

        static Look Candy(Texture2D albedo) => new() { albedo = albedo, tint = Color.white, metallic = 0.7f, smoothness = 0.92f };

        static Texture2D Paint(KnifeFinish f, System.Func<float, float, Color> pixel)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = f.ToString(), wrapMode = TextureWrapMode.Clamp };
            var px = new Color[Size * Size];
            for (int j = 0; j < Size; j++)
            for (int i = 0; i < Size; i++)
                px[j * Size + i] = pixel((i + 0.5f) / Size, (j + 0.5f) / Size * 3f);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // 0 below e0, 1 above e1, smooth in between (like a shader smoothstep)
        static float Edge(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static float Noise(float x, float y, int seed) => Mathf.PerlinNoise(x + seed * 17.13f, y + seed * 7.71f);
        static Color Grey(float v) => new(v, v, v, 1f);

        static Color Ramp(Color[] stops, float t)
        {
            t = Mathf.Clamp01(t) * (stops.Length - 1);
            int i = Mathf.Min((int)t, stops.Length - 2);
            return Color.Lerp(stops[i], stops[i + 1], Mathf.SmoothStep(0f, 1f, t - i));
        }

        static readonly Color[] NebulaStops =
        {
            new(0.06f, 0.03f, 0.25f), new(0.4f, 0.08f, 0.7f), new(1f, 0.35f, 0.8f), new(0.45f, 0.75f, 1f), new(0.95f, 0.9f, 1f),
        };
        static readonly Color[] EmeraldStops =
        {
            new(0.01f, 0.15f, 0.1f), new(0.03f, 0.5f, 0.28f), new(0.25f, 0.95f, 0.55f), new(0.1f, 0.55f, 0.75f), new(0.85f, 1f, 0.9f),
        };
        static readonly Color[] SunsetStops =
        {
            new(0.4f, 0.12f, 0.85f), new(0.95f, 0.3f, 0.65f), new(1f, 0.55f, 0.25f), new(1f, 0.88f, 0.25f),
        };

        // Domain-warped noise mapped onto a palette, with dark flecks: a galaxy-like swirl
        static Color Swirl(float u, float y, float seed, Color[] stops)
        {
            float wx = Noise(u * 2.5f, y * 1.2f, (int)seed), wy = Noise(u * 2.5f + 5f, y * 1.2f + 3f, (int)seed + 1);
            float n = Noise(u * 1.6f + wx * 2.6f, y * 1.1f + wy * 2.6f, (int)seed + 2);
            var c = Ramp(stops, (n - 0.2f) / 0.6f);
            if (Noise(u * 22f, y * 22f, (int)seed + 3) > 0.74f) c *= 0.25f;
            return c;
        }

        // Heat-tempered steel: mostly deep and bright blues, with patches of gold and a
        // little purple and bare silver, all flowing in long streaks along the blade
        static readonly Color[] TemperedStops =
        {
            new(0.04f, 0.1f, 0.38f), new(0.12f, 0.3f, 0.8f), new(0.35f, 0.55f, 0.95f), new(0.45f, 0.25f, 0.55f), new(0.85f, 0.65f, 0.25f), new(0.95f, 0.85f, 0.55f),
        };

        static Color Tempered(float u, float y)
        {
            float wx = Noise(u * 1.5f, y * 0.8f, 61), wy = Noise(u * 1.5f + 4f, y * 0.8f + 2f, 62);
            float n = Noise(u * 2.2f + wx * 1.8f, y * 0.9f + wy * 1.8f, 63);
            float gold = Edge(0.62f, 0.7f, Noise(u * 1.8f + wy, y * 1.1f, 64));
            var c = Ramp(TemperedStops, Mathf.Clamp01((n - 0.25f) / 0.5f) * 0.6f);
            c = Color.Lerp(c, Ramp(TemperedStops, 0.8f + 0.2f * n), gold);
            float silver = Edge(0.7f, 0.76f, Noise(u * 3f, y * 2.5f, 65));
            return Color.Lerp(c, new Color(0.82f, 0.84f, 0.88f), silver * 0.6f);
        }

        // Bands of red, yellow and blue twisted into a swirl
        static Color CandySwirl(float u, float y)
        {
            Vector2 p = new(u - 0.5f, (y - 1.2f) * 0.5f);
            float a = Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI);
            float s = a + p.magnitude * 2.4f + Noise(u * 2f, y, 31) * 0.35f;
            float band = Mathf.Repeat(s * 3f, 3f);
            Color[] colors = { new(0.95f, 0.08f, 0.1f), new(1f, 0.82f, 0.1f), new(0.1f, 0.3f, 1f) };
            int i = (int)band;
            float blend = Edge(0.8f, 1f, band - i);
            return Color.Lerp(colors[i % 3], colors[(i + 1) % 3], blend);
        }

        // Polished gold with dark tiger stripes, thicker toward the spine
        static Color AmberStripe(float u, float y)
        {
            var gold = Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(0.95f, 0.58f, 0.12f), y / 3f);
            float phase = y * 5.5f + Noise(u * 3f, y * 1.5f, 41) * 4f + u * 3f;
            float s = Mathf.Sin(phase * Mathf.PI);
            float stripe = Edge(Mathf.Lerp(0.85f, 0.3f, u), Mathf.Lerp(0.95f, 0.45f, u), s);
            return Color.Lerp(gold, new Color(0.35f, 0.12f, 0.02f), stripe);
        }

        // Deep red with a black spider web: spokes from a point near the base, sagging rings
        static Color RedWeb(float u, float y)
        {
            var baseColor = new Color(0.85f, 0.08f, 0.08f) * (0.85f + Noise(u * 4f, y * 4f, 51) * 0.3f);
            Vector2 p = new(u - 0.45f, y - 0.9f);
            float r = p.magnitude;
            float a = Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI);
            const int spokes = 11;
            float spoke = Mathf.Abs(Mathf.Repeat(a * spokes + 0.5f, 1f) - 0.5f) / spokes * 2f * Mathf.PI * r;
            float cell = Mathf.Repeat(a * spokes, 1f);
            float sag = 0.05f * Mathf.Sin(cell * Mathf.PI);
            float ringR = (r + sag) / 0.26f;
            float ring = Mathf.Abs(ringR - Mathf.Round(ringR)) * 0.26f;
            float line = Mathf.Min(spoke, Mathf.Round(ringR) > 0f ? ring : 1f);
            return Color.Lerp(new Color(0.02f, 0.01f, 0.01f), baseColor, Edge(0.003f, 0.008f, line));
        }

        // Thin glowing veins
        static float Vein(float u, float y, int seed)
        {
            float n = Noise(u * 5f, y * 4f, seed);
            return 1f - Edge(0.005f, 0.03f, Mathf.Abs(n - 0.5f));
        }

        // A rolling wave line along the edge side, bright at the edge, with curls
        static Color Waves(float u, float y)
        {
            float crest = 0.32f + 0.1f * Mathf.Sin(y * 7f) + 0.05f * Mathf.Sin(y * 17f + 1f);
            float below = 1f - Edge(crest - 0.03f, crest + 0.01f, u);
            float rim = 1f - Edge(0f, 0.035f, Mathf.Abs(u - crest));
            float edge = 1f - Edge(0.01f, 0.08f, u);
            return Grey(Mathf.Clamp01(below * 0.35f + rim + edge));
        }
    }
}
