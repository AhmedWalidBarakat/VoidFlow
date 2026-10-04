using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Surf ramp geometry, shared by the endless course (at runtime) and the editor tools.
    //
    // Two kinds of ramp:
    //  - Prism: a free-standing ridge with two curved faces (wide ones build speed and catch
    //    you, thin ones are precise blades)
    //  - Slab: a flat, thin panel tilted to one side that bends along its length, toward
    //    the side it faces, like a banked turn
    //
    // A ramp is laid along a path: a ridge (prism) or top edge (slab) line whose slope and
    // heading blend between control points. The next ramp starts as a landing hill that
    // follows the arc you fly off the end of the last one, so long flights land smoothly.
    //
    // Rule for every bend: at ~2300 u/s it has to be long and gradual (radius around 150m+),
    // or the push needed to bend your path mostly goes sideways into the face and throws you
    // off faster than strafing can hold you on. Slabs bend toward their face, where the ramp
    // does the turning for you.
    public static class RampShapes
    {
        public enum Kind { Prism, Slab }

        public const float Gravity = 800f * PlayerMovement.SourceUnit;

        // Prism cross-section, one side, from the ridge down: starts at TopAngle and curves
        // steeper to BottomAngle. All steeper than ~45.6 degrees (normal.y < 0.7), so all
        // of it is surfable.
        public const float TopAngle = 50f, BottomAngle = 60f;
        const float ProfileStepWidth = 1f;

        // Slabs: flat face tilted SlabBank degrees, SlabThickness thick
        public const float SlabBank = 55f;
        const float SlabThickness = 0.6f;

        // Every ramp starts as a sharp wedge, growing to full size over its first TaperLength
        // metres, so arriving a little low slides you onto it instead of into a blunt wall
        const float TaperLength = 10f;

        static float Taper(RampPath path, int i)
        {
            if (!path.taper) return 1f;
            float t = Mathf.Clamp01(path.distance[i] / TaperLength);
            return Mathf.Max(0.06f, t * t * (3f - 2f * t));
        }

        const float PathStep = 1.25f; // mesh resolution along a ramp (fine, so curves stay smooth)
        public const float UvMeters = 2f;

        // Where you'd ride, as a fraction of the way down the face. Over the last SwingLength
        // metres the line rises toward the top for the launch. Flights are designed for a
        // launch from the middle line.
        public const float RideFraction = 0.45f;
        public const float SwingFraction = 0.15f;
        public const float SwingLength = 50f;

        public class RampPath
        {
            public Kind kind;
            public float width;       // prism: how far each face reaches out; slab: face width
            public float side = -1f;  // which way the face you ride points: -1 left, +1 right
            public bool taper = true; // start as a wedge (landing hills), or full size (drop-ins)
            public float bank = SlabBank; // slabs: how steeply the face leans (spins lean further, to hold the turn at speed)
            public readonly List<Vector3> ridge = new();    // prism ridge / slab top edge
            public readonly List<Vector3> forward = new();  // horizontal travel direction
            public readonly List<Vector3> right = new();    // horizontal right of travel
            public readonly List<float> distance = new();   // metres along the ramp

            public Vector3 End => ridge[^1];
            public Vector3 EndForward => forward[^1];
            public float Length => distance[^1];
            public float EndSlope
            {
                get
                {
                    Vector3 d = ridge[^1] - ridge[^2];
                    return d.y / new Vector2(d.x, d.z).magnitude;
                }
            }

            // How far below the ridge the bottom of the face is
            public float Depth => kind == Kind.Prism
                ? FaceDepth(width, width)
                : width * Mathf.Sin(bank * Mathf.Deg2Rad);

            // A point `fraction` of the way down the face you ride, at sample i
            public Vector3 FacePoint(int i, float fraction)
            {
                if (kind == Kind.Prism)
                    return ridge[i] + right[i] * (side * fraction * width) + Vector3.down * FaceDepth(width, fraction * width);
                float lean = bank * Mathf.Deg2Rad;
                return ridge[i] + (right[i] * (side * Mathf.Cos(lean)) + Vector3.down * Mathf.Sin(lean)) * (fraction * width);
            }

            // Where you'd ride at sample i: the middle line, rising toward the top over the
            // last SwingLength metres for the launch
            public Vector3 RideLine(int i)
            {
                float toEnd = Length - distance[i];
                return FacePoint(i, Mathf.Lerp(SwingFraction, RideFraction, toEnd / SwingLength));
            }
        }

        // Lays a ramp starting at `start`, heading along `startForward`. Its slope (rise per
        // metre) is given at control points (distance, slope), and optionally its bend
        // (degrees turned so far, positive = right) at (distance, bend) points. Before
        // `smoothFrom` metres they blend linearly (so a landing hill matches its flight arc
        // exactly); after it each change eases in and out, so dives, climbs and turns flow
        // into each other with no sudden kinks.
        public static RampPath Lay(Kind kind, float width, float side, Vector3 start, Vector3 startForward,
            (float s, float v)[] slopes, (float s, float v)[] bends, float smoothFrom = 0f)
        {
            var path = new RampPath { kind = kind, width = width, side = side };
            float baseHeading = Mathf.Atan2(startForward.x, startForward.z) * Mathf.Rad2Deg;
            float length = slopes[^1].s;
            int steps = Mathf.CeilToInt(length / PathStep);
            float ds = length / steps;
            Vector3 flat = start;
            float y = start.y;
            for (int i = 0; i <= steps; i++)
            {
                float d = i * ds;
                float h = (baseHeading + Interpolate(bends, d, smoothFrom)) * Mathf.Deg2Rad;
                path.ridge.Add(new Vector3(flat.x, y, flat.z));
                path.forward.Add(new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h)));
                path.right.Add(new Vector3(Mathf.Cos(h), 0f, -Mathf.Sin(h)));
                path.distance.Add(d);

                float mid = d + ds * 0.5f;
                float hMid = (baseHeading + Interpolate(bends, mid, smoothFrom)) * Mathf.Deg2Rad;
                flat += new Vector3(Mathf.Sin(hMid), 0f, Mathf.Cos(hMid)) * ds;
                y += Interpolate(slopes, mid, smoothFrom) * ds;
            }
            return path;
        }

        public static float Interpolate((float s, float v)[] points, float d, float smoothFrom = float.MaxValue)
        {
            if (points == null) return 0f;
            if (d <= points[0].s) return points[0].v;
            for (int i = 1; i < points.Length; i++)
                if (d <= points[i].s)
                {
                    float t = Mathf.InverseLerp(points[i - 1].s, points[i].s, d);
                    if (points[i - 1].s >= smoothFrom) t = t * t * (3f - 2f * t);
                    return Mathf.Lerp(points[i - 1].v, points[i].v, t);
                }
            return points[^1].v;
        }

        // The arc you fly at `speed` after leaving the end of a ramp from its middle riding
        // line: its slope and height as functions of distance flown along the ramp's end
        // direction, plus the launch point.
        public static (Func<float, float> slope, Func<float, float> height, Vector3 launch) Flight(RampPath from, float speed)
        {
            Vector3 launch = from.FacePoint(from.ridge.Count - 1, RideFraction);
            float k = from.EndSlope;
            float vf = speed / Mathf.Sqrt(1f + k * k), vy = vf * k;
            return (d => vy / vf - Gravity * d / (vf * vf),
                    d => launch.y + vy * d / vf - 0.5f * Gravity * (d / vf) * (d / vf),
                    launch);
        }

        public struct Landing
        {
            public float speed;       // design flight speed (m/s)
            public float gap;         // metres flown before the landing hill starts
            public float length;      // metres of landing hill
            public float clearStart;  // metres below the arc at the start of the hill
            public float clearEnd;    // ...and at its end (0 = it meets the arc)
            public float shift;       // sideways offset of the riding line (+ = right)
        }

        // The next ramp: its riding line starts `landing.gap` metres on from where you launch,
        // shifted sideways, as a landing hill that follows the flight arc. Then the given
        // shape, measured from the end of the landing hill. You ride the face you arrive on:
        // shifted right, you come in over its left face. Slabs bend toward their face.
        public static RampPath LandingRamp(RampPath from, Landing landing, Kind kind, float width,
            (float s, float v)[] shape, (float s, float v)[] bend, float bank = SlabBank)
        {
            var (arcSlope, arcY, launch) = Flight(from, landing.speed);
            float side = landing.shift > 0f ? -1f : 1f;
            Vector3 fwd = from.EndForward;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            // Where the riding line sits relative to the ridge / top edge
            var probe = new RampPath { kind = kind, width = width, side = side, bank = bank };
            probe.ridge.Add(Vector3.zero);
            probe.right.Add(Vector3.right);
            Vector3 line = probe.FacePoint(0, RideFraction);

            float rise = (landing.clearStart - landing.clearEnd) / landing.length;
            Vector3 lineStart = launch + fwd * landing.gap + right * landing.shift;
            Vector3 start = lineStart - right * line.x;
            start.y = arcY(landing.gap) - line.y - landing.clearStart;

            var slopes = new List<(float, float)>
            {
                (0f, arcSlope(landing.gap) + rise),
                (landing.length, arcSlope(landing.gap + landing.length) + rise),
            };
            foreach (var (d, slope) in shape) slopes.Add((landing.length + d, slope));

            (float, float)[] bends = null;
            if (bend != null)
            {
                var list = new List<(float, float)> { (0f, 0f) };
                foreach (var (d, degrees) in bend) list.Add((landing.length + d, degrees * side));
                bends = list.ToArray();
            }
            var path = Lay(kind, width, side, start, fwd, slopes.ToArray(), bends, landing.length);
            path.bank = bank;
            return path;
        }

        // A twin of a ramp: moved sideways by `offset` metres and down by `drop`, covering only
        // the middle of it, from `startAt` metres to `endBefore` short of the end, so it's
        // never in the way of the flight in or the launch out
        public static RampPath Offset(RampPath main, float offset, float drop, float startAt, float endBefore)
        {
            var copy = new RampPath { kind = main.kind, width = main.width, side = main.side, bank = main.bank };
            for (int i = 0; i < main.ridge.Count; i++)
            {
                float d = main.distance[i];
                if (d < startAt || d > main.Length - endBefore) continue;
                copy.ridge.Add(main.ridge[i] + main.right[i] * offset + Vector3.down * drop);
                copy.forward.Add(main.forward[i]);
                copy.right.Add(main.right[i]);
                copy.distance.Add(d - startAt);
            }
            return copy.ridge.Count >= 2 ? copy : null;
        }

        // The part of a ramp from `from` to `to` metres along it, ending square (no wedge)
        public static RampPath Slice(RampPath main, float from, float to)
        {
            var piece = new RampPath { kind = main.kind, width = main.width, side = main.side, bank = main.bank, taper = from <= 0f && main.taper };
            for (int i = 0; i < main.ridge.Count; i++)
            {
                float d = main.distance[i];
                if (d < from || d > to) continue;
                piece.ridge.Add(main.ridge[i]);
                piece.forward.Add(main.forward[i]);
                piece.right.Add(main.right[i]);
                piece.distance.Add(d - from);
            }
            return piece.ridge.Count >= 2 ? piece : null;
        }

        // A glowing gate (visual only) standing over a point on a ramp: two posts and a beam,
        // `width` apart, `height` tall, facing along `facing`. Marks the start of a stage.
        public static Mesh GateMesh(Vector3 foot, Vector3 facing, float width, float height, string name)
        {
            const float post = 0.8f;
            var mesh = new MeshBuilder(foot);
            Vector3 f = new Vector3(facing.x, 0f, facing.z).normalized, r = Vector3.Cross(Vector3.up, f).normalized;
            void Box(Vector3 center, Vector3 half)
            {
                Vector3 X = r * half.x, Y = Vector3.up * half.y, Z = f * half.z;
                Vector3 c = center;
                mesh.FlatQuad(c - X - Y - Z, c + X - Y - Z, c + X + Y - Z, c - X + Y - Z, -f, r);
                mesh.FlatQuad(c - X - Y + Z, c + X - Y + Z, c + X + Y + Z, c - X + Y + Z, f, r);
                mesh.FlatQuad(c - X - Y - Z, c - X - Y + Z, c - X + Y + Z, c - X + Y - Z, -r, f);
                mesh.FlatQuad(c + X - Y - Z, c + X - Y + Z, c + X + Y + Z, c + X + Y - Z, r, f);
                mesh.FlatQuad(c - X + Y - Z, c + X + Y - Z, c + X + Y + Z, c - X + Y + Z, Vector3.up, r);
                mesh.FlatQuad(c - X - Y - Z, c + X - Y - Z, c + X - Y + Z, c - X - Y + Z, Vector3.down, r);
            }
            foreach (float s in new[] { -1f, 1f })
                Box(foot + r * (s * width * 0.5f) + Vector3.up * (height * 0.5f), new Vector3(post * 0.5f, height * 0.5f, post * 0.5f));
            Box(foot + Vector3.up * height, new Vector3(width * 0.5f + post * 0.5f, post * 0.5f, post * 0.5f));
            return mesh.ToMesh(name);
        }

        // The far wall of a "hole": a slab facing back across a gap at the bottom, so you surf
        // inside a canyon. It covers only the middle of the ramp, from `startAt` metres to
        // `endBefore` metres short of the end, so it's never in the way of the flight in or
        // the launch out.
        public static RampPath HoleWall(RampPath main, float bottomGap, float startAt, float endBefore)
        {
            float bank = main.bank * Mathf.Deg2Rad;
            float separation = 2f * main.width * Mathf.Cos(bank) + bottomGap;
            var wall = new RampPath { kind = Kind.Slab, width = main.width, side = -main.side };
            for (int i = 0; i < main.ridge.Count; i++)
            {
                float d = main.distance[i];
                if (d < startAt || d > main.Length - endBefore) continue;
                wall.ridge.Add(main.ridge[i] + main.right[i] * (main.side * separation));
                wall.forward.Add(main.forward[i]);
                wall.right.Add(main.right[i]);
                wall.distance.Add(d - startAt);
            }
            return wall.ridge.Count >= 2 ? wall : null;
        }

        // A ring of glowing blocks (visual only), hung at the top of a big-air flight to fly
        // through. Vertices relative to `center`.
        public static Mesh RingMesh(Vector3 center, Vector3 facing, float radius, string name, float thickness = 0.5f, int blocks = 16)
        {
            var mesh = new MeshBuilder(center);
            Vector3 right = Vector3.Cross(Vector3.up, facing).normalized, up = Vector3.up;
            for (int b = 0; b < blocks; b++)
            {
                float a0 = b * Mathf.PI * 2f / blocks, a1 = (b + 0.8f) * Mathf.PI * 2f / blocks;
                Vector3 p0 = center + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * radius;
                Vector3 p1 = center + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * radius;
                Vector3 outward = (p0 + p1) * 0.5f - center;
                Vector3 inward = -outward.normalized * thickness;
                Vector3 depth = facing.normalized * thickness;
                mesh.FlatQuad(p0 - depth, p1 - depth, p1 + depth, p0 + depth, outward, facing);
                mesh.FlatQuad(p0 + inward - depth, p1 + inward - depth, p1 + inward + depth, p0 + inward + depth, -outward, facing);
                mesh.FlatQuad(p0 - depth, p1 - depth, p1 + inward - depth, p0 + inward - depth, -facing, right);
                mesh.FlatQuad(p0 + depth, p1 + depth, p1 + inward + depth, p0 + inward + depth, facing, right);
            }
            return mesh.ToMesh(name);
        }

        // Cross-section of one prism face: (distance out from the ridge, depth below it)
        public static List<Vector2> Profile(float width)
        {
            var profile = new List<Vector2> { Vector2.zero };
            int steps = Mathf.CeilToInt(width / ProfileStepWidth);
            float step = width / steps;
            for (int k = 0; k < steps; k++)
            {
                float angle = Mathf.Lerp(TopAngle, BottomAngle, (k + 0.5f) / steps) * Mathf.Deg2Rad;
                profile.Add(profile[^1] + new Vector2(step, step * Mathf.Tan(angle)));
            }
            return profile;
        }

        public static float FaceDepth(float width, float offset)
        {
            var profile = Profile(width);
            for (int k = 1; k < profile.Count; k++)
                if (offset <= profile[k].x)
                    return Mathf.Lerp(profile[k - 1].y, profile[k].y, Mathf.InverseLerp(profile[k - 1].x, profile[k].x, offset));
            return profile[^1].y;
        }

        // Collects triangles for a ramp mesh, flipping each one to face `outward`.
        // Vertices are relative to `origin`.
        class MeshBuilder
        {
            readonly Vector3 origin;
            public readonly List<Vector3> verts = new();
            readonly List<Vector2> uvs = new();
            readonly List<int> tris = new();

            public MeshBuilder(Vector3 origin) => this.origin = origin;

            public int Add(Vector3 p, Vector2 uv)
            {
                verts.Add(p - origin);
                uvs.Add(uv);
                return verts.Count - 1;
            }

            public void Tri(int a, int b, int c, Vector3 outward)
            {
                Vector3 normal = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (Vector3.Dot(normal, outward) < 0f) (b, c) = (c, b);
                tris.Add(a); tris.Add(b); tris.Add(c);
            }

            public void Quad(int a, int b, int c, int d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            // A flat-shaded quad with its own vertices
            public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector3 uAxis)
            {
                Vector3 vAxis = Vector3.Cross(outward.normalized, uAxis).normalized;
                Vector2 Uv(Vector3 p) => new Vector2(Vector3.Dot(p, uAxis), Vector3.Dot(p, vAxis)) / UvMeters;
                Quad(Add(a, Uv(a)), Add(b, Uv(b)), Add(c, Uv(c)), Add(d, Uv(d)), outward);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // The ramp's surf mesh, with vertices relative to path.ridge[0]
        public static Mesh BuildMesh(RampPath path, string name) =>
            path.kind == Kind.Prism ? PrismMesh(path, name) : SlabMesh(path, name);

        // Two curved faces meeting at a sharp ridge, plus the underside and end caps
        static Mesh PrismMesh(RampPath path, string name)
        {
            var profile = Profile(path.width);
            var profileArc = new List<float> { 0f };
            for (int k = 1; k < profile.Count; k++)
                profileArc.Add(profileArc[^1] + Vector2.Distance(profile[k - 1], profile[k]));

            var mesh = new MeshBuilder(path.ridge[0]);
            int n = path.ridge.Count, kCount = profile.Count, bottom = kCount - 1;

            Vector3 Point(int i, float side, int k) =>
                path.ridge[i] + (path.right[i] * (side * profile[k].x) + Vector3.down * profile[k].y) * Taper(path, i);

            // Faces: vertices shared within a face so it shades smoothly, but not across the
            // ridge, which stays sharp
            foreach (float side in new[] { -1f, 1f })
            {
                int first = mesh.verts.Count;
                for (int i = 0; i < n; i++)
                for (int k = 0; k < kCount; k++)
                    mesh.Add(Point(i, side, k), new Vector2(path.distance[i], profileArc[k]) / UvMeters);

                for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < kCount - 1; k++)
                {
                    int a = first + i * kCount + k, b = a + kCount;
                    mesh.Quad(a, b, b + 1, a + 1, path.right[i] * side + Vector3.up);
                }
            }

            for (int i = 0; i < n - 1; i++)
                mesh.FlatQuad(Point(i, -1f, bottom), Point(i + 1, -1f, bottom), Point(i + 1, 1f, bottom), Point(i, 1f, bottom),
                    Vector3.down, path.forward[i]);

            foreach (int i in new[] { 0, n - 1 })
            {
                Vector3 outward = i == 0 ? -path.forward[i] : path.forward[i];
                var outline = new List<Vector3>();
                for (int k = bottom; k >= 0; k--) outline.Add(Point(i, -1f, k));
                for (int k = 1; k <= bottom; k++) outline.Add(Point(i, 1f, k));

                Vector3 middle = Vector3.zero;
                foreach (var p in outline) middle += p;
                middle /= outline.Count;
                Vector3 right = path.right[i];
                Vector2 CapUv(Vector3 p) => new Vector2(Vector3.Dot(p, right), p.y) / UvMeters;

                int center = mesh.Add(middle, CapUv(middle));
                var ring = new List<int>();
                foreach (var p in outline) ring.Add(mesh.Add(p, CapUv(p)));
                for (int j = 0; j < ring.Count; j++)
                    mesh.Tri(center, ring[j], ring[(j + 1) % ring.Count], outward);
            }

            return mesh.ToMesh(name);
        }

        // A flat panel hanging down from its top edge toward the side it faces
        static Mesh SlabMesh(RampPath path, string name)
        {
            var mesh = new MeshBuilder(path.ridge[0]);
            int n = path.ridge.Count;
            var top = new Vector3[n];
            var bottom = new Vector3[n];
            var normal = new Vector3[n];
            SlabEdges(path, top, bottom, normal);

            int first = mesh.verts.Count;
            for (int i = 0; i < n; i++)
            {
                mesh.Add(top[i], new Vector2(path.distance[i], 0f) / UvMeters);
                mesh.Add(bottom[i], new Vector2(path.distance[i], path.width) / UvMeters);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = first + i * 2;
                mesh.Quad(a, a + 2, a + 3, a + 1, normal[i]);
            }

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 t0 = top[i], t1 = top[i + 1], b0 = bottom[i], b1 = bottom[i + 1];
                Vector3 tb0 = t0 - normal[i] * SlabThickness, tb1 = t1 - normal[i + 1] * SlabThickness;
                Vector3 bb0 = b0 - normal[i] * SlabThickness, bb1 = b1 - normal[i + 1] * SlabThickness;
                Vector3 downSlope = (b0 - t0).normalized;
                mesh.FlatQuad(tb0, tb1, bb1, bb0, -normal[i], path.forward[i]);
                mesh.FlatQuad(t0, t1, tb1, tb0, -downSlope, path.forward[i]);
                mesh.FlatQuad(b0, b1, bb1, bb0, downSlope, path.forward[i]);
            }

            foreach (int i in new[] { 0, n - 1 })
            {
                Vector3 outward = i == 0 ? -path.forward[i] : path.forward[i];
                Vector3 tb = top[i] - normal[i] * SlabThickness, bb = bottom[i] - normal[i] * SlabThickness;
                mesh.FlatQuad(top[i], bottom[i], bb, tb, outward, path.right[i]);
            }

            return mesh.ToMesh(name);
        }

        static void SlabEdges(RampPath path, Vector3[] top, Vector3[] bottom, Vector3[] normal)
        {
            float bank = path.bank * Mathf.Deg2Rad;
            for (int i = 0; i < path.ridge.Count; i++)
            {
                Vector3 down = path.right[i] * (path.side * Mathf.Cos(bank)) + Vector3.down * Mathf.Sin(bank);
                top[i] = path.ridge[i];
                bottom[i] = top[i] + down * (path.width * Taper(path, i));
                normal[i] = path.right[i] * (path.side * Mathf.Sin(bank)) + Vector3.up * Mathf.Cos(bank);
            }
        }

        // Thin glowing trim strips hanging under the bottom edges of a ramp (visual only),
        // like the lit edges on the ramps in boreas and the neon maps
        public static Mesh TrimMesh(RampPath path, string name)
        {
            const float height = 0.35f;
            var mesh = new MeshBuilder(path.ridge[0]);
            int n = path.ridge.Count;

            void Strip(Func<int, Vector3> edge, Vector3 outwardSide)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 a = edge(i), b = edge(i + 1);
                    Vector3 outward = path.right[i] * outwardSide.x + Vector3.up * outwardSide.y;
                    mesh.FlatQuad(a, b, b + Vector3.down * height, a + Vector3.down * height, outward, path.forward[i]);
                    mesh.FlatQuad(a, b, b + Vector3.down * height, a + Vector3.down * height, -outward, path.forward[i]);
                }
            }

            if (path.kind == Kind.Prism)
            {
                var profile = Profile(path.width);
                Vector2 last = profile[^1];
                foreach (float side in new[] { -1f, 1f })
                    Strip(i => path.ridge[i] + (path.right[i] * (side * last.x) + Vector3.down * last.y) * Taper(path, i), new Vector3(side, 0f, 0f));
            }
            else
            {
                var top = new Vector3[n];
                var bottom = new Vector3[n];
                var normal = new Vector3[n];
                SlabEdges(path, top, bottom, normal);
                Strip(i => bottom[i], new Vector3(path.side, 0f, 0f));
            }
            return mesh.ToMesh(name);
        }
    }
}
