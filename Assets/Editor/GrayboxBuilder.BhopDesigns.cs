using UnityEngine;

namespace VoidFlow.EditorTools
{
    // The bhop challenge's rooms: their floors and walls, painted in code (every texture covers
    // 8m x 8m; the rooms map them at 1/8 per metre). The floors are where a miss lands you, so
    // each reads at a glance as "not here": water, moss, sand, lava, a void with a grid.
    public static partial class GrayboxBuilder
    {
        // Still water: deep teal, soft ripples and a faint net of light
        static Texture2D MakeBhopWater() => Design("BhopWater", (u, v) =>
        {
            float n = Noise(u * 8f, v * 8f, 8, 901), ripple = Mathf.Sin((u * 3f + n * 1.5f) * Mathf.PI * 4f) * 0.5f + 0.5f;
            var (f1, f2, _) = Voronoi(u * 10f + n * 0.6f, v * 10f + n * 0.6f, 10, 903);
            float caustic = Mathf.Clamp01(1f - (f2 - f1) * 9f);
            float shade = 0.75f + 0.12f * ripple + 0.35f * caustic * caustic + 0.08f * Mottle(u, v, 905);
            return Solid(Rgb(0.1f, 0.42f, 0.55f) * shade);
        });

        // Moss and grass, darker in patches, with a few pale pebbles
        static Texture2D MakeBhopMoss() => Design("BhopMoss", (u, v) =>
        {
            var (f1, _, id) = Voronoi(u * 40f, v * 40f, 40, 911);
            if (f1 < 0.16f && Hash(id, 1, 913) > 0.93f) return Solid(Rgb(0.62f, 0.6f, 0.55f) * (0.8f + 0.3f * Hash(id, 2, 915)));
            float blade = Noise(u * 128f, v * 128f, 128, 917);
            float shade = 0.55f + 0.35f * Mottle(u, v, 919) + 0.2f * blade;
            return Solid(Rgb(0.2f, 0.36f, 0.13f) * shade);
        });

        // Red sand with wind ripples
        static Texture2D MakeBhopSand() => Design("BhopSand", (u, v) =>
        {
            float n = Noise(u * 6f, v * 6f, 6, 921);
            float ripple = Mathf.Pow(Mathf.Abs(Mathf.Sin((v * 14f + n * 2f) * Mathf.PI)), 3f);
            float shade = 0.8f + 0.12f * ripple + 0.12f * Mottle(u, v, 923);
            return Solid(Rgb(0.62f, 0.3f, 0.17f) * shade);
        });

        // Lava: dark crust plates with bright cracks between them (and the cracks' glow)
        static float LavaCrack(float u, float v)
        {
            float n = Noise(u * 6f, v * 6f, 6, 931);
            var (f1, f2, _) = Voronoi(u * 6f + n * 0.4f, v * 6f + n * 0.4f, 6, 933);
            return Mathf.Clamp01(1f - (f2 - f1) * 7f);
        }
        static Texture2D MakeBhopLava() => Design("BhopLava", (u, v) =>
        {
            float c = LavaCrack(u, v);
            var crust = Rgb(0.13f, 0.09f, 0.08f) * (0.7f + 0.5f * Mottle(u, v, 935));
            return Solid(Color.Lerp(crust, Rgb(1f, 0.55f, 0.12f), c));
        });
        static Texture2D MakeBhopLavaGlow() => Design("BhopLavaGlow", (u, v) =>
        {
            float c = LavaCrack(u, v);
            return Solid(Rgb(1f, 0.45f, 0.1f) * (c * c + 0.08f));
        });

        // White panels: a 1m grid of fine grey seams, every 4m a stronger one (the clean rooms)
        static Texture2D MakeBhopGridWhite() => Design("BhopGridWhite", (u, v) =>
        {
            if (ToLine(u * 2f) * 64f < 1f || ToLine(v * 2f) * 64f < 1f) return Grey(0.6f);
            if (ToLine(u * 8f) * 64f < 0.5f || ToLine(v * 8f) * 64f < 0.5f) return Grey(0.78f);
            return Grey(0.94f + 0.03f * Mottle(u, v, 941));
        });

        // The void with a grid: near black, cyan lines every 2m, magenta every 8m (and their glow)
        static Color NeonLine(float u, float v, bool glow)
        {
            if (ToLine(u) * 64f < 1.2f || ToLine(v) * 64f < 1.2f) return glow ? Rgb(1f, 0.25f, 0.8f) : Rgb(1f, 0.4f, 0.85f);
            if (ToLine(u * 4f) * 64f < 0.6f || ToLine(v * 4f) * 64f < 0.6f) return glow ? Rgb(0.2f, 0.85f, 1f) : Rgb(0.4f, 0.9f, 1f);
            return glow ? Dark : Solid(Grey(0.03f + 0.02f * Mottle(u, v, 951)));
        }
        static Texture2D MakeBhopNeon() => Design("BhopNeon", (u, v) => NeonLine(u, v, false));
        static Texture2D MakeBhopNeonGlow() => Design("BhopNeonGlow", (u, v) => NeonLine(u, v, true));

        // Ice: pale blue, frosted, with fine cracks
        static Texture2D MakeBhopIce() => Design("BhopIce", (u, v) =>
        {
            var (f1, f2, _) = Voronoi(u * 7f, v * 7f, 7, 961);
            float crack = f2 - f1 < 0.03f ? 0.82f : 1f;
            float shade = (0.82f + 0.12f * Mottle(u, v, 963) + 0.06f * f1) * crack;
            return Solid(Rgb(0.72f, 0.88f, 0.98f) * shade);
        });

        // Crystal rock: dark violet stone with glowing veins (and their glow)
        static float Vein(float u, float v)
        {
            float n = Noise(u * 5f, v * 5f, 5, 971);
            var (f1, f2, _) = Voronoi(u * 4f + n * 0.7f, v * 4f + n * 0.7f, 4, 973);
            return Mathf.Clamp01(1f - (f2 - f1) * 16f);
        }
        static Texture2D MakeBhopCrystalRock() => Design("BhopCrystalRock", (u, v) =>
        {
            float vein = Vein(u, v);
            var rock = Rgb(0.14f, 0.09f, 0.2f) * (0.65f + 0.6f * Mottle(u, v, 975));
            return Solid(Color.Lerp(rock, Rgb(0.8f, 0.55f, 1f), vein));
        });
        static Texture2D MakeBhopCrystalGlow() => Design("BhopCrystalGlow", (u, v) => Solid(Rgb(0.7f, 0.4f, 1f) * Mathf.Pow(Vein(u, v), 2f)));

        // Big stone bricks with deep mortar (grey: the garden's walls)
        static Texture2D MakeBhopStoneBrick() => Design("BhopStoneBrick", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 12f);
            float x = u * 6f + (row % 2) * 0.5f;
            float cu = x - Mathf.Floor(x), cv = v * 12f - row;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu) * 0.5f, Mathf.Min(cv, 1f - cv));
            if (e < 0.05f) return Grey(0.22f);
            float shade = 0.55f + 0.2f * Hash(Wrap(Mathf.FloorToInt(x), 6), row, 981) + 0.15f * Mottle(u, v, 983) + (e < 0.1f ? 0.08f : 0f);
            return Solid(Rgb(0.78f, 0.8f, 0.76f) * shade);
        });

        // Clean white panels with thin seams (the finale's walls)
        static Texture2D MakeBhopPanels() => Design("BhopPanels", (u, v) =>
        {
            float cu = u * 4f % 1f, cv = v * 2f % 1f;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu) * 2f, Mathf.Min(cv, 1f - cv) * 4f);
            if (e < 0.02f) return Grey(0.62f);
            return Grey(0.95f + 0.03f * Mottle(u, v, 991) - (e < 0.05f ? 0.04f : 0f));
        });
    }
}
