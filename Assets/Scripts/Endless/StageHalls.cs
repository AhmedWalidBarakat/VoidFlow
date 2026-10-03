using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow
{
    // The enclosed zones are built the way surf maps are: each stage is one big hall, a single
    // room round all of its ramps and flights with a flat floor far below, a ceiling well above
    // the highest launch and straight walls well out to the sides, and the ramps float inside
    // it. Neighbouring halls run into each other as one space (the panels of a hall that stand
    // inside the next one are left out), and wherever a ramp or a flight crosses a panel, the
    // panel gives way. Walls, floors and ceilings are cut into panels so those openings stay
    // local.
    public static class StageHalls
    {
        // The halls' architecture, each zone in its own (after the looks of the maps):
        //  - Stripes: utopia's halls. Bevelled lower walls, long bands of the zone's colours
        //    running round the room, bright window strips, skylights in the roof
        //  - Ribs: cathedrals and temples. Pilasters up every other bay, a dark band low down,
        //    heavy beams under the roof
        //  - Panels: labs and tech. Recessed wall panels outlined in glowing seams, a grid of
        //    light panels in the ceiling
        //  - Mixed: bands, pilasters and windows together, for the rest
        // ...and each real map in a look of its own (after the map's run):
        //  - Cave: rough rock walls bulging in and out, crystals, stalactites (mesa, essentia's
        //    rock tube, corruption)
        //  - NeonGrid: a dark void ruled in a sparse grid of light (strike bonus, spin, tensor,
        //    the finale)
        //  - Spikes: dark walls studded with spikes (blackheart, frags nightmare)
        //  - Patchwork: walls of mismatched panels (trofle, sinsane's stitched maps)
        //  - Industrial: pipes, catwalks, hazard stripes (before, techslop, gigapede, helljumper)
        //  - Sanctum: tall pointed windows of light, gold trim, a coffered roof (deity)
        //  - Tomb: massive gold-banded columns, a carved band, torches (anubis)
        //  - DevGrid: the old grey-and-orange measuring grid of the first maps (original)
        //  - Zen: paper screens in wooden lattices (not so zen)
        //  - Funhouse: checkered bands and blocks of colour (not so funhouse)
        //  - Shade: dark walls, a few shafts of light (shade)
        //  - Facets: walls folded into shallow facets (exonic)
        //  - Gallery: framed paintings between pilasters (raphaello)
        //  - Ruins: broken columns and a worn band (essentia)
        //  - Backrooms: plain walls, a baseboard, rows of fluorescent ceiling lights
        public enum Design { Stripes, Ribs, Panels, Mixed, Cave, NeonGrid, Spikes, Patchwork, Industrial, Sanctum, Tomb, DevGrid, Zen, Funhouse, Shade, Facets, Gallery, Ruins, Backrooms }

        // A real map's own look, by its zone's name; the rest by their style
        public static Design DesignFor(Biome biome, int stage)
        {
            string n = biome.name;
            if (n.Contains("UTOPIA") || n.Contains("POOLROOMS")) return Design.Stripes;
            if (n.Contains("MESA") || n.Contains("ROCK TUBE") || n.Contains("CORRUPTION")) return Design.Cave;
            if (n.Contains("STRIKE") || n.StartsWith("SPIN") || n.Contains("TENSOR") || n.Contains("FINALE") || n.Contains("NEON STACKS")) return Design.NeonGrid;
            if (n.Contains("BLACKHEART") || n.Contains("NIGHTMARE")) return Design.Spikes;
            if (n.Contains("TROFLE") || n.Contains("PATCHWORK")) return Design.Patchwork;
            if (n.StartsWith("BEFORE") || n.Contains("TECHSLOP") || n.Contains("GIGAPEDE") || n.Contains("HELLJUMPER") || n.Contains("GREEN SHAFT")) return Design.Industrial;
            if (n.Contains("DEITY")) return Design.Sanctum;
            if (n.Contains("ANUBIS")) return Design.Tomb;
            if (n.Contains("ORIGINAL")) return Design.DevGrid;
            if (n.Contains("ZEN")) return Design.Zen;
            if (n.Contains("FUNHOUSE")) return Design.Funhouse;
            if (n.Contains("SHADE") || n.Contains("DARK POOL")) return Design.Shade;
            if (n.Contains("EXONIC")) return Design.Facets;
            if (n.Contains("RAPHAELLO") || n.Contains("FLOATING HOUSES")) return Design.Gallery;
            if (n.Contains("ESSENTIA")) return Design.Ruins;
            if (n.Contains("BACKROOMS")) return Design.Backrooms;
            if (n.Contains("STARLIT")) return Design.Ribs;
            return DesignFor(biome.style, stage);
        }

        public static Design DesignFor(SceneryStyle style, int stage) => style switch
        {
            SceneryStyle.Gallery or SceneryStyle.Sunset or SceneryStyle.Candy or SceneryStyle.Toy or SceneryStyle.Palace => Design.Stripes,
            SceneryStyle.Cathedral or SceneryStyle.Temple or SceneryStyle.Library or SceneryStyle.Mine or SceneryStyle.Grotto => Design.Ribs,
            SceneryStyle.Lab or SceneryStyle.Synth or SceneryStyle.Wire or SceneryStyle.Forge or SceneryStyle.Rings => Design.Panels,
            _ => (Design)((stage * 7 + 3) % 4),
        };
        public class Volume
        {
            public int stage;
            public List<Vector2> outline;   // convex, counter-clockwise, course-local x/z
            public float floor, ceiling;
            public bool openTop;
            public BiomeKit kit;
            public List<Vector3> course;    // a sample of the stage's riding lines and flights (set dressing keeps clear of it)
            public Design design;           // how its walls and roof are shaped and lit

            public bool Contains(Vector3 p, float inset)
            {
                if (p.y <= floor + inset || p.y >= ceiling - inset) return false;
                var q = new Vector2(p.x, p.z);
                for (int i = 0; i < outline.Count; i++)
                {
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                    Vector2 e = b - a, n = new Vector2(e.y, -e.x).normalized; // outward for counter-clockwise
                    if (Vector2.Dot(q - a, n) > -inset) return false;
                }
                return true;
            }
        }

        public const float Margin = 95f, Headroom = 95f, Panel = 30f; // room for every line and speed, not just the designed one

        // A stage's hall round its course points (riding lines and flights)
        public static Volume Plan(int stage, List<Vector3> points, float depth, bool openTop, BiomeKit kit)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            var ring = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y);
                if (i % 3 != 0) continue;
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f;
                    ring.Add(new Vector2(p.x + Mathf.Cos(a) * Margin, p.z + Mathf.Sin(a) * Margin));
                }
            }
            var course = new List<Vector3>();
            for (int i = 0; i < points.Count; i += 4) course.Add(points[i]);
            return new Volume { stage = stage, outline = Hull(ring), floor = lo - depth, ceiling = hi + Headroom, openTop = openTop, kit = kit, course = course };
        }

        // Builds a hall's panels, leaving out any that stand inside a neighbouring hall
        public static GameObject Build(Volume v, List<Volume> neighbours, Transform parent, List<Mesh> meshes, Mesh cube)
        {
            var kit = v.kit;
            var faces = new Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<int> t)>();
            bool Outside(Vector3 c)
            {
                foreach (var o in neighbours) if (o.Contains(c, 0.5f)) return false;
                return true;
            }
            void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                if (!m || !Outside((a + b + c + d) * 0.25f)) return;
                if (!faces.TryGetValue(m, out var f)) faces[m] = f = (new List<Vector3>(), new List<Vector3>(), new List<int>());
                Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
                foreach (float s in new[] { 1f, -1f })
                {
                    int i = f.v.Count;
                    f.v.AddRange(new[] { a, b, c, d });
                    for (int k = 0; k < 4; k++) f.n.Add(normal * s);
                    if (s > 0f) f.t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
                    else f.t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                }
            }
            // A thin glowing strip from p to q, facing into the hall
            void Strip(Material m, Vector3 p, Vector3 q, Vector3 inward, float width)
            {
                Vector3 along = (q - p).normalized, up = Vector3.Cross(inward, along).normalized * (width * 0.5f);
                Vector3 off = inward * 0.6f; // (clear of the wall: no flicker, even far off)
                Quad(m, p - up + off, q - up + off, q + up + off, p + up + off);
            }

            Material wall = kit.scenery ? kit.scenery : kit.slab, floor = kit.floor ? kit.floor : kit.slab, ceiling = kit.slab ? kit.slab : wall;
            var o = v.outline;
            Vector2 center = Vector2.zero;
            foreach (var p in o) center += p;
            center /= o.Count;

            // Walls, panel by panel, in the zone's design (see Design), with glowing seams where
            // they meet the floor and the roof
            Material trimA = kit.accent ? kit.accent : (kit.slab ? kit.slab : wall);
            Material lightA = kit.glowAlt ? kit.glowAlt : kit.glow;
            var design = v.design;
            // Tech zones' wall textures are drawn in fine lines, which ripple across walls this
            // big: their halls are built in the zone's smoother slab, the colour and light coming
            // from the recessed panels and their outlines
            if ((design is Design.Panels or Design.NeonGrid or Design.Shade or Design.Industrial or Design.DevGrid) && kit.slab) wall = kit.slab;
            var dr = new System.Random(v.stage * 131 + 7); // the look's own dice
            // rock: every corner of the wall pushed in by its own amount (the same for the panels
            // that share it, so the rock stays whole)
            float Bulge(Vector3 q)
            {
                float h = Mathf.Sin(q.x * 0.0731f + q.z * 0.0517f) * 43758.5453f + Mathf.Sin(q.y * 0.0913f + q.x * 0.0291f) * 12345.678f;
                return (h - Mathf.Floor(h)) * 7f;
            }
            int panelIndex = 0;
            float height = v.ceiling - v.floor;
            for (int i = 0; i < o.Count; i++)
            {
                Vector2 a2 = o[i], b2 = o[(i + 1) % o.Count];
                float len = Vector2.Distance(a2, b2);
                int cols = Mathf.Max(1, Mathf.CeilToInt(len / Panel)), rows = Mathf.Max(1, Mathf.CeilToInt(height / Panel));
                Vector2 e = (b2 - a2) / len;
                Vector3 inward = new Vector3(-e.y, 0f, e.x); // left of a counter-clockwise edge points in
                for (int c = 0; c < cols; c++, panelIndex++)
                {
                    Vector2 p0 = a2 + (b2 - a2) * (c / (float)cols), p1 = a2 + (b2 - a2) * ((c + 1) / (float)cols);
                    Vector3 f0 = new Vector3(p0.x, 0f, p0.y), f1 = new Vector3(p1.x, 0f, p1.y);
                    Vector3 along = (f1 - f0).normalized;
                    float pw = Vector3.Distance(f0, f1);
                    for (int r = 0; r < rows; r++)
                    {
                        float y0 = Mathf.Lerp(v.floor, v.ceiling, r / (float)rows), y1 = Mathf.Lerp(v.floor, v.ceiling, (r + 1) / (float)rows);
                        Vector3 w00 = f0.WithY(y0), w10 = f1.WithY(y0), w11 = f1.WithY(y1), w01 = f0.WithY(y1);
                        if (design == Design.Cave)
                            Quad(wall, w00 + inward * Bulge(w00), w10 + inward * Bulge(w10), w11 + inward * Bulge(w11), w01 + inward * Bulge(w01));
                        else
                            Quad(wall, w00, w10, w11, w01);
                        Vector3 mid = (w00 + w10 + w11 + w01) * 0.25f;
                        switch (design)
                        {
                            case Design.Spikes when (panelIndex + r) % 2 == 0:
                            case Design.Facets:
                            {
                                // a spike (or a shallow facet) rising from the panel
                                float depth = design == Design.Spikes ? 9f : 3.5f;
                                float k = design == Design.Spikes ? 0.2f : 0f;
                                Vector3 a = Vector3.Lerp(w00, mid, k), b = Vector3.Lerp(w10, mid, k), cc = Vector3.Lerp(w11, mid, k), d = Vector3.Lerp(w01, mid, k);
                                Vector3 apex = mid + inward * depth + inward * 0.3f;
                                Material m = design == Design.Spikes ? trimA : wall;
                                Quad(m, a + inward * 0.3f, b + inward * 0.3f, apex, apex);
                                Quad(m, b + inward * 0.3f, cc + inward * 0.3f, apex, apex);
                                Quad(m, cc + inward * 0.3f, d + inward * 0.3f, apex, apex);
                                Quad(m, d + inward * 0.3f, a + inward * 0.3f, apex, apex);
                                if (design == Design.Spikes) Strip(lightA, a, b, inward, 0.8f);
                                break;
                            }
                            case Design.Patchwork:
                            {
                                // every panel its own patch
                                int pick = dr.Next(5);
                                Material m = pick switch { 0 => trimA, 1 => kit.slab ? kit.slab : wall, 2 => wall, 3 => trimA, _ => lightA };
                                Vector3 inset = (w10 - w00) * 0.04f, up = (w01 - w00) * 0.04f, off = inward * 0.5f;
                                Quad(m, w00 + inset + up + off, w10 - inset + up + off, w11 - inset - up + off, w01 + inset - up + off);
                                break;
                            }
                            case Design.Zen:
                            {
                                // a paper screen in a wooden lattice
                                Vector3 inset = (w10 - w00) * 0.08f, up = (w01 - w00) * 0.08f;
                                Quad(lightA, w00 + inset + up + inward * 0.5f, w10 - inset + up + inward * 0.5f, w11 - inset - up + inward * 0.5f, w01 + inset - up + inward * 0.5f);
                                for (int k = 1; k <= 3; k++)
                                {
                                    float f = k / 4f;
                                    Strip(trimA, Vector3.Lerp(w00, w10, f).WithY(y0 + 1f), Vector3.Lerp(w00, w10, f).WithY(y1 - 1f), inward * 1.6f, 0.9f);
                                    Strip(trimA, w00.WithY(Mathf.Lerp(y0, y1, f)) + along * 1f, w10.WithY(Mathf.Lerp(y0, y1, f)) - along * 1f, inward * 1.6f, 0.9f);
                                }
                                break;
                            }
                            case Design.Sanctum when panelIndex % 2 == 0 && r >= 1 && r < rows - 1:
                            {
                                // a tall window of light with a pointed head
                                Vector3 m0 = f0 + along * (pw * 0.3f), m1 = f1 - along * (pw * 0.3f), off = inward * 0.8f;
                                float ya = y0 + 2f, yb = y1 - 6f;
                                Quad(lightA, m0.WithY(ya) + off, m1.WithY(ya) + off, m1.WithY(yb) + off, m0.WithY(yb) + off);
                                Vector3 peak = ((m0 + m1) * 0.5f).WithY(y1 - 1f) + off;
                                Quad(lightA, m0.WithY(yb) + off, m1.WithY(yb) + off, peak, peak);
                                Strip(trimA, m0.WithY(ya), m0.WithY(yb), inward * 1.2f, 1.2f);
                                Strip(trimA, m1.WithY(ya), m1.WithY(yb), inward * 1.2f, 1.2f);
                                break;
                            }
                        }
                        if (design == Design.Panels && (panelIndex + r) % 2 == 0) // (every other panel, checkered: thin lines packed close shimmer far off)
                        {
                            // a recessed panel outlined in light
                            Vector3 m0 = f0 + along * (pw * 0.12f), m1 = f1 - along * (pw * 0.12f);
                            float ya = Mathf.Lerp(y0, y1, 0.12f), yb = Mathf.Lerp(y0, y1, 0.88f);
                            Vector3 off = inward * 0.35f; // (behind its outline of light, clear of the wall)
                            Quad(trimA, m0.WithY(ya) + off, m1.WithY(ya) + off, m1.WithY(yb) + off, m0.WithY(yb) + off);
                            Strip(kit.glow, m0.WithY(ya), m1.WithY(ya), inward, 1.4f);
                            Strip(kit.glow, m0.WithY(yb), m1.WithY(yb), inward, 1.4f);
                            Strip(kit.glow, m0.WithY(ya), m0.WithY(yb), inward, 1.4f);
                            Strip(kit.glow, m1.WithY(ya), m1.WithY(yb), inward, 1.4f);
                        }
                        // windows: tall bright strips in a band round the middle of the room
                        bool window = (design == Design.Stripes && panelIndex % 3 == 1 && r == rows / 2)
                                   || (design == Design.Mixed && panelIndex % 5 == 2 && r == rows / 2);
                        if (window)
                        {
                            Vector3 m0 = f0 + along * (pw * 0.22f), m1 = f1 - along * (pw * 0.22f);
                            float ya = Mathf.Lerp(y0, y1, 0.1f), yb = Mathf.Lerp(y0, y1, 0.92f);
                            Vector3 off = inward * 0.8f;
                            Quad(lightA, m0.WithY(ya) + off, m1.WithY(ya) + off, m1.WithY(yb) + off, m0.WithY(yb) + off);
                            Strip(trimA, m0.WithY(ya - 0.6f), m1.WithY(ya - 0.6f), inward, 1.2f);
                            Strip(trimA, m0.WithY(yb + 0.6f), m1.WithY(yb + 0.6f), inward, 1.2f);
                        }
                    }
                    // seams where the walls meet the floor and the roof
                    Strip(kit.glow, f0.WithY(v.floor + 1.5f), f1.WithY(v.floor + 1.5f), inward, 1.2f);
                    if (!v.openTop) Strip(kit.glow, f0.WithY(v.ceiling - 1.5f), f1.WithY(v.ceiling - 1.5f), inward, 1.2f);
                    switch (design)
                    {
                        case Design.Stripes:
                        {
                            // a bevel along the foot of the wall, and bands of colour above it and
                            // under the roof, like utopia's halls
                            const float BevelUp = 12f, BevelIn = 8f;
                            Quad(wall, f0.WithY(v.floor + BevelUp), f1.WithY(v.floor + BevelUp), (f1 + inward * BevelIn).WithY(v.floor), (f0 + inward * BevelIn).WithY(v.floor));
                            Strip(trimA, f0.WithY(v.floor + BevelUp + 4f), f1.WithY(v.floor + BevelUp + 4f), inward, 3f);
                            Strip(kit.glow, f0.WithY(v.floor + BevelUp + 7f), f1.WithY(v.floor + BevelUp + 7f), inward, 1.2f);
                            Strip(lightA, f0.WithY(v.floor + BevelUp + 9.5f), f1.WithY(v.floor + BevelUp + 9.5f), inward, 2f);
                            if (!v.openTop)
                            {
                                Strip(trimA, f0.WithY(v.ceiling - 12f), f1.WithY(v.ceiling - 12f), inward, 3f);
                                Strip(kit.glow, f0.WithY(v.ceiling - 9f), f1.WithY(v.ceiling - 9f), inward, 1.2f);
                            }
                            break;
                        }
                        case Design.Ribs:
                        {
                            // pilasters up every other bay, a dark band low down
                            Strip(trimA, f0.WithY(v.floor + 9f), f1.WithY(v.floor + 9f), inward, 4f);
                            if (panelIndex % 2 == 0)
                            {
                                Box(trimA, f0.WithY(v.floor + height * 0.5f) + inward * 1.85f, new Vector3(4f, height, 3.2f), Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg);
                                Strip(kit.glow, f0.WithY(v.floor + 3f) + inward * 1.7f - along * 2.1f, f0.WithY(v.floor + 3f) + inward * 1.7f + along * 2.1f, inward, 0.8f);
                            }
                            break;
                        }
                        case Design.Panels:
                        case Design.Spikes:
                        case Design.Patchwork:
                        case Design.Facets:
                            break;
                        case Design.Cave:
                        {
                            // crystals growing out of the rock here and there
                            if (dr.NextDouble() < 0.35)
                            {
                                Vector3 at = Vector3.Lerp(f0, f1, (float)dr.NextDouble()).WithY(v.floor + (float)dr.NextDouble() * height * 0.8f) + inward * 4f;
                                Box(lightA, at, new Vector3(2f + (float)dr.NextDouble() * 3f, 8f + (float)dr.NextDouble() * 10f, 2f + (float)dr.NextDouble() * 3f), (float)dr.NextDouble() * 90f);
                            }
                            break;
                        }
                        case Design.NeonGrid:
                        {
                            // a sparse grid of light ruled over the dark
                            if (panelIndex % 2 == 0)
                                Strip(kit.glow, f0.WithY(v.floor), f0.WithY(v.ceiling), inward, 1.5f);
                            for (int r = 2; r < rows; r += 2)
                                Strip(lightA, f0.WithY(v.floor + r * Panel), f1.WithY(v.floor + r * Panel), inward, 1.2f);
                            break;
                        }
                        case Design.Industrial:
                        {
                            // pipes along the wall, a catwalk, a hazard band at the foot
                            float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg + 90f;
                            Vector3 midc = (f0 + f1) * 0.5f;
                            Box(trimA, midc.WithY(v.floor + 9f) + inward * 1.5f, new Vector3(pw, 1.6f, 1.6f), yaw);
                            Box(trimA, midc.WithY(v.floor + 11.5f) + inward * 1.2f, new Vector3(pw, 1f, 1f), yaw);
                            Box(kit.slab ? kit.slab : wall, midc.WithY(v.floor + height * 0.5f) + inward * 2.5f, new Vector3(pw, 0.6f, 4f), yaw);
                            Strip(kit.glow, f0.WithY(v.floor + height * 0.5f + 1.2f) + inward * 4.3f, f1.WithY(v.floor + height * 0.5f + 1.2f) + inward * 4.3f, inward, 0.4f);
                            for (int k = 0; k < 6; k++)
                                Strip(k % 2 == 0 ? lightA : trimA, Vector3.Lerp(f0, f1, k / 6f).WithY(v.floor + 3.5f), Vector3.Lerp(f0, f1, (k + 1) / 6f).WithY(v.floor + 3.5f), inward, 2.5f);
                            break;
                        }
                        case Design.Sanctum:
                        {
                            // gold trim along the foot and under the roof
                            Strip(kit.glow, f0.WithY(v.floor + 6f), f1.WithY(v.floor + 6f), inward, 1.6f);
                            if (!v.openTop) Strip(kit.glow, f0.WithY(v.ceiling - 7f), f1.WithY(v.ceiling - 7f), inward, 1.6f);
                            break;
                        }
                        case Design.Tomb:
                        {
                            // massive columns banded in gold, a carved band between, torches
                            if (panelIndex % 2 == 0)
                            {
                                float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
                                Box(trimA, f0.WithY(v.floor + height * 0.5f) + inward * 3.4f, new Vector3(6.5f, height, 6.5f), yaw);
                                for (float y = v.floor + 8f; y < v.ceiling - 4f; y += 18f)
                                    Box(kit.glow, f0.WithY(y) + inward * 3.4f, new Vector3(7.2f, 1.2f, 7.2f), yaw);
                            }
                            else
                                Box(lightA, ((f0 + f1) * 0.5f).WithY(v.floor + 13f) + inward * 1.2f, new Vector3(1.2f, 2.4f, 1.2f), 0f);
                            for (int k = 0; k < 8; k++)
                                Strip(k % 2 == 0 ? trimA : wall, Vector3.Lerp(f0, f1, k / 8f).WithY(v.floor + 22f), Vector3.Lerp(f0, f1, (k + 1) / 8f).WithY(v.floor + 22f), inward, 3f);
                            break;
                        }
                        case Design.DevGrid:
                        {
                            // the measuring grid: a bold line every 16m both ways
                            Strip(trimA, f0.WithY(v.floor), f0.WithY(v.ceiling), inward, 1.2f);
                            Strip(trimA, ((f0 + f1) * 0.5f).WithY(v.floor), ((f0 + f1) * 0.5f).WithY(v.ceiling), inward, 0.8f);
                            for (float y = v.floor + 16f; y < v.ceiling; y += 16f)
                                Strip(trimA, f0.WithY(y), f1.WithY(y), inward, 1f);
                            break;
                        }
                        case Design.Zen:
                            break;
                        case Design.Funhouse:
                        {
                            // two checkered bands
                            foreach (float band in new[] { 10f, 26f })
                                for (int k = 0; k < 6; k++)
                                    for (int row = 0; row < 2; row++)
                                    {
                                        Vector3 a = Vector3.Lerp(f0, f1, k / 6f), b = Vector3.Lerp(f0, f1, (k + 1) / 6f);
                                        float y = v.floor + band + row * 4f;
                                        Quad((k + row) % 2 == 0 ? trimA : lightA, a.WithY(y) + inward * 0.6f, b.WithY(y) + inward * 0.6f, b.WithY(y + 4f) + inward * 0.6f, a.WithY(y + 4f) + inward * 0.6f);
                                    }
                            break;
                        }
                        case Design.Shade:
                        {
                            // now and then a shaft of light down the dark wall
                            if (panelIndex % 5 == 2)
                                Strip(lightA, ((f0 + f1) * 0.5f).WithY(v.floor + 2f), ((f0 + f1) * 0.5f).WithY(v.ceiling - 2f), inward, 3.5f);
                            break;
                        }
                        case Design.Gallery:
                        {
                            // a framed painting in every bay, pilasters between
                            Vector3 m0 = f0 + along * (pw * 0.18f), m1 = f1 - along * (pw * 0.18f), off = inward * 0.8f;
                            float ya = v.floor + 10f, yb = v.floor + 26f;
                            Quad(panelIndex % 3 == 0 ? lightA : trimA, m0.WithY(ya) + off, m1.WithY(ya) + off, m1.WithY(yb) + off, m0.WithY(yb) + off);
                            Strip(kit.glow, m0.WithY(ya - 0.8f), m1.WithY(ya - 0.8f), inward * 1.4f, 1.4f);
                            Strip(kit.glow, m0.WithY(yb + 0.8f), m1.WithY(yb + 0.8f), inward * 1.4f, 1.4f);
                            Strip(kit.glow, m0.WithY(ya - 0.8f), m0.WithY(yb + 0.8f), inward * 1.4f, 1.4f);
                            Strip(kit.glow, m1.WithY(ya - 0.8f), m1.WithY(yb + 0.8f), inward * 1.4f, 1.4f);
                            if (panelIndex % 2 == 0)
                                Box(wall, f0.WithY(v.floor + height * 0.5f) + inward * 1.85f, new Vector3(3.5f, height, 3.2f), Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg);
                            break;
                        }
                        case Design.Ruins:
                        {
                            // broken columns, a worn band
                            Strip(trimA, f0.WithY(v.floor + 7f), f1.WithY(v.floor + 7f), inward, 2.5f);
                            if (panelIndex % 2 == 0)
                            {
                                float colH = height * (0.25f + 0.6f * (float)dr.NextDouble());
                                Box(trimA, f0.WithY(v.floor + colH * 0.5f) + inward * 2.8f, new Vector3(5f, colH, 5f), (float)dr.NextDouble() * 20f);
                            }
                            break;
                        }
                        case Design.Backrooms:
                            Strip(trimA, f0.WithY(v.floor + 1.2f), f1.WithY(v.floor + 1.2f), inward, 2.4f); // a baseboard
                            break;
                        default:
                        {
                            // mixed: a band of colour, a light line and a pilaster every few bays
                            Strip(trimA, f0.WithY(v.floor + 14f), f1.WithY(v.floor + 14f), inward, 3f);
                            Strip(lightA, f0.WithY(v.floor + 17.5f), f1.WithY(v.floor + 17.5f), inward, 1.2f);
                            if (panelIndex % 4 == 0)
                                Box(trimA, f0.WithY(v.floor + height * 0.5f) + inward * 1.65f, new Vector3(3.5f, height, 2.8f), Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg);
                            break;
                        }
                    }
                }
            }

            // Floor and ceiling: a grid of panels over the outline
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in o) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
            for (float x = minX; x < maxX; x += Panel)
                for (float z = minZ; z < maxZ; z += Panel)
                {
                    float x1 = Mathf.Min(x + Panel, maxX), z1 = Mathf.Min(z + Panel, maxZ);
                    if (!Inside(o, new Vector2((x + x1) * 0.5f, (z + z1) * 0.5f))) continue;
                    Quad(floor, new Vector3(x, v.floor, z), new Vector3(x1, v.floor, z), new Vector3(x1, v.floor, z1), new Vector3(x, v.floor, z1));
                    if (!v.openTop)
                    {
                        int gx = Mathf.RoundToInt((x - minX) / Panel), gz = Mathf.RoundToInt((z - minZ) / Panel);
                        // skylights (stripes), a grid of light panels (tech), or a plain roof
                        bool sky = (design == Design.Stripes && gz % 3 == 1) || (design == Design.Panels && (gx + gz) % 2 == 0)
                                || (design == Design.Zen && (gx + gz) % 3 == 0) || (design == Design.Gallery && gz % 2 == 0);
                        Quad(ceiling, new Vector3(x, v.ceiling, z), new Vector3(x, v.ceiling, z1), new Vector3(x1, v.ceiling, z1), new Vector3(x1, v.ceiling, z));
                        if (sky)
                        {
                            float ix = (x1 - x) * 0.18f, iz = (z1 - z) * 0.18f, y = v.ceiling - 1f;
                            Quad(lightA, new Vector3(x + ix, y, z + iz), new Vector3(x + ix, y, z1 - iz), new Vector3(x1 - ix, y, z1 - iz), new Vector3(x1 - ix, y, z + iz));
                        }
                        // fluorescent tubes (backrooms), a coffered roof (sanctum), stalactites
                        // (cave), a grid of light (neon), the measuring grid (dev)
                        if (design == Design.Backrooms)
                            foreach (float f in new[] { 0.3f, 0.7f })
                            {
                                float zc = Mathf.Lerp(z, z1, f), y = v.ceiling - 0.6f;
                                Quad(lightA, new Vector3(x + 6f, y, zc - 1.2f), new Vector3(x + 6f, y, zc + 1.2f), new Vector3(x1 - 6f, y, zc + 1.2f), new Vector3(x1 - 6f, y, zc - 1.2f));
                            }
                        if (design == Design.Sanctum)
                        {
                            Box(kit.glow, new Vector3(x, v.ceiling - 2.75f, (z + z1) * 0.5f), new Vector3(2.4f, 5f, z1 - z), 0f);
                            Box(kit.glow, new Vector3((x + x1) * 0.5f, v.ceiling - 2.75f, z), new Vector3(x1 - x, 5f, 2.4f), 0f);
                        }
                        if (design == Design.Cave && dr.NextDouble() < 0.3)
                            Box(trimA, new Vector3((x + x1) * 0.5f, v.ceiling - 6f, (z + z1) * 0.5f), new Vector3(4f, 12f, 4f), (float)dr.NextDouble() * 90f);
                        if (design == Design.NeonGrid && gx % 2 == 0)
                            Box(lightA, new Vector3(x, v.ceiling - 0.8f, (z + z1) * 0.5f), new Vector3(1.2f, 0.4f, z1 - z), 0f);
                        if (design == Design.DevGrid)
                            Box(trimA, new Vector3(x, v.ceiling - 0.8f, (z + z1) * 0.5f), new Vector3(1f, 0.4f, z1 - z), 0f);
                        // heavy beams under the roof (ribs) or a frame of beams (mixed)
                        if ((design == Design.Ribs && gx % 2 == 0) || (design == Design.Mixed && gx % 3 == 0))
                            Box(trimA, new Vector3(x, v.ceiling - 2.75f, (z + z1) * 0.5f), new Vector3(3f, 5f, z1 - z), 0f); // (just under the roof, not in it)
                    }
                    // glowing grid lines on the floor every other panel
                    if (Mathf.RoundToInt((x - minX) / Panel) % 2 == 0)
                        Strip(kit.glow, new Vector3(x, v.floor, z), new Vector3(x, v.floor, z1), Vector3.up, 0.6f);
                }

            // Set dressing, a different mix in every hall, like a map's: pillars standing floor to
            // ceiling with bands of light, floating platforms and stacked blocks at all heights.
            // All of it keeps well clear of where you ride and fly.
            void Box(Material m, Vector3 c, Vector3 size, float yaw)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 X = q * Vector3.right * (size.x * 0.5f), Y = Vector3.up * (size.y * 0.5f), Z = q * Vector3.forward * (size.z * 0.5f);
                Quad(m, c - X - Y - Z, c + X - Y - Z, c + X + Y - Z, c - X + Y - Z);
                Quad(m, c - X - Y + Z, c - X + Y + Z, c + X + Y + Z, c + X - Y + Z);
                Quad(m, c - X - Y - Z, c - X + Y - Z, c - X + Y + Z, c - X - Y + Z);
                Quad(m, c + X - Y - Z, c + X - Y + Z, c + X + Y + Z, c + X + Y - Z);
                Quad(m, c - X + Y - Z, c + X + Y - Z, c + X + Y + Z, c - X + Y + Z);
                Quad(m, c - X - Y - Z, c - X - Y + Z, c + X - Y + Z, c + X - Y - Z);
            }
            bool ClearAcross(Vector3 p, float r) // nothing of the course anywhere above or below
            {
                foreach (var c in v.course) { float dx = c.x - p.x, dz = c.z - p.z; if (dx * dx + dz * dz < r * r) return false; }
                return true;
            }
            bool ClearOf(Vector3 p, float r)
            {
                foreach (var c in v.course) if ((c - p).sqrMagnitude < r * r) return false;
                return true;
            }
            var rng = new System.Random(v.stage * 7919 + 17);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material accent = kit.accent ? kit.accent : ceiling, glowA = kit.glow, glowB = kit.glowAlt ? kit.glowAlt : kit.glow;
            int look = v.stage % 4; // 0 colonnade, 1 floating platforms, 2 stacks, 3 a bit of everything
            float h = v.ceiling - v.floor;
            if (look == 0 || look == 3)
            {
                float step = look == 0 ? 60f : 90f;
                int made = 0;
                for (float x = minX + step * 0.5f; x < maxX && made < 30; x += step)
                    for (float z = minZ + step * 0.5f; z < maxZ && made < 30; z += step)
                    {
                        var at = new Vector3(x + R(-10f, 10f), 0f, z + R(-10f, 10f));
                        if (!Inside(o, new Vector2(at.x, at.z)) || !ClearAcross(at, 45f) || rng.NextDouble() < 0.3) continue;
                        float w = R(6f, 10f);
                        Box(wall, at.WithY(v.floor + h * 0.5f), new Vector3(w, h, w), 0f);
                        for (float y = v.floor + 30f; y < v.ceiling - 10f; y += 45f)
                            Box(glowA, at.WithY(y), new Vector3(w + 0.6f, 1.2f, w + 0.6f), 0f);
                        made++;
                    }
            }
            if (look == 1 || look == 3)
            {
                int made = 0;
                for (int tries = 0; tries < 200 && made < (look == 1 ? 26 : 12); tries++)
                {
                    var at = new Vector3(R(minX, maxX), R(v.floor + 15f, v.ceiling - 15f), R(minZ, maxZ));
                    if (!Inside(o, new Vector2(at.x, at.z)) || !ClearOf(at, 45f)) continue;
                    float w = R(16f, 36f), d = R(16f, 36f), yaw = R(0f, 90f);
                    Box(accent, at, new Vector3(w, R(2f, 5f), d), yaw);
                    Box(glowB, at + Vector3.down * 3.2f, new Vector3(w * 0.6f, 0.4f, d * 0.6f), yaw); // light underneath
                    made++;
                }
            }
            if (look == 2 || look == 3)
            {
                int made = 0;
                for (int tries = 0; tries < 200 && made < (look == 2 ? 18 : 8); tries++)
                {
                    var at = new Vector3(R(minX, maxX), v.floor, R(minZ, maxZ));
                    if (!Inside(o, new Vector2(at.x, at.z)) || !ClearAcross(at, 40f)) continue;
                    float y = v.floor, size = R(14f, 26f), yaw = R(0f, 90f);
                    int blocks = rng.Next(3, 8);
                    for (int k = 0; k < blocks && y < v.ceiling - 20f; k++)
                    {
                        float bh = R(8f, 22f);
                        Box(k % 2 == 0 ? wall : accent, at.WithY(y + bh * 0.5f), new Vector3(size, bh, size), yaw);
                        Box(glowA, at.WithY(y + bh + 0.4f), new Vector3(size + 0.5f, 0.8f, size + 0.5f), yaw);
                        y += bh + 0.8f;
                        size *= R(0.7f, 0.95f);
                        yaw += R(-25f, 25f);
                    }
                    made++;
                }
            }

            var go = new GameObject($"Hall {v.stage}");
            go.transform.SetParent(parent, false);
            foreach (var (m, f) in faces)
            {
                var mesh = new Mesh { name = "Architecture", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(f.v);
                mesh.SetNormals(f.n);
                var uv = new Vector2[f.v.Count];
                for (int i = 0; i < uv.Length; i++)
                {
                    Vector3 n = f.n[i], p = f.v[i];
                    Vector3 a = new(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                    // a repeat every 16m: on walls this big a finer pattern turns to shimmer and
                    // moire in the distance (real maps scale their textures up on big surfaces too)
                    uv[i] = (a.y >= a.x && a.y >= a.z ? new Vector2(p.x, p.z) : a.x >= a.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) / 16f;
                }
                Architecture.Batch.SmallUvs(uv);
                mesh.uv = uv;
                mesh.SetTriangles(f.t, 0);
                mesh.RecalculateBounds();
                meshes.Add(mesh);
                var part = new GameObject("Architecture");
                part.transform.SetParent(go.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = part.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = m != kit.glow && m != kit.glowAlt; // the walls catch the ramps' shadows
            }
            return go;
        }

        // A tube round a flight path, for clearing the panels it passes through (a doorway
        // between halls, a window onto the next one): a bundle of lines filling its section
        // (each as a flat triangle), close enough together that no panel it crosses is missed
        public static List<Vector3> FlightTube(IList<Vector3> path, float radius, bool dense = true)
        {
            var tris = new List<Vector3>();
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 a = path[i], b = path[i + 1], f = (b - a);
                if (f.sqrMagnitude < 1e-4f) continue;
                f.Normalize();
                Vector3 s = Vector3.Cross(Vector3.up, f);
                if (s.sqrMagnitude < 1e-4f) s = Vector3.right;
                s.Normalize();
                Vector3 u = Vector3.Cross(f, s);
                void Line(Vector3 o) { tris.Add(a + o); tris.Add(b + o); tris.Add(b + o); }
                Line(Vector3.zero);
                foreach (float r in dense ? new[] { radius * 0.5f, radius } : new[] { radius })
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * Mathf.PI / 4f;
                        Line((s * Mathf.Cos(ang) + u * Mathf.Sin(ang)) * r);
                    }
            }
            return tris;
        }

        static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);

        static bool Inside(List<Vector2> o, Vector2 q)
        {
            for (int i = 0; i < o.Count; i++)
            {
                Vector2 a = o[i], b = o[(i + 1) % o.Count];
                if ((b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x) < 0f) return false;
            }
            return true;
        }

        // Convex hull, counter-clockwise (monotone chain)
        static List<Vector2> Hull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            foreach (var p in pts)
            {
                while (h.Count >= 2 && Cross(h[^2], h[^1], p) <= 0f) h.RemoveAt(h.Count - 1);
                h.Add(p);
            }
            int lower = h.Count + 1;
            for (int i = pts.Count - 2; i >= 0; i--)
            {
                var p = pts[i];
                while (h.Count >= lower && Cross(h[^2], h[^1], p) <= 0f) h.RemoveAt(h.Count - 1);
                h.Add(p);
            }
            h.RemoveAt(h.Count - 1);
            return h;
        }
    }
}
