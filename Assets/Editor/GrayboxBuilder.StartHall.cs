using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow.EditorTools
{
    // The start hall: the Celestial Terrace, a white-and-gold court open to a bright sky, high
    // above a distant utopia of floating islands and gleaming spires, where you spawn before
    // dropping into the course. On the ledge above ramp 1, open at the front onto the course,
    // with:
    //  - a white marble floor inlaid with gold, white walls carved with gold-framed arches,
    //    and no roof: white arches span it against the sky
    //  - a runway with speed gates and holographic diamonds down the lane to the drop
    //  - the Void Case gallery: every knife, sniper and glove a Void Case can drop, on lit
    //    ledges along the walls, just to look at
    //  - the Void Case on show, with a sign saying how to earn one
    //  - a skeet range with its own corner: snipe clay discs out of the air
    //  - a hanging "press I" inventory sign you see as you spawn
    public static partial class GrayboxBuilder
    {
        const float HallHalfWidth = 22f, HallDepth = 60f, HallHeight = 16f;
        const float HallFront = -1f, HallBack = HallFront - HallDepth;
        const int FloatingShards = 14;

        // Builds the hall around `lane`, the x you walk down to drop onto the first ramp.
        // Returns the start zone (the whole hall floor), the spawn point at the back, and the
        // hall itself; all three move together when the world recenters.
        static (BoxCollider zone, Transform spawn, Transform hall) BuildStartHall(Transform parent, float lane, Texture2D grid, Material rampMat)
        {
            var hall = new GameObject("StartHall").transform;
            hall.SetParent(parent, false);

            // Void Sanctum: polished black marble with gold inlay and violet-lit seams, violet
            // stone walls carved with gold-framed gothic arches, a coffered ceiling. Warm gold
            // light for the structure; purple and magenta for the things you use.
            // White marble and gold, deep lapis blue for the signs (so gold and white text reads),
            // a cool sky blue for the lit edges; warm gold light over it all
            Material floor = MakeMaterial("HallFloor", Color.white, MakeSanctumFloor());
            floor.SetTextureScale("_BaseMap", Vector2.one * 0.25f); // 8m of pattern per tile
            floor.SetFloat("_Smoothness", 0.7f);
            floor.SetFloat("_Metallic", 0.05f);
            GlowMap(floor, MakeSanctumFloorGlow(), new Color(1f, 0.85f, 0.6f) * 0.45f);
            Material wall = MakeMaterial("HallWall", Color.white, MakeSanctumWall());
            wall.SetTextureScale("_BaseMap", new Vector2(0.25f, 2f)); // 8m across, 8m of height
            wall.SetFloat("_Smoothness", 0.3f);
            wall.SetFloat("_Metallic", 0.15f);
            Material dark = MakeMaterial("HallDark", new Color(0.07f, 0.1f, 0.24f), GrayboxBuilder.metal); // lapis
            dark.SetFloat("_Smoothness", 0.7f);
            dark.SetFloat("_Metallic", 0.3f);
            Material metal = MakeMaterial("HallMetal", new Color(0.95f, 0.76f, 0.4f), GrayboxBuilder.metal); // polished gold
            metal.SetFloat("_Smoothness", 0.8f);
            metal.SetFloat("_Metallic", 0.9f);
            Material ivory = MakeMaterial("HallIvory", new Color(0.97f, 0.96f, 0.93f), GrayboxBuilder.metal);
            ivory.SetFloat("_Smoothness", 0.45f);
            Material purple = MakeGlow("GlowPurple", new Color(0.45f, 0.72f, 1f), 1f);   // (sky blue)
            Material violet = MakeGlow("GlowViolet", new Color(0.75f, 0.88f, 1f), 0.9f); // (pale sky)
            Material gold = MakeGlow("GlowGold", new Color(1f, 0.68f, 0.28f), 1.5f);
            Material goldSoft = MakeGlow("GlowGoldSoft", new Color(0.95f, 0.6f, 0.3f), 0.8f);
            Material magenta = MakeGlow("GlowMagenta", new Color(1f, 0.66f, 0.5f), 1.1f); // (rose gold)

            float left = lane - HallHalfWidth, right = lane + HallHalfWidth;
            float width = right - left, midZ = (HallFront + HallBack) * 0.5f;

            // Shell: floor up to a 1m purple drop edge, walls, a ceiling split by a skylight slot
            Box("HallFloor", new Vector3(lane, -0.5f, midZ - 0.5f), new Vector3(width, 1f, HallDepth - 1f), floor, hall);
            Box("DropEdge", new Vector3(lane, -0.5f, HallFront - 0.5f), new Vector3(width, 1f, 1f), purple, hall);
            Box("HallWallLeft", new Vector3(left - 0.5f, HallHeight * 0.5f, midZ), new Vector3(1f, HallHeight, HallDepth), wall, hall, stripes: true);
            Box("HallWallRight", new Vector3(right + 0.5f, HallHeight * 0.5f, midZ), new Vector3(1f, HallHeight, HallDepth), wall, hall, stripes: true);
            Box("HallWallBack", new Vector3(lane, HallHeight * 0.5f, HallBack - 0.5f), new Vector3(width + 2f, HallHeight, 1f), wall, hall, stripes: true);
            // A gold coping along the tops of the walls (the court is open to the sky)
            foreach (float x in new[] { left - 0.5f, right + 0.5f })
                Deco("Coping", hall, new Vector3(x, HallHeight + 0.2f, midZ), new Vector3(1.6f, 0.4f, HallDepth + 1f), Quaternion.identity, metal);
            Deco("Coping", hall, new Vector3(lane, HallHeight + 0.2f, HallBack - 0.5f), new Vector3(width + 2.6f, 0.4f, 1.6f), Quaternion.identity, metal);

            // Crisp edges: a dark baseboard with a gold line along every wall, a purple line up top
            foreach (var (a, b) in new[] { (new Vector3(left, 0f, HallBack), new Vector3(left, 0f, HallFront - 1f)), (new Vector3(right, 0f, HallBack), new Vector3(right, 0f, HallFront - 1f)), (new Vector3(left, 0f, HallBack), new Vector3(right, 0f, HallBack)) })
            {
                Vector3 mid = (a + b) * 0.5f, dir = (b - a).normalized;
                Vector3 inward = dir.x != 0f ? Vector3.forward : (a.x < lane ? Vector3.right : Vector3.left);
                float len = (b - a).magnitude;
                Vector3 Size(float along, float h, float depth) => dir.x != 0f ? new Vector3(along, h, depth) : new Vector3(depth, h, along);
                Deco("Baseboard", hall, mid + inward * 0.1f + Vector3.up * 0.15f, Size(len, 0.3f, 0.2f), Quaternion.identity, dark);
                Deco("BaseLight", hall, mid + inward * 0.21f + Vector3.up * 0.33f, Size(len, 0.04f, 0.03f), Quaternion.identity, gold);
                Deco("TopLight", hall, mid + inward * 0.06f + Vector3.up * (HallHeight - 1.6f), Size(len, 0.12f, 0.1f), Quaternion.identity, purple);
            }

            // No roof: tall round white arches span the court every 12m against the sky, each
            // with a gold line under its curve
            for (float z = HallBack + 6f; z < HallFront - 4f; z += 12f)
            {
                const int pieces = 18;
                float r = width * 0.5f;
                for (int p = 0; p < pieces; p++)
                {
                    float a0 = Mathf.PI * p / pieces, a1 = Mathf.PI * (p + 1) / pieces;
                    Vector3 q0 = new(lane - Mathf.Cos(a0) * r, HallHeight + Mathf.Sin(a0) * r * 0.55f, z);
                    Vector3 q1 = new(lane - Mathf.Cos(a1) * r, HallHeight + Mathf.Sin(a1) * r * 0.55f, z);
                    var along = Quaternion.LookRotation(q1 - q0, Vector3.forward);
                    Deco("ArchStone", hall, (q0 + q1) * 0.5f, new Vector3(0.9f, 1.2f, (q1 - q0).magnitude + 0.2f), along, ivory);
                    Deco("ArchGold", hall, (q0 + q1) * 0.5f + along * Vector3.down * 0.65f, new Vector3(0.3f, 0.1f, (q1 - q0).magnitude + 0.05f), along, gold);
                }
            }

            // The opening: a heavy dark frame, gold inside, purple on its outer face
            Box("FrameLeft", new Vector3(left + 0.7f, HallHeight * 0.5f, HallFront - 0.5f), new Vector3(1.4f, HallHeight, 1f), metal, hall);
            Box("FrameRight", new Vector3(right - 0.7f, HallHeight * 0.5f, HallFront - 0.5f), new Vector3(1.4f, HallHeight, 1f), metal, hall);
            Box("FrameTop", new Vector3(lane, HallHeight - 0.7f, HallFront - 0.5f), new Vector3(width, 1.4f, 1f), metal, hall);
            Deco("FrameEdgeL", hall, new Vector3(left + 1.42f, HallHeight * 0.5f - 0.7f, HallFront - 0.5f), new Vector3(0.05f, HallHeight - 1.4f, 0.4f), Quaternion.identity, gold);
            Deco("FrameEdgeR", hall, new Vector3(right - 1.42f, HallHeight * 0.5f - 0.7f, HallFront - 0.5f), new Vector3(0.05f, HallHeight - 1.4f, 0.4f), Quaternion.identity, gold);
            Deco("FrameEdgeTop", hall, new Vector3(lane, HallHeight - 1.42f, HallFront - 0.5f), new Vector3(width - 2.8f, 0.05f, 0.4f), Quaternion.identity, gold);
            Deco("FrameGlowL", hall, new Vector3(left + 0.7f, HallHeight * 0.5f, HallFront + 0.02f), new Vector3(0.3f, HallHeight, 0.05f), Quaternion.identity, purple);
            Deco("FrameGlowR", hall, new Vector3(right - 0.7f, HallHeight * 0.5f, HallFront + 0.02f), new Vector3(0.3f, HallHeight, 0.05f), Quaternion.identity, purple);

            // Runway down the lane: a dark inset strip with gold edges and chevrons
            float runFrom = HallBack + 11f, runTo = HallFront - 2f;
            Deco("Runway", hall, new Vector3(lane, 0.006f, (runFrom + runTo) * 0.5f), new Vector3(5f, 0.012f, runTo - runFrom), Quaternion.identity, dark);
            foreach (float sx in new[] { -2.5f, 2.5f })
                Deco("RunwayEdge", hall, new Vector3(lane + sx, 0.014f, (runFrom + runTo) * 0.5f), new Vector3(0.08f, 0.02f, runTo - runFrom), Quaternion.identity, gold);
            for (float z = runFrom + 2f; z < runTo - 4f; z += 4f)
            {
                Deco("ArrowL", hall, new Vector3(lane - 0.45f, 0.016f, z), new Vector3(0.22f, 0.02f, 1.3f), Quaternion.Euler(0f, 45f, 0f), goldSoft);
                Deco("ArrowR", hall, new Vector3(lane + 0.45f, 0.016f, z), new Vector3(0.22f, 0.02f, 1.3f), Quaternion.Euler(0f, -45f, 0f), goldSoft);
            }
            Label("DROP IN", hall, new Vector3(lane, 0.03f, HallFront - 2.6f), 0f, 0.9f, new Color(1f, 0.78f, 0.42f), pitch: 90f);

            // Spawn platform: a dark disc on a glowing ring, a violet core
            var spawnPos = new Vector3(lane, 0.02f, HallBack + 8f);
            Shape(PrimitiveType.Cylinder, "SpawnRing", hall, spawnPos.WithY(0.03f), new Vector3(5f, 0.03f, 5f), gold);
            Shape(PrimitiveType.Cylinder, "SpawnPad", hall, spawnPos.WithY(0.04f), new Vector3(4.6f, 0.04f, 4.6f), dark);
            Shape(PrimitiveType.Cylinder, "SpawnCore", hall, spawnPos.WithY(0.07f), new Vector3(1.4f, 0.03f, 1.4f), violet);

            // Speed gates down the runway: dark posts with lit inner edges, a glowing underside
            for (int g = 0; g < 4; g++)
            {
                float z = HallBack + 18f + g * 9f;
                Material glow = g % 2 == 0 ? gold : purple;
                foreach (float sx in new[] { -1f, 1f })
                {
                    Box($"Gate{g + 1}Post{(sx < 0 ? "L" : "R")}", new Vector3(lane + sx * 4.5f, 2.7f, z), new Vector3(0.5f, 5.4f, 0.5f), metal, hall);
                    Deco("GateEdge", hall, new Vector3(lane + sx * 4.23f, 2.6f, z), new Vector3(0.04f, 4.8f, 0.22f), Quaternion.identity, glow);
                }
                Box($"Gate{g + 1}Beam", new Vector3(lane, 5.45f, z), new Vector3(9.5f, 0.5f, 0.5f), metal, hall);
                Deco("GateGlow", hall, new Vector3(lane, 5.18f, z), new Vector3(8.3f, 0.04f, 0.22f), Quaternion.identity, glow);
            }

            // Over the runway near the opening: two holographic diamonds turning opposite ways
            foreach (var (size, speed, mat) in new[] { (5.2f, 14f, gold), (3.6f, -22f, magenta) })
            {
                var diamond = new GameObject("HoloDiamond").transform;
                diamond.SetParent(hall, false);
                diamond.SetPositionAndRotation(new Vector3(lane, 6.4f, HallFront - 9f), Quaternion.Euler(0f, 0f, 45f));
                float h = size * 0.5f;
                Deco("Edge", diamond, new Vector3(0f, h, 0f), new Vector3(size + 0.12f, 0.12f, 0.12f), Quaternion.identity, mat, local: true);
                Deco("Edge", diamond, new Vector3(0f, -h, 0f), new Vector3(size + 0.12f, 0.12f, 0.12f), Quaternion.identity, mat, local: true);
                Deco("Edge", diamond, new Vector3(h, 0f, 0f), new Vector3(0.12f, size, 0.12f), Quaternion.identity, mat, local: true);
                Deco("Edge", diamond, new Vector3(-h, 0f, 0f), new Vector3(0.12f, size, 0.12f), Quaternion.identity, mat, local: true);
                var spin = diamond.gameObject.AddComponent<Floaty>();
                spin.spin = new Vector3(0f, 0f, speed);
                spin.bobHeight = 0.15f;
                spin.bobSpeed = 0.6f;
            }

            // The Void Case gallery: everything a Void Case can drop, to look at (not to take).
            // Knives down the left wall, snipers along the right, gloves on the back wall.
            GalleryWall(hall, "KNIVES", Skins.Knives, ItemSlot.Secondary, new Vector3(left, 0f, HallBack + 6f), Vector3.forward, Vector3.right,
                tiers: 3, spacing: 1.95f, depth: 1.35f, step: 0.22f, scale: 3.8f, lift: 0.75f, rampMat, metal, ivory, gold);
            GalleryWall(hall, "SNIPERS", Skins.Snipers, ItemSlot.Primary, new Vector3(right, 0f, HallFront - 8f), Vector3.back, Vector3.left,
                tiers: 4, spacing: 2.7f, depth: 1.7f, step: 0f, scale: 1.1f, lift: 0.5f, rampMat, metal, ivory, purple);
            int gloveColumns = Mathf.CeilToInt((Skins.Gloves.Length - 1) / 2f);
            GalleryWall(hall, "GLOVES", Skins.Gloves, ItemSlot.Hands, new Vector3(lane - (gloveColumns - 1) * 2.3f * 0.5f, 0f, HallBack), Vector3.right, Vector3.forward,
                tiers: 2, spacing: 2.3f, depth: 1.2f, step: 0f, scale: 4f, lift: 0.5f, rampMat, metal, ivory, magenta);
            Label("VOID CASE GALLERY", hall, new Vector3(lane, HallHeight - 3.2f, HallBack + 0.2f), 180f, 1.1f, new Color(0.85f, 0.6f, 1f));

            // The Void Case on show, on a round platform by the lane, with a sign saying how to
            // earn one. It's only to look at: earned cases are opened from the inventory.
            {
                var at = new Vector3(lane - 9f, 0f, HallFront - 14f);
                Material pink = MakeGlow("GlowVoidCase", new Color(1f, 0.35f, 0.8f), 1.5f);
                Shape(PrimitiveType.Cylinder, "VoidCasePlatformRing", hall, at.WithY(0.03f), new Vector3(7.4f, 0.03f, 7.4f), pink);
                Shape(PrimitiveType.Cylinder, "VoidCasePlatform", hall, at.WithY(0.05f), new Vector3(7f, 0.05f, 7f), dark);
                Box("VoidCasePedestal", at + new Vector3(0f, 0.6f, 0f), new Vector3(2f, 1.2f, 2f), metal, hall);
                Deco("VoidCasePedestalRim", hall, at + new Vector3(0f, 1.22f, 0f), new Vector3(2.1f, 0.06f, 2.1f), Quaternion.identity, pink);
                var show = new GameObject("VoidCaseDisplay").AddComponent<VoidCaseDisplay>();
                show.transform.SetParent(hall, false);
                show.transform.position = at + new Vector3(0f, 2.1f, 0f);
                show.template = rampMat;
                show.scale = 1.6f;
                PointLight("VoidCaseLight", hall, at + new Vector3(0f, 4f, 0f), new Color(1f, 0.4f, 0.85f), 1.4f, 10f);

                // The sign stands on the far side of the case, facing the lane (its back to the
                // wall: 3D text shows through from behind)
                var sign = new GameObject("VoidCaseSign").transform;
                sign.SetParent(hall, false);
                sign.SetPositionAndRotation(at + new Vector3(-4.3f, 4.8f, 0f), Quaternion.Euler(0f, -90f, 0f));
                Deco("Panel", sign, new Vector3(0f, 0f, 0.06f), new Vector3(7f, 4.4f, 0.12f), Quaternion.identity, dark, local: true);
                foreach (float y in new[] { -2.25f, 2.25f })
                    Deco("Edge", sign, new Vector3(0f, y, -0.02f), new Vector3(7.2f, 0.08f, 0.1f), Quaternion.identity, pink, local: true);
                foreach (float x in new[] { -3.55f, 3.55f })
                    Deco("Edge", sign, new Vector3(x, 0f, -0.02f), new Vector3(0.08f, 4.5f, 0.1f), Quaternion.identity, purple, local: true);
                Deco("Leg", sign, new Vector3(0f, -3.5f, 0.06f), new Vector3(0.3f, 2.6f, 0.12f), Quaternion.identity, metal, local: true);
                Label("VOID CASE", sign, new Vector3(0f, 1.55f, -0.03f), 0f, 0.8f, Color.white, local: true);
                Label("knives  ·  snipers  ·  gloves   ·   Void 6%", sign, new Vector3(0f, 0.95f, -0.03f), 0f, 0.26f, new Color(1f, 0.6f, 0.9f), local: true);
                Label("HOW TO EARN ONE", sign, new Vector3(0f, 0.35f, -0.03f), 0f, 0.3f, new Color(1f, 0.8f, 0.5f), local: true);
                Label("hit 4 of 10 at the skeet range\ncollect 25 Void Shards on the course",
                    sign, new Vector3(0f, -0.65f, -0.03f), 0f, 0.3f, Color.white, local: true);
                Label("open them from your inventory  ( I )", sign, new Vector3(0f, -1.75f, -0.03f), 0f, 0.24f, new Color(1f, 0.6f, 0.9f), local: true);
            }

            // "Press I for inventory": a glowing sign hung over the runway, facing you as you
            // spawn, with a keycap that bobs as if it's being pressed
            {
                var sign = new GameObject("InventorySign").transform;
                sign.SetParent(hall, false);
                sign.SetPositionAndRotation(new Vector3(lane, 7.6f, HallBack + 23f), Quaternion.identity);
                Material hot = MakeGlow("GlowInventory", new Color(1f, 0.3f, 0.75f), 1.8f);
                Material cap = MakeGlow("GlowKeycap", new Color(0.85f, 0.78f, 1f), 0.7f);
                Material capTop = MakeGlow("GlowKeycapTop", new Color(0.95f, 0.9f, 1f), 1.2f);
                Deco("SignPanel", sign, new Vector3(0f, 0f, 0.06f), new Vector3(8.2f, 2.8f, 0.12f), Quaternion.identity, dark, local: true);
                foreach (float y in new[] { -1.45f, 1.45f })
                    Deco("SignEdge", sign, new Vector3(0f, y, -0.02f), new Vector3(8.4f, 0.1f, 0.1f), Quaternion.identity, hot, local: true);
                foreach (float x in new[] { -4.15f, 4.15f })
                {
                    Deco("SignEdge", sign, new Vector3(x, 0f, -0.02f), new Vector3(0.1f, 3f, 0.1f), Quaternion.identity, purple, local: true);
                    Deco("SignCap", sign, new Vector3(x, 0f, 0.06f), new Vector3(0.3f, 3.3f, 0.3f), Quaternion.identity, metal, local: true); // gold end posts: it floats
                }
                var key = new GameObject("Keycap").transform;
                key.SetParent(sign, false);
                key.localPosition = new Vector3(-3.05f, 0f, -0.2f);
                Deco("KeycapBase", key, Vector3.zero, new Vector3(1.7f, 1.7f, 0.4f), Quaternion.identity, cap, local: true);
                Deco("KeycapTop", key, new Vector3(0f, 0.04f, -0.22f), new Vector3(1.4f, 1.4f, 0.06f), Quaternion.identity, capTop, local: true);
                Label("I", key, new Vector3(0f, 0.06f, -0.28f), 0f, 1.25f, new Color(0.18f, 0.06f, 0.3f), local: true);
                var drift = sign.gameObject.AddComponent<Floaty>();
                drift.spin = Vector3.zero;
                drift.bobHeight = 0.18f;
                drift.bobSpeed = 0.45f;
                var bob = key.gameObject.AddComponent<Floaty>();
                bob.spin = Vector3.zero;
                bob.bobHeight = 0.07f;
                bob.bobSpeed = 2.2f;
                Label("INVENTORY", sign, new Vector3(1.05f, 0.35f, -0.03f), 0f, 0.95f, Color.white, local: true);
                Label("press  I  to open", sign, new Vector3(1.05f, -0.7f, -0.03f), 0f, 0.42f, new Color(1f, 0.55f, 0.85f), local: true);
            }

            // Skeet range in the back right corner, with room to breathe: the launcher by the
            // right wall throws discs up and across the back of the hall; the start button stands
            // out on the open floor
            var range = new GameObject("SkeetRange").AddComponent<SkeetRange>();
            range.transform.SetParent(hall, false);
            var launchAt = new Vector3(right - 3f, 0f, HallBack + 6f);
            range.transform.position = launchAt;
            range.template = rampMat;
            Shape(PrimitiveType.Cylinder, "SkeetPadRing", hall, launchAt.WithY(0.03f), new Vector3(3.8f, 0.03f, 3.8f), violet);
            Box("SkeetLauncherBase", launchAt + new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 1.4f), metal, hall);
            var aim = Quaternion.LookRotation(new Vector3(-0.85f, 0f, 0.55f));
            Deco("SkeetLauncherArm", hall, launchAt + new Vector3(0f, 1.2f, 0f), new Vector3(0.35f, 0.35f, 1.3f), aim * Quaternion.Euler(-40f, 0f, 0f), dark);
            Deco("SkeetLauncherStripe", hall, launchAt + new Vector3(0f, 0.92f, 0f), new Vector3(1.45f, 0.06f, 1.45f), Quaternion.identity, magenta);
            var launcher = new GameObject("Launcher").transform;
            launcher.SetParent(range.transform, false);
            launcher.SetPositionAndRotation(launchAt + new Vector3(0f, 1.6f, 0f), aim);
            range.launcher = launcher;
            var buttonAt = new Vector3(right - 9f, 0f, HallBack + 15f);
            Shape(PrimitiveType.Cylinder, "SkeetButtonRing", hall, buttonAt.WithY(0.03f), new Vector3(2.6f, 0.03f, 2.6f), violet);
            Box("SkeetButtonPedestal", buttonAt + new Vector3(0f, 0.55f, 0f), new Vector3(1f, 1.1f, 1f), metal, hall);
            Deco("SkeetButton", hall, buttonAt + new Vector3(0f, 1.15f, 0f), new Vector3(0.55f, 0.1f, 0.55f), Quaternion.identity, magenta);
            var button = new GameObject("StartButton").transform;
            button.SetParent(range.transform, false);
            button.position = buttonAt + new Vector3(0f, 1.1f, 0f);
            range.startButton = button;
            Label("SKEET\nE to start", hall, buttonAt + new Vector3(-0.52f, 0.62f, 0f), 90f, 0.2f, new Color(0.85f, 0.55f, 1f));
            Label("SKEET RANGE", hall, new Vector3(right - 0.15f, 9f, HallBack + 9f), 90f, 1f, new Color(0.8f, 0.55f, 1f));
            Label("snipe the discs before they land  ·  hit 4 for a Void Case", hall, new Vector3(right - 0.15f, 7.8f, HallBack + 9f), 90f, 0.35f, new Color(0.8f, 0.55f, 1f));

            // Title over the opening, on a dark banner so it reads against the sky
            Deco("TitleBanner", hall, new Vector3(lane, 11.4f, HallFront - 1.1f), new Vector3(26f, 4.6f, 0.2f), Quaternion.identity, dark);
            Deco("TitleBannerEdge", hall, new Vector3(lane, 9.05f, HallFront - 1.15f), new Vector3(26f, 0.12f, 0.2f), Quaternion.identity, gold);
            Label("VOIDFLOW", hall, new Vector3(lane, 12f, HallFront - 1.3f), 0f, 3f, Color.white);
            Label("surf  /  bhop  /  knives  /  cases", hall, new Vector3(lane, 9.9f, HallFront - 1.3f), 0f, 0.7f, new Color(1f, 0.8f, 0.5f));

            // Calm floating orbs high over the court, white with a gold band
            var rng = new System.Random(20260925);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            for (int c = 0; c < FloatingShards; c++)
            {
                float x = Rand(left + 5f, right - 5f);
                if (Mathf.Abs(x - lane) < 5.5f) x += x < lane ? -6f : 6f; // keep clear of the sign's cables
                var shard = new GameObject("FloatingShard").transform;
                shard.SetParent(hall, false);
                shard.SetPositionAndRotation(new Vector3(x, Rand(18f, 26f), Rand(HallBack + 8f, HallFront - 8f)), Quaternion.Euler(Rand(-20f, 20f), Rand(0f, 360f), Rand(-20f, 20f)));
                float h = Rand(0.8f, 1.6f);
                Shape(PrimitiveType.Sphere, "Body", shard, shard.position, Vector3.one * h, ivory);
                Deco("Band", shard, shard.position, new Vector3(h * 1.08f, 0.08f, h * 1.08f), shard.rotation, c % 3 == 0 ? violet : gold);
                var f = shard.gameObject.AddComponent<Floaty>();
                f.spin = new Vector3(0f, Rand(-20f, 20f), 0f);
                f.bobHeight = Rand(0.2f, 0.45f);
                f.bobSpeed = Rand(0.4f, 0.9f);
            }

            // Moody colored light, and reflections of it all in the glossy floor
            PointLight("FrontGold", hall, new Vector3(lane, 7f, HallFront - 8f), new Color(1f, 0.86f, 0.6f), 1.4f, 30f);
            PointLight("LeftSky", hall, new Vector3(left + 8f, 8f, midZ), new Color(0.8f, 0.9f, 1f), 1.1f, 28f);
            PointLight("RightSky", hall, new Vector3(right - 8f, 8f, midZ + 6f), new Color(0.8f, 0.9f, 1f), 1.1f, 28f);
            PointLight("BackGold", hall, new Vector3(lane, 7f, HallBack + 10f), new Color(1f, 0.85f, 0.6f), 1.2f, 26f);
            PointLight("SkeetRose", hall, new Vector3(right - 8f, 5f, HallBack + 9f), new Color(1f, 0.75f, 0.6f), 0.9f, 18f);
            BuildUtopia(hall, lane, ivory, metal, gold, violet);
            var probe = new GameObject("HallReflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(hall, false);
            probe.transform.position = new Vector3(lane, 4f, midZ);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(width, HallHeight, HallDepth);
            probe.boxProjection = true;
            probe.importance = 2;
            // Low resolution and no shadows: it bakes the whole room once, and the room holds a lot
            probe.resolution = 64;
            probe.shadowDistance = 0f;
            probe.cullingMask = ~(1 << 30);

            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(hall, false);
            spawn.SetPositionAndRotation(spawnPos, Quaternion.identity);
            BoxCollider zone = Zone("StartZone", new Vector3(lane, 2f, midZ), new Vector3(width, 4f, HallDepth), hall);
            return (zone, spawn, hall);
        }

        // The view from the terrace: a distant utopia. Floating islands all round, far off and
        // below, each crowned with white spires and gold domes; soft clouds drifting beneath
        // them; two great gold halos turning slowly in the sky. Seen over the walls and out of
        // the open front, it only exists while the hall does.
        static void BuildUtopia(Transform hall, float lane, Material ivory, Material gold, Material glow, Material sky)
        {
            var rng = new System.Random(777);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var root = new GameObject("Utopia").transform;
            root.SetParent(hall, false);
            Vector3 centre = new(lane, 0f, HallFront - 30f);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f + Rand(-0.15f, 0.15f);
                float dist = Rand(380f, 820f);
                var island = new GameObject("Island").transform;
                island.SetParent(root, false);
                island.position = centre + new Vector3(Mathf.Sin(angle) * dist, Rand(-120f, 60f), Mathf.Cos(angle) * dist);
                float size = Rand(90f, 180f);
                // The island: a flattened white disc over a rounded underside
                Shape(PrimitiveType.Sphere, "Top", island, island.position, new Vector3(size, size * 0.14f, size), ivory);
                Shape(PrimitiveType.Sphere, "Underside", island, island.position + Vector3.down * size * 0.22f, new Vector3(size * 0.8f, size * 0.5f, size * 0.8f), ivory);
                // Spires, the first the tallest, each with a gold dome and a glowing needle
                int spires = rng.Next(3, 7);
                for (int k = 0; k < spires; k++)
                {
                    Vector3 at = island.position + new Vector3(Rand(-0.3f, 0.3f) * size, 0f, Rand(-0.3f, 0.3f) * size);
                    float h = Rand(0.4f, 1.3f) * size * (k == 0 ? 1.4f : 1f), w = Rand(0.06f, 0.12f) * size;
                    Shape(PrimitiveType.Cylinder, "Spire", island, at + Vector3.up * h * 0.5f, new Vector3(w, h * 0.5f, w), ivory);
                    Shape(PrimitiveType.Sphere, "Dome", island, at + Vector3.up * h, Vector3.one * w * 1.35f, gold);
                    Deco("Needle", island, at + Vector3.up * (h + w * 1.2f), new Vector3(w * 0.12f, w * 1.6f, w * 0.12f), Quaternion.identity, glow);
                }
                // Clouds drifting under it
                for (int k = 0; k < 3; k++)
                    Shape(PrimitiveType.Sphere, "Cloud", island, island.position + new Vector3(Rand(-1f, 1f) * size, -size * Rand(0.5f, 0.9f), Rand(-1f, 1f) * size),
                        new Vector3(size * Rand(0.8f, 1.6f), size * Rand(0.12f, 0.2f), size * Rand(0.6f, 1.1f)), ivory);
            }
            // Two great halos turning in the sky beyond the drop
            foreach (var (at, radius, tilt, speed) in new[] { (centre + new Vector3(-120f, 220f, 600f), 140f, 70f, 3f), (centre + new Vector3(260f, 160f, 420f), 80f, 55f, -5f) })
            {
                var halo = new GameObject("Halo").transform;
                halo.SetParent(root, false);
                halo.SetPositionAndRotation(at, Quaternion.Euler(tilt, Rand(0f, 360f), 0f));
                const int blocks = 40;
                for (int b = 0; b < blocks; b++)
                {
                    float a0 = b * Mathf.PI * 2f / blocks, a1 = (b + 1) * Mathf.PI * 2f / blocks;
                    Vector3 p0 = new(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius), p1 = new(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                    Deco("Arc", halo, (p0 + p1) * 0.5f, new Vector3(radius * 0.04f, radius * 0.02f, (p1 - p0).magnitude + 0.5f), Quaternion.LookRotation(p1 - p0), b % 5 == 0 ? sky : glow, local: true);
                }
                var turn = halo.gameObject.AddComponent<Floaty>();
                turn.spin = new Vector3(0f, speed, 0f);
                turn.bobHeight = 0f;
            }
        }

        // A gallery wall: lit ledges stepped up the wall, each item spinning over a small pad
        // glowing in its rarity color, its name on the ledge's edge. Mythics fill the lower
        // ledges and the Voids burn along the top. `start` is where the first column meets the
        // wall, `along` runs down the wall and `outward` points into the hall.
        static void GalleryWall(Transform hall, string title, Skins.Skin[] pool, ItemSlot slot, Vector3 start, Vector3 along, Vector3 outward,
            int tiers, float spacing, float depth, float step, float scale, float lift, Material rampMat, Material metal, Material dark, Material edge)
        {
            var items = new System.Collections.Generic.List<int>();
            for (int i = 1; i < pool.Length; i++) if (pool[i].rarity == SkinRarity.Mythic) items.Add(i);
            int mythics = items.Count;
            for (int i = 1; i < pool.Length; i++) if (pool[i].rarity == SkinRarity.Void) items.Add(i);
            int columns = Mathf.CeilToInt(items.Count / (float)tiers);
            float length = (columns - 1) * spacing;
            Vector3 center = start + along * (length * 0.5f);
            float yaw = Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg; // labels face into the hall
            Vector3 Size(float alongLen, float h, float outLen) => along.x != 0f ? new Vector3(alongLen, h, outLen) : new Vector3(outLen, h, alongLen);
            const float tierHeight = 1.4f, firstLedge = 0.9f;
            float top = firstLedge + tiers * tierHeight;

            // Backboard with a lit header line, and the section title above it
            Deco($"{title}Backboard", hall, center + outward * 0.04f + Vector3.up * (top * 0.5f + 0.3f), Size(length + spacing + 1f, top + 0.6f, 0.06f), Quaternion.identity, dark);
            Deco($"{title}HeaderLine", hall, center + outward * 0.09f + Vector3.up * (top + 0.65f), Size(length + spacing + 1f, 0.06f, 0.04f), Quaternion.identity, edge);
            Label(title, hall, center + outward * 0.1f + Vector3.up * (top + 1.35f), yaw, 1.1f, Color.white);
            Label($"in the Void Case   ·   {mythics} Mythic   ·   {items.Count - mythics} Void", hall, center + outward * 0.1f + Vector3.up * (top + 0.95f), yaw, 0.3f, new Color(0.8f, 0.7f, 1f));

            for (int t = 0; t < tiers; t++)
            {
                float y = firstLedge + t * tierHeight, d = depth - t * step;
                Box($"{title}Ledge{t + 1}", center + outward * (d * 0.5f) + Vector3.up * y, Size(length + spacing, 0.12f, d), metal, hall);
                Deco($"{title}LedgeEdge", hall, center + outward * (d + 0.01f) + Vector3.up * y, Size(length + spacing, 0.04f, 0.03f), Quaternion.identity, edge);
            }
            for (int n = 0; n < items.Count; n++)
            {
                int t = n / columns, c = n % columns, index = items[n];
                var skin = pool[index];
                float y = firstLedge + t * tierHeight, d = depth - t * step;
                Vector3 at = start + along * (c * spacing) + outward * (d * 0.55f);
                Color color = skin.rarity == SkinRarity.Void ? new Color(0.6f, 0.25f, 1f) : Skins.RarityColor(skin.rarity);
                Material glow = MakeGlow($"Glow{Skins.RarityName(skin.rarity)}", color, 0.8f);
                Deco("Pad", hall, at + Vector3.up * (y + 0.075f), new Vector3(0.36f, 0.02f, 0.36f), Quaternion.identity, glow);

                var display = new GameObject($"Display {skin.name}").AddComponent<SkinDisplay>();
                display.transform.SetParent(hall, false);
                display.transform.position = at + Vector3.up * (y + lift);
                display.gallery = true;
                display.shelfBelow = lift - 0.06f;
                display.maxHeight = tierHeight - 0.3f;
                display.sniper = slot == ItemSlot.Primary;
                display.glove = slot == ItemSlot.Hands;
                display.scale = scale;
                display.useRange = 1.8f;
                display.skinIndex = index;
                display.template = rampMat;

                string name = UiArtName(skin.name);
                Label($"{name}\n{Skins.RarityName(skin.rarity).ToUpper()}", hall, start + along * (c * spacing) + outward * (d + 0.04f) + Vector3.up * (y - 0.17f), yaw, 0.075f, color); // on the ledge edge, under the item
            }
        }

        static string UiArtName(string name)
        {
            int bar = name.IndexOf('|');
            return bar >= 0 ? name.Substring(bar + 1).Trim() : name;
        }

        // A decorative primitive (cylinder, sphere...) with no collider
        static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // Lights a material's glowing parts from a mask (white = glows) in `color`
        static void GlowMap(Material m, Texture2D mask, Color color)
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", mask);
            m.SetColor("_EmissionColor", color);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
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

        static void PointLight(string name, Transform parent, Vector3 position, Color color, float intensity = 2f, float range = 30f)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
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
