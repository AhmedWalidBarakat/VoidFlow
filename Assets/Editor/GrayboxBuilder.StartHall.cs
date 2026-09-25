using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow.EditorTools
{
    // The start hall: where you spawn before dropping into the course. A room on the ledge
    // above ramp 1, open at the front onto the course, with:
    //  - a glowing VOIDFLOW title over the opening
    //  - neon speed gates and floor arrows down the middle lane to the purple drop edge
    //  - a knife wall: every Mythic and Void knife spinning over a pedestal (E to try on)
    //  - the Knife Case and Sniper Case (E to open)
    //  - a skeet range: snipe clay discs out of the air
    //  - black and purple throughout: purple neon strips, a skylight, and glowing cubes scattered
    //    at random overhead
    public static partial class GrayboxBuilder
    {
        const float HallHalfWidth = 16f, HallDepth = 45f, HallHeight = 15f;
        const float HallFront = -1f, HallBack = HallFront - HallDepth;
        const int FloatingCubes = 32;

        // Builds the hall around `lane`, the x you walk down to drop onto the first ramp.
        // Returns the start zone (the whole hall floor), the spawn point at the back, and the
        // hall itself; all three move together when the world recenters.
        static (BoxCollider zone, Transform spawn, Transform hall) BuildStartHall(Transform parent, float lane, Texture2D grid, Material rampMat)
        {
            var hall = new GameObject("StartHall").transform;
            hall.SetParent(parent, false);

            // Wood floor, stone walls, dark riveted metal overhead
            Material floor = MakeMaterial("HallFloor", new Color(0.42f, 0.36f, 0.5f), wood);
            Material wall = MakeMaterial("HallWall", new Color(0.24f, 0.22f, 0.3f), stone);
            Material dark = MakeMaterial("HallDark", new Color(0.07f, 0.06f, 0.09f), GrayboxBuilder.metal);
            Material metal = MakeMaterial("HallMetal", new Color(0.2f, 0.18f, 0.25f), GrayboxBuilder.metal);
            Material purple = MakeGlow("GlowPurple", new Color(0.55f, 0.12f, 1f), 0.75f);
            Material violet = MakeGlow("GlowViolet", new Color(0.7f, 0.3f, 1f), 0.6f);
            Material indigo = MakeGlow("GlowIndigo", new Color(0.3f, 0.08f, 0.75f), 0.9f);

            float left = lane - HallHalfWidth, right = lane + HallHalfWidth;
            float width = right - left, midZ = (HallFront + HallBack) * 0.5f;

            // Shell: floor up to a 1m purple drop edge, walls, and a ceiling split by a
            // skylight slot over the lane
            Box("HallFloor", new Vector3(lane, -0.5f, midZ - 0.5f), new Vector3(width, 1f, HallDepth - 1f), floor, hall);
            Box("DropEdge", new Vector3(lane, -0.5f, HallFront - 0.5f), new Vector3(width, 1f, 1f), purple, hall);
            Box("HallWallLeft", new Vector3(left - 0.5f, HallHeight * 0.5f, midZ), new Vector3(1f, HallHeight, HallDepth), wall, hall);
            Box("HallWallRight", new Vector3(right + 0.5f, HallHeight * 0.5f, midZ), new Vector3(1f, HallHeight, HallDepth), wall, hall);
            Box("HallWallBack", new Vector3(lane, HallHeight * 0.5f, HallBack - 0.5f), new Vector3(width + 2f, HallHeight, 1f), wall, hall);
            const float slot = 3f;
            Box("HallCeilingLeft", new Vector3((left + lane - slot) * 0.5f, HallHeight + 0.5f, midZ), new Vector3(lane - slot - left, 1f, HallDepth), dark, hall);
            Box("HallCeilingRight", new Vector3((right + lane + slot) * 0.5f, HallHeight + 0.5f, midZ), new Vector3(right - lane - slot, 1f, HallDepth), dark, hall);

            // Purple frame around the opening
            Box("FrameLeft", new Vector3(left + 0.4f, HallHeight * 0.5f, HallFront - 0.4f), new Vector3(0.8f, HallHeight, 0.8f), purple, hall);
            Box("FrameRight", new Vector3(right - 0.4f, HallHeight * 0.5f, HallFront - 0.4f), new Vector3(0.8f, HallHeight, 0.8f), purple, hall);
            Box("FrameTop", new Vector3(lane, HallHeight - 0.4f, HallFront - 0.4f), new Vector3(width, 0.8f, 0.8f), purple, hall);

            // Neon strips along both side walls
            foreach (float x in new[] { left + 0.06f, right - 0.06f })
            {
                Deco("StripLow", hall, new Vector3(x, 1.2f, midZ), new Vector3(0.1f, 0.25f, HallDepth), Quaternion.identity, purple);
                Deco("StripHigh", hall, new Vector3(x, 12.6f, midZ), new Vector3(0.1f, 0.4f, HallDepth), Quaternion.identity, indigo);
                Deco("StripTop", hall, new Vector3(x, 13.3f, midZ), new Vector3(0.1f, 0.15f, HallDepth), Quaternion.identity, purple);
            }

            // Spawn pad with a glowing ring
            var spawnPos = new Vector3(lane, 0.02f, HallBack + 5f);
            Deco("SpawnPad", hall, spawnPos.WithY(0.01f), new Vector3(4f, 0.02f, 4f), Quaternion.identity, dark);
            foreach (var (offset, size) in new[]
                     {
                         (new Vector3(0f, 0f, 2f), new Vector3(4.2f, 0.04f, 0.2f)), (new Vector3(0f, 0f, -2f), new Vector3(4.2f, 0.04f, 0.2f)),
                         (new Vector3(2f, 0f, 0f), new Vector3(0.2f, 0.04f, 4.2f)), (new Vector3(-2f, 0f, 0f), new Vector3(0.2f, 0.04f, 4.2f)),
                     })
                Deco("SpawnRing", hall, spawnPos.WithY(0.02f) + offset, size, Quaternion.identity, violet);

            // Speed gates down the lane, alternating violet and purple, with arrows between them
            for (int g = 0; g < 4; g++)
            {
                float z = HallBack + 14f + g * 8f;
                Material m = g % 2 == 0 ? violet : purple;
                Box($"Gate{g + 1}PostL", new Vector3(lane - 4.5f, 2.6f, z), new Vector3(0.4f, 5.2f, 0.4f), m, hall);
                Box($"Gate{g + 1}PostR", new Vector3(lane + 4.5f, 2.6f, z), new Vector3(0.4f, 5.2f, 0.4f), m, hall);
                Box($"Gate{g + 1}Beam", new Vector3(lane, 5.4f, z), new Vector3(9.4f, 0.4f, 0.4f), m, hall);
            }
            for (float z = HallBack + 10f; z < HallFront - 4f; z += 4f)
            {
                Deco("ArrowL", hall, new Vector3(lane - 0.45f, 0.01f, z), new Vector3(0.25f, 0.02f, 1.3f), Quaternion.Euler(0f, 45f, 0f), purple);
                Deco("ArrowR", hall, new Vector3(lane + 0.45f, 0.01f, z), new Vector3(0.25f, 0.02f, 1.3f), Quaternion.Euler(0f, -45f, 0f), purple);
            }
            Label("DROP IN", hall, new Vector3(lane, 0.02f, HallFront - 2.5f), 0f, 0.9f, new Color(0.85f, 0.5f, 1f), pitch: 90f);

            // Knife wall on the left: every Mythic and Void knife spinning over a pedestal, in two
            // staggered rows
            int perRow = Mathf.CeilToInt((Skins.Knives.Length - 1) / 2f);
            float spacing = 38f / Mathf.Max(1, perRow - 1);
            for (int k = 1; k < Skins.Knives.Length; k++)
            {
                var skin = Skins.Knives[k];
                int row = (k - 1) % 2, place = (k - 1) / 2;
                float knifeX = left + 2.4f + row * 3.6f;
                float z = HallBack + 3.5f + place * spacing + row * spacing * 0.5f;
                Color color = skin.rarity == SkinRarity.Void ? new Color(0.6f, 0.25f, 1f) : Skins.RarityColor(skin.rarity);
                Material glow = MakeGlow($"Glow{Skins.RarityName(skin.rarity)}", color, 1.4f);
                Box($"KnifePedestal{k}", new Vector3(knifeX, 0.5f, z), new Vector3(0.8f, 1f, 0.8f), metal, hall);
                Deco("PedestalRim", hall, new Vector3(knifeX, 1.02f, z), new Vector3(0.88f, 0.05f, 0.88f), Quaternion.identity, glow);

                var display = new GameObject($"Display {skin.name}").AddComponent<SkinDisplay>();
                display.transform.SetParent(hall, false);
                display.transform.position = new Vector3(knifeX, 2.2f, z);
                display.scale = 5f;
                display.useRange = 1.8f;
                display.skinIndex = k;
                display.template = rampMat;

                string name = skin.name.Replace(" | ", "\n");
                Label($"{name}\n{Skins.RarityName(skin.rarity).ToUpper()}", hall, new Vector3(knifeX + 0.42f, 0.6f, z), -90f, 0.13f, color);
            }
            Label("KNIVES\nwalk up + E to try on", hall, new Vector3(left + 0.15f, 7.5f, HallBack + 22f), -90f, 1f, Color.white);

            // The two cases near the front on the right
            float caseX = right - 4f;
            var cases = new[]
            {
                ("KNIFE CASE", false, false, 2, HallFront - 8f, new Color(0.04f, 0.03f, 0.06f), new Color(0.55f, 0.15f, 1f), new Color(0.7f, 0.35f, 1f)),
                ("SNIPER CASE", true, false, 1, HallFront - 16f, new Color(0.04f, 0.03f, 0.06f), new Color(0.8f, 0.5f, 1f), new Color(0.9f, 0.3f, 1f)),
                ("GLOVE CASE", false, true, Skins.Gloves.Length - 10, HallFront - 24f, new Color(0.04f, 0.03f, 0.06f), new Color(1f, 0.3f, 0.8f), new Color(1f, 0.45f, 0.85f)),
            };
            foreach (var (title, sniper, gloves, showcase, caseZ, stripeA, stripeB, rayColor) in cases)
            {
                Material rim = MakeGlow(gloves ? "GlowGloveCase" : sniper ? "GlowSniperCase" : "GlowKnifeCase", rayColor, 1.4f);
                Box(gloves ? "GloveCasePedestal" : sniper ? "SniperCasePedestal" : "KnifeCasePedestal", new Vector3(caseX, 0.6f, caseZ), new Vector3(2.2f, 1.2f, 2.2f), metal, hall);
                Deco("CasePedestalRim", hall, new Vector3(caseX, 1.22f, caseZ), new Vector3(2.3f, 0.06f, 2.3f), Quaternion.identity, rim);
                var station = new GameObject(title).AddComponent<CaseStation>();
                station.transform.SetParent(hall, false);
                station.transform.SetPositionAndRotation(new Vector3(caseX, 1.25f, caseZ), Quaternion.Euler(0f, 90f, 0f));
                station.title = title;
                station.sniperCase = sniper;
                station.gloveCase = gloves;
                station.showcaseSkin = showcase;
                station.stripeA = stripeA;
                station.stripeB = stripeB;
                station.rayColor = rayColor;
                station.template = rampMat;
                Label($"{title}\nMythic  ·  Void 6%", hall, new Vector3(caseX - 1.12f, 0.7f, caseZ), 90f, 0.26f, rayColor);
            }

            // Glove wall along the back, behind the spawn: every glove over a pedestal, two
            // staggered rows
            {
                int count = Skins.Gloves.Length - 1, gloveRow = Mathf.CeilToInt(count / 2f);
                float from = left + 8.5f, to = right - 7f, step = (to - from) / Mathf.Max(1, gloveRow - 1);
                for (int g = 1; g < Skins.Gloves.Length; g++)
                {
                    var skin = Skins.Gloves[g];
                    int row = (g - 1) % 2, place = (g - 1) / 2;
                    float x = from + place * step + row * step * 0.5f, z = HallBack + 1.3f + row * 1.5f;
                    Color color = skin.rarity == SkinRarity.Void ? new Color(0.6f, 0.25f, 1f) : Skins.RarityColor(skin.rarity);
                    Material glow = MakeGlow($"Glow{Skins.RarityName(skin.rarity)}", color, 1.4f);
                    Box($"GlovePedestal{g}", new Vector3(x, 0.45f, z), new Vector3(0.6f, 0.9f, 0.6f), metal, hall);
                    Deco("PedestalRim", hall, new Vector3(x, 0.92f, z), new Vector3(0.66f, 0.04f, 0.66f), Quaternion.identity, glow);
                    var display = new GameObject($"Display {skin.name}").AddComponent<SkinDisplay>();
                    display.transform.SetParent(hall, false);
                    display.transform.position = new Vector3(x, 1.4f, z);
                    display.glove = true;
                    display.scale = 4f;
                    display.useRange = 1.3f;
                    display.skinIndex = g;
                    display.template = rampMat;
                    string name = skin.name.Replace("Gloves | ", "");
                    Label($"{name}\n{Skins.RarityName(skin.rarity).ToUpper()}", hall, new Vector3(x, 0.5f, z + 0.32f), 180f, 0.1f, color);
                }
                Label("GLOVES\nwalk up + E to try on", hall, new Vector3(lane, 7.5f, HallBack + 0.15f), 180f, 1f, Color.white);
            }

            // Skeet range at the back right: a launcher throws discs up and across the hall,
            // you snipe them before they land. Start button on a pedestal nearby.
            var range = new GameObject("SkeetRange").AddComponent<SkeetRange>();
            range.transform.SetParent(hall, false);
            range.transform.position = new Vector3(right - 2.5f, 0f, HallBack + 4f);
            range.template = rampMat;
            Box("SkeetLauncherBase", new Vector3(right - 2.5f, 0.45f, HallBack + 4f), new Vector3(1.4f, 0.9f, 1.4f), metal, hall);
            var aim = Quaternion.LookRotation(new Vector3(-0.85f, 0f, 0.55f));
            Deco("SkeetLauncherArm", hall, new Vector3(right - 2.5f, 1.2f, HallBack + 4f), new Vector3(0.35f, 0.35f, 1.3f), aim * Quaternion.Euler(-40f, 0f, 0f), dark);
            Deco("SkeetLauncherStripe", hall, new Vector3(right - 2.5f, 0.92f, HallBack + 4f), new Vector3(1.45f, 0.06f, 1.45f), Quaternion.identity, purple);
            var launcher = new GameObject("Launcher").transform;
            launcher.SetParent(range.transform, false);
            launcher.SetPositionAndRotation(new Vector3(right - 2.5f, 1.6f, HallBack + 4f), aim);
            range.launcher = launcher;
            Box("SkeetButtonPedestal", new Vector3(right - 6f, 0.55f, HallBack + 11f), new Vector3(1f, 1.1f, 1f), metal, hall);
            Deco("SkeetButton", hall, new Vector3(right - 6f, 1.15f, HallBack + 11f), new Vector3(0.55f, 0.1f, 0.55f), Quaternion.identity, violet);
            var button = new GameObject("StartButton").transform;
            button.SetParent(range.transform, false);
            button.position = new Vector3(right - 6f, 1.1f, HallBack + 11f);
            range.startButton = button;
            Label("SKEET\nE to start", hall, new Vector3(right - 6.52f, 0.62f, HallBack + 11f), 90f, 0.2f, new Color(0.85f, 0.55f, 1f));
            Label("SKEET RANGE", hall, new Vector3(right - 0.15f, 8.5f, HallBack + 14f), 90f, 0.9f, new Color(0.8f, 0.55f, 1f));
            Label("snipe the discs before they land", hall, new Vector3(right - 0.15f, 7.3f, HallBack + 14f), 90f, 0.4f, new Color(0.8f, 0.55f, 1f));

            // Title over the opening, on a dark banner so it reads against the sky
            Deco("TitleBanner", hall, new Vector3(lane, 10.6f, HallFront - 0.9f), new Vector3(22f, 4.6f, 0.2f), Quaternion.identity, dark);
            Deco("TitleBannerEdge", hall, new Vector3(lane, 8.25f, HallFront - 0.95f), new Vector3(22f, 0.15f, 0.2f), Quaternion.identity, purple);
            Label("VOIDFLOW", hall, new Vector3(lane, 11.2f, HallFront - 1.1f), 0f, 3f, Color.white);
            Label("surf  /  bhop  /  knives  /  cases", hall, new Vector3(lane, 9.1f, HallFront - 1.1f), 0f, 0.7f, new Color(0.75f, 0.5f, 1f));

            // Glowing cubes scattered at random overhead, each spinning and bobbing its own way
            var rng = new System.Random(20260924);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var palette = new[] { purple, violet, indigo, MakeGlow("GlowMagenta", new Color(0.85f, 0.15f, 1f), 0.8f), dark };
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
            PointLight("VioletLight", hall, new Vector3(lane - 8f, 8f, midZ + 6f), new Color(0.7f, 0.4f, 1f));
            PointLight("PurpleLight", hall, new Vector3(lane + 8f, 8f, midZ - 8f), new Color(0.55f, 0.2f, 1f));

            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(hall, false);
            spawn.SetPositionAndRotation(spawnPos, Quaternion.identity);
            BoxCollider zone = Zone("StartZone", new Vector3(lane, 2f, midZ), new Vector3(width, 4f, HallDepth), hall);
            return (zone, spawn, hall);
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
