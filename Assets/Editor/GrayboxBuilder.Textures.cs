using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Hand-painted style textures, generated so they're original and tile seamlessly. They're
    // grey-scale-ish with bold dark outlines (a cartoon look) and get tinted per biome by
    // the material color.
    public static partial class GrayboxBuilder
    {
        const int PaintSize = 256;

        // Tileable value noise: smooth random values on a grid that wraps every `period` cells
        static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float Hash(int ix, int iy)
            {
                ix = ((ix % period) + period) % period;
                iy = ((iy % period) + period) % period;
                uint h = (uint)(ix * 374761393 + iy * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177;
                return (h & 0xFFFF) / 65535f;
            }
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), sx);
            float b = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), sx);
            return Mathf.Lerp(a, b, sy);
        }

        // Layered noise for a painted, mottled look (u, v in 0..1)
        static float Mottle(float u, float v, int seed) =>
            Noise(u * 4f, v * 4f, 4, seed) * 0.6f + Noise(u * 16f, v * 16f, 16, seed + 1) * 0.3f + Noise(u * 64f, v * 64f, 64, seed + 2) * 0.1f;

        static Texture2D Paint(string name, System.Func<float, float, Color> pixel)
        {
            var tex = new Texture2D(PaintSize, PaintSize, TextureFormat.RGBA32, false);
            for (int y = 0; y < PaintSize; y++)
            for (int x = 0; x < PaintSize; x++)
                tex.SetPixel(x, y, pixel((x + 0.5f) / PaintSize, (y + 0.5f) / PaintSize));
            return SaveTexture(tex, $"{Root}/{name}.png");
        }

        // Clean cartoon panels: 2x2 slightly domed tiles with a bright top-left rim and bold
        // dark seams
        static Texture2D MakeTilesTexture() => Paint("Tiles", (u, v) =>
        {
            float cu = u * 2f % 1f, cv = v * 2f % 1f;
            float edge = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv));
            if (edge < 0.025f) return Grey(0.25f);
            float rim = cu < 0.07f || cv > 0.93f ? 0.08f : 0f;
            float shade = 0.86f + rim - 0.1f * Mathf.Pow(Mathf.Max(Mathf.Abs(cu - 0.5f), Mathf.Abs(cv - 0.5f)) * 2f, 3f);
            return Grey(shade + (Mottle(u, v, 3) - 0.5f) * 0.06f);
        });

        // Stone blocks: staggered, irregular-shaded blocks with thick dark mortar, a light
        // chipped top edge, and speckle
        static Texture2D MakeStoneTexture() => Paint("Stone", (u, v) =>
        {
            const int rows = 4;
            int row = Mathf.FloorToInt(v * rows);
            float ru = u * 2f + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(ru);
            float bu = ru - col, bv = v * rows - row;
            float edge = Mathf.Min(Mathf.Min(bu, 1f - bu) * 0.5f, Mathf.Min(bv, 1f - bv) / rows) + (Noise(u * 32f, v * 32f, 32, 9) - 0.5f) * 0.012f;
            if (edge < 0.012f) return Grey(0.18f);
            float block = 0.62f + 0.18f * Noise(col % 2 * 1.7f + 0.5f, row * 2.3f + 0.5f, 64, 5);
            float top = bv > 0.88f ? 0.12f : 0f;
            float speck = Noise(u * 128f, v * 128f, 128, 7) > 0.82f ? -0.12f : 0f;
            return Grey(block + top + speck + (Mottle(u, v, 11) - 0.5f) * 0.18f);
        });

        // Riveted metal plates: 2x2 plates with bevelled edges, brushed streaks and rivets
        static Texture2D MakeMetalTexture() => Paint("Metal", (u, v) =>
        {
            float cu = u * 2f % 1f, cv = v * 2f % 1f;
            float edge = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv));
            if (edge < 0.02f) return Grey(0.15f);
            foreach (var (ru, rv) in new[] { (0.08f, 0.08f), (0.92f, 0.08f), (0.08f, 0.92f), (0.92f, 0.92f) })
            {
                float d = Mathf.Sqrt((cu - ru) * (cu - ru) + (cv - rv) * (cv - rv));
                if (d < 0.035f) return Grey(d < 0.018f ? 0.92f : 0.3f);
            }
            float bevel = edge < 0.05f ? (cu < 0.05f || cv > 0.95f ? 0.15f : -0.12f) : 0f;
            float streak = (Noise(u * 4f, v * 128f, 128, 13) - 0.5f) * 0.12f;
            return Grey(0.6f + bevel + streak + (Mottle(u, v, 17) - 0.5f) * 0.08f);
        });

        // Wood planks: vertical boards with grain, dark seams and nail heads
        static Texture2D MakeWoodTexture() => Paint("Wood", (u, v) =>
        {
            const int planks = 4;
            float pu = u * planks;
            int plank = Mathf.FloorToInt(pu);
            float bu = pu - plank;
            float offset = Noise(plank * 3.1f + 0.5f, 0.5f, 64, 21);
            float bv = (v + offset) % 1f;
            if (bu < 0.04f || bu > 0.96f || bv < 0.012f) return new Color(0.16f, 0.1f, 0.06f);
            if ((bv > 0.05f && bv < 0.08f || bv > 0.92f && bv < 0.95f) && Mathf.Abs(bu - 0.5f) > 0.3f && Mathf.Abs(bu - 0.5f) < 0.36f)
                return new Color(0.2f, 0.18f, 0.16f);
            float grain = Mathf.Sin((bu * 3f + Noise(u * 8f, v * 2f, 8, 23) * 2.5f) * Mathf.PI * 6f) * 0.5f + 0.5f;
            float tone = 0.55f + 0.12f * offset + grain * 0.1f + (Mottle(u, v, 27) - 0.5f) * 0.12f;
            return new Color(tone, tone * 0.72f, tone * 0.48f);
        });

        // Cracked ice: pale, streaked, with dark fracture lines and bright edges
        static Texture2D MakeIceTexture() => Paint("Ice", (u, v) =>
        {
            float cells = Noise(u * 6f, v * 6f, 6, 31);
            float crack = Mathf.Abs(Noise(u * 8f + cells * 2f, v * 8f, 8, 33) - 0.5f);
            if (crack < 0.018f) return new Color(0.45f, 0.58f, 0.7f);
            float glint = crack < 0.035f ? 0.1f : 0f;
            float streak = (Noise(u * 3f + v * 12f, v * 3f, 64, 35) - 0.5f) * 0.12f;
            float shade = 0.82f + glint + streak + (Mottle(u, v, 37) - 0.5f) * 0.1f;
            return new Color(shade * 0.92f, shade * 0.97f, Mathf.Min(1f, shade * 1.05f));
        });

        static Color Grey(float v) => new(v, v, v, 1f);
    }
}
