using UnityEngine;

namespace VoidFlow.EditorTools
{
    // What glows on each design: emission maps that line up with the designs in
    // GrayboxBuilder.Designs (same 8m tiles). Ramp masks carry their own colors; wall masks are
    // white and take the zone's glow color. Bright lines run along the direction you travel
    // (ramps: along u; walls: horizontal): lines crossing your path flicker at surf speed, so
    // those stay faint and far apart.
    public static partial class GrayboxBuilder
    {
        static readonly Color Dark = new(0f, 0f, 0f, 1f);

        // Crimson: the red inlay round each slab and its diamond
        static Texture2D MakeRampCrimsonGlow() => Design("RampCrimsonGlow", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 2f);
            float x = u * 4f + (row % 2) * 0.5f;
            float cu = x - Mathf.Floor(x), cv = v * 2f - row;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu) * 2f, Mathf.Min(cv, 1f - cv) * 4f);
            float dia = Mathf.Abs(cu - 0.5f) * 2f + Mathf.Abs(cv - 0.5f) * 4f;
            bool lit = e >= 0.05f && (Mathf.Abs(e - 0.34f) < 0.035f || dia > 0.5f && dia < 0.58f);
            return lit ? Rgb(0.9f, 0.08f, 0.1f) : Dark;
        });

        // Forge: the seams between the plates run hot
        static Texture2D MakeRampForgeGlow() => Design("RampForgeGlow", (u, v) =>
        {
            float cu = u * 4f % 1f, cv = v * 4f % 1f;
            float along = Mathf.Min(cv, 1f - cv) * 2f, across = Mathf.Min(cu, 1f - cu) * 2f;
            if (along < 0.03f) return Rgb(1f, 0.42f, 0.08f);
            if (across < 0.03f) return Rgb(0.35f, 0.14f, 0.03f); // crossing seams: dimmer
            return Dark;
        });

        // Gallery: the blue line down the ramp
        static Texture2D MakeRampGalleryGlow() => Design("RampGalleryGlow", (u, v) =>
            Mathf.Abs(v - 0.5f) < 0.006f ? Rgb(0.3f, 0.6f, 1f) : Dark);

        // Sky Palace: the gold studs, and the white seams that run along the ramp
        static Texture2D MakeRampPalaceGlow() => Design("RampPalaceGlow", (u, v) =>
        {
            float bu = ToLine(u * 2f) * 4f, bv = ToLine(v * 2f) * 4f;
            if (bu + bv < 0.3f) return Rgb(1f, 0.75f, 0.3f);
            if (bv < 0.05f) return Rgb(0.45f, 0.5f, 0.6f);
            return Dark;
        });

        // Neon Rings: the cyan lines along the ramp bright, the magenta grid soft
        static Texture2D MakeRampNeonGlow() => Design("RampNeonGlow", (u, v) =>
        {
            if (ToLine(v * 2f) * 4f < 0.07f) return Rgb(0.3f, 0.95f, 1f);
            if (ToLine(v * 8f) < 0.03f) return Rgb(0.6f, 0.15f, 0.65f);
            if (ToLine(u * 2f) * 4f < 0.07f) return Rgb(0.08f, 0.25f, 0.28f);
            return Dark;
        });

        // Wireframe: red lines along the ramp
        static Texture2D MakeRampWireGlow() => Design("RampWireGlow", (u, v) =>
        {
            if (ToLine(v * 2f) * 4f < 0.05f) return Rgb(1f, 0.12f, 0.08f);
            if (ToLine(v * 8f) < 0.02f) return Rgb(0.4f, 0.04f, 0.03f);
            if (ToLine(u * 2f) * 4f < 0.05f) return Rgb(0.2f, 0.02f, 0.02f);
            return Dark;
        });

        // Grotto: the crystal veins
        static Texture2D MakeRampGrottoGlow() => Design("RampGrottoGlow", (u, v) =>
        {
            var (f1, f2, _) = Voronoi(u * 6f, v * 6f, 6, 191);
            float edge = f2 - f1;
            return edge < 0.06f ? Rgb(0.3f, 1f, 0.85f) * (1f - edge / 0.06f) : Dark;
        });

        // Spectrum: near-black panels ruled with fine lines. The ramp takes its color from the
        // glow (one hue per ramp); the lines along the ramp shine, the few across it are faint.
        static Texture2D MakeRampSpectrum() => Design("RampSpectrum", (u, v) =>
        {
            float shade = 0.06f + 0.03f * Mottle(u, v, 301);
            if (ToLine(v * 4f) * 2f < 0.05f || ToLine(u) * 8f < 0.05f) shade = 0.35f;
            return Grey(shade);
        });

        static Texture2D MakeRampSpectrumGlow() => Design("RampSpectrumGlow", (u, v) =>
        {
            if (ToLine(v * 4f) * 2f < 0.05f) return Grey(1f);   // along the ramp, every 2m
            if (ToLine(v * 4f) * 2f < 0.12f) return Grey(0.2f); // soft halo
            if (ToLine(u) * 8f < 0.05f) return Grey(0.3f);       // across, every 8m, faint
            return Dark;
        });

        // Wireframe walls, Spectrum panels: black panels with a glowing grid. Horizontal lines
        // every 4m (they run the way you travel) bright; the uprights every 8m faint. Kept
        // sparse and crisp: close-packed lines blur into a muddy wash from a distance.
        static Texture2D MakeWallGrid() => Design("WallGrid", (u, v) =>
        {
            float shade = 0.025f + 0.015f * Mottle(u, v, 311);
            if (ToLine(v * 2f) * 4f < 0.06f || ToLine(u) * 8f < 0.05f) shade = 0.25f;
            return Grey(shade);
        });

        static Texture2D MakeWallGridGlow() => Design("WallGridGlow", (u, v) =>
        {
            if (ToLine(v * 2f) * 4f < 0.06f) return Grey(1f);
            if (ToLine(u) * 8f < 0.05f) return Grey(0.2f);
            return Dark;
        });

        // Crimson walls: the round windows at the top of each arch
        static Texture2D MakeWallTraceryGlow() => Design("WallTraceryGlow", (u, v) =>
        {
            float ax = ((u * 2f) % 1f - 0.5f) * 4f, ay = v * 8f;
            float ring = new Vector2(ax, ay - 6.3f).magnitude;
            return Mathf.Abs(ring - 0.5f) < 0.08f ? Grey(0.22f) : Dark; // just a faint ring: filled, they read as polka dots
        });

        // Neon Rings walls: the rims of the hex vents, softly
        static Texture2D MakeWallHexVentsGlow() => Design("WallHexVentsGlow", (u, v) =>
        {
            if (Mathf.Min(ToLine(u * 2f), ToLine(v * 2f)) * 4f < 0.06f) return Dark;
            var (f1, f2, _) = HexCells(u * 14f, v * 8f * S3, 14, 8);
            return f2 - f1 < 0.12f ? Grey(0.5f) : Dark;
        });
    }
}
