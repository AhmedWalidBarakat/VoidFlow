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
    // Generates a small utopia-style surf test map: a hall with straight, free-standing
    // ridge ramps down the middle, so you can look forward the whole run. Each ramp dives,
    // levels out, then rises gently at the end. Swing up the face toward the peak there and
    // you launch off the end a long way to the next ramp, which starts as a landing hill
    // shaped to the flight arc, so you touch down smoothly and keep your speed.
    // Run from the menu (VoidFlow > Rebuild Graybox Map) after changing the layout below.
    public static class GrayboxBuilder
    {
        const string Root = "Assets/Graybox";
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";
        const float UvMeters = 2f;          // one grid tile = 2m
        const float StripeTileMeters = 16f; // wall stripe pattern repeats every 16m of height

        // Ramp cross-section, one side, from the ridge down. The face starts at 48 degrees and
        // curves steeper to 62 at the bottom. All of it is steeper than ~45.6 degrees
        // (normal.y < 0.7), so every part is surfable. Steeper faces turn more of the ramp's
        // push sideways when the ridge bends, which throws you off at speed.
        const float FaceWidth = 18f;
        const float TopAngle = 48f, BottomAngle = 62f;
        const int ProfileSteps = 8;
        const float PathStep = 2f; // ramp mesh resolution along its length

        // Flights between ramps. The next ramp starts FlightGap metres past the end of the last
        // and follows the arc for LandingLength metres: 6m below you at first, rising to meet
        // you near the end, so you land somewhere along it whether you launched from high or
        // low on the face, fast or slow.
        //
        // Rule for every bend in a ridge: at ~2300 u/s it has to be long and gradual (radius
        // around 150m+), or the push needed to bend your path mostly goes sideways into the
        // face and throws you off the ramp faster than strafing can hold you on.
        const float FlightGap = 35f;
        const float LandingLength = 40f;
        const float LandingClearStart = 6f, LandingClearEnd = 0f;
        const float Gravity = 800f * PlayerMovement.SourceUnit;

        // The line the bot swings around, which is also roughly where you'd ride. Over the
        // last SwingLength metres of a ramp the line moves up toward the peak for the launch.
        // Flights are designed for a launch from the middle line, the lowest likely one.
        const float RideOffset = FaceWidth * 0.45f;
        const float SwingOffset = FaceWidth * 0.15f;
        const float SwingLength = 50f;

        // Where the bot aims, as (x, z) in order: a line along the left face of every ramp,
        // then the finish. Filled in by Build() from the layout so the bot always matches.
        public static readonly List<Vector2> BotRoute = new();

        class RampPath
        {
            public readonly List<Vector3> ridge = new();   // world position of the ridge line
            public readonly List<Vector3> right = new();   // horizontal right vector at each point
            public readonly List<float> distance = new();  // metres along the ramp
            public Vector3 End => ridge[^1];
            public float EndSlope => (ridge[^1].y - ridge[^2].y) / (ridge[^1].z - ridge[^2].z);
        }

        [MenuItem("VoidFlow/Rebuild Graybox Map")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Root + "/Meshes");
            Texture2D grid = MakeGridTexture();
            Texture2D stripes = MakeStripeTexture();
            Material rampMat = MakeMaterial("Ramp", new Color(0.9f, 0.89f, 0.93f), grid);
            Material wallMat = MakeMaterial("Wall", Color.white, stripes);
            Material floorMat = MakeMaterial("Floor", new Color(0.35f, 0.38f, 0.45f), grid);
            Material startMat = MakeMaterial("Start", new Color(0.75f, 0.75f, 0.78f), grid);
            Material edgeMat = MakeMaterial("Edge", new Color(0.93f, 0.42f, 0.12f), grid);
            Material endMat = MakeMaterial("Finish", new Color(0.95f, 0.75f, 0.25f), grid);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            var map = new GameObject("Map").transform;

            // Ramp 1, the big drop: starts level just below the ledge, dives at ~24 degrees,
            // levels out over 90m, then rises gently at the end for the launch
            var ramp1 = LayRamp(new Vector3(0f, -1f, 2f), (0f, 0f), (40f, -0.45f), (90f, -0.45f), (180f, 0f), (230f, 0.08f));
            // Ramps 2 and 3: land on the arc, level out over 120m, rise gently again
            var ramp2 = LandingRamp(ramp1, (120f, 0f), (170f, 0.08f));
            var ramp3 = LandingRamp(ramp2, (120f, 0f), (160f, 0.06f));
            var ramps = new[] { ramp1, ramp2, ramp3 };

            BotRoute.Clear();
            for (int r = 0; r < ramps.Length; r++)
            {
                RidgeRamp($"Ramp{r + 1}", ramps[r], rampMat, map);
                var ridge = ramps[r].ridge;
                for (int i = 0; i < ridge.Count; i += 3)
                {
                    float toEnd = ridge[^1].z - ridge[i].z;
                    float offset = Mathf.Lerp(SwingOffset, RideOffset, toEnd / SwingLength);
                    Vector3 p = ridge[i] - ramps[r].right[i] * offset;
                    BotRoute.Add(new Vector2(p.x, p.z));
                }
            }

            // Start ledge hangs just above the left face of ramp 1, with an orange lip at the
            // edge. Walk off the front and you drop a few metres onto the face.
            Box("StartPlatform", new Vector3(-5f, -0.5f, -10.5f), new Vector3(6f, 1f, 19f), startMat, map);
            Box("StartEdge", new Vector3(-5f, -0.5f, -0.5f), new Vector3(6f, 1f, 1f), edgeMat, map);

            // Finish platform: a long flat landing under the flight off ramp 3
            Vector3 end = ramp3.End;
            var (_, flightY) = Flight(ramp3);
            float finishTop = flightY(70f) - 1f;
            const float finishLength = 180f;
            var finishCenter = new Vector3(end.x, finishTop - 0.5f, end.z + 30f + finishLength * 0.5f);
            Box("FinishPlatform", finishCenter, new Vector3(60f, 1f, finishLength), endMat, map);
            BotRoute.Add(new Vector2(finishCenter.x, finishCenter.z));

            // The hall: striped walls around everything, and a floor far below
            float xMin = -16f, xMax = 4f, zMin = -20f, zMax = finishCenter.z + finishLength * 0.5f;
            foreach (var ramp in ramps)
            foreach (var p in ramp.ridge)
            {
                xMin = Mathf.Min(xMin, p.x - FaceWidth);
                xMax = Mathf.Max(xMax, p.x + FaceWidth);
            }
            xMin = Mathf.Min(xMin, finishCenter.x - 30f) - 12f;
            xMax = Mathf.Max(xMax, finishCenter.x + 30f) + 12f;
            float yTop = 30f, yBottom = finishTop - 25f;
            float xMid = (xMin + xMax) * 0.5f, xLen = xMax - xMin;
            float zMid = (zMin + zMax) * 0.5f, zLen = zMax - zMin;
            float yMid = (yTop + yBottom) * 0.5f, yLen = yTop - yBottom;
            Box("WallLeft", new Vector3(xMin - 0.5f, yMid, zMid), new Vector3(1f, yLen, zLen + 2f), wallMat, map, stripes: true);
            Box("WallRight", new Vector3(xMax + 0.5f, yMid, zMid), new Vector3(1f, yLen, zLen + 2f), wallMat, map, stripes: true);
            Box("WallBack", new Vector3(xMid, yMid, zMin - 0.5f), new Vector3(xLen, yLen, 1f), wallMat, map, stripes: true);
            Box("WallEnd", new Vector3(xMid, yMid, zMax + 0.5f), new Vector3(xLen, yLen, 1f), wallMat, map, stripes: true);
            Box("Floor", new Vector3(xMid, yBottom - 0.5f, zMid), new Vector3(xLen, 1f, zLen), floorMat, map);

            BoxCollider startZone = Zone("StartZone", new Vector3(-5f, 2f, -10f), new Vector3(6f, 4f, 20f));
            BoxCollider endZone = Zone("FinishZone", finishCenter + Vector3.up * 2.5f, new Vector3(60f, 4f, finishLength));

            var spawn = new GameObject("Spawn").transform;
            spawn.SetPositionAndRotation(new Vector3(-5f, 0.02f, -16f), Quaternion.identity);

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

        // Lays a straight ramp along +Z. The ridge's slope (rise per metre) is given at control
        // points (distance, slope) and blends linearly between them, so a steady change in
        // slope makes a smooth curve, and matching a flight arc is exact.
        static RampPath LayRamp(Vector3 start, params (float s, float slope)[] slopes)
        {
            var path = new RampPath();
            float length = slopes[^1].s;
            int steps = Mathf.CeilToInt(length / PathStep);
            float ds = length / steps;
            float y = start.y;
            for (int i = 0; i <= steps; i++)
            {
                float d = i * ds;
                path.ridge.Add(new Vector3(start.x, y, start.z + d));
                path.right.Add(Vector3.right);
                path.distance.Add(d);
                y += SlopeAt(slopes, d + ds * 0.5f) * ds;
            }
            return path;
        }

        static float SlopeAt((float s, float slope)[] slopes, float d)
        {
            for (int i = 1; i < slopes.Length; i++)
                if (d <= slopes[i].s)
                    return Mathf.Lerp(slopes[i - 1].slope, slopes[i].slope, Mathf.InverseLerp(slopes[i - 1].s, slopes[i].s, d));
            return slopes[^1].slope;
        }

        // The arc you fly after leaving the end of a ramp: its slope and height as
        // functions of forward distance past the end. Launch speed is estimated from the
        // height dropped since the start ledge.
        static (Func<float, float> slope, Func<float, float> height) Flight(RampPath from)
        {
            float launchY = from.End.y - FaceDepth(RideOffset);
            float speed = Mathf.Sqrt(2f * Gravity * Mathf.Max(-launchY, 1f)) * 0.95f;
            float k = from.EndSlope;
            float vz = speed / Mathf.Sqrt(1f + k * k), vy = vz * k;
            return (d => vy / vz - Gravity * d / (vz * vz),
                    d => launchY + vy * d / vz - 0.5f * Gravity * (d / vz) * (d / vz));
        }

        // The next ramp: a landing hill that follows the flight arc off `from`, then the
        // given (distance, slope) points after it, measured from the end of the landing hill.
        static RampPath LandingRamp(RampPath from, params (float s, float slope)[] after)
        {
            var (arcSlope, arcY) = Flight(from);
            float rise = (LandingClearStart - LandingClearEnd) / LandingLength;
            var start = new Vector3(from.End.x, arcY(FlightGap) + FaceDepth(RideOffset) - LandingClearStart, from.End.z + FlightGap);

            var slopes = new List<(float, float)>
            {
                (0f, arcSlope(FlightGap) + rise),
                (LandingLength, arcSlope(FlightGap + LandingLength) + rise),
            };
            foreach (var (d, slope) in after) slopes.Add((LandingLength + d, slope));
            return LayRamp(start, slopes.ToArray());
        }

        // Cross-section of one face: (distance out from the ridge, depth below it). The face
        // starts at TopAngle and curves steeper to BottomAngle.
        static List<Vector2> Profile()
        {
            var profile = new List<Vector2> { Vector2.zero };
            float step = FaceWidth / ProfileSteps;
            for (int k = 0; k < ProfileSteps; k++)
            {
                float angle = Mathf.Lerp(TopAngle, BottomAngle, (k + 0.5f) / ProfileSteps) * Mathf.Deg2Rad;
                profile.Add(profile[^1] + new Vector2(step, step * Mathf.Tan(angle)));
            }
            return profile;
        }

        static float FaceDepth(float offset)
        {
            var profile = Profile();
            for (int k = 1; k < profile.Count; k++)
                if (offset <= profile[k].x)
                    return Mathf.Lerp(profile[k - 1].y, profile[k].y, Mathf.InverseLerp(profile[k - 1].x, profile[k].x, offset));
            return profile[^1].y;
        }

        // Builds a free-standing ridge ramp along a path: two curved faces meeting at a sharp
        // ridge, plus the underside and end caps. Uses a (non-convex) mesh collider so the
        // curved faces are exactly what you surf on.
        static void RidgeRamp(string name, RampPath path, Material mat, Transform parent)
        {
            var profile = Profile();
            var profileArc = new List<float> { 0f };
            for (int k = 1; k < profile.Count; k++)
                profileArc.Add(profileArc[^1] + Vector2.Distance(profile[k - 1], profile[k]));

            Vector3 origin = path.ridge[0];
            int n = path.ridge.Count, kCount = profile.Count;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            Vector3 Point(int i, float side, int k) =>
                path.ridge[i] + path.right[i] * (side * profile[k].x) + Vector3.down * profile[k].y;

            void Tri(int a, int b, int c, Vector3 outward)
            {
                Vector3 normal = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (Vector3.Dot(normal, outward) < 0f) (b, c) = (c, b);
                tris.Add(a); tris.Add(b); tris.Add(c);
            }

            int Add(Vector3 world, Vector2 uv)
            {
                verts.Add(world - origin);
                uvs.Add(uv);
                return verts.Count - 1;
            }

            // The two surf faces. Vertices are shared within a face so it shades smoothly,
            // but not across the ridge, which stays sharp.
            foreach (float side in new[] { -1f, 1f })
            {
                int first = verts.Count;
                for (int i = 0; i < n; i++)
                for (int k = 0; k < kCount; k++)
                    Add(Point(i, side, k), new Vector2(path.distance[i], profileArc[k]) / UvMeters);

                for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < kCount - 1; k++)
                {
                    int a = first + i * kCount + k, b = a + kCount, c = b + 1, d = a + 1;
                    Vector3 outward = path.right[i] * side + Vector3.up;
                    Tri(a, b, c, outward);
                    Tri(a, c, d, outward);
                }
            }

            // Underside
            int bottom = kCount - 1;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 l0 = Point(i, -1f, bottom), l1 = Point(i + 1, -1f, bottom);
                Vector3 r0 = Point(i, 1f, bottom), r1 = Point(i + 1, 1f, bottom);
                int a = Add(l0, new Vector2(l0.x, l0.z) / UvMeters), b = Add(l1, new Vector2(l1.x, l1.z) / UvMeters);
                int c = Add(r1, new Vector2(r1.x, r1.z) / UvMeters), d = Add(r0, new Vector2(r0.x, r0.z) / UvMeters);
                Tri(a, b, c, Vector3.down);
                Tri(a, c, d, Vector3.down);
            }

            // End caps: the cross-section outline, fanned from its middle
            foreach (int i in new[] { 0, n - 1 })
            {
                Vector3 forward = i == 0 ? -(path.ridge[1] - path.ridge[0]).WithY(0f) : (path.ridge[i] - path.ridge[i - 1]).WithY(0f);
                var outline = new List<Vector3>();
                for (int k = bottom; k >= 0; k--) outline.Add(Point(i, -1f, k));
                for (int k = 1; k <= bottom; k++) outline.Add(Point(i, 1f, k));

                Vector3 middle = Vector3.zero;
                foreach (var p in outline) middle += p;
                middle /= outline.Count;
                Vector3 right = path.right[i];
                Vector2 CapUv(Vector3 p) => new Vector2(Vector3.Dot(p, right), p.y) / UvMeters;

                int center = Add(middle, CapUv(middle));
                var ring = new List<int>();
                foreach (var p in outline) ring.Add(Add(p, CapUv(p)));
                for (int j = 0; j < ring.Count; j++)
                    Tri(center, ring[j], ring[(j + 1) % ring.Count], forward);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            SpawnMesh(name, origin, mesh, mat, parent, convex: false);
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
