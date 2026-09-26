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
            SceneryStyle.Candy => 8f,
            SceneryStyle.Grotto => 9f,
            _ => 12f,
        };

        const float Roof = 18f, Margin = 12f;

        // How far the floor lies below the ramp: the rooms after Raphaelo have theirs close by
        // (you see it), the rest drop away into depth
        public static float DepthFor(SceneryStyle s) => s is SceneryStyle.Sunset or SceneryStyle.Gallery ? 8f : 24f;

        // Cross-sections along a ramp, between two distances along it
        public static List<Frame> RampFrames(RampShapes.RampPath path, float from, float to, float step, float depth = 24f)
        {
            var frames = new List<Frame>();
            bool prism = path.kind == RampShapes.Kind.Prism;
            // Walls stand clear of the whole face (a slab's face hangs on one side only)
            float s = prism ? 1f : path.side;
            float near = prism ? path.width + Margin : 9f, far = path.width + Margin;
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
                    top = Mathf.Max(Mathf.Lerp(from.top, to.top, u), level + Margin),
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
            readonly Mesh cube;
            public Batch(Mesh cube) { this.cube = cube; }

            public void Box(Material m, Vector3 center, Vector3 size, Quaternion rotation)
            {
                if (!m || size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f) return;
                if (!parts.TryGetValue(m, out var list)) parts[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(center, rotation, size) });
            }

            public void Build(Transform parent, List<Mesh> meshes, bool worldUv)
            {
                foreach (var (m, list) in parts)
                {
                    var mesh = new Mesh { name = "Architecture", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(list.ToArray(), true, true);
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

            // Textures tile at a fixed size in the world (every 4m), whatever the box's size,
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
                float width = Mathf.Max(span, lastSpan);

                // Pieces that run from the last rib to this one, following the slope and the
                // curve: `dy` is a height relative to each rib's roof / floor / riding level
                void Roof(Material m, float dy, float w, float thick) => Slab(b, m, lastMid.WithY(last.top + dy), mid.WithY(top + dy), w, thick);
                void Floor(Material m, float dy, float w, float thick) => Slab(b, m, lastMid.WithY(last.bottom + dy), mid.WithY(bottom + dy), w, thick);
                void AtLevel(Material m, Vector3 fromOffset, Vector3 toOffset, float dy, float w, float thick) =>
                    Slab(b, m, (lastMid + fromOffset).WithY(last.level + dy), (mid + toOffset).WithY(level + dy), w, thick);

                switch (biome.style)
                {
                    case SceneryStyle.Cathedral:
                    {
                        foreach (var at in new[] { A, B })
                        {
                            b.Box(kit.scenery, new Vector3(at.x, (top + bottom) * 0.5f, at.z), new Vector3(2.2f, h, 2.2f), rot);
                            b.Box(kit.glow, new Vector3(at.x, level - 2f, at.z) + (mid - at).WithY(0f).normalized * 1.2f, new Vector3(0.3f, h * 0.6f, 0.3f), rot);
                        }
                        float rise = span * 0.28f;
                        Vector3 apex = mid.WithY(top + rise);
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 foot = at.WithY(top);
                            b.Box(kit.scenery, (foot + apex) * 0.5f, new Vector3(1.6f, 1.6f, (apex - foot).magnitude + 1f), Quaternion.LookRotation(apex - foot, f));
                        }
                        if (hasLast)
                        {
                            bool window = ribs % 4 == 2;
                            WallBay(b, kit.scenery, kit.glow, last, fr, true, 1f, window, 2f, 14f, 0.3f, narrow: true);
                            WallBay(b, kit.scenery, kit.glow, last, fr, false, 1f, window, 2f, 14f, 0.3f, narrow: true);
                            // The pitched roof, each side a panel from the eave up to the ridge
                            float lastRise = lastSpan * 0.28f;
                            Pitch(b, kit.slab, last.A.WithY(last.top), A.WithY(top), lastMid.WithY(last.top + lastRise), apex, 0.8f);
                            Pitch(b, kit.slab, last.B.WithY(last.top), B.WithY(top), lastMid.WithY(last.top + lastRise), apex, 0.8f);
                            for (int n = 0; n < 2; n++)
                            {
                                float u = (n + 0.5f) / 2f;
                                Vector3 spike = Vector3.Lerp(lastMid.WithY(last.top + lastRise), apex, u);
                                b.Box(kit.glowAlt, spike + Vector3.down * Rand(1f, 3f), new Vector3(0.7f, Rand(3f, 6f), 0.7f), Quaternion.Euler(45f, Rand(0f, 90f), 45f));
                            }
                            Floor(floor, 0f, width, 1f);
                            Floor(kit.glow, 0.6f, 0.8f, 0.4f);
                        }
                        if (ribs % 2 == 1)
                            foreach (var at in new[] { A, B })
                            {
                                Vector3 ch = at + (mid - at).WithY(0f).normalized * 3.5f;
                                b.Box(kit.slab, new Vector3(ch.x, top - 7f, ch.z), new Vector3(0.35f, 14f, 0.35f), rot);
                            }
                        break;
                    }
                    case SceneryStyle.Palace:
                    {
                        foreach (var at in new[] { A, B })
                        {
                            b.Box(kit.scenery, new Vector3(at.x, (top + bottom) * 0.5f, at.z), new Vector3(1.8f, h, 1.8f), rot);
                            b.Box(kit.scenery, new Vector3(at.x, top - 0.5f, at.z), new Vector3(3f, 1f, 3f), rot);
                        }
                        b.Box(kit.scenery, new Vector3(mid.x, top + 0.8f, mid.z), new Vector3(span + 3f, 1.6f, 1.6f), rot);
                        if (hasLast)
                        {
                            Line(b, kit.glow, last.A.WithY(last.top + 1.8f), A.WithY(top + 1.8f), 0.35f);
                            Line(b, kit.glow, last.B.WithY(last.top + 1.8f), B.WithY(top + 1.8f), 0.35f);
                            Vector3 cc = (lastMid + mid) * 0.5f;
                            b.Box(kit.scenery, cc.WithY((last.top + top) * 0.5f + 1.6f), new Vector3(span, 0.4f, 1.2f), rot);
                            if (ribs % 2 == 0)
                            {
                                Vector3 cloud = cc + right * Rand(-60f, 60f);
                                b.Box(kit.scenery, new Vector3(cloud.x, bottom - Rand(20f, 45f), cloud.z), new Vector3(Rand(30f, 70f), Rand(4f, 9f), Rand(25f, 50f)), Quaternion.Euler(0f, Rand(0f, 360f), 0f));
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Rings:
                    {
                        Vector3 center = mid.WithY(level + 2f);
                        float radius = span * 0.5f + 4f;
                        Material m = ribs % 2 == 0 ? kit.glow : kit.glowAlt;
                        const int blocks = 32;
                        for (int n = 0; n < blocks; n++)
                        {
                            float a0 = n * Mathf.PI * 2f / blocks, a1 = (n + 1) * Mathf.PI * 2f / blocks;
                            Vector3 p0 = center + (right * Mathf.Cos(a0) + Vector3.up * Mathf.Sin(a0)) * radius;
                            Vector3 p1 = center + (right * Mathf.Cos(a1) + Vector3.up * Mathf.Sin(a1)) * radius;
                            b.Box(m, (p0 + p1) * 0.5f, new Vector3(1.1f, 1.1f, (p1 - p0).magnitude + 0.5f), Quaternion.LookRotation(p1 - p0, f));
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
                            WallBay(b, kit.scenery, null, last, fr, true, 3f, false, 0f, 0f, 0f);
                            WallBay(b, kit.scenery, null, last, fr, false, 3f, false, 0f, 0f, 0f);
                            Roof(kit.scenery, 1.5f, width + 6f, 3f);
                            Floor(kit.glow, 0f, width + 2f, 0.3f);
                            if (ribs % 7 == 3) Roof(kit.glowAlt, 0.1f, 1.2f, 0.4f);
                        }
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 inward = (mid - at).WithY(0f).normalized;
                            for (int n = 0; n < 2; n++)
                            {
                                float y = Mathf.Lerp(bottom + 4f, top - 2f, n) + Rand(-3f, 3f);
                                Vector3 rc = at + inward * Rand(0f, 3f);
                                b.Box(kit.scenery, rc.WithY(y), new Vector3(Rand(6f, 11f), Rand(6f, 11f), Rand(7f, 12f)), Quaternion.Euler(Rand(-30f, 30f), Rand(0f, 360f), Rand(-30f, 30f)));
                            }
                            if (rng.NextDouble() < 0.5)
                            {
                                Vector3 mu = at + inward * Rand(2f, 4f);
                                float y = bottom + Rand(3f, 10f), stem = Rand(1.5f, 3f);
                                b.Box(kit.slab, mu.WithY(y + stem * 0.5f), new Vector3(0.5f, stem, 0.5f), rot);
                                b.Box(kit.glow, mu.WithY(y + stem), new Vector3(Rand(1.8f, 3f), 0.6f, Rand(1.8f, 3f)), Quaternion.Euler(0f, 45f, 0f));
                            }
                            if (rng.NextDouble() < 0.35)
                            {
                                Vector3 cr = at + inward * Rand(1f, 3f);
                                b.Box(kit.glowAlt, cr.WithY(level + Rand(4f, 12f)), new Vector3(0.9f, Rand(3f, 6f), 0.9f), Quaternion.Euler(Rand(-35f, 35f), Rand(0f, 90f), Rand(-35f, 35f)));
                            }
                        }
                        if (rng.NextDouble() < 0.6)
                        {
                            Vector3 st = mid + right * Rand(-span * 0.45f, span * 0.45f);
                            b.Box(kit.scenery, st.WithY(top - 3f), new Vector3(1.4f, 7f, 1.4f), Quaternion.Euler(Rand(-8f, 8f), 45f, Rand(-8f, 8f)));
                        }
                        break;
                    }
                    case SceneryStyle.Candy:
                    {
                        if (hasLast)
                        {
                            bool window = ribs % 5 == 2;
                            WallBay(b, kit.scenery, kit.glowAlt, last, fr, true, 1.2f, window, 3f, 11f, 0.4f);
                            WallBay(b, kit.scenery, kit.glowAlt, last, fr, false, 1.2f, window, 3f, 11f, 0.4f);
                            Roof(kit.scenery, 0f, width + 1f, 1.2f);
                            Floor(kit.scenery, 0f, width + 1f, 1.2f);
                            Vector3 cc = (lastMid + mid) * 0.5f;
                            float cTop = (last.top + top) * 0.5f;
                            var bay = Quaternion.LookRotation((mid - lastMid).WithY(0f).sqrMagnitude > 0.01f ? (mid - lastMid).WithY(0f) : f);
                            b.Box(kit.glow, cc.WithY(cTop - 0.7f), new Vector3(3.2f, 0.3f, 3.2f), bay * Quaternion.Euler(0f, 45f, 0f));
                            b.Box(kit.glowAlt, cc.WithY(cTop - 0.7f), new Vector3(2.2f, 0.32f, 2.2f), bay);
                        }
                        if (ribs % 5 == 0) Portal(b, kit.glowAlt, A, B, bottom, top, rot, 0.8f);
                        if (ribs % 3 == 0)
                        {
                            var mats = new[] { kit.ramp, kit.slab, kit.glow, kit.glowAlt };
                            Vector3 o = mid + right * (rng.NextDouble() < 0.5 ? -1f : 1f) * Rand(span * 0.5f + 20f, span * 0.5f + 70f);
                            b.Box(mats[rng.Next(mats.Length)], o.WithY(level + Rand(-40f, 30f)), new Vector3(Rand(6f, 22f), Rand(6f, 30f), Rand(6f, 22f)), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                        }
                        break;
                    }
                    case SceneryStyle.Forge:
                    {
                        Portal(b, kit.scenery, A, B, bottom, top, rot, 2.4f);
                        if (hasLast)
                        {
                            WallBay(b, kit.scenery, null, last, fr, true, 1.4f, false, 0f, 0f, 0f);
                            WallBay(b, kit.scenery, null, last, fr, false, 1.4f, false, 0f, 0f, 0f);
                            foreach (bool sideA in new[] { true, false })
                            {
                                Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? A : B;
                                Vector3 in0 = (lastMid - w0).WithY(0f).normalized * 0.8f, in1 = (mid - w1).WithY(0f).normalized * 0.8f;
                                Vector3 s0 = (w0 + in0).WithY(last.level + 6f), s1 = (w1 + in1).WithY(level + 6f);
                                Vector3 m0 = Vector3.Lerp(s0, s1, 0.2f), m1 = Vector3.Lerp(s0, s1, 0.8f);
                                Slab(b, kit.glowAlt, m0, m1, 0.3f, 1f, vertical: true);
                            }
                            Roof(kit.slab, 0.8f, width, 1f);
                            if (ribs % 3 == 0) Roof(kit.glow, 0.25f, width * 0.5f, 0.2f);
                            Floor(kit.glow, 0f, width, 0.5f);
                            for (int n = -2; n <= 2; n++)
                            {
                                Vector3 o0 = last.right * (n * lastSpan * 0.2f), o1 = right * (n * span * 0.2f);
                                Slab(b, kit.glow, (lastMid + o0).WithY(last.bottom), (mid + o1).WithY(bottom), 0.6f, 0.5f);
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Wire:
                    {
                        Material m = ribs % 2 == 0 ? kit.glow : kit.glowAlt;
                        float wireBottom = Mathf.Max(bottom, level - 16f);
                        Portal(b, m, A, B, wireBottom, top, rot, 0.35f);
                        if (hasLast)
                        {
                            float lastWire = Mathf.Max(last.bottom, last.level - 16f);
                            foreach (var (w0, w1) in new[] { (last.A, A), (last.B, B) })
                            {
                                Line(b, kit.glow, w0.WithY(last.top), w1.WithY(top), 0.3f);
                                Line(b, kit.glow, w0.WithY(lastWire), w1.WithY(wireBottom), 0.3f);
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Gallery:
                    {
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 pil = at + (mid - at).WithY(0f).normalized * 1.2f;
                            b.Box(kit.slab, pil.WithY((top + bottom) * 0.5f), new Vector3(2.4f, h, 2.4f), rot);
                        }
                        if (hasLast)
                        {
                            int side = (ribs / 3) % 2;
                            bool windowA = ribs % 3 == 1 && side == 0, windowB = ribs % 3 == 1 && side == 1;
                            WallBay(b, kit.scenery, kit.slab, last, fr, true, 1f, windowA, -2f, 13f, 0.5f);
                            WallBay(b, kit.scenery, kit.slab, last, fr, false, 1f, windowB, -2f, 13f, 0.5f);
                            foreach (var (sideA, win) in new[] { (true, windowA), (false, windowB) })
                            {
                                Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? A : B;
                                Vector3 in0 = (lastMid - w0).WithY(0f).normalized, in1 = (mid - w1).WithY(0f).normalized;
                                if (!win)
                                {
                                    Vector3 sc = Vector3.Lerp((w0 + in0 * 0.7f).WithY(last.level + 4f), (w1 + in1 * 0.7f).WithY(level + 4f), 0.5f);
                                    b.Box(kit.glow, sc, new Vector3(0.5f, 1.6f, 0.9f), Quaternion.LookRotation((w1 - w0).WithY(0f)));
                                }
                                Slab(b, kit.glowAlt, (w0 + in0 * 1.2f).WithY(last.bottom + 0.9f), (w1 + in1 * 1.2f).WithY(bottom + 0.9f), 0.6f, 0.2f);
                            }
                            Roof(kit.scenery, 0f, width + 2f, 1f);
                            Roof(kit.glowAlt, -0.55f, 2f, 0.15f);
                            Floor(floor, 0f, width + 2f, 1f);
                        }
                        break;
                    }
                    case SceneryStyle.Sunset:
                    {
                        if (hasLast)
                        {
                            int side = (ribs / 4) % 2;
                            bool windowA = ribs % 4 == 2 && side == 0, windowB = ribs % 4 == 2 && side == 1;
                            WallBay(b, kit.scenery, kit.glow, last, fr, true, 1f, windowA, -4f, 12f, 0.9f);
                            WallBay(b, kit.scenery, kit.glow, last, fr, false, 1f, windowB, -4f, 12f, 0.9f);
                            foreach (bool sideA in new[] { true, false })
                            {
                                Vector3 w0 = sideA ? last.A : last.B, w1 = sideA ? A : B;
                                Vector3 in0 = (lastMid - w0).WithY(0f).normalized, in1 = (mid - w1).WithY(0f).normalized;
                                // The sloped foot of the wall, leaning into the room
                                Vector3 f0 = (w0 + in0 * 3f).WithY(last.bottom + 4f), f1 = (w1 + in1 * 3f).WithY(bottom + 4f);
                                Vector3 along = f1 - f0;
                                if (along.sqrMagnitude > 0.01f)
                                {
                                    Vector3 lean = (Vector3.up * 0.82f + ((in0 + in1) * 0.5f).normalized * 0.57f).normalized;
                                    b.Box(kit.scenery, (f0 + f1) * 0.5f, new Vector3(7f, 1f, along.magnitude + 1f), Quaternion.LookRotation(along, lean));
                                }
                                Slab(b, kit.glowAlt, (w0 + in0 * 0.6f).WithY(last.top - 1.2f), (w1 + in1 * 0.6f).WithY(top - 1.2f), 0.4f, 0.5f);
                            }
                            Roof(kit.slab, 0f, width + 2f, 1f);
                            Floor(floor, 0f, width + 2f, 1f);
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

        // A roof panel between two eaves and two ridge points (a quad, laid as one box)
        static void Pitch(Batch b, Material m, Vector3 eave0, Vector3 eave1, Vector3 ridge0, Vector3 ridge1, float thick)
        {
            Vector3 along = ((eave1 - eave0) + (ridge1 - ridge0)) * 0.5f;
            Vector3 across = (ridge0 + ridge1) * 0.5f - (eave0 + eave1) * 0.5f;
            if (along.sqrMagnitude < 0.01f || across.sqrMagnitude < 0.01f) return;
            Vector3 up = Vector3.Cross(along, across).normalized;
            if (up.y < 0f) up = -up;
            b.Box(m, (eave0 + eave1 + ridge0 + ridge1) * 0.25f, new Vector3(across.magnitude + 1.2f, thick, along.magnitude + 1.2f), Quaternion.LookRotation(along, up));
        }

        // A wall bay from the last rib's wall line to this one's, on side A or B. It follows the
        // slope: the wall is laid as a slab running from rib to rib along the riding level, as
        // deep as the building (from the floor to the roof). With a window, it's split around
        // an opening between `lo` and `hi` above the riding level, edged in `frame`.
        static void WallBay(Batch b, Material wall, Material frame, Frame last, Frame fr, bool sideA, float thickness,
            bool window, float lo, float hi, float frameWidth, bool narrow = false)
        {
            Vector3 a = sideA ? last.A : last.B, c = sideA ? fr.A : fr.B;
            // Heights relative to the riding level at each end
            float down = Mathf.Max(last.level - last.bottom, fr.level - fr.bottom);
            float up = Mathf.Max(last.top - last.level, fr.top - fr.level);
            Vector3 from = a.WithY(last.level), to = c.WithY(fr.level);
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            // Upright wall along the sloping line: forward along the wall, up as close to up as
            // the slope allows
            Vector3 fwd = d / len;
            Vector3 upright = (Vector3.up - fwd * Vector3.Dot(Vector3.up, fwd)).normalized;
            var rot = Quaternion.LookRotation(fwd, upright);
            Vector3 center = (from + to) * 0.5f;
            void Band(Material m, float y0, float y1, float along0, float along1, float thick)
            {
                if (y1 - y0 < 0.01f || along1 - along0 < 0.01f) return;
                Vector3 at = center + upright * ((y0 + y1) * 0.5f) + fwd * ((along0 + along1) * 0.5f);
                b.Box(m, at, new Vector3(thick, y1 - y0, along1 - along0), rot);
            }
            float half = len * 0.5f + 0.6f; // overlap the next bay so bends close
            if (!window || hi <= lo)
            {
                Band(wall, -down, up, -half, half, thickness);
                return;
            }
            float open = narrow ? len * 0.3f : Mathf.Max(len - 3f, len * 0.6f);
            Band(wall, -down, lo, -half, half, thickness);
            Band(wall, hi, up, -half, half, thickness);
            Band(wall, lo, hi, -half, -open * 0.5f, thickness);
            Band(wall, lo, hi, open * 0.5f, half, thickness);
            if (!frame || frameWidth <= 0f) return;
            float t = thickness + 0.2f, w = frameWidth * 0.5f;
            Band(frame, lo - w, lo + w, -open * 0.5f - w, open * 0.5f + w, t);
            Band(frame, hi - w, hi + w, -open * 0.5f - w, open * 0.5f + w, t);
            Band(frame, lo, hi, -open * 0.5f - w, -open * 0.5f + w, t);
            Band(frame, lo, hi, open * 0.5f - w, open * 0.5f + w, t);
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
