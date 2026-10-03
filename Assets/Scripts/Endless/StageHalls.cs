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
        public class Volume
        {
            public int stage;
            public List<Vector2> outline;   // convex, counter-clockwise, course-local x/z
            public float floor, ceiling;
            public bool openTop;
            public BiomeKit kit;

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

        public const float Margin = 95f, Headroom = 95f, Panel = 30f; // room for every line and speed, not just the designed one

        // A stage's hall round its course points (riding lines and flights)
        public static Volume Plan(int stage, List<Vector3> points, float depth, bool openTop, BiomeKit kit)
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
            return new Volume { stage = stage, outline = Hull(ring), floor = lo - depth, ceiling = hi + Headroom, openTop = openTop, kit = kit };
        }

        // Builds a hall's panels, leaving out any that stand inside a neighbouring hall
        public static GameObject Build(Volume v, List<Volume> neighbours, Transform parent, List<Mesh> meshes, Mesh cube)
        {
            var kit = v.kit;
            var faces = new Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<int> t)>();
            bool Outside(Vector3 c)
            {
                foreach (var o in neighbours) if (o.Contains(c, 0.5f)) return false;
                return true;
            }
            void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                if (!m || !Outside((a + b + c + d) * 0.25f)) return;
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
                Vector3 off = inward * 0.25f;
                Quad(m, p - up + off, q - up + off, q + up + off, p + up + off);
            }

            Material wall = kit.scenery ? kit.scenery : kit.slab, floor = kit.floor ? kit.floor : kit.slab, ceiling = kit.slab ? kit.slab : wall;
            var o = v.outline;
            Vector2 center = Vector2.zero;
            foreach (var p in o) center += p;
            center /= o.Count;

            // Walls, panel by panel, with glowing seams at the floor and ceiling, pilasters of
            // light every few panels, and light panels along the middle
            int panelIndex = 0;
            for (int i = 0; i < o.Count; i++)
            {
                Vector2 a2 = o[i], b2 = o[(i + 1) % o.Count];
                float len = Vector2.Distance(a2, b2);
                int cols = Mathf.Max(1, Mathf.CeilToInt(len / Panel)), rows = Mathf.Max(1, Mathf.CeilToInt((v.ceiling - v.floor) / Panel));
                Vector2 e = (b2 - a2) / len;
                Vector3 inward = new Vector3(-e.y, 0f, e.x); // left of a counter-clockwise edge points in
                for (int c = 0; c < cols; c++, panelIndex++)
                {
                    Vector2 p0 = a2 + (b2 - a2) * (c / (float)cols), p1 = a2 + (b2 - a2) * ((c + 1) / (float)cols);
                    for (int r = 0; r < rows; r++)
                    {
                        float y0 = Mathf.Lerp(v.floor, v.ceiling, r / (float)rows), y1 = Mathf.Lerp(v.floor, v.ceiling, (r + 1) / (float)rows);
                        Quad(wall, new Vector3(p0.x, y0, p0.y), new Vector3(p1.x, y0, p1.y), new Vector3(p1.x, y1, p1.y), new Vector3(p0.x, y1, p0.y));
                        // light panels in a band round the middle of the hall
                        if (r == rows / 2 && panelIndex % 3 == 1)
                        {
                            Vector3 m0 = new Vector3(p0.x, 0f, p0.y), m1 = new Vector3(p1.x, 0f, p1.y), d = (m1 - m0) * 0.2f;
                            float ya = Mathf.Lerp(y0, y1, 0.2f), yb = Mathf.Lerp(y0, y1, 0.8f);
                            Vector3 off = inward * 0.3f;
                            Quad(kit.glowAlt ? kit.glowAlt : kit.glow, (m0 + d).WithY(ya) + off, (m1 - d).WithY(ya) + off, (m1 - d).WithY(yb) + off, (m0 + d).WithY(yb) + off);
                        }
                    }
                    Vector3 f0 = new Vector3(p0.x, 0f, p0.y), f1 = new Vector3(p1.x, 0f, p1.y);
                    Strip(kit.glow, f0.WithY(v.floor + 1.5f), f1.WithY(v.floor + 1.5f), inward, 1.2f);
                    if (!v.openTop) Strip(kit.glow, f0.WithY(v.ceiling - 1.5f), f1.WithY(v.ceiling - 1.5f), inward, 1.2f);
                    if (panelIndex % 4 == 0)
                        for (int r = 0; r < rows; r++)
                            Strip(kit.glow, f0.WithY(Mathf.Lerp(v.floor, v.ceiling, r / (float)rows)), f0.WithY(Mathf.Lerp(v.floor, v.ceiling, (r + 1) / (float)rows)), inward, 1.6f);
                }
            }

            // Floor and ceiling: a grid of panels over the outline
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in o) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
            for (float x = minX; x < maxX; x += Panel)
                for (float z = minZ; z < maxZ; z += Panel)
                {
                    float x1 = Mathf.Min(x + Panel, maxX), z1 = Mathf.Min(z + Panel, maxZ);
                    if (!Inside(o, new Vector2((x + x1) * 0.5f, (z + z1) * 0.5f))) continue;
                    Quad(floor, new Vector3(x, v.floor, z), new Vector3(x1, v.floor, z), new Vector3(x1, v.floor, z1), new Vector3(x, v.floor, z1));
                    if (!v.openTop) Quad(ceiling, new Vector3(x, v.ceiling, z), new Vector3(x, v.ceiling, z1), new Vector3(x1, v.ceiling, z1), new Vector3(x1, v.ceiling, z));
                    // glowing grid lines on the floor every other panel
                    if (Mathf.RoundToInt((x - minX) / Panel) % 2 == 0)
                        Strip(kit.glow, new Vector3(x, v.floor, z), new Vector3(x, v.floor, z1), Vector3.up, 0.6f);
                }

            var go = new GameObject($"Hall {v.stage}");
            go.transform.SetParent(parent, false);
            foreach (var (m, f) in faces)
            {
                var mesh = new Mesh { name = "Architecture", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(f.v);
                mesh.SetNormals(f.n);
                var uv = new Vector2[f.v.Count];
                for (int i = 0; i < uv.Length; i++)
                {
                    Vector3 n = f.n[i], p = f.v[i];
                    Vector3 a = new(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                    uv[i] = (a.y >= a.x && a.y >= a.z ? new Vector2(p.x, p.z) : a.x >= a.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) / 4f;
                }
                mesh.uv = uv;
                mesh.SetTriangles(f.t, 0);
                mesh.RecalculateBounds();
                meshes.Add(mesh);
                var part = new GameObject("Architecture");
                part.transform.SetParent(go.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = part.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return go;
        }

        // A tube round a flight path, for clearing the panels it passes through (a doorway
        // between halls, a window onto the next one): a bundle of lines filling its section
        // (each as a flat triangle), close enough together that no panel it crosses is missed
        public static List<Vector3> FlightTube(IList<Vector3> path, float radius)
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
                foreach (float r in new[] { radius * 0.5f, radius })
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
