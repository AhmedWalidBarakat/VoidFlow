using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow.EditorTools
{
    // Generates the graybox surf test map: start platform, three surf ramps, finish platform.
    // Run from the menu (VoidFlow > Rebuild Graybox Map) after changing the layout below.
    public static class GrayboxBuilder
    {
        const string Root = "Assets/Graybox";
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";
        const float UvMeters = 2f; // one grid tile = 2m

        // Ramp cross-section: 12m tall, 8m half-width, about 56 degrees. Anything
        // steeper than ~45.6 degrees (normal.y < 0.7) is surfable.
        const float RampHeight = 12f;
        const float RampHalfWidth = 8f;

        [MenuItem("VoidFlow/Rebuild Graybox Map")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Root + "/Meshes");
            Texture2D grid = MakeGridTexture();
            Material platformMat = MakeMaterial("Platform", new Color(0.55f, 0.56f, 0.6f), grid);
            Material rampMat = MakeMaterial("Ramp", new Color(0.25f, 0.6f, 0.78f), grid);
            Material startMat = MakeMaterial("Start", new Color(0.3f, 0.75f, 0.42f), grid);
            Material endMat = MakeMaterial("Finish", new Color(0.95f, 0.75f, 0.25f), grid);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();

            var map = new GameObject("Map").transform;

            // Start platform hangs over the left face of ramp 1: walk off the front edge to drop in
            Box("StartPlatform", new Vector3(-5f, -0.5f, -4f), new Vector3(8f, 1f, 14f), startMat, map);
            Ramp("Ramp1", new Vector3(0f, -3f, -2f), 122f, rampMat, map);
            Ramp("Ramp2", new Vector3(0f, -16f, 124f), 126f, rampMat, map);
            Ramp("Ramp3", new Vector3(0f, -29f, 254f), 120f, rampMat, map);
            Box("FinishPlatform", new Vector3(0f, -45.5f, 404f), new Vector3(30f, 1f, 52f), endMat, map);
            Box("FinishBackWall", new Vector3(0f, -39f, 431f), new Vector3(30f, 14f, 2f), platformMat, map);

            BoxCollider startZone = Zone("StartZone", new Vector3(-5f, 1.5f, -4f), new Vector3(8f, 3f, 14f));
            BoxCollider endZone = Zone("FinishZone", new Vector3(0f, -43f, 404f), new Vector3(30f, 4f, 52f));

            var spawn = new GameObject("Spawn").transform;
            spawn.SetPositionAndRotation(new Vector3(-5f, 0.02f, -9f), Quaternion.identity);

            PlayerMovement player = MakePlayer(spawn);

            var timer = new GameObject("RunTimer").AddComponent<RunTimer>();
            timer.player = player;
            timer.startZone = startZone;
            timer.endZone = endZone;
            timer.spawnPoint = spawn;

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
            var go = new GameObject("Player");
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

        // Triangular prism with its ridge at `ridge`, running `length` metres along +Z
        static void Ramp(string name, Vector3 ridge, float length, Material mat, Transform parent)
        {
            float h = RampHeight, w = RampHalfWidth;
            Vector3 l0 = new(-w, -h, 0f), r0 = new(w, -h, 0f), t0 = Vector3.zero;
            Vector3 l1 = new(-w, -h, length), r1 = new(w, -h, length), t1 = new(0f, 0f, length);

            var mb = new MeshBuilder(ridge) { SolidCenter = new Vector3(0f, -h / 3f, length * 0.5f) };
            mb.Face(l0, t0, t1, l1);
            mb.Face(t0, r0, r1, t1);
            mb.Face(l0, r0, r1, l1);
            mb.Face(l0, r0, t0);
            mb.Face(l1, r1, t1);
            Spawn(name, ridge, mb, mat, parent);
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
