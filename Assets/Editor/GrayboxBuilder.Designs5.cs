using UnityEngine;

namespace VoidFlow.EditorTools
{
    // The real maps' own looks (8m tiles; u along the ramp or wall, v down its face), drawn
    // after their footage
    public static partial class GrayboxBuilder
    {
        // surf_utopia_njv: smooth off-white plaster ramps
        static Texture2D MakeRampUtopia() => Design("RampUtopia", (u, v) =>
        {
            float seam = ToLine(u * 2f) < 0.004f ? 0.9f : 1f;
            return Solid(Rgb(0.91f, 0.87f, 0.83f) * (0.93f + 0.07f * Mottle(u, v, 1101)) * seam);
        });

        // ...and its halls: off-white walls banded with a thick orange line, a steel-blue
        // panel, another orange line and a thin grey one
        static Texture2D MakeWallUtopia() => Design("WallUtopia", (u, v) =>
        {
            float y = Frac(v);
            if (y > 0.40f && y < 0.435f) return Rgb(0.95f, 0.42f, 0.1f);
            if (y >= 0.435f && y < 0.56f) return Solid(Rgb(0.35f, 0.5f, 0.68f) * (0.92f + 0.08f * Mottle(u, v, 1103)));
            if (y >= 0.56f && y < 0.595f) return Rgb(0.95f, 0.42f, 0.1f);
            if (y > 0.63f && y < 0.645f) return Grey(0.55f);
            return Solid(Rgb(0.9f, 0.86f, 0.82f) * (0.93f + 0.07f * Mottle(u, v, 1105)));
        });

        // surf_mesa: light grey ramps ruled with dark tile seams, a glowing cyan strip along
        // the top
        static Texture2D MakeRampMesa() => Design("RampMesa", (u, v) =>
        {
            if (v < 0.035f) return Rgb(0.2f, 0.9f, 1f);
            if (ToLine(u * 8f) < 0.012f || ToLine(v * 4f) < 0.008f) return Grey(0.32f);
            return Grey(0.7f + 0.08f * Mottle(u * 2f, v * 2f, 1111));
        });
        static Texture2D MakeRampMesaGlow() => Design("RampMesaGlow", (u, v) => v < 0.035f ? Rgb(0.2f, 0.9f, 1f) : Dark);

        // surf_not_so_funhouse: red and black circus stripes with a cyan neon edge
        static Texture2D MakeRampFunhouse() => Design("RampFunhouse", (u, v) =>
        {
            if (v > 0.965f) return Rgb(0.2f, 1f, 1f);
            return Frac(u * 8f) < 0.5f ? Solid(Rgb(0.55f, 0.06f, 0.05f) * (0.9f + 0.1f * Mottle(u, v, 1121))) : Rgb(0.05f, 0.02f, 0.02f);
        });
        static Texture2D MakeRampFunhouseGlow() => Design("RampFunhouseGlow", (u, v) => v > 0.965f ? Rgb(0.2f, 1f, 1f) : Dark);

        // surf_frags_nightmare: plain light grey developer tiles, a line every metre
        static Texture2D MakeRampDevGrid() => Design("RampDevGrid", (u, v) =>
        {
            if (ToLine(u * 8f) < 0.01f || ToLine(v * 8f) < 0.01f) return Grey(ToLine(u * 2f) < 0.01f || ToLine(v * 2f) < 0.01f ? 0.45f : 0.55f);
            return Grey(0.74f + 0.04f * Mottle(u, v, 1131));
        });
    }
}
