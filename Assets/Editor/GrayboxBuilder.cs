using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow.EditorTools
{
    // Generates a small utopia-style surf test map: an enclosed corridor with surf ramps
    // on alternating walls, a high start platform to drop in from, and a finish platform.
    // Everything is sized in Source units so the ramps match CS movement.
    // Run from the menu (VoidFlow > Rebuild Graybox Map) after changing the layout below.
    public static class GrayboxBuilder
    {
        const string Root = "Assets/Graybox";
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";
        const float UvMeters = 2f; // one grid tile = 2m

        const float U = PlayerMovement.SourceUnit;

        // Wall ramp cross-section: 1024u tall, 704u out from the wall, about 55 degrees.
        // Anything steeper than ~45.6 degrees (normal.y < 0.7) is surfable. Tall faces give
        // room to soak up the speed from landing on a ramp, like utopia's ramps.
        const float RampHeight = 1024f * U;
        const float RampDepth = 704f * U;
        const float RampLength = 4096f * U;
        const float RampDecline = 256f * U;   // gentle downhill along each ramp
        const float CorridorHalfWidth = 640f * U; // ramps reach past the middle; stages are far enough apart that they never touch
        const float TransferOverlap = 1024f * U; // next ramp starts this far before the previous ends
        // A transfer at ~900 u/s crosses the corridor sideways at ~15 m/s and falls ~1000u on the
        // way, so the next ramp starts that far below the end of the previous one
        const float StageDrop = 1024f * U;

        // Where the bot aims, as (x, z) in order: the middle of each ramp, a dip low on it for
        // speed before the transfer, then the finish.
        // Filled in by Build() from the layout so the bot always matches the map.
        public static readonly List<Vector2> BotRoute = new();

        [MenuItem("VoidFlow/Rebuild Graybox Map")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Root + "/Meshes");
            Texture2D grid = MakeGridTexture();
            Material wallMat = MakeMaterial("Wall", new Color(0.82f, 0.82f, 0.85f), grid);
            Material rampMat = MakeMaterial("Ramp", new Color(0.95f, 0.5f, 0.2f), grid);
            Material floorMat = MakeMaterial("Floor", new Color(0.2f, 0.21f, 0.25f), grid);
            Material startMat = MakeMaterial("Start", new Color(0.3f, 0.75f, 0.42f), grid);
            Material endMat = MakeMaterial("Finish", new Color(0.95f, 0.75f, 0.25f), grid);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();

            var map = new GameObject("Map").transform;

            // Three ramps on alternating walls: left, right, left. Each one starts before the
            // previous ends and a stage lower, so you transfer across the corridor to it.
            BotRoute.Clear();
            float z = 0f, top = 0f;
            float midX = CorridorHalfWidth - RampDepth * 0.5f;
            float lowX = CorridorHalfWidth - RampDepth * 0.8f;
            for (int i = 0; i < 3; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                WallRamp($"Ramp{i + 1}", side, z, top, rampMat, map);
                BotRoute.Add(new Vector2(side * midX, z + RampLength * 0.6f));
                BotRoute.Add(new Vector2(side * lowX, z + RampLength - TransferOverlap));
                z += RampLength - TransferOverlap;
                top -= RampDecline + StageDrop;
            }
            float rampsEnd = z + TransferOverlap;
            float lastTop = top + StageDrop;

            // Start ledge juts 3m out from the left wall, just above the top of ramp 1: walk off
            // the front edge and you land high on the face with room to start surfing
            float wall = CorridorHalfWidth;
            Box("StartPlatform", new Vector3(-wall + 1.5f, 1.5f, -8f), new Vector3(3f, 1f, 12f), startMat, map);

            float finishTop = lastTop - RampHeight - 8f;
            float finishLength = 80f;
            Vector3 finishCenter = new(0f, finishTop - 0.5f, rampsEnd + 2f + finishLength * 0.5f);
            Box("FinishPlatform", finishCenter, new Vector3(wall * 2f, 1f, finishLength), endMat, map);
            BotRoute.Add(new Vector2(0f, finishCenter.z));

            // Corridor shell: side walls, a back wall behind the start, an end wall, and a floor
            float zMin = -20f, zMax = finishCenter.z + finishLength * 0.5f + 1f;
            float yTop = 25f, yBottom = finishTop - 30f;
            float zMid = (zMin + zMax) * 0.5f, zLen = zMax - zMin, yMid = (yTop + yBottom) * 0.5f, yLen = yTop - yBottom;
            Box("WallLeft", new Vector3(-wall - 0.5f, yMid, zMid), new Vector3(1f, yLen, zLen), wallMat, map);
            Box("WallRight", new Vector3(wall + 0.5f, yMid, zMid), new Vector3(1f, yLen, zLen), wallMat, map);
            Box("WallBack", new Vector3(0f, yMid, zMin - 0.5f), new Vector3(wall * 2f, yLen, 1f), wallMat, map);
            Box("WallEnd", new Vector3(0f, yMid, zMax + 0.5f), new Vector3(wall * 2f, yLen, 1f), wallMat, map);
            Box("Floor", new Vector3(0f, yBottom - 0.5f, zMid), new Vector3(wall * 2f, 1f, zLen), floorMat, map);

            BoxCollider startZone = Zone("StartZone", new Vector3(-wall + 1.5f, 4f, -8f), new Vector3(3f, 4f, 12f));
            BoxCollider endZone = Zone("FinishZone", finishCenter + Vector3.up * 2.5f, new Vector3(wall * 2f, 4f, finishLength));

            var spawn = new GameObject("Spawn").transform;
            spawn.SetPositionAndRotation(new Vector3(-wall + 1.5f, 2.02f, -12f), Quaternion.identity);

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

        static void SetupLighting()
        {
            Color voidColor = new Color(0.05f, 0.06f, 0.1f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.36f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.15f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = voidColor;
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 450f;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
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

        static void Box(string name, Vector3 center, Vector3 size, Material mat, Transform parent)
        {
            Vector3 e = size * 0.5f;
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
                c[i] = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);

            var mb = new MeshBuilder(center);
            mb.Face(c[0], c[1], c[3], c[2]); // front
            mb.Face(c[4], c[5], c[7], c[6]); // back
            mb.Face(c[0], c[2], c[6], c[4]); // left
            mb.Face(c[1], c[3], c[7], c[5]); // right
            mb.Face(c[2], c[3], c[7], c[6]); // top
            mb.Face(c[0], c[1], c[5], c[4]); // bottom
            Spawn(name, center, mb, mat, parent);
        }

        // Surf ramp attached to a side wall (side -1 = left, +1 = right). Its top edge runs
        // along the wall starting at (z, top); the face slopes down and out into the corridor.
        static void WallRamp(string name, float side, float z, float top, Material mat, Transform parent)
        {
            float h = RampHeight, d = RampDepth, len = RampLength, drop = RampDecline;
            float inward = -side * d;
            Vector3 t0 = Vector3.zero, b0 = new(0f, -h, 0f), i0 = new(inward, -h, 0f);
            Vector3 t1 = new(0f, -drop, len), b1 = new(0f, -h - drop, len), i1 = new(inward, -h - drop, len);

            var origin = new Vector3(side * CorridorHalfWidth, top, z);
            var mb = new MeshBuilder(origin) { SolidCenter = (t0 + b0 + i0 + t1 + b1 + i1) / 6f };
            mb.Face(t0, i0, i1, t1); // the surf face
            mb.Face(t0, b0, b1, t1); // against the wall
            mb.Face(b0, i0, i1, b1); // underside
            mb.Face(t0, b0, i0);
            mb.Face(t1, b1, i1);
            Spawn(name, origin, mb, mat, parent);
        }

        static void Spawn(string name, Vector3 pos, MeshBuilder mb, Material mat, Transform parent)
        {
            Mesh mesh = mb.ToMesh(name);
            string path = $"{Root}/Meshes/{name}.asset";
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
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

        static Material MakeMaterial(string name, Color color, Texture2D grid)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", grid);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, $"{Root}/{name}.mat");
            return mat;
        }

        static Texture2D MakeGridTexture()
        {
            string path = Root + "/Grid.png";
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

        // Flat-shaded convex solid with grid UVs in world metres, so every surface tiles the same
        class MeshBuilder
        {
            readonly Vector3 origin;
            readonly List<Vector3> verts = new();
            readonly List<Vector3> normals = new();
            readonly List<Vector2> uvs = new();
            readonly List<int> tris = new();
            public Vector3 SolidCenter = Vector3.zero;

            public MeshBuilder(Vector3 worldOrigin) => origin = worldOrigin;

            public void Face(params Vector3[] pts)
            {
                Vector3 centroid = Vector3.zero;
                foreach (var p in pts) centroid += p;
                centroid /= pts.Length;

                Vector3 n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]).normalized;
                if (Vector3.Dot(n, centroid - SolidCenter) < 0f)
                {
                    System.Array.Reverse(pts);
                    n = -n;
                }

                Vector3 u = (pts[1] - pts[0]).normalized;
                Vector3 v = Vector3.Cross(n, u);
                int start = verts.Count;
                foreach (var p in pts)
                {
                    Vector3 world = origin + p;
                    verts.Add(p);
                    normals.Add(n);
                    uvs.Add(new Vector2(Vector3.Dot(world, u), Vector3.Dot(world, v)) / UvMeters);
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
