using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow
{
    // The buildings the course runs through. Most zones are one continuous interior, the way
    // the most beautiful surf maps feel: the building wraps each ramp end to end and carries on
    // through the flight to the next one, so rooms flow into rooms and you never drop out into
    // empty air. Its gaps are deliberate ones: windows onto the sky, light slots, a doorway
    // where one zone gives way to the next. The Sky Palace and Spectrum stay open, as the
    // breaths of fresh air between interiors (and twin and canyon ramps stay open vistas).
    //
    // Zones (all original designs, after kinds of spaces from the maps people call the most
    // beautiful):
    //  - Cathedral (Crimson Hall): arched ribs, banded walls, a vaulted roof with spikes,
    //    chains, a red line along the floor, tall narrow windows
    //  - Palace (Sky Palace): white colonnades open to the sky, gold top lines, clouds below
    //  - Rings (Neon Rings): a black void with glowing rings to fly through
    //  - Grotto: a rock cave over a glowing lake, mushrooms, crystals, stalactites
    //  - Candy (Candy Blocks): lavender rooms and doorways, floating colored blocks outside
    //  - Forge: heavy orange frames, glowing slits, a lava grid below
    //  - Wire (Wireframe): a tunnel drawn in glowing edges
    //  - Gallery (White Gallery): white rooms with pillars, tall windows onto the sky, warm
    //    sconces, cool light pooling along the floor
    //  - Sunset (Sunset Rooms): salmon rooms over a dark hexagon floor, a gridded ceiling,
    //    peach light slots, and great windows framed in glowing orange onto a sunset
    //
    // Nothing here has colliders (the ramps are the only things you touch). Every piece of one
    // material is merged into a single mesh with world-scale texture coordinates, so a whole
    // building costs a handful of draw calls, and it belongs to its ramp, so everything behind
    // you de-renders.
    public static class Architecture
    {
        // One cross-section of the building: the level you ride at, the two wall lines and
        // the floor and roof heights
        public struct Frame
        {
            public Vector3 p, f, right, A, B;
            public float level, top, bottom;
        }

        public static bool Continuous(SceneryStyle s) => s is not (SceneryStyle.Palace or SceneryStyle.Spectrum
            or SceneryStyle.Alpine or SceneryStyle.Glass or SceneryStyle.Ember or SceneryStyle.Amethyst or SceneryStyle.Toy);

        // Enclosed zones with no roof: walls either side, the sky overhead
        public static bool OpenTop(SceneryStyle s) => s is SceneryStyle.Canyon or SceneryStyle.Garden;

        public static float StepFor(SceneryStyle s) => s switch
        {
            SceneryStyle.Candy => 10f,
            SceneryStyle.Grotto => 9f,
            _ => 12f,
        };

        const float Roof = 64f, Margin = 85f; // roomy: walls 85m out from the ramp and a high roof, so every space reads big and distant

        // How far the floor lies below the ramp: the rooms after Raphaelo have theirs close by
        // (you see it), the rest drop away into depth
        public static float DepthFor(SceneryStyle s) => s is SceneryStyle.Sunset or SceneryStyle.Gallery or SceneryStyle.Library ? 16f
            : s is SceneryStyle.Garden or SceneryStyle.Lab or SceneryStyle.Temple or SceneryStyle.Canyon ? 18f : 40f;

        // Cross-sections along a ramp, between two distances along it
        public static List<Frame> RampFrames(RampShapes.RampPath path, float from, float to, float step, float depth = 24f, float extra = 0f)
        {
            var frames = new List<Frame>();
            bool prism = path.kind == RampShapes.Kind.Prism;
            // Walls stand clear of the whole face (a slab's face hangs on one side only)
            float s = prism ? 1f : path.side;
            float near = (prism ? path.width + Margin : Margin * 0.7f) + extra, far = path.width + Margin + extra;
            // Ribs every `step`, and always one exactly at the end, so the building meets the
            // next piece with no hole
            int count = Mathf.Max(1, Mathf.CeilToInt((to - from) / step - 0.01f));
            for (int n = 0; n <= count; n++)
            {
                float d = Mathf.Lerp(from, to, (float)n / count);
                int k = Index(path, Mathf.Min(d, path.Length));
                Vector3 p = path.ridge[k], right = path.right[k];
                // A is always the left wall and B the right, whichever way the ramp faces: if they
                // swapped with the face, the building joining two ramps across a flight would
                // sweep its walls right across the jump
                frames.Add(new Frame
                {
                    p = p, f = path.forward[k], right = right,
                    A = p - right * (s > 0f ? near : far), B = p + right * (s > 0f ? far : near),
                    level = p.y, top = p.y + Roof, bottom = p.y - path.Depth - depth,
                });
            }
            return frames;
        }

        // Cross-sections along a flight, joining the end of one building to the start of the
        // next: the walls sweep across from one ramp's to the other's, the floor and roof
        // follow the arc you fly (high enough for big air)
        public static List<Frame> FlightFrames(Frame from, Frame to, Func<float, float> arcY, float gap, float step)
        {
            var frames = new List<Frame> { from };
            int n = Mathf.Max(1, Mathf.CeilToInt(gap / step));
            Vector3 dir = to.p - from.p;
            dir.y = 0f;
            Vector3 f = dir.sqrMagnitude > 0.01f ? dir.normalized : from.f;
            Vector3 right = new(f.z, 0f, -f.x);
            for (int i = 1; i < n; i++)
            {
                float u = (float)i / n, level = arcY(u * gap);
                frames.Add(new Frame
                {
                    p = Vector3.Lerp(from.p, to.p, u).WithY(level), f = f, right = right,
                    A = Vector3.Lerp(from.A, to.A, u), B = Vector3.Lerp(from.B, to.B, u),
                    level = level,
                    top = Mathf.Max(Mathf.Lerp(from.top, to.top, u), level + 48f),
                    bottom = Mathf.Min(Mathf.Lerp(from.bottom, to.bottom, u), level - 22f),
                });
            }
            return frames;
        }

        // A solid wall right across the building with a rounded hole to jump through. `hole` is
        // the hole's centre, `facing` the way you fly through it, hw and hh its half width and
        // height. The wall runs out past the building's walls, floor and vault (hidden behind
        // them) so there's no way round it. Returns the wall; its solid parts get colliders.
        public static GameObject HoleWall(Frame fr, Vector3 hole, Vector3 facing, float hw, float hh, BiomeKit kit,
            Transform parent, List<Mesh> meshes, Mesh cube)
        {
            var b = new Batch(cube);
            Vector3 normal = facing.WithY(0f).normalized, across = new Vector3(normal.z, 0f, -normal.x);
            const float half = 1.5f; // half the wall's thickness
            const int sides = 48;
            float xa = Mathf.Min(Vector3.Dot(fr.A - hole, across), Vector3.Dot(fr.B - hole, across)) - 15f;
            float xb = Mathf.Max(Vector3.Dot(fr.A - hole, across), Vector3.Dot(fr.B - hole, across)) + 15f;
            float y0 = fr.bottom - hole.y - 10f, y1 = fr.level + (fr.top - fr.level) * 1.6f + 10f - hole.y;
            // The hole: a squircle, round-cornered but wide open
            Vector2 H(int i)
            {
                float t = i * Mathf.PI * 2f / sides, c = Mathf.Cos(t), s = Mathf.Sin(t);
                return new Vector2(hw * Mathf.Sign(c) * Mathf.Sqrt(Mathf.Abs(c)), hh * Mathf.Sign(s) * Mathf.Sqrt(Mathf.Abs(s)));
            }
            // Out from the centre through a hole point to the wall's edge
            Vector2 O(Vector2 h)
            {
                float tx = h.x > 0.001f ? xb / h.x : h.x < -0.001f ? xa / h.x : float.MaxValue;
                float ty = h.y > 0.001f ? y1 / h.y : h.y < -0.001f ? y0 / h.y : float.MaxValue;
                return h * Mathf.Min(tx, ty);
            }
            Vector3 W(Vector2 q, float z) => hole + across * q.x + Vector3.up * q.y + normal * z;
            for (int i = 0; i < sides; i++)
            {
                Vector2 h0 = H(i), h1 = H(i + 1), o0 = O(h0), o1 = O(h1);
                foreach (float z in new[] { -half, half })
                {
                    b.Quad(kit.scenery, W(h0, z), W(o0, z), W(o1, z), W(h1, z));
                    // A glowing rim round the hole on both faces, so you can read it from the ramp
                    Vector2 g0 = h0 + h0.normalized * 1.2f, g1 = h1 + h1.normalized * 1.2f;
                    float zg = z + Mathf.Sign(z) * 0.06f;
                    b.Quad(kit.glow, W(h0, zg), W(g0, zg), W(g1, zg), W(h1, zg));
                }
                b.Quad(kit.slab, W(h0, -half), W(h1, -half), W(h1, half), W(h0, half)); // inside the hole
            }
            var wall = new GameObject("HoleWall");
            wall.transform.SetParent(parent, false);
            b.Build(wall.transform, meshes, true);
            foreach (var mf in wall.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<MeshRenderer>().sharedMaterial != kit.glow)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return wall;
        }

        public static void Shift(ref Frame f, Vector3 delta)
        {
            f.p += delta;
            f.A += delta;
            f.B += delta;
            f.level += delta.y;
            f.top += delta.y;
            f.bottom += delta.y;
        }

        // ------------------------------------------------------------------ building

        class Batch
        {
            readonly Dictionary<Material, List<CombineInstance>> parts = new();
            readonly Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)> quads = new();
            readonly HashSet<Material> fixedUv = new(); // light: keeps its own 0..1 coordinates
            readonly Mesh cube;
            public Batch(Mesh cube) { this.cube = cube; }

            public void Box(Material m, Vector3 center, Vector3 size, Quaternion rotation)
            {
                if (!m || size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f) return;
                if (!parts.TryGetValue(m, out var list)) parts[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(center, rotation, size) });
            }

            // A four-cornered face (a, b, c, d in order round it), showing from both sides
            public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool uv = false)
            {
                if (!m) return;
                Vector3 normal = Vector3.Cross(b - a, d - a) + Vector3.Cross(d - c, b - c);
                if (normal.sqrMagnitude < 1e-6f) return;
                normal.Normalize();
                if (!quads.TryGetValue(m, out var q)) quads[m] = q = (new List<Vector3>(), new List<Vector3>(), new List<Vector2>(), new List<int>());
                if (uv) fixedUv.Add(m);
                foreach (float s in new[] { 1f, -1f })
                {
                    int i = q.v.Count;
                    q.v.AddRange(new[] { a, b, c, d });
                    for (int k = 0; k < 4; k++) q.n.Add(normal * s);
                    q.uv.AddRange(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                    if (s > 0f) q.t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
                    else q.t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                }
            }

            public void Build(Transform parent, List<Mesh> meshes, bool worldUv)
            {
                var all = new HashSet<Material>(parts.Keys);
                all.UnionWith(quads.Keys);
                foreach (var m in all)
                {
                    var list = parts.TryGetValue(m, out var l) ? new List<CombineInstance>(l) : new List<CombineInstance>();
                    Mesh shell = null;
                    if (quads.TryGetValue(m, out var q))
                    {
                        shell = new Mesh { indexFormat = IndexFormat.UInt32 };
                        shell.SetVertices(q.v);
                        shell.SetNormals(q.n);
                        shell.SetUVs(0, q.uv);
                        shell.SetTriangles(q.t, 0);
                        list.Add(new CombineInstance { mesh = shell, transform = Matrix4x4.identity });
                    }
                    var mesh = new Mesh { name = "Architecture", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(list.ToArray(), true, true);
                    if (shell) WeaponBuilder.Kill(shell);
                    if (worldUv && !fixedUv.Contains(m)) WorldUvs(mesh);
                    mesh.RecalculateBounds();
                    meshes.Add(mesh);
                    var go = new GameObject("Architecture");
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = m;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }

            // Textures tile at a fixed size in the world (every 4m), whatever the piece's size,
            // projected onto each face from its main direction
            static void WorldUvs(Mesh mesh)
            {
                var v = mesh.vertices;
                var n = mesh.normals;
                var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 a = new(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                    uv[i] = (a.y >= a.x && a.y >= a.z ? new Vector2(v[i].x, v[i].z)
                        : a.x >= a.z ? new Vector2(v[i].z, v[i].y) : new Vector2(v[i].x, v[i].y)) / 4f;
                }
                mesh.uv = uv;
            }
        }

        // Builds the zone's structure through the given cross-sections (course-local
        // coordinates). `doorway` marks the cross-section where this zone begins.
        // How high each zone's straight walls stand (a fraction of the roof height); the vault
        // above them is the zone's own shape
        static float WallTop(SceneryStyle s) => s switch
        {
            SceneryStyle.Cathedral => 0.5f,
            SceneryStyle.Sunset => 0.45f,
            SceneryStyle.Gallery => 1f,
            SceneryStyle.Candy => 0.35f,
            SceneryStyle.Forge => 0.55f,
            SceneryStyle.Rings => 0.05f,
            SceneryStyle.Grotto => 0.25f,
            SceneryStyle.Library => 0.72f,
            SceneryStyle.Canyon => 1f,
            SceneryStyle.Garden => 0.45f,
            SceneryStyle.Lab or SceneryStyle.Synth => 0.55f,
            SceneryStyle.Mine => 0.25f,
            SceneryStyle.Temple => 0.35f,
            _ => 1f,
        };

        // The zone's cross-section above its walls, from the A wall's top (x = 0) over to the B
        // wall's top (x = 1), as (x across, y above the riding level). Every rib uses the same
        // number of points so the bays loft cleanly; `seed` varies the cave from rib to rib.
        static List<Vector2> Profile(SceneryStyle s, float roof, Vector3 at)
        {
            float wall = roof * WallTop(s);
            var pts = new List<Vector2>();
            switch (s)
            {
                case SceneryStyle.Cathedral: // a tall pointed gothic arch
                    for (int i = 0; i <= 12; i++)
                    {
                        float x = i / 12f, e = 1f - Mathf.Abs(2f * x - 1f);
                        pts.Add(new Vector2(x, wall + (roof * 1.35f - wall) * Mathf.Pow(e, 0.6f)));
                    }
                    break;
                case SceneryStyle.Sunset: // walls rolling round into a flat ceiling
                    for (int i = 0; i <= 14; i++)
                    {
                        float x = i / 14f, e = Mathf.Abs(2f * x - 1f);
                        float k = e < 0.5f ? 1f : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((e - 0.5f) / 0.5f, 2f)));
                        pts.Add(new Vector2(x, wall + (roof + 4f - wall) * k));
                    }
                    break;
                case SceneryStyle.Gallery: // a cornice stepping in to a coffered ceiling
                    pts.AddRange(new[] { new Vector2(0f, wall), new Vector2(0.03f, wall), new Vector2(0.03f, wall + 3f), new Vector2(0.97f, wall + 3f), new Vector2(0.97f, wall), new Vector2(1f, wall) });
                    break;
                case SceneryStyle.Candy or SceneryStyle.Temple: // stepped terraces climbing to the roof
                {
                    float h = (roof + 8f - wall) / 3f;
                    for (int n = 0; n < 3; n++) { pts.Add(new Vector2(n * 0.1f, wall + n * h)); pts.Add(new Vector2(n * 0.1f + 0.1f, wall + n * h)); }
                    pts.Add(new Vector2(0.3f, wall + 3f * h));
                    pts.Add(new Vector2(0.7f, wall + 3f * h));
                    for (int n = 2; n >= 0; n--) { pts.Add(new Vector2(1f - n * 0.1f - 0.1f, wall + n * h)); pts.Add(new Vector2(1f - n * 0.1f, wall + n * h)); }
                    break;
                }
                case SceneryStyle.Forge or SceneryStyle.Lab or SceneryStyle.Synth: // an octagon: chamfered up to a flat roof
                    pts.AddRange(new[] { new Vector2(0f, wall), new Vector2(0.22f, roof + 4f), new Vector2(0.78f, roof + 4f), new Vector2(1f, wall) });
                    break;
                case SceneryStyle.Rings: // a round tube
                    for (int i = 0; i <= 16; i++)
                    {
                        float x = i / 16f;
                        pts.Add(new Vector2(x, wall + (roof - wall) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(2f * x - 1f, 2f)))));
                    }
                    break;
                case SceneryStyle.Grotto or SceneryStyle.Mine: // a lumpy cave vault (the lumps follow the world, so ribs agree)
                    for (int i = 0; i <= 12; i++)
                    {
                        float x = i / 12f, k = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(2f * x - 1f, 2f)));
                        float lump = i == 0 || i == 12 ? 0f : (Mathf.PerlinNoise(at.x * 0.03f + i * 0.9f, at.z * 0.03f) - 0.5f) * roof * 0.5f;
                        pts.Add(new Vector2(x, wall + (roof * 1.1f - wall) * k + lump));
                    }
                    break;
                case SceneryStyle.Library: // a barrel vault over the bookshelves
                    for (int i = 0; i <= 12; i++)
                    {
                        float x = i / 12f;
                        pts.Add(new Vector2(x, wall + roof * 0.45f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(2f * x - 1f, 2f)))));
                    }
                    break;
                default:
                    pts.Add(new Vector2(0f, wall));
                    pts.Add(new Vector2(1f, wall));
                    break;
            }
            return pts;
        }

        static Vector3 ProfilePoint(Frame fr, Vector2 q) => Vector3.Lerp(fr.A, fr.B, q.x).WithY(fr.level + q.y);

        // Open zones: where the course runs near this ramp (its riding line, the flight in, the
        // ramp before). Scenery never stands within reach of it, and the ground lies below all
        // of it, so a ramp that loops back never runs into a tree, tower or the ground.
        public static readonly List<Vector3> KeepOut = new();
        public const string ScenePiece = "ScenePiece";
        const float KeepClear = 30f;

        static bool Clear(Vector3 p, float radius)
        {
            float r = radius + KeepClear;
            foreach (var k in KeepOut)
            {
                float dx = k.x - p.x, dz = k.z - p.z;
                if (dx * dx + dz * dz < r * r) return false;
            }
            return true;
        }

        static float GroundBelow(float fallback, float depth)
        {
            if (KeepOut.Count == 0) return fallback - depth;
            float low = float.MaxValue;
            foreach (var k in KeepOut) low = Mathf.Min(low, k.y);
            return Mathf.Min(low, fallback) - depth;
        }

        public static void Build(List<Frame> frames, Biome biome, BiomeKit kit, Transform parent, List<Mesh> meshes,
            System.Random rng, Mesh cube, int doorway = -1)
        {
            // Open-zone scenery is built piece by piece (a tree, a peak, a tower...), each its
            // own object, so a ramp that comes along later can clear any piece in its way
            var pieces = new List<Batch>();
            Batch Piece() { var nb = new Batch(cube); pieces.Add(nb); return nb; }

            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var b = new Batch(cube);
            Material floor = kit.floor ? kit.floor : kit.slab;
            var style = biome.style;
            float wallFrac = WallTop(style);

            for (int ribs = 0; ribs < frames.Count; ribs++)
            {
                var fr = frames[ribs];
                Vector3 f = fr.f, right = fr.right, A = fr.A, B = fr.B;
                float top = fr.top, bottom = fr.bottom, h = top - bottom, level = fr.level;
                var rot = Quaternion.LookRotation(f, Vector3.up);
                Vector3 mid = (A + B) * 0.5f;
                float span = Vector3.Distance(A.WithY(0f), B.WithY(0f));
                bool hasLast = ribs > 0;
                var last = hasLast ? frames[ribs - 1] : fr;
                Vector3 lastMid = (last.A + last.B) * 0.5f;
                float roof = top - level, lastRoof = last.top - last.level;

                // The same frames with their tops at the wall tops: walls, windows and mouldings
                // use these, and the zone's vault is lofted above them
                var wl = last; wl.top = last.level + lastRoof * wallFrac;
                var wf = fr; wf.top = fr.level + roof * wallFrac;
                float wallTop = roof * wallFrac;

                // Walls (with windows), vault and floor for this bay
                void Room(Material wall, Material ceiling, Material vaultWall, Material fl, bool winA, bool winB,
                    float lo, float hi, float u0, float u1, Material edge, float edgeWidth, Material mullion = null, int px = 1, int py = 1)
                {
                    WallSide(b, wall, edge, wl, wf, true, winA, lo, hi, u0, u1, edgeWidth, mullion, px, py);
                    WallSide(b, wall, edge, wl, wf, false, winB, lo, hi, u0, u1, edgeWidth, mullion, px, py);
                    var p0 = Profile(style, lastRoof, lastMid);
                    var p1 = Profile(style, roof, mid);
                    if (OpenTop(style)) p0.Clear(); // no roof: open to the sky
                    for (int k = 0; k + 1 < p0.Count && k + 1 < p1.Count; k++)
                    {
                        Vector3 a0 = ProfilePoint(last, p0[k]), b0 = ProfilePoint(last, p0[k + 1]);
                        Vector3 a1 = ProfilePoint(fr, p1[k]), b1 = ProfilePoint(fr, p1[k + 1]);
                        Vector3 seg = b1 - a1;
                        bool flat = Mathf.Abs(seg.y) < 0.35f * new Vector2(seg.x, seg.z).magnitude;
                        b.Quad(flat ? ceiling : vaultWall, a0, a1, b1, b0);
                    }
                    if (fl) b.Quad(fl, last.A.WithY(last.bottom), A.WithY(bottom), B.WithY(bottom), last.B.WithY(last.bottom));
                }

                switch (style)
                {
                    case SceneryStyle.Cathedral:
                    {
                        if (!hasLast) break;
                        bool window = ribs % 3 == 1;
                        Room(kit.scenery, kit.slab, kit.scenery, floor, window, window, 4f, 22f, 0.38f, 0.62f, kit.glow, 0.5f, kit.slab, 1, 4);
                        Mouldings(b, kit.slab, wl, wf);
                        foreach (bool sideA in new[] { true, false })
                        {
                            if (window) Shaft(b, kit.shaft, kit.skyPool, wl, wf, sideA, 0.38f, 0.62f, 4f, 22f);
                            else WallPanel(b, kit.accent, kit.slab, wl, wf, sideA, 0.14f, 0.86f, -6f, 16f);
                        }
                        // The red floor line, and a glowing seam up the crown of the arch
                        Slab(b, kit.glow, lastMid.WithY(last.bottom + 0.6f), mid.WithY(bottom + 0.6f), 1.2f, 0.4f);
                        Slab(b, kit.glow, lastMid.WithY(last.level + lastRoof * 1.35f - 0.6f), mid.WithY(level + roof * 1.35f - 0.6f), 0.8f, 0.3f);
                        WallLights(b, kit.glow, wl, wf, lastMid, mid, wallTop - 1f, 0.5f);
                        break;
                    }
                    case SceneryStyle.Palace:
                    {
                        foreach (var at in new[] { A, B })
                        {
                            b.Box(kit.scenery, at.WithY((top + bottom) * 0.5f), new Vector3(1.8f, h, 1.8f), rot);
                            b.Box(kit.scenery, at.WithY(top - 0.5f), new Vector3(3f, 1f, 3f), rot);
                        }
                        b.Box(kit.scenery, mid.WithY(top + 0.8f), new Vector3(span + 3f, 1.6f, 1.6f), rot);
                        if (hasLast)
                        {
                            Line(b, kit.glow, last.A.WithY(last.top + 1.8f), A.WithY(top + 1.8f), 0.35f);
                            Line(b, kit.glow, last.B.WithY(last.top + 1.8f), B.WithY(top + 1.8f), 0.35f);
                            Vector3 cc = (lastMid + mid) * 0.5f;
                            b.Box(kit.scenery, cc.WithY((last.top + top) * 0.5f + 1.6f), new Vector3(span, 0.4f, 1.2f), rot);
                            if (ribs % 2 == 0)
                            {
                                Vector3 cloud = cc + right * Rand(-60f, 60f);
                                b.Box(kit.scenery, cloud.WithY(bottom - Rand(20f, 45f)), new Vector3(Rand(30f, 70f), Rand(4f, 9f), Rand(25f, 50f)), Quaternion.Euler(0f, Rand(0f, 360f), 0f));
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Rings:
                    {
                        if (hasLast)
                        {
                            Room(kit.scenery, kit.scenery, kit.scenery, kit.scenery, false, false, 0f, 0f, 0f, 0f, null, 0f);
                            Slab(b, kit.glow, lastMid.WithY(last.bottom + 0.3f), mid.WithY(bottom + 0.3f), 1.5f, 0.3f);
                            // Glowing seams running the length of the tube
                            var p0 = Profile(style, lastRoof, lastMid);
                            var p1 = Profile(style, roof, mid);
                            foreach (int k in new[] { 3, 8, 13 })
                                Slab(b, kit.glowAlt, ProfilePoint(last, p0[k]), ProfilePoint(fr, p1[k]), 0.5f, 0.5f);
                        }
                        if (ribs % 4 == 0)
                        {
                            float radius = Mathf.Min(span * 0.5f - 3f, roof - 4f);
                            Vector3 center = mid.WithY(level + 2f);
                            Material m = (ribs / 4) % 2 == 0 ? kit.glow : kit.glowAlt;
                            const int blocks = 36;
                            for (int n = 0; n < blocks; n++)
                            {
                                float a0 = n * Mathf.PI * 2f / blocks, a1 = (n + 1) * Mathf.PI * 2f / blocks;
                                Vector3 q0 = center + (right * Mathf.Cos(a0) + Vector3.up * Mathf.Sin(a0)) * radius;
                                Vector3 q1 = center + (right * Mathf.Cos(a1) + Vector3.up * Mathf.Sin(a1)) * radius;
                                b.Box(m, (q0 + q1) * 0.5f, new Vector3(1.2f, 1.2f, (q1 - q0).magnitude + 0.5f), Quaternion.LookRotation(q1 - q0, f));
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Grotto:
                    {
                        if (!hasLast) break;
                        Room(kit.scenery, kit.scenery, kit.scenery, kit.glow, false, false, 0f, 0f, 0f, 0f, null, 0f);
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 inward = (mid - at).WithY(0f).normalized;
                            if (rng.NextDouble() < 0.6)
                            {
                                Vector3 mu = at + inward * Rand(4f, 8f);
                                float y = bottom + Rand(1f, 6f), stem = Rand(2f, 4f);
                                b.Box(kit.slab, mu.WithY(y + stem * 0.5f), new Vector3(0.6f, stem, 0.6f), rot);
                                b.Box(kit.glow, mu.WithY(y + stem), new Vector3(Rand(2.5f, 4f), 0.7f, Rand(2.5f, 4f)), Quaternion.Euler(0f, 45f, 0f));
                            }
                            if (rng.NextDouble() < 0.4)
                                b.Box(kit.glowAlt, (at + inward * Rand(2f, 6f)).WithY(level + Rand(4f, 16f)), new Vector3(1.2f, Rand(4f, 8f), 1.2f), Quaternion.Euler(Rand(-35f, 35f), Rand(0f, 90f), Rand(-35f, 35f)));
                        }
                        if (ribs % 6 == 2)
                        {
                            Vector3 lantern = (mid + right * Rand(-span * 0.3f, span * 0.3f)).WithY(level + Rand(6f, 14f));
                            b.Box(kit.glowAlt, lantern, Vector3.one * 2.4f, Quaternion.Euler(Rand(0f, 45f), Rand(0f, 90f), Rand(0f, 45f)));
                            Pool(b, kit.skyPool, lantern.WithY(bottom + 0.3f), right, f, 18f, 18f);
                        }
                        break;
                    }
                    case SceneryStyle.Candy:
                    {
                        if (!hasLast) break;
                        bool window = ribs % 5 == 2;
                        // Stepped blocky terraces over cyan glowing water
                        Room(kit.scenery, kit.scenery, kit.accent, kit.glowAlt, window, window, 2f, 13f, 0.25f, 0.75f, kit.glowAlt, 0.6f, kit.glowAlt, 2, 2);
                        Mouldings(b, kit.accent, wl, wf);
                        if (window) foreach (bool sideA in new[] { true, false }) Shaft(b, kit.shaft, kit.skyPool, wl, wf, sideA, 0.25f, 0.75f, 2f, 13f);
                        // Pastel blocks sitting on the terrace ledges, now and then
                        if (ribs % 3 == 0)
                        {
                            var ledge = Profile(style, roof, mid);
                            var mats = new[] { kit.ramp, kit.slab, kit.accent, kit.glow };
                            foreach (int k in new[] { 1, 3, ledge.Count - 4, ledge.Count - 2 })
                            {
                                if (rng.NextDouble() < 0.5) continue;
                                float size = Rand(3f, 7f);
                                Vector3 on = ProfilePoint(fr, ledge[k]) + Vector3.up * size * 0.5f + (mid - ProfilePoint(fr, ledge[k])).WithY(0f).normalized * size * 0.6f;
                                b.Box(mats[rng.Next(mats.Length)], on, Vector3.one * size, rot * Quaternion.Euler(0f, Rand(-10f, 10f), 0f));
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Forge:
                    {
                        if (!hasLast) break;
                        Room(kit.scenery, kit.slab, kit.scenery, null, false, false, 0f, 0f, 0f, 0f, null, 0f);
                        Mouldings(b, kit.slab, wl, wf);
                        if (ribs % 2 == 0) foreach (bool sideA in new[] { true, false }) WallPanel(b, kit.accent, kit.slab, wl, wf, sideA, 0.1f, 0.9f, 4f, 20f);
                        // Pipes running the length of both walls, one of them glowing hot
                        foreach (bool sideA in new[] { true, false })
                            foreach (var (y, hot) in new[] { (wallTop - 3f, false), (wallTop - 6f, true), (wallTop - 9f, false) })
                                Slab(b, hot ? kit.glowAlt : kit.slab, OnWall(wl, wf, sideA, 0f, y, 1.6f), OnWall(wl, wf, sideA, 1f, y, 1.6f), 1.4f, 1.4f);
                        Slab(b, kit.glow, lastMid.WithY(last.level + lastRoof + 3.7f), mid.WithY(level + roof + 3.7f), 2.5f, 0.2f);
                        // The lava floor: a glowing sea with dark grid bars over it
                        b.Quad(kit.glow, last.A.WithY(last.bottom), A.WithY(bottom), B.WithY(bottom), last.B.WithY(last.bottom));
                        float lastSpan = Vector3.Distance(last.A.WithY(0f), last.B.WithY(0f));
                        for (int n = -3; n <= 3; n++)
                        {
                            Vector3 o0 = last.right * (n * lastSpan / 7f), o1 = right * (n * span / 7f);
                            Slab(b, kit.slab, (lastMid + o0).WithY(last.bottom + 0.4f), (mid + o1).WithY(bottom + 0.4f), 0.8f, 0.6f);
                        }
                        break;
                    }
                    case SceneryStyle.Wire:
                    {
                        // A room walled in glowing grid, its long edges traced in unbroken
                        // lines. (No more rings of beams across the way: they flickered past.)
                        if (!hasLast) break;
                        Room(kit.scenery, kit.scenery, kit.scenery, kit.floor, false, false, 0f, 0f, 0f, 0f, null, 0f);
                        foreach (var (w0, w1) in new[] { (last.A, A), (last.B, B) })
                        {
                            Slab(b, kit.glow, w0.WithY(last.top - 0.4f), w1.WithY(top - 0.4f), 0.6f, 0.6f);
                            Slab(b, kit.glow, w0.WithY(last.bottom + 0.4f), w1.WithY(bottom + 0.4f), 0.6f, 0.6f);
                        }
                        Slab(b, kit.glow, lastMid.WithY(last.top - 0.3f), mid.WithY(top - 0.3f), 1.2f, 0.3f);
                        break;
                    }
                    case SceneryStyle.Canyon:
                    {
                        // A red-rock canyon open to the sky: layered cliffs, waterfalls pouring
                        // down them into glowing pools, blue crystals at their feet
                        if (!hasLast) break;
                        Room(kit.scenery, kit.scenery, kit.scenery, kit.floor, false, false, 0f, 0f, 0f, 0f, null, 0f);
                        foreach (bool sideA in new[] { true, false })
                            foreach (float y in new[] { wallTop * 0.35f, wallTop * 0.7f })
                                Slab(b, kit.slab, OnWall(wl, wf, sideA, 0f, y, 1.5f), OnWall(wl, wf, sideA, 1f, y, 1.5f), 3.5f, 1.4f);
                        if (ribs % 6 == 2)
                        {
                            bool sideA = (ribs / 6) % 2 == 0;
                            float floorY = bottom - level;
                            b.Quad(kit.shaft, OnWall(wl, wf, sideA, 0.3f, wallTop, 0.7f), OnWall(wl, wf, sideA, 0.7f, wallTop, 0.7f),
                                OnWall(wl, wf, sideA, 0.7f, floorY, 0.7f), OnWall(wl, wf, sideA, 0.3f, floorY, 0.7f), uv: true);
                            foreach (float u in new[] { 0.38f, 0.5f, 0.62f })
                                Slab(b, kit.glowAlt, OnWall(wl, wf, sideA, u, wallTop, 0.9f), OnWall(wl, wf, sideA, u, floorY, 0.9f), 0.35f, 0.35f, vertical: true);
                            Pool(b, kit.skyPool, OnWall(wl, wf, sideA, 0.5f, floorY + 0.3f, 10f), f, Inward(wl, wf, sideA, 0.5f), 22f, 18f);
                        }
                        if (ribs % 3 == 0)
                            foreach (bool sideA in new[] { true, false })
                                b.Box(kit.glowAlt, OnWall(wl, wf, sideA, Rand(0.2f, 0.8f), bottom - level + 3f, Rand(3f, 8f)), new Vector3(1.6f, Rand(4f, 9f), 1.6f),
                                    Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 90f), Rand(-25f, 25f)));
                        break;
                    }
                    case SceneryStyle.Garden:
                    {
                        // A moonlit garden: tall hedges with stone coping, a reflecting pool below,
                        // lanterns glowing along the hedges, a stone arch now and then
                        if (!hasLast) break;
                        Room(kit.scenery, kit.scenery, kit.scenery, kit.floor, false, false, 0f, 0f, 0f, 0f, null, 0f);
                        foreach (bool sideA in new[] { true, false })
                        {
                            Slab(b, kit.accent, OnWall(wl, wf, sideA, 0f, wallTop + 0.6f, 0.2f), OnWall(wl, wf, sideA, 1f, wallTop + 0.6f, 0.2f), 3f, 1.2f);
                            Slab(b, kit.accent, OnWall(wl, wf, sideA, 0f, bottom - level + 0.6f, 1f), OnWall(wl, wf, sideA, 1f, bottom - level + 0.6f, 1f), 2.5f, 1.2f);
                        }
                        if (ribs % 3 == 1)
                            foreach (bool sideA in new[] { true, false })
                            {
                                Vector3 lamp = OnWall(wl, wf, sideA, 0.5f, 10f, 1.2f);
                                b.Box(kit.accent, lamp - Vector3.up * 1.4f, new Vector3(0.5f, 2f, 0.5f), rot);
                                b.Box(kit.glow, lamp, new Vector3(1.1f, 1.4f, 1.1f), rot);
                                Pool(b, kit.pool, OnWall(wl, wf, sideA, 0.5f, bottom - level + 0.35f, 6f), f, Inward(wl, wf, sideA, 0.5f), 14f, 12f);
                            }
                        if (ribs % 12 == 6) ArchWall(b, kit.accent, kit.glow, fr, 0.15f, 0.85f, 26f, 16f);
                        break;
                    }
                    case SceneryStyle.Lab:
                    {
                        // A white lab hall: glowing hex panels, pipes along the walls (one lit
                        // green), a light strip down the floor
                        if (!hasLast) break;
                        Room(kit.scenery, kit.slab, kit.scenery, kit.floor, false, false, 0f, 0f, 0f, 0f, null, 0f);
                        Mouldings(b, kit.slab, wl, wf);
                        if (ribs % 2 == 0) foreach (bool sideA in new[] { true, false }) WallPanel(b, kit.accent, kit.slab, wl, wf, sideA, 0.08f, 0.92f, 4f, wallTop - 8f);
                        foreach (bool sideA in new[] { true, false })
                            foreach (var (y, lit) in new[] { (wallTop - 3f, false), (wallTop - 5.5f, true), (wallTop - 8f, false) })
                                Slab(b, lit ? kit.glow : kit.slab, OnWall(wl, wf, sideA, 0f, y, 1.4f), OnWall(wl, wf, sideA, 1f, y, 1.4f), 1.2f, 1.2f);
                        Slab(b, kit.glow, lastMid.WithY(last.bottom + 0.5f), mid.WithY(bottom + 0.5f), 2f, 0.3f);
                        break;
                    }
                    case SceneryStyle.Mine:
                    {
                        // A torchlit mine: a rough cave with timber props along its walls, torches
                        // burning on them, rails along the floor far below
                        if (!hasLast) break;
                        Room(kit.scenery, kit.scenery, kit.scenery, kit.floor, false, false, 0f, 0f, 0f, 0f, null, 0f);
                        if (ribs % 3 == 0)
                            foreach (bool sideA in new[] { true, false })
                            {
                                Vector3 foot = OnWall(wl, wf, sideA, 1f, bottom - level, 1.2f), head = OnWall(wl, wf, sideA, 1f, wallTop, 1.2f);
                                Slab(b, kit.slab, foot, head, 1.4f, 1.4f, vertical: true);
                                Vector3 inward = Inward(wl, wf, sideA, 1f);
                                Slab(b, kit.slab, head - Vector3.up * 1.5f, head + inward * 12f + Vector3.up * 6f, 1.1f, 1.1f); // a brace up under the roof
                            }
                        if (ribs % 2 == 0)
                        {
                            bool sideA = (ribs / 2) % 2 == 0;
                            Vector3 torch = OnWall(wl, wf, sideA, 0.5f, wallTop * 0.7f, 1f);
                            b.Box(kit.slab, torch - Vector3.up * 0.8f, new Vector3(0.3f, 1.4f, 0.3f), rot);
                            b.Box(kit.glow, torch, new Vector3(0.7f, 1f, 0.7f), Quaternion.Euler(0f, 45f, 0f));
                            Pool(b, kit.pool, OnWall(wl, wf, sideA, 0.5f, wallTop * 0.7f, 0.4f), f, Vector3.up, 9f, 12f);
                        }
                        foreach (float x in new[] { -3f, 3f })
                            Slab(b, kit.slab, (lastMid + last.right * x).WithY(last.bottom + 0.3f), (mid + right * x).WithY(bottom + 0.3f), 0.5f, 0.4f);
                        break;
                    }
                    case SceneryStyle.Synth:
                    {
                        // A synthwave station: grid walls glowing pink, hot pink and white light
                        // strips running its length, a great ring every eighth rib
                        if (!hasLast) break;
                        Room(kit.scenery, kit.accent, kit.scenery, kit.floor, false, false, 0f, 0f, 0f, 0f, null, 0f);
                        foreach (bool sideA in new[] { true, false })
                        {
                            Slab(b, kit.glow, OnWall(wl, wf, sideA, 0f, wallTop - 1f, 0.6f), OnWall(wl, wf, sideA, 1f, wallTop - 1f, 0.6f), 0.8f, 0.4f);
                            Slab(b, kit.slab, OnWall(wl, wf, sideA, 0f, wallTop * 0.35f, 0.6f), OnWall(wl, wf, sideA, 1f, wallTop * 0.35f, 0.6f), 0.6f, 0.4f);
                        }
                        if (ribs % 8 == 4)
                        {
                            float radius = Mathf.Min(span * 0.5f - 4f, roof - 6f);
                            Vector3 centre = mid.WithY(level + 2f);
                            const int blocks = 40;
                            for (int n = 0; n < blocks; n++)
                            {
                                float a0 = n * Mathf.PI * 2f / blocks, a1 = (n + 1) * Mathf.PI * 2f / blocks;
                                Vector3 q0 = centre + (right * Mathf.Cos(a0) + Vector3.up * Mathf.Sin(a0)) * radius;
                                Vector3 q1 = centre + (right * Mathf.Cos(a1) + Vector3.up * Mathf.Sin(a1)) * radius;
                                b.Box(n % 2 == 0 ? kit.glow : kit.glowAlt, (q0 + q1) * 0.5f, new Vector3(1.2f, 1.2f, (q1 - q0).magnitude + 0.5f), Quaternion.LookRotation(q1 - q0, f));
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Temple:
                    {
                        // A sandstone temple: stepped walls climbing to the roof, carved panels,
                        // torches, and sunlight falling through high windows
                        if (!hasLast) break;
                        bool window = ribs % 4 == 2;
                        Room(kit.scenery, kit.scenery, kit.scenery, kit.floor, window, window, 6f, wallTop - 4f, 0.3f, 0.7f, kit.glow, 0.5f, kit.slab, 1, 2);
                        Mouldings(b, kit.slab, wl, wf);
                        foreach (bool sideA in new[] { true, false })
                        {
                            if (window) Shaft(b, kit.shaft, kit.skyPool, wl, wf, sideA, 0.3f, 0.7f, 6f, wallTop - 4f);
                            else if (ribs % 2 == 0) WallPanel(b, kit.accent, kit.slab, wl, wf, sideA, 0.15f, 0.85f, 2f, wallTop - 6f);
                        }
                        if (ribs % 3 == 1)
                            foreach (bool sideA in new[] { true, false })
                            {
                                Vector3 torch = OnWall(wl, wf, sideA, 0.5f, 8f, 1f);
                                b.Box(kit.glow, torch, new Vector3(0.8f, 1.1f, 0.8f), Quaternion.Euler(0f, 45f, 0f));
                                Pool(b, kit.pool, OnWall(wl, wf, sideA, 0.5f, 8f, 0.4f), f, Vector3.up, 10f, 13f);
                            }
                        break;
                    }
                    case SceneryStyle.Alpine:
                    {
                        // Open mountains: a snowfield far below, pine forest on the slopes either
                        // side, and snowy peaks beyond
                        float ground = GroundBelow(bottom, 80f);
                        if (hasLast)
                            b.Quad(kit.floor, (last.p - last.right * 420f).WithY(ground), (fr.p - right * 420f).WithY(ground),
                                (fr.p + right * 420f).WithY(ground), (last.p + last.right * 420f).WithY(ground));
                        if (ribs % 2 == 0)
                            foreach (float side in new[] { -1f, 1f })
                                for (int t = 0; t < 3; t++)
                                {
                                    float dist = Rand(55f, 180f);
                                    Vector3 at = (fr.p + right * (side * dist) + f * Rand(-6f, 6f)).WithY(ground + (dist - 55f) * 0.35f);
                                    float tall = Rand(14f, 28f);
                                    if (Clear(at, tall * 0.3f)) Pine(Piece(), kit.accent, kit.slab, kit.floor, at, tall);
                                }
                        if (ribs % 6 == 3)
                        {
                            float side = (ribs / 6) % 2 == 0 ? 1f : -1f, r = Rand(90f, 170f), hgt = Rand(170f, 320f);
                            Vector3 foot = (fr.p + right * (side * Rand(280f, 440f))).WithY(ground - 20f);
                            if (Clear(foot, r))
                            {
                                var pb = Piece();
                                Cone(pb, kit.scenery, foot, r, hgt, 9);
                                Cone(pb, kit.floor, foot + Vector3.up * hgt * 0.6f, r * 0.42f, hgt * 0.4f + 0.5f, 9);
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Ember:
                    {
                        // A burning sunset over dark mesas: black ground far below, flat-topped
                        // mesas and ragged peaks in silhouette, an ember-lit hoop now and then
                        float ground = GroundBelow(bottom, 90f);
                        if (hasLast)
                            b.Quad(kit.floor, (last.p - last.right * 450f).WithY(ground), (fr.p - right * 450f).WithY(ground),
                                (fr.p + right * 450f).WithY(ground), (last.p + last.right * 450f).WithY(ground));
                        if (ribs % 4 == 1)
                        {
                            float side = Rand(0f, 1f) < 0.5f ? -1f : 1f, hgt = Rand(60f, 150f);
                            Vector3 c = (fr.p + right * (side * Rand(140f, 320f))).WithY(ground + hgt * 0.5f);
                            float mw = Rand(50f, 110f), md = Rand(50f, 110f);
                            if (Clear(c, Mathf.Max(mw, md) * 0.75f))
                            {
                                var pb = Piece();
                                pb.Box(kit.scenery, c, new Vector3(mw, hgt, md), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                                pb.Box(kit.glow, c + Vector3.up * (hgt * 0.5f - 1f), new Vector3(Rand(30f, 60f), 0.6f, 0.6f), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                            }
                        }
                        if (ribs % 7 == 3)
                        {
                            float side = (ribs / 7) % 2 == 0 ? 1f : -1f;
                            Vector3 foot = (fr.p + right * (side * Rand(360f, 480f))).WithY(ground - 10f);
                            float r = Rand(90f, 150f), hgt = Rand(180f, 300f);
                            if (Clear(foot, r)) Cone(Piece(), kit.scenery, foot, r, hgt, 7);
                        }
                        if (ribs % 9 == 5)
                        {
                            float side = (ribs / 9) % 2 == 0 ? 1f : -1f;
                            Vector3 c = fr.p + right * (side * 90f) + Vector3.up * 20f;
                            if (!Clear(c, 26f)) break;
                            var ring = Piece();
                            const int blocks = 32;
                            for (int n = 0; n < blocks; n++)
                            {
                                float a0 = n * Mathf.PI * 2f / blocks, a1 = (n + 1) * Mathf.PI * 2f / blocks;
                                Vector3 q0 = c + (f * Mathf.Cos(a0) + Vector3.up * Mathf.Sin(a0)) * 26f, q1 = c + (f * Mathf.Cos(a1) + Vector3.up * Mathf.Sin(a1)) * 26f;
                                ring.Box(kit.glow, (q0 + q1) * 0.5f, new Vector3(1.2f, 1.2f, (q1 - q0).magnitude + 0.4f), Quaternion.LookRotation(q1 - q0, right));
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Glass:
                    {
                        // A glass city in the dark: towers rising out of the void, dark cores in
                        // glass skins, their edges lit in neon
                        if (ribs % 3 == 0)
                            foreach (float side in new[] { -1f, 1f })
                            {
                                if (Rand(0f, 1f) < 0.3f) continue;
                                float w = Rand(18f, 34f), d = Rand(18f, 34f), hgt = Rand(80f, 220f);
                                Vector3 foot = (fr.p + right * (side * Rand(70f, 170f)) + f * Rand(-10f, 10f)).WithY(GroundBelow(bottom, 140f));
                                var q = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(0f, Rand(-20f, 20f), 0f);
                                if (Clear(foot, Mathf.Max(w, d) * 0.75f)) Tower(Piece(), kit, foot, w, d, hgt, q, Rand(0f, 1f) < 0.5f ? kit.glow : kit.glowAlt);
                            }
                        break;
                    }
                    case SceneryStyle.Amethyst:
                    {
                        // An amethyst void: great lavender crystal clusters hanging in the dark
                        // around the ramp, each lit through its heart
                        if (ribs % 2 == 0)
                            foreach (float side in new[] { -1f, 1f })
                            {
                                Vector3 c = fr.p + right * (side * Rand(50f, 150f)) + Vector3.up * Rand(-60f, 40f);
                                if (!Clear(c, 40f)) continue;
                                var pb = Piece();
                                int shards = 3 + (int)Rand(0f, 3f);
                                for (int k = 0; k < shards; k++)
                                {
                                    var q = Quaternion.Euler(Rand(-40f, 40f), Rand(0f, 360f), Rand(-40f, 40f));
                                    float len = Rand(18f, 55f), w = Rand(4f, 10f);
                                    pb.Box(kit.scenery, c + q * Vector3.up * (len * 0.4f), new Vector3(w, len, w * 0.8f), q * Quaternion.Euler(0f, 45f, 0f));
                                    Line(pb, kit.glow, c, c + q * Vector3.up * (len * 0.85f), 0.5f);
                                }
                            }
                        break;
                    }
                    case SceneryStyle.Toy:
                    {
                        // Toy town: a bright green world far below, towers of chunky coloured
                        // blocks, white columns, puffy blocky clouds in a blue sky
                        float ground = GroundBelow(bottom, 80f);
                        if (hasLast)
                            b.Quad(kit.floor, (last.p - last.right * 420f).WithY(ground), (fr.p - right * 420f).WithY(ground),
                                (fr.p + right * 420f).WithY(ground), (last.p + last.right * 420f).WithY(ground));
                        var blocks = new[] { kit.accent, kit.slab, kit.scenery, kit.glowAlt };
                        if (ribs % 3 == 0)
                            foreach (float side in new[] { -1f, 1f })
                            {
                                Vector3 at = (fr.p + right * (side * Rand(60f, 170f)) + f * Rand(-8f, 8f)).WithY(ground);
                                if (!Clear(at, 14f)) continue;
                                var pb = Piece();
                                int count = 2 + (int)Rand(0f, 4f);
                                for (int k = 0; k < count; k++)
                                {
                                    float size = Rand(10f, 20f);
                                    at += Vector3.up * size * 0.5f;
                                    pb.Box(blocks[(k + ribs) % blocks.Length], at, Vector3.one * size, Quaternion.Euler(0f, Rand(-15f, 15f), 0f));
                                    at += Vector3.up * size * 0.5f;
                                }
                            }
                        if (ribs % 6 == 4)
                        {
                            float side = (ribs / 6) % 2 == 0 ? 1f : -1f;
                            Vector3 col = (fr.p + right * (side * Rand(90f, 150f))).WithY(ground);
                            float hgt = Rand(60f, 110f);
                            if (Clear(col, 8f))
                            {
                                var pb = Piece();
                                pb.Box(kit.scenery, col + Vector3.up * hgt * 0.5f, new Vector3(7f, hgt, 7f), rot);
                                pb.Box(kit.slab, col + Vector3.up * (hgt + 1.5f), new Vector3(12f, 3f, 12f), rot);
                            }
                        }
                        if (ribs % 4 == 2)
                        {
                            Vector3 cloud = fr.p + right * Rand(-200f, 200f) + Vector3.up * Rand(60f, 110f);
                            var puff = Clear(cloud, 30f) ? Piece() : null;
                            for (int k = 0; k < 5 && puff != null; k++)
                                puff.Box(kit.scenery, cloud + new Vector3(Rand(-18f, 18f), Rand(-3f, 5f), Rand(-10f, 10f)), new Vector3(Rand(14f, 26f), Rand(8f, 14f), Rand(12f, 20f)), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                        }
                        break;
                    }
                    case SceneryStyle.Spectrum:
                    {
                        // An open void like the neon surf classics: the ramp held up by glowing
                        // lattice towers, and giant grid panels hanging far out to the sides.
                        // Nothing comes near the riding line or the flights.
                        if (ribs % 4 == 2)
                        {
                            float y0 = fr.bottom + 23.5f, y1 = y0 - 70f;
                            Vector3 r = right * 2f, g = f * 2f;
                            var corner = new[] { fr.p + r + g, fr.p + r - g, fr.p - r - g, fr.p - r + g };
                            foreach (var c in corner) Line(b, kit.glow, c.WithY(y0), c.WithY(y1), 0.3f);
                            for (float y = y0; y - 6f >= y1; y -= 6f)
                                for (int k = 0; k < 4; k++)
                                    Line(b, kit.glow, corner[k].WithY(y), corner[(k + 1) % 4].WithY(y - 6f), 0.16f);
                        }
                        if (ribs % 5 == 0)
                        {
                            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                            Vector3 c = fr.p + right * (side * Rand(100f, 150f)) + Vector3.up * Rand(-40f, 30f);
                            float w = Rand(30f, 55f), hgt = Rand(18f, 32f);
                            var q = Quaternion.LookRotation(right * side, Vector3.up) * Quaternion.Euler(Rand(-35f, 35f), Rand(-30f, 30f), Rand(-20f, 20f));
                            Vector3 ax = q * Vector3.right * (w * 0.5f), ay = q * Vector3.up * (hgt * 0.5f);
                            Vector3 p0 = c - ax - ay, p1 = c + ax - ay, p2 = c + ax + ay, p3 = c - ax + ay;
                            b.Quad(kit.scenery, p0, p1, p2, p3);
                            Line(b, kit.glow, p0, p1, 0.5f);
                            Line(b, kit.glow, p1, p2, 0.5f);
                            Line(b, kit.glow, p2, p3, 0.5f);
                            Line(b, kit.glow, p3, p0, 0.5f);
                        }
                        break;
                    }
                    case SceneryStyle.Gallery:
                    {
                        if (hasLast)
                        {
                            int side = (ribs / 3) % 2;
                            bool winA = ribs % 3 == 1 && side == 0, winB = ribs % 3 == 1 && side == 1;
                            Room(kit.scenery, kit.scenery, kit.slab, floor, winA, winB, -2f, 20f, 0.15f, 0.85f, kit.slab, 0.8f, kit.slab, 4, 3);
                            Mouldings(b, kit.slab, wl, wf);
                            foreach (var (sideA, win) in new[] { (true, winA), (false, winB) })
                            {
                                Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? A : B;
                                Vector3 in0 = (lastMid - w0).WithY(0f).normalized, in1 = (mid - w1).WithY(0f).normalized;
                                if (win) Shaft(b, kit.shaft, kit.skyPool, wl, wf, sideA, 0.15f, 0.85f, -2f, 20f);
                                else
                                {
                                    WallPanel(b, kit.accent, kit.slab, wl, wf, sideA, 0.2f, 0.8f, -1f, 13f);
                                    Vector3 sc = OnWall(wl, wf, sideA, 0.5f, 6f, 0.8f);
                                    b.Box(kit.glow, sc, new Vector3(0.6f, 2.2f, 1.2f), Quaternion.LookRotation((w1 - w0).WithY(0f)));
                                    Pool(b, kit.pool, OnWall(wl, wf, sideA, 0.5f, 6f, 0.55f), (w1 - w0).WithY(0f).normalized, Vector3.up, 9f, 11f);
                                }
                                Slab(b, kit.glowAlt, (w0 + in0 * 1.6f).WithY(last.bottom + 0.8f), (w1 + in1 * 1.6f).WithY(bottom + 0.8f), 0.8f, 0.2f);
                            }
                            // Light panel down the coffered ceiling
                            Slab(b, kit.glowAlt, lastMid.WithY(last.level + lastRoof + 2.7f), mid.WithY(level + roof + 2.7f), 4f, 0.2f);
                        }
                        // Every eighth rib the gallery is divided by a wall with a tall round-headed
                        // archway through it: room after room, like the white corridors
                        if (ribs % 8 == 4) ArchWall(b, kit.scenery, kit.slab, fr, 0.2f, 0.8f, 24f, 14f);
                        break;
                    }
                    case SceneryStyle.Sunset:
                    {
                        if (!hasLast) break;
                        int side = (ribs / 4) % 2;
                        bool winA = ribs % 4 == 2 && side == 0, winB = ribs % 4 == 2 && side == 1;
                        Room(kit.scenery, kit.slab, kit.scenery, floor, winA, winB, -4f, 18f, 0.1f, 0.9f, kit.glow, 1.2f);
                        foreach (var (sideA, win) in new[] { (true, winA), (false, winB) })
                        {
                            if (win) Shaft(b, kit.shaft, kit.skyPool, wl, wf, sideA, 0.1f, 0.9f, -4f, 18f);
                            else if (ribs % 2 == 0) WallPanel(b, kit.accent, kit.slab, wl, wf, sideA, 0.12f, 0.88f, -1f, 15f);
                            // Peach light slot where the wall starts rolling into the ceiling
                            Slab(b, kit.glowAlt, OnWall(wl, wf, sideA, 0f, wallTop - 0.5f, 0.6f), OnWall(wl, wf, sideA, 1f, wallTop - 0.5f, 0.6f), 0.5f, 0.6f);
                            // The sloped foot of the wall, leaning into the room
                            Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? A : B;
                            Vector3 in0 = (lastMid - w0).WithY(0f).normalized, in1 = (mid - w1).WithY(0f).normalized;
                            b.Quad(kit.scenery, w0.WithY(last.bottom + 9f), w1.WithY(bottom + 9f), (w1 + in1 * 6f).WithY(bottom), (w0 + in0 * 6f).WithY(last.bottom));
                        }
                        break;
                    }
                    case SceneryStyle.Library:
                    {
                        if (!hasLast) break;
                        bool window = ribs % 2 == 1;
                        float lo = wallTop - 16f, hi = wallTop - 3f;
                        Room(kit.scenery, kit.slab, kit.scenery, floor, window, window, lo, hi, 0.3f, 0.7f, kit.glow, 0.6f, kit.slab, 2, 3);
                        Mouldings(b, kit.slab, wl, wf);
                        foreach (bool sideA in new[] { true, false })
                        {
                            // Bookshelves the whole length of the wall, a gold light along their top
                            float shelfLo = Mathf.Max(last.bottom - last.level, bottom - level) + 1.5f;
                            WallPanel(b, kit.accent, kit.slab, wl, wf, sideA, 0f, 1f, shelfLo, lo - 1.5f);
                            Slab(b, kit.glow, OnWall(wl, wf, sideA, 0f, lo - 1f, 1f), OnWall(wl, wf, sideA, 1f, lo - 1f, 1f), 0.4f, 0.4f);
                            if (window) Shaft(b, kit.shaft, kit.skyPool, wl, wf, sideA, 0.3f, 0.7f, lo, hi);
                        }
                        // Gold ribs following the vault every other rib, and now and then a glowing
                        // blue portal hanging in the air
                        if (ribs % 2 == 0)
                        {
                            var pr = Profile(style, roof, mid);
                            for (int k = 0; k + 1 < pr.Count; k++) Line(b, kit.slab, ProfilePoint(fr, pr[k]), ProfilePoint(fr, pr[k + 1]), 0.6f);
                        }
                        if (ribs % 10 == 5)
                        {
                            Vector3 c = (mid + right * Rand(-span * 0.25f, span * 0.25f)).WithY(level + Rand(10f, 18f));
                            const int blocks = 28;
                            for (int n = 0; n < blocks; n++)
                            {
                                float a0 = n * Mathf.PI * 2f / blocks, a1 = (n + 1) * Mathf.PI * 2f / blocks;
                                Vector3 q0 = c + (right * Mathf.Cos(a0) + Vector3.up * Mathf.Sin(a0)) * 6f, q1 = c + (right * Mathf.Cos(a1) + Vector3.up * Mathf.Sin(a1)) * 6f;
                                b.Box(kit.glowAlt, (q0 + q1) * 0.5f, new Vector3(0.8f, 0.8f, (q1 - q0).magnitude + 0.3f), Quaternion.LookRotation(q1 - q0, f));
                            }
                            Pool(b, kit.skyPool, c, right, Vector3.up, 12f, 12f);
                            Pool(b, kit.skyPool, c.WithY(bottom + 0.3f), right, f, 20f, 20f);
                        }
                        break;
                    }
                }
            }

            // The doorway where this zone begins: a heavy frame following the zone's own shape
            if (doorway >= 0 && doorway < frames.Count)
            {
                var fr = frames[doorway];
                var rot = Quaternion.LookRotation(fr.f, Vector3.up);
                float roof = fr.top - fr.level;
                var pr = Profile(style, roof, (fr.A + fr.B) * 0.5f);
                foreach (var at in new[] { fr.A, fr.B })
                {
                    b.Box(kit.scenery, at.WithY((fr.bottom + fr.level + roof * wallFrac) * 0.5f), new Vector3(3f, fr.level + roof * wallFrac - fr.bottom, 3f), rot);
                    b.Box(kit.glow, (at + ((fr.A + fr.B) * 0.5f - at).WithY(0f).normalized * 1.8f).WithY((fr.bottom + fr.level + roof * wallFrac) * 0.5f), new Vector3(0.6f, fr.level + roof * wallFrac - fr.bottom, 0.6f), rot);
                }
                for (int k = 0; k + 1 < pr.Count && !OpenTop(style); k++)
                {
                    Line(b, kit.scenery, ProfilePoint(fr, pr[k]), ProfilePoint(fr, pr[k + 1]), 3f);
                    Line(b, kit.glow, ProfilePoint(fr, pr[k]) + Vector3.down * 1.8f, ProfilePoint(fr, pr[k + 1]) + Vector3.down * 1.8f, 0.6f);
                }
            }

            foreach (var piece in pieces)
            {
                var go = new GameObject(ScenePiece);
                go.transform.SetParent(parent, false);
                piece.Build(go.transform, meshes, true);
            }
            b.Build(parent, meshes, style != SceneryStyle.Palace);
        }

        // A cone (for mountains): `sides` faces from the ring at `foot` up to the peak
        static void Cone(Batch b, Material m, Vector3 foot, float radius, float height, int sides)
        {
            Vector3 apex = foot + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = foot + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius, p1 = foot + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                b.Quad(m, p0, p1, apex, apex);
            }
        }

        // A pine tree: a trunk, three tiers of branches narrowing upward, a dusting of snow
        static void Pine(Batch b, Material needles, Material trunk, Material snow, Vector3 foot, float height)
        {
            b.Box(trunk, foot + Vector3.up * height * 0.15f, new Vector3(height * 0.06f, height * 0.3f, height * 0.06f), Quaternion.identity);
            for (int t = 0; t < 3; t++)
            {
                float w = height * (0.5f - t * 0.13f), y = height * (0.3f + t * 0.22f);
                var q = Quaternion.Euler(0f, 45f + t * 20f, 0f);
                b.Box(needles, foot + Vector3.up * y, new Vector3(w, height * 0.24f, w), q);
                b.Box(snow, foot + Vector3.up * (y + height * 0.125f), new Vector3(w * 0.8f, height * 0.03f, w * 0.8f), q);
            }
        }

        // A glass tower: a dark core in a glass skin, its corners and floors traced in neon
        static void Tower(Batch b, BiomeKit kit, Vector3 foot, float w, float d, float height, Quaternion q, Material neon)
        {
            Vector3 X = q * Vector3.right * (w * 0.5f), Z = q * Vector3.forward * (d * 0.5f), up = Vector3.up * height;
            b.Box(kit.scenery, foot + up * 0.5f, new Vector3(w * 0.9f, height, d * 0.9f), q);
            var corners = new[] { foot + X + Z, foot + X - Z, foot - X - Z, foot - X + Z };
            for (int i = 0; i < 4; i++)
            {
                Vector3 c0 = corners[i], c1 = corners[(i + 1) % 4];
                b.Quad(kit.shaft, c0, c1, c1 + up, c0 + up, uv: true);
                Line(b, neon, c0, c0 + up, 0.5f);
                for (float y = 12f; y < height; y += 24f) Line(b, kit.slab, c0 + Vector3.up * y, c1 + Vector3.up * y, 0.35f);
                Line(b, neon, c0 + up, c1 + up, 0.5f);
            }
        }

        // A wall across the room with a round-headed archway through it (from u0 to u1 across,
        // `spring` metres above the riding level before the arch starts, `rise` for the arch)
        static void ArchWall(Batch b, Material wall, Material trim, Frame fr, float u0, float u1, float spring, float rise)
        {
            Vector3 P(float u, float y) => Vector3.Lerp(fr.A, fr.B, u).WithY(fr.level + y);
            float bot = fr.bottom - fr.level, topY = fr.top - fr.level;
            b.Quad(wall, P(0f, bot), P(u0, bot), P(u0, topY), P(0f, topY));
            b.Quad(wall, P(u1, bot), P(1f, bot), P(1f, topY), P(u1, topY));
            const int n = 12;
            Vector3 last = P(u0, spring);
            for (int i = 1; i <= n; i++)
            {
                float t = (float)i / n, u = Mathf.Lerp(u0, u1, t);
                float y = spring + rise * Mathf.Sin(t * Mathf.PI);
                float t0 = (float)(i - 1) / n, uPrev = Mathf.Lerp(u0, u1, t0);
                b.Quad(wall, P(uPrev, spring + rise * Mathf.Sin(t0 * Mathf.PI)), P(u, y), P(u, topY), P(uPrev, topY));
                Vector3 here = P(u, y);
                Line(b, trim, last, here, 1.2f);
                last = here;
            }
            Line(b, trim, P(u0, bot), P(u0, spring), 1.2f);
            Line(b, trim, P(u1, bot), P(u1, spring), 1.2f);
        }

        static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);

        static int Index(RampShapes.RampPath path, float d)
        {
            int lo = 0, hi = path.distance.Count - 1;
            while (lo < hi)
            {
                int m = (lo + hi) / 2;
                if (path.distance[m] < d) lo = m + 1; else hi = m;
            }
            return lo;
        }

        // A slab from one point to another (their middles): lies along the slope and round the
        // curve, `width` across and `thick` through, a little longer than the gap so neighbours
        // overlap and bends close up. `vertical`: stands on edge instead (a strip on a wall).
        static void Slab(Batch b, Material m, Vector3 from, Vector3 to, float width, float thick, bool vertical = false)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            var rot = Quaternion.LookRotation(d, Vector3.up);
            var size = vertical ? new Vector3(thick, width, len + 1f) : new Vector3(width, thick, len + 1f);
            b.Box(m, (from + to) * 0.5f, size, rot);
        }

        // Where a point on one wall of a bay is: `u` along the bay, `y` above the riding level,
        // `inward` metres into the room
        static Vector3 OnWall(Frame last, Frame fr, bool sideA, float u, float y, float inward = 0f)
        {
            Vector3 a = sideA ? last.A : last.B, c = sideA ? fr.A : fr.B;
            Vector3 in0 = (((last.A + last.B) * 0.5f) - a).WithY(0f).normalized, in1 = (((fr.A + fr.B) * 0.5f) - c).WithY(0f).normalized;
            Vector3 inDir = Vector3.Lerp(in0, in1, u).normalized;
            return (Vector3.Lerp(a, c, u) + inDir * inward).WithY(Mathf.Lerp(last.level, fr.level, u) + y);
        }

        static Vector3 Inward(Frame last, Frame fr, bool sideA, float u) =>
            (OnWall(last, fr, sideA, u, 0f, 1f) - OnWall(last, fr, sideA, u, 0f)).WithY(0f).normalized;

        // Light falling in through a window: soft glowing planes slanting from the window's
        // edges down into the room, and a pool where it lands on the floor
        static void Shaft(Batch b, Material shaft, Material pool, Frame last, Frame fr, bool sideA, float u0, float u1, float lo, float hi)
        {
            if (!shaft) return;
            float um = (u0 + u1) * 0.5f;
            Vector3 inward = Inward(last, fr, sideA, um);
            float floorY = Mathf.Lerp(last.bottom, fr.bottom, um) - Mathf.Lerp(last.level, fr.level, um); // floor, relative to the riding level
            float drop = hi - floorY, reach = drop * 0.55f;
            foreach (float y in new[] { hi - 0.5f, (lo + hi) * 0.5f, lo + 0.5f })
            {
                Vector3 t0 = OnWall(last, fr, sideA, u0 + 0.03f, y, 0.4f), t1 = OnWall(last, fr, sideA, u1 - 0.03f, y, 0.4f);
                Vector3 down = inward * reach * ((y - floorY) / drop) + Vector3.down * (y - floorY);
                b.Quad(shaft, t0 + down, t1 + down, t1, t0, uv: true);
            }
            if (!pool) return;
            Vector3 land = OnWall(last, fr, sideA, um, floorY + 0.15f, reach * 0.8f);
            Vector3 along = (OnWall(last, fr, sideA, u1, 0f) - OnWall(last, fr, sideA, u0, 0f)).WithY(0f);
            Pool(b, pool, land, along.normalized, inward, along.magnitude * 1.1f, reach * 0.9f);
        }

        // A soft glowing patch (light on a floor or wall): centred on `c`, spanning `w` along
        // `u` and `h` along `v`
        static void Pool(Batch b, Material m, Vector3 c, Vector3 u, Vector3 v, float w, float h)
        {
            if (!m) return;
            Vector3 x = u * (w * 0.5f), y = v * (h * 0.5f);
            b.Quad(m, c - x - y, c + x - y, c + x + y, c - x + y, uv: true);
        }

        // A raised panel on a wall between `u0..u1` along the bay and `lo..hi` above the riding
        // level, framed by a trim moulding
        static void WallPanel(Batch b, Material face, Material trim, Frame last, Frame fr, bool sideA, float u0, float u1, float lo, float hi)
        {
            Vector3 P(float u, float y, float d) => OnWall(last, fr, sideA, u, y, d);
            b.Quad(face, P(u0, lo, 0.35f), P(u1, lo, 0.35f), P(u1, hi, 0.35f), P(u0, hi, 0.35f));
            if (!trim) return;
            const float t = 0.45f;
            Line(b, trim, P(u0, lo, 0.45f), P(u1, lo, 0.45f), t);
            Line(b, trim, P(u0, hi, 0.45f), P(u1, hi, 0.45f), t);
            Line(b, trim, P(u0, lo, 0.45f), P(u0, hi, 0.45f), t);
            Line(b, trim, P(u1, lo, 0.45f), P(u1, hi, 0.45f), t);
        }

        // Baseboard and crown moulding running the whole length of both walls
        static void Mouldings(Batch b, Material trim, Frame last, Frame fr)
        {
            foreach (bool sideA in new[] { true, false })
            {
                Vector3 b0 = OnWall(last, fr, sideA, 0f, last.bottom - last.level + 1.2f, 0.5f), b1 = OnWall(last, fr, sideA, 1f, fr.bottom - fr.level + 1.2f, 0.5f);
                Slab(b, trim, b0, b1, 1.2f, 2.4f, vertical: true);
                Vector3 c0 = OnWall(last, fr, sideA, 0f, last.top - last.level - 1.5f, 0.6f), c1 = OnWall(last, fr, sideA, 1f, fr.top - fr.level - 1.5f, 0.6f);
                Slab(b, trim, c0, c1, 1.2f, 1.6f, vertical: true);
            }
        }

        // A glowing line along both walls at `height` above the riding level, carried rib to rib
        // so it runs unbroken down the whole building
        static void WallLights(Batch b, Material m, Frame last, Frame fr, Vector3 lastMid, Vector3 mid, float height, float size)
        {
            foreach (bool sideA in new[] { true, false })
            {
                Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? fr.A : fr.B;
                Vector3 in0 = (lastMid - w0).WithY(0f).normalized * 0.5f, in1 = (mid - w1).WithY(0f).normalized * 0.5f;
                Slab(b, m, (w0 + in0).WithY(last.level + height), (w1 + in1).WithY(fr.level + height), size, size);
            }
        }

        // One wall of a bay, from the last rib's wall line to this rib's, floor to roof, stitched
        // through the ribs' own corners. With a window, the part between u0 and u1 along the bay
        // is split around an opening from `lo` to `hi` above the riding level, edged in `edge`.
        static void WallSide(Batch b, Material wall, Material edge, Frame last, Frame fr, bool sideA, bool window,
            float lo, float hi, float u0, float u1, float edgeWidth, Material mullion = null, int panesX = 1, int panesY = 1)
        {
            Vector3 a = sideA ? last.A : last.B, c = sideA ? fr.A : fr.B;
            Vector3 P(float u, float y0, float y1) => Vector3.Lerp(a, c, u).WithY(Mathf.Lerp(y0, y1, u));
            Vector3 Bot(float u) => P(u, last.bottom, fr.bottom);
            Vector3 Top(float u) => P(u, last.top, fr.top);
            Vector3 Lo(float u) => P(u, last.level + lo, fr.level + lo);
            Vector3 Hi(float u) => P(u, last.level + hi, fr.level + hi);
            if (!window || hi <= lo)
            {
                b.Quad(wall, Bot(0f), Bot(1f), Top(1f), Top(0f));
                return;
            }
            b.Quad(wall, Bot(0f), Bot(u0), Top(u0), Top(0f));
            b.Quad(wall, Bot(u1), Bot(1f), Top(1f), Top(u1));
            b.Quad(wall, Bot(u0), Bot(u1), Lo(u1), Lo(u0));
            b.Quad(wall, Hi(u0), Hi(u1), Top(u1), Top(u0));
            if (!edge || edgeWidth <= 0f) return;
            Line(b, edge, Lo(u0), Lo(u1), edgeWidth);
            Line(b, edge, Hi(u0), Hi(u1), edgeWidth);
            Line(b, edge, Lo(u0), Hi(u0), edgeWidth);
            Line(b, edge, Lo(u1), Hi(u1), edgeWidth);
            // Glazing bars: a grid of panes, like the windows in the white rooms
            if (!mullion) return;
            for (int i = 1; i < panesX; i++)
            {
                float u = Mathf.Lerp(u0, u1, (float)i / panesX);
                Line(b, mullion, Lo(u), Hi(u), edgeWidth * 0.5f);
            }
            for (int j = 1; j < panesY; j++)
            {
                float k = (float)j / panesY;
                Line(b, mullion, Vector3.Lerp(Lo(u0), Hi(u0), k), Vector3.Lerp(Lo(u1), Hi(u1), k), edgeWidth * 0.5f);
            }
        }

        static void Line(Batch b, Material m, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 0.01f) return;
            b.Box(m, (from + to) * 0.5f, new Vector3(thickness, thickness, d.magnitude + thickness), Quaternion.LookRotation(d));
        }

        // A rectangular frame standing across the building
        static void Portal(Batch b, Material m, Vector3 a, Vector3 c, float bottom, float top, Quaternion rot, float thickness)
        {
            foreach (var at in new[] { a, c })
                b.Box(m, new Vector3(at.x, (top + bottom) * 0.5f, at.z), new Vector3(thickness, top - bottom, thickness), rot);
            Vector3 mid = (a + c) * 0.5f;
            Vector3 across = (c - a).WithY(0f);
            var acrossRot = Quaternion.LookRotation(across.sqrMagnitude > 0.01f ? across : Vector3.right);
            b.Box(m, new Vector3(mid.x, top, mid.z), new Vector3(thickness, thickness, across.magnitude + thickness), acrossRot);
            b.Box(m, new Vector3(mid.x, bottom, mid.z), new Vector3(thickness, thickness, across.magnitude + thickness), acrossRot);
        }
    }
}
