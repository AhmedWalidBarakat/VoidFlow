using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace VoidFlow.EditorTools
{
    // Generates a boreas-style surf test map: ramps spaced far apart, each one shifted
    // sideways from the last, mixing three kinds:
    //  - wide prisms: big two-sided ramps that build speed and catch you after hard parts
    //  - thin prisms: narrow two-sided blades for the precise flights
    //  - slabs: flat, thin glass-like panels tilted to one side, bending along their length
    // Each ramp rises gently at the end. Swing up toward the top there and you launch a long
    // way, air strafing sideways onto the next ramp, which starts as a landing hill shaped
    // to the flight arc, so you touch down smoothly and keep your speed.
    // Run from the menu (VoidFlow > Rebuild Graybox Map) after changing the layout below.
    public static class GrayboxBuilder
    {
        const string Root = "Assets/Graybox";
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";
        const float UvMeters = 2f;          // one grid tile = 2m
        const float StripeTileMeters = 16f; // wall stripe pattern repeats every 16m of height

        // Prism cross-section, one side, from the ridge down. Thin blades reach 6m out (~8m
        // deep), wide prisms 18m (~25m deep). The face starts at 50 degrees and curves steeper
        // to 60 at the bottom. All of it is steeper than ~45.6 degrees (normal.y < 0.7), so
        // every part is surfable. Steeper faces turn more of the ramp's push sideways when the
        // ridge bends, which throws you off at speed.
        public const float Thin = 6f, Wide = 18f;
        const float TopAngle = 50f, BottomAngle = 60f;
        const float ProfileStepWidth = 1.5f;

        // Slabs: a flat face SlabWidth metres down its slope, tilted SlabBank degrees, and
        // SlabThickness thick. They bend toward the side their face points, like a banked
        // turn, so the ramp itself carries you around the bend.
        const float SlabWidth = 12f;
        const float SlabBank = 55f;
        const float SlabThickness = 0.6f;

        const float PathStep = 2f; // ramp mesh resolution along its length

        // Flights between ramps. The next ramp starts FlightGap metres on from the end of the
        // last, in the direction it was pointing, with its riding line shifted sideways. It
        // follows the arc for LandingLength metres: 6m below you at first, rising to meet
        // you near the end, so you land somewhere along it whether you launched from high or
        // low on the face, fast or slow.
        //
        // Rule for every bend in a ramp: at ~2300 u/s it has to be long and gradual (radius
        // around 150m+), or the push needed to bend your path mostly goes sideways into the
        // face and throws you off the ramp faster than strafing can hold you on. Slabs bend
        // toward their face, where the ramp does the turning for you.
        const float FlightGap = 35f;
        const float LandingLength = 40f;
        const float LandingClearStart = 6f, LandingClearEnd = 0f;
        const float Gravity = 800f * PlayerMovement.SourceUnit;
        const float MaxDesignSpeed = 2900f * PlayerMovement.SourceUnit; // about what a good run reaches

        // The line the bot swings around, as a fraction of the way down the face; also
        // roughly where you'd ride. Over the last SwingLength metres of a ramp the line moves
        // up toward the top for the launch. Flights are designed for a launch from the middle
        // line, the lowest likely one.
        const float RideFraction = 0.45f;
        const float SwingFraction = 0.15f;
        const float SwingLength = 50f;

        // Ramp shapes after the landing hill, measured from its end: the slope (rise per
        // metre) at control points, and for slabs how far (degrees) they've bent by then.
        // Blade: level out, rise gently for the launch. Dive: keep diving to rebuild speed,
        // level out over a long bend, then rise. Bend: a slab that curves 20 degrees.
        static readonly (float, float)[] Blade = { (120f, 0f), (160f, 0.08f) };
        static readonly (float, float)[] Dive = { (80f, -0.35f), (200f, 0f), (250f, 0.08f) };
        static readonly (float, float)[] Bend = { (30f, 0f), (150f, 20f), (160f, 20f) };

        // Where the bot aims, as (x, z) in order: a line along the face you ride on every
        // ramp, then the finish. Filled in by Build() from the layout so the bot always matches.
        public static readonly List<Vector2> BotRoute = new();

        enum Kind { Prism, Slab }

        class RampPath
        {
            public Kind kind;
            public float width;       // prism: how far each face reaches out; slab: face width
            public float side = -1f;  // which way the face you ride points: -1 left, +1 right
            public readonly List<Vector3> ridge = new();    // prism ridge / slab top edge
            public readonly List<Vector3> forward = new();  // horizontal travel direction
            public readonly List<Vector3> right = new();    // horizontal right of travel
            public readonly List<float> distance = new();   // metres along the ramp

            public Vector3 End => ridge[^1];
            public Vector3 EndForward => forward[^1];
            public float EndSlope
            {
                get
                {
                    Vector3 d = ridge[^1] - ridge[^2];
                    return d.y / new Vector2(d.x, d.z).magnitude;
                }
            }

            // A point `fraction` of the way down the face you ride, at sample i
            public Vector3 FacePoint(int i, float fraction)
            {
                if (kind == Kind.Prism)
                    return ridge[i] + right[i] * (side * fraction * width) + Vector3.down * FaceDepth(width, fraction * width);
                float bank = SlabBank * Mathf.Deg2Rad;
                return ridge[i] + (right[i] * (side * Mathf.Cos(bank)) + Vector3.down * Mathf.Sin(bank)) * (fraction * width);
            }
        }

        [MenuItem("VoidFlow/Rebuild Graybox Map")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Root + "/Meshes");
            Texture2D grid = MakeGridTexture();
            Texture2D stripes = MakeStripeTexture();
            Material rampMat = MakeMaterial("Ramp", new Color(0.9f, 0.89f, 0.93f), grid);
            Material slabMat = MakeMaterial("Slab", new Color(0.62f, 0.8f, 0.95f), grid);
            Material wallMat = MakeMaterial("Wall", Color.white, stripes);
            Material floorMat = MakeMaterial("Floor", new Color(0.35f, 0.38f, 0.45f), grid);
            Material startMat = MakeMaterial("Start", new Color(0.75f, 0.75f, 0.78f), grid);
            Material edgeMat = MakeMaterial("Edge", new Color(0.93f, 0.42f, 0.12f), grid);
            Material endMat = MakeMaterial("Finish", new Color(0.95f, 0.75f, 0.25f), grid);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            var map = new GameObject("Map").transform;

            // Ramp 1 is a wide prism: the big drop that builds your speed. After it, placed
            // like boreas: slabs that bend you around, thin blades between them, and wide
            // prisms that catch you and dive to rebuild speed. Every ramp's riding line is
            // shifted sideways from the one before, so each flight needs an air strafe.
            var ramps = new List<RampPath>
            {
                LayRamp(Kind.Prism, Wide, -1f, new Vector3(0f, -1f, 2f), Vector3.forward,
                    new[] { (0f, 0f), (40f, -0.45f), (90f, -0.45f), (180f, 0f), (230f, 0.08f) }, null),
            };
            void Next(Kind kind, float width, float shift, (float, float)[] shape, (float, float)[] bend = null) =>
                ramps.Add(LandingRamp(ramps[^1], kind, width, shift, shape, bend));
            Next(Kind.Slab, SlabWidth, 14f, Blade, Bend);
            Next(Kind.Prism, Thin, -14f, Blade);
            Next(Kind.Slab, SlabWidth, -14f, Blade, Bend);
            Next(Kind.Prism, Wide, 18f, Dive);
            Next(Kind.Slab, SlabWidth, -14f, Dive, Bend);
            Next(Kind.Prism, Thin, 14f, Blade);
            Next(Kind.Slab, SlabWidth, 14f, Blade, Bend);

            BotRoute.Clear();
            for (int r = 0; r < ramps.Count; r++)
            {
                var ramp = ramps[r];
                if (ramp.kind == Kind.Prism) RidgeRamp($"Ramp{r + 1}", ramp, rampMat, map);
                else SlabRamp($"Ramp{r + 1}", ramp, slabMat, map);

                for (int i = 0; i < ramp.ridge.Count; i += 3)
                {
                    float toEnd = ramp.distance[^1] - ramp.distance[i];
                    Vector3 p = ramp.FacePoint(i, Mathf.Lerp(SwingFraction, RideFraction, toEnd / SwingLength));
                    BotRoute.Add(new Vector2(p.x, p.z));
                }
            }

            // Start ledge hangs just above the left face of ramp 1, with an orange lip at the
            // edge. Walk off the front and you drop a few metres onto the face.
            float ledgeWidth = Mathf.Min(6f, ramps[0].width * 0.66f), ledgeX = -1f - ledgeWidth * 0.5f;
            Box("StartPlatform", new Vector3(ledgeX, -0.5f, -10.5f), new Vector3(ledgeWidth, 1f, 19f), startMat, map);
            Box("StartEdge", new Vector3(ledgeX, -0.5f, -0.5f), new Vector3(ledgeWidth, 1f, 1f), edgeMat, map);

            // Finish: a big square landing under the flight off the last ramp, whichever way
            // it points
            var last = ramps[^1];
            var (_, flightY, launch) = Flight(last);
            const float finishSize = 120f;
            float finishTop = flightY(70f) - 1f;
            Vector3 finishCenter = (launch + last.EndForward * 80f).WithY(finishTop - 0.5f);
            Box("FinishPlatform", finishCenter, new Vector3(finishSize, 1f, finishSize), endMat, map);
            BotRoute.Add(new Vector2(finishCenter.x, finishCenter.z));

            // The hall: striped walls around everything, and a floor far below
            float xMin = -16f, xMax = 4f, zMin = -20f, zMax = 0f;
            foreach (var ramp in ramps)
            foreach (var p in ramp.ridge)
            {
                float reach = ramp.width + 2f;
                xMin = Mathf.Min(xMin, p.x - reach);
                xMax = Mathf.Max(xMax, p.x + reach);
                zMax = Mathf.Max(zMax, p.z + reach);
            }
            xMin = Mathf.Min(xMin, finishCenter.x - finishSize * 0.5f) - 12f;
            xMax = Mathf.Max(xMax, finishCenter.x + finishSize * 0.5f) + 12f;
            zMax = Mathf.Max(zMax, finishCenter.z + finishSize * 0.5f) + 12f;
            float yTop = 30f, yBottom = finishTop - 25f;
            float xMid = (xMin + xMax) * 0.5f, xLen = xMax - xMin;
            float zMid = (zMin + zMax) * 0.5f, zLen = zMax - zMin;
            float yMid = (yTop + yBottom) * 0.5f, yLen = yTop - yBottom;
            Box("WallLeft", new Vector3(xMin - 0.5f, yMid, zMid), new Vector3(1f, yLen, zLen + 2f), wallMat, map, stripes: true);
            Box("WallRight", new Vector3(xMax + 0.5f, yMid, zMid), new Vector3(1f, yLen, zLen + 2f), wallMat, map, stripes: true);
            Box("WallBack", new Vector3(xMid, yMid, zMin - 0.5f), new Vector3(xLen, yLen, 1f), wallMat, map, stripes: true);
            Box("WallEnd", new Vector3(xMid, yMid, zMax + 0.5f), new Vector3(xLen, yLen, 1f), wallMat, map, stripes: true);
            Box("Floor", new Vector3(xMid, yBottom - 0.5f, zMid), new Vector3(xLen, 1f, zLen), floorMat, map);

            BoxCollider startZone = Zone("StartZone", new Vector3(ledgeX, 2f, -10f), new Vector3(ledgeWidth, 4f, 20f));
            BoxCollider endZone = Zone("FinishZone", finishCenter + Vector3.up * 2.5f, new Vector3(finishSize, 4f, finishSize));

            var spawn = new GameObject("Spawn").transform;
            spawn.SetPositionAndRotation(new Vector3(ledgeX, 0.02f, -16f), Quaternion.identity);

            PlayerMovement player = MakePlayer(spawn);

            var timer = new GameObject("RunTimer").AddComponent<RunTimer>();
            timer.player = player;
            timer.startZone = startZone;
            timer.endZone = endZone;
            timer.spawnPoint = spawn;
            timer.killHeight = yBottom + 5f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("VoidFlow: graybox map built at " + ScenePath);
        }

        static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);

        // Lays a ramp starting at `start`, heading along `startForward`. Its slope (rise per
        // metre) is given at control points (distance, slope), and optionally its bend
        // (degrees turned so far, positive = right) at (distance, bend) points; both blend
        // linearly between points, so a steady change makes a smooth curve, and matching a
        // flight arc is exact.
        static RampPath LayRamp(Kind kind, float width, float side, Vector3 start, Vector3 startForward,
            (float s, float v)[] slopes, (float s, float v)[] bends)
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
                float h = (baseHeading + Interpolate(bends, d)) * Mathf.Deg2Rad;
                path.ridge.Add(new Vector3(flat.x, y, flat.z));
                path.forward.Add(new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h)));
                path.right.Add(new Vector3(Mathf.Cos(h), 0f, -Mathf.Sin(h)));
                path.distance.Add(d);

                float mid = d + ds * 0.5f;
                float hMid = (baseHeading + Interpolate(bends, mid)) * Mathf.Deg2Rad;
                flat += new Vector3(Mathf.Sin(hMid), 0f, Mathf.Cos(hMid)) * ds;
                y += Interpolate(slopes, mid) * ds;
            }
            return path;
        }

        static float Interpolate((float s, float v)[] points, float d)
        {
            if (points == null) return 0f;
            if (d <= points[0].s) return points[0].v;
            for (int i = 1; i < points.Length; i++)
                if (d <= points[i].s)
                    return Mathf.Lerp(points[i - 1].v, points[i].v, Mathf.InverseLerp(points[i - 1].s, points[i].s, d));
            return points[^1].v;
        }

        // The arc you fly after leaving the end of a ramp from its middle riding line: its
        // slope and height as functions of distance flown along the ramp's end direction,
        // plus the launch point. Launch speed is estimated from the height dropped since the
        // start ledge, up to about what a good run reaches.
        static (Func<float, float> slope, Func<float, float> height, Vector3 launch) Flight(RampPath from)
        {
            Vector3 launch = from.FacePoint(from.ridge.Count - 1, RideFraction);
            float speed = Mathf.Min(Mathf.Sqrt(2f * Gravity * Mathf.Max(-launch.y, 1f)) * 0.95f, MaxDesignSpeed);
            float k = from.EndSlope;
            float vf = speed / Mathf.Sqrt(1f + k * k), vy = vf * k;
            return (d => vy / vf - Gravity * d / (vf * vf),
                    d => launch.y + vy * d / vf - 0.5f * Gravity * (d / vf) * (d / vf),
                    launch);
        }

        // The next ramp: its riding line starts FlightGap metres on from where you launch,
        // `shift` metres to the side, as a landing hill that follows the flight arc. Then the
        // given shape, measured from the end of the landing hill. You ride the face you
        // arrive on: shifted right, you come in over its left face. Slabs bend toward their
        // face by the given degrees.
        static RampPath LandingRamp(RampPath from, Kind kind, float width, float shift,
            (float s, float v)[] shape, (float s, float v)[] bend)
        {
            var (arcSlope, arcY, launch) = Flight(from);
            float side = shift > 0f ? -1f : 1f;
            Vector3 fwd = from.EndForward;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            // Where the riding line sits relative to the ridge / top edge
            var probe = new RampPath { kind = kind, width = width, side = side };
            probe.ridge.Add(Vector3.zero); probe.right.Add(Vector3.right);
            Vector3 line = probe.FacePoint(0, RideFraction);

            float rise = (LandingClearStart - LandingClearEnd) / LandingLength;
            Vector3 lineStart = launch + fwd * FlightGap + right * shift;
            Vector3 start = (lineStart - right * (line.x)).WithY(arcY(FlightGap) - line.y - LandingClearStart);

            var slopes = new List<(float, float)>
            {
                (0f, arcSlope(FlightGap) + rise),
                (LandingLength, arcSlope(FlightGap + LandingLength) + rise),
            };
            foreach (var (d, slope) in shape) slopes.Add((LandingLength + d, slope));

            (float, float)[] bends = null;
            if (bend != null)
            {
                var list = new List<(float, float)> { (0f, 0f) };
                foreach (var (d, degrees) in bend) list.Add((LandingLength + d, degrees * side));
                bends = list.ToArray();
            }
            return LayRamp(kind, width, side, start, fwd, slopes.ToArray(), bends);
        }

        // Cross-section of one prism face: (distance out from the ridge, depth below it). The
        // face starts at TopAngle and curves steeper to BottomAngle.
        static List<Vector2> Profile(float width)
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

        static float FaceDepth(float width, float offset)
        {
            var profile = Profile(width);
            for (int k = 1; k < profile.Count; k++)
                if (offset <= profile[k].x)
                    return Mathf.Lerp(profile[k - 1].y, profile[k].y, Mathf.InverseLerp(profile[k - 1].x, profile[k].x, offset));
            return profile[^1].y;
        }

        // Collects triangles for a generated ramp mesh, flipping each one to face `outward`
        class RampMesh
        {
            readonly Vector3 origin;
            public readonly List<Vector3> verts = new();
            readonly List<Vector2> uvs = new();
            readonly List<int> tris = new();

            public RampMesh(Vector3 worldOrigin) => origin = worldOrigin;

            public int Add(Vector3 world, Vector2 uv)
            {
                verts.Add(world - origin);
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

            public void Spawn(string name, Material mat, Transform parent)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                SpawnMesh(name, origin, mesh, mat, parent, convex: false);
            }
        }

        // Builds a free-standing ridge ramp along a path: two curved faces meeting at a sharp
        // ridge, plus the underside and end caps. Uses a (non-convex) mesh collider so the
        // curved faces are exactly what you surf on.
        static void RidgeRamp(string name, RampPath path, Material mat, Transform parent)
        {
            var profile = Profile(path.width);
            var profileArc = new List<float> { 0f };
            for (int k = 1; k < profile.Count; k++)
                profileArc.Add(profileArc[^1] + Vector2.Distance(profile[k - 1], profile[k]));

            var mesh = new RampMesh(path.ridge[0]);
            int n = path.ridge.Count, kCount = profile.Count, bottom = kCount - 1;

            Vector3 Point(int i, float side, int k) =>
                path.ridge[i] + path.right[i] * (side * profile[k].x) + Vector3.down * profile[k].y;

            // The two surf faces. Vertices are shared within a face so it shades smoothly,
            // but not across the ridge, which stays sharp.
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

            // Underside
            for (int i = 0; i < n - 1; i++)
                mesh.FlatQuad(Point(i, -1f, bottom), Point(i + 1, -1f, bottom), Point(i + 1, 1f, bottom), Point(i, 1f, bottom),
                    Vector3.down, path.forward[i]);

            // End caps: the cross-section outline, fanned from its middle
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

            mesh.Spawn(name, mat, parent);
        }

        // Builds a slab ramp along a path: a flat panel hanging down from its top edge toward
        // the side it faces, tilted SlabBank degrees, SlabThickness thick. The surf face
        // shades smoothly along the bends.
        static void SlabRamp(string name, RampPath path, Material mat, Transform parent)
        {
            var mesh = new RampMesh(path.ridge[0]);
            int n = path.ridge.Count;
            float bank = SlabBank * Mathf.Deg2Rad;

            var top = new Vector3[n];
            var bottom = new Vector3[n];
            var normal = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 down = path.right[i] * (path.side * Mathf.Cos(bank)) + Vector3.down * Mathf.Sin(bank);
                top[i] = path.ridge[i];
                bottom[i] = top[i] + down * path.width;
                normal[i] = path.right[i] * (path.side * Mathf.Sin(bank)) + Vector3.up * Mathf.Cos(bank);
            }

            // Surf face: shared vertices along its length so the bends shade smoothly
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

            // Back, top edge, bottom edge
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

            // End caps
            foreach (int i in new[] { 0, n - 1 })
            {
                Vector3 outward = i == 0 ? -path.forward[i] : path.forward[i];
                Vector3 tb = top[i] - normal[i] * SlabThickness, bb = bottom[i] - normal[i] * SlabThickness;
                mesh.FlatQuad(top[i], bottom[i], bb, tb, outward, path.right[i]);
            }

            mesh.Spawn(name, mat, parent);
        }

        static void SetupLighting()
        {
            Color sky = new Color(0.62f, 0.76f, 0.92f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.8f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.6f, 0.65f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.33f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 800f;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        static PlayerMovement MakePlayer(Transform spawn)
        {
            var go = new GameObject("Player") { layer = 2 };
            go.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            // Roughly the Source player hull: 72u tall, 32u wide
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.4f;
            capsule.height = 1.83f;
            capsule.center = new Vector3(0f, 0.915f, 0f);

            var cam = new GameObject("Camera");
            cam.tag = "MainCamera";
            cam.transform.SetParent(go.transform, false);
            cam.transform.localPosition = new Vector3(0f, 1.63f, 0f); // 64u eye height
            var camera = cam.AddComponent<Camera>();
            camera.fieldOfView = 74f; // ~90 horizontal at 16:9, like CS
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            cam.AddComponent<AudioListener>();

            var movement = go.AddComponent<PlayerMovement>();
            movement.cameraPivot = cam.transform;
            return movement;
        }

        static void Box(string name, Vector3 center, Vector3 size, Material mat, Transform parent, bool stripes = false)
        {
            Vector3 e = size * 0.5f;
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
                c[i] = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);

            var mb = new MeshBuilder(center) { Stripes = stripes };
            mb.Face(c[0], c[1], c[3], c[2]); // front
            mb.Face(c[4], c[5], c[7], c[6]); // back
            mb.Face(c[0], c[2], c[6], c[4]); // left
            mb.Face(c[1], c[3], c[7], c[5]); // right
            mb.Face(c[2], c[3], c[7], c[6]); // top
            mb.Face(c[0], c[1], c[5], c[4]); // bottom
            SpawnMesh(name, center, mb.ToMesh(name), mat, parent, convex: true);
        }

        static void SpawnMesh(string name, Vector3 pos, Mesh mesh, Material mat, Transform parent, bool convex)
        {
            AssetDatabase.CreateAsset(mesh, $"{Root}/Meshes/{name}.asset");

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = convex;
        }

        static BoxCollider Zone(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            return box;
        }

        static Material MakeMaterial(string name, Color color, Texture2D texture)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, $"{Root}/{name}.mat");
            return mat;
        }

        static Texture2D MakeGridTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool major = x < 3 || y < 3 || x >= size - 3 || y >= size - 3;
                bool minor = Mathf.Abs(x - size / 2) < 1 || Mathf.Abs(y - size / 2) < 1;
                float v = major ? 0.55f : minor ? 0.8f : 0.95f;
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
            return SaveTexture(tex, Root + "/Grid.png");
        }

        // Utopia-style wall bands: pale grey with orange and blue stripes
        static Texture2D MakeStripeTexture()
        {
            var grey = new Color(0.86f, 0.85f, 0.88f);
            var orange = new Color(0.93f, 0.42f, 0.12f);
            var blue = new Color(0.25f, 0.45f, 0.72f);
            const int height = 256;
            var tex = new Texture2D(4, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                float f = (float)y / height;
                Color c = f is >= 0.70f and < 0.78f ? orange
                    : f is >= 0.80f and < 0.90f ? blue
                    : f is >= 0.93f and < 0.96f ? orange
                    : grey;
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
            }
            return SaveTexture(tex, Root + "/Stripes.png");
        }

        static Texture2D SaveTexture(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Flat-shaded convex solid with grid UVs in world metres, so every surface tiles the same.
        // With Stripes on, V follows world height instead, so wall stripes line up everywhere.
        class MeshBuilder
        {
            readonly Vector3 origin;
            readonly List<Vector3> verts = new();
            readonly List<Vector3> normals = new();
            readonly List<Vector2> uvs = new();
            readonly List<int> tris = new();
            public Vector3 SolidCenter = Vector3.zero;
            public bool Stripes;

            public MeshBuilder(Vector3 worldOrigin) => origin = worldOrigin;

            public void Face(params Vector3[] pts)
            {
                Vector3 centroid = Vector3.zero;
                foreach (var p in pts) centroid += p;
                centroid /= pts.Length;

                Vector3 n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]).normalized;
                if (Vector3.Dot(n, centroid - SolidCenter) < 0f)
                {
                    Array.Reverse(pts);
                    n = -n;
                }

                Vector3 u = (pts[1] - pts[0]).normalized;
                Vector3 v = Vector3.Cross(n, u);
                Vector3 across = Vector3.Cross(Vector3.up, n);
                bool stripeFace = Stripes && across.sqrMagnitude > 0.01f;
                across.Normalize();

                int start = verts.Count;
                foreach (var p in pts)
                {
                    Vector3 world = origin + p;
                    verts.Add(p);
                    normals.Add(n);
                    uvs.Add(stripeFace
                        ? new Vector2(Vector3.Dot(world, across) / UvMeters, world.y / StripeTileMeters)
                        : new Vector2(Vector3.Dot(world, u), Vector3.Dot(world, v)) / UvMeters);
                }
                for (int i = 1; i < pts.Length - 1; i++)
                {
                    tris.Add(start);
                    tris.Add(start + i);
                    tris.Add(start + i + 1);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
