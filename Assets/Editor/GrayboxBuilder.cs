using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace VoidFlow.EditorTools
{
    // Builds the game's one scene: the start hall, the player, the HUD, and the endless
    // course that generates itself ahead of you as you play (see EndlessCourse).
    // Run from the menu (VoidFlow > Rebuild Scene).
    public static partial class GrayboxBuilder
    {
        const string Root = "Assets/Graybox";
        public const string ScenePath = "Assets/Scenes/VoidFlow.unity";
        const float UvMeters = RampShapes.UvMeters;
        const float StripeTileMeters = 16f; // wall stripe pattern repeats every 16m of height

        [MenuItem("VoidFlow/Rebuild Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Root + "/Meshes");
            Texture2D grid = MakeGridTexture();
            Texture2D stripes = MakeStripeTexture();
            Texture2D hazard = MakeHazardTexture();
            Texture2D bricks = MakeBrickTexture();
            tiles = MakeTilesTexture();
            stone = MakeStoneTexture();
            metal = MakeMetalTexture();
            wood = MakeWoodTexture();
            ice = MakeIceTexture();
            hex = MakeHexTexture();
            panel = MakeHallPanelTexture();
            plaster = MakePlasterTexture();
            Material rampMat = MakeMaterial("Ramp", new Color(0.9f, 0.89f, 0.93f), grid);
            var sky = new Material(Shader.Find("VoidFlow/GradientSky"));
            AssetDatabase.CreateAsset(sky, $"{Root}/Sky.mat");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Light sun = MakeSun();
            RenderSettings.skybox = sky;
            var map = new GameObject("World").transform;

            // The start hall sits on the ledge above the first ramp. Its middle lane lines up
            // over that ramp's left face, so walking straight off the drop edge lands you on it.
            const float lane = -4f;
            var (startZone, spawn, hall) = BuildStartHall(map, lane, grid, rampMat);
            AddGlowVolume(map);

            PlayerMovement player = MakePlayer(spawn, rampMat);

            var course = map.gameObject.AddComponent<EndlessCourse>();
            course.player = player;
            course.startHall = hall;
            course.sun = sun;
            course.view = player.cameraPivot.GetComponent<Camera>();
            course.kits = MakeBiomeKits(rampMat, grid, stripes, hazard, bricks);
            course.skybox = sky;

            var timer = new GameObject("RunTimer").AddComponent<RunTimer>();
            timer.player = player;
            timer.course = course;
            timer.startZone = startZone;
            timer.spawnPoint = spawn;


            // The course's rewards (Void Shards, speed rings, stage times) and the sounds of
            // your own movement
            course.shardMaterial = MakeGlow("GlowShard", new Color(1f, 0.4f, 0.9f), 2.6f);
            var rewards = new GameObject("CourseRewards").AddComponent<CourseRewards>();
            rewards.course = course;
            rewards.player = player;
            rewards.timer = timer;
            player.gameObject.AddComponent<PlayerFeedback>().course = course;

            // Particle effects (CC0 sprites from Kenney's Particle Pack, Assets/Fx)
            var fx = new GameObject("Fx").AddComponent<FxLibrary>();
            fx.spark = FxMaterial("Spark", "circle_05", true, Color.white);
            fx.flare = FxMaterial("Flare", "flare_01", true, Color.white);
            fx.star = FxMaterial("Star", "star_04", true, Color.white);
            fx.smoke = FxMaterial("Smoke", "smoke_07", false, Color.white);
            fx.dirt = FxMaterial("Dirt", "dirt_01", false, Color.white);
            fx.magic = FxMaterial("Magic", "magic_04", true, Color.white);
            fx.mote = FxMaterial("Mote", "circle_05", true, Color.white);
            fx.ring = FxMaterial("Ring", "light_02", true, Color.white);
            fx.streak = FxMaterial("Streak", "trace_02", true, Color.white);
            fx.muzzle = FxMaterial("Muzzle", "muzzle_01", true, Color.white);
            fx.scorch = FxMaterial("Scorch", "scorch_01", false, Color.white);
            fx.player = player;
            fx.course = course;
            fx.timer = timer;
            player.cameraPivot.GetComponent<ViewModel>().fx = fx;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("VoidFlow: scene built at " + ScenePath);
        }

        static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);

        // A URP particle material around one of the Fx sprites: additive for anything that
        // glows, alpha-blended for smoke, dirt and scorch marks
        static Material FxMaterial(string name, string texture, bool additive, Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Fx/{texture}.png"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, $"{Root}/Fx_{name}.mat");
            return m;
        }

        // Painted textures, made at the start of Build and used by the hall and biomes
        static Texture2D tiles, stone, metal, wood, ice, hex, panel, plaster;

        // CC0 photo textures from ambientCG (Assets/Textures/CC0, see its LICENSE.txt)
        static Texture2D CC0(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/CC0/{name}.jpg");

        // Every biome's materials, saved as assets under Graybox/Biomes
        static BiomeKit[] MakeBiomeKits(Material template, Texture2D grid, Texture2D stripes, Texture2D hazard, Texture2D bricks)
        {
            Directory.CreateDirectory(Root + "/Biomes");
            Texture2D Tex(Surface s) => s switch
            {
                Surface.Stripes => stripes,
                Surface.Hazard => hazard,
                Surface.Bricks => bricks,
                Surface.Tiles => tiles,
                Surface.Stone => stone,
                Surface.Metal => metal,
                Surface.Wood => wood,
                Surface.Ice => ice,
                Surface.Hex => hex,
                Surface.Panel => panel,
                Surface.Plaster => CC0("Plaster001"),
                Surface.Concrete => CC0("Concrete034"),
                Surface.HexTile => CC0("Tiles072"),
                Surface.WhiteTile => CC0("Tiles107"),
                Surface.DarkStone => CC0("Bricks034"),
                Surface.Plates => CC0("MetalPlates006"),
                Surface.Rock => CC0("Rock051"),
                _ => grid,
            };

            var kits = new BiomeKit[Biome.All.Length];
            for (int i = 0; i < kits.Length; i++)
            {
                Biome b = Biome.All[i];
                string name = b.name.Replace(" ", "");
                Material Save(Material m, string part)
                {
                    AssetDatabase.CreateAsset(m, $"{Root}/Biomes/{name}_{part}.mat");
                    return m;
                }
                kits[i] = new BiomeKit
                {
                    ramp = Save(BiomeKit.Surface(template, b.ramp, Tex(b.rampSurface), Vector2.one), "Ramp"),
                    slab = Save(BiomeKit.Surface(template, b.slab, Tex(b.rampSurface), Vector2.one), "Slab"),
                    scenery = Save(BiomeKit.Surface(template, b.scenery, Tex(b.scenerySurface), Vector2.one), "Scenery"),
                    glow = Save(BiomeKit.Glow(template, b.glow), "Glow"),
                    glowAlt = Save(BiomeKit.Glow(template, b.glowAlt), "GlowAlt"),
                    floor = Save(BiomeKit.Surface(template, b.floor.a > 0f ? b.floor : b.slab, Tex(b.floor.a > 0f ? b.floorSurface : b.rampSurface), Vector2.one), "Floor"),
                };
            }
            return kits;
        }

        static Light MakeSun()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.skybox = null;
            return sun;
        }

        static PlayerMovement MakePlayer(Transform spawn, Material viewModelTemplate)
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
            camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(120f, 16f / 9f); // 120 horizontal (the ViewModel keeps it right for any screen)
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cam.AddComponent<AudioListener>();

            var movement = go.AddComponent<PlayerMovement>();
            movement.cameraPivot = cam.transform;

            // The gloved hand and knife, drawn by its own overlay camera
            var viewModel = cam.AddComponent<ViewModel>();
            viewModel.player = movement;
            viewModel.template = viewModelTemplate;
            var fade = ViewModel.MakeTransparent(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            AssetDatabase.CreateAsset(fade, $"{Root}/ArmFade.mat");
            viewModel.fadeTemplate = fade;
            // Flames, glows and the case reveal are see-through AND glowing; builds strip that
            // shader variant unless a saved material uses it, and they'd draw as solid squares
            var glowFade = ViewModel.MakeTransparent(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            glowFade.EnableKeyword("_EMISSION");
            glowFade.SetColor("_EmissionColor", Color.white);
            glowFade.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(glowFade, $"{Root}/GlowFade.mat");
            viewModel.keepVariants = new[] { glowFade };

            // The inventory (I): loadout, unboxed skins and Void Cases, for this session
            cam.AddComponent<Inventory>().viewModel = viewModel;
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

        static BoxCollider Zone(string name, Vector3 center, Vector3 size, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
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
            mat.enableInstancing = true;
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

        // Utopia-style bands: pale grey with orange and blue stripes
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

        // Yellow and black diagonal hazard stripes, for the industrial biome
        static Texture2D MakeHazardTexture()
        {
            const int size = 128;
            var yellow = new Color(0.95f, 0.78f, 0.1f);
            var black = new Color(0.08f, 0.08f, 0.08f);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, (x + y) % 64 < 32 ? yellow : black);
            return SaveTexture(tex, Root + "/Hazard.png");
        }

        // Staggered bricks with dark mortar, for stone biomes (tinted by the material color)
        static Texture2D MakeBrickTexture()
        {
            const int size = 256, rows = 8, cols = 4, mortar = 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int rowH = size / rows, colW = size / cols;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int row = y / rowH;
                int xs = (x + (row % 2) * colW / 2) % size;
                bool joint = y % rowH < mortar || xs % colW < mortar;
                float shade = 0.82f + 0.12f * Mathf.PerlinNoise(x * 0.05f + row * 3.1f, y * 0.05f);
                float v = joint ? 0.35f : shade;
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
            return SaveTexture(tex, Root + "/Bricks.png");
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
