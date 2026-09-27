using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // A solid cut from a side outline: the outline (and any holes through it) drawn in the
    // weapon's side plane as (z forward, y up), given thickness across X, with its edges
    // bevelled so it catches the light like moulded polymer rather than a block.
    public static class ProfileMesh
    {
        public static Mesh Extrude(Vector2[] outline, Vector2[][] holes, float halfWidth, float bevel, string name = "Profile")
        {
            var outer = Oriented(outline, true);
            var hs = new List<Vector2[]>();
            if (holes != null) foreach (var h in holes) hs.Add(Oriented(h, false));

            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();

            // Faces: the inset outline (and holes) at ±halfWidth
            var insetOuter = Inset(outer, bevel);
            var insetHoles = hs.ConvertAll(h => Inset(h, bevel));
            var flat = Triangulate(insetOuter, insetHoles, out var faceTris);
            foreach (float side in new[] { 1f, -1f })
            {
                int start = v.Count;
                foreach (var p in flat)
                {
                    v.Add(new Vector3(side * halfWidth, p.y, p.x));
                    n.Add(new Vector3(side, 0f, 0f));
                    uv.Add(new Vector2(p.x, p.y) / 0.1f);
                }
                for (int i = 0; i < faceTris.Count; i += 3)
                {
                    int a = start + faceTris[i], b = start + faceTris[i + 1], c = start + faceTris[i + 2];
                    if (side > 0f) tris.AddRange(new[] { a, c, b }); else tris.AddRange(new[] { a, b, c });
                }
            }

            // Walls with bevels, round every contour
            void Wall(Vector2[] c, Vector2[] inset)
            {
                int count = c.Length;
                float run = 0f;
                // rows: inset at +w, outer at +(w-b), outer at -(w-b), inset at -w
                var rows = new (Vector2[] pts, float x)[] { (inset, halfWidth), (c, halfWidth - bevel), (c, -(halfWidth - bevel)), (inset, -halfWidth) };
                int start = v.Count;
                for (int i = 0; i <= count; i++)
                {
                    int k = i % count;
                    if (i > 0) run += (c[k] - c[i - 1]).magnitude;
                    Vector2 prev = c[(k - 1 + count) % count], next = c[(k + 1) % count];
                    Vector2 tangent = (next - prev).normalized;
                    Vector2 outward = new(tangent.y, -tangent.x); // material on the left: outward is to the right
                    for (int r = 0; r < 4; r++)
                    {
                        Vector2 p = rows[r].pts[k];
                        v.Add(new Vector3(rows[r].x, p.y, p.x));
                        float sx = r == 0 ? 0.7f : r == 3 ? -0.7f : r == 1 ? 0.25f : -0.25f;
                        n.Add(new Vector3(sx, outward.y, outward.x).normalized);
                        uv.Add(new Vector2(run, rows[r].x) / 0.1f);
                    }
                }
                for (int i = 0; i < count; i++)
                    for (int r = 0; r < 3; r++)
                    {
                        int a = start + i * 4 + r, b = a + 1, c2 = a + 4, d = c2 + 1;
                        tris.AddRange(new[] { a, c2, b, b, c2, d });
                    }
            }
            Wall(outer, insetOuter);
            for (int h = 0; h < hs.Count; h++) Wall(hs[h], insetHoles[h]);

            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tris, 0);
            // Walls are wound by the contour's direction: flip any triangle facing inward
            var t = m.triangles; var vv = m.vertices; var nn = m.normals;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 fn = Vector3.Cross(vv[t[i + 1]] - vv[t[i]], vv[t[i + 2]] - vv[t[i]]);
                if (Vector3.Dot(fn, nn[t[i]] + nn[t[i + 1]] + nn[t[i + 2]]) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            m.triangles = t;
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static float Area(Vector2[] p)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++) { var q = p[i]; var r = p[(i + 1) % p.Length]; a += q.x * r.y - r.x * q.y; }
            return a * 0.5f;
        }

        // Outline counter-clockwise, holes clockwise: either way the material is on the left
        static Vector2[] Oriented(Vector2[] p, bool ccw)
        {
            var c = (Vector2[])p.Clone();
            if ((Area(c) > 0f) != ccw) System.Array.Reverse(c);
            return c;
        }

        // Every point moved in toward the material by `d`
        static Vector2[] Inset(Vector2[] c, float d)
        {
            var o = new Vector2[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                Vector2 a = c[(i - 1 + c.Length) % c.Length], p = c[i], b = c[(i + 1) % c.Length];
                Vector2 n1 = Left((p - a).normalized), n2 = Left((b - p).normalized);
                Vector2 m = (n1 + n2).normalized;
                float cos = Mathf.Max(0.35f, Vector2.Dot(m, n1));
                o[i] = p + m * (d / cos);
            }
            return o;
        }

        static Vector2 Left(Vector2 dir) => new(-dir.y, dir.x);

        // Ear clipping, with holes bridged into the outline first
        static List<Vector2> Triangulate(Vector2[] outer, List<Vector2[]> holes, out List<int> tris)
        {
            var poly = new List<Vector2>(outer);
            var sorted = new List<Vector2[]>(holes);
            sorted.Sort((a, b) => MaxX(b).CompareTo(MaxX(a)));
            foreach (var h in sorted)
            {
                int hi = 0;
                for (int i = 1; i < h.Length; i++) if (h[i].x > h[hi].x) hi = i;
                Vector2 hp = h[hi];
                int best = -1; float bestD = float.MaxValue;
                for (int i = 0; i < poly.Count; i++)
                {
                    float d = (poly[i] - hp).sqrMagnitude;
                    if (d >= bestD || poly[i].x < hp.x - 1e-4f) continue;
                    if (Blocked(hp, poly[i], poly, holes)) continue;
                    best = i; bestD = d;
                }
                if (best < 0)
                    for (int i = 0; i < poly.Count; i++)
                    {
                        float d = (poly[i] - hp).sqrMagnitude;
                        if (d < bestD && !Blocked(hp, poly[i], poly, holes)) { best = i; bestD = d; }
                    }
                var merged = new List<Vector2>();
                for (int i = 0; i <= best; i++) merged.Add(poly[i]);
                for (int i = 0; i <= h.Length; i++) merged.Add(h[(hi + i) % h.Length]);
                for (int i = best; i < poly.Count; i++) merged.Add(poly[i]);
                poly = merged;
            }

            tris = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < poly.Count; i++) idx.Add(i);
            int guard = poly.Count * poly.Count;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if (Cross(b - a, c - b) <= 1e-9f) continue; // reflex
                    bool inside = false;
                    for (int j = 0; j < idx.Count && !inside; j++)
                    {
                        int ij = idx[j];
                        if (ij == ia || ij == ib || ij == ic) continue;
                        var p = poly[ij];
                        if (p == a || p == b || p == c) continue;
                        inside = InTriangle(p, a, b, c);
                    }
                    if (inside) continue;
                    tris.AddRange(new[] { ia, ib, ic });
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) tris.AddRange(new[] { idx[0], idx[1], idx[2] });
            return poly;
        }

        static float MaxX(Vector2[] p) { float m = float.MinValue; foreach (var q in p) m = Mathf.Max(m, q.x); return m; }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 > 0f && d2 > 0f && d3 > 0f;
        }

        // Does the segment from a to b cross any edge (other than at its ends)?
        static bool Blocked(Vector2 a, Vector2 b, List<Vector2> poly, List<Vector2[]> holes)
        {
            bool Hits(Vector2 c, Vector2 d)
            {
                if (c == a || c == b || d == a || d == b) return false;
                float d1 = Cross(b - a, c - a), d2 = Cross(b - a, d - a), d3 = Cross(d - c, a - c), d4 = Cross(d - c, b - c);
                return d1 * d2 < 0f && d3 * d4 < 0f;
            }
            for (int i = 0; i < poly.Count; i++) if (Hits(poly[i], poly[(i + 1) % poly.Count])) return true;
            foreach (var h in holes)
                for (int i = 0; i < h.Length; i++) if (Hits(h[i], h[(i + 1) % h.Length])) return true;
            return false;
        }
    }
}
