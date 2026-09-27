using UnityEngine;

namespace VoidFlow.EditorTools
{
    // The finale's zone designs (8m tiles, own colors unless noted), each after the feel of a
    // legendary hard surf map (original designs, nothing copied): neon chevrons, castle stone,
    // hellish maroon panels, torii lacquer, a torchlit tomb, a copper sanctum, corrupted white
    // prisms, a patchwork of old textures, floating ruins and a stark pro finish.
    public static partial class GrayboxBuilder
    {
        // Omnific: pale panels with neon chevrons pointing down the ramp (grey, tinted per hue)
        static Texture2D MakeRampOmnific() => Design("RampOmnific", (u, v) =>
        {
            float cu = u * 4f % 1f, cv = v * 2f % 1f;
            float chevron = Mathf.Abs(cu - 0.5f - Mathf.Abs(cv - 0.5f) * 0.6f);
            if (chevron < 0.04f) return Grey(1f);
            if (ToLine(v * 4f) * 2f < 0.02f) return Grey(0.95f);
            return Grey(0.62f + 0.1f * Mottle(u, v, 601));
        });
        static Texture2D MakeRampOmnificGlow() => Design("RampOmnificGlow", (u, v) =>
        {
            float cu = u * 4f % 1f, cv = v * 2f % 1f;
            float chevron = Mathf.Abs(cu - 0.5f - Mathf.Abs(cv - 0.5f) * 0.6f);
            if (chevron < 0.04f) return Grey(1f);
            if (chevron < 0.08f) return Grey(0.25f);
            return ToLine(v * 4f) * 2f < 0.02f ? Grey(0.6f) : Dark;
        });

        // Castle walls: big grey stones laid in courses, dark mortar, a little moss low down
        static Texture2D MakeRampCastle() => Design("RampCastle", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 5f);
            float x = u * 3f + Hash(row, 1, 611) * 0.8f;
            int col = Mathf.FloorToInt(x);
            float cu = x - col, cv = v * 5f - row;
            float e = Mathf.Min(Mathf.Min(cu * 3f, (1f - cu) * 3f), Mathf.Min(cv, 1f - cv));
            if (e < 0.05f) return Rgb(0.16f, 0.15f, 0.14f);
            float stone = 0.42f + 0.14f * Hash(Wrap(col, 3), Wrap(row, 5), 613) + 0.12f * Mottle(u * 2f, v * 2f, 615);
            var c = Rgb(stone, stone * 0.97f, stone * 0.92f);
            float moss = Mathf.Clamp01((Mottle(u * 3f, v * 3f, 617) - 0.62f) * 4f) * (e < 0.15f ? 1f : 0.4f);
            return Solid(Color.Lerp(c, Rgb(0.22f, 0.3f, 0.14f), moss));
        });
        static Texture2D MakeWallCastle() => Design("WallCastle", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 8f);
            float x = u * 5f + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float cu = x - col, cv = v * 8f - row;
            float e = Mathf.Min(Mathf.Min(cu * 1.6f, (1f - cu) * 1.6f), Mathf.Min(cv, 1f - cv));
            if (e < 0.06f) return Grey(0.3f);
            return Grey(0.7f + 0.2f * Hash(Wrap(col, 5), row, 619) + 0.1f * Mottle(u, v, 621));
        });

        // 666: deep maroon panels, riveted, with bright strip lights between them
        static Texture2D MakeRampHell() => Design("RampHell", (u, v) =>
        {
            float cu = u * 2f % 1f, cv = v * 4f % 1f;
            if (Mathf.Abs(cv - 0.5f) > 0.46f) return Rgb(1f, 0.55f, 0.4f);
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu) * 4f, Mathf.Min(cv, 1f - cv));
            float rivet = (Mathf.Abs(cu - 0.08f) < 0.012f || Mathf.Abs(cu - 0.92f) < 0.012f) && Mathf.Abs(cv - 0.5f) < 0.3f && ToLine(cv * 6f) < 0.12f ? 1.5f : 1f;
            float shade = (0.75f + 0.25f * Mathf.Clamp01(e * 6f) + 0.12f * Mottle(u, v, 631)) * rivet;
            return Solid(Rgb(0.36f, 0.08f, 0.07f) * shade);
        });
        static Texture2D MakeRampHellGlow() => Design("RampHellGlow", (u, v) =>
            Mathf.Abs(v * 4f % 1f - 0.5f) > 0.46f ? Rgb(1f, 0.3f, 0.15f) : Dark);

        // Jade shrine: vermilion lacquer with black bands and gold studs, like a torii gate
        static Texture2D MakeRampTorii() => Design("RampTorii", (u, v) =>
        {
            float cv = v * 2f % 1f;
            if (Mathf.Abs(cv - 0.5f) > 0.44f) return Rgb(0.06f, 0.05f, 0.05f);
            if (Mathf.Abs(cv - 0.5f) > 0.41f && ToLine(u * 16f) < 0.1f) return Rgb(0.95f, 0.75f, 0.3f);
            float grain = 0.88f + 0.12f * Mottle(u, v * 6f, 641) + 0.05f * Mathf.Sin(v * 180f + Mottle(u, v, 643) * 6f);
            return Solid(Rgb(0.78f, 0.13f, 0.07f) * grain);
        });

        // Tomb of Anubis: dark sandstone carved with rows of little glyphs, gold at the seams
        static Texture2D MakeRampTomb() => Design("RampTomb", (u, v) =>
        {
            float cv = v * 4f % 1f;
            if (Mathf.Abs(cv - 0.5f) > 0.47f) return Rgb(0.85f, 0.62f, 0.22f);
            float stone = 0.8f + 0.15f * Mottle(u, v, 651);
            var sand = Rgb(0.62f, 0.42f, 0.24f) * stone;
            // A glyph in each cell: a few strokes picked by hash
            float gu = u * 16f, gv = cv * 3f;
            int gx = Mathf.FloorToInt(gu), gy = Mathf.FloorToInt(gv);
            float lu = gu - gx, lv = gv - gy;
            int row = Mathf.FloorToInt(v * 4f);
            float h = Hash(Wrap(gx, 16), row * 3 + gy, 653);
            bool cut = false;
            if (Mathf.Abs(cv - 0.5f) < 0.36f)
            {
                if (h < 0.3f) cut = Mathf.Abs(lu - 0.5f) < 0.07f && lv > 0.2f && lv < 0.8f;                         // staff
                else if (h < 0.55f) cut = Mathf.Abs(new Vector2(lu - 0.5f, lv - 0.55f).magnitude - 0.22f) < 0.06f;   // sun
                else if (h < 0.75f) cut = Mathf.Abs(lv - 0.3f) < 0.06f && lu > 0.2f && lu < 0.8f || Mathf.Abs(lu - 0.3f) < 0.06f && lv > 0.3f && lv < 0.8f; // step
                else cut = Mathf.Abs(Mathf.Abs(lu - 0.5f) - (0.8f - lv) * 0.4f) < 0.06f && lv > 0.2f;              // pyramid
            }
            return Solid(cut ? sand * 0.55f : sand);
        });

        // Deity's sanctum: copper plates with diamond inlays
        static Texture2D MakeRampDeity() => Design("RampDeity", (u, v) =>
        {
            float cu = u * 4f % 1f, cv = v * 2f % 1f;
            float d = Mathf.Abs(cu - 0.5f) + Mathf.Abs(cv - 0.5f);
            if (Mathf.Abs(d - 0.3f) < 0.025f) return Rgb(1f, 0.75f, 0.45f);
            if (d < 0.3f) return Solid(Rgb(0.35f, 0.16f, 0.08f) * (0.85f + 0.2f * Mottle(u, v, 661)));
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv));
            float patina = Mathf.Clamp01((Mottle(u * 2f, v * 2f, 663) - 0.6f) * 3f);
            var copper = Rgb(0.72f, 0.38f, 0.2f) * (e < 0.02f ? 0.5f : 0.85f + 0.2f * Mottle(u, v, 665));
            return Solid(Color.Lerp(copper, Rgb(0.3f, 0.55f, 0.45f), patina * 0.5f));
        });
        static Texture2D MakeWallCopper() => Design("WallCopper", (u, v) =>
        {
            float slat = u * 24f % 1f;
            if (slat < 0.12f) return Grey(0.2f);
            return Grey((0.7f + 0.2f * Mottle(u * 8f, v, 667)) * (0.85f + 0.15f * slat));
        });

        // Corruption: white prism panels broken up by glitching violet scanlines and blocks
        static Texture2D MakeRampCorrupt() => Design("RampCorrupt", (u, v) =>
        {
            int band = Mathf.FloorToInt(v * 32f);
            float glitch = Hash(band, 0, 671);
            float shift = glitch > 0.8f ? (Hash(band, 1, 673) - 0.5f) * 0.3f : 0f;
            float x = u + shift;
            if (glitch > 0.9f && Hash(Mathf.FloorToInt(x * 12f), band, 675) > 0.5f) return Rgb(0.55f, 0.1f, 0.9f);
            if (ToLine(x * 2f) < 0.01f) return Rgb(0.6f, 0.3f, 1f);
            return Grey(0.93f + 0.05f * Mottle(u, v, 677));
        });
        static Texture2D MakeRampCorruptGlow() => Design("RampCorruptGlow", (u, v) =>
        {
            int band = Mathf.FloorToInt(v * 32f);
            float glitch = Hash(band, 0, 671);
            float shift = glitch > 0.8f ? (Hash(band, 1, 673) - 0.5f) * 0.3f : 0f;
            float x = u + shift;
            if (glitch > 0.9f && Hash(Mathf.FloorToInt(x * 12f), band, 675) > 0.5f) return Rgb(0.6f, 0.1f, 1f);
            return ToLine(x * 2f) < 0.01f ? Rgb(0.5f, 0.2f, 1f) : Dark;
        });

        // Sinsane: a patchwork of old-map textures, each patch its own (brick, planks, plate, tile)
        static Texture2D MakeRampPatchwork() => Design("RampPatchwork", (u, v) =>
        {
            int px = Mathf.FloorToInt(u * 2f), py = Mathf.FloorToInt(v * 2f);
            float lu = u * 2f - px, lv = v * 2f - py;
            if (Mathf.Min(Mathf.Min(lu, 1f - lu), Mathf.Min(lv, 1f - lv)) < 0.01f) return Grey(0.15f);
            int kind = Mathf.FloorToInt(Hash(px, py, 681) * 4f);
            float m = Mottle(u, v, 683);
            switch (kind)
            {
                case 0: // brick
                {
                    int row = Mathf.FloorToInt(lv * 10f);
                    float bx = lu * 5f + (row % 2) * 0.5f;
                    bool mortar = ToLine(lv * 10f) < 0.06f || ToLine(bx) < 0.04f;
                    return mortar ? Rgb(0.55f, 0.5f, 0.45f) : Solid(Rgb(0.55f, 0.28f, 0.2f) * (0.85f + 0.25f * m));
                }
                case 1: // planks
                    return ToLine(lu * 8f) < 0.04f ? Rgb(0.2f, 0.12f, 0.06f) : Solid(Rgb(0.6f, 0.42f, 0.24f) * (0.8f + 0.2f * Mottle(u * 8f, v, 685)));
                case 2: // tread plate
                    return (ToLine(lu * 12f + lv * 12f) < 0.07f || ToLine(lu * 12f - lv * 12f) < 0.07f) ? Grey(0.65f) : Grey(0.45f + 0.1f * m);
                default: // grey dev tile
                    return ToLine(lu * 4f) < 0.02f || ToLine(lv * 4f) < 0.02f ? Grey(0.4f) : Grey(0.72f + 0.06f * m);
            }
        });

        // Essentia: weathered grey stone slabs lined in faded gold, among the floating ruins
        static Texture2D MakeRampRuins() => Design("RampRuins", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 3f);
            float x = u * 2f + Hash(row, 3, 691) * 0.6f;
            int col = Mathf.FloorToInt(x);
            float cu = x - col, cv = v * 3f - row;
            float e = Mathf.Min(Mathf.Min(cu * 1.5f, (1f - cu) * 1.5f), Mathf.Min(cv, 1f - cv));
            if (e < 0.03f) return Rgb(0.3f, 0.3f, 0.32f);
            if (Mathf.Abs(e - 0.09f) < 0.012f) return Rgb(0.85f, 0.72f, 0.45f);
            float crack = Mathf.Abs(Mottle(u * 6f, v * 6f, 693) - 0.5f) < 0.012f ? 0.6f : 1f;
            float stone = (0.62f + 0.1f * Hash(Wrap(col, 2), row, 695) + 0.12f * Mottle(u, v, 697)) * crack;
            return Solid(Rgb(stone * 0.97f, stone * 0.96f, stone));
        });

        // Pro: matte black, one thin white rule along each edge band and a red line down it
        static Texture2D MakeRampPro() => Design("RampPro", (u, v) =>
        {
            float cv = v * 4f % 1f;
            if (Mathf.Abs(cv - 0.5f) > 0.48f) return Grey(0.95f);
            if (Mathf.Abs(cv - 0.5f) < 0.006f && ToLine(u * 8f) < 0.4f) return Rgb(0.9f, 0.08f, 0.1f);
            return Grey(0.05f + 0.025f * Mottle(u, v, 701));
        });
        static Texture2D MakeRampProGlow() => Design("RampProGlow", (u, v) =>
        {
            float cv = v * 4f % 1f;
            if (Mathf.Abs(cv - 0.5f) > 0.48f) return Grey(0.7f);
            return Mathf.Abs(cv - 0.5f) < 0.006f && ToLine(u * 8f) < 0.4f ? Rgb(1f, 0.1f, 0.12f) : Dark;
        });
    }
}
