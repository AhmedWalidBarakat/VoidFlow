using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow.EditorTools
{
    // The start hall: where you spawn before dropping into the course. A room on the ledge
    // above ramp 1, open at the front onto the course, with:
    //  - a glowing VOIDFLOW title over the opening
    //  - neon speed gates and floor arrows down the middle lane to the orange drop edge
    //  - a knife wall: one spinning knife per rarity, a teaser for knives to come
    //  - a spinning VOID CASE, earned by finishing maps
    //  - a small practice ramp to warm up your strafes
    //  - utopia's orange and blue as neon strips, a skylight, and glowing cubes scattered
    //    at random overhead
    public static partial class GrayboxBuilder
    {
        const float HallHalfWidth = 16f, HallDepth = 45f, HallHeight = 15f;
        const float HallFront = -1f, HallBack = HallFront - HallDepth;
        const int FloatingCubes = 32;

        static readonly (string name, string rarity, Color color)[] ShowcaseKnives =
        {
            ("Tide Cutter", "RARE", new Color(0.2f, 0.5f, 1f)),
            ("Frost Line", "EPIC", new Color(0.6f, 0.3f, 1f)),
            ("Neon Fang", "MYTHIC", new Color(1f, 0.2f, 0.6f)),
            ("Void Edge", "LEGENDARY", new Color(1f, 0.75f, 0.15f)),
        };

        // Builds the hall around `lane`, the x you walk down to drop onto the first ramp.
        // Returns the start zone (the whole hall floor), the spawn point at the back, and the
        // hall itself; all three move together when the world recenters.
        static (BoxCollider zone, Transform spawn, Transform hall) BuildStartHall(Transform parent, float lane, Texture2D grid, Material rampMat)
        {
            var hall = new GameObject("StartHall").transform;
            hall.SetParent(parent, false);

            Material floor = MakeMaterial("HallFloor", new Color(0.18f, 0.19f, 0.23f), grid);
            Material wall = MakeMaterial("HallWall", new Color(0.8f, 0.8f, 0.84f), grid);
            Material dark = MakeMaterial("HallDark", new Color(0.08f, 0.08f, 0.1f), grid);
            Material metal = MakeMaterial("HallMetal", new Color(0.35f, 0.36f, 0.4f), grid);
            Material orange = MakeGlow("GlowOrange", new Color(1f, 0.33f, 0.02f), 1.1f);
            Material cyan = MakeGlow("GlowCyan", new Color(0.1f, 0.75f, 1f), 1.3f);
            Material blue = MakeGlow("GlowBlue", new Color(0.2f, 0.35f, 1f), 1.3f);

            float left = lane - HallHalfWidth, right = lane + HallHalfWidth;
            float width = right - left, midZ = (HallFront + HallBack) * 0.5f;

            // Shell: floor up to a 1m orange drop edge, walls, and a ceiling split by a
            // skylight slot over the lane
            Box("HallFloor", new Vector3(lane, -0.5f, midZ - 0.5f), new Vector3(width, 1f, HallDepth - 1f), floor, hall);
            Box("DropEdge", new Vector3(lane, -0.5f, HallFront - 0.5f), new Vector3(width, 1f, 1f), orange, hall);
            Box("HallWallLeft", new Vector3(left - 0.5f, HallHeight * 0.5f, midZ), new Vector3(1f, HallHeight, HallDepth), wall, hall);
            Box("HallWallRight", new Vector3(right + 0.5f, HallHeight * 0.5f, midZ), new Vector3(1f, HallHeight, HallDepth), wall, hall);
            Box("HallWallBack", new Vector3(lane, HallHeight * 0.5f, HallBack - 0.5f), new Vector3(width + 2f, HallHeight, 1f), wall, hall);
            const float slot = 3f;
            Box("HallCeilingLeft", new Vector3((left + lane - slot) * 0.5f, HallHeight + 0.5f, midZ), new Vector3(lane - slot - left, 1f, HallDepth), dark, hall);
            Box("HallCeilingRight", new Vector3((right + lane + slot) * 0.5f, HallHeight + 0.5f, midZ), new Vector3(right - lane - slot, 1f, HallDepth), dark, hall);

            // Orange frame around the opening
            Box("FrameLeft", new Vector3(left + 0.4f, HallHeight * 0.5f, HallFront - 0.4f), new Vector3(0.8f, HallHeight, 0.8f), orange, hall);
            Box("FrameRight", new Vector3(right - 0.4f, HallHeight * 0.5f, HallFront - 0.4f), new Vector3(0.8f, HallHeight, 0.8f), orange, hall);
            Box("FrameTop", new Vector3(lane, HallHeight - 0.4f, HallFront - 0.4f), new Vector3(width, 0.8f, 0.8f), orange, hall);

            // Neon strips along both side walls
            foreach (float x in new[] { left + 0.06f, right - 0.06f })
            {
                Deco("StripLow", hall, new Vector3(x, 1.2f, midZ), new Vector3(0.1f, 0.25f, HallDepth), Quaternion.identity, orange);
                Deco("StripHigh", hall, new Vector3(x, 12.6f, midZ), new Vector3(0.1f, 0.4f, HallDepth), Quaternion.identity, blue);
                Deco("StripTop", hall, new Vector3(x, 13.3f, midZ), new Vector3(0.1f, 0.15f, HallDepth), Quaternion.identity, orange);
            }

            // Spawn pad with a glowing ring
            var spawnPos = new Vector3(lane, 0.02f, HallBack + 5f);
            Deco("SpawnPad", hall, spawnPos.WithY(0.01f), new Vector3(4f, 0.02f, 4f), Quaternion.identity, dark);
            foreach (var (offset, size) in new[]
                     {
                         (new Vector3(0f, 0f, 2f), new Vector3(4.2f, 0.04f, 0.2f)), (new Vector3(0f, 0f, -2f), new Vector3(4.2f, 0.04f, 0.2f)),
                         (new Vector3(2f, 0f, 0f), new Vector3(0.2f, 0.04f, 4.2f)), (new Vector3(-2f, 0f, 0f), new Vector3(0.2f, 0.04f, 4.2f)),
                     })
                Deco("SpawnRing", hall, spawnPos.WithY(0.02f) + offset, size, Quaternion.identity, cyan);

            // Speed gates down the lane, alternating cyan and orange, with arrows between them
            for (int g = 0; g < 4; g++)
            {
                float z = HallBack + 14f + g * 8f;
                Material m = g % 2 == 0 ? cyan : orange;
                Box($"Gate{g + 1}PostL", new Vector3(lane - 4.5f, 2.6f, z), new Vector3(0.4f, 5.2f, 0.4f), m, hall);
                Box($"Gate{g + 1}PostR", new Vector3(lane + 4.5f, 2.6f, z), new Vector3(0.4f, 5.2f, 0.4f), m, hall);
                Box($"Gate{g + 1}Beam", new Vector3(lane, 5.4f, z), new Vector3(9.4f, 0.4f, 0.4f), m, hall);
            }
            for (float z = HallBack + 10f; z < HallFront - 4f; z += 4f)
            {
                Deco("ArrowL", hall, new Vector3(lane - 0.45f, 0.01f, z), new Vector3(0.25f, 0.02f, 1.3f), Quaternion.Euler(0f, 45f, 0f), orange);
                Deco("ArrowR", hall, new Vector3(lane + 0.45f, 0.01f, z), new Vector3(0.25f, 0.02f, 1.3f), Quaternion.Euler(0f, -45f, 0f), orange);
            }
            Label("DROP IN", hall, new Vector3(lane, 0.02f, HallFront - 2.5f), 0f, 0.9f, new Color(1f, 0.55f, 0.2f), pitch: 90f);

            // Knife wall on the left: one spinning knife per rarity on a glowing pedestal
            Material grip = MakeMaterial("KnifeGrip", new Color(0.12f, 0.1f, 0.09f), grid);
            float knifeX = left + 3f;
            for (int k = 0; k < ShowcaseKnives.Length; k++)
            {
                var (name, rarity, color) = ShowcaseKnives[k];
                float z = HallBack + 10f + k * 8f;
                Material glow = MakeGlow($"Glow{rarity[0]}{rarity.Substring(1).ToLower()}", color, 1.4f);
                Box($"KnifePedestal{k + 1}", new Vector3(knifeX, 0.6f, z), new Vector3(1.6f, 1.2f, 1.6f), metal, hall);
                Deco("PedestalRim", hall, new Vector3(knifeX, 1.22f, z), new Vector3(1.7f, 0.06f, 1.7f), Quaternion.identity, glow);

                var knife = new GameObject(name).transform;
                knife.SetParent(hall, false);
                knife.SetPositionAndRotation(new Vector3(knifeX, 3f, z), Quaternion.Euler(0f, 0f, 18f));
                knife.localScale = Vector3.one * 2.4f;
                BuildKnife(knife, glow, grip, metal);
                var floaty = knife.gameObject.AddComponent<Floaty>();
                floaty.spin = new Vector3(0f, 55f, 0f);
                floaty.bobHeight = 0.12f;

                Label($"{name}\n{rarity}", hall, new Vector3(knifeX + 0.82f, 0.65f, z), -90f, 0.28f, color);
            }
            Label("KNIVES\ncoming soon", hall, new Vector3(left + 0.15f, 7.5f, HallBack + 22f), -90f, 1.1f, Color.white);

            // Case display near the front on the right
            float caseX = right - 4f, caseZ = HallFront - 10f;
            Box("CasePedestal", new Vector3(caseX, 0.6f, caseZ), new Vector3(2f, 1.2f, 2f), metal, hall);
            Deco("CasePedestalRim", hall, new Vector3(caseX, 1.22f, caseZ), new Vector3(2.1f, 0.06f, 2.1f), Quaternion.identity, orange);
            var voidCase = new GameObject("VoidCase").transform;
            voidCase.SetParent(hall, false);
            voidCase.position = new Vector3(caseX, 2.6f, caseZ);
            voidCase.localScale = Vector3.one * 1.6f;
            BuildCase(voidCase, dark, metal, orange);
            var spinCase = voidCase.gameObject.AddComponent<Floaty>();
            spinCase.spin = new Vector3(0f, 30f, 0f);
            spinCase.bobHeight = 0.08f;
            Label("VOID CASE\nearn cases by finishing maps", hall, new Vector3(caseX - 1.02f, 0.65f, caseZ), 90f, 0.24f, new Color(1f, 0.6f, 0.25f));

            // Practice ramp along the right wall, to warm up your strafes before dropping in
            var practice = RampShapes.Lay(RampShapes.Kind.Prism, 4f, -1f, new Vector3(right - 4f, 5.3f, HallBack + 3f), Vector3.forward,
                new[] { (0f, 0f), (22f, 0f) }, null);
            SpawnMesh("PracticeRamp", practice.ridge[0], RampShapes.BuildMesh(practice, "PracticeRamp"), rampMat, hall, convex: false);
            Label("PRACTICE RAMP", hall, new Vector3(right - 0.15f, 8.5f, HallBack + 14f), 90f, 0.9f, new Color(0.5f, 0.85f, 1f));

            // Title over the opening, on a dark banner so it reads against the sky
            Deco("TitleBanner", hall, new Vector3(lane, 10.6f, HallFront - 0.9f), new Vector3(22f, 4.6f, 0.2f), Quaternion.identity, dark);
            Deco("TitleBannerEdge", hall, new Vector3(lane, 8.25f, HallFront - 0.95f), new Vector3(22f, 0.15f, 0.2f), Quaternion.identity, orange);
            Label("VOIDFLOW", hall, new Vector3(lane, 11.2f, HallFront - 1.1f), 0f, 3f, Color.white);
            Label("surf  /  bhop  /  knives  /  cases", hall, new Vector3(lane, 9.1f, HallFront - 1.1f), 0f, 0.7f, new Color(0.4f, 0.85f, 1f));

            // Glowing cubes scattered at random overhead, each spinning and bobbing its own way
            var rng = new System.Random(20260924);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var palette = new[] { orange, cyan, blue, MakeGlow("GlowPink", new Color(1f, 0.15f, 0.55f), 1.3f), MakeGlow("GlowGold", new Color(1f, 0.7f, 0.1f), 1.3f) };
            for (int c = 0; c < FloatingCubes; c++)
            {
                float size = Rand(0.25f, 1.1f);
                var cube = Deco("FloatingCube", hall,
                    new Vector3(Rand(left + 2f, right - 2f), Rand(6.5f, 13f), Rand(HallBack + 3f, HallFront - 3f)),
                    Vector3.one * size, Quaternion.Euler(Rand(0f, 360f), Rand(0f, 360f), Rand(0f, 360f)),
                    palette[rng.Next(palette.Length)]);
                var f = cube.AddComponent<Floaty>();
                f.spin = new Vector3(Rand(-60f, 60f), Rand(-60f, 60f), Rand(-60f, 60f));
                f.bobHeight = Rand(0.2f, 0.6f);
                f.bobSpeed = Rand(0.5f, 1.5f);
            }

            // A little colored light to go with the neon
            PointLight("CyanLight", hall, new Vector3(lane - 8f, 8f, midZ + 6f), new Color(0.3f, 0.8f, 1f));
            PointLight("OrangeLight", hall, new Vector3(lane + 8f, 8f, midZ - 8f), new Color(1f, 0.5f, 0.15f));

            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(hall, false);
            spawn.SetPositionAndRotation(spawnPos, Quaternion.identity);
            BoxCollider zone = Zone("StartZone", new Vector3(lane, 2f, midZ), new Vector3(width, 4f, HallDepth), hall);
            return (zone, spawn, hall);
        }

        // A simple knife from boxes: glowing blade with a diamond tip, guard, grip, pommel
        static void BuildKnife(Transform root, Material blade, Material grip, Material metal)
        {
            Deco("Blade", root, new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 0.75f, 0.2f), Quaternion.identity, blade, local: true);
            Deco("Tip", root, new Vector3(0f, 0.93f, 0.03f), new Vector3(0.05f, 0.16f, 0.16f), Quaternion.Euler(45f, 0f, 0f), blade, local: true);
            Deco("Guard", root, new Vector3(0f, 0.15f, 0f), new Vector3(0.1f, 0.06f, 0.38f), Quaternion.identity, metal, local: true);
            Deco("Grip", root, new Vector3(0f, -0.07f, 0f), new Vector3(0.08f, 0.34f, 0.12f), Quaternion.identity, grip, local: true);
            Deco("Pommel", root, new Vector3(0f, -0.27f, 0f), new Vector3(0.1f, 0.07f, 0.15f), Quaternion.identity, metal, local: true);
        }

        // A case from boxes: dark body, glowing band and seams, metal lid and handle, a "?"
        static void BuildCase(Transform root, Material body, Material metal, Material glow)
        {
            Deco("Body", root, Vector3.zero, new Vector3(1.4f, 0.8f, 0.9f), Quaternion.identity, body, local: true);
            Deco("Band", root, Vector3.zero, new Vector3(1.44f, 0.07f, 0.94f), Quaternion.identity, glow, local: true);
            Deco("Lid", root, new Vector3(0f, 0.46f, 0f), new Vector3(1.44f, 0.12f, 0.94f), Quaternion.identity, metal, local: true);
            Deco("Handle", root, new Vector3(0f, 0.58f, 0f), new Vector3(0.5f, 0.08f, 0.08f), Quaternion.identity, metal, local: true);
            foreach (float x in new[] { -0.7f, 0.7f })
                Deco("Seam", root, new Vector3(x, 0f, 0f), new Vector3(0.04f, 0.82f, 0.92f), Quaternion.identity, glow, local: true);
            Label("?", root, new Vector3(0f, -0.15f, -0.46f), 0f, 0.5f, new Color(1f, 0.6f, 0.25f), local: true);
            Label("?", root, new Vector3(0f, -0.15f, 0.46f), 180f, 0.5f, new Color(1f, 0.6f, 0.25f), local: true);
        }

        // A decorative cube with no collider
        static GameObject Deco(string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material mat, bool local = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            if (local) go.transform.SetLocalPositionAndRotation(position, rotation);
            else go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // 3D text. It reads correctly when you look along its forward direction (yaw/pitch).
        static GameObject Label(string text, Transform parent, Vector3 position, float yaw, float height, Color color,
            float pitch = 0f, bool local = false)
        {
            var go = new GameObject("Label " + text.Split('\n')[0]);
            go.transform.SetParent(parent, false);
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (local) go.transform.SetLocalPositionAndRotation(position, rotation);
            else go.transform.SetPositionAndRotation(position, rotation);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 100;
            tm.characterSize = height * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return go;
        }

        static void PointLight(string name, Transform parent, Vector3 position, Color color)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = 30f;
            light.intensity = 2f;
        }

        static Material MakeGlow(string name, Color color, float intensity)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color * 0.25f); // let the glow carry the color, or lighting washes it out
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(mat, $"{Root}/{name}.mat");
            return mat;
        }

        // Soft bloom so the neon glows
        static void AddGlowVolume(Transform parent)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Root + "/GlowVolume.asset");
            var bloom = profile.Add<Bloom>(true);
            bloom.name = "Bloom";
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.6f);
            AssetDatabase.AddObjectToAsset(bloom, profile);

            var go = new GameObject("GlowVolume");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }
}
