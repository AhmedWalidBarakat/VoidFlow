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
        // ...and every other zone dressed after its name, the way each of the great maps dresses
        // its walls to fit its run (rusted grates round rusted ramps, torchlit brick round a
        // tomb's, neon round neon):
        //  - Crystal: rock bursting with clusters of glowing crystals, crystals hanging from the
        //    roof, veins of light (cyan crystal, amethyst, glacier cave)
        //  - Castle: a wall walk with battlements, arrow slits, towers, banners (castle walls,
        //    great wall)
        //  - Hex / HexVines: a honeycomb of glowing hexagons (hex lab), overgrown (hex jungle)
        //  - Pixel: big pixel monsters of light on a dark wall (arcade monsters, toy town)
        //  - Hearts: ribs of light round the tunnel, pixel hearts (love tunnel)
        //  - Waves: bands of light rolling round the room (chrome wave)
        //  - Retro: sunset stripes and neon triangles (synthwave, vapor geometry, ember sunset)
        //  - Warehouse: tall many-paned windows, steel columns, stacked crates, roof trusses
        //  - Barn: plank walls, cross bracing, lanterns, rafters (cornfield)
        //  - Stadium: stepped stands round the foot, a checkered band, floodlights (race arena,
        //    skate park)
        //  - Streaks: streaks of light at speed along the walls (light streaks)
        //  - Lab: a red line running round the room like a circuit (red line lab)
        //  - Bunker: heavy buttresses, slit windows, caged lamps, heavy beams (concrete bunker)
        //  - Shapes: great neon circles, triangles and squares on the dark (neon shapes/rings)
        //  - Arches: an arcade of round arches under a cornice (palaces, terraces, mauve temple)
        //  - Stepped: ledges stepping out down the walls in bands (stripe pyramid, canyon, quarry)
        //  - Nautical: portholes, lifebuoys, a wave band (sea mines)
        //  - City: building fronts with lit windows round the room (glass city, night city)
        //  - Library: shelves of books up the walls (library)
        //  - Mine: rock held up by timber frames hung with lanterns (torch mines)
        //  - Crater: grey walls pocked with craters, boulders round the foot (moon crater)
        //  - Furnace: glowing furnace mouths, chimney ducts (forge, dark foundry)
        //  - Banners: long flags hanging down the walls (flag hills)
        public enum Design { Stripes, Ribs, Panels, Mixed, Cave, NeonGrid, Spikes, Patchwork, Industrial, Sanctum, Tomb, DevGrid, Zen, Funhouse, Shade, Facets, Gallery, Ruins, Backrooms,
            Crystal, Castle, Hex, HexVines, Pixel, Hearts, Waves, Retro, Warehouse, Barn, Stadium, Streaks, Lab, Bunker, Shapes, Arches, Stepped, Nautical, City, Library, Mine, Crater, Furnace, Banners }

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
            if (n.Contains("CRYSTAL") || n.Contains("AMETHYST") || n.Contains("GLACIER")) return Design.Crystal;
            if (n.Contains("CASTLE") || n.Contains("GREAT WALL")) return Design.Castle;
            if (n.Contains("HEX JUNGLE")) return Design.HexVines;
            if (n.Contains("HEX")) return Design.Hex;
            if (n.Contains("ARCADE") || n.Contains("TOY TOWN")) return Design.Pixel;
            if (n.Contains("LOVE")) return Design.Hearts;
            if (n.Contains("CHROME")) return Design.Waves;
            if (n.Contains("SYNTHWAVE") || n.Contains("VAPOR") || n.Contains("EMBER SUNSET")) return Design.Retro;
            if (n.Contains("WAREHOUSE")) return Design.Warehouse;
            if (n.Contains("CORNFIELD")) return Design.Barn;
            if (n.Contains("RACE ARENA") || n.Contains("SKATE")) return Design.Stadium;
            if (n.Contains("STREAKS")) return Design.Streaks;
            if (n.Contains("RED LINE")) return Design.Lab;
            if (n.Contains("BUNKER")) return Design.Bunker;
            if (n.Contains("NEON SHAPES") || n.Contains("NEON RINGS") || n.Contains("OMNIFIC")) return Design.Shapes;
            if (n.Contains("PALACE") || n.Contains("CELESTIAL") || n.Contains("MAUVE") || n.Contains("MOONLIT GARDEN")) return Design.Arches;
            if (n.Contains("PYRAMID") || n.Contains("CANYON") || n.Contains("QUARRY")) return Design.Stepped;
            if (n.Contains("SEA MINES")) return Design.Nautical;
            if (n.Contains("CITY") || n.Contains("SURF TOWN")) return Design.City;
            if (n.Contains("LIBRARY")) return Design.Library;
            if (n.Contains("TORCH MINES")) return Design.Mine;
            if (n.Contains("MOON CRATER")) return Design.Crater;
            if (n == "FORGE" || n.Contains("DARK FOUNDRY")) return Design.Furnace;
            if (n.Contains("HAZARD FOUNDRY")) return Design.Industrial;
            if (n.Contains("FLAG HILLS")) return Design.Banners;
            if (n.Contains("WHITE GALLERY")) return Design.Gallery;
            if (n.Contains("CANDY") || n.Contains("FRUIT")) return Design.Funhouse;
            if (n.Contains("WIREFRAME") || n == "PRO") return Design.NeonGrid;
            if (n.Contains("GROTTO") || n.Contains("ALPINE") || n.Contains("WILD HILLS")) return Design.Cave;
            if (n.Contains("SANDSTONE")) return Design.Tomb;
            if (n.Contains("SIX SIX SIX")) return Design.Spikes;
            if (n.Contains("JADE")) return Design.Zen;
            if (n.Contains("SURF SCHOOL")) return Design.DevGrid;
            if (n.Contains("SPECTRUM") || n.Contains("SUMMER")) return Design.Stripes;
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
            public List<Vector3> lines;     // the riding lines alone (a ramp crossing a dividing wall needs a wider opening than a flight)
            public List<Divider> dividers;  // walls across the hall, each between one ramp's chamber and the next

            // The floor in terraces: each panel of the floor at its own height, stepping down to
            // sit a little under the course above it (as a map's floor follows its ramps down),
            // instead of one floor far below everything. Panels left out (where a neighbouring
            // hall needs the room) aren't present.
            public float gridX, gridZ;
            public Dictionary<Vector2Int, float> terrace;
            public HashSet<Vector2Int> present;
            public Vector2Int Cell(float x, float z) => new(Mathf.FloorToInt((x - gridX) / Panel), Mathf.FloorToInt((z - gridZ) / Panel));
            public float Terrace(float x, float z) => terrace != null && terrace.TryGetValue(Cell(x, z), out float h) ? h : floor;
            // The floor's height under a point, if this hall has floor there
            public float? FloorAt(float x, float z)
            {
                var c = Cell(x, z);
                if (present != null && !present.Contains(c)) return null;
                return terrace != null && terrace.TryGetValue(c, out float h) ? h : floor;
            }

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

        // A wall across a hall at a flight: a window (the hole planned for that jump, framed and
        // just big enough) or a mouth (an opening round wherever the course crosses it)
        public class Divider
        {
            public Vector3 at, dir;      // a point on the flight, and its way across
            public bool window;          // its flight's opening is planned (a window, or a doorway: mouth)
            public bool mouth;           // a doorway out of a chamber, wider than a window
            public Vector3 center;       // the opening's centre
            public float hw, hh;         // the opening's half width and height
            public int ramp;             // the ramp the flight lands on
        }

        // Where a built dividing wall stands (course-local), for the sweep that checks the course
        // gets through it
        public class DividerPlane : MonoBehaviour
        {
            public Vector3 point, normal;
            public bool window, mouth;
            public float holeY, holeW, holeH; // its planned opening (centred on `point`), for the bots' reports
        }

        static readonly string[] Heart = { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#..." };

        // Ivy: a leafy green made from the hall's own wall material (one per material)
        static readonly Dictionary<Material, Material> ivy = new();
        static Material Ivy(Material from)
        {
            if (!from) return from;
            if (ivy.TryGetValue(from, out var m) && m) return m;
            m = new Material(from) { name = from.name + "_Ivy" };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.2f, 0.36f, 0.14f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            return ivy[from] = m;
        }

        public const float Margin = 95f, Headroom = 95f, Panel = 30f; // room for every line and speed, not just the designed one
        // A floor carries on over a neighbouring hall's room only where none of that hall's
        // course comes within this far across, and this far above it (ramps reach well below
        // their riding lines)
        const float FloorClearance = 70f, FloorHeadroom = 80f;

        // The floor's terraces sit this far under the lowest of the course within reach (ramp
        // bodies, flights, platforms), in steps of this much
        const float TerraceGap = 18f, TerraceStep = 36f, TerraceReach = 60f;

        // A stage's hall round its course points (riding lines and flights). `lows` are the
        // lowest the course reaches (the bottoms of ramp bodies, flights less a rider's
        // clearance): the floor's terraces keep under them; `lines` the riding lines alone.
        public static Volume Plan(int stage, List<Vector3> points, float depth, bool openTop, BiomeKit kit, List<Vector3> lows = null, List<Vector3> lines = null, bool flat = false)
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
            var v = new Volume { stage = stage, outline = Hull(ring), floor = lo - depth, ceiling = hi + Headroom, openTop = openTop, kit = kit, course = course, lines = lines ?? new List<Vector3>() };
            if (lows != null && lows.Count > 0 && !flat) PlanTerraces(v, lows);
            return v;
        }

        // Each panel of the floor at the height it can rise to: TerraceGap under the lowest of
        // the course within TerraceReach across (the nearest bit of course, for panels out by the
        // walls), in steps, so the floor reads as level terraces dropping in cliffs. A panel
        // jutting out above three or four of its neighbours comes down to the highest of those
        // (a few times over), so the terraces have clean edges rather than a jagged maze (only
        // ever lowered: the course keeps its clearance).
        static void PlanTerraces(Volume v, List<Vector3> lows)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var q in v.outline) { minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x); minZ = Mathf.Min(minZ, q.y); maxZ = Mathf.Max(maxZ, q.y); }
            v.gridX = minX; v.gridZ = minZ;
            var raw = new Dictionary<Vector2Int, float>();
            float lowest = v.floor;
            int nx = Mathf.CeilToInt((maxX - minX) / Panel), nz = Mathf.CeilToInt((maxZ - minZ) / Panel);
            for (int gx = 0; gx < nx; gx++)
                for (int gz = 0; gz < nz; gz++)
                {
                    float cx = minX + (gx + 0.5f) * Panel, cz = minZ + (gz + 0.5f) * Panel;
                    float best = float.MaxValue, nearest = float.MaxValue, nearestY = 0f;
                    foreach (var q in lows)
                    {
                        float d2 = (q.x - cx) * (q.x - cx) + (q.z - cz) * (q.z - cz);
                        if (d2 < TerraceReach * TerraceReach) best = Mathf.Min(best, q.y);
                        if (d2 < nearest) { nearest = d2; nearestY = q.y; }
                    }
                    float h = (best < float.MaxValue ? best : nearestY) - TerraceGap;
                    raw[new Vector2Int(gx, gz)] = h;
                    lowest = Mathf.Min(lowest, h);
                }
            // (the hall's lowest floor goes under every terrace: the walls start there)
            v.floor = Mathf.Min(v.floor, lowest);
            var t = new Dictionary<Vector2Int, float>();
            foreach (var (c, h) in raw) t[c] = v.floor + Mathf.Floor((h - v.floor) / TerraceStep) * TerraceStep;
            for (int pass = 0; pass < 3; pass++)
            {
                var smooth = new Dictionary<Vector2Int, float>();
                foreach (var (c, h) in t)
                {
                    int lower = 0;
                    float under = float.MinValue;
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                        if (t.TryGetValue(c + d, out float hn) && hn < h - 0.5f) { lower++; under = Mathf.Max(under, hn); }
                    smooth[c] = lower >= 3 ? under : h;
                }
                t = smooth;
            }
            v.terrace = t;
        }

        // Builds a hall's panels, leaving out any that stand inside a neighbouring hall
        public static GameObject Build(Volume v, List<Volume> neighbours, Transform parent, List<Mesh> meshes, Mesh cube)
        {
            var kit = v.kit;
            var faces = new Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<int> t)>();
            var main = faces; // (faces go to the hall itself, its floor or one of its dividing walls)
            var floorFaces = new Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<int> t)>();
            var dividerFaces = new List<Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<int> t)>>();
            var dividerPlanes = new List<(Vector3 point, Vector3 normal, bool window, bool mouth, float y, float w, float h)>();
            bool Outside(Vector3 c)
            {
                foreach (var o in neighbours) if (o.Contains(c, 0.5f)) return false;
                return true;
            }
            // a motif (a ring, a picture, a crystal) stands or goes as a whole: its pieces one
            // by one would leave it in broken dashes where it meets a neighbouring hall
            int whole = 0; // 0 each face on its own, 1 keep, -1 leave out
            void Motif(Vector3 at) => whole = Outside(at) ? 1 : -1;
            bool force = false; // (the floor carried on over a neighbour's room, and its cliffs: kept regardless)
            void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                if (!m || whole < 0 || (whole == 0 && !force && !Outside((a + b + c + d) * 0.25f))) return;
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

            Material wall = kit.scenery ? kit.scenery : kit.slab, floor = kit.floor ? kit.floor : kit.slab, ceiling = kit.ceiling ? kit.ceiling : kit.slab ? kit.slab : wall;
            // The hot zones' floors are a sea of lava, after cannonball's (falling in is a fall,
            // as any floor is)
            if (v.design is Design.Furnace or Design.Spikes && (kit.glowAlt || kit.glow)) floor = kit.glowAlt ? kit.glowAlt : kit.glow;
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
            if ((design is Design.Panels or Design.NeonGrid or Design.Shade or Design.Industrial or Design.DevGrid or Design.Hex or Design.Pixel or Design.Hearts
                 or Design.Waves or Design.Retro or Design.Barn or Design.Streaks or Design.Lab or Design.Shapes or Design.Crater or Design.Furnace) && kit.slab) wall = kit.slab;
            bool rock = design is Design.Cave or Design.Crystal or Design.Mine;
            float walked = 0f; // how far round the room (waves roll on unbroken from wall to wall)
            Vector3 runA = Vector3.zero, runB = Vector3.zero; // the straight run of wall being dressed
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
                runA = new Vector3(a2.x, 0f, a2.y); runB = new Vector3(b2.x, 0f, b2.y);
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
                        if (rock)
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
                        case Design.Mixed:
                        {
                            // mixed: a band of colour, a light line and a pilaster every few bays
                            Strip(trimA, f0.WithY(v.floor + 14f), f1.WithY(v.floor + 14f), inward, 3f);
                            Strip(lightA, f0.WithY(v.floor + 17.5f), f1.WithY(v.floor + 17.5f), inward, 1.2f);
                            if (panelIndex % 4 == 0)
                                Box(trimA, f0.WithY(v.floor + height * 0.5f) + inward * 1.65f, new Vector3(3.5f, height, 2.8f), Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg);
                            break;
                        }
                        default:
                            Themed(f0, f1, along, inward, pw, panelIndex, rows, walked);
                            break;
                    }
                    walked += pw;
                    // Ivy hanging down the old stone walls, after atrium's
                    if (design is Design.Ruins or Design.Arches or Design.Castle or Design.Stepped && dr.NextDouble() < 0.5)
                    {
                        float S = Mathf.Clamp(height / 140f, 1f, 4f);
                        Vector3 at = Vector3.Lerp(f0, f1, (float)dr.NextDouble()) + inward * 1.4f;
                        float y1 = v.openTop ? v.ceiling - 2f : v.ceiling - 4f, y0 = y1 - height * (0.15f + 0.45f * (float)dr.NextDouble());
                        for (float y = y0; y < y1; y += 3.2f * S)
                            Box(Ivy(wall), at.WithY(y) + along * ((float)dr.NextDouble() - 0.5f) * 2f * S,
                                new Vector3(1f, 3f * S, (1.5f + (float)dr.NextDouble() * 2f) * S), Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg + ((float)dr.NextDouble() - 0.5f) * 40f);
                    }
                }
            }

            // Dividing walls, after the maps that pass from room to room (boreas, the white rooms,
            // 666): a wall right across the hall between one ramp's chamber and the next, the
            // flight through it the only way on, so each ramp waits round its own corner and the
            // next only shows as you come through. A window is the opening planned for that jump:
            // just big enough for every line off the ramp, solid all round, a real window to aim
            // for. Elsewhere the wall opens round wherever the course crosses it. Every opening is
            // a clean rounded hole in a deep frame with a glowing rim. A wall the course would
            // have to cross in more than a couple of other places isn't built (more holes than
            // wall). Each wall is a piece of its own: solid, and taken down again if a player-sized
            // sweep along the course can't get through it (see EndlessCourse.BuildHall).
            if (v.dividers != null)
                foreach (var dv in v.dividers)
                {
                    Vector3 n = new Vector3(dv.dir.x, 0f, dv.dir.z);
                    if (n.sqrMagnitude < 1e-4f) continue;
                    n.Normalize();
                    Vector3 t = new(-n.z, 0f, n.x), P = dv.window ? dv.center : dv.at, basePoint = new(P.x, 0f, P.z);
                    // where the wall's line crosses the outline (it's convex: one span)
                    float s0 = float.MaxValue, s1 = float.MinValue;
                    Vector2 P2 = new(P.x, P.z), T = new(t.x, t.z);
                    for (int k = 0; k < o.Count; k++)
                    {
                        Vector2 a = o[k], e = o[(k + 1) % o.Count] - a, d = a - P2;
                        float den = T.x * e.y - T.y * e.x;
                        if (Mathf.Abs(den) < 1e-5f) continue;
                        float u = (d.x * T.y - d.y * T.x) / den;
                        if (u < -1e-4f || u > 1f + 1e-4f) continue;
                        float sAt = (d.x * e.y - d.y * e.x) / den;
                        s0 = Mathf.Min(s0, sAt); s1 = Mathf.Max(s1, sAt);
                    }
                    if (s1 - s0 < 40f) continue;
                    var holes = new List<(float s, float y, float hw, float hh)>();
                    if (dv.window) holes.Add((0f, dv.center.y, dv.hw, dv.hh));
                    // where the course crosses the wall: a box round each crossing
                    var boxes = new List<(float s0, float s1, float y0, float y1, bool line)>();
                    void Crossing(Vector3 c, bool line)
                    {
                        if (Mathf.Abs(Vector3.Dot(c - basePoint, n)) > 12f) return;
                        float sc = Vector3.Dot(c - basePoint, t), yc = c.y;
                        if (sc < s0 - 20f || sc > s1 + 20f || yc < v.floor || yc > v.ceiling) return;
                        foreach (var hole in holes) if (Super(sc - hole.s, yc - hole.y, hole.hw, hole.hh) < 3f) return; // (through the window, or its flight just either side of it)
                        for (int k = 0; k < boxes.Count; k++)
                        {
                            var bx = boxes[k];
                            if (sc > bx.s0 - 30f && sc < bx.s1 + 30f && yc > bx.y0 - 30f && yc < bx.y1 + 30f)
                            {
                                boxes[k] = (Mathf.Min(bx.s0, sc), Mathf.Max(bx.s1, sc), Mathf.Min(bx.y0, yc), Mathf.Max(bx.y1, yc), bx.line || line);
                                return;
                            }
                        }
                        boxes.Add((sc, sc, yc, yc, line));
                    }
                    foreach (var c in v.lines) Crossing(c, true);
                    foreach (var c in v.course) Crossing(c, false);
                    foreach (var nb in neighbours)
                    {
                        if (nb.lines != null) foreach (var c in nb.lines) Crossing(c, true);
                        if (nb.course != null) foreach (var c in nb.course) Crossing(c, false);
                    }
                    if (boxes.Count > (dv.window ? 2 : 3)) continue; // more holes than wall
                    // (a ramp crossing needs room for its body under the line, a flight for every line through it)
                    foreach (var bx in boxes)
                        holes.Add(((bx.s0 + bx.s1) * 0.5f, (bx.y0 + bx.y1) * 0.5f, (bx.s1 - bx.s0) * 0.5f + (bx.line ? 24f : 30f), (bx.y1 - bx.y0) * 0.5f + (bx.line ? 28f : 30f)));

                    faces = new Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<int> t)>();
                    dividerFaces.Add(faces);
                    dividerPlanes.Add((basePoint, n, dv.window && !dv.mouth, dv.mouth, dv.center.y, dv.hw, dv.hh));
                    force = true;
                    Vector3 W(float sw, float y) => basePoint + t * sw + Vector3.up * y;
                    // the wall in panels, finer round the holes: a panel clear of every hole is
                    // laid whole, one wholly in a hole left out, one on a hole's edge split in four
                    // down to small ones (those on the edge itself are left to the frame)
                    int Classify(float sa, float sb, float ya, float yb)
                    {
                        int result = 0;
                        foreach (var hole in holes)
                        {
                            float nx = Mathf.Clamp(hole.s, sa, sb) - hole.s, ny = Mathf.Clamp(hole.y, ya, yb) - hole.y;
                            if (Super(nx, ny, hole.hw, hole.hh) >= 1f) continue;
                            float fx = Mathf.Max(Mathf.Abs(sa - hole.s), Mathf.Abs(sb - hole.s)), fy = Mathf.Max(Mathf.Abs(ya - hole.y), Mathf.Abs(yb - hole.y));
                            if (Super(fx, fy, hole.hw, hole.hh) < 1f) return 1;
                            result = 2;
                        }
                        return result;
                    }
                    void Fill(float sa, float sb, float ya, float yb)
                    {
                        int c = Classify(sa, sb, ya, yb);
                        if (c == 1) return;
                        if (c == 0) { Quad(wall, W(sa, ya), W(sb, ya), W(sb, yb), W(sa, yb)); return; }
                        if (sb - sa < 2.6f) return;
                        float sm = (sa + sb) * 0.5f, ym = (ya + yb) * 0.5f;
                        Fill(sa, sm, ya, ym); Fill(sm, sb, ya, ym); Fill(sa, sm, ym, yb); Fill(sm, sb, ym, yb);
                    }
                    const float Big = 60f;
                    for (float sa = s0; sa < s1 - 0.5f; sa += Big)
                        for (float ya = v.floor; ya < v.ceiling - 0.5f; ya += Big)
                            Fill(sa, Mathf.Min(sa + Big, s1), ya, Mathf.Min(ya + Big, v.ceiling));
                    // each hole's frame: a deep moulding round it on both faces, a glowing rim on
                    // its inner edge, and the reveal through the wall
                    foreach (var hole in holes)
                    {
                        const int sides = 40;
                        const float FrameW = 3.2f, Deep = 1.4f, RimW = 0.8f;
                        Vector2 H(int i, float grow)
                        {
                            float ang = i * Mathf.PI * 2f / sides, c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                            return new Vector2((hole.hw + grow) * Mathf.Sign(c) * Mathf.Sqrt(Mathf.Abs(c)), (hole.hh + grow) * Mathf.Sign(sn) * Mathf.Sqrt(Mathf.Abs(sn)));
                        }
                        Vector3 F(Vector2 q, float z) => W(hole.s + q.x, hole.y + q.y) + n * z;
                        for (int i = 0; i < sides; i++)
                        {
                            Vector2 a0 = H(i, 0f), a1 = H(i + 1, 0f), b0 = H(i, FrameW), b1 = H(i + 1, FrameW), g0 = H(i, RimW), g1 = H(i + 1, RimW);
                            foreach (float z in new[] { -Deep, Deep })
                            {
                                Quad(trimA, F(a0, z), F(b0, z), F(b1, z), F(a1, z));
                                float zg = z + Mathf.Sign(z) * 0.08f;
                                Quad(kit.glow, F(a0, zg), F(g0, zg), F(g1, zg), F(a1, zg));
                            }
                            Quad(trimA, F(a0, -Deep), F(a1, -Deep), F(a1, Deep), F(a0, Deep));
                            Quad(trimA, F(b0, -Deep), F(b1, -Deep), F(b1, Deep), F(b0, Deep));
                        }
                    }
                    force = false;
                    faces = main;
                }

            // Floor and ceiling: panels over the outline, cut to it exactly where the walls stand
            // (a panel the outline crosses is clipped to it, so the floor meets the walls all the
            // way round). The floor stands in terraces (PlanTerraces): each panel at its own height
            // a little under the course above it, as a map's floor follows its ramps down. Where
            // the next hall runs into this one, a panel carries on over that hall's room wherever
            // its course passes well above, and is left out where its course needs the room. Every
            // drop, from one terrace to the next or down into the next hall, is a cliff with a
            // trimmed, glowing lip; skirting runs round the foot of the walls.
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in o) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
            bool lava = v.design is Design.Furnace or Design.Spikes; // (a sea of lava lies flat at the bottom)
            bool neon = v.design is Design.NeonGrid or Design.DevGrid or Design.Retro or Design.Streaks or Design.Shapes or Design.Pixel;
            float Level(Vector2Int c) => lava || v.terrace == null ? v.floor : v.terrace.TryGetValue(c, out float h) ? h : v.floor;
            bool FloorStays(Vector3 at)
            {
                foreach (var n in neighbours)
                {
                    if (!n.Contains(at, 0.5f)) continue;
                    if (n.course == null) return false;
                    foreach (var q in n.course)
                        if ((q.x - at.x) * (q.x - at.x) + (q.z - at.z) * (q.z - at.z) < FloorClearance * FloorClearance && q.y < at.y + FloorHeadroom) return false;
                }
                return true;
            }
            v.gridX = minX; v.gridZ = minZ;
            v.present = new HashSet<Vector2Int>();
            var cells = new Dictionary<Vector2Int, (List<Vector2> poly, Rect r)>();
            void Poly(Material m, List<Vector2> pts, float y)
            {
                for (int i = 1; i + 1 < pts.Count; i++)
                {
                    Vector3 a = new(pts[0].x, y, pts[0].y), b = new(pts[i].x, y, pts[i].y), c = new(pts[i + 1].x, y, pts[i + 1].y);
                    Quad(m, a, b, c, c);
                }
            }
            int panelsX = Mathf.CeilToInt((maxX - minX) / Panel - 0.001f), panelsZ = Mathf.CeilToInt((maxZ - minZ) / Panel - 0.001f);
            for (int gx = 0; gx < panelsX; gx++)
                for (int gz = 0; gz < panelsZ; gz++)
                {
                    float x = minX + gx * Panel, z = minZ + gz * Panel;
                    float x1 = Mathf.Min(x + Panel, maxX), z1 = Mathf.Min(z + Panel, maxZ);
                    var poly = Clip(o, x, z, x1, z1);
                    if (poly.Count < 3) continue;
                    var cell = new Vector2Int(gx, gz);
                    bool full = Inside(o, new Vector2(x, z)) && Inside(o, new Vector2(x1, z)) && Inside(o, new Vector2(x1, z1)) && Inside(o, new Vector2(x, z1));
                    float level = Level(cell);
                    Vector2 cen = Vector2.zero;
                    foreach (var q in poly) cen += q;
                    cen /= poly.Count;
                    if (FloorStays(new Vector3(cen.x, level, cen.y)))
                    {
                        v.present.Add(cell);
                        cells[cell] = (poly, Rect.MinMaxRect(x, z, x1, z1));
                        faces = floorFaces; force = true;
                        Poly(floor, poly, level);
                        // neon zones: a fine grid of light ruled over the floor
                        if (neon && full)
                        {
                            Strip(kit.glow, new Vector3(x, level, z), new Vector3(x, level, z1), Vector3.up, 0.35f);
                            Strip(kit.glow, new Vector3(x, level, z), new Vector3(x1, level, z), Vector3.up, 0.35f);
                        }
                        force = false; faces = main;
                    }
                    if (!v.openTop)
                    {
                        // skylights (stripes), a grid of light panels (tech), or a plain roof
                        bool sky = (design == Design.Stripes && gz % 3 == 1) || (design == Design.Panels && (gx + gz) % 2 == 0)
                                || (design == Design.Zen && (gx + gz) % 3 == 0) || (design == Design.Gallery && gz % 2 == 0)
                                || (design == Design.Hex && (gx + gz) % 3 == 0) || (design == Design.Warehouse && gz % 3 == 1);
                        var roof = new List<Vector2>(poly);
                        roof.Reverse(); // (facing down)
                        Poly(ceiling, roof, v.ceiling);
                        if (!full) continue; // (the lights and beams only on whole panels: on cut ones they'd poke through the walls)
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
                        // Chandeliers hanging from the roof, after cannonball's: a ring of lights
                        // on chains, high above everything
                        if (design is Design.Tomb or Design.Castle or Design.Warehouse or Design.Library or Design.Arches or Design.Sanctum && dr.NextDouble() < 0.1)
                        {
                            float S = Mathf.Clamp(height / 140f, 1f, 3f);
                            Vector3 top = new((x + x1) * 0.5f, v.ceiling, (z + z1) * 0.5f);
                            float drop = 14f * S, rr = 4f * S;
                            if (ClearOf(top.WithY(v.ceiling - drop), 40f))
                            {
                                Box(trimA, top.WithY(v.ceiling - drop * 0.5f), new Vector3(0.4f, drop, 0.4f), 0f);
                                for (int k = 0; k < 8; k++)
                                {
                                    float a = k * Mathf.PI / 4f;
                                    Vector3 p = top.WithY(v.ceiling - drop) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr;
                                    Box(trimA, p, new Vector3(rr * 0.8f, 0.4f * S, 0.4f * S), -k * 45f + 90f);
                                    Box(kit.glow, p + Vector3.up * (0.8f * S), new Vector3(0.5f, 1.2f, 0.5f) * S, 0f);
                                }
                            }
                        }
                        if (design == Design.Crystal && dr.NextDouble() < 0.3)
                            for (int k = 0; k < 3; k++)
                                Shard(k == 0 ? kit.glow : lightA, new Vector3(Mathf.Lerp(x, x1, (float)dr.NextDouble()), v.ceiling, Mathf.Lerp(z, z1, (float)dr.NextDouble())),
                                    Vector3.down * 3f + new Vector3((float)dr.NextDouble() - 0.5f, 0f, (float)dr.NextDouble() - 0.5f), 8f + (float)dr.NextDouble() * 16f, 3f + (float)dr.NextDouble() * 3f);
                        if ((design is Design.Hearts or Design.Retro) && gx % 2 == 0)
                            Box(design == Design.Hearts ? lightA : kit.glow, new Vector3(x, v.ceiling - 0.8f, (z + z1) * 0.5f), new Vector3(1.4f, 0.4f, z1 - z), 0f);
                        if (design == Design.Lab && gx % 3 == 0)
                            Box(kit.glow, new Vector3(x, v.ceiling - 0.8f, (z + z1) * 0.5f), new Vector3(2f, 0.4f, z1 - z), 0f);
                        if (design == Design.NeonGrid && gx % 2 == 0)
                            Box(lightA, new Vector3(x, v.ceiling - 0.8f, (z + z1) * 0.5f), new Vector3(1.2f, 0.4f, z1 - z), 0f);
                        if (design == Design.DevGrid)
                            Box(trimA, new Vector3(x, v.ceiling - 0.8f, (z + z1) * 0.5f), new Vector3(1f, 0.4f, z1 - z), 0f);
                        // heavy beams under the roof (ribs) or a frame of beams (mixed)
                        if ((design == Design.Ribs && gx % 2 == 0) || (design == Design.Mixed && gx % 3 == 0) || design == Design.Barn
                            || ((design is Design.Castle or Design.Warehouse or Design.Bunker or Design.Arches or Design.Library or Design.Mine or Design.Furnace) && gx % 2 == 0))
                            Box(trimA, new Vector3(x, v.ceiling - 2.75f, (z + z1) * 0.5f), new Vector3(3f, 5f, z1 - z), 0f); // (just under the roof, not in it)
                    }
                }

            // The floor's drops: from each terrace down to a lower one beside it, or down into
            // the room of the next hall (to that hall's floor below), a cliff in the hall's wall,
            // a band of trim along its top and a glowing lip, so every drop reads as a terrace's
            // edge. Round the walls, skirting where the floor meets them.
            faces = floorFaces; force = true;
            foreach (var (cell, (poly, r)) in cells)
            {
                float top = Level(cell);
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    // the panel's edge on this side: its corners lying on that side of its square
                    float side = d.x > 0 ? r.xMax : d.x < 0 ? r.xMin : d.y > 0 ? r.yMax : r.yMin;
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var q in poly)
                    {
                        float on = d.x != 0 ? q.x : q.y, along = d.x != 0 ? q.y : q.x;
                        if (Mathf.Abs(on - side) > 0.05f) continue;
                        lo = Mathf.Min(lo, along); hi = Mathf.Max(hi, along);
                    }
                    if (hi - lo < 0.5f) continue;
                    Vector3 a = d.x != 0 ? new Vector3(side, top, lo) : new Vector3(lo, top, side), b = d.x != 0 ? new Vector3(side, top, hi) : new Vector3(hi, top, side);
                    Vector3 dir = new(d.x, 0f, d.y);
                    float bottom;
                    if (cells.ContainsKey(cell + d))
                    {
                        float hn = Level(cell + d);
                        if (hn >= top - 0.5f) continue;
                        bottom = hn;
                    }
                    else
                    {
                        Vector3 probe = (a + b) * 0.5f + dir * 3f + Vector3.down * 4f;
                        bottom = float.MaxValue;
                        foreach (var nb in neighbours)
                            if (nb.Contains(probe, 0.5f)) bottom = Mathf.Min(bottom, nb.Terrace(probe.x, probe.z));
                        if (bottom == float.MaxValue) continue; // (this hall's own wall stands there)
                        bottom = Mathf.Min(bottom, top - 4f);
                    }
                    Quad(wall, a, b, b.WithY(bottom), a.WithY(bottom));
                    Vector3 off = dir * 0.25f;
                    Quad(trimA, a + off, b + off, b + off + Vector3.down * 2.4f, a + off + Vector3.down * 2.4f);
                    Strip(kit.glow, a + Vector3.down * 0.45f, b + Vector3.down * 0.45f, dir, 0.7f);
                }
                // skirting: the panel's edges along the outline (not on its square's sides)
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector2 qa = poly[i], qb = poly[(i + 1) % poly.Count];
                    bool onSquare = (Mathf.Abs(qa.x - qb.x) < 0.05f && (Mathf.Abs(qa.x - r.xMin) < 0.05f || Mathf.Abs(qa.x - r.xMax) < 0.05f))
                                 || (Mathf.Abs(qa.y - qb.y) < 0.05f && (Mathf.Abs(qa.y - r.yMin) < 0.05f || Mathf.Abs(qa.y - r.yMax) < 0.05f));
                    if (onSquare || (qa - qb).sqrMagnitude < 0.25f) continue;
                    Vector2 e = (qb - qa).normalized;
                    Vector3 inward = new(-e.y, 0f, e.x); // (the outline runs counter-clockwise: in is to the left)
                    Vector3 pa = new Vector3(qa.x, top, qa.y) + inward * 0.5f, pb = new Vector3(qb.x, top, qb.y) + inward * 0.5f;
                    Quad(trimA, pa, pb, pb + Vector3.up * 2.4f, pa + Vector3.up * 2.4f);
                }
            }
            force = false; faces = main;

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

            // a pointed crystal: a square foot round `at`, tapering to a point `len` along `dir`
            void Shard(Material m, Vector3 at, Vector3 dir, float len, float w)
            {
                dir.Normalize();
                Vector3 s = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized * (w * 0.5f);
                Vector3 u = Vector3.Cross(dir, s).normalized * (w * 0.5f), tip = at + dir * len;
                Vector3 a = at + s, b = at + u, c = at - s, d = at - u;
                Motif(at);
                Quad(m, a, b, tip, tip); Quad(m, b, c, tip, tip); Quad(m, c, d, tip, tip); Quad(m, d, a, tip, tip);
                whole = 0;
            }
            // how far a motif round c may reach along the wall without running past the corner
            // (past it the wall turns, and the motif would go on behind the next run)
            float Fit(Vector3 c, Vector3 along, float r)
            {
                Vector3 flat = c.WithY(0f);
                return Mathf.Min(r, Vector3.Dot(flat - runA, along) - 1f, Vector3.Dot(runB - flat, along) - 1f);
            }
            Vector3 OnWall(Vector3 c, Vector3 along, float ang, float r) => c + along * (Mathf.Cos(ang) * r) + Vector3.up * (Mathf.Sin(ang) * r);
            // an outline of light on the wall round c (a polygon of `sides`)
            void Ring(Material m, Vector3 c, Vector3 along, Vector3 inward, float r, int sides, float width, float turn = 0f)
            {
                r = Fit(c, along, r);
                if (r < 3f) return;
                width = Mathf.Min(width, r * 0.25f);
                Motif(c + inward.normalized);
                for (int k = 0; k < sides; k++)
                    Strip(m, OnWall(c, along, turn + k * 2f * Mathf.PI / sides, r), OnWall(c, along, turn + (k + 1) * 2f * Mathf.PI / sides, r), inward, width);
                whole = 0;
            }
            // a filled fan on the wall round c, from angle a0 to a1, `off` out from it
            void Fan(Material m, Vector3 c, Vector3 along, Vector3 inward, float r, float a0, float a1, int steps, float off)
            {
                Vector3 o = inward * off;
                r = Fit(c, along, r);
                if (r < 2f) return;
                Motif(c + inward.normalized);
                for (int k = 0; k < steps; k++)
                    Quad(m, OnWall(c, along, Mathf.Lerp(a0, a1, k / (float)steps), r) + o, OnWall(c, along, Mathf.Lerp(a0, a1, (k + 1) / (float)steps), r) + o, c + o, c + o);
                whole = 0;
            }
            // a flat rectangle on the wall, from along-offsets s0..s1 of p and heights y0..y1
            void Pane(Material m, Vector3 p, Vector3 along, Vector3 inward, float s0, float s1, float y0, float y1, float off)
            {
                Vector3 a = p + along * s0 + inward * off, b = p + along * s1 + inward * off;
                Quad(m, a.WithY(y0), b.WithY(y0), b.WithY(y1), a.WithY(y1));
            }
            // a picture in squares of light, its top row first, its middle at c
            void Pixels(Material m, Vector3 c, Vector3 along, Vector3 inward, string[] art, float px)
            {
                px = Mathf.Min(px, Fit(c, along, art[0].Length * px * 0.5f) * 2f / art[0].Length);
                if (px < 0.8f) return;
                float w = art[0].Length * px, tall = art.Length * px;
                Motif(c + inward.normalized);
                for (int r = 0; r < art.Length; r++)
                    for (int k = 0; k < art[r].Length; k++)
                        if (art[r][k] == '#')
                        {
                            float s0 = -w * 0.5f + k * px, y = c.y + tall * 0.5f - (r + 1) * px;
                            Pane(m, c, along, inward, s0 + 0.15f, s0 + px - 0.15f, y + 0.15f, y + px - 0.15f, 0.7f);
                        }
                whole = 0;
            }
            // a pixel monster of its own: random, mirrored, with two eyes
            string[] Monster()
            {
                var art = new string[7];
                for (int r = 0; r < 7; r++)
                {
                    var row = new char[7];
                    for (int k = 0; k < 4; k++)
                    {
                        bool on = r == 0 ? k == 1 || dr.NextDouble() < 0.3
                                : r == 2 ? k != 2
                                : r is 1 or 3 or 4 ? k > 0 || dr.NextDouble() < 0.5
                                : dr.NextDouble() < 0.55;
                        row[k] = row[6 - k] = on ? '#' : '.';
                    }
                    art[r] = new string(row);
                }
                return art;
            }

            // A zone's own dressing on one bay of wall (see Design); `round` is how far round the
            // room the bay starts
            void Themed(Vector3 f0, Vector3 f1, Vector3 along, Vector3 inward, float pw, int bay, int rows, float round)
            {
                float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg; // a Box at this yaw: x into the room, z along the wall
                Vector3 midc = (f0 + f1) * 0.5f;
                float F(float a, float b) => a + (float)dr.NextDouble() * (b - a);
                float top = v.openTop ? v.ceiling - 4f : v.ceiling - 6f;
                // halls run hundreds of metres high and you see their walls from a hundred
                // metres off or more: the motifs grow with the hall, the big ones spread over
                // several bays
                float S = Mathf.Clamp(height / 140f, 1f, 4f);
                int every = Mathf.Max(1, Mathf.RoundToInt(S));
                bool motif = bay % every == 0;
                switch (design)
                {
                    case Design.Crystal:
                    {
                        // a cluster at the foot, now and then one up the wall, a vein of light
                        Vector3 foot = Vector3.Lerp(f0, f1, F(0.2f, 0.8f)).WithY(v.floor) + inward * 6f;
                        int n = 3 + dr.Next(4);
                        for (int k = 0; k < n; k++)
                            Shard(k % 2 == 0 ? lightA : kit.glow, foot + along * F(-6f, 6f), Vector3.up * 2f + inward * F(0f, 0.6f) + along * F(-0.9f, 0.9f), F(14f, 38f) * Mathf.Min(S, 2.5f), F(3f, 7f) * Mathf.Min(S, 2.5f));
                        if (dr.NextDouble() < 0.5)
                        {
                            Vector3 at = Vector3.Lerp(f0, f1, F(0.2f, 0.8f)).WithY(v.floor + height * F(0.3f, 0.8f)) + inward * 6f;
                            for (int k = 0; k < 3; k++)
                                Shard(k == 0 ? kit.glow : lightA, at, inward * 2f + Vector3.up * F(-1f, 1f) + along * F(-1f, 1f), F(8f, 20f) * Mathf.Min(S, 1.6f), F(2f, 4.5f) * Mathf.Min(S, 2f));
                        }
                        float vy = v.floor + height * F(0.15f, 0.85f);
                        Strip(kit.glow, f0.WithY(vy) + inward * 6.5f, f1.WithY(vy + F(-12f, 12f)) + inward * 6.5f, inward, 0.8f * S);
                        break;
                    }
                    case Design.Castle:
                    {
                        // the wall walk and its battlements, arrow slits above and below it
                        float walk = v.floor + Mathf.Min(height * 0.45f, 60f);
                        Box(trimA, midc.WithY(walk - 1.5f) + inward * 3f, new Vector3(6f, 3f, pw), yaw);
                        Strip(kit.glow, f0.WithY(walk - 3.4f) + inward * 5.5f, f1.WithY(walk - 3.4f) + inward * 5.5f, inward, 0.6f);
                        for (int k = 0; k < 3; k++)
                            Box(trimA, Vector3.Lerp(f0, f1, (k + 0.5f) / 3f).WithY(walk + 2.5f) + inward * 5.4f, new Vector3(1.2f, 5f, pw / 6f), yaw);
                        foreach (float ys in new[] { v.floor + 14f, walk + 10f })
                            for (int k = 0; k < 2; k++)
                                Pane(lightA, Vector3.Lerp(f0, f1, (k + 0.5f) / 2f), along, inward, -0.8f * S, 0.8f * S, ys, ys + 7f * S, 0.4f);
                        if (bay % 4 == 0)
                        {
                            // a tower, a torch on it and its own battlements up top
                            Box(wall, f0.WithY(v.floor + height * 0.5f) + inward * 5f, new Vector3(10f, height, 12f), yaw);
                            Box(kit.glow, f0.WithY(walk + 6f) + inward * 10.6f, new Vector3(1f, 2.2f, 1f), yaw);
                            if (v.openTop)
                                for (int k = -1; k <= 1; k += 2)
                                    Box(wall, (f0 + along * (k * 4f)).WithY(v.ceiling + 2.5f) + inward * 5f, new Vector3(10f, 5f, 3f), yaw);
                        }
                        else if (bay % 4 == 2)
                        {
                            // a banner with a swallowtail
                            Vector3 b = midc + inward * 0.9f;
                            float y1 = top, y0 = Mathf.Max(walk + 8f, y1 - 26f * S);
                            Pane(lightA, b, along, Vector3.zero, -3.5f, 3.5f, y0, y1, 0f);
                            Quad(lightA, (b - along * 3.5f).WithY(y0), (b - along * 3.5f).WithY(y0 - 5f), b.WithY(y0), b.WithY(y0));
                            Quad(lightA, b.WithY(y0), (b + along * 3.5f).WithY(y0 - 5f), (b + along * 3.5f).WithY(y0), (b + along * 3.5f).WithY(y0));
                        }
                        if (v.openTop)
                            for (int k = 0; k < 3; k++) // the wall's own battlements, against the sky
                                Box(wall, Vector3.Lerp(f0, f1, (k + 0.5f) / 3f).WithY(v.ceiling + 2.5f) + inward * 1.5f, new Vector3(3f, 5f, pw / 6f), yaw);
                        break;
                    }
                    case Design.Hex:
                    case Design.HexVines:
                    {
                        // a honeycomb of hexagons, a row to each panel, every other row shifted half a bay
                        // (hexagons as wide as `every` bays, a row of them every `every` panels up)
                        if (motif)
                            for (int r = 0, n = 0; r < rows; r += every, n++)
                            {
                                Vector3 c = (f0 + along * (pw * every * (n % 2 == 0 ? 0.5f : 1f))).WithY(Mathf.Lerp(v.floor, v.ceiling, (r + every * 0.5f) / rows));
                                int pick = bay / every + n;
                                Ring(pick % 3 == 0 ? lightA : kit.glow, c, along, inward, 11f * S, 6, 1.2f * S, Mathf.PI / 6f);
                                if (pick % 3 == 1) Fan(trimA, c, along, inward, 9.5f * S, 0f, 2f * Mathf.PI, 6, 0.4f);
                            }
                        if (design == Design.HexVines)
                            for (int k = 0; k < 1; k++)
                            {
                                // vines hanging from the roof, leaves along them
                                Vector3 at = Vector3.Lerp(f0, f1, F(0.1f, 0.9f)) + inward * 1.6f;
                                float y1 = top, y0 = y1 - height * F(0.2f, 0.6f);
                                Strip(trimA, at.WithY(y0), at.WithY(y1), inward, 0.9f * S);
                                for (float y = y0 + 2f; y < y1; y += F(4f, 7f) * S)
                                    Box(wall, at.WithY(y) + inward * 1.2f + along * F(-1.2f, 1.2f) * S, new Vector3(1.2f, F(1.5f, 2.5f) * S, F(2f, 3.2f) * S), yaw + F(-30f, 30f));
                            }
                        break;
                    }
                    case Design.Pixel:
                    {
                        // a monster of its own in every bay, a row of dots along the foot
                        if (motif)
                            foreach (float band in new[] { F(0.15f, 0.45f), F(0.55f, 0.85f) })
                                Pixels(dr.NextDouble() < 0.5 ? kit.glow : lightA, midc.WithY(v.floor + height * band), along, inward, Monster(), 2.4f * S);
                        for (int k = 0; k < 5; k++)
                            Pane(kit.glow, Vector3.Lerp(f0, f1, (k + 0.5f) / 5f), along, inward, -0.9f, 0.9f, v.floor + 8f, v.floor + 8f + 1.8f * S, 0.7f);
                        break;
                    }
                    case Design.Hearts:
                    {
                        // ribs of light round the tunnel, a heart here and there
                        Strip(lightA, f0.WithY(v.floor), f0.WithY(v.ceiling), inward, 1.6f * S);
                        if (bay % (2 * every) == every)
                            Pixels(bay % (4 * every) == every ? kit.glow : lightA, midc.WithY(v.floor + height * F(0.25f, 0.75f)), along, inward, Heart, 2.4f * S);
                        Strip(trimA, f0.WithY(v.floor + 10f), f1.WithY(v.floor + 10f), inward, 3f);
                        break;
                    }
                    case Design.Waves:
                    {
                        // three waves of light rolling round the room, unbroken from wall to wall
                        for (int w = 0; w < 3; w++)
                        {
                            float baseY = v.floor + height * (0.25f + 0.22f * w), amp = (7f + w * 2f) * S, wl = 110f + w * 25f, ph = w * 1.7f;
                            for (int k = 0; k < 6; k++)
                            {
                                float s0 = round + pw * k / 6f, s1 = round + pw * (k + 1) / 6f;
                                Strip(w == 1 ? lightA : kit.glow, Vector3.Lerp(f0, f1, k / 6f).WithY(baseY + amp * Mathf.Sin(s0 * 2f * Mathf.PI / wl + ph)),
                                    Vector3.Lerp(f0, f1, (k + 1) / 6f).WithY(baseY + amp * Mathf.Sin(s1 * 2f * Mathf.PI / wl + ph)), inward, (w == 1 ? 2.6f : 1.4f) * S);
                            }
                        }
                        break;
                    }
                    case Design.Retro:
                    {
                        // the stripes of a setting sun, further apart as they go down, a neon triangle every few bays
                        float y = v.floor + height * 0.5f, gap = 1.5f * S;
                        for (int k = 0; k < 8 && y > v.floor + 4f; k++, y -= gap + 1.4f * S, gap *= 1.35f)
                            Strip(kit.glow, f0.WithY(y), f1.WithY(y), inward, 1.4f * S);
                        if (bay % (3 * every) == 0)
                            Ring(lightA, midc.WithY(v.floor + height * F(0.62f, 0.8f)), along, inward, 13f * S, 3, 1.5f * S, Mathf.PI / 2f);
                        break;
                    }
                    case Design.Warehouse:
                    {
                        // a steel column, a tall window of many panes up high, crates at the foot
                        Material frame = kit.slab ? kit.slab : trimA;
                        Box(trimA, f0.WithY(v.floor + height * 0.5f) + inward * 1.5f, new Vector3(2.6f, height, 2.6f), yaw);
                        float ya = v.floor + height * 0.42f, yb = Mathf.Min(ya + 42f, v.ceiling - 8f);
                        if (yb - ya > 12f)
                        {
                            float s0 = pw * 0.18f, s1 = pw * 0.82f;
                            Pane(lightA, f0, along, inward, s0, s1, ya, yb, 0.4f);
                            for (int k = 0; k <= 3; k++)
                                Strip(frame, (f0 + along * Mathf.Lerp(s0, s1, k / 3f)).WithY(ya), (f0 + along * Mathf.Lerp(s0, s1, k / 3f)).WithY(yb), inward * 1.6f, 0.9f);
                            for (int k = 0; k <= 4; k++)
                                Strip(frame, (f0 + along * s0).WithY(Mathf.Lerp(ya, yb, k / 4f)), (f0 + along * s1).WithY(Mathf.Lerp(ya, yb, k / 4f)), inward * 1.6f, 0.9f);
                        }
                        int crates = dr.Next(4);
                        float cy = v.floor;
                        for (int k = 0; k < crates; k++)
                        {
                            float sz = F(5f, 8f);
                            Box(frame, Vector3.Lerp(f0, f1, F(0.3f, 0.7f)).WithY(cy + sz * 0.5f) + inward * (sz * 0.5f + 0.8f), new Vector3(sz, sz, sz), yaw + F(-15f, 15f));
                            cy += sz;
                        }
                        break;
                    }
                    case Design.Barn:
                    {
                        // planks with battens, cross bracing down low, a rail, lanterns
                        for (int k = 0; k < 4; k++)
                            Strip(trimA, Vector3.Lerp(f0, f1, k / 4f).WithY(v.floor), Vector3.Lerp(f0, f1, k / 4f).WithY(v.ceiling), inward, 1.1f);
                        float yb = v.floor + Mathf.Min(30f, height * 0.4f);
                        Strip(trimA, f0.WithY(v.floor + 1f), f1.WithY(yb), inward * 1.6f, 1.6f);
                        Strip(trimA, f0.WithY(yb), f1.WithY(v.floor + 1f), inward * 1.6f, 1.6f);
                        Strip(trimA, f0.WithY(yb), f1.WithY(yb), inward * 1.6f, 2f);
                        if (bay % 2 == 0)
                            Box(kit.glow, f0.WithY(yb + 6f) + inward * 1.6f, new Vector3(1.2f, 2f, 1.2f), yaw);
                        break;
                    }
                    case Design.Stadium:
                    {
                        // stepped stands round the foot, a checkered band above, floodlights
                        for (int k = 0; k < 4; k++)
                        {
                            float st = Mathf.Min(S, 2f), hk = (k + 1) * 3f * st, inn = (12f - 3f * k) * st;
                            Box(k % 2 == 0 ? trimA : wall, midc.WithY(v.floor + hk * 0.5f) + inward * (inn - 1.5f * st), new Vector3(3f * st, hk, pw), yaw);
                            Strip(lightA, f0.WithY(v.floor + hk + 0.2f) + inward * (inn - 0.3f), f1.WithY(v.floor + hk + 0.2f) + inward * (inn - 0.3f), Vector3.up, 0.5f);
                        }
                        // the tiers of the stand, each with its checkered band and its rail of light
                        float sq = pw / 6f;
                        foreach (float at in new[] { 0.22f, 0.45f, 0.68f })
                        {
                            float yb = v.floor + height * at;
                            Box(trimA, midc.WithY(yb - 2f * S) + inward * (3f * S), new Vector3(6f * S, 4f * S, pw), yaw);
                            for (int k = 0; k < 6; k++)
                                for (int r = 0; r < 2; r++)
                                    Pane((k + r) % 2 == 0 ? kit.glow : wall, Vector3.Lerp(f0, f1, k / 6f), along, inward, 0f, sq, yb + r * sq, yb + (r + 1) * sq, 0.6f);
                            Strip(lightA, f0.WithY(yb - 4f * S) + inward * (6f * S), f1.WithY(yb - 4f * S) + inward * (6f * S), inward, 1f * S);
                        }
                        if (bay % 3 == 0) // floodlights along the top
                        {
                            Box(trimA, f0.WithY(top - 10f * S) + inward * (3f * S), new Vector3(6f * S, 1.2f * S, 1.2f * S), yaw);
                            Box(lightA, f0.WithY(top - 8f * S) + inward * (6f * S), new Vector3(2f, 5f, 10f) * S, yaw);
                        }
                        break;
                    }
                    case Design.Streaks:
                    {
                        // streaks of light at speed
                        for (int k = 0; k < 6; k++)
                        {
                            float a = F(0f, 0.6f), b = Mathf.Min(1f, a + F(0.25f, 0.9f)), y = v.floor + F(6f, height - 6f);
                            Strip(k % 2 == 0 ? kit.glow : lightA, Vector3.Lerp(f0, f1, a).WithY(y), Vector3.Lerp(f0, f1, b).WithY(y), inward, F(0.6f, 2.4f) * S);
                        }
                        break;
                    }
                    case Design.Lab:
                    {
                        // the red line round the room, rising and falling like a circuit, a green one under it
                        float y = v.floor + Mathf.Min(height * 0.3f, 40f);
                        Strip(kit.glow, f0.WithY(y), f1.WithY(y), inward, 2.4f * S);
                        Strip(lightA, f0.WithY(y - 4f * S), f1.WithY(y - 4f * S), inward, 0.8f * S);
                        if (bay % 3 == 0)
                        {
                            Strip(kit.glow, midc.WithY(y), midc.WithY(v.ceiling), inward, 2.4f * S);
                            Fan(kit.glow, midc.WithY(y), along, inward, 2.8f * S, 0f, 2f * Mathf.PI, 8, 0.8f);
                        }
                        if (bay % 5 == 2) Strip(kit.glow, midc.WithY(v.floor), midc.WithY(y), inward, 2.4f * S);
                        break;
                    }
                    case Design.Bunker:
                    {
                        // buttresses, a slit window high up, caged lamps, the joints of the pour
                        if (bay % 2 == 0)
                        {
                            Box(wall, f0.WithY(v.floor + height * 0.25f) + inward * 3f, new Vector3(6f, height * 0.5f, 5f), yaw);
                            Box(wall, f0.WithY(v.floor + height * 0.75f) + inward * 2f, new Vector3(4f, height * 0.5f, 4f), yaw);
                        }
                        for (float y = v.floor + 60f * S; y < v.ceiling - 20f * S; y += 90f * S)
                            Pane(lightA, f0, along, inward, pw * 0.2f, pw * 0.8f, y, y + 2.5f * S, 0.4f);
                        for (float y = v.floor + 12f; y < v.ceiling - 10f; y += 45f * S)
                        {
                            Box(kit.glow, midc.WithY(y) + inward * 0.9f * S, new Vector3(1.6f, 1.6f, 1.6f) * S, yaw);
                            Box(trimA, midc.WithY(y + 1.1f * S) + inward * 0.9f * S, new Vector3(2f, 0.4f, 2f) * S, yaw);
                        }
                        for (float y = v.floor + 20f * S; y < v.ceiling - 20f; y += 20f * S)
                            Strip(trimA, f0.WithY(y), f1.WithY(y), inward * 0.5f, 0.5f);
                        break;
                    }
                    case Design.Shapes:
                    {
                        // a great neon shape in every bay
                        if (!motif) break;
                        int pick = bay / every;
                        float w = 1.4f * S;
                        foreach (float band in new[] { F(0.15f, 0.45f), F(0.55f, 0.85f) })
                        {
                            Vector3 c = midc.WithY(v.floor + height * band);
                            Material m = pick % 2 == 0 ? kit.glow : lightA;
                            switch (pick++ % 4)
                            {
                                case 0: Ring(m, c, along, inward, 11f * S, 20, w); break;
                                case 1: Ring(m, c, along, inward, 12f * S, 3, w, Mathf.PI / 2f); break;
                                case 2: Ring(m, c, along, inward, 11f * S, 4, w, Mathf.PI / 4f); break;
                                default: Ring(m, c, along, inward, 12f * S, 20, w); Ring(kit.glow, c, along, inward, 6.5f * S, 20, w); break;
                            }
                        }
                        break;
                    }
                    case Design.Arches:
                    {
                        // an arcade: a pilaster each side, a round arch between, a cornice over all
                        // (tier upon tier of them up the whole wall, like the galleries round a great court)
                        float r = pw * 0.5f - 2f, tier = Mathf.Clamp(height / 6f, r * 2.6f + 12f, 110f);
                        for (float y0 = v.floor; y0 + tier <= v.ceiling - 4f; y0 += tier)
                        {
                            float spring = y0 + tier - r - 9f;
                            Box(trimA, f0.WithY((y0 + spring) * 0.5f) + inward * 1.5f, new Vector3(3f, spring - y0, 4f), yaw);
                            Vector3 c = midc.WithY(spring);
                            for (int k = 0; k < 10; k++)
                                Strip(trimA, OnWall(c, along, Mathf.PI * k / 10f, r), OnWall(c, along, Mathf.PI * (k + 1) / 10f, r), inward * 2.4f, 2.6f);
                            Pane(lightA, f0, along, inward, pw * 0.5f - r + 1.5f, pw * 0.5f + r - 1.5f, y0 + 3f, spring, 0.5f);
                            Fan(lightA, c, along, inward, r - 1.5f, 0f, Mathf.PI, 10, 0.5f);
                            Box(trimA, midc.WithY(y0 + tier - 3f) + inward * 2f, new Vector3(4f, 3f, pw), yaw); // the gallery's floor
                            Strip(kit.glow, f0.WithY(y0 + tier - 5f) + inward * 3.5f, f1.WithY(y0 + tier - 5f) + inward * 3.5f, inward, 1f);
                        }
                        break;
                    }
                    case Design.Stepped:
                    {
                        // ledges stepping out further the lower they are, bands of colour between
                        int k = 0;
                        for (float y = v.floor + 14f * S; y < v.ceiling - 6f; y += 14f * S, k++)
                        {
                            float depth = 2f + 7f * (1f - (y - v.floor) / height);
                            Box(k % 2 == 0 ? trimA : wall, midc.WithY(y) + inward * (depth * S * 0.5f), new Vector3(depth * S, 2.5f * S, pw), yaw);
                            Strip(k % 2 == 0 ? wall : trimA, f0.WithY(y - 6f * S), f1.WithY(y - 6f * S), inward, 5f * S);
                        }
                        break;
                    }
                    case Design.Nautical:
                    {
                        // portholes, a lifebuoy now and then, a wave band along the foot
                        if (motif)
                            foreach (float y in new[] { v.floor + height * 0.35f, v.floor + height * 0.65f })
                            {
                                Vector3 c = midc.WithY(y);
                                Ring(trimA, c, along, inward, 4.4f * S, 8, 1.4f * S, Mathf.PI / 8f);
                                Fan(lightA, c, along, inward, 3.8f * S, 0f, 2f * Mathf.PI, 8, 0.4f);
                            }
                        if (bay % (4 * every) == every)
                            Ring(kit.glow, midc.WithY(v.floor + height * 0.18f), along, inward, 3.6f * S, 12, 2.2f * S);
                        for (int k = 0; k < 6; k++)
                        {
                            float s0 = round + pw * k / 6f, s1 = round + pw * (k + 1) / 6f;
                            Strip(trimA, Vector3.Lerp(f0, f1, k / 6f).WithY(v.floor + 6f * S + 2.5f * S * Mathf.Sin(s0 * 0.12f / S)), Vector3.Lerp(f0, f1, (k + 1) / 6f).WithY(v.floor + 6f * S + 2.5f * S * Mathf.Sin(s1 * 0.12f / S)), inward, 3f * S);
                        }
                        break;
                    }
                    case Design.City:
                    {
                        // two buildings to a bay, their windows lit floor by floor
                        for (int b = 0; b < 2; b++)
                        {
                            float s0 = pw * b * 0.5f + 0.8f, s1 = pw * (b + 1) * 0.5f - 0.8f, bh = height * F(0.3f, 0.85f), depth = F(3f, 10f);
                            Vector3 foot = f0 + along * ((s0 + s1) * 0.5f);
                            Box(b % 2 == 0 ? wall : trimA, foot.WithY(v.floor + bh * 0.5f) + inward * (depth * 0.5f), new Vector3(depth, bh, s1 - s0), yaw);
                            for (float y = v.floor + 6f * S; y < v.floor + bh - 4f * S; y += 6f * S)
                                for (int k = 0; k < 3; k++)
                                    if (dr.NextDouble() < 0.55)
                                        Strip(dr.NextDouble() < 0.7 ? kit.glow : lightA, (f0 + along * Mathf.Lerp(s0 + 1f, s1 - 1f, k / 3f + 0.03f)).WithY(y) + inward * depth,
                                            (f0 + along * Mathf.Lerp(s0 + 1f, s1 - 1f, (k + 1) / 3f - 0.03f)).WithY(y) + inward * depth, inward, 2f * S);
                            Box(lightA, foot.WithY(v.floor + bh + 3f) + inward * (depth * 0.5f), new Vector3(0.6f, 6f, 0.6f), yaw); // a mast
                        }
                        break;
                    }
                    case Design.Library:
                    {
                        // shelves up the walls, every shelf full of books
                        float shelfTop = v.floor + height * 0.45f;
                        Material[] spines = { wall, trimA, kit.slab ? kit.slab : wall, kit.floor ? kit.floor : trimA };
                        for (float y = v.floor + 2f; y < shelfTop; y += 5f * S)
                        {
                            Box(trimA, midc.WithY(y) + inward * 1.5f, new Vector3(3f, 0.6f * S, pw), yaw);
                            for (float s = 0.5f; s < pw - 0.6f;)
                            {
                                float w = Mathf.Min(F(1.8f, 4.5f) * Mathf.Sqrt(S), pw - 0.5f - s);
                                Pane(spines[dr.Next(spines.Length)], f0, along, inward, s + 0.1f, s + w - 0.1f, y + 0.3f * S, y + F(3f, 4.4f) * S, 1.4f);
                                s += w;
                            }
                        }
                        Strip(kit.glow, f0.WithY(shelfTop + 2f * S), f1.WithY(shelfTop + 2f * S), inward * 2f, 1.2f * S);
                        break;
                    }
                    case Design.Mine:
                    {
                        // timber frames propping the rock, lanterns hung on them
                        // (level upon level of them, the way a mine goes down)
                        if (bay % 2 == 0)
                        {
                            float fh = 40f * Mathf.Sqrt(S);
                            for (float y0 = v.floor; y0 + fh < v.ceiling - 6f; y0 += fh + 12f * S)
                            {
                                foreach (float s in new[] { 2f, pw - 2f })
                                {
                                    Box(trimA, (f0 + along * s).WithY(y0 + fh * 0.5f) + inward * 8f, new Vector3(2.2f * Mathf.Sqrt(S), fh, 2.2f * Mathf.Sqrt(S)), yaw);
                                    Box(kit.glow, (f0 + along * s).WithY(y0 + fh - 8f) + inward * 10f, new Vector3(1.2f, 1.8f, 1.2f) * Mathf.Sqrt(S), yaw);
                                }
                                Box(trimA, midc.WithY(y0 + fh) + inward * 8f, new Vector3(2.6f, 2.6f, pw) * 1f, yaw);
                                Box(trimA, midc.WithY(y0 + fh + 1.6f) + inward * 9f, new Vector3(9f, 0.8f, pw), yaw); // the level's boards
                            }
                        }
                        break;
                    }
                    case Design.Crater:
                    {
                        // craters pocking the walls, boulders round the foot
                        int n = dr.Next(2, 5);
                        for (int k = 0; k < n; k++)
                        {
                            Vector3 c = Vector3.Lerp(f0, f1, F(0.25f, 0.75f)).WithY(v.floor + height * F(0.15f, 0.85f));
                            float r = F(4f, 10f) * S;
                            Ring(trimA, c, along, inward * 1.5f, r, 12, 1.8f * S);
                            Fan(kit.floor ? kit.floor : trimA, c, along, inward, r - 0.6f, 0f, 2f * Mathf.PI, 12, 0.4f);
                            if (dr.NextDouble() < 0.3) Ring(kit.glow, c, along, inward * 2f, r * 0.4f, 12, 0.6f * S);
                        }
                        if (dr.NextDouble() < 0.6)
                        {
                            float sz = F(5f, 14f) * S;
                            Box(wall, Vector3.Lerp(f0, f1, F(0.2f, 0.8f)).WithY(v.floor + sz * 0.35f) + inward * (sz * 0.5f + 1f), new Vector3(sz, sz * 0.7f, sz * F(0.8f, 1.3f)), F(0f, 90f));
                        }
                        break;
                    }
                    case Design.Furnace:
                    {
                        // a furnace mouth glowing at the foot of every bay, chimney ducts between, a band of grating
                        float s0 = pw * 0.28f, s1 = pw * 0.72f, ym = v.floor + 12f * S, half = (s1 - s0) * 0.5f;
                        Pane(lightA, f0, along, inward, s0, s1, v.floor + 2f, ym, 0.4f);
                        Fan(lightA, (f0 + along * (s0 + half)).WithY(ym), along, inward, half, 0f, Mathf.PI, 8, 0.4f);
                        Strip(trimA, (f0 + along * (s0 - 1f)).WithY(v.floor + 2f), (f0 + along * (s0 - 1f)).WithY(ym), inward * 1.6f, 2f);
                        Strip(trimA, (f0 + along * (s1 + 1f)).WithY(v.floor + 2f), (f0 + along * (s1 + 1f)).WithY(ym), inward * 1.6f, 2f);
                        Strip(trimA, (f0 + along * (s0 - 2f)).WithY(ym + half + 2f), (f0 + along * (s1 + 2f)).WithY(ym + half + 2f), inward * 1.6f, 2.5f);
                        if (bay % 2 == 0)
                        {
                            Box(trimA, f0.WithY(v.floor + height * 0.5f) + inward * 2.5f, new Vector3(4.5f, height, 4.5f), yaw);
                            for (float y = v.floor + 25f * S; y < v.ceiling - 5f; y += 25f * S)
                                Box(kit.glow, f0.WithY(y) + inward * 2.5f, new Vector3(5.2f, 1f * S, 5.2f), yaw);
                        }
                        Strip(trimA, f0.WithY(v.floor + height * 0.5f), f1.WithY(v.floor + height * 0.5f), inward, 5f);
                        break;
                    }
                    case Design.Banners:
                    {
                        // a long flag down the wall in every bay, a stone band along the foot
                        Vector3 b = midc + inward * 0.9f;
                        float y1 = top, y0 = Mathf.Max(v.floor + 20f, y1 - 34f * S), bw = Mathf.Min(4f * S, pw * 0.4f);
                        Material m = bay % 2 == 0 ? kit.glow : lightA;
                        Pane(m, b, along, Vector3.zero, -bw, bw, y0, y1, 0f);
                        Quad(m, (b - along * bw).WithY(y0), (b - along * bw).WithY(y0 - 1.5f * bw), b.WithY(y0), b.WithY(y0));
                        Quad(m, b.WithY(y0), (b + along * bw).WithY(y0 - 1.5f * bw), (b + along * bw).WithY(y0), (b + along * bw).WithY(y0));
                        Box(trimA, b.WithY(y1 + 0.6f), new Vector3(1f, 1f, bw * 2.5f), yaw);
                        Strip(trimA, f0.WithY(v.floor + 6f), f1.WithY(v.floor + 6f), inward, 6f);
                        break;
                    }
                }
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
                        float w = R(6f, 10f), foot = v.Terrace(at.x, at.z);
                        Box(wall, at.WithY((foot + v.ceiling) * 0.5f), new Vector3(w, v.ceiling - foot, w), 0f);
                        for (float y = foot + 30f; y < v.ceiling - 10f; y += 45f)
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
                    float y = v.Terrace(at.x, at.z), size = R(14f, 26f), yaw = R(0f, 90f);
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
            Emit(main, go.transform);
            var floorGo = new GameObject("Floor");
            floorGo.transform.SetParent(go.transform, false);
            Emit(floorFaces, floorGo.transform);
            for (int k = 0; k < dividerFaces.Count; k++)
            {
                var dg = new GameObject($"Divider {k}");
                dg.transform.SetParent(go.transform, false);
                Emit(dividerFaces[k], dg.transform);
                var plane = dg.AddComponent<DividerPlane>();
                (plane.point, plane.normal, plane.window, plane.mouth, plane.holeY, plane.holeW, plane.holeH) = dividerPlanes[k];
            }
            return go;

            void Emit(Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<int> t)> group, Transform into)
            {
            foreach (var (m, f) in group)
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
                part.transform.SetParent(into, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = part.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = m != kit.glow && m != kit.glowAlt; // the walls catch the ramps' shadows
            }
            }
        }

        // A superellipse's measure (below 1 inside a rounded-square hole of half size hw x hh)
        static float Super(float dx, float dy, float hw, float hh)
        {
            float x = dx / hw, y = dy / hh;
            x *= x; y *= y;
            return x * x + y * y;
        }

        // A square of the floor cut to the outline (convex, counter-clockwise): its corners,
        // counter-clockwise, empty if it lies outside
        static List<Vector2> Clip(List<Vector2> o, float x0, float z0, float x1, float z1)
        {
            var poly = new List<Vector2> { new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1) };
            for (int i = 0; i < o.Count && poly.Count > 0; i++)
            {
                Vector2 a = o[i], b = o[(i + 1) % o.Count];
                float Side(Vector2 q) => (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x);
                var next = new List<Vector2>();
                for (int k = 0; k < poly.Count; k++)
                {
                    Vector2 p = poly[k], q = poly[(k + 1) % poly.Count];
                    float sp = Side(p), sq = Side(q);
                    if (sp >= 0f) next.Add(p);
                    if ((sp >= 0f) != (sq >= 0f)) next.Add(Vector2.Lerp(p, q, sp / (sp - sq)));
                }
                poly = next;
            }
            return poly;
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
