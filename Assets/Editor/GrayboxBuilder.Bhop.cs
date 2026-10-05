using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow.EditorTools
{
    // The 10 stage bhop challenge behind the start hall (BhopLayout lays it out): out through an
    // archway in the hall's back wall, a terrace, a bhop trail of wide marble steps, and the
    // plaza with the challenge's sign and its prize on show; from there ten stages spiral down
    // round a white and gold spire, each in its own look (the first a sky of marble steps, then
    // a lantern garden, red spires, a white banked bowl, a null room, a neon grid, glass, a field
    // of crystal shards, a lava gauntlet, and a gold finale), with a checkpoint pad between each.
    // Every stage's blocks and decorations are merged into a few meshes per stage.
    public static partial class GrayboxBuilder
    {
        class Theme
        {
            public Material top, side, trim, deco, deco2;
            public float thick = 0.8f, pillar;  // block thickness; spires: how far each block's pillar runs down
            public Color glow;
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

            // A thin glowing line round the top edges of a box
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

            public void Emit(Transform parent, string name)
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
                    r.shadowCastingMode = lit ? ShadowCastingMode.Off : ShadowCastingMode.On;
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

        static Theme[] BhopThemes(Material marble, Material gold)
        {
            Material Glow(string n, Color c, float i) => MakeGlow("Bhop" + n, c, i);
            var t = new Theme[11];
            // 0: the trail and plaza, in the hall's marble and gold
            t[0] = new Theme { top = marble, side = marble, trim = Glow("TrailTrim", new Color(1f, 0.72f, 0.32f), 1.5f), thick = 1.2f, glow = new Color(1f, 0.75f, 0.4f) };
            var ivory = Mat("BhopIvory", new Color(0.96f, 0.95f, 0.92f), stone, 0.55f);
            t[1] = new Theme { top = marble, side = ivory, trim = Glow("SkyTrim", new Color(1f, 0.74f, 0.34f), 1.6f), deco = gold, thick = 1f, glow = new Color(1f, 0.78f, 0.4f) };
            t[2] = new Theme { top = Mat("BhopWood", new Color(0.42f, 0.26f, 0.15f), wood, 0.45f, 0f, 0.5f), side = Mat("BhopLacquer", new Color(0.55f, 0.05f, 0.04f), null, 0.75f),
                trim = Glow("LanternTrim", new Color(1f, 0.55f, 0.22f), 1.4f), deco = Glow("Lantern", new Color(1f, 0.6f, 0.25f), 2.2f), deco2 = Mat("BhopLanternCap", new Color(0.08f, 0.05f, 0.04f), null, 0.4f),
                thick = 0.45f, glow = new Color(1f, 0.6f, 0.3f) };
            t[3] = new Theme { top = Mat("BhopSpireTop", new Color(0.75f, 0.72f, 0.68f), stone, 0.35f), side = Mat("BhopTerracotta", new Color(0.62f, 0.16f, 0.1f), plaster, 0.25f, 0f, 0.35f),
                trim = Glow("SpireTrim", new Color(1f, 0.82f, 0.55f), 1.1f), thick = 1f, pillar = 34f, glow = new Color(1f, 0.5f, 0.35f) };
            t[4] = new Theme { top = Mat("BhopGloss", new Color(0.93f, 0.94f, 0.96f), null, 0.85f), side = Mat("BhopGlossSide", new Color(0.82f, 0.85f, 0.9f), null, 0.7f),
                trim = Glow("BowlTrim", new Color(0.3f, 0.9f, 1f), 1.8f), deco = Glow("BowlRing", new Color(0.3f, 0.9f, 1f), 1.4f), thick = 0.7f, glow = new Color(0.3f, 0.9f, 1f) };
            t[5] = new Theme { top = Mat("BhopNull", new Color(0.03f, 0.03f, 0.035f), null, 0.3f), side = Mat("BhopNullSide", new Color(0.05f, 0.05f, 0.055f), null, 0.3f),
                trim = Glow("NullTrim", new Color(0.95f, 0.97f, 1f), 1.3f), deco = Mat("BhopNullWhite", new Color(0.96f, 0.96f, 0.97f), tiles, 0.4f, 0f, 0.1f), thick = 0.5f, glow = Color.white };
            t[6] = new Theme { top = Mat("BhopNeon", new Color(0.05f, 0.06f, 0.16f), hex, 0.6f, 0.2f, 0.5f), side = Mat("BhopNeonSide", new Color(0.04f, 0.04f, 0.1f), null, 0.6f),
                trim = Glow("NeonTrim", new Color(1f, 0.25f, 0.85f), 2.2f), deco = Glow("NeonWire", new Color(0.25f, 0.9f, 1f), 2f), thick = 0.7f, glow = new Color(1f, 0.3f, 0.9f) };
            t[7] = new Theme { top = Mat("BhopGlass", new Color(0.62f, 0.86f, 0.95f), ice, 0.95f, 0.15f, 0.3f), side = Mat("BhopGlassSide", new Color(0.5f, 0.78f, 0.9f), null, 0.95f, 0.15f),
                trim = Glow("GlassTrim", new Color(0.85f, 0.97f, 1f), 1.8f), deco = Glow("GlassBeam", new Color(0.55f, 0.9f, 1f), 0.6f), thick = 0.6f, glow = new Color(0.6f, 0.95f, 1f) };
            t[8] = new Theme { top = Mat("BhopShard", new Color(0.2f, 0.07f, 0.32f), null, 0.9f, 0.3f), side = Mat("BhopShardSide", new Color(0.12f, 0.04f, 0.2f), null, 0.9f, 0.3f),
                trim = Glow("ShardTrim", new Color(0.75f, 0.4f, 1f), 2f), deco = Mat("BhopCrystal", new Color(0.45f, 0.25f, 0.75f), null, 0.95f, 0.4f), deco2 = Glow("ShardStar", new Color(0.85f, 0.75f, 1f), 1.6f), thick = 0.5f, glow = new Color(0.75f, 0.45f, 1f) };
            t[9] = new Theme { top = Mat("BhopBasalt", new Color(0.15f, 0.13f, 0.13f), stone, 0.3f), side = Mat("BhopBasaltSide", new Color(0.1f, 0.09f, 0.09f), stone, 0.25f),
                trim = Glow("LavaTrim", new Color(1f, 0.42f, 0.1f), 2f), deco = Glow("Magma", new Color(1f, 0.35f, 0.06f), 1.6f), thick = 0.8f, glow = new Color(1f, 0.45f, 0.15f) };
            t[10] = new Theme { top = marble, side = gold, trim = Glow("FinaleTrim", new Color(1f, 0.8f, 0.4f), 1.8f), deco = gold, thick = 0.8f, glow = new Color(1f, 0.8f, 0.4f) };
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

        // How far down from `top` is clear of every other piece (anything within 7m across):
        // pillars, beams and crystals under a block stop 6m short of whatever is below
        static List<BhopPiece> bhopPieces;
        static float ClearBelow(Vector3 top, float want)
        {
            float room = want;
            foreach (var q in bhopPieces)
            {
                if (q.kind == BhopKind.Again) continue;
                Vector3 at = q.kind == BhopKind.Ramp ? q.line[q.line.Length / 2] : q.center;
                if (q.kind == BhopKind.Ramp)
                {
                    foreach (var l in q.line)
                        if (l.y < top.y - 0.5f && new Vector2(l.x - top.x, l.z - top.z).magnitude < 9f) room = Mathf.Min(room, top.y - l.y - 9f);
                    continue;
                }
                if (at.y < top.y - 0.5f && new Vector2(at.x - top.x, at.z - top.z).magnitude < 7f + Mathf.Max(q.size.x, q.size.y) * 0.5f)
                    room = Mathf.Min(room, top.y - at.y - 6f);
            }
            return Mathf.Max(room, 0f);
        }

        // The pieces' heading, slope and bank as a rotation
        static Quaternion PieceRotation(in BhopPiece p) => Quaternion.Euler(0f, p.yaw, 0f) * (p.kind == BhopKind.Slope ? Quaternion.Euler(p.tilt, 0f, 0f) : p.kind == BhopKind.Bank ? Quaternion.Euler(0f, 0f, -p.tilt) : Quaternion.identity);

        static void BuildBhop(Transform hall, PlayerMovement player, Material marble, Material gold, Material rampMat)
        {
            var pieces = BhopLayout.Build(out var headings);
            bhopPieces = pieces;
            var root = new GameObject("BhopChallenge").transform;
            root.SetParent(hall, false);
            var themes = BhopThemes(marble, gold);
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(root, false);
            var batch = new Batch();
            var padTop = Mat("BhopPad", new Color(0.07f, 0.1f, 0.24f), metal, 0.7f, 0.3f);

            void Solid(Vector3 top, Quaternion rot, Vector3 size, string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(colliders, false);
                go.transform.SetPositionAndRotation(top - rot * Vector3.up * (size.y * 0.5f), rot);
                go.isStatic = true;
                go.AddComponent<BoxCollider>().size = size;
            }

            int stageNow = -1;
            Transform stageRoot = null;
            void Flush()
            {
                if (stageRoot) batch.Emit(stageRoot, $"Bhop_{stageRoot.name.Replace(" ", "")}");
            }
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p.stage != stageNow)
                {
                    Flush();
                    stageNow = p.stage;
                    stageRoot = new GameObject(p.stage == 0 ? "Trail" : $"Stage {p.stage}").transform;
                    stageRoot.SetParent(root, false);
                }
                var th = themes[Mathf.Clamp(p.stage, 0, 10)];
                var rot = PieceRotation(p);
                switch (p.kind)
                {
                    case BhopKind.Again: break;
                    case BhopKind.Ramp: BuildRamp(batch, colliders, p, th, rampMat); break;
                    case BhopKind.Pad:
                    {
                        // A checkpoint: a dark pad, a ring of the next stage's light, its number
                        int next = p.stage == 1 && i == 0 ? 1 : p.stage + 1;
                        var nt = themes[Mathf.Clamp(next, 1, 10)];
                        var size = new Vector3(p.size.y, 1.2f, p.size.x);
                        batch.Box(padTop, th.side ?? padTop, p.center, rot, size, 0.25f);
                        batch.Rim(nt.trim, p.center, rot, new Vector2(p.size.y - 0.3f, p.size.x - 0.3f), 0.1f);
                        Solid(p.center, rot, size, "Pad");
                        bool finish = next > 10;
                        float exit = finish ? p.yaw : headings[next - 1];
                        var dir = Quaternion.Euler(0f, exit, 0f);
                        Label(finish ? "FINISH" : $"STAGE {next}", stageRoot, p.center + Vector3.up * 0.03f, exit, finish ? 1.6f : 1.2f, new Color(1f, 1f, 1f, 0.9f), pitch: 90f);
                        if (!finish)
                        {
                            // its sign, hanging over the far edge: the stage, its name and what it asks
                            var at = p.center + dir * Vector3.forward * (p.size.x * 0.5f + 0.4f) + Vector3.up * 3.6f;
                            Deco("PadSign", stageRoot, at + dir * Vector3.forward * 0.08f, new Vector3(5.2f, 1.7f, 0.1f), dir, padTop);
                            Deco("PadSignEdge", stageRoot, at + dir * Vector3.forward * 0.02f + Vector3.down * 0.86f, new Vector3(5.3f, 0.06f, 0.06f), dir, nt.trim);
                            Label($"STAGE {next}  ·  {BhopLayout.Stages[next - 1].name.ToUpper()}", stageRoot, at + Vector3.up * 0.3f - dir * Vector3.forward * 0.02f, exit, 0.45f, Color.white);
                            Label(BhopLayout.Stages[next - 1].line, stageRoot, at - Vector3.up * 0.35f - dir * Vector3.forward * 0.02f, exit, 0.24f, new Color(0.85f, 0.9f, 1f));
                        }
                        break;
                    }
                    case BhopKind.Plaza:
                    {
                        batch.Cylinder(th.top, p.center + Vector3.down * 1.4f, p.size.x * 0.5f, 1.4f, 40);
                        batch.Ring(th.trim, p.center + Vector3.up * 0.01f, p.size.x * 0.5f - 0.15f, 0.08f, 48);
                        // (a flat disc: boxes turned round the circle hold you up)
                        for (int k = 0; k < 8; k++)
                            Solid(p.center, Quaternion.Euler(0f, k * 22.5f, 0f), new Vector3(p.size.x * 0.97f, 1.4f, p.size.x * 0.4f), "PlazaSlab");
                        break;
                    }
                    default:
                    {
                        var size = new Vector3(p.size.y, th.thick, p.size.x);
                        if (p.kind == BhopKind.Field) size.y = 1.2f;
                        float pillar = th.pillar > 0f && p.kind == BhopKind.Block ? ClearBelow(p.center, th.pillar) : 0f;
                        if (pillar > 0f)
                        {
                            // a red spire: the block is its cap, the pillar runs on down
                            batch.Box(th.top, th.side, p.center, rot, new Vector3(size.x, size.y + pillar, size.z), 0.25f);
                            Solid(p.center, rot, new Vector3(size.x, size.y + pillar, size.z), "Spire");
                        }
                        else
                        {
                            batch.Box(th.top, th.side, p.center, rot, size, 0.25f);
                            Solid(p.center, rot, size, p.kind.ToString());
                        }
                        batch.Rim(th.trim, p.center, rot, new Vector2(size.x, size.z));
                        Decorate(batch, stageRoot, p, th, rot, i);
                        break;
                    }
                }
            }
            Flush();

            // The spire in the middle, with a ring of light at each stage's pad
            var spire = new GameObject("Spire").transform;
            spire.SetParent(root, false);
            var sb = new Batch();
            var spireAt = new Vector3(BhopLayout.Spire.x, 0f, BhopLayout.Spire.y);
            var b = BhopBounds(pieces);
            float bottom = b.min.y - 30f, topY = 34f;
            sb.Cylinder(marble, spireAt + Vector3.up * bottom, BhopLayout.SpireRadius, topY - bottom, 32);
            for (float y = bottom + 8f; y < topY; y += 12f) sb.Cylinder(gold, spireAt + Vector3.up * y, BhopLayout.SpireRadius + 0.25f, 0.5f, 32);
            int st = 0;
            foreach (var p in pieces)
            {
                if (p.kind != BhopKind.Pad) continue;
                st++;
                var nt = themes[Mathf.Clamp(st, 1, 10)];
                sb.Ring(nt.trim, spireAt + Vector3.up * (p.center.y + 2f), BhopLayout.SpireRadius + 1.6f, 0.3f, 48);
                if (st <= 10)
                {
                    var toPad = new Vector3(p.center.x - spireAt.x, 0f, p.center.z - spireAt.z).normalized;
                    Label($"{st}", spire, spireAt + toPad * (BhopLayout.SpireRadius + 0.1f) + Vector3.up * (p.center.y + 5f), Mathf.Atan2(-toPad.x, -toPad.z) * Mathf.Rad2Deg, 4f, nt.glow);
                }
            }
            sb.Ring(gold, spireAt + Vector3.up * (topY + 6f), 11f, 0.6f, 64);
            sb.Ring(themes[0].trim, spireAt + Vector3.up * (topY + 6.2f), 11.6f, 0.15f, 64);
            sb.Cylinder(gold, spireAt + Vector3.up * topY, BhopLayout.SpireRadius + 0.8f, 1.2f, 32);
            sb.Emit(spire, "Bhop_Spire");
            var spireCol = new GameObject("SpireCollider");
            spireCol.transform.SetParent(colliders, false);
            spireCol.transform.position = spireAt + Vector3.up * ((bottom + topY) * 0.5f);
            var cc = spireCol.AddComponent<CapsuleCollider>();
            cc.radius = BhopLayout.SpireRadius;
            cc.height = topY - bottom;

            BuildBhopPlaza(root, pieces, headings, themes, gold);

            // The challenge itself: checkpoints, falls, the clock and the prize
            var challenge = root.gameObject.AddComponent<BhopChallenge>();
            challenge.pieces = pieces.ToArray();
            challenge.headings = headings;
            challenge.player = player;
            var area = b;
            area.Encapsulate(new Vector3(area.center.x, area.min.y - 60f, area.center.z));
            area.Encapsulate(new Vector3(area.center.x, area.max.y + 25f, area.center.z));
            area.Expand(new Vector3(60f, 0f, 60f));
            var max = area.max;
            max.z = -50.8f; // (from the hall's back wall out)
            area.max = max;
            challenge.area = area;
            var finishPad = pieces.FindLast(q => q.kind == BhopKind.Pad);
            challenge.portal = finishPad.center + Quaternion.Euler(0f, finishPad.yaw, 0f) * Vector3.forward * 3f;
            var portal = new Batch();
            portal.Ring(themes[10].trim, challenge.portal + Vector3.up * 0.05f, 1.4f, 0.12f, 32);
            portal.Ring(themes[0].trim, challenge.portal + Vector3.up * 2.6f, 1.4f, 0.12f, 32);
            // (a gold arch over the ring back up, and the finish's name on it)
            var face = Quaternion.Euler(0f, finishPad.yaw, 0f);
            Vector3 side = face * Vector3.right, back = face * Vector3.forward;
            foreach (float sx in new[] { -1f, 1f })
                portal.Box(gold, gold, finishPad.center + side * (sx * 3.6f) + back * 1.5f + Vector3.up * 6.5f, face, new Vector3(0.7f, 6.5f, 0.7f), 0.5f);
            portal.Box(gold, gold, finishPad.center + back * 1.5f + Vector3.up * 7.2f, face, new Vector3(8f, 0.7f, 0.9f), 0.5f);
            portal.Box(themes[10].trim, themes[10].trim, finishPad.center + back * 1.05f + Vector3.up * 6.55f, face, new Vector3(7.2f, 0.08f, 0.08f), 1f);
            portal.Emit(root, "Bhop_Portal");
            Label("FINISH", root, finishPad.center + back * 1.0f + Vector3.up * 5.8f, finishPad.yaw, 0.9f, new Color(1f, 0.85f, 0.45f));
            Label("BACK UP", root, challenge.portal + Vector3.up * 3.4f, finishPad.yaw, 0.5f, Color.white);
        }

        // Per stage: what gives it its look (lanterns, crystals, beams...), round each block
        static void Decorate(Batch batch, Transform stage, in BhopPiece p, Theme th, Quaternion rot, int i)
        {
            Vector3 c = p.center, right = rot * Vector3.right, down = Vector3.down;
            switch (p.stage)
            {
                case 1: // a gold finial hanging under each marble step
                    batch.Box(th.deco, th.deco, c + down * th.thick, rot, new Vector3(0.5f, Mathf.Min(1.4f, ClearBelow(c, 1.4f)), 0.5f), 1f);
                    break;
                case 5: // a white tile floating under each black block, its shadow in the void
                    if (ClearBelow(c, 4f) >= 4f) batch.Box(th.deco, th.deco, c + down * 3.5f, rot, new Vector3(p.size.y * 1.6f, 0.08f, p.size.x * 1.6f), 0.5f);
                    break;
                case 9: // magma dripping from under each block
                    batch.Box(th.deco, th.deco, c + down * th.thick, rot * Quaternion.Euler(0f, 45f, 0f), new Vector3(p.size.y * 0.3f, Mathf.Min(2.2f, ClearBelow(c, 2.2f)), p.size.x * 0.3f), 1f);
                    break;
                case 2: // a paper lantern floating beside every other plank
                    if (i % 2 == 0)
                    {
                        var at = c + right * (p.size.y * 0.5f + 1.6f) * (i % 4 == 0 ? 1f : -1f) + Vector3.up * 1.2f;
                        batch.Box(th.deco, th.deco, at, rot, new Vector3(0.6f, 0.9f, 0.6f), 1f);
                        batch.Box(th.deco2, th.deco2, at + Vector3.up * 0.12f, rot, new Vector3(0.7f, 0.12f, 0.7f), 1f);
                    }
                    break;
                case 4: // a ring of light round each bank, below it
                    batch.Ring(th.deco, c + down * 3f, Mathf.Max(p.size.x, p.size.y) * 0.7f, 0.08f, 20);
                    break;
                case 6: // wireframe cubes hanging in the dark
                    if (i % 3 == 0)
                    {
                        var at = c + right * 6f * (i % 2 == 0 ? 1f : -1f) + Vector3.up * 2f;
                        float s = 1.6f;
                        foreach (var e in new[] { Vector3.right, Vector3.up, Vector3.forward })
                            for (int a = -1; a <= 1; a += 2)
                                for (int d = -1; d <= 1; d += 2)
                                {
                                    Vector3 o1 = e == Vector3.right ? Vector3.up : Vector3.right, o2 = e == Vector3.forward ? Vector3.up : Vector3.forward;
                                    var mid = at + (o1 * a + o2 * d) * s * 0.5f + e * s * 0.5f;
                                    batch.Box(th.deco, th.deco, mid + Vector3.up * 0.025f, Quaternion.LookRotation(e == Vector3.up ? Vector3.forward : e, e == Vector3.up ? Vector3.right : Vector3.up), new Vector3(0.05f, 0.05f, s), 1f);
                                }
                    }
                    break;
                case 7: // a pale beam of light falling from each glass block
                {
                    float beam = ClearBelow(c, 18f);
                    if (beam > 1f) batch.Box(th.deco, th.deco, c + down * th.thick, rot, new Vector3(0.18f, beam, 0.18f), 1f);
                    break;
                }
                case 8: // a crystal hanging under each shard, and stars
                    batch.Box(th.deco, th.deco, c + down * th.thick, rot * Quaternion.Euler(0f, 45f, 0f), new Vector3(p.size.y * 0.45f, Mathf.Min(2.6f, ClearBelow(c, 2.6f)), p.size.x * 0.45f), 1f);
                    if (i % 2 == 0) batch.Box(th.deco2, th.deco2, c + right * 9f + down * 6f, rot, Vector3.one * 0.18f, 1f);
                    break;
            }
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
            for (int k = 0; k < n; k++)
            {
                Vector3 R0 = ridges[k], R1 = ridges[k + 1], F0 = faces[k], F1 = faces[k + 1], B0 = backs[k], B1 = backs[k + 1];
                var nFace = Vector3.Cross(R1 - R0, F0 - R0);
                Vector3 toRider = Vector3.Cross(Vector3.up, pts[Mathf.Min(k + 1, n)] - pts[k]).normalized * side;
                if (Vector3.Dot(nFace, toRider) > 0f) { batch.Quad(rampMat, R0, R1, F1, F0, 0.15f); Q(R0, R1, F1, F0); }
                else { batch.Quad(rampMat, R0, F0, F1, R1, 0.15f); Q(R0, F0, F1, R1); }
                if (Vector3.Dot(Vector3.Cross(R1 - R0, B0 - R0), toRider) < 0f) { batch.Quad(th.side, R0, R1, B1, B0, 0.15f); Q(R0, R1, B1, B0); }
                else { batch.Quad(th.side, R0, B0, B1, R1, 0.15f); Q(R0, B0, B1, R1); }
                if (Vector3.Cross(F1 - F0, B0 - F0).y < 0f) batch.Quad(th.side, F0, F1, B1, B0, 0.15f);
                else batch.Quad(th.side, F0, B0, B1, F1, 0.15f);
                batch.Box(th.trim, th.trim, (R0 + R1) * 0.5f + Vector3.up * 0.06f, Quaternion.LookRotation(R1 - R0), new Vector3(0.12f, 0.12f, (R1 - R0).magnitude + 0.05f), 1f);
            }
            if (Vector3.Dot(Vector3.Cross(faces[0] - ridges[0], backs[0] - ridges[0]), -f0) > 0f) batch.Tri(th.side, ridges[0], faces[0], backs[0]);
            else batch.Tri(th.side, ridges[0], backs[0], faces[0]);
            Vector3 fn = pts[n] - pts[n - 1]; fn.y = 0f;
            if (Vector3.Dot(Vector3.Cross(faces[n] - ridges[n], backs[n] - ridges[n]), fn) > 0f) batch.Tri(th.side, ridges[n], faces[n], backs[n]);
            else batch.Tri(th.side, ridges[n], backs[n], faces[n]);
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

        // The plaza: the challenge's sign facing the trail, its prize turning on two pedestals
        static void BuildBhopPlaza(Transform root, List<BhopPiece> pieces, float[] headings, Theme[] themes, Material gold)
        {
            var plaza = pieces.Find(p => p.kind == BhopKind.Plaza);
            var deco = new GameObject("Plaza").transform;
            deco.SetParent(root, false);
            Material ink = hallMarble;
            Material edge = MakeGlow("BhopSignEdge", new Color(0.3f, 0.9f, 1f), 1.6f);
            // The sign stands at the plaza's far side from the trail, facing back up it
            var sign = new GameObject("BhopSign").transform;
            sign.SetParent(deco, false);
            Vector3 at = plaza.center + new Vector3(-6.2f, 5.2f, -2.5f);
            sign.SetPositionAndRotation(at, Quaternion.Euler(0f, 160f, 0f));
            Deco("Panel", sign, new Vector3(0f, 0f, 0.06f), new Vector3(9f, 5.2f, 0.14f), Quaternion.identity, ink, local: true);
            foreach (float y in new[] { -2.65f, 2.65f })
                Deco("Edge", sign, new Vector3(0f, y, -0.02f), new Vector3(9.2f, 0.1f, 0.1f), Quaternion.identity, edge, local: true);
            foreach (float x in new[] { -4.55f, 4.55f })
            {
                Deco("Edge", sign, new Vector3(x, 0f, -0.02f), new Vector3(0.1f, 5.4f, 0.1f), Quaternion.identity, edge, local: true);
                Deco("Post", sign, new Vector3(x, -3.9f, 0.06f), new Vector3(0.3f, 3f, 0.3f), Quaternion.identity, gold, local: true);
            }
            Label("10 STAGE", sign, new Vector3(0f, 1.75f, -0.04f), 0f, 0.7f, Ink, local: true);
            Label("BHOP CHALLENGE", sign, new Vector3(0f, 0.9f, -0.04f), 0f, 0.85f, Ink, local: true);
            Label("for a karambit and matching gloves set", sign, new Vector3(0f, 0.15f, -0.04f), 0f, 0.34f, Bronze, local: true);
            Label("KARAMBIT | VELOCITY   +   VOID GLOVES | VELOCITY", sign, new Vector3(0f, -0.5f, -0.04f), 0f, 0.3f, new Color(0.05f, 0.35f, 0.5f), local: true);
            Label("finish all 10 stages  ·  every stage's pad is a checkpoint\nT  restart the stage   ·   R  back to stage 1", sign, new Vector3(0f, -1.55f, -0.04f), 0f, 0.24f, Bronze, local: true);

            // The prize on two pedestals by the sign
            int knife = Skins.IndexOf(ItemSlot.Secondary, BhopChallenge.PrizeKnife), gloves = Skins.IndexOf(ItemSlot.Hands, BhopChallenge.PrizeGloves);
            var rampMat = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Ramp.mat");
            foreach (var (index, glove, off) in new[] { (knife, false, -2.6f), (gloves, true, 2.6f) })
            {
                if (index < 0) continue;
                var foot = at + sign.rotation * new Vector3(off, 0f, -2.2f);
                foot.y = plaza.center.y;
                Shape(PrimitiveType.Cylinder, "PrizePedestal", deco, foot + Vector3.up * 0.55f, new Vector3(1.2f, 0.55f, 1.2f), hallMarble);
                Shape(PrimitiveType.Cylinder, "PrizeRing", deco, foot + Vector3.up * 1.12f, new Vector3(1.3f, 0.03f, 1.3f), edge);
                var display = new GameObject(glove ? "Display Prize Gloves" : "Display Prize Knife").AddComponent<SkinDisplay>();
                display.transform.SetParent(deco, false);
                display.transform.position = foot + Vector3.up * 2f;
                display.gallery = true;
                display.glove = glove;
                display.skinIndex = index;
                display.scale = glove ? 4f : 5f;
                display.maxHeight = 1.4f;
                display.shelfBelow = 0.8f;
                display.useRange = 3f;
                display.template = rampMat;
            }
            PointLight("BhopPlazaLight", deco, plaza.center + Vector3.up * 6f, new Color(0.7f, 0.95f, 1f), 1.2f, 24f);
        }
    }
}
