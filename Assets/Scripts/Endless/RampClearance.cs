using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Where a ramp runs into a building (a spin curling back through its own walls, the next
    // ramp cutting across the corner of this one's hall, a drop through the floor), the building
    // gives way: every face of a building or its scenery that a ramp passes through is taken
    // out, so ramps never show through walls, floors or trim. Faces go whole (a wall panel, one
    // side of a block), and only the ones a ramp actually crosses.
    public static class RampClearance
    {
        const float Cell = 16f;

        // One ramp's triangles in world space, filed in a grid (made once, used against every
        // building near it)
        public class Ramps
        {
            public readonly List<Vector3> tris = new();
            public readonly List<Vector3> lo = new(), hi = new();
            public readonly Dictionary<Vector3Int, List<int>> grid = new();
            public Bounds bounds;
            public int Count => lo.Count;
        }

        public static Ramps Prepare(IEnumerable<MeshFilter> surfaces)
        {
            var r = new Ramps();
            bool any = false;
            foreach (var mf in surfaces)
            {
                var mesh = mf ? mf.sharedMesh : null;
                if (!mesh || !mesh.isReadable) continue;
                var v = mesh.vertices; var t = mesh.triangles; var tr = mf.transform;
                var w = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) w[i] = tr.TransformPoint(v[i]);
                for (int k = 0; k < t.Length; k += 3)
                {
                    int id = r.lo.Count;
                    Vector3 a = w[t[k]], b = w[t[k + 1]], c = w[t[k + 2]];
                    r.tris.Add(a); r.tris.Add(b); r.tris.Add(c);
                    Vector3 lo = Vector3.Min(a, Vector3.Min(b, c)), hi = Vector3.Max(a, Vector3.Max(b, c));
                    r.lo.Add(lo); r.hi.Add(hi);
                    if (any) { r.bounds.Encapsulate(lo); r.bounds.Encapsulate(hi); } else { r.bounds = new Bounds(lo, Vector3.zero); r.bounds.Encapsulate(hi); any = true; }
                    Vector3Int ca = Vector3Int.FloorToInt(lo / Cell), cb = Vector3Int.FloorToInt(hi / Cell);
                    for (int x = ca.x; x <= cb.x; x++)
                        for (int y = ca.y; y <= cb.y; y++)
                            for (int z = ca.z; z <= cb.z; z++)
                            {
                                var key = new Vector3Int(x, y, z);
                                if (!r.grid.TryGetValue(key, out var list)) r.grid[key] = list = new List<int>(4);
                                list.Add(id);
                            }
                }
            }
            return r;
        }

        public static int Cut(List<MeshFilter> buildings, List<Ramps> ramps)
        {
            ramps = ramps.FindAll(r => r != null && r.Count > 0);
            if (ramps.Count == 0) return 0;
            int removed = 0;
            var seen = new HashSet<int>();
            foreach (var mf in buildings)
            {
                var mesh = mf ? mf.sharedMesh : null;
                if (!mesh || !mesh.isReadable || mesh.subMeshCount != 1) continue;
                var tr = mf.transform;
                // whole building piece clear of every ramp: nothing to do
                Bounds mb = TransformBounds(tr, mesh.bounds);
                if (!ramps.Exists(r => r.bounds.Intersects(mb))) continue;
                var v = mesh.vertices; var t = mesh.triangles;
                var w = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) w[i] = tr.TransformPoint(v[i]);
                // Faces: triangles joined by shared corners (a quad's two halves, one side of a block)
                var parent = new int[v.Length];
                for (int i = 0; i < v.Length; i++) parent[i] = i;
                int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
                for (int k = 0; k < t.Length; k += 3)
                {
                    int r0 = Find(t[k]);
                    parent[Find(t[k + 1])] = r0;
                    parent[Find(t[k + 2])] = r0;
                }
                HashSet<int> cut = null;
                for (int k = 0; k < t.Length; k += 3)
                {
                    int face = Find(t[k]);
                    if (cut != null && cut.Contains(face)) continue;
                    Vector3 a = w[t[k]], b = w[t[k + 1]], c = w[t[k + 2]];
                    Vector3 lo = Vector3.Min(a, Vector3.Min(b, c)), hi = Vector3.Max(a, Vector3.Max(b, c));
                    bool hit = false;
                    foreach (var r in ramps)
                    {
                        if (hit) break;
                        if (lo.x > r.bounds.max.x || hi.x < r.bounds.min.x || lo.y > r.bounds.max.y || hi.y < r.bounds.min.y || lo.z > r.bounds.max.z || hi.z < r.bounds.min.z) continue;
                        Vector3Int ca = Vector3Int.FloorToInt(Vector3.Max(lo, r.bounds.min) / Cell), cb = Vector3Int.FloorToInt(Vector3.Min(hi, r.bounds.max) / Cell);
                        seen.Clear();
                        for (int x = ca.x; x <= cb.x && !hit; x++)
                            for (int y = ca.y; y <= cb.y && !hit; y++)
                                for (int z = ca.z; z <= cb.z && !hit; z++)
                                {
                                    if (!r.grid.TryGetValue(new Vector3Int(x, y, z), out var list)) continue;
                                    foreach (int id in list)
                                    {
                                        if (!seen.Add(id)) continue;
                                        Vector3 l2 = r.lo[id], h2 = r.hi[id];
                                        if (lo.x > h2.x || hi.x < l2.x || lo.y > h2.y || hi.y < l2.y || lo.z > h2.z || hi.z < l2.z) continue;
                                        if (TrianglesMeet(a, b, c, r.tris[id * 3], r.tris[id * 3 + 1], r.tris[id * 3 + 2])) { hit = true; break; }
                                    }
                                }
                    }
                    if (hit) (cut ??= new HashSet<int>()).Add(face);
                }
                if (cut == null) continue;
                var keep = new List<int>(t.Length);
                for (int k = 0; k < t.Length; k += 3)
                {
                    if (cut.Contains(Find(t[k]))) { removed++; continue; }
                    keep.Add(t[k]); keep.Add(t[k + 1]); keep.Add(t[k + 2]);
                }
                mesh.SetTriangles(keep, 0);
            }
            return removed;
        }

        static Bounds TransformBounds(Transform t, Bounds b)
        {
            var r = new Bounds(t.TransformPoint(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                r.Encapsulate(t.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1))));
            return r;
        }

        // Two triangles cross when an edge of either passes through the other (after the quick
        // test: no crossing while one lies wholly on one side of the other's plane)
        static bool TrianglesMeet(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f)
        {
            Vector3 n1 = Vector3.Cross(b - a, c - a);
            float dd = Vector3.Dot(n1, d - a), de = Vector3.Dot(n1, e - a), df = Vector3.Dot(n1, f - a);
            if ((dd > 0f && de > 0f && df > 0f) || (dd < 0f && de < 0f && df < 0f)) return false;
            Vector3 n2 = Vector3.Cross(e - d, f - d);
            float da = Vector3.Dot(n2, a - d), db = Vector3.Dot(n2, b - d), dc = Vector3.Dot(n2, c - d);
            if ((da > 0f && db > 0f && dc > 0f) || (da < 0f && db < 0f && dc < 0f)) return false;
            if (SegmentHits(a, b, d, e, f) || SegmentHits(b, c, d, e, f) || SegmentHits(c, a, d, e, f) ||
                SegmentHits(d, e, a, b, c) || SegmentHits(e, f, a, b, c) || SegmentHits(f, d, a, b, c)) return true;
            // lying (nearly) flat on each other: a corner of one within a few centimetres of the other
            return Touches(d, a, b, c, n1) || Touches(e, a, b, c, n1) || Touches(f, a, b, c, n1)
                || Touches(a, d, e, f, n2) || Touches(b, d, e, f, n2) || Touches(c, d, e, f, n2);
        }

        static bool Touches(Vector3 p, Vector3 a, Vector3 b, Vector3 c, Vector3 n)
        {
            float len = n.magnitude;
            if (len < 1e-9f) return false;
            float dist = Vector3.Dot(n, p - a) / len;
            if (dist > 0.05f || dist < -0.05f) return false;
            Vector3 q = p - n / len * dist;
            Vector3 v0 = c - a, v1 = b - a, v2 = q - a;
            float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
            float den = d00 * d11 - d01 * d01;
            if (Mathf.Abs(den) < 1e-12f) return false;
            float u = (d11 * d20 - d01 * d21) / den, v = (d00 * d21 - d01 * d20) / den;
            return u >= 0f && v >= 0f && u + v <= 1f;
        }

        static bool SegmentHits(Vector3 p, Vector3 q, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 dir = q - p, e1 = b - a, e2 = c - a;
            Vector3 h = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, h);
            if (det > -1e-7f && det < 1e-7f) return false;
            float inv = 1f / det;
            Vector3 s = p - a;
            float u = inv * Vector3.Dot(s, h);
            if (u < 0f || u > 1f) return false;
            Vector3 qv = Vector3.Cross(s, e1);
            float vv = inv * Vector3.Dot(dir, qv);
            if (vv < 0f || u + vv > 1f) return false;
            float tt = inv * Vector3.Dot(e2, qv);
            return tt >= 0f && tt <= 1f;
        }
    }
}
