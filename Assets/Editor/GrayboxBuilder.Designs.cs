using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Each zone's own ramp surface and a few wall patterns, painted in code so they're original.
    // Every texture here covers 8m x 8m (ramps map 2m per UV, walls 4m, and the materials scale
    // to match). Ramp designs carry their own colors; the wall patterns are grey and get tinted.
    public static partial class GrayboxBuilder
    {
        const int DesignSize = 512;
        const float S3 = 1.7320508f;

        static Texture2D Design(string name, System.Func<float, float, Color> pixel) => Paint(name, pixel, DesignSize);

        static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }

        static int Wrap(int i, int n) => ((i % n) + n) % n;
        static Color Rgb(float r, float g, float b) => new(r, g, b, 1f);
        static Color Solid(Color c) { c.a = 1f; return c; }

        // Distance (in cells) from x to the nearest whole number: for grid lines
        static float ToLine(float x) => Mathf.Abs(x - Mathf.Round(x));

        // Nearest and second-nearest jittered points on a grid wrapping every `n` cells
        static (float f1, float f2, int id) Voronoi(float x, float y, int n, int seed)
        {
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = 9f, f2 = 9f; int id = 0;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                float px = gx + 0.15f + 0.7f * Hash(Wrap(gx, n), Wrap(gy, n), seed);
                float py = gy + 0.15f + 0.7f * Hash(Wrap(gx, n), Wrap(gy, n), seed + 1);
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < f1) { f2 = f1; f1 = d; id = Wrap(gx, n) * 97 + Wrap(gy, n); }
                else if (d < f2) f2 = d;
            }
            return (f1, f2, id);
        }

        // Nearest and second-nearest centres of a hex lattice (1 apart): for hex tiles.
        // x wraps every `nx`, y every `ny` rows of S3.
        static (float f1, float f2, int id) HexCells(float x, float y, int nx, int ny)
        {
            float f1 = 9f, f2 = 9f; int id = 0;
            for (int k = 0; k < 2; k++)
            {
                float ox = k * 0.5f, oy = k * S3 * 0.5f;
                int ci = Mathf.FloorToInt(x - ox), cj = Mathf.FloorToInt((y - oy) / S3);
                for (int j = -1; j <= 2; j++)
                for (int i = -1; i <= 2; i++)
                {
                    float px = ci + i + ox, py = (cj + j) * S3 + oy;
                    float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                    if (d < f1) { f2 = f1; f1 = d; id = (Wrap(ci + i, nx) * 31 + Wrap(cj + j, ny)) * 2 + k; }
                    else if (d < f2) f2 = d;
                }
            }
            return (f1, f2, id);
        }

        // A pointed (gothic) arch opening, `w` half-wide, straight sides up to `spring`, from
        // `sill` up; x is across from its centre line
        static bool InArch(float x, float y, float w, float sill, float spring)
        {
            if (y < sill) return false;
            if (y < spring) return Mathf.Abs(x) < w;
            float dx = Mathf.Abs(x) + w, dy = y - spring;
            return dx * dx + dy * dy < 4f * w * w;
        }

        // ------------------------------------------------------------------ ramps

        // Crimson: charcoal stone slabs, staggered, each with a crimson inlay and a diamond
        static Texture2D MakeRampCrimson() => Design("RampCrimson", (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 2f);
            float x = u * 4f + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float cu = x - col, cv = v * 2f - row;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu) * 2f, Mathf.Min(cv, 1f - cv) * 4f); // metres to the slab edge
            float m = Mottle(u, v, 101);
            if (e < 0.05f) return Rgb(0.03f, 0.02f, 0.025f);
            var red = Rgb(0.85f, 0.1f, 0.13f) * (0.85f + 0.3f * m);
            if (Mathf.Abs(e - 0.34f) < 0.035f) return Solid(red);
            float dia = Mathf.Abs(cu - 0.5f) * 2f + Mathf.Abs(cv - 0.5f) * 4f;
            if (dia > 0.5f && dia < 0.58f) return Solid(red);
            float shade = 0.8f + 0.35f * m + 0.2f * (Hash(Wrap(col, 4), row, 7) - 0.5f);
            if (e < 0.14f) shade += cu < 0.5f || cv > 0.5f ? 0.3f : -0.25f;
            if (dia < 0.5f) shade *= 0.65f;
            return Solid(Rgb(0.19f, 0.15f, 0.16f) * shade);
        });

        // Forge: riveted steel tread plate, 2m panels of raised lugs, with rust creeping in
        static Texture2D MakeRampForge() => Design("RampForge", (u, v) =>
        {
            float x = u * 4f, y = v * 4f;
            float cu = x - Mathf.Floor(x), cv = y - Mathf.Floor(y);
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) * 2f;
            if (e < 0.03f) return Rgb(0.04f, 0.035f, 0.03f);
            foreach (float ru in new[] { 0.05f, 0.95f })
                foreach (float rv in new[] { 0.05f, 0.95f })
                {
                    float d = new Vector2(cu - ru, cv - rv).magnitude * 2f;
                    if (d < 0.05f) return Rgb(0.8f, 0.78f, 0.75f) * (1f - d * 6f);
                }
            float lx = cu * 8f, ly = cv * 8f;
            int ix = Mathf.FloorToInt(lx), iy = Mathf.FloorToInt(ly);
            float fx = lx - ix - 0.5f, fy = ly - iy - 0.5f;
            float s = ((ix + iy) & 1) == 0 ? 1f : -1f;
            float ax = (fx + s * fy) * 0.7071f, ay = (fy - s * fx) * 0.7071f;
            float lug = ax * ax / 0.14f + ay * ay / 0.008f;
            float m = Mottle(u, v, 113);
            float shade = 0.72f + 0.25f * m + (e < 0.08f ? 0.18f : 0f);
            if (lug < 1f) shade += ay > 0f ? 0.35f : 0.12f;
            else if (lug < 1.4f) shade -= 0.2f;
            float rust = Mathf.Clamp01((Noise(u * 6f, v * 6f, 6, 117) * 0.7f + Noise(u * 24f, v * 24f, 24, 119) * 0.3f - 0.55f) * 4f);
            return Solid(Color.Lerp(Rgb(0.42f, 0.41f, 0.43f), Rgb(0.46f, 0.22f, 0.09f), rust * 0.8f) * shade);
        });

        // Sunset: hexagon tiles in cream, salmon and peach with plum grout
        static Texture2D MakeRampSunset() => Design("RampSunset", (u, v) =>
        {
            var (f1, f2, id) = HexCells(u * 7f, v * 4f * S3, 7, 4);
            if (f2 - f1 < 0.07f) return Rgb(0.28f, 0.15f, 0.2f);
            float h = Hash(id, 3, 131);
            Color c = h < 0.45f ? Rgb(0.96f, 0.85f, 0.75f) : h < 0.8f ? Rgb(0.93f, 0.56f, 0.48f) : h < 0.94f ? Rgb(0.98f, 0.7f, 0.52f) : Rgb(0.72f, 0.35f, 0.3f);
            float shade = 1.02f - 0.2f * f1 + (Mottle(u, v, 137) - 0.5f) * 0.1f + (f2 - f1 < 0.12f ? 0.08f : 0f);
            return Solid(c * shade);
        });

        // Gallery: white marble slabs with soft grey veins and a thin blue line down the ramp
        static Texture2D MakeRampGallery() => Design("RampGallery", (u, v) =>
        {
            float cu = u * 2f % 1f, cv = v * 2f % 1f;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) * 4f;
            if (e < 0.025f) return Rgb(0.55f, 0.56f, 0.6f);
            if (Mathf.Abs(v - 0.5f) < 0.006f) return Rgb(0.35f, 0.6f, 1f);
            float n = Noise(u * 4f, v * 4f, 4, 141) * 0.55f + Noise(u * 8f, v * 8f, 8, 143) * 0.3f + Noise(u * 32f, v * 32f, 32, 145) * 0.15f;
            float vein = Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (2f * u + v) + n * 9f));
            float vein2 = Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (u - 3f * v) + n * 12f));
            float shade = 0.93f + (Mottle(u, v, 147) - 0.5f) * 0.06f;
            shade -= Mathf.Pow(1f - vein, 18f) * 0.28f + Mathf.Pow(1f - vein2, 30f) * 0.14f;
            return Rgb(shade * 0.99f, shade * 0.99f, shade * 1.01f);
        });

        // Sky Palace: glossy navy panels, white seams, gold studs where the big lines cross
        static Texture2D MakeRampPalace() => Design("RampPalace", (u, v) =>
        {
            float x = u * 8f, y = v * 8f;
            float big = Mathf.Min(ToLine(u * 2f), ToLine(v * 2f)) * 4f;
            float bu = ToLine(u * 2f) * 4f, bv = ToLine(v * 2f) * 4f;
            if (bu + bv < 0.3f) return Solid(Rgb(1f, 0.78f, 0.35f) * (1.1f - (bu + bv)));
            if (big < 0.05f) return Rgb(0.85f, 0.88f, 0.96f);
            if (Mathf.Min(ToLine(x), ToLine(y)) < 0.02f) return Rgb(0.4f, 0.44f, 0.58f);
            float panel = Hash(Wrap(Mathf.FloorToInt(x), 8), Wrap(Mathf.FloorToInt(y), 8), 151);
            float shade = 0.85f + 0.3f * panel + 0.25f * Mottle(u, v, 153);
            return Solid(Rgb(0.07f, 0.09f, 0.16f) * shade);
        });

        // Candy: candy-cane stripes, pink, cream and mint, gently rounded
        static Texture2D MakeRampCandy() => Design("RampCandy", (u, v) =>
        {
            float s = (u + v) * 9f;
            int k = Wrap(Mathf.FloorToInt(s), 3);
            float f = s - Mathf.Floor(s);
            Color c = k == 0 ? Rgb(1f, 0.5f, 0.78f) : k == 1 ? Rgb(1f, 0.96f, 0.94f) : Rgb(0.55f, 0.95f, 0.88f);
            float shade = 0.86f + 0.16f * Mathf.Sin(f * Mathf.PI) + (Mottle(u, v, 161) - 0.5f) * 0.05f;
            if (f < 0.035f || f > 0.965f) shade *= 0.8f;
            if (Mathf.Abs(f - 0.3f) < 0.03f) shade += 0.08f; // glossy highlight
            return Solid(c * shade);
        });

        // Neon Rings: black-violet panels with a magenta grid and a cyan line every 4m
        static Texture2D MakeRampNeon() => Design("RampNeon", (u, v) =>
        {
            float x = u * 8f, y = v * 8f;
            if (ToLine(v * 2f) * 4f < 0.07f) return Rgb(0.3f, 0.95f, 1f);
            if (ToLine(u * 2f) * 4f < 0.07f) return Rgb(0.12f, 0.35f, 0.4f); // across the ramp: faint, so it doesn't flicker
            if (ToLine(y) < 0.03f) return Solid(Rgb(0.9f, 0.25f, 0.95f) * 0.8f);
            if (ToLine(x) < 0.03f) return Rgb(0.2f, 0.08f, 0.25f);
            float cx = x - Mathf.Floor(x) - 0.5f, cy = y - Mathf.Floor(y) - 0.5f;
            float panel = Hash(Wrap(Mathf.FloorToInt(x), 8), Wrap(Mathf.FloorToInt(y), 8), 171);
            float shade = 0.8f + 0.4f * panel + 0.25f * Mottle(u, v, 173);
            if (Mathf.Max(Mathf.Abs(cx), Mathf.Abs(cy)) > 0.42f) shade *= 0.6f;
            if (panel > 0.85f && Mathf.Abs(cx) < 0.25f && ToLine(cy * 6f) < 0.12f && Mathf.Abs(cy) < 0.25f) return Rgb(0.5f, 0.2f, 0.6f);
            return Solid(Rgb(0.08f, 0.05f, 0.12f) * shade);
        });

        // Wireframe: black with a faint red grid, brighter every 4m
        static Texture2D MakeRampWire() => Design("RampWire", (u, v) =>
        {
            float x = u * 8f, y = v * 8f;
            if (ToLine(v * 2f) * 4f < 0.05f) return Rgb(0.9f, 0.12f, 0.1f);
            if (ToLine(u * 2f) * 4f < 0.05f) return Rgb(0.3f, 0.04f, 0.04f);
            if (ToLine(y) < 0.02f) return Rgb(0.45f, 0.06f, 0.06f);
            if (ToLine(x) < 0.02f) return Rgb(0.12f, 0.03f, 0.03f);
            return Solid(Rgb(0.035f, 0.033f, 0.04f) * (0.8f + 0.4f * Mottle(u, v, 181)));
        });

        // Grotto: dark blue stone split by glowing teal crystal veins, flecked with violet
        static Texture2D MakeRampGrotto() => Design("RampGrotto", (u, v) =>
        {
            var (f1, f2, id) = Voronoi(u * 6f, v * 6f, 6, 191);
            float edge = f2 - f1;
            float m = Mottle(u, v, 193);
            var rock = Rgb(0.2f, 0.22f, 0.32f) * (0.55f + 0.7f * m) * (0.85f + 0.3f * Hash(id, 1, 195));
            if (edge < 0.06f) return Solid(Color.Lerp(Rgb(0.4f, 1f, 0.88f), rock, edge / 0.06f));
            if (Noise(u * 96f, v * 96f, 96, 197) > 0.86f) return Rgb(0.75f, 0.55f, 1f);
            return Solid(rock);
        });

        // Library: oak parquet laid in a basket weave, each 1m block four planks turning
        static Texture2D MakeRampLibrary() => Design("RampLibrary", (u, v) =>
        {
            float x = u * 8f, y = v * 8f;
            int bi = Mathf.FloorToInt(x), bj = Mathf.FloorToInt(y);
            float fx = x - bi, fy = y - bj;
            bool along = ((bi + bj) & 1) == 0;
            float across = along ? fy : fx, length = along ? fx : fy;
            int plank = Mathf.Min(3, Mathf.FloorToInt(across * 4f));
            float pf = across * 4f - plank;
            if (pf < 0.05f || length < 0.012f || length > 0.988f) return Rgb(0.13f, 0.08f, 0.05f);
            float h = Hash(Wrap(bi, 8) * 4 + plank, Wrap(bj, 8), 201);
            Color wood = Color.Lerp(Rgb(0.46f, 0.28f, 0.14f), Rgb(0.66f, 0.45f, 0.25f), h);
            float grain = Noise(length * 3f + h * 20f, pf * 22f + plank * 30f, 256, 203);
            float ring = Mathf.Abs(Mathf.Sin((pf * 6f + grain * 3f + h * 5f) * Mathf.PI));
            float shade = 0.85f + 0.2f * grain - 0.12f * Mathf.Pow(1f - ring, 6f) + 0.06f * Mathf.Sin(pf * Mathf.PI);
            return Solid(wood * shade);
        });

        // ------------------------------------------------------------------ walls

        // Gothic blind arches over ashlar: two tall pointed arches every 8m, each with a mullion
        // and a round window at the top. Grey, for tinting.
        static float TraceryShade(float u, float v)
        {
            float ax = ((u * 2f) % 1f - 0.5f) * 4f, ay = v * 8f;
            const float w = 1.3f;
            if (InArch(ax, ay, w, 0.6f, 5f))
            {
                float ring = new Vector2(ax, ay - 6.3f).magnitude;
                if (Mathf.Abs(ring - 0.5f) < 0.08f || Mathf.Abs(ax) < 0.09f && ay < 5.7f) return 0.85f;
                if (ring < 0.42f) return 0.2f;
                return 0.38f + 0.08f * Mottle(u, v, 211);
            }
            if (InArch(ax, ay, w + 0.28f, 0.35f, 5f)) return InArch(ax - 0.1f, ay + 0.1f, w + 0.28f, 0.35f, 5f) ? 0.95f : 0.7f;
            // Ashlar: 1m x 0.5m staggered blocks
            int row = Mathf.FloorToInt(v * 16f);
            float bx = u * 8f + (row % 2) * 0.5f, cu = bx - Mathf.Floor(bx), cv = v * 16f - row;
            if (Mathf.Min(cu, 1f - cu) < 0.025f || Mathf.Min(cv, 1f - cv) < 0.05f) return 0.35f;
            return 0.68f + 0.12f * Hash(Wrap(Mathf.FloorToInt(bx), 8), row, 213) + 0.12f * (Mottle(u, v, 215) - 0.5f);
        }

        static Texture2D MakeWallTracery() => Design("WallTracery", (u, v) => Grey(TraceryShade(u, v)));

        // Forge: heavy grates, 4m panels of louvres in a bolted frame. Grey, for tinting.
        static Texture2D MakeWallGrate() => Design("WallGrate", (u, v) =>
        {
            float cu = u * 2f % 1f, cv = v * 2f % 1f;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) * 4f;
            float m = Mottle(u, v, 221);
            if (e < 0.04f) return Grey(0.1f);
            if (e < 0.3f)
            {
                foreach (float ru in new[] { 0.04f, 0.96f })
                    foreach (float rv in new[] { 0.04f, 0.96f })
                        if (new Vector2(cu - ru, cv - rv).magnitude * 4f < 0.07f) return Grey(0.95f);
                return Grey(0.62f + 0.15f * m + (e < 0.08f ? 0.15f : 0f));
            }
            float l = cv * 16f % 1f;
            return Grey((l < 0.18f ? 0.08f : 0.3f + 0.5f * l) + 0.1f * (m - 0.5f));
        });

        // Gallery: raised plaster panels, 4m square, with a moulded frame. Grey, for tinting.
        static Texture2D MakeWallPanels() => Design("WallPanels", (u, v) =>
        {
            float cu = u * 2f % 1f, cv = v * 2f % 1f;
            float e = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) * 4f;
            float shade = 0.9f + 0.05f * (Mottle(u, v, 231) - 0.5f);
            if (e < 0.03f) return Grey(0.62f);
            if (e > 0.3f && e < 0.36f) return Grey(cu < cv ? 1f : 0.72f);
            if (e > 0.36f && e < 0.42f) return Grey(cu < cv ? 0.7f : 0.98f);
            if (e > 0.42f) shade -= 0.04f;
            return Grey(shade);
        });

        // Candy: a city of bevelled blocks, big and small. Grey, for tinting.
        static Texture2D MakeWallBlocks() => Design("WallBlocks", (u, v) =>
        {
            float x = u * 4f, y = v * 4f;
            int ci = Mathf.FloorToInt(x), cj = Mathf.FloorToInt(y);
            float fx = x - ci, fy = y - cj;
            float size = 2f;
            int id = ci * 8 + cj;
            if (Hash(ci, cj, 241) < 0.5f)
            {
                int si = fx < 0.5f ? 0 : 1, sj = fy < 0.5f ? 0 : 1;
                fx = fx * 2f % 1f; fy = fy * 2f % 1f; size = 1f; id = id * 4 + si * 2 + sj;
            }
            float ex = Mathf.Min(fx, 1f - fx) * size, ey = Mathf.Min(fy, 1f - fy) * size;
            float shade = 0.78f + 0.2f * Hash(id, 5, 243);
            if (Mathf.Min(ex, ey) < 0.03f) return Grey(0.3f);
            if (Mathf.Min(ex, ey) < 0.14f)
                shade += (ex < ey ? fx < 0.5f : fy > 0.5f) ? 0.18f : -0.2f;
            return Grey(shade + 0.04f * (Mottle(u, v, 245) - 0.5f));
        });

        // Neon Rings: dark hex vents in a panel grid. Grey, for tinting.
        static Texture2D MakeWallHexVents() => Design("WallHexVents", (u, v) =>
        {
            if (Mathf.Min(ToLine(u * 2f), ToLine(v * 2f)) * 4f < 0.06f) return Grey(0.15f);
            var (f1, f2, _) = HexCells(u * 14f, v * 8f * S3, 14, 8);
            float edge = f2 - f1;
            if (edge < 0.12f) return Grey(0.85f + 0.1f * Mottle(u, v, 251));
            if (edge < 0.2f) return Grey(0.5f);
            return Grey(0.18f + 0.3f * f1);
        });

        // Library: walnut wall panelling, raised panels 2m wide in carved frames (own colors)
        static Texture2D MakeWallWood() => Design("WallWood", (u, v) =>
        {
            float cu = u * 4f % 1f, cv = v * 2f % 1f;
            int col = Mathf.FloorToInt(u * 4f), row = Mathf.FloorToInt(v * 2f);
            float ex = Mathf.Min(cu, 1f - cu) * 2f, ey = Mathf.Min(cv, 1f - cv) * 4f;
            float e = Mathf.Min(ex, ey);
            float h = Hash(col, row, 261);
            Color wood = Color.Lerp(Rgb(0.3f, 0.17f, 0.09f), Rgb(0.42f, 0.25f, 0.13f), h);
            float vertical = e == ex ? 1f : 0f; // grain runs up the stiles, across the rails
            float grain = vertical > 0f ? Noise(u * 80f, v * 6f, 80, 263) : Noise(u * 6f, v * 80f, 80, 265);
            if (e < 0.02f) return Rgb(0.08f, 0.05f, 0.03f);
            float shade = 0.85f + 0.25f * grain;
            if (e < 0.3f) return Solid(wood * (shade + 0.1f));
            if (e < 0.34f) return Solid(wood * (cu < cv ? 0.6f : 1.3f));
            if (e < 0.5f) return Solid(wood * (shade * (cu < cv ? 1.2f : 0.8f)));
            return Solid(wood * shade * 0.95f);
        });

        // ------------------------------------------------------------------ start hall

        // Celestial terrace floor: white marble slabs, 2m square, with soft grey veins and a gold
        // diamond lattice inlaid through them (own colors)
        static Texture2D MakeSanctumFloor() => Design("SanctumFloor", (u, v) =>
        {
            float x = u * 4f, y = v * 4f;
            float dia = Mathf.Min(ToLine((x + y) * 0.5f), ToLine((x - y) * 0.5f)) * 2.83f; // metres to the lattice
            if (dia < 0.03f) return Rgb(0.98f, 0.76f, 0.36f);
            if (dia < 0.045f) return Rgb(0.78f, 0.6f, 0.3f);
            float seam = Mathf.Min(ToLine(x), ToLine(y)) * 2f;
            if (seam < 0.01f) return Rgb(0.72f, 0.7f, 0.68f);
            float n = Noise(u * 4f, v * 4f, 4, 271) * 0.6f + Noise(u * 16f, v * 16f, 16, 273) * 0.4f;
            float vein = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (u + 2f * v) + n * 10f)), 24f);
            float shade = 0.95f + 0.05f * Mottle(u, v, 275) - vein * 0.22f;
            return Rgb(shade * 0.98f, shade * 0.97f, shade * 0.95f);
        });

        // Where that floor glows: the 2m tile seams, a soft warm light (emission mask)
        static Texture2D MakeSanctumFloorGlow() => Design("SanctumFloorGlow", (u, v) =>
        {
            float seam = Mathf.Min(ToLine(u * 4f), ToLine(v * 4f)) * 2f;
            return Grey(seam < 0.01f ? 1f : seam < 0.025f ? 0.2f : 0f);
        });

        // Walls: white stone carved with tall arches framed in gold (own colors)
        static Texture2D MakeSanctumWall() => Design("SanctumWall", (u, v) =>
        {
            float t = TraceryShade(u, v);
            float ax = ((u * 2f) % 1f - 0.5f) * 4f, ay = v * 8f;
            bool frame = !InArch(ax, ay, 1.3f, 0.6f, 5f) && InArch(ax, ay, 1.58f, 0.35f, 5f);
            if (frame || t > 0.84f) return Rgb(1f, 0.8f, 0.42f) * (0.75f + 0.25f * t);
            // pale stone, the recesses a cool sky blue
            return Solid(InArch(ax, ay, 1.3f, 0.6f, 5f) ? Rgb(0.72f, 0.8f, 0.92f) * (0.8f + 0.3f * t) : Rgb(0.95f, 0.94f, 0.9f) * (0.75f + 0.3f * t));
        });
    }
}
