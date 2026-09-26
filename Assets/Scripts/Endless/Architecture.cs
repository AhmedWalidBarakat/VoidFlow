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
    // where one zone gives way to the next. The Sky Palace and Neon Rings stay open, as the
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

        public static bool Continuous(SceneryStyle s) => s is not (SceneryStyle.Palace or SceneryStyle.Rings);

        public static float StepFor(SceneryStyle s) => s switch
        {
            SceneryStyle.Rings => 42f,
            SceneryStyle.Candy => 10f,
            SceneryStyle.Grotto => 9f,
            _ => 12f,
        };

        const float Roof = 32f, Margin = 30f; // roomy: you can air-strafe a long way and stay inside

        // How far the floor lies below the ramp: the rooms after Raphaelo have theirs close by
        // (you see it), the rest drop away into depth
        public static float DepthFor(SceneryStyle s) => s is SceneryStyle.Sunset or SceneryStyle.Gallery ? 12f : 30f;

        // Cross-sections along a ramp, between two distances along it
        public static List<Frame> RampFrames(RampShapes.RampPath path, float from, float to, float step, float depth = 24f)
        {
            var frames = new List<Frame>();
            bool prism = path.kind == RampShapes.Kind.Prism;
            // Walls stand clear of the whole face (a slab's face hangs on one side only)
            float s = prism ? 1f : path.side;
            float near = prism ? path.width + Margin : Margin * 0.7f, far = path.width + Margin;
            // Ribs every `step`, and always one exactly at the end, so the building meets the
            // next piece with no hole
            int count = Mathf.Max(1, Mathf.CeilToInt((to - from) / step - 0.01f));
            for (int n = 0; n <= count; n++)
            {
                float d = Mathf.Lerp(from, to, (float)n / count);
                int k = Index(path, Mathf.Min(d, path.Length));
                Vector3 p = path.ridge[k], right = path.right[k];
                frames.Add(new Frame
                {
                    p = p, f = path.forward[k], right = right,
                    A = p + right * (-near * s), B = p + right * (far * s),
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
                    top = Mathf.Max(Mathf.Lerp(from.top, to.top, u), level + 24f),
                    bottom = Mathf.Min(Mathf.Lerp(from.bottom, to.bottom, u), level - 16f),
                });
            }
            return frames;
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
            readonly Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<int> t)> quads = new();
            readonly Mesh cube;
            public Batch(Mesh cube) { this.cube = cube; }

            public void Box(Material m, Vector3 center, Vector3 size, Quaternion rotation)
            {
                if (!m || size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f) return;
                if (!parts.TryGetValue(m, out var list)) parts[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(center, rotation, size) });
            }

            // A four-cornered face (a, b, c, d in order round it), showing from both sides
            public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                if (!m) return;
                Vector3 normal = Vector3.Cross(b - a, d - a) + Vector3.Cross(d - c, b - c);
                if (normal.sqrMagnitude < 1e-6f) return;
                normal.Normalize();
                if (!quads.TryGetValue(m, out var q)) quads[m] = q = (new List<Vector3>(), new List<Vector3>(), new List<int>());
                foreach (float s in new[] { 1f, -1f })
                {
                    int i = q.v.Count;
                    q.v.AddRange(new[] { a, b, c, d });
                    for (int k = 0; k < 4; k++) q.n.Add(normal * s);
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
                        shell.SetUVs(0, new Vector2[q.v.Count]);
                        shell.SetTriangles(q.t, 0);
                        list.Add(new CombineInstance { mesh = shell, transform = Matrix4x4.identity });
                    }
                    var mesh = new Mesh { name = "Architecture", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(list.ToArray(), true, true);
                    if (shell) WeaponBuilder.Kill(shell);
                    if (worldUv) WorldUvs(mesh);
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
        public static void Build(List<Frame> frames, Biome biome, BiomeKit kit, Transform parent, List<Mesh> meshes,
            System.Random rng, Mesh cube, int doorway = -1)
        {
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var b = new Batch(cube);
            Material floor = kit.floor ? kit.floor : kit.slab;

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
                float lastSpan = Vector3.Distance(last.A.WithY(0f), last.B.WithY(0f));

                // The shell between the last rib and this one: both walls, roof (flat or pitched
                // up to a ridge) and floor, stitched through the ribs' own corner points so it
                // is watertight. A window leaves an opening in a wall between `lo` and `hi`
                // above the riding level, over the middle part of the bay (u0..u1), edged in
                // `edge`.
                void Shell(Material wall, Material roof, Material fl, float rise, bool winA, bool winB,
                    float lo, float hi, float u0, float u1, Material edge, float edgeWidth)
                {
                    WallSide(b, wall, edge, last, fr, true, winA, lo, hi, u0, u1, edgeWidth);
                    WallSide(b, wall, edge, last, fr, false, winB, lo, hi, u0, u1, edgeWidth);
                    Vector3 tl0 = last.A.WithY(last.top), tl1 = A.WithY(top), tr0 = last.B.WithY(last.top), tr1 = B.WithY(top);
                    if (rise > 0f)
                    {
                        Vector3 r0 = lastMid.WithY(last.top + lastSpan * rise), r1 = mid.WithY(top + span * rise);
                        b.Quad(roof, tl0, tl1, r1, r0);
                        b.Quad(roof, r0, r1, tr1, tr0);
                    }
                    else b.Quad(roof, tl0, tl1, tr1, tr0);
                    if (fl) b.Quad(fl, last.A.WithY(last.bottom), A.WithY(bottom), B.WithY(bottom), last.B.WithY(last.bottom));
                }

                switch (biome.style)
                {
                    case SceneryStyle.Cathedral:
                    {
                        const float rise = 0.22f;
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 inward = (mid - at).WithY(0f).normalized;
                            b.Box(kit.slab, (at + inward * 1.4f).WithY((top + bottom) * 0.5f), new Vector3(3f, h, 3f), rot);
                            b.Box(kit.glow, (at + inward * 3.1f).WithY(level - 2f), new Vector3(0.4f, h * 0.6f, 0.4f), rot);
                        }
                        // Arched rib across the vault
                        Vector3 apex = mid.WithY(top + span * rise);
                        foreach (var at in new[] { A, B })
                            Line(b, kit.slab, at.WithY(top), apex, 2f);
                        if (hasLast)
                        {
                            bool window = ribs % 3 == 1;
                            Shell(kit.scenery, kit.slab, floor, rise, window, window, 4f, 22f, 0.38f, 0.62f, kit.glow, 0.5f);
                            // Spikes down the ridge, the red floor line
                            Vector3 r0 = lastMid.WithY(last.top + lastSpan * rise);
                            for (int n = 0; n < 2; n++)
                                b.Box(kit.glowAlt, Vector3.Lerp(r0, apex, (n + 0.5f) / 2f) + Vector3.down * Rand(2f, 4f), new Vector3(1f, Rand(4f, 8f), 1f), Quaternion.Euler(45f, Rand(0f, 90f), 45f));
                            Slab(b, kit.glow, lastMid.WithY(last.bottom + 0.6f), mid.WithY(bottom + 0.6f), 1.2f, 0.4f);
                        }
                        if (ribs % 2 == 1)
                            foreach (var at in new[] { A, B })
                            {
                                Vector3 ch = at + (mid - at).WithY(0f).normalized * 6f;
                                b.Box(kit.slab, ch.WithY(top - 10f), new Vector3(0.5f, 20f, 0.5f), rot);
                            }
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
                        Vector3 center = mid.WithY(level + 2f);
                        float radius = span * 0.5f + 4f;
                        Material m = ribs % 2 == 0 ? kit.glow : kit.glowAlt;
                        const int blocks = 40;
                        for (int n = 0; n < blocks; n++)
                        {
                            float a0 = n * Mathf.PI * 2f / blocks, a1 = (n + 1) * Mathf.PI * 2f / blocks;
                            Vector3 p0 = center + (right * Mathf.Cos(a0) + Vector3.up * Mathf.Sin(a0)) * radius;
                            Vector3 p1 = center + (right * Mathf.Cos(a1) + Vector3.up * Mathf.Sin(a1)) * radius;
                            b.Box(m, (p0 + p1) * 0.5f, new Vector3(1.4f, 1.4f, (p1 - p0).magnitude + 0.6f), Quaternion.LookRotation(p1 - p0, f));
                        }
                        Vector3 plat = center + right * Rand(-radius, radius);
                        float py = bottom - Rand(5f, 25f);
                        var platRot = Quaternion.Euler(0f, Rand(0f, 90f), 0f);
                        b.Box(kit.scenery, plat.WithY(py), new Vector3(16f, 1.5f, 16f), platRot);
                        b.Box(kit.glowAlt, plat.WithY(py + 0.8f), new Vector3(16.4f, 0.2f, 16.4f), platRot);
                        break;
                    }
                    case SceneryStyle.Grotto:
                    {
                        if (hasLast)
                        {
                            Shell(kit.scenery, kit.scenery, kit.glow, 0.12f, false, false, 0f, 0f, 0f, 0f, null, 0f);
                            // A crack of light in the roof now and then
                            if (ribs % 7 == 3) Slab(b, kit.glowAlt, lastMid.WithY(last.top + lastSpan * 0.12f - 0.4f), mid.WithY(top + span * 0.12f - 0.4f), 1.5f, 0.3f);
                        }
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 inward = (mid - at).WithY(0f).normalized;
                            for (int n = 0; n < 3; n++)
                            {
                                float y = Mathf.Lerp(bottom + 4f, top - 3f, n / 2f) + Rand(-3f, 3f);
                                b.Box(kit.scenery, (at + inward * Rand(1f, 5f)).WithY(y), new Vector3(Rand(8f, 14f), Rand(7f, 13f), Rand(9f, 15f)), Quaternion.Euler(Rand(-30f, 30f), Rand(0f, 360f), Rand(-30f, 30f)));
                            }
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
                        if (rng.NextDouble() < 0.7)
                        {
                            Vector3 st = mid + right * Rand(-span * 0.4f, span * 0.4f);
                            b.Box(kit.scenery, st.WithY(top - 4f), new Vector3(2f, 9f, 2f), Quaternion.Euler(Rand(-8f, 8f), 45f, Rand(-8f, 8f)));
                        }
                        break;
                    }
                    case SceneryStyle.Candy:
                    {
                        if (hasLast)
                        {
                            bool window = ribs % 5 == 2;
                            Shell(kit.scenery, kit.scenery, kit.scenery, 0f, window, window, 4f, 16f, 0.25f, 0.75f, kit.glowAlt, 0.6f);
                            Vector3 cc = (lastMid + mid) * 0.5f;
                            float cTop = (last.top + top) * 0.5f;
                            var bay = Quaternion.LookRotation((mid - lastMid).WithY(0f).sqrMagnitude > 0.01f ? (mid - lastMid).WithY(0f) : f);
                            b.Box(kit.glow, cc.WithY(cTop - 0.7f), new Vector3(5f, 0.3f, 5f), bay * Quaternion.Euler(0f, 45f, 0f));
                            b.Box(kit.glowAlt, cc.WithY(cTop - 0.7f), new Vector3(3.4f, 0.32f, 3.4f), bay);
                        }
                        if (ribs % 5 == 0) Portal(b, kit.glowAlt, A, B, bottom, top, rot, 1.2f);
                        if (ribs % 3 == 0)
                        {
                            var mats = new[] { kit.ramp, kit.slab, kit.glow, kit.glowAlt };
                            Vector3 o = mid + right * (rng.NextDouble() < 0.5 ? -1f : 1f) * Rand(span * 0.5f + 25f, span * 0.5f + 80f);
                            b.Box(mats[rng.Next(mats.Length)], o.WithY(level + Rand(-40f, 30f)), new Vector3(Rand(6f, 22f), Rand(6f, 30f), Rand(6f, 22f)), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                        }
                        break;
                    }
                    case SceneryStyle.Forge:
                    {
                        Portal(b, kit.slab, A, B, bottom, top, rot, 3f);
                        if (hasLast)
                        {
                            Shell(kit.scenery, kit.slab, null, 0f, false, false, 0f, 0f, 0f, 0f, null, 0f);
                            foreach (bool sideA in new[] { true, false })
                            {
                                Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? A : B;
                                Vector3 in0 = (lastMid - w0).WithY(0f).normalized * 0.6f, in1 = (mid - w1).WithY(0f).normalized * 0.6f;
                                Vector3 s0 = (w0 + in0).WithY(last.level + 8f), s1 = (w1 + in1).WithY(level + 8f);
                                Slab(b, kit.glowAlt, Vector3.Lerp(s0, s1, 0.2f), Vector3.Lerp(s0, s1, 0.8f), 0.4f, 1.5f, vertical: true);
                            }
                            if (ribs % 3 == 0) Slab(b, kit.glow, lastMid.WithY(last.top - 0.3f), mid.WithY(top - 0.3f), span * 0.4f, 0.2f);
                            // The lava floor: a glowing sea with dark grid bars over it
                            b.Quad(kit.glow, last.A.WithY(last.bottom), A.WithY(bottom), B.WithY(bottom), last.B.WithY(last.bottom));
                            for (int n = -3; n <= 3; n++)
                            {
                                Vector3 o0 = last.right * (n * lastSpan / 7f), o1 = right * (n * span / 7f);
                                Slab(b, kit.slab, (lastMid + o0).WithY(last.bottom + 0.4f), (mid + o1).WithY(bottom + 0.4f), 0.8f, 0.6f);
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Wire:
                    {
                        Material m = ribs % 2 == 0 ? kit.glow : kit.glowAlt;
                        float wireBottom = Mathf.Max(bottom, level - 20f);
                        Portal(b, m, A, B, wireBottom, top, rot, 0.45f);
                        if (hasLast)
                        {
                            float lastWire = Mathf.Max(last.bottom, last.level - 20f);
                            foreach (var (w0, w1) in new[] { (last.A, A), (last.B, B) })
                            {
                                Line(b, kit.glow, w0.WithY(last.top), w1.WithY(top), 0.4f);
                                Line(b, kit.glow, w0.WithY(lastWire), w1.WithY(wireBottom), 0.4f);
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Gallery:
                    {
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 pil = at + (mid - at).WithY(0f).normalized * 1.6f;
                            b.Box(kit.slab, pil.WithY((top + bottom) * 0.5f), new Vector3(3.2f, h, 3.2f), rot);
                        }
                        if (hasLast)
                        {
                            int side = (ribs / 3) % 2;
                            bool winA = ribs % 3 == 1 && side == 0, winB = ribs % 3 == 1 && side == 1;
                            Shell(kit.scenery, kit.scenery, floor, 0f, winA, winB, -2f, 20f, 0.15f, 0.85f, kit.slab, 0.8f);
                            foreach (var (sideA, win) in new[] { (true, winA), (false, winB) })
                            {
                                Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? A : B;
                                Vector3 in0 = (lastMid - w0).WithY(0f).normalized, in1 = (mid - w1).WithY(0f).normalized;
                                if (!win)
                                {
                                    Vector3 sc = Vector3.Lerp((w0 + in0 * 0.6f).WithY(last.level + 6f), (w1 + in1 * 0.6f).WithY(level + 6f), 0.5f);
                                    b.Box(kit.glow, sc, new Vector3(0.6f, 2.2f, 1.2f), Quaternion.LookRotation((w1 - w0).WithY(0f)));
                                }
                                Slab(b, kit.glowAlt, (w0 + in0 * 1.6f).WithY(last.bottom + 0.8f), (w1 + in1 * 1.6f).WithY(bottom + 0.8f), 0.8f, 0.2f);
                            }
                            Slab(b, kit.glowAlt, lastMid.WithY(last.top - 0.3f), mid.WithY(top - 0.3f), 3f, 0.2f);
                        }
                        break;
                    }
                    case SceneryStyle.Sunset:
                    {
                        if (hasLast)
                        {
                            int side = (ribs / 4) % 2;
                            bool winA = ribs % 4 == 2 && side == 0, winB = ribs % 4 == 2 && side == 1;
                            Shell(kit.scenery, kit.slab, floor, 0f, winA, winB, -4f, 18f, 0.1f, 0.9f, kit.glow, 1.2f);
                            foreach (bool sideA in new[] { true, false })
                            {
                                Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? A : B;
                                Vector3 in0 = (lastMid - w0).WithY(0f).normalized, in1 = (mid - w1).WithY(0f).normalized;
                                // The sloped foot of the wall, leaning into the room: a band of
                                // quads from the wall down to the floor, stitched like the shell
                                Vector3 a0 = w0.WithY(last.bottom + 9f), a1 = w1.WithY(bottom + 9f);
                                Vector3 c0 = (w0 + in0 * 6f).WithY(last.bottom), c1 = (w1 + in1 * 6f).WithY(bottom);
                                b.Quad(kit.scenery, a0, a1, c1, c0);
                                Slab(b, kit.glowAlt, (w0 + in0 * 0.6f).WithY(last.top - 1.5f), (w1 + in1 * 0.6f).WithY(top - 1.5f), 0.5f, 0.6f);
                            }
                        }
                        break;
                    }
                }
            }

            // The doorway where this zone begins: a heavy frame in its colors
            if (doorway >= 0 && doorway < frames.Count)
            {
                var fr = frames[doorway];
                var rot = Quaternion.LookRotation(fr.f, Vector3.up);
                Portal(b, kit.scenery, fr.A, fr.B, fr.bottom, fr.top, rot, 3f);
                Vector3 inA = fr.A + (fr.B - fr.A).WithY(0f).normalized * 1.6f, inB = fr.B + (fr.A - fr.B).WithY(0f).normalized * 1.6f;
                Portal(b, kit.glow, inA, inB, fr.bottom + 1.6f, fr.top - 1.6f, rot, 0.5f);
            }

            b.Build(parent, meshes, biome.style != SceneryStyle.Palace);
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

        // One wall of a bay, from the last rib's wall line to this rib's, floor to roof, stitched
        // through the ribs' own corners. With a window, the part between u0 and u1 along the bay
        // is split around an opening from `lo` to `hi` above the riding level, edged in `edge`.
        static void WallSide(Batch b, Material wall, Material edge, Frame last, Frame fr, bool sideA, bool window,
            float lo, float hi, float u0, float u1, float edgeWidth)
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
