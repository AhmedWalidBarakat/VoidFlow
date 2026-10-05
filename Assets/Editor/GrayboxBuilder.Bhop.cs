using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow.EditorTools
{
    // The 10 stage bhop challenge behind the start hall (BhopLayout lays it out): out through an
    // archway in the hall's back wall, a terrace, a bhop trail of wide marble steps, and the
    // plaza, closed at its far end by stage 1's wall: the challenge's sign over its doorway, the
    // prize on show either side. Then ten rooms, one per stage, in the manner of the classic
    // bhop maps: walls all round, a floor a few metres under the blocks that sends you back to
    // the pad, and the blocks themselves standing up from it as pillars with a coloured cap.
    // Each room has its own look (a marble court over water, a lantern garden, red spires over
    // sand, a white bowl, a clean grid room, a neon void, ice, a crystal cave, lava, and a white
    // and gold finale), a start pad up on its ledge, and an exit portal to the next room.
    // Every room's walls, blocks and decorations are merged into a few meshes per material.
    public static partial class GrayboxBuilder
    {
        class Theme
        {
            public Material cap, band, pillar, inset, rim;           // the blocks: top, collar, body, a square on top, a glowing edge
            public Material wall, wallTrim, floor, strip, ceiling, deco, deco2; // the room
            public float bandH = 0.3f;
            public Color glow;                                        // its signs' and portal's colour
        }

        // Meshes gathered per material, merged into one renderer each
        class Batch
        {
            readonly Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)> parts = new();

            (List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t) Get(Material m)
            {
                if (!parts.TryGetValue(m, out var p)) parts[m] = p = (new(), new(), new(), new());
                return p;
            }

            public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale = 0.25f)
            {
                var p = Get(m);
                Vector3 n = Vector3.Cross(b - a, d - a).normalized;
                Vector3 u = (b - a).normalized, w = Vector3.Cross(n, u);
                if (Mathf.Abs(u.y) > 0.7f) (u, w) = (w, u); // (on an upright face the texture runs level: bricks lie flat)
                int s = p.v.Count;
                foreach (var q in new[] { a, b, c, d })
                {
                    p.v.Add(q);
                    p.n.Add(n);
                    p.uv.Add(new Vector2(Vector3.Dot(q, u), Vector3.Dot(q, w)) * uvScale);
                }
                p.t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            public void Tri(Material m, Vector3 a, Vector3 b, Vector3 c, float uvScale = 0.25f)
            {
                var p = Get(m);
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                Vector3 u = (b - a).normalized, w = Vector3.Cross(n, u);
                int s = p.v.Count;
                foreach (var q in new[] { a, b, c })
                {
                    p.v.Add(q);
                    p.n.Add(n);
                    p.uv.Add(new Vector2(Vector3.Dot(q, u), Vector3.Dot(q, w)) * uvScale);
                }
                p.t.AddRange(new[] { s, s + 1, s + 2 });
            }

            // A box: its top's middle at `top`, turned by `rot`, `size` = (across, height, along);
            // the top face in `topMat`, the rest in `sideMat`
            public void Box(Material topMat, Material sideMat, Vector3 top, Quaternion rot, Vector3 size, float uvScale = 0.25f)
            {
                Vector3 r = rot * Vector3.right * (size.x * 0.5f), u = rot * Vector3.up, f = rot * Vector3.forward * (size.z * 0.5f);
                Vector3 hi = top, lo = top - u * size.y;
                Vector3 P(Vector3 c, float sr, float sf) => c + r * sr + f * sf;
                Quad(topMat, P(hi, -1, -1), P(hi, -1, 1), P(hi, 1, 1), P(hi, 1, -1), uvScale);
                Quad(sideMat, P(lo, -1, 1), P(lo, -1, -1), P(lo, 1, -1), P(lo, 1, 1), uvScale);
                Quad(sideMat, P(lo, -1, -1), P(hi, -1, -1), P(hi, 1, -1), P(lo, 1, -1), uvScale);
                Quad(sideMat, P(lo, 1, 1), P(hi, 1, 1), P(hi, -1, 1), P(lo, -1, 1), uvScale);
                Quad(sideMat, P(lo, -1, 1), P(hi, -1, 1), P(hi, -1, -1), P(lo, -1, -1), uvScale);
                Quad(sideMat, P(lo, 1, -1), P(hi, 1, -1), P(hi, 1, 1), P(lo, 1, 1), uvScale);
            }

            // A box whose top (and with `under`, its underside) is mapped in the world's own
            // frame: floors and ceilings of overlapping chambers then look as one
            public void Slab(Material topMat, Material sideMat, Vector3 top, Quaternion rot, Vector3 size, float uvScale, bool under = false)
            {
                Vector3 r = rot * Vector3.right * (size.x * 0.5f), f = rot * Vector3.forward * (size.z * 0.5f);
                Vector3 hi = top, lo = top - Vector3.up * size.y;
                Vector3 P(Vector3 c, float sr, float sf) => c + r * sr + f * sf;
                void Flat(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
                {
                    var p = Get(m);
                    Vector3 n = Vector3.Cross(b - a, d - a).normalized;
                    int s = p.v.Count;
                    foreach (var q in new[] { a, b, c, d })
                    {
                        p.v.Add(q);
                        p.n.Add(n);
                        p.uv.Add(new Vector2(q.x, q.z) * uvScale);
                    }
                    p.t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                }
                Flat(topMat, P(hi, -1, -1), P(hi, -1, 1), P(hi, 1, 1), P(hi, 1, -1));
                if (under) Flat(sideMat, P(lo, -1, 1), P(lo, -1, -1), P(lo, 1, -1), P(lo, 1, 1));
                else Quad(sideMat, P(lo, -1, 1), P(lo, -1, -1), P(lo, 1, -1), P(lo, 1, 1), uvScale);
                Quad(sideMat, P(lo, -1, -1), P(hi, -1, -1), P(hi, 1, -1), P(lo, 1, -1), uvScale);
                Quad(sideMat, P(lo, 1, 1), P(hi, 1, 1), P(hi, -1, 1), P(lo, -1, 1), uvScale);
                Quad(sideMat, P(lo, -1, 1), P(hi, -1, 1), P(hi, -1, -1), P(lo, -1, -1), uvScale);
                Quad(sideMat, P(lo, 1, -1), P(hi, 1, -1), P(hi, 1, 1), P(lo, 1, 1), uvScale);
            }

            // A thin line round the top edges of a box
            public void Rim(Material m, Vector3 top, Quaternion rot, Vector2 size, float w = 0.06f)
            {
                Vector3 r = rot * Vector3.right, f = rot * Vector3.forward, u = rot * Vector3.up;
                float a = size.x * 0.5f, b = size.y * 0.5f;
                Vector3 lift = u * 0.012f;
                foreach (var (c, s) in new[] { (f * b, new Vector3(size.x + w, 0.05f, w)), (-f * b, new Vector3(size.x + w, 0.05f, w)), (r * a, new Vector3(w, 0.05f, size.y + w)), (-r * a, new Vector3(w, 0.05f, size.y + w)) })
                    Box(m, m, top + c + lift + u * 0.025f, rot, s, 1f);
            }

            public void Cylinder(Material m, Vector3 bottom, float radius, float height, int sides = 24, bool caps = true)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 p0 = new(Mathf.Sin(a0) * radius, 0f, Mathf.Cos(a0) * radius), p1 = new(Mathf.Sin(a1) * radius, 0f, Mathf.Cos(a1) * radius);
                    Quad(m, bottom + p0, bottom + p0 + Vector3.up * height, bottom + p1 + Vector3.up * height, bottom + p1, 0.12f);
                    if (caps)
                    {
                        Tri(m, bottom + Vector3.up * height, bottom + p1 + Vector3.up * height, bottom + p0 + Vector3.up * height);
                        Tri(m, bottom, bottom + p0, bottom + p1);
                    }
                }
            }

            public void Ring(Material m, Vector3 center, float radius, float thick, int segments = 32)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 p0 = center + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius, p1 = center + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
                    Box(m, m, (p0 + p1) * 0.5f + Vector3.up * thick * 0.5f, Quaternion.LookRotation(p1 - p0), new Vector3(thick, thick, (p1 - p0).magnitude + 0.02f), 1f);
                }
            }

            // `shadows` false: seen but casting none (a ceiling the sun still lights the room through)
            public void Emit(Transform parent, string name, bool shadows = true)
            {
                int k = 0;
                foreach (var (m, p) in parts)
                {
                    if (p.v.Count == 0) continue;
                    var mesh = new Mesh { name = $"{name}_{k}", indexFormat = p.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.SetVertices(p.v);
                    mesh.SetNormals(p.n);
                    mesh.SetUVs(0, p.uv);
                    mesh.SetTriangles(p.t, 0);
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    AssetDatabase.CreateAsset(mesh, $"{Root}/Meshes/{name}_{k++}.asset");
                    var go = new GameObject($"{name} {m.name}");
                    go.transform.SetParent(parent, false);
                    go.isStatic = true;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = m;
                    bool lit = m.IsKeywordEnabled("_EMISSION") && m.GetColor("_EmissionColor").maxColorComponent > 0.3f;
                    r.shadowCastingMode = lit || !shadows ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                parts.Clear();
            }
        }

        static Material Mat(string name, Color color, Texture2D tex, float smooth, float metal = 0f, float tile = 0.25f)
        {
            var m = MakeMaterial(name, color, tex);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (tex) m.SetTextureScale("_BaseMap", Vector2.one * tile);
            return m;
        }

        // The rooms' painted surfaces map 8m to a texture; tinted in the material
        const float RoomUv = 0.125f;

        static Theme[] BhopThemes(Material marble, Material gold)
        {
            Material Glow(string n, Color c, float i) => MakeGlow("Bhop" + n, c, i);
            Material Painted(string n, Color c, Texture2D tex, float smooth, float relief = 0f, float metal = 0f)
            {
                var m = Mat("Bhop" + n, c, tex, smooth, metal, 1f);
                if (relief > 0f) Relief(m, relief);
                return m;
            }
            Material Lit(string n, Color c, Texture2D tex, Texture2D glow, Color glowColor, float smooth)
            {
                var m = Mat("Bhop" + n, c, tex, smooth, 0f, 1f);
                GlowMap(m, glow, glowColor);
                return m;
            }
            Texture2D water = MakeBhopWater(), moss = MakeBhopMoss(), sand = MakeBhopSand(), lava = MakeBhopLava(), lavaGlow = MakeBhopLavaGlow(),
                gridWhite = MakeBhopGridWhite(), neon = MakeBhopNeon(), neonGlow = MakeBhopNeonGlow(), iceTex = MakeBhopIce(),
                rock = MakeBhopCrystalRock(), rockGlow = MakeBhopCrystalGlow(), brick = MakeBhopStoneBrick(), panels = MakeBhopPanels();
            var ivory = Mat("BhopIvory", new Color(0.96f, 0.95f, 0.92f), stone, 0.55f);
            var t = new Theme[11];
            // 0: the trail and plaza, in the hall's marble and gold
            t[0] = new Theme { cap = marble, band = gold, pillar = ivory, wall = ivory, wallTrim = gold, rim = Glow("TrailTrim", new Color(1f, 0.72f, 0.32f), 1.5f), glow = new Color(1f, 0.75f, 0.4f) };
            // 1: a marble court over still water
            t[1] = new Theme { cap = marble, band = gold, pillar = ivory, wall = Painted("SkyWall", new Color(0.97f, 0.95f, 0.9f), panels, 0.5f), wallTrim = gold,
                floor = Painted("SkyWater", Color.white, water, 0.92f), glow = new Color(1f, 0.78f, 0.4f) };
            // 2: a lantern garden: stone walls, red posts, wooden planks over moss
            t[2] = new Theme { cap = Mat("BhopWood", new Color(0.5f, 0.32f, 0.18f), wood, 0.45f, 0f, 0.5f), band = Mat("BhopLacquer", new Color(0.6f, 0.06f, 0.04f), null, 0.75f),
                pillar = Mat("BhopGardenStone", new Color(0.42f, 0.43f, 0.4f), stone, 0.2f), wall = Painted("GardenWall", Color.white, brick, 0.15f, 0.8f),
                wallTrim = Mat("BhopGardenBeam", new Color(0.2f, 0.12f, 0.08f), wood, 0.35f, 0f, 0.5f), floor = Painted("GardenMoss", Color.white, moss, 0.1f, 0.5f),
                deco = Glow("Lantern", new Color(1f, 0.6f, 0.25f), 2.2f), deco2 = Mat("BhopLanternPost", new Color(0.55f, 0.05f, 0.03f), null, 0.7f), glow = new Color(1f, 0.6f, 0.3f) };
            // 3: red spires standing out of red sand, sandstone walls
            t[3] = new Theme { cap = Mat("BhopSpireTop", new Color(0.8f, 0.76f, 0.7f), stone, 0.35f), band = Mat("BhopSpireBand", new Color(0.3f, 0.12f, 0.08f), null, 0.4f),
                pillar = Mat("BhopTerracotta", new Color(0.66f, 0.18f, 0.1f), plaster, 0.25f, 0f, 0.35f), wall = Painted("SpireWall", new Color(0.95f, 0.72f, 0.52f), MakeWallSandstone(), 0.2f, 0.9f),
                wallTrim = Mat("BhopSpireTrim", new Color(0.55f, 0.2f, 0.12f), plaster, 0.3f), floor = Painted("SpireSand", Color.white, sand, 0.1f, 0.4f), glow = new Color(1f, 0.55f, 0.35f) };
            // 4: a white bowl, cyan light in its walls, over a pale pool
            var white = Mat("BhopGloss", new Color(0.94f, 0.95f, 0.97f), null, 0.85f);
            t[4] = new Theme { cap = white, band = Glow("BowlBand", new Color(0.3f, 0.9f, 1f), 1.6f), pillar = Mat("BhopGlossSide", new Color(0.84f, 0.87f, 0.91f), null, 0.7f),
                wall = Painted("BowlWall", new Color(0.93f, 0.95f, 0.98f), panels, 0.7f), wallTrim = white, strip = Glow("BowlStrip", new Color(0.3f, 0.9f, 1f), 2f),
                floor = Painted("BowlPool", new Color(0.75f, 1f, 1f), water, 0.95f), glow = new Color(0.3f, 0.9f, 1f), bandH = 0.12f };
            // 5: the null room: a clean grid room with a lit ceiling, grey blocks with a white square
            var grey = Mat("BhopNullGrey", new Color(0.3f, 0.31f, 0.33f), null, 0.3f);
            t[5] = new Theme { cap = grey, band = grey, pillar = Mat("BhopNullPillar", new Color(0.36f, 0.37f, 0.39f), null, 0.25f), inset = Mat("BhopNullWhite", new Color(0.97f, 0.97f, 0.98f), null, 0.4f),
                wall = Painted("NullWall", Color.white, gridWhite, 0.35f), wallTrim = Mat("BhopNullTrim", new Color(0.78f, 0.79f, 0.81f), null, 0.3f),
                floor = Painted("NullFloor", new Color(0.32f, 0.33f, 0.36f), gridWhite, 0.4f), ceiling = Painted("NullCeiling", new Color(0.92f, 0.92f, 0.94f), gridWhite, 0.2f),
                deco = Glow("NullLight", new Color(1f, 0.98f, 0.94f), 2.2f), glow = Color.white, bandH = 0.05f };
            // 6: the neon grid: a black void ruled in light
            var black = Mat("BhopNeonBlack", new Color(0.04f, 0.04f, 0.07f), null, 0.6f, 0.2f);
            t[6] = new Theme { cap = Mat("BhopNeonTop", new Color(0.05f, 0.06f, 0.16f), hex, 0.6f, 0.2f, 0.5f), band = black, pillar = black, rim = Glow("NeonTrim", new Color(0.25f, 0.9f, 1f), 2.2f),
                wall = Lit("NeonWall", Color.white, neon, neonGlow, new Color(1f, 1f, 1f) * 1.6f, 0.5f), wallTrim = black, strip = Glow("NeonStrip", new Color(1f, 0.25f, 0.85f), 2.4f),
                floor = Lit("NeonFloor", Color.white, neon, neonGlow, new Color(1f, 1f, 1f) * 1.2f, 0.7f), glow = new Color(1f, 0.3f, 0.9f), bandH = 0.15f };
            // 7: glass over ice
            t[7] = new Theme { cap = Mat("BhopGlass", new Color(0.66f, 0.88f, 0.96f), ice, 0.95f, 0.15f, 0.3f), band = Glow("GlassBand", new Color(0.85f, 0.97f, 1f), 1.2f),
                pillar = Mat("BhopGlassSide", new Color(0.55f, 0.8f, 0.92f), null, 0.95f, 0.15f), wall = Painted("IceWall", Color.white, iceTex, 0.85f, 0.4f),
                wallTrim = Mat("BhopIceTrim", new Color(0.85f, 0.95f, 1f), ice, 0.9f), strip = Glow("GlassStrip", new Color(0.55f, 0.9f, 1f), 1.4f),
                floor = Painted("IceWater", new Color(0.45f, 0.75f, 1f), water, 0.95f), glow = new Color(0.6f, 0.95f, 1f), bandH = 0.08f };
            // 8: a crystal cave
            t[8] = new Theme { cap = Mat("BhopShard", new Color(0.3f, 0.12f, 0.45f), null, 0.9f, 0.3f), band = Glow("ShardBand", new Color(0.75f, 0.4f, 1f), 1.8f),
                pillar = Mat("BhopShardSide", new Color(0.14f, 0.06f, 0.22f), null, 0.85f, 0.3f), wall = Lit("CaveWall", Color.white, rock, rockGlow, new Color(1f, 1f, 1f) * 1.4f, 0.4f),
                wallTrim = Mat("BhopCaveTrim", new Color(0.12f, 0.07f, 0.17f), stone, 0.3f), floor = Lit("CaveFloor", new Color(0.7f, 0.65f, 0.8f), rock, rockGlow, new Color(0.8f, 0.8f, 0.9f), 0.5f),
                deco = Mat("BhopCrystal", new Color(0.5f, 0.28f, 0.82f), null, 0.95f, 0.4f), deco2 = Glow("CrystalTip", new Color(0.85f, 0.65f, 1f), 1.8f), glow = new Color(0.75f, 0.45f, 1f), bandH = 0.08f };
            // 9: the gauntlet: basalt over lava
            var basalt = Mat("BhopBasalt", new Color(0.17f, 0.15f, 0.15f), stone, 0.3f);
            t[9] = new Theme { cap = basalt, band = Glow("LavaBand", new Color(1f, 0.42f, 0.1f), 2f), pillar = Mat("BhopBasaltSide", new Color(0.11f, 0.1f, 0.1f), stone, 0.25f),
                wall = Painted("BasaltWall", new Color(0.3f, 0.27f, 0.26f), brick, 0.2f, 1f), wallTrim = Mat("BhopBasaltTrim", new Color(0.08f, 0.07f, 0.07f), stone, 0.3f),
                strip = Glow("LavaStrip", new Color(1f, 0.4f, 0.08f), 2f), floor = Lit("Lava", Color.white, lava, lavaGlow, new Color(1f, 1f, 1f) * 2.2f, 0.3f), glow = new Color(1f, 0.45f, 0.15f), bandH = 0.1f };
            // 10: the finale: white and gold, black-topped pillars
            t[10] = new Theme { cap = Mat("BhopFinaleTop", new Color(0.05f, 0.05f, 0.06f), null, 0.9f, 0.3f), band = gold, pillar = Mat("BhopFinaleWhite", new Color(0.95f, 0.95f, 0.96f), null, 0.5f),
                wall = Painted("FinaleWall", Color.white, panels, 0.55f), wallTrim = gold, strip = Glow("FinaleStrip", new Color(1f, 0.8f, 0.4f), 1.6f),
                floor = Lit("FinaleFloor", new Color(1f, 0.92f, 0.75f), gridWhite, gridWhite, new Color(0.35f, 0.25f, 0.1f), 0.6f), glow = new Color(1f, 0.8f, 0.4f), bandH = 0.12f };
            return t;
        }

        static Bounds BhopBounds(List<BhopPiece> pieces)
        {
            var b = new Bounds(pieces[0].center, Vector3.zero);
            foreach (var p in pieces)
            {
                if (p.kind == BhopKind.Ramp) foreach (var q in p.line) b.Encapsulate(q);
                else b.Encapsulate(p.center);
            }
            return b;
        }

        // The pieces' heading, slope and bank as a rotation
        static Quaternion PieceRotation(in BhopPiece p) => Quaternion.Euler(0f, p.yaw, 0f) * (p.kind == BhopKind.Slope ? Quaternion.Euler(p.tilt, 0f, 0f) : p.kind == BhopKind.Bank ? Quaternion.Euler(0f, 0f, -p.tilt) : Quaternion.identity);

        // Where the player flies in a stage: the pads and blocks, the riding lines, and each hop's
        // arc between them (a pillar may not stand in any of it)
        static List<Vector3> FlightPath(List<BhopPiece> pieces, int stage)
        {
            var path = new List<Vector3>();
            Vector3? from = null;
            const float G = 800f * 0.0254f, VJ = 301.993f * 0.0254f;
            foreach (var p in pieces)
            {
                if (p.stage != stage || p.kind == BhopKind.Again) continue;
                if (p.kind == BhopKind.Ramp)
                {
                    if (from.HasValue)
                        for (int k = 1; k < 8; k++) path.Add(Vector3.Lerp(from.Value, p.line[0], k / 8f) + Vector3.up * 0.5f);
                    path.AddRange(p.line);
                    from = p.line[p.line.Length - 1];
                    continue;
                }
                if (from.HasValue)
                {
                    var a = from.Value;
                    var b = p.landing;
                    float T = Mathf.Max(0.2f, p.airtime);
                    for (int k = 1; k < 12; k++)
                    {
                        float t = k / 12f * T;
                        float y = p.fall ? a.y - G * t * t * 0.5f : a.y + VJ * t - G * t * t * 0.5f;
                        var q = Vector3.Lerp(a, b, k / 12f);
                        path.Add(new Vector3(q.x, y, q.z));
                    }
                }
                path.Add(p.landing);
                from = p.landing;
            }
            return path;
        }

        // How far a pillar may run down from a block's top: to the room's floor, unless a flight
        // path or another block passes under it (then it stops 3m above that)
        static float PillarDepth(BhopPiece p, float floor, List<Vector3> path, List<BhopPiece> pieces)
        {
            float depth = p.center.y - floor;
            var rot = Quaternion.Euler(0f, -p.yaw, 0f);
            float hx = p.size.y * 0.5f + 1.3f, hz = p.size.x * 0.5f + 1.3f;
            bool Under(Vector3 q, float grow)
            {
                var d = rot * new Vector3(q.x - p.center.x, 0f, q.z - p.center.z);
                return Mathf.Abs(d.x) < hx + grow && Mathf.Abs(d.z) < hz + grow;
            }
            foreach (var q in path)
                if (q.y < p.center.y - 0.6f && Under(q, 0f)) depth = Mathf.Min(depth, p.center.y - q.y - 3f);
            foreach (var o in pieces)
                if (o.stage == p.stage && o.kind != BhopKind.Again && o.kind != BhopKind.Ramp && o.center.y < p.center.y - 0.6f && Under(o.center, Mathf.Max(o.size.x, o.size.y) * 0.5f))
                    depth = Mathf.Min(depth, p.center.y - o.center.y - 3f);
            return Mathf.Max(depth, 0f);
        }

        static void BuildBhop(Transform hall, PlayerMovement player, Material marble, Material gold, Material rampMat)
        {
            var pieces = BhopLayout.Build(out var headings, out var rooms);
            var root = new GameObject("BhopChallenge").transform;
            root.SetParent(hall, false);
            var themes = BhopThemes(marble, gold);
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(root, false);
            var padTop = Mat("BhopPad", new Color(0.07f, 0.1f, 0.24f), metal, 0.7f, 0.3f);
            BhopLayout.Pads(pieces.ToArray(), out var starts, out var exits);

            void Solid(Vector3 top, Quaternion rot, Vector3 size, string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(colliders, false);
                go.transform.SetPositionAndRotation(top - rot * Vector3.up * (size.y * 0.5f), rot);
                go.isStatic = true;
                go.AddComponent<BoxCollider>().size = size;
            }

            for (int s = 1; s <= BhopLayout.Stages.Length; s++)
            {
                var stageRoot = new GameObject($"Stage {s}").transform;
                stageRoot.SetParent(root, false);
                var th = themes[s];
                float floorY = BhopLayout.FloorOf(rooms, s);
                var batch = new Batch();
                var shade = new Batch(); // (ceilings: seen, but the sun still lights the room)
                BuildRoom(batch, shade, Solid, rooms, th, s, s == 1 ? pieces[starts[1]] : (BhopPiece?)null);
                var path = FlightPath(pieces, s);
                for (int i = 0; i < pieces.Count; i++)
                {
                    var p = pieces[i];
                    if (p.stage != s) continue;
                    var rot = PieceRotation(p);
                    switch (p.kind)
                    {
                        case BhopKind.Again: break;
                        case BhopKind.Ramp:
                            BuildRamp(batch, colliders, p, th, rampMat);
                            break;
                        case BhopKind.Pad:
                        {
                            bool start = i == starts[s], finish = !start && s == BhopLayout.Stages.Length;
                            var size = new Vector3(p.size.y, 1.2f, p.size.x);
                            float depth = s == 1 && start ? 1.2f : Mathf.Max(1.2f, PillarDepth(p, floorY, path, pieces));
                            batch.Box(padTop, th.pillar, p.center, rot, new Vector3(size.x - 0.1f, depth, size.z - 0.1f), 0.25f);
                            batch.Box(padTop, th.band, p.center + Vector3.up * 0.004f, rot, new Vector3(size.x, 0.3f, size.z), 0.25f);
                            batch.Rim(themes[start ? s : Mathf.Min(s + 1, 10)].rim ?? th.band, p.center, rot, new Vector2(p.size.y - 0.3f, p.size.x - 0.3f), 0.1f);
                            Solid(p.center, rot, new Vector3(size.x, depth, size.z), "Pad");
                            if (start) StartSign(stageRoot, p, s, headings[s - 1], padTop, th);
                            else if (!finish) ExitPortal(batch, stageRoot, p, s, themes, gold);
                            break;
                        }
                        default:
                            BuildBlock(batch, Solid, p, rot, th, PillarDepth(p, floorY, path, pieces));
                            break;
                    }
                }
                batch.Emit(stageRoot, $"Bhop_Stage{s}");
                shade.Emit(stageRoot, $"Bhop_Stage{s}Ceiling", false);
            }

            // The trail
            var trail = new GameObject("Trail").transform;
            trail.SetParent(root, false);
            var tb = new Batch();
            foreach (var p in pieces)
            {
                if (p.kind != BhopKind.Trail) continue;
                var rot = Quaternion.Euler(0f, p.yaw, 0f);
                var size = new Vector3(p.size.y, 1.2f, p.size.x);
                tb.Box(marble, marble, p.center, rot, size, 0.25f);
                tb.Rim(themes[0].rim, p.center, rot, new Vector2(size.x, size.z));
                Solid(p.center, rot, size, "Trail");
            }
            tb.Emit(trail, "Bhop_Trail");
            BuildBhopPlaza(root, Solid, pieces, rooms[0], pieces[starts[1]], themes, gold, marble);

            // The challenge itself: checkpoints, falls, the clock and the prize
            var challenge = root.gameObject.AddComponent<BhopChallenge>();
            challenge.pieces = pieces.ToArray();
            challenge.headings = headings;
            challenge.rooms = rooms;
            challenge.player = player;
            var area = BhopBounds(pieces);
            foreach (var r in rooms)
                foreach (var c in BhopLayout.Corners(r, 2f))
                {
                    area.Encapsulate(new Vector3(c.x, r.Floor - 6f, c.y));
                    area.Encapsulate(new Vector3(c.x, r.top + 20f, c.y));
                }
            area.Expand(new Vector3(8f, 0f, 8f));
            var max = area.max;
            max.z = -50.8f; // (from the hall's back wall out)
            area.max = max;
            challenge.area = area;

            // The finish: a gold arch over the finish pad, and a ring back up to the plaza
            var finishPad = pieces[exits[BhopLayout.Stages.Length]];
            challenge.portal = finishPad.center + Quaternion.Euler(0f, finishPad.yaw, 0f) * Vector3.forward * 3f;
            var portal = new Batch();
            portal.Ring(themes[10].strip, challenge.portal + Vector3.up * 0.05f, 1.4f, 0.12f, 32);
            portal.Ring(themes[0].rim, challenge.portal + Vector3.up * 2.6f, 1.4f, 0.12f, 32);
            var face = Quaternion.Euler(0f, finishPad.yaw, 0f);
            Vector3 side = face * Vector3.right, back = face * Vector3.forward;
            foreach (float sx in new[] { -1f, 1f })
                portal.Box(gold, gold, finishPad.center + side * (sx * 3.6f) + back * 1.5f + Vector3.up * 6.5f, face, new Vector3(0.7f, 6.5f, 0.7f), 0.5f);
            portal.Box(gold, gold, finishPad.center + back * 1.5f + Vector3.up * 7.2f, face, new Vector3(8f, 0.7f, 0.9f), 0.5f);
            portal.Box(themes[10].strip, themes[10].strip, finishPad.center + back * 1.05f + Vector3.up * 6.55f, face, new Vector3(7.2f, 0.08f, 0.08f), 1f);
            portal.Emit(root, "Bhop_Portal");
            Label("FINISH", root, finishPad.center + back * 1.0f + Vector3.up * 5.8f, finishPad.yaw, 0.9f, new Color(1f, 0.85f, 0.45f));
            Label("BACK UP", root, challenge.portal + Vector3.up * 3.4f, finishPad.yaw, 0.5f, Color.white);
        }

        // A block: a pillar standing up from the floor with a cap on it (its top, a collar in the
        // room's colour, sometimes a square set into the top, sometimes a glowing edge)
        static void BuildBlock(Batch batch, System.Action<Vector3, Quaternion, Vector3, string> solid, in BhopPiece p, Quaternion rot, Theme th, float depth)
        {
            var size = new Vector3(p.size.y, 0f, p.size.x);
            float band = p.kind == BhopKind.Field ? 0.6f : th.bandH;
            float body = Mathf.Max(depth, band + 0.5f);
            bool tilted = p.kind == BhopKind.Slope || p.kind == BhopKind.Bank;
            batch.Box(th.cap, th.band, p.center, rot, new Vector3(size.x, Mathf.Max(band, 0.05f), size.z), 0.25f);
            if (tilted)
            {
                // a slope's or bank's tilted top on an upright pillar, its top tucked under the tilt
                var up = Quaternion.Euler(0f, p.yaw, 0f);
                float sink = Mathf.Max(size.x, size.z) * 0.5f * Mathf.Sin(p.tilt * Mathf.Deg2Rad) + 0.1f;
                batch.Box(th.pillar, th.pillar, p.center + Vector3.down * sink, up, new Vector3(size.x * 0.8f, Mathf.Max(0.5f, body - sink), size.z * 0.8f), 0.25f);
                solid(p.center, rot, new Vector3(size.x, 0.8f, size.z), p.kind.ToString());
                if (body - sink > 1f) solid(p.center + Vector3.down * (sink + 0.4f), up, new Vector3(size.x * 0.8f, body - sink - 0.4f, size.z * 0.8f), "Pillar");
            }
            else
            {
                batch.Box(th.pillar, th.pillar, p.center + Vector3.down * band, rot, new Vector3(size.x - 0.1f, body - band, size.z - 0.1f), 0.25f);
                solid(p.center, rot, new Vector3(size.x, body, size.z), p.kind.ToString());
            }
            if (th.inset && Mathf.Min(size.x, size.z) > 0.8f)
            {
                // the square set into the top
                float m = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.18f, 0.12f, 0.6f);
                Vector3 r = rot * Vector3.right * (size.x * 0.5f - m), f = rot * Vector3.forward * (size.z * 0.5f - m), lift = rot * Vector3.up * 0.004f;
                Vector3 c = p.center + lift;
                batch.Quad(th.inset, c - r - f, c - r + f, c + r + f, c + r - f, 0.25f);
            }
            if (th.rim) batch.Rim(th.rim, p.center, rot, new Vector2(size.x, size.z));
        }

        // A stage's room, its chambers joined: their floor (a miss lands you here, and back on the
        // pad), walls along the outside of them all with pilasters and a cornice, the room's own
        // decorations, and for the clean room a ceiling. Stage 1's back wall, over the plaza, has
        // a doorway where its pad is. Where chambers overlap there's no wall between them, and
        // their floors and ceilings are mapped in the world's own frame, so they meet seamlessly.
        static void BuildRoom(Batch b, Batch shade, System.Action<Vector3, Quaternion, Vector3, string> solid, BhopRoom[] all, Theme th, int s, BhopPiece? door)
        {
            var mine = new List<BhopRoom>();
            foreach (var r in all) if (r.stage == s) mine.Add(r);
            float W = BhopLayout.Wall;
            var up = Vector3.up;
            for (int ci = 0; ci < mine.Count; ci++)
            {
                var room = mine[ci];
                var rot = Quaternion.Euler(0f, room.yaw, 0f);
                Vector3 fw = rot * Vector3.forward, rt = rot * Vector3.right;
                float hf = room.size.x * 0.5f, hr = room.size.y * 0.5f;
                float floorY = room.Floor, topY = room.top, h = topY - floorY;
                var c = room.center;
                var floorSize = new Vector3(room.size.y + 2f * W, 1.5f, room.size.x + 2f * W);
                b.Slab(th.floor, th.wall, c, rot, floorSize, RoomUv);
                solid(c, rot, floorSize, "Floor");
                // (under it, a stepped base: the rooms float like the utopia's islands)
                b.Box(th.wallTrim, th.wall, c + up * -1.5f, rot, new Vector3(floorSize.x * 0.82f, 3f, floorSize.z * 0.82f), RoomUv);
                b.Box(th.wallTrim, th.wall, c + up * -4.5f, rot, new Vector3(floorSize.x * 0.55f, 4f, floorSize.z * 0.55f), RoomUv);

                // The stretches of a wall's middle line, from a0 to a1 along it, that aren't inside
                // another chamber
                List<(float a0, float a1)> Open(Vector3 mid, Vector3 along, float a0, float a1)
                {
                    var cuts = new List<(float, float)>();
                    for (int oi = 0; oi < mine.Count; oi++)
                    {
                        if (oi == ci) continue;
                        var o = mine[oi];
                        var orot = Quaternion.Euler(0f, o.yaw, 0f);
                        float lo = a0, hi = a1;
                        foreach (var (axis, half) in new[] { (orot * Vector3.forward, o.size.x * 0.5f), (orot * Vector3.right, o.size.y * 0.5f) })
                        {
                            float p0 = Vector3.Dot(mid - o.center, axis), dp = Vector3.Dot(along, axis);
                            if (Mathf.Abs(dp) < 1e-5f) { if (Mathf.Abs(p0) >= half) { lo = 1f; hi = 0f; } continue; }
                            float t0 = (-half - p0) / dp, t1 = (half - p0) / dp;
                            if (t0 > t1) (t0, t1) = (t1, t0);
                            lo = Mathf.Max(lo, t0);
                            hi = Mathf.Min(hi, t1);
                        }
                        if (hi > lo) cuts.Add((lo, hi));
                    }
                    cuts.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                    var open = new List<(float, float)>();
                    float at = a0;
                    foreach (var (x0, x1) in cuts)
                    {
                        if (x0 > at) open.Add((at, x0));
                        at = Mathf.Max(at, x1);
                    }
                    if (at < a1) open.Add((at, a1));
                    return open;
                }

                // a run of wall along one side, from below the floor to the top
                void Run(Vector3 mid, Vector3 along, float a0, float a1, float y0, float y1)
                {
                    if (a1 - a0 < 0.05f || y1 - y0 < 0.05f) return;
                    var wr = Quaternion.LookRotation(along);
                    var top = mid + along * ((a0 + a1) * 0.5f) + up * (y1 - floorY);
                    var size = new Vector3(W, y1 - y0, a1 - a0);
                    b.Box(th.wallTrim, th.wall, top, wr, size, RoomUv);
                    solid(top, wr, size, "Wall");
                }
                var sides = new (Vector3 mid, Vector3 along, Vector3 inward, float half)[]
                {
                    (c + fw * (hf + W * 0.5f), rt, -fw, hr + W),
                    (c - fw * (hf + W * 0.5f), rt, fw, hr + W),
                    (c + rt * (hr + W * 0.5f), fw, -rt, hf),
                    (c - rt * (hr + W * 0.5f), fw, rt, hf),
                };
                bool doorHere = door.HasValue && ci == 0;
                float doorAt = 0f, doorHalf = 3.5f, sill = 0f, lintel = 5.5f;
                if (doorHere) doorAt = Vector3.Dot(door.Value.center - c, rt);
                for (int k = 0; k < 4; k++)
                {
                    var (mid, along, inward, half) = sides[k];
                    var face = mid + inward * (W * 0.5f);
                    foreach (var (a0, a1) in Open(mid, along, -half, half))
                    {
                        if (k == 1 && doorHere)
                        {
                            Run(mid, along, a0, Mathf.Min(a1, doorAt - doorHalf), floorY - 1.5f, topY);
                            Run(mid, along, Mathf.Max(a0, doorAt + doorHalf), a1, floorY - 1.5f, topY);
                            Run(mid, along, Mathf.Max(a0, doorAt - doorHalf), Mathf.Min(a1, doorAt + doorHalf), floorY - 1.5f, sill);
                            Run(mid, along, Mathf.Max(a0, doorAt - doorHalf), Mathf.Min(a1, doorAt + doorHalf), lintel, topY);
                        }
                        else Run(mid, along, a0, a1, floorY - 1.5f, topY);
                        float len = a1 - a0, am = (a0 + a1) * 0.5f;
                        // the cornice along its top
                        b.Box(th.wallTrim, th.wallTrim, mid + along * am + up * (h + 0.35f), Quaternion.LookRotation(along), new Vector3(W + 0.9f, 0.6f, len + 0.9f), RoomUv);
                        // pilasters every 7m or so along its inside, a light strip along it near the top
                        int n = Mathf.Max(1, Mathf.RoundToInt(len / 7f));
                        for (int i = 0; i <= n; i++)
                        {
                            float a = a0 + 0.45f + (len - 0.9f) * i / n;
                            if (doorHere && k == 1 && Mathf.Abs(a - doorAt) < doorHalf + 0.6f) continue;
                            var at = face + along * a + inward * 0.2f;
                            b.Box(th.wallTrim, th.wallTrim, at + up * (h - 0.01f), Quaternion.LookRotation(along), new Vector3(0.4f, h, 0.9f), RoomUv);
                            if (s == 2 && i % 2 == 0) Lantern(b, at + inward * 0.6f + up * (h - 2.4f), th);
                            if (s == 8 && i % 2 == 1) Crystals(b, face + along * a + inward * 1.4f, inward, along, th, i + k * 7 + ci * 13);
                        }
                        if (th.strip && len > 1f)
                        {
                            b.Box(th.strip, th.strip, face + along * am + inward * 0.06f + up * (h - 1.6f), Quaternion.LookRotation(along), new Vector3(0.1f, 0.14f, len - 0.4f), 1f);
                            if (s == 4) b.Box(th.strip, th.strip, face + along * am + inward * 0.06f + up * (h * 0.45f), Quaternion.LookRotation(along), new Vector3(0.1f, 0.1f, len - 0.4f), 1f);
                        }
                    }
                }
                if (th.ceiling)
                {
                    // the clean room's ceiling (casting no shadow: the sun still lights it) and its
                    // grid of light panels
                    shade.Slab(th.ceiling, th.ceiling, c + up * (h + 0.8f), rot, new Vector3(room.size.y + 2f * W, 0.8f, room.size.x + 2f * W), RoomUv, under: true);
                    for (float a = -hf + 4f; a < hf - 2f; a += 8f)
                    for (float r = -hr + 4f; r < hr - 2f; r += 8f)
                        shade.Box(th.deco, th.deco, c + fw * a + rt * r + up * (h - 0.01f), rot, new Vector3(2.4f, 0.06f, 2.4f), 1f);
                }
            }
        }

        // A paper lantern on its post's arm, inside the garden's wall
        static void Lantern(Batch b, Vector3 at, Theme th)
        {
            b.Box(th.deco, th.deco, at, Quaternion.identity, new Vector3(0.7f, 1f, 0.7f), 1f);
            b.Box(th.deco2, th.deco2, at + Vector3.up * 0.14f, Quaternion.identity, new Vector3(0.82f, 0.14f, 0.82f), 1f);
            b.Box(th.deco2, th.deco2, at + Vector3.down * 0.98f, Quaternion.identity, new Vector3(0.82f, 0.1f, 0.82f), 1f);
        }

        // A cluster of crystals at a cave wall's foot, leaning out of it
        static void Crystals(Batch b, Vector3 foot, Vector3 inward, Vector3 along, Theme th, int seed)
        {
            for (int k = 0; k < 3; k++)
            {
                float hgt = 2.2f + ((seed * 7 + k * 3) % 5) * 0.6f, lean = 12f + k * 9f;
                var rot = Quaternion.LookRotation(along) * Quaternion.Euler(0f, 0f, 0f) * Quaternion.AngleAxis((k - 1) * 18f, Vector3.up) * Quaternion.AngleAxis(lean, Vector3.forward);
                var at = foot + along * ((k - 1) * 0.9f) + inward * (k % 2) * 0.5f;
                b.Box(th.deco, th.deco, at + rot * Vector3.up * hgt, rot * Quaternion.Euler(0f, 45f, 0f), new Vector3(0.6f, hgt, 0.6f), 1f);
                b.Box(th.deco2, th.deco2, at + rot * Vector3.up * (hgt + 0.5f), rot * Quaternion.Euler(0f, 45f, 0f), new Vector3(0.3f, 0.5f, 0.3f), 1f);
            }
        }

        // A stage's start pad: its number on the floor and its sign over the first hop
        static void StartSign(Transform stage, in BhopPiece p, int s, float heading, Material panel, Theme th)
        {
            var dir = Quaternion.Euler(0f, heading, 0f);
            Label($"STAGE {s}", stage, p.center + Vector3.up * 0.03f, heading, 1.2f, new Color(1f, 1f, 1f, 0.9f), pitch: 90f);
            if (s == 1) return; // (stage 1's is on the plaza's sign)
            var at = p.center + dir * Vector3.forward * (p.size.x * 0.5f + 0.4f) + Vector3.up * 3.6f;
            Deco("PadSign", stage, at + dir * Vector3.forward * 0.08f, new Vector3(5.2f, 1.7f, 0.1f), dir, panel);
            Deco("PadSignEdge", stage, at + dir * Vector3.forward * 0.02f + Vector3.down * 0.86f, new Vector3(5.3f, 0.06f, 0.06f), dir, th.band);
            Label($"STAGE {s}  ·  {BhopLayout.Stages[s - 1].name.ToUpper()}", stage, at + Vector3.up * 0.3f - dir * Vector3.forward * 0.02f, heading, 0.45f, Color.white);
            Label(BhopLayout.Stages[s - 1].line, stage, at - Vector3.up * 0.35f - dir * Vector3.forward * 0.02f, heading, 0.24f, new Color(0.85f, 0.9f, 1f));
        }

        // A stage's exit: an arch over its pad with the next stage's light in it. Land on the pad
        // and you're through, onto the next room's start pad.
        static readonly Dictionary<int, Material> portalGlows = new();
        static void ExitPortal(Batch b, Transform stage, in BhopPiece p, int s, Theme[] themes, Material gold)
        {
            var next = themes[s + 1];
            if (!portalGlows.TryGetValue(s + 1, out var glow) || !glow)
            {
                glow = LightMaterial(next.glow, null, 0.55f, 1.2f);
                glow.name = $"BhopPortal{s + 1}";
                AssetDatabase.CreateAsset(glow, $"{Root}/BhopPortal{s + 1}.mat");
                portalGlows[s + 1] = glow;
            }
            var face = Quaternion.Euler(0f, p.yaw, 0f);
            Vector3 side = face * Vector3.right, fwd = face * Vector3.forward;
            float half = p.size.y * 0.5f - 0.35f, hgt = 4.6f;
            var at = p.center + fwd * 0.6f;
            foreach (float sx in new[] { -1f, 1f })
                b.Box(gold, gold, at + side * (sx * half) + Vector3.up * hgt, face, new Vector3(0.45f, hgt, 0.45f), 0.5f);
            b.Box(gold, gold, at + Vector3.up * (hgt + 0.5f), face, new Vector3(half * 2f + 0.9f, 0.5f, 0.6f), 0.5f);
            b.Box(next.rim ?? next.band, next.rim ?? next.band, at + Vector3.up * (hgt + 0.02f) - fwd * 0.32f, face, new Vector3(half * 2f, 0.07f, 0.07f), 1f);
            b.Quad(glow, at - side * half + Vector3.up * 0.05f, at - side * half + Vector3.up * hgt, at + side * half + Vector3.up * hgt, at + side * half + Vector3.up * 0.05f, 0.25f);
            b.Quad(glow, at + side * half + Vector3.up * 0.05f, at + side * half + Vector3.up * hgt, at - side * half + Vector3.up * hgt, at - side * half + Vector3.up * 0.05f, 0.25f);
            Label($"STAGE {s + 1}", stage, at + Vector3.up * (hgt + 1.2f) - fwd * 0.4f, p.yaw, 0.7f, next.glow);
            Label("EXIT", stage, p.center + Vector3.up * 0.03f - fwd * 1.2f, p.yaw, 1f, new Color(1f, 1f, 1f, 0.85f), pitch: 90f);
        }

        // A surf ramp along its riding line: a prism whose 60 degree face the line runs down,
        // on the inside of its curve (so the face turns you), a lit ridge along its top. It
        // starts a little before the line, to catch a short landing.
        static void BuildRamp(Batch batch, Transform colliders, in BhopPiece p, Theme th, Material rampMat)
        {
            var line = p.line;
            int n = line.Length;
            float side = p.turn < 0f ? -1f : 1f; // the rider on the right of the ridge turning right
            var ridges = new Vector3[n + 1];
            var faces = new Vector3[n + 1];
            var backs = new Vector3[n + 1];
            var pts = new Vector3[n + 1];
            Vector3 f0 = (line[1] - line[0]); f0.y = 0f; f0.Normalize();
            pts[0] = line[0] - f0 * 3f;
            for (int k = 0; k < n; k++) pts[k + 1] = line[k];
            const float up = 2.4f, below = 4.2f;
            for (int k = 0; k <= n; k++)
            {
                Vector3 f = k < n ? pts[Mathf.Min(k + 1, n)] - pts[k] : pts[k] - pts[k - 1];
                f.y = 0f; f.Normalize();
                Vector3 r = Vector3.Cross(Vector3.up, f) * side; // toward the rider
                Vector3 alongFace = Vector3.up * 0.866f - r * 0.5f;   // up the 60 degree face
                ridges[k] = pts[k] + alongFace * up;
                faces[k] = pts[k] - alongFace * below;
                var v = faces[k] - ridges[k];
                backs[k] = ridges[k] + (v - 2f * Vector3.Dot(v, r) * r);
            }
            var mb = new List<Vector3>();
            var tris = new List<int>();
            void Q(Vector3 a, Vector3 b2, Vector3 c, Vector3 d)
            {
                // (wound so its normal faces out, toward the rider on the face)
                int s = mb.Count;
                mb.AddRange(new[] { a, b2, c, d });
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            var trim = th.rim ?? th.band;
            for (int k = 0; k < n; k++)
            {
                Vector3 R0 = ridges[k], R1 = ridges[k + 1], F0 = faces[k], F1 = faces[k + 1], B0 = backs[k], B1 = backs[k + 1];
                var nFace = Vector3.Cross(R1 - R0, F0 - R0);
                Vector3 toRider = Vector3.Cross(Vector3.up, pts[Mathf.Min(k + 1, n)] - pts[k]).normalized * side;
                if (Vector3.Dot(nFace, toRider) > 0f) { batch.Quad(rampMat, R0, R1, F1, F0, 0.15f); Q(R0, R1, F1, F0); }
                else { batch.Quad(rampMat, R0, F0, F1, R1, 0.15f); Q(R0, F0, F1, R1); }
                if (Vector3.Dot(Vector3.Cross(R1 - R0, B0 - R0), toRider) < 0f) { batch.Quad(th.pillar, R0, R1, B1, B0, 0.15f); Q(R0, R1, B1, B0); }
                else { batch.Quad(th.pillar, R0, B0, B1, R1, 0.15f); Q(R0, B0, B1, R1); }
                if (Vector3.Cross(F1 - F0, B0 - F0).y < 0f) batch.Quad(th.pillar, F0, F1, B1, B0, 0.15f);
                else batch.Quad(th.pillar, F0, B0, B1, F1, 0.15f);
                batch.Box(trim, trim, (R0 + R1) * 0.5f + Vector3.up * 0.06f, Quaternion.LookRotation(R1 - R0), new Vector3(0.12f, 0.12f, (R1 - R0).magnitude + 0.05f), 1f);
            }
            if (Vector3.Dot(Vector3.Cross(faces[0] - ridges[0], backs[0] - ridges[0]), -f0) > 0f) batch.Tri(th.pillar, ridges[0], faces[0], backs[0]);
            else batch.Tri(th.pillar, ridges[0], backs[0], faces[0]);
            Vector3 fn = pts[n] - pts[n - 1]; fn.y = 0f;
            if (Vector3.Dot(Vector3.Cross(faces[n] - ridges[n], backs[n] - ridges[n]), fn) > 0f) batch.Tri(th.pillar, ridges[n], faces[n], backs[n]);
            else batch.Tri(th.pillar, ridges[n], backs[n], faces[n]);
            var mesh = new Mesh { name = "BhopRamp" };
            mesh.SetVertices(mb);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, $"{Root}/Meshes/BhopRamp_{p.stage}_{Mathf.RoundToInt(p.center.x)}_{Mathf.RoundToInt(p.center.z)}.asset");
            var go = new GameObject("Ramp");
            go.transform.SetParent(colliders, false);
            go.isStatic = true;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // The plaza: a marble terrace closed at its far end by stage 1's wall. Over the wall's
        // doorway (stage 1's pad is in it) the challenge's sign; the prize turning on two
        // pedestals either side of the doorway; low parapets down its sides.
        static void BuildBhopPlaza(Transform root, System.Action<Vector3, Quaternion, Vector3, string> solid, List<BhopPiece> pieces, BhopRoom room1, BhopPiece pad, Theme[] themes, Material gold, Material marble)
        {
            var plaza = pieces.Find(p => p.kind == BhopKind.Plaza);
            var deco = new GameObject("Plaza").transform;
            deco.SetParent(root, false);
            var rot = Quaternion.Euler(0f, plaza.yaw, 0f);
            Vector3 fw = rot * Vector3.forward, rt = rot * Vector3.right, up = Vector3.up;
            float depth = plaza.size.x, width = plaza.size.y;
            var b = new Batch();
            var slab = new Vector3(width, 1.4f, depth);
            b.Box(marble, marble, plaza.center + Vector3.down * 0.02f, rot, slab, 0.25f); // (a touch under stage 1's pad, set into it)
            b.Rim(themes[0].rim, plaza.center, rot, new Vector2(width, depth), 0.08f);
            solid(plaza.center, rot, slab, "PlazaSlab");
            // parapets down both sides, and along the near edge either side of the trail's landing
            void Parapet(Vector3 mid, Vector3 along, float len)
            {
                var wr = Quaternion.LookRotation(along);
                var size = new Vector3(0.5f, 1.1f, len);
                b.Box(gold, marble, mid + up * 1.1f, wr, size, 0.25f);
                solid(mid + up * 1.1f, wr, size, "Parapet");
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                Parapet(plaza.center + rt * (sx * (width * 0.5f - 0.25f)), fw, depth);
                Parapet(plaza.center - fw * (depth * 0.5f - 0.25f) + rt * (sx * (width * 0.25f + 2f)), rt, width * 0.5f - 4f);
            }

            // The doorway's gold frame and the sign over it, on the wall's plaza side
            var wallFace = pad.center + fw * (pad.size.x * 0.5f) + up * 0f; // (the wall's outer face, at the pad's front edge)
            var doorC = wallFace - fw * 0.05f;
            foreach (float sx in new[] { -1f, 1f })
                b.Box(gold, gold, doorC + rt * (sx * 3.75f) + up * 5.9f, rot, new Vector3(0.5f, 5.9f, 0.3f), 0.5f);
            b.Box(gold, gold, doorC + up * 6.1f, rot, new Vector3(8f, 0.6f, 0.3f), 0.5f);
            b.Box(themes[0].rim, themes[0].rim, doorC - fw * 0.17f + up * 5.62f, rot, new Vector3(7f, 0.06f, 0.06f), 1f);
            b.Emit(deco, "Bhop_Plaza");

            var sign = new GameObject("BhopSign").transform;
            sign.SetParent(deco, false);
            Vector3 at = wallFace - fw * 0.12f + up * 8.3f;
            // (it faces the trail: its readable side looks back up the plaza)
            sign.SetPositionAndRotation(at, Quaternion.LookRotation(fw));
            // (dark like the stages' signs, so it reads from across the plaza)
            Material ink = Mat("BhopSignPanel", new Color(0.05f, 0.07f, 0.17f), metal, 0.6f, 0.3f);
            Material edge = MakeGlow("BhopSignEdge", new Color(0.3f, 0.9f, 1f), 1.6f);
            Deco("Panel", sign, new Vector3(0f, 0f, 0.06f), new Vector3(12f, 4.2f, 0.14f), Quaternion.identity, ink, local: true);
            foreach (float y in new[] { -2.15f, 2.15f })
                Deco("Edge", sign, new Vector3(0f, y, -0.02f), new Vector3(12.2f, 0.1f, 0.1f), Quaternion.identity, edge, local: true);
            foreach (float x in new[] { -6.05f, 6.05f })
                Deco("Edge", sign, new Vector3(x, 0f, -0.02f), new Vector3(0.1f, 4.4f, 0.1f), Quaternion.identity, edge, local: true);
            Label("10 STAGE BHOP CHALLENGE", sign, new Vector3(0f, 1.25f, -0.04f), 0f, 0.8f, Color.white, local: true);
            Label("for a karambit and matching gloves set", sign, new Vector3(0f, 0.35f, -0.04f), 0f, 0.42f, new Color(1f, 0.82f, 0.4f), local: true);
            Label("KARAMBIT | VELOCITY   +   VOID GLOVES | VELOCITY", sign, new Vector3(0f, -0.4f, -0.04f), 0f, 0.34f, new Color(0.35f, 0.9f, 1f), local: true);
            Label("ten rooms  ·  every stage's pad is a checkpoint  ·  touch the floor and you're back on it\nT  restart the stage   ·   R  back to stage 1", sign, new Vector3(0f, -1.35f, -0.04f), 0f, 0.26f, new Color(0.82f, 0.85f, 0.95f), local: true);

            // The prize on two pedestals either side of the doorway
            int knife = Skins.IndexOf(ItemSlot.Secondary, BhopChallenge.PrizeKnife), gloves = Skins.IndexOf(ItemSlot.Hands, BhopChallenge.PrizeGloves);
            var rampMat = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Ramp.mat");
            foreach (var (index, glove, off) in new[] { (knife, false, -6.5f), (gloves, true, 6.5f) })
            {
                if (index < 0) continue;
                var foot = wallFace - fw * 3.2f + rt * off;
                foot.y = plaza.center.y;
                Shape(PrimitiveType.Cylinder, "PrizePedestal", deco, foot + up * 0.55f, new Vector3(1.2f, 0.55f, 1.2f), hallMarble);
                Shape(PrimitiveType.Cylinder, "PrizeRing", deco, foot + up * 1.12f, new Vector3(1.3f, 0.03f, 1.3f), edge);
                var display = new GameObject(glove ? "Display Prize Gloves" : "Display Prize Knife").AddComponent<SkinDisplay>();
                display.transform.SetParent(deco, false);
                display.transform.position = foot + up * 2f;
                display.gallery = true;
                display.glove = glove;
                display.skinIndex = index;
                display.scale = glove ? 4f : 5f;
                display.maxHeight = 1.4f;
                display.shelfBelow = 0.8f;
                display.useRange = 3f;
                display.template = rampMat;
                Label(glove ? "VOID GLOVES | VELOCITY" : "KARAMBIT | VELOCITY", deco, foot + up * 0.6f - fw * 0.62f, plaza.yaw, 0.2f, new Color(0.3f, 0.9f, 1f));
            }
            PointLight("BhopPlazaLight", deco, plaza.center + up * 6f, new Color(0.7f, 0.95f, 1f), 1.2f, 24f);
        }
    }
}
