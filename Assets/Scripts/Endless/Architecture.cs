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
            for (float d = from; d <= to + 0.01f; d += step)
            {
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
                Vector3 p = fr.p, f = fr.f, right = fr.right, A = fr.A, B = fr.B;
                float top = fr.top, bottom = fr.bottom, h = top - bottom, level = fr.level;
                var rot = Quaternion.LookRotation(f, Vector3.up);
                Vector3 mid = (A + B) * 0.5f;
                float span = Vector3.Distance(A.WithY(0f), B.WithY(0f));
                bool hasLast = ribs > 0;
                var last = hasLast ? frames[ribs - 1] : fr;
                Vector3 qa = last.A, qb = last.B;
                Vector3 lastMid = (qa + qb) * 0.5f;
                float between = Vector3.Distance(lastMid.WithY(0f), mid.WithY(0f));
                Vector3 c = (lastMid + mid) * 0.5f; // middle of the bay between this rib and the last
                float cTop = (last.top + top) * 0.5f, cBottom = (last.bottom + bottom) * 0.5f, cLevel = (last.level + level) * 0.5f;
                var bayRot = between > 0.01f ? Quaternion.LookRotation((mid - lastMid).WithY(0f), Vector3.up) : rot;

                switch (biome.style)
                {
                    case SceneryStyle.Cathedral:
                    {
                        foreach (var at in new[] { A, B })
                            b.Box(kit.scenery, new Vector3(at.x, (top + bottom) * 0.5f, at.z), new Vector3(2.2f, h, 2.2f), rot);
                        Vector3 apex = new(mid.x, top + span * 0.28f, mid.z);
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 foot = new(at.x, top, at.z);
                            b.Box(kit.scenery, (foot + apex) * 0.5f, new Vector3(1.6f, 1.6f, (apex - foot).magnitude + 1f), Quaternion.LookRotation(apex - foot, f));
                            b.Box(kit.glow, new Vector3(at.x, level - 2f, at.z) + (mid - at).WithY(0f).normalized * 1.2f, new Vector3(0.3f, h * 0.6f, 0.3f), rot);
                        }
                        if (hasLast)
                        {
                            // Banded walls with a tall narrow window now and then, a pitched
                            // roof both sides of a spine of spikes, a red floor line
                            bool window = ribs % 4 == 2;
                            WallBay(b, kit.scenery, kit.glow, qa, A, last.bottom, bottom, last.top, top, 1f, window ? level + 2f : 0f, window ? level + 14f : 0f, 0.3f, narrow: true);
                            WallBay(b, kit.scenery, kit.glow, qb, B, last.bottom, bottom, last.top, top, 1f, window ? level + 2f : 0f, window ? level + 14f : 0f, 0.3f, narrow: true);
                            float rise = span * 0.28f;
                            foreach (var (w0, w1) in new[] { (qa, A), (qb, B) })
                            {
                                Vector3 eave = ((w0 + w1) * 0.5f).WithY(cTop), ridge = c.WithY(cTop + rise);
                                Vector3 slope = ridge - eave;
                                // The panel lies along the bay and up the slope
                                Vector3 bayDir = bayRot * Vector3.forward, up = Vector3.Cross(bayDir, slope).normalized;
                                if (up.y < 0f) up = -up;
                                b.Box(kit.slab, (eave + ridge) * 0.5f, new Vector3(slope.magnitude + 1f, 0.8f, between + 0.6f), Quaternion.LookRotation(bayDir, up));
                            }
                            for (int n = 0; n < 2; n++)
                            {
                                Vector3 spike = Vector3.Lerp(lastMid, mid, (n + 0.5f) / 2f);
                                b.Box(kit.glowAlt, new Vector3(spike.x, cTop + rise - Rand(1f, 3f), spike.z), new Vector3(0.7f, Rand(3f, 6f), 0.7f), Quaternion.Euler(45f, Rand(0f, 90f), 45f));
                            }
                            b.Box(floor, new Vector3(c.x, cBottom, c.z), new Vector3(span, 1f, between + 0.5f), bayRot);
                            b.Box(kit.glow, new Vector3(c.x, cBottom + 0.6f, c.z), new Vector3(0.8f, 0.4f, between), bayRot);
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
                            Line(b, kit.glow, qa.WithY(last.top + 1.8f), A.WithY(top + 1.8f), 0.35f);
                            Line(b, kit.glow, qb.WithY(last.top + 1.8f), B.WithY(top + 1.8f), 0.35f);
                            b.Box(kit.scenery, new Vector3(c.x, cTop + 1.6f, c.z), new Vector3(span, 0.4f, 1.2f), rot);
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
                        Vector3 center = new(mid.x, level + 2f, mid.z);
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
                        var platRot = Quaternion.Euler(0f, Rand(0f, 90f), 0f);
                        b.Box(kit.scenery, new Vector3(plat.x, py, plat.z), new Vector3(16f, 1.5f, 16f), platRot);
                        b.Box(kit.glowAlt, new Vector3(plat.x, py + 0.8f, plat.z), new Vector3(16.4f, 0.2f, 16.4f), platRot);
                        break;
                    }
                    case SceneryStyle.Grotto:
                    {
                        if (hasLast)
                        {
                            // Solid rock walls and roof, then boulders tumbled over them
                            WallBay(b, kit.scenery, null, qa, A, last.bottom, bottom, last.top, top, 3f, 0f, 0f, 0f);
                            WallBay(b, kit.scenery, null, qb, B, last.bottom, bottom, last.top, top, 3f, 0f, 0f, 0f);
                            b.Box(kit.scenery, new Vector3(c.x, cTop + 1.5f, c.z), new Vector3(span + 6f, 3f, between + 0.8f), bayRot);
                            // The glowing lake
                            b.Box(kit.glow, new Vector3(c.x, cBottom, c.z), new Vector3(span + 2f, 0.3f, between + 0.5f), bayRot);
                            // A skylight crack now and then: a strategic glimpse out
                            if (ribs % 7 == 3) b.Box(kit.glowAlt, new Vector3(c.x, cTop + 0.1f, c.z), new Vector3(1.2f, 0.4f, between * 0.8f), bayRot);
                        }
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 inward = (mid - at).WithY(0f).normalized;
                            for (int n = 0; n < 2; n++)
                            {
                                float y = Mathf.Lerp(bottom + 4f, top - 2f, n) + Rand(-3f, 3f);
                                Vector3 rc = at + inward * Rand(0f, 3f);
                                b.Box(kit.scenery, new Vector3(rc.x, y, rc.z), new Vector3(Rand(6f, 11f), Rand(6f, 11f), Rand(7f, 12f)), Quaternion.Euler(Rand(-30f, 30f), Rand(0f, 360f), Rand(-30f, 30f)));
                            }
                            if (rng.NextDouble() < 0.5)
                            {
                                Vector3 mu = at + inward * Rand(2f, 4f);
                                float y = bottom + Rand(3f, 10f), stem = Rand(1.5f, 3f);
                                b.Box(kit.slab, new Vector3(mu.x, y + stem * 0.5f, mu.z), new Vector3(0.5f, stem, 0.5f), rot);
                                b.Box(kit.glow, new Vector3(mu.x, y + stem, mu.z), new Vector3(Rand(1.8f, 3f), 0.6f, Rand(1.8f, 3f)), Quaternion.Euler(0f, 45f, 0f));
                            }
                            if (rng.NextDouble() < 0.35)
                            {
                                Vector3 cr = at + inward * Rand(1f, 3f);
                                b.Box(kit.glowAlt, new Vector3(cr.x, level + Rand(4f, 12f), cr.z), new Vector3(0.9f, Rand(3f, 6f), 0.9f), Quaternion.Euler(Rand(-35f, 35f), Rand(0f, 90f), Rand(-35f, 35f)));
                            }
                        }
                        if (rng.NextDouble() < 0.6)
                        {
                            Vector3 st = mid + right * Rand(-span * 0.45f, span * 0.45f);
                            b.Box(kit.scenery, new Vector3(st.x, top - 3f, st.z), new Vector3(1.4f, 7f, 1.4f), Quaternion.Euler(Rand(-8f, 8f), 45f, Rand(-8f, 8f)));
                        }
                        break;
                    }
                    case SceneryStyle.Candy:
                    {
                        // Rooms one after another: solid walls, roof and floor, a glowing
                        // doorframe between rooms and a square window in each room's side
                        if (hasLast)
                        {
                            bool window = ribs % 5 == 2;
                            WallBay(b, kit.scenery, kit.glowAlt, qa, A, last.bottom, bottom, last.top, top, 1.2f, window ? level + 3f : 0f, window ? level + 11f : 0f, 0.4f);
                            WallBay(b, kit.scenery, kit.glowAlt, qb, B, last.bottom, bottom, last.top, top, 1.2f, window ? level + 3f : 0f, window ? level + 11f : 0f, 0.4f);
                            b.Box(kit.scenery, new Vector3(c.x, cTop, c.z), new Vector3(span, 1.2f, between + 0.3f), bayRot);
                            b.Box(kit.scenery, new Vector3(c.x, cBottom, c.z), new Vector3(span, 1.2f, between + 0.3f), bayRot);
                            b.Box(kit.glow, new Vector3(c.x, cTop - 0.7f, c.z), new Vector3(3.2f, 0.3f, 3.2f), bayRot * Quaternion.Euler(0f, 45f, 0f));
                            b.Box(kit.glowAlt, new Vector3(c.x, cTop - 0.7f, c.z), new Vector3(2.2f, 0.32f, 2.2f), bayRot);
                        }
                        if (ribs % 5 == 0) Portal(b, kit.glowAlt, A, B, bottom, top, rot, 0.8f);
                        if (ribs % 3 == 0)
                        {
                            var mats = new[] { kit.ramp, kit.slab, kit.glow, kit.glowAlt };
                            Vector3 o = mid + right * (rng.NextDouble() < 0.5 ? -1f : 1f) * Rand(span * 0.5f + 20f, span * 0.5f + 70f);
                            b.Box(mats[rng.Next(mats.Length)], new Vector3(o.x, level + Rand(-40f, 30f), o.z), new Vector3(Rand(6f, 22f), Rand(6f, 30f), Rand(6f, 22f)), Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                        }
                        break;
                    }
                    case SceneryStyle.Forge:
                    {
                        Portal(b, kit.scenery, A, B, bottom, top, rot, 2.4f);
                        if (hasLast)
                        {
                            WallBay(b, kit.scenery, null, qa, A, last.bottom, bottom, last.top, top, 1.4f, 0f, 0f, 0f);
                            WallBay(b, kit.scenery, null, qb, B, last.bottom, bottom, last.top, top, 1.4f, 0f, 0f, 0f);
                            foreach (var (w0, w1) in new[] { (qa, A), (qb, B) })
                            {
                                Vector3 wm = (w0 + w1) * 0.5f + (mid - w1).WithY(0f).normalized * 0.8f;
                                b.Box(kit.glowAlt, new Vector3(wm.x, cLevel + 6f, wm.z), new Vector3(0.3f, 1f, between * 0.6f), bayRot);
                            }
                            b.Box(kit.slab, new Vector3(c.x, cTop + 0.8f, c.z), new Vector3(span, 1f, between + 0.3f), bayRot);
                            // Glowing slots in the roof, and the lava grid below
                            if (ribs % 3 == 0) b.Box(kit.glow, new Vector3(c.x, cTop + 0.25f, c.z), new Vector3(span * 0.5f, 0.2f, 1.2f), bayRot);
                            b.Box(kit.glow, new Vector3(c.x, cBottom, c.z), new Vector3(span, 0.5f, 0.6f), bayRot);
                            for (int n = -2; n <= 2; n++)
                            {
                                Vector3 g = c + right * (n * span * 0.2f);
                                b.Box(kit.glow, new Vector3(g.x, cBottom, g.z), new Vector3(0.6f, 0.5f, between), bayRot);
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
                            foreach (var (w0, w1) in new[] { (qa, A), (qb, B) })
                            {
                                Line(b, kit.glow, w0.WithY(last.top), w1.WithY(top), 0.3f);
                                Line(b, kit.glow, w0.WithY(lastWire), w1.WithY(wireBottom), 0.3f);
                            }
                        }
                        break;
                    }
                    case SceneryStyle.Gallery:
                    {
                        // White rooms: square pillars along the walls, walls with tall windows
                        // onto the sky, warm sconces, cool light along the foot of each wall,
                        // a white ceiling with a light slot down the middle
                        foreach (var at in new[] { A, B })
                        {
                            Vector3 inward = (mid - at).WithY(0f).normalized;
                            Vector3 pil = at + inward * 1.2f;
                            b.Box(kit.slab, new Vector3(pil.x, (top + bottom) * 0.5f, pil.z), new Vector3(2.4f, h, 2.4f), rot);
                        }
                        if (hasLast)
                        {
                            int side = (ribs / 3) % 2;
                            bool windowA = ribs % 3 == 1 && side == 0, windowB = ribs % 3 == 1 && side == 1;
                            WallBay(b, kit.scenery, kit.slab, qa, A, last.bottom, bottom, last.top, top, 1f, windowA ? cLevel - 2f : 0f, windowA ? cLevel + 13f : 0f, 0.5f);
                            WallBay(b, kit.scenery, kit.slab, qb, B, last.bottom, bottom, last.top, top, 1f, windowB ? cLevel - 2f : 0f, windowB ? cLevel + 13f : 0f, 0.5f);
                            foreach (var (w0, w1, win) in new[] { (qa, A, windowA), (qb, B, windowB) })
                            {
                                Vector3 inward = (mid - w1).WithY(0f).normalized;
                                Vector3 wc = (w0 + w1) * 0.5f + inward * 0.7f;
                                if (!win) b.Box(kit.glow, new Vector3(wc.x, cLevel + 4f, wc.z), new Vector3(0.5f, 1.6f, 0.9f), bayRot);
                                Vector3 foot = (w0 + w1) * 0.5f + inward * 1.2f;
                                b.Box(kit.glowAlt, new Vector3(foot.x, cBottom + 0.9f, foot.z), new Vector3(0.6f, 0.2f, between), bayRot);
                            }
                            b.Box(kit.scenery, new Vector3(c.x, cTop, c.z), new Vector3(span + 2f, 1f, between + 0.4f), bayRot);
                            b.Box(kit.glowAlt, new Vector3(c.x, cTop - 0.55f, c.z), new Vector3(2f, 0.15f, between * 0.85f), bayRot);
                            b.Box(floor, new Vector3(c.x, cBottom, c.z), new Vector3(span + 2f, 1f, between + 0.4f), bayRot);
                        }
                        break;
                    }
                    case SceneryStyle.Sunset:
                    {
                        // Salmon rooms: smooth walls leaning in at their foot, peach light slots
                        // along the top of each wall, a dark gridded ceiling, a dark hexagon
                        // floor, and now and then a great window framed in glowing orange
                        if (hasLast)
                        {
                            int side = (ribs / 4) % 2;
                            bool windowA = ribs % 4 == 2 && side == 0, windowB = ribs % 4 == 2 && side == 1;
                            WallBay(b, kit.scenery, kit.glow, qa, A, last.bottom, bottom, last.top, top, 1f, windowA ? cLevel - 4f : 0f, windowA ? cLevel + 12f : 0f, 0.9f);
                            WallBay(b, kit.scenery, kit.glow, qb, B, last.bottom, bottom, last.top, top, 1f, windowB ? cLevel - 4f : 0f, windowB ? cLevel + 12f : 0f, 0.9f);
                            foreach (var (w0, w1) in new[] { (qa, A), (qb, B) })
                            {
                                Vector3 inward = (mid - w1).WithY(0f).normalized;
                                Vector3 wc = (w0 + w1) * 0.5f;
                                // The sloped foot of the wall, leaning into the room
                                Vector3 lean = wc + inward * 3f;
                                b.Box(kit.scenery, new Vector3(lean.x, cBottom + 4f, lean.z), new Vector3(7f, 1f, between + 0.4f),
                                    bayRot * Quaternion.Euler(0f, 0f, Vector3.Dot(inward, Quaternion.Euler(0f, 90f, 0f) * (bayRot * Vector3.forward)) > 0f ? -35f : 35f));
                                Vector3 slot = wc + inward * 0.6f;
                                b.Box(kit.glowAlt, new Vector3(slot.x, cTop - 1.2f, slot.z), new Vector3(0.4f, 0.5f, between + 0.2f), bayRot);
                            }
                            b.Box(kit.slab, new Vector3(c.x, cTop, c.z), new Vector3(span + 2f, 1f, between + 0.4f), bayRot);
                            b.Box(floor, new Vector3(c.x, cBottom, c.z), new Vector3(span + 2f, 1f, between + 0.4f), bayRot);
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

        // A wall bay from one rib's wall line to the next, floor to roof (each end at its own
        // heights, so it follows the building down the slope). With a window (lo < hi) the
        // wall is split around an opening between those heights, edged in `frame`.
        static void WallBay(Batch b, Material wall, Material frame, Vector3 a, Vector3 c, float bottomA, float bottomC,
            float topA, float topC, float thickness, float lo, float hi, float frameWidth, bool narrow = false)
        {
            Vector3 along = (c - a).WithY(0f);
            float len = along.magnitude;
            if (len < 0.01f) return;
            var rot = Quaternion.LookRotation(along);
            Vector3 mid = ((a + c) * 0.5f).WithY(0f);
            float bottom = Mathf.Min(bottomA, bottomC), top = Mathf.Max(topA, topC);
            if (hi <= lo)
            {
                b.Box(wall, mid.WithY((top + bottom) * 0.5f), new Vector3(thickness, top - bottom, len + 0.4f), rot);
                return;
            }
            // The opening: narrow slits in the middle third, or wide windows leaving jambs
            float open = narrow ? len * 0.3f : Mathf.Max(len - 3f, len * 0.6f);
            float jamb = (len - open) * 0.5f;
            b.Box(wall, mid.WithY((lo + bottom) * 0.5f), new Vector3(thickness, lo - bottom, len + 0.4f), rot);
            b.Box(wall, mid.WithY((top + hi) * 0.5f), new Vector3(thickness, top - hi, len + 0.4f), rot);
            Vector3 dir = along / len;
            foreach (float s in new[] { -1f, 1f })
                b.Box(wall, (mid + dir * s * (len * 0.5f - jamb * 0.5f)).WithY((lo + hi) * 0.5f), new Vector3(thickness, hi - lo, jamb + 0.2f), rot);
            if (!frame || frameWidth <= 0f) return;
            b.Box(frame, mid.WithY(lo), new Vector3(thickness + 0.2f, frameWidth, open + frameWidth), rot);
            b.Box(frame, mid.WithY(hi), new Vector3(thickness + 0.2f, frameWidth, open + frameWidth), rot);
            foreach (float s in new[] { -1f, 1f })
                b.Box(frame, (mid + dir * s * open * 0.5f).WithY((lo + hi) * 0.5f), new Vector3(thickness + 0.2f, hi - lo, frameWidth), rot);
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
