using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow
{
    // The buildings the course runs through: each zone wraps its ramps in its own kind of
    // structure, the way the best-looking surf maps put you inside lit, designed spaces
    // instead of out on bare ramps. All original designs, built from boxes:
    //  - Cathedral (Crimson Hall): pointed-arch ribs, banded walls, a vaulted ceiling with
    //    spikes, chains hanging, a red line along the floor
    //  - Palace (Sky Palace): white columns and beams open to the sky, a gold line along the
    //    top, a sea of clouds far below
    //  - Rings (Neon Rings): no roof; giant glowing rings to fly through, platforms below
    //  - Grotto: a cave of tumbled rock over a glowing lake, with glowing mushrooms and
    //    crystals
    //  - Candy (Candy Blocks): lavender box rooms with ceiling lights, one after another
    //  - Forge: heavy orange frames, glowing window slits, a lava grid far below
    //  - Wire (Wireframe): a tunnel drawn only in glowing edges
    //
    // A building covers the middle of its ramp only: it opens before the landing and ends
    // before the launch, so every flight goes in and out through open ends. Nothing here has
    // colliders (the ramps are the only things you touch). Every piece of one material is
    // merged into a single mesh per ramp, so a whole building costs a handful of draw calls,
    // and it goes when its ramp does, so everything behind you de-renders.
    public static class Architecture
    {
        class Batch
        {
            readonly Dictionary<Material, List<CombineInstance>> parts = new();
            readonly Mesh cube;
            public Batch(Mesh cube) { this.cube = cube; }

            public void Box(Material m, Vector3 center, Vector3 size, Quaternion rotation)
            {
                if (!m) return;
                if (!parts.TryGetValue(m, out var list)) parts[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(center, rotation, size) });
            }

            public void Build(Transform parent, List<Mesh> meshes)
            {
                foreach (var (m, list) in parts)
                {
                    var mesh = new Mesh { name = "Architecture", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(list.ToArray(), true, true);
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
        }

        // Builds the zone's structure around `path` (course-local coordinates, like the ramp's)
        public static void Build(RampShapes.RampPath path, Biome biome, BiomeKit kit, Transform parent, List<Mesh> meshes, System.Random rng, Mesh cube)
        {
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var b = new Batch(cube);
            float length = path.Length;
            float from = Mathf.Min(length * 0.1f, 30f), to = Mathf.Min(length * 0.88f, length - 25f);
            if (to - from < 30f) return;

            // How far out the walls stand, either side of the ridge: clear of the whole face
            // (a slab's face hangs on one side only), and how high the roof is
            bool prism = path.kind == RampShapes.Kind.Prism;
            float near = prism ? path.width + 12f : 9f, far = path.width + 12f;
            float roof = 18f, depth = path.Depth + 24f;

            float step = biome.style switch
            {
                SceneryStyle.Rings => 42f,
                SceneryStyle.Candy => 8f,
                SceneryStyle.Grotto => 9f,
                _ => 12f,
            };
            int ribs = 0;
            Vector3? lastP = null;
            Vector3 lastA = default, lastB = default;
            for (float d = from; d <= to; d += step, ribs++)
            {
                int k = Index(path, d);
                Vector3 p = path.ridge[k], f = path.forward[k], right = path.right[k];
                // Offsets across the ramp, from the non-face side to the face side
                float s = prism ? 1f : path.side;
                float offA = -near * s, offB = far * s;
                var rot = Quaternion.LookRotation(f, Vector3.up);
                Vector3 A = p + right * offA, B = p + right * offB;
                float top = p.y + roof, bottom = p.y - depth, h = top - bottom;
                Vector3 mid = (A + B) * 0.5f;
                float span = Mathf.Abs(offB - offA);

                switch (biome.style)
                {
                    case SceneryStyle.Cathedral:
                    {
                        // Pillars and a pointed arch meeting over the middle
                        foreach (var at in new[] { A, B })
                            b.Box(kit.scenery, new Vector3(at.x, (top + bottom) * 0.5f, at.z), new Vector3(2.2f, h, 2.2f), rot);
                        Vector3 apex = new(mid.x, top + span * 0.28f, mid.z);
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 foot = new(at.x, top, at.z);
                            b.Box(kit.scenery, (foot + apex) * 0.5f, new Vector3(1.6f, 1.6f, (apex - foot).magnitude + 1f), Quaternion.LookRotation(apex - foot, f));
                        }
                        // A red glow up the inside of each pillar
                        foreach (var at in new[] { A, B })
                            b.Box(kit.glow, new Vector3(at.x, p.y - 2f, at.z) + (mid - at).normalized * 1.2f, new Vector3(0.3f, h * 0.6f, 0.3f), rot);
                        if (lastP is Vector3 q)
                        {
                            // Banded walls, a roof with a spine of spikes, a red floor line
                            Vector3 qa = lastA, qb = lastB;
                            Panel(b, kit.scenery, qa, A, bottom, top, 1f);
                            Panel(b, kit.scenery, qb, B, bottom, top, 1f);
                            Vector3 roofMid = ((qa + qb) * 0.5f + mid) * 0.5f;
                            b.Box(kit.slab, new Vector3(roofMid.x, top + span * 0.3f, roofMid.z), new Vector3(span, 1f, (mid - (qa + qb) * 0.5f).magnitude + 0.5f), rot);
                            for (int n = 0; n < 3; n++)
                            {
                                Vector3 spike = Vector3.Lerp((qa + qb) * 0.5f, mid, (n + 0.5f) / 3f) + right * Rand(-span * 0.3f, span * 0.3f);
                                b.Box(kit.glowAlt, new Vector3(spike.x, top + Rand(-1f, 4f), spike.z), new Vector3(0.7f, Rand(3f, 6f), 0.7f), Quaternion.Euler(45f, Rand(0f, 90f), 45f));
                            }
                            b.Box(kit.glow, new Vector3(((q + p) * 0.5f).x, bottom + 0.5f, ((q + p) * 0.5f).z), new Vector3(0.8f, 0.4f, (p - q).magnitude), rot);
                        }
                        // Chains hanging near the walls
                        if (ribs % 2 == 1)
                            foreach (var at in new[] { A, B })
                            {
                                Vector3 c = at + (mid - at).normalized * 3.5f;
                                b.Box(kit.slab, new Vector3(c.x, top - 7f, c.z), new Vector3(0.35f, 14f, 0.35f), rot);
                            }
                        break;
                    }
                    case SceneryStyle.Palace:
                    {
                        // White columns with capitals and a beam across; open to the sky
                        foreach (var at in new[] { A, B })
                        {
                            b.Box(kit.scenery, new Vector3(at.x, (top + bottom) * 0.5f, at.z), new Vector3(1.8f, h, 1.8f), rot);
                            b.Box(kit.scenery, new Vector3(at.x, top - 0.5f, at.z), new Vector3(3f, 1f, 3f), rot);
                        }
                        b.Box(kit.scenery, new Vector3(mid.x, top + 0.8f, mid.z), new Vector3(span + 3f, 1.6f, 1.6f), rot);
                        if (lastP is Vector3 q)
                        {
                            Vector3 qa = lastA, qb = lastB;
                            // Gold lines along both top edges, roof slats, a floor far below
                            Line(b, kit.glow, new Vector3(qa.x, top + 1.8f, qa.z), new Vector3(A.x, top + 1.8f, A.z), 0.35f);
                            Line(b, kit.glow, new Vector3(qb.x, top + 1.8f, qb.z), new Vector3(B.x, top + 1.8f, B.z), 0.35f);
                            Vector3 c = ((qa + qb) * 0.5f + mid) * 0.5f;
                            b.Box(kit.scenery, new Vector3(c.x, top + 1.6f, c.z), new Vector3(span, 0.4f, 1.2f), rot);
                            // Clouds far below
                            if (ribs % 2 == 0)
                            {
                                Vector3 cloud = c + right * Rand(-60f, 60f);
                                b.Box(kit.scenery, new Vector3(cloud.x, bottom - Rand(20f, 45f), cloud.z), new Vector3(Rand(30f, 70f), Rand(4f, 9f), Rand(25f, 50f)), Quaternion.Euler(0f, Rand(0f, 360f), 0f));
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Rings:
                    {
                        // A ring to fly through, standing across the ramp, and a platform below
                        Vector3 center = new(mid.x, p.y + 2f, mid.z);
                        float radius = span * 0.5f + 4f;
                        Material m = ribs % 2 == 0 ? kit.glow : kit.glowAlt;
                        const int blocks = 24;
                        for (int n = 0; n < blocks; n++)
                        {
                            float a0 = n * Mathf.PI * 2f / blocks, a1 = (n + 1) * Mathf.PI * 2f / blocks;
                            Vector3 p0 = center + (right * Mathf.Cos(a0) + Vector3.up * Mathf.Sin(a0)) * radius;
                            Vector3 p1 = center + (right * Mathf.Cos(a1) + Vector3.up * Mathf.Sin(a1)) * radius;
                            b.Box(m, (p0 + p1) * 0.5f, new Vector3(1.1f, 1.1f, (p1 - p0).magnitude + 0.2f), Quaternion.LookRotation(p1 - p0, f));
                        }
                        Vector3 plat = center + right * Rand(-radius, radius);
                        float py = bottom - Rand(5f, 25f);
                        b.Box(kit.scenery, new Vector3(plat.x, py, plat.z), new Vector3(16f, 1.5f, 16f), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                        b.Box(kit.glowAlt, new Vector3(plat.x, py + 0.8f, plat.z), new Vector3(16.4f, 0.2f, 16.4f), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                        break;
                    }
                    case SceneryStyle.Grotto:
                    {
                        // Rock tumbled up both walls and across the roof
                        foreach (var (at, dir) in new[] { (A, -1f), (B, 1f) })
                            for (int n = 0; n < 3; n++)
                            {
                                float y = Mathf.Lerp(bottom + 4f, top, n / 2f) + Rand(-3f, 3f);
                                Vector3 c = at + right * (s * dir * Rand(0f, 5f));
                                b.Box(kit.scenery, new Vector3(c.x, y, c.z), new Vector3(Rand(6f, 12f), Rand(7f, 13f), Rand(8f, 14f)), Quaternion.Euler(Rand(-30f, 30f), Rand(0f, 360f), Rand(-30f, 30f)));
                            }
                        Vector3 rc = mid + right * Rand(-span * 0.3f, span * 0.3f);
                        b.Box(kit.scenery, new Vector3(rc.x, top + Rand(1f, 5f), rc.z), new Vector3(Rand(10f, 18f), Rand(4f, 8f), Rand(10f, 16f)), Quaternion.Euler(Rand(-20f, 20f), Rand(0f, 360f), Rand(-20f, 20f)));
                        // The glowing lake, and stalactites dripping from the roof
                        b.Box(kit.glow, new Vector3(mid.x, bottom, mid.z), new Vector3(span + 14f, 0.3f, step + 1f), rot);
                        if (rng.NextDouble() < 0.6)
                        {
                            Vector3 st = mid + right * Rand(-span * 0.45f, span * 0.45f);
                            b.Box(kit.scenery, new Vector3(st.x, top - 3f, st.z), new Vector3(1.4f, 7f, 1.4f), Quaternion.Euler(Rand(-8f, 8f), 45f, Rand(-8f, 8f)));
                        }
                        // Glowing mushrooms low on the walls, crystals higher up
                        foreach (var (at, dir) in new[] { (A, -1f), (B, 1f) })
                        {
                            if (rng.NextDouble() < 0.5)
                            {
                                Vector3 m = at + right * (s * -dir * Rand(1f, 3f));
                                float y = bottom + Rand(3f, 10f), stem = Rand(1.5f, 3f);
                                b.Box(kit.slab, new Vector3(m.x, y + stem * 0.5f, m.z), new Vector3(0.5f, stem, 0.5f), rot);
                                b.Box(kit.glow, new Vector3(m.x, y + stem, m.z), new Vector3(Rand(1.8f, 3f), 0.6f, Rand(1.8f, 3f)), Quaternion.Euler(0f, 45f, 0f));
                            }
                            if (rng.NextDouble() < 0.35)
                            {
                                Vector3 c = at + right * (s * -dir * Rand(0f, 2f));
                                b.Box(kit.glowAlt, new Vector3(c.x, p.y + Rand(4f, 12f), c.z), new Vector3(0.9f, Rand(3f, 6f), 0.9f), Quaternion.Euler(Rand(-35f, 35f), Rand(0f, 90f), Rand(-35f, 35f)));
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Candy:
                    {
                        // Box rooms 32m long with 8m gaps between them: walls, roof and floor in
                        // flat lavender, square lights down the roof
                        bool inRoom = (ribs % 5) != 4;
                        if (inRoom && lastP is Vector3 q && (ribs % 5) != 0)
                        {
                            Vector3 qa = lastA, qb = lastB;
                            Panel(b, kit.scenery, qa, A, bottom, top, 1.2f);
                            Panel(b, kit.scenery, qb, B, bottom, top, 1.2f);
                            Vector3 c = ((qa + qb) * 0.5f + mid) * 0.5f;
                            float len = (mid - (qa + qb) * 0.5f).magnitude + 0.3f;
                            b.Box(kit.scenery, new Vector3(c.x, top, c.z), new Vector3(span, 1.2f, len), rot);
                            b.Box(kit.scenery, new Vector3(c.x, bottom, c.z), new Vector3(span, 1.2f, len), rot);
                            b.Box(kit.glow, new Vector3(c.x, top - 0.7f, c.z), new Vector3(3.2f, 0.3f, 3.2f), rot * Quaternion.Euler(0f, 45f, 0f));
                            b.Box(kit.glowAlt, new Vector3(c.x, top - 0.7f, c.z), new Vector3(2.2f, 0.32f, 2.2f), rot);
                        }
                        // A doorframe glowing at each end of a room
                        if ((ribs % 5) == 0 || (ribs % 5) == 3)
                            Frame(b, kit.glowAlt, A, B, bottom, top, rot, 0.8f);
                        // Floating blocks in flat saturated colors outside
                        if (ribs % 3 == 0)
                        {
                            var mats = new[] { kit.ramp, kit.slab, kit.glow, kit.glowAlt };
                            Vector3 o = mid + right * (Rand(0f, 1f) < 0.5f ? -1f : 1f) * Rand(span * 0.5f + 20f, span * 0.5f + 70f);
                            b.Box(mats[rng.Next(mats.Length)], new Vector3(o.x, p.y + Rand(-40f, 30f), o.z), new Vector3(Rand(6f, 22f), Rand(6f, 30f), Rand(6f, 22f)), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                        }
                        break;
                    }
                    case SceneryStyle.Forge:
                    {
                        // Heavy square frames, walls with glowing slits, a lava grid below
                        Frame(b, kit.scenery, A, B, bottom, top, rot, 2.4f);
                        if (lastP is Vector3 q)
                        {
                            Vector3 qa = lastA, qb = lastB;
                            Panel(b, kit.scenery, qa, A, bottom, top, 1.4f);
                            Panel(b, kit.scenery, qb, B, bottom, top, 1.4f);
                            foreach (var (w0, w1, inward) in new[] { (qa, A, 1f), (qb, B, -1f) })
                            {
                                Vector3 wm = (w0 + w1) * 0.5f + (mid - w1).normalized * 0.8f;
                                b.Box(kit.glowAlt, new Vector3(wm.x, p.y + 6f, wm.z), new Vector3(0.3f, 1f, (w1 - w0).magnitude * 0.6f), rot);
                            }
                            Vector3 c = ((qa + qb) * 0.5f + mid) * 0.5f;
                            b.Box(kit.slab, new Vector3(c.x, top + 0.8f, c.z), new Vector3(span, 1f, (mid - (qa + qb) * 0.5f).magnitude + 0.3f), rot);
                            // Lava grid: glowing bars across and along, over the dark floor
                            b.Box(kit.glow, new Vector3(c.x, bottom, c.z), new Vector3(span, 0.5f, 0.6f), rot);
                            for (int n = -2; n <= 2; n++)
                            {
                                Vector3 g = c + right * (n * span * 0.2f);
                                b.Box(kit.glow, new Vector3(g.x, bottom, g.z), new Vector3(0.6f, 0.5f, step), rot);
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Wire:
                    {
                        // Glowing edges only: a frame, and lines running on to the next one
                        Material m = ribs % 2 == 0 ? kit.glow : kit.glowAlt;
                        Frame(b, m, A, B, p.y - path.Depth - 6f, top, rot, 0.35f);
                        if (lastP is Vector3 q)
                        {
                            Vector3 qa = lastA, qb = lastB;
                            float qTop = q.y + roof, qBottom = q.y - path.Depth - 6f;
                            foreach (var (w0, w1) in new[] { (qa, A), (qb, B) })
                            {
                                Line(b, kit.glow, new Vector3(w0.x, qTop, w0.z), new Vector3(w1.x, top, w1.z), 0.3f);
                                Line(b, kit.glow, new Vector3(w0.x, qBottom, w0.z), new Vector3(w1.x, p.y - path.Depth - 6f, w1.z), 0.3f);
                            }
                        }
                        break;
                    }
                }
                lastP = p;
                lastA = A;
                lastB = B;
            }
            b.Build(parent, meshes);
        }

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

        // A wall panel from one rib to the next, floor to roof
        static void Panel(Batch b, Material m, Vector3 a, Vector3 c, float bottom, float top, float thickness)
        {
            Vector3 along = c - a;
            along.y = 0f;
            if (along.sqrMagnitude < 0.01f) return;
            Vector3 center = (a + c) * 0.5f;
            b.Box(m, new Vector3(center.x, (top + bottom) * 0.5f, center.z), new Vector3(thickness, top - bottom, along.magnitude + 0.4f), Quaternion.LookRotation(along));
        }

        // A bar from one point to another
        static void Line(Batch b, Material m, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 0.01f) return;
            b.Box(m, (from + to) * 0.5f, new Vector3(thickness, thickness, d.magnitude + thickness), Quaternion.LookRotation(d));
        }

        // A rectangular frame standing across the ramp
        static void Frame(Batch b, Material m, Vector3 a, Vector3 c, float bottom, float top, Quaternion rot, float thickness)
        {
            foreach (var at in new[] { a, c })
                b.Box(m, new Vector3(at.x, (top + bottom) * 0.5f, at.z), new Vector3(thickness, top - bottom, thickness), rot);
            Vector3 mid = (a + c) * 0.5f;
            Vector3 across = c - a;
            across.y = 0f;
            var acrossRot = Quaternion.LookRotation(across.sqrMagnitude > 0.01f ? across : Vector3.right);
            b.Box(m, new Vector3(mid.x, top, mid.z), new Vector3(thickness, thickness, across.magnitude + thickness), acrossRot);
            b.Box(m, new Vector3(mid.x, bottom, mid.z), new Vector3(thickness, thickness, across.magnitude + thickness), acrossRot);
        }
    }
}
