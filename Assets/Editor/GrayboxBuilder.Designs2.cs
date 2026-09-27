using UnityEngine;

namespace VoidFlow.EditorTools
{
    // The second lap's zones: their ramp designs, glow masks and wall patterns (same 8m tiles
    // as GrayboxBuilder.Designs; ramps map u along the ramp, v down its face). Bright lines run
    // the way you travel; lines across your path stay faint and far apart.
    public static partial class GrayboxBuilder
    {
        // Alpine: packed snow, faintly blue in its hollows, with ski grooves running down the ramp
        static Texture2D MakeRampSnow() => Design("RampSnow", (u, v) =>
        {
            float n = Mottle(u, v, 401);
            float groove = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * (v * 24f + Noise(u * 4f, v * 4f, 4, 403) * 0.6f))), 12f);
            float shade = 0.9f + 0.08f * n - 0.08f * groove;
            return Rgb(shade * 0.95f, shade * 0.97f, Mathf.Min(1f, shade * 1.02f));
        });

        // Canyon: clean white panels ruled with a grey grid, the lines along the ramp stronger
        static Texture2D MakeRampWhiteGrid() => Design("RampWhiteGrid", (u, v) =>
        {
            if (ToLine(v * 4f) * 2f < 0.03f) return Rgb(0.55f, 0.58f, 0.62f);
            if (ToLine(u * 4f) * 2f < 0.02f) return Rgb(0.72f, 0.74f, 0.77f);
            float shade = 0.95f + 0.03f * Mottle(u, v, 411);
            return Rgb(shade, shade, Mathf.Min(1f, shade * 1.01f));
        });

        // Glass City: pale glass with bright edges along the ramp and soft reflections
        static Texture2D MakeRampGlass() => Design("RampGlass", (u, v) =>
        {
            if (ToLine(v * 2f) * 4f < 0.05f) return Rgb(0.8f, 0.97f, 1f);
            float streak = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (u * 0.5f + v * 1.5f))), 30f);
            float shade = 0.55f + 0.1f * Mottle(u, v, 421) + 0.3f * streak;
            return Rgb(shade * 0.75f, shade * 0.9f, shade);
        });

        static Texture2D MakeRampGlassGlow() => Design("RampGlassGlow", (u, v) =>
            ToLine(v * 2f) * 4f < 0.05f ? Rgb(0.4f, 0.9f, 1f) : ToLine(u) * 8f < 0.04f ? Rgb(0.08f, 0.18f, 0.2f) : Dark);

        // Moonlit Garden: small white bricks, staggered, with grey mortar
        static Texture2D MakeRampBrick() => Design("RampBrick", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 32f);
            float x = u * 16f + (row % 2) * 0.5f;
            float cu = x - Mathf.Floor(x), cv = v * 32f - row;
            if (Mathf.Min(cu, 1f - cu) < 0.04f || Mathf.Min(cv, 1f - cv) < 0.08f) return Rgb(0.62f, 0.64f, 0.66f);
            float shade = 0.86f + 0.1f * Hash(Wrap(Mathf.FloorToInt(x), 16), row, 431) + 0.05f * (Mottle(u, v, 433) - 0.5f);
            return Rgb(shade * 0.98f, shade, shade);
        });

        // Hex Lab: white panels with a glowing green hexagon grid
        static Texture2D MakeRampHex() => Design("RampHex", (u, v) =>
        {
            var (f1, f2, _) = HexCells(u * 8f, v * 4f * S3, 8, 4);
            if (f2 - f1 < 0.06f) return Rgb(0.3f, 0.85f, 0.35f);
            float shade = 0.9f + 0.05f * Mottle(u, v, 441) - 0.08f * f1;
            return Rgb(shade, shade, shade);
        });

        static Texture2D MakeRampHexGlow() => Design("RampHexGlow", (u, v) =>
        {
            var (f1, f2, _) = HexCells(u * 8f, v * 4f * S3, 8, 4);
            return f2 - f1 < 0.06f ? Rgb(0.2f, 1f, 0.3f) * (1f - (f2 - f1) / 0.06f * 0.5f) : Dark;
        });

        // Ember Sunset: black with glowing orange lines along the ramp, faint ones across
        static Texture2D MakeRampEmber() => Design("RampEmber", (u, v) =>
        {
            if (ToLine(v * 2f) * 4f < 0.06f) return Rgb(1f, 0.5f, 0.1f);
            if (ToLine(v * 8f) < 0.02f) return Rgb(0.5f, 0.2f, 0.05f);
            if (ToLine(u) * 8f < 0.03f) return Rgb(0.3f, 0.1f, 0.03f);
            return Solid(Rgb(0.04f, 0.03f, 0.03f) * (0.8f + 0.4f * Mottle(u, v, 451)));
        });

        static Texture2D MakeRampEmberGlow() => Design("RampEmberGlow", (u, v) =>
            ToLine(v * 2f) * 4f < 0.06f ? Rgb(1f, 0.45f, 0.08f) : ToLine(v * 8f) < 0.02f ? Rgb(0.5f, 0.18f, 0.03f) : ToLine(u) * 8f < 0.03f ? Rgb(0.2f, 0.06f, 0.01f) : Dark);

        // Amethyst: deep violet crystal facets with white light lines along the ramp
        static Texture2D MakeRampAmethyst() => Design("RampAmethyst", (u, v) =>
        {
            if (ToLine(v * 2f) * 4f < 0.04f) return Rgb(0.95f, 0.9f, 1f);
            var (f1, f2, id) = Voronoi(u * 5f, v * 5f, 5, 461);
            float facet = 0.55f + 0.45f * Hash(id, 2, 463);
            if (f2 - f1 < 0.03f) return Rgb(0.75f, 0.6f, 1f);
            return Solid(Rgb(0.32f, 0.18f, 0.5f) * facet);
        });

        static Texture2D MakeRampAmethystGlow() => Design("RampAmethystGlow", (u, v) =>
        {
            if (ToLine(v * 2f) * 4f < 0.04f) return Rgb(0.9f, 0.85f, 1f);
            var (f1, f2, _) = Voronoi(u * 5f, v * 5f, 5, 461);
            return f2 - f1 < 0.03f ? Rgb(0.4f, 0.25f, 0.6f) : Dark;
        });

        // Toy Town: big cartoon tiles in bright primary colours with bold dark outlines
        static Texture2D MakeRampToy() => Design("RampToy", (u, v) =>
        {
            float x = u * 4f, y = v * 4f;
            int ci = Mathf.FloorToInt(x), cj = Mathf.FloorToInt(y);
            float cu = x - ci, cv = y - cj;
            if (Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) < 0.035f) return Rgb(0.12f, 0.1f, 0.15f);
            var palette = new[] { Rgb(1f, 0.3f, 0.25f), Rgb(1f, 0.82f, 0.2f), Rgb(0.25f, 0.55f, 1f), Rgb(0.35f, 0.85f, 0.35f), Rgb(1f, 0.55f, 0.15f) };
            var c = palette[Mathf.FloorToInt(Hash(Wrap(ci, 4), Wrap(cj, 4), 471) * palette.Length) % palette.Length];
            float shade = 0.95f + (cu < 0.1f || cv > 0.9f ? 0.1f : 0f) - (cu > 0.9f || cv < 0.1f ? 0.12f : 0f);
            return Solid(c * shade);
        });

        // Torch Mines: dark timber planks laid down the ramp with two iron rails running along it
        static Texture2D MakeRampMine() => Design("RampMine", (u, v) =>
        {
            float rail = Mathf.Min(Mathf.Abs(v - 0.35f), Mathf.Abs(v - 0.65f));
            if (rail < 0.008f) return Rgb(0.55f, 0.55f, 0.58f);
            if (rail < 0.013f) return Rgb(0.2f, 0.2f, 0.22f);
            float x = u * 16f;
            int plank = Mathf.FloorToInt(x);
            if (x - plank < 0.06f) return Rgb(0.08f, 0.05f, 0.03f);
            float h = Hash(Wrap(plank, 16), 1, 481);
            float grain = Noise(v * 40f + h * 10f, (x - plank) * 3f, 64, 483);
            return Solid(Color.Lerp(Rgb(0.3f, 0.19f, 0.1f), Rgb(0.45f, 0.3f, 0.16f), h) * (0.8f + 0.3f * grain));
        });

        // Synthwave Station: glossy deep indigo with hot pink and white stripes along the ramp
        static Texture2D MakeRampSynth() => Design("RampSynth", (u, v) =>
        {
            float stripe = ToLine(v * 2f) * 4f;
            if (stripe < 0.05f) return Rgb(1f, 0.25f, 0.65f);
            if (Mathf.Abs(stripe - 0.25f) < 0.025f) return Rgb(0.95f, 0.95f, 1f);
            return Solid(Rgb(0.1f, 0.06f, 0.3f) * (0.85f + 0.3f * Mottle(u, v, 491)));
        });

        static Texture2D MakeRampSynthGlow() => Design("RampSynthGlow", (u, v) =>
        {
            float stripe = ToLine(v * 2f) * 4f;
            return stripe < 0.05f ? Rgb(1f, 0.2f, 0.6f) : Mathf.Abs(stripe - 0.25f) < 0.025f ? Rgb(0.5f, 0.55f, 0.8f) : Dark;
        });

        // Sandstone Temple: warm carved sandstone blocks with a stepped border motif
        static Texture2D MakeRampSandstone() => Design("RampSandstone", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 4f);
            float x = u * 4f + (row % 2) * 0.5f;
            float cu = x - Mathf.Floor(x), cv = v * 4f - row;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) * 2f;
            if (e < 0.03f) return Rgb(0.35f, 0.2f, 0.12f);
            float band = Mathf.Abs(e - 0.22f) < 0.025f || Mathf.Abs(e - 0.3f) < 0.012f ? 0.75f : 1f;
            float shade = (0.85f + 0.2f * Mottle(u, v, 501) + 0.1f * Hash(Wrap(Mathf.FloorToInt(x), 4), row, 503)) * band;
            return Solid(Rgb(0.88f, 0.6f, 0.38f) * shade);
        });

        // Walls: a leafy hedge (own colors)
        static Texture2D MakeWallHedge() => Design("WallHedge", (u, v) =>
        {
            var (f1, f2, id) = Voronoi(u * 24f, v * 24f, 24, 511);
            float leaf = 1f - Mathf.Clamp01(f1 * 1.6f);
            float light = 0.45f + 0.55f * leaf * (0.7f + 0.3f * Hash(id, 3, 513)) + 0.15f * Mottle(u, v, 515);
            if (f2 - f1 < 0.05f) light *= 0.55f;
            return Solid(Rgb(0.1f, 0.26f, 0.12f) * light);
        });

        // Walls: clean white lab panels with grey seams and small vents (grey, for tinting)
        static Texture2D MakeWallLab() => Design("WallLab", (u, v) =>
        {
            float cu = u * 4f % 1f, cv = v * 2f % 1f;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu) * 2f, Mathf.Min(cv, 1f - cv) * 4f);
            if (e < 0.03f) return Grey(0.45f);
            if (cv > 0.85f && cv < 0.93f && cu > 0.3f && cu < 0.7f && ToLine(u * 64f) < 0.25f) return Grey(0.35f);
            return Grey(0.93f + 0.04f * Mottle(u, v, 521) - (e < 0.06f ? 0.06f : 0f));
        });

        // Walls: big sandstone blocks with worn edges (grey, for tinting)
        static Texture2D MakeWallSandstone() => Design("WallSandstone", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 8f);
            float x = u * 4f + (row % 2) * 0.5f;
            float cu = x - Mathf.Floor(x), cv = v * 8f - row;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu) * 2f, Mathf.Min(cv, 1f - cv));
            float wear = Noise(u * 32f, v * 32f, 32, 531) * 0.03f;
            if (e < 0.025f + wear) return Grey(0.42f);
            return Grey(0.78f + 0.12f * Hash(Wrap(Mathf.FloorToInt(x), 4), row, 533) + 0.1f * (Mottle(u, v, 535) - 0.5f));
        });
    }
}
