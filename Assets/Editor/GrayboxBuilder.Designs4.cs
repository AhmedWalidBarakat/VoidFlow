using UnityEngine;

namespace VoidFlow.EditorTools
{
    // The Legend zones' ramp designs (8m tiles; u along the ramp, v down its face), each after
    // the look of a map from the free surf gameplay playlist (original designs, nothing copied)
    public static partial class GrayboxBuilder
    {
        static float Frac(float x) => x - Mathf.Floor(x);

        // Love Tunnel: rose pink with rows of little hearts
        static Texture2D MakeRampLove() => Design("RampLove", (u, v) =>
        {
            float x = Frac(u * 8f) - 0.5f, y = Frac(v * 4f) - 0.45f;
            float heart = Mathf.Pow(x * x + y * y - 0.06f, 3f) - x * x * y * y * y * 1.6f;
            if (heart < 0f) return Rgb(1f, 0.55f, 0.8f);
            return Solid(Rgb(0.62f, 0.18f, 0.5f) * (0.85f + 0.2f * Mottle(u, v, 801)));
        });
        static Texture2D MakeRampLoveGlow() => Design("RampLoveGlow", (u, v) =>
        {
            float x = Frac(u * 8f) - 0.5f, y = Frac(v * 4f) - 0.45f;
            return Mathf.Pow(x * x + y * y - 0.06f, 3f) - x * x * y * y * y * 1.6f < 0f ? Rgb(1f, 0.4f, 0.75f) : Dark;
        });

        // Cornfield Sunset: green crop rows running down the ramp
        static Texture2D MakeRampCornfield() => Design("RampCornfield", (u, v) =>
        {
            float row = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 16f));
            var green = Rgb(0.28f, 0.55f, 0.16f) * (0.8f + 0.3f * Mottle(u * 2f, v * 2f, 811));
            return Solid(Color.Lerp(Rgb(0.45f, 0.35f, 0.18f), green, Mathf.SmoothStep(0.2f, 0.6f, row)));
        });

        // Neon Shapes: black with outlined triangles and rings
        static Texture2D MakeRampNeonShapes() => Design("RampNeonShapes", (u, v) => NeonShape(u, v) > 0f ? Rgb(0.3f, 0.95f, 1f) : Solid(Grey(0.04f + 0.02f * Mottle(u, v, 821))));
        static Texture2D MakeRampNeonShapesGlow() => Design("RampNeonShapesGlow", (u, v) => NeonShape(u, v) > 0f ? (Hash(Mathf.FloorToInt(u * 4f), Mathf.FloorToInt(v * 2f), 823) > 0.5f ? Rgb(1f, 0.3f, 0.8f) : Rgb(0.3f, 0.95f, 1f)) : Dark);
        static float NeonShape(float u, float v)
        {
            int cx = Mathf.FloorToInt(u * 4f), cy = Mathf.FloorToInt(v * 2f);
            float x = Frac(u * 4f) - 0.5f, y = Frac(v * 2f) - 0.5f;
            if (Hash(cx, cy, 825) > 0.5f) return Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.28f) < 0.025f ? 1f : 0f;
            float tri = Mathf.Max(Mathf.Abs(x) * 1.7f + y * 0.85f, -y) - 0.22f;
            return Mathf.Abs(tri) < 0.022f ? 1f : 0f;
        }

        // Glacier Cave: pale blue ice, cracked
        static Texture2D MakeRampGlacier() => Design("RampGlacier", (u, v) =>
        {
            var (f1, f2, _) = Voronoi(u * 6f, v * 3f, 6, 831);
            float crack = Mathf.Clamp01((f2 - f1) * 12f);
            var ice = Color.Lerp(Rgb(0.55f, 0.72f, 0.88f), Rgb(0.85f, 0.95f, 1f), Mottle(u, v, 833));
            return Solid(Color.Lerp(Rgb(0.3f, 0.45f, 0.62f), ice, crack));
        });

        // Red Line Lab: white panels ruled with red lines
        static Texture2D MakeRampRedLine() => Design("RampRedLine", (u, v) =>
        {
            if (ToLine(v * 4f) < 0.012f) return Rgb(1f, 0.1f, 0.12f);
            if (ToLine(u * 4f) < 0.004f) return Grey(0.7f);
            return Grey(0.92f + 0.05f * Mottle(u, v, 841));
        });
        static Texture2D MakeRampRedLineGlow() => Design("RampRedLineGlow", (u, v) => ToLine(v * 4f) < 0.012f ? Rgb(1f, 0.1f, 0.12f) : Dark);

        // Concrete Bunker: grey cast panels with round porthole recesses
        static Texture2D MakeRampBunker() => Design("RampBunker", (u, v) =>
        {
            float x = Frac(u * 4f) - 0.5f, y = Frac(v * 2f) - 0.5f;
            float r = Mathf.Sqrt(x * x + y * y);
            float g = 0.5f + 0.12f * Mottle(u * 2f, v * 2f, 851);
            if (Mathf.Abs(r - 0.2f) < 0.02f) g *= 0.6f;
            else if (r < 0.2f) g *= 0.8f;
            if (ToLine(u * 4f) < 0.006f || ToLine(v * 2f) < 0.006f) g *= 0.55f;
            return Grey(g);
        });

        // Cyan Crystal: faceted cyan stone
        static Texture2D MakeRampCyanCrystal() => Design("RampCyanCrystal", (u, v) =>
        {
            var (f1, f2, id) = Voronoi(u * 5f, v * 3f, 5, 861);
            float facet = 0.6f + 0.4f * Hash(id, 1, 863);
            if (f2 - f1 < 0.04f) return Rgb(0.6f, 1f, 1f);
            return Solid(Rgb(0.1f, 0.62f, 0.7f) * facet);
        });

        // Chrome Wave: a checker of mirror-bright and dark chrome tiles
        static Texture2D MakeRampChromeWave() => Design("RampChromeWave", (u, v) =>
        {
            int cx = Mathf.FloorToInt(u * 16f), cy = Mathf.FloorToInt(v * 8f);
            float shade = (cx + cy) % 2 == 0 ? 0.85f : 0.45f;
            shade += 0.1f * Mathf.Sin(u * 30f + v * 12f);
            return Grey(shade * (ToLine(u * 16f) < 0.03f || ToLine(v * 8f) < 0.03f ? 0.6f : 1f));
        });

        // Light Streaks: deep blue with white streaks racing down the ramp
        static Texture2D MakeRampStreak() => Design("RampStreak", (u, v) =>
        {
            float s = Hash(Mathf.FloorToInt(v * 32f), 0, 871);
            if (s > 0.7f && Frac(u * 2f + s * 3f) < 0.35f) return Rgb(0.8f, 0.9f, 1f);
            return Solid(Rgb(0.05f, 0.08f, 0.28f) * (0.8f + 0.3f * Mottle(u, v, 873)));
        });
        static Texture2D MakeRampStreakGlow() => Design("RampStreakGlow", (u, v) =>
        {
            float s = Hash(Mathf.FloorToInt(v * 32f), 0, 871);
            return s > 0.7f && Frac(u * 2f + s * 3f) < 0.35f ? Rgb(0.6f, 0.8f, 1f) : Dark;
        });

        // Great Wall: old grey-brown stone blocks
        static Texture2D MakeRampGreatWall() => Design("RampGreatWall", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 8f);
            float x = u * 6f + (row % 2) * 0.5f;
            if (ToLine(v * 8f) < 0.05f || ToLine(x) < 0.03f) return Rgb(0.3f, 0.28f, 0.25f);
            float s = 0.55f + 0.15f * Hash(Wrap(Mathf.FloorToInt(x), 6), row, 881) + 0.1f * Mottle(u, v, 883);
            return Solid(Rgb(s, s * 0.95f, s * 0.86f));
        });

        // Arcade Monsters: little pixel invaders in green on black
        static Texture2D MakeRampArcade() => Design("RampArcade", (u, v) => Invader(u, v) ? Rgb(0.35f, 1f, 0.3f) : Solid(Grey(0.05f)));
        static Texture2D MakeRampArcadeGlow() => Design("RampArcadeGlow", (u, v) => Invader(u, v) ? Rgb(0.3f, 1f, 0.25f) : Dark);
        static bool Invader(float u, float v)
        {
            int px = Mathf.FloorToInt(Frac(u * 4f) * 16f) - 2, py = Mathf.FloorToInt(Frac(v * 2f) * 12f) - 2;
            if (px < 0 || px > 10 || py < 0 || py > 7) return false;
            string[] art = { "00100000100", "00010001000", "00111111100", "01101110110", "11111111111", "10111111101", "10100000101", "00011011000" };
            return art[py][px] == '1';
        }

        // Hazard Foundry: black with bold orange diagonal bands
        static Texture2D MakeRampHazard() => Design("RampHazard", (u, v) => Frac(u * 8f + v * 4f) < 0.35f ? Rgb(1f, 0.5f, 0.05f) : Solid(Grey(0.06f + 0.03f * Mottle(u, v, 891))));
        static Texture2D MakeRampHazardGlow() => Design("RampHazardGlow", (u, v) => Frac(u * 8f + v * 4f) < 0.35f ? Rgb(1f, 0.45f, 0.05f) * 0.6f : Dark);

        // Hex Jungle: dark glass hexes edged in green light
        static Texture2D MakeRampHexJungle() => Design("RampHexJungle", (u, v) => HexEdge(u * 10f, v * 5f) ? Rgb(0.3f, 1f, 0.4f) : Solid(Rgb(0.08f, 0.18f, 0.12f) * (0.8f + 0.3f * Mottle(u, v, 901))));
        static Texture2D MakeRampHexJungleGlow() => Design("RampHexJungleGlow", (u, v) => HexEdge(u * 10f, v * 5f) ? Rgb(0.3f, 1f, 0.4f) : Dark);
        static bool HexEdge(float x, float y)
        {
            float r = Frac(y) < 0.5f ? 0f : 0.5f;
            float hx = Frac(x + r) - 0.5f, hy = Frac(y * 2f) - 0.5f;
            float d = Mathf.Max(Mathf.Abs(hx) * 0.87f + Mathf.Abs(hy) * 0.5f, Mathf.Abs(hy));
            return d > 0.46f;
        }

        // Dark Foundry: dark steel plates with salmon light seams
        static Texture2D MakeRampIndustrial() => Design("RampIndustrial", (u, v) =>
        {
            if (ToLine(u * 2f) < 0.008f) return Rgb(1f, 0.55f, 0.45f);
            if (ToLine(v * 4f) < 0.01f) return Grey(0.1f);
            return Grey(0.22f + 0.08f * Mottle(u * 2f, v * 2f, 911));
        });
        static Texture2D MakeRampIndustrialGlow() => Design("RampIndustrialGlow", (u, v) => ToLine(u * 2f) < 0.008f ? Rgb(1f, 0.5f, 0.4f) : Dark);

        // Rock Quarry: weathered wooden planks laid down the ramp
        static Texture2D MakeRampQuarry() => Design("RampQuarry", (u, v) =>
        {
            int plank = Mathf.FloorToInt(v * 12f);
            if (ToLine(v * 12f) < 0.06f) return Rgb(0.2f, 0.14f, 0.08f);
            float grain = 0.8f + 0.2f * Mottle(u * 4f, v * 12f, 921) + 0.1f * Hash(plank, 0, 923);
            return Solid(Rgb(0.55f, 0.4f, 0.25f) * grain);
        });

        // Mauve Temple: pink stone slabs with a carved border
        static Texture2D MakeRampMauve() => Design("RampMauve", (u, v) =>
        {
            float cv = Frac(v * 2f);
            if (Mathf.Abs(cv - 0.5f) > 0.44f) return Rgb(0.5f, 0.3f, 0.4f);
            if (ToLine(u * 4f) < 0.01f) return Rgb(0.55f, 0.35f, 0.45f);
            return Solid(Rgb(0.82f, 0.6f, 0.7f) * (0.85f + 0.2f * Mottle(u, v, 931)));
        });

        // Surf School: black and white chevrons pointing down the ramp
        static Texture2D MakeRampSurfSchool() => Design("RampSurfSchool", (u, v) => Frac(u * 4f + Mathf.Abs(Frac(v * 2f) - 0.5f)) < 0.5f ? Grey(0.95f) : Grey(0.07f));

        // Vapor Geometry: pink and purple angled panels on black
        static Texture2D MakeRampVapor() => Design("RampVapor", (u, v) =>
        {
            float a = Frac(u * 3f - v * 1.5f);
            if (a < 0.04f) return Rgb(1f, 0.35f, 0.75f);
            return Solid(a < 0.5f ? Rgb(0.35f, 0.12f, 0.5f) : Rgb(0.12f, 0.05f, 0.18f));
        });
        static Texture2D MakeRampVaporGlow() => Design("RampVaporGlow", (u, v) => Frac(u * 3f - v * 1.5f) < 0.04f ? Rgb(1f, 0.3f, 0.7f) : Dark);

        // Flag Hills: dusty sand with red pennant stripes along the edge
        static Texture2D MakeRampFlags() => Design("RampFlags", (u, v) =>
        {
            float cv = Frac(v * 2f);
            if (cv > 0.9f && Frac(u * 8f) < 0.5f) return Rgb(0.85f, 0.12f, 0.12f);
            return Solid(Rgb(0.78f, 0.68f, 0.5f) * (0.85f + 0.2f * Mottle(u, v, 941)));
        });

        // Old Warehouse: dark wooden boards with a painted stripe
        static Texture2D MakeRampWarehouse() => Design("RampWarehouse", (u, v) =>
        {
            if (Mathf.Abs(Frac(v * 2f) - 0.5f) < 0.04f) return Rgb(0.95f, 0.85f, 0.3f);
            if (ToLine(v * 16f) < 0.05f) return Rgb(0.15f, 0.1f, 0.06f);
            return Solid(Rgb(0.42f, 0.28f, 0.16f) * (0.8f + 0.25f * Mottle(u * 4f, v * 16f, 951)));
        });

        // Sea Mines: white and ocean-blue bands
        static Texture2D MakeRampSeaMine() => Design("RampSeaMine", (u, v) => Frac(v * 4f) < 0.5f ? Grey(0.95f) : Solid(Rgb(0.1f, 0.4f, 0.75f) * (0.9f + 0.15f * Mottle(u, v, 961))));

        // Moon Crater: grey regolith pocked with small craters, teal guide lines
        static Texture2D MakeRampMoon() => Design("RampMoon", (u, v) =>
        {
            if (ToLine(v * 4f) < 0.008f) return Rgb(0.3f, 1f, 0.9f);
            var (f1, _, id) = Voronoi(u * 12f, v * 6f, 12, 971);
            float g = 0.45f + 0.15f * Mottle(u * 2f, v * 2f, 973);
            if (Hash(id, 2, 975) > 0.6f && f1 < 0.18f) g *= f1 < 0.12f ? 0.7f : 1.25f;
            return Grey(g);
        });
        static Texture2D MakeRampMoonGlow() => Design("RampMoonGlow", (u, v) => ToLine(v * 4f) < 0.008f ? Rgb(0.3f, 1f, 0.9f) : Dark);

        // Stripe Pyramid: olive and cream stripes fanning across the ramp
        static Texture2D MakeRampStripe() => Design("RampStripe", (u, v) => Frac(u * 6f + v * 0.5f) < 0.5f ? Solid(Rgb(0.52f, 0.5f, 0.3f)) : Solid(Rgb(0.9f, 0.85f, 0.7f)));

        // Race Arena: red, white and black racing stripes
        static Texture2D MakeRampRaceTrack() => Design("RampRaceTrack", (u, v) =>
        {
            float c = Frac(v * 2f);
            if (c < 0.15f || c > 0.85f) return Frac(u * 8f) < 0.5f ? Rgb(0.9f, 0.1f, 0.1f) : Grey(0.95f);
            return Solid(Grey(0.12f + 0.05f * Mottle(u, v, 981)));
        });
        static Texture2D MakeRampRaceTrackGlow() => Design("RampRaceTrackGlow", (u, v) =>
        {
            float c = Frac(v * 2f);
            return (c < 0.15f || c > 0.85f) && Frac(u * 8f) < 0.5f ? Rgb(1f, 0.1f, 0.1f) * 0.5f : Dark;
        });

        // Fruit Stages: bunches of purple grapes on lime green
        static Texture2D MakeRampFruit() => Design("RampFruit", (u, v) =>
        {
            var (f1, _, id) = Voronoi(u * 10f, v * 5f, 10, 991);
            if (f1 < 0.35f) return Solid(Color.Lerp(Rgb(0.45f, 0.15f, 0.6f), Rgb(0.7f, 0.4f, 0.85f), 1f - f1 / 0.35f) * (0.8f + 0.3f * Hash(id, 3, 993)));
            return Solid(Rgb(0.55f, 0.85f, 0.25f));
        });

        // Surf Town: dark asphalt with painted road lines
        static Texture2D MakeRampTown() => Design("RampTown", (u, v) =>
        {
            float c = Frac(v * 2f);
            if (Mathf.Abs(c - 0.5f) < 0.02f && Frac(u * 4f) < 0.5f) return Rgb(0.95f, 0.85f, 0.2f);
            if (c < 0.04f || c > 0.96f) return Grey(0.9f);
            return Grey(0.18f + 0.06f * Mottle(u * 4f, v * 4f, 1001));
        });

        // Wild Hills: meadow grass with patches of dirt
        static Texture2D MakeRampHills() => Design("RampHills", (u, v) =>
        {
            float dirt = Mathf.Clamp01((Mottle(u * 2f, v * 2f, 1011) - 0.6f) * 5f);
            var grass = Rgb(0.3f, 0.58f, 0.2f) * (0.8f + 0.3f * Mottle(u * 8f, v * 8f, 1013));
            return Solid(Color.Lerp(grass, Rgb(0.45f, 0.33f, 0.2f), dirt));
        });
    }
}
