using System;
using UnityEngine;

namespace VoidFlow
{
    // The zones the endless course cycles through, each after a kind of space the most
    // beautiful surf maps are known for (all original designs): a blood-red gothic hall, a
    // white palace over the clouds, a void of neon rings, a glowing grotto, candy-colored
    // box rooms among floating blocks, an orange forge over lava, and a tunnel drawn in
    // glowing edges, and after Raphaelo a white gallery and salmon sunset rooms. They run in
    // an order whose colors flow into each other (crimson, forge orange, sunset salmon, white,
    // sky, candy, neon, wire red, grotto teal and round again). Each sets the sky, fog and
    // light, the ramp colors, the building the ramps run through (Architecture) and the
    // scenery further out.
    public enum SceneryStyle { Cathedral, Palace, Rings, Grotto, Candy, Forge, Wire, Gallery, Sunset }

    public enum Surface { Grid, Stripes, Hazard, Bricks, Tiles, Stone, Metal, Wood, Ice, Hex, Panel, Plaster, Concrete, HexTile, WhiteTile, DarkStone, Plates, Rock }

    public class Biome
    {
        public string name;
        public Color sky;                 // horizon and fog color
        public Color skyTop, skyBottom;   // sky dome above and below the horizon
        public float fogStart, fogEnd;
        public Color ambientSky, ambientEquator, ambientGround;
        public Color sunColor;
        public float sunIntensity;
        public Color ramp, slab;          // prism and slab colors
        public Surface rampSurface;
        public Color glow, glowAlt;       // trims and scenery lights
        public Color scenery;             // main scenery color
        public Surface scenerySurface;
        public Color floor;               // the building's floor (clear: the slab color)
        public Surface floorSurface;
        public SceneryStyle style;

        public static readonly Biome[] All =
        {
            new Biome
            {
                name = "CRIMSON HALL", sky = new Color(0.3f, 0.02f, 0.04f), skyTop = new Color(0.05f, 0f, 0.01f), skyBottom = new Color(0.08f, 0f, 0.01f),
                fogStart = 50f, fogEnd = 420f,
                ambientSky = new Color(0.6f, 0.12f, 0.14f), ambientEquator = new Color(0.35f, 0.05f, 0.07f), ambientGround = new Color(0.1f, 0.01f, 0.02f),
                sunColor = new Color(1f, 0.4f, 0.4f), sunIntensity = 0.75f,
                ramp = new Color(0.07f, 0.06f, 0.07f), slab = new Color(0.22f, 0.03f, 0.05f), rampSurface = Surface.Metal,
                glow = new Color(1f, 0.08f, 0.12f), glowAlt = new Color(0.75f, 0.03f, 0.1f),
                scenery = new Color(0.4f, 0.04f, 0.06f), scenerySurface = Surface.DarkStone, style = SceneryStyle.Cathedral,
                floor = new Color(0.16f, 0.1f, 0.11f),
                floorSurface = Surface.Concrete,
            },
            new Biome
            {
                name = "FORGE", sky = new Color(0.45f, 0.15f, 0.03f), skyTop = new Color(0.08f, 0.02f, 0f), skyBottom = new Color(0.15f, 0.04f, 0f),
                fogStart = 70f, fogEnd = 500f,
                ambientSky = new Color(0.72f, 0.38f, 0.16f), ambientEquator = new Color(0.45f, 0.2f, 0.08f), ambientGround = new Color(0.15f, 0.05f, 0.02f),
                sunColor = new Color(1f, 0.6f, 0.3f), sunIntensity = 0.9f,
                ramp = new Color(0.18f, 0.16f, 0.15f), slab = new Color(0.9f, 0.45f, 0.1f), rampSurface = Surface.Metal,
                glow = new Color(1f, 0.45f, 0.05f), glowAlt = new Color(1f, 0.85f, 0.3f),
                scenery = new Color(0.75f, 0.35f, 0.1f), scenerySurface = Surface.Plates, style = SceneryStyle.Forge,
                floor = new Color(0.2f, 0.12f, 0.08f),
                floorSurface = Surface.Plates,
            },
            new Biome
            {
                name = "SUNSET ROOMS", sky = new Color(0.95f, 0.55f, 0.4f), skyTop = new Color(0.28f, 0.2f, 0.45f), skyBottom = new Color(0.35f, 0.16f, 0.12f),
                fogStart = 120f, fogEnd = 700f,
                ambientSky = new Color(0.95f, 0.72f, 0.66f), ambientEquator = new Color(0.7f, 0.46f, 0.42f), ambientGround = new Color(0.22f, 0.14f, 0.14f),
                sunColor = new Color(1f, 0.72f, 0.5f), sunIntensity = 1.1f,
                ramp = new Color(0.52f, 0.5f, 0.52f), slab = new Color(0.2f, 0.17f, 0.2f), rampSurface = Surface.Concrete,
                glow = new Color(1f, 0.5f, 0.18f), glowAlt = new Color(1f, 0.68f, 0.52f),
                scenery = new Color(0.86f, 0.56f, 0.5f), scenerySurface = Surface.Plaster, style = SceneryStyle.Sunset,
                floor = new Color(0.12f, 0.11f, 0.13f), floorSurface = Surface.HexTile,
            },
            new Biome
            {
                name = "WHITE GALLERY", sky = new Color(0.82f, 0.9f, 1f), skyTop = new Color(0.35f, 0.6f, 0.95f), skyBottom = new Color(0.9f, 0.93f, 0.98f),
                fogStart = 160f, fogEnd = 800f,
                ambientSky = new Color(0.95f, 0.97f, 1f), ambientEquator = new Color(0.82f, 0.85f, 0.9f), ambientGround = new Color(0.62f, 0.64f, 0.7f),
                sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.25f,
                ramp = new Color(0.5f, 0.51f, 0.54f), slab = new Color(0.62f, 0.63f, 0.67f), rampSurface = Surface.Concrete,
                glow = new Color(1f, 0.62f, 0.3f), glowAlt = new Color(0.45f, 0.78f, 1f),
                scenery = new Color(0.94f, 0.94f, 0.95f), scenerySurface = Surface.Plaster, style = SceneryStyle.Gallery,
                floor = new Color(0.84f, 0.85f, 0.87f), floorSurface = Surface.WhiteTile,
            },
            new Biome
            {
                name = "SKY PALACE", sky = new Color(0.78f, 0.87f, 0.98f), skyTop = new Color(0.3f, 0.55f, 0.92f), skyBottom = new Color(0.95f, 0.96f, 0.99f),
                fogStart = 220f, fogEnd = 950f,
                ambientSky = new Color(0.95f, 0.97f, 1f), ambientEquator = new Color(0.85f, 0.88f, 0.95f), ambientGround = new Color(0.7f, 0.72f, 0.78f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.3f,
                ramp = new Color(0.11f, 0.11f, 0.15f), slab = new Color(0.92f, 0.92f, 0.95f), rampSurface = Surface.Metal,
                glow = new Color(1f, 0.75f, 0.3f), glowAlt = new Color(0.4f, 0.72f, 1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.Grid, style = SceneryStyle.Palace,
            },
            new Biome
            {
                name = "CANDY BLOCKS", sky = new Color(0.06f, 0.03f, 0.12f), skyTop = new Color(0f, 0f, 0.02f), skyBottom = new Color(0.02f, 0.01f, 0.05f),
                fogStart = 160f, fogEnd = 820f,
                ambientSky = new Color(0.85f, 0.8f, 0.95f), ambientEquator = new Color(0.62f, 0.58f, 0.72f), ambientGround = new Color(0.3f, 0.25f, 0.4f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.2f,
                ramp = new Color(1f, 0.35f, 0.75f), slab = new Color(0.25f, 0.9f, 1f), rampSurface = Surface.Tiles,
                glow = new Color(1f, 0.85f, 0.2f), glowAlt = new Color(0.2f, 1f, 0.95f),
                scenery = new Color(0.78f, 0.7f, 0.95f), scenerySurface = Surface.Plaster, style = SceneryStyle.Candy,
            },
            new Biome
            {
                name = "NEON RINGS", sky = new Color(0.08f, 0.02f, 0.12f), skyTop = new Color(0f, 0f, 0.01f), skyBottom = new Color(0.02f, 0f, 0.04f),
                fogStart = 90f, fogEnd = 560f,
                ambientSky = new Color(0.4f, 0.22f, 0.55f), ambientEquator = new Color(0.22f, 0.1f, 0.32f), ambientGround = new Color(0.05f, 0.02f, 0.08f),
                sunColor = new Color(0.9f, 0.7f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.1f, 0.08f, 0.14f), slab = new Color(0.2f, 0.06f, 0.28f), rampSurface = Surface.Grid,
                glow = new Color(1f, 0.25f, 0.85f), glowAlt = new Color(0.15f, 0.95f, 1f),
                scenery = new Color(0.05f, 0.04f, 0.08f), scenerySurface = Surface.Metal, style = SceneryStyle.Rings,
            },
            new Biome
            {
                name = "WIREFRAME", sky = new Color(0.04f, 0.02f, 0.02f), skyTop = new Color(0f, 0f, 0f), skyBottom = new Color(0.01f, 0f, 0f),
                fogStart = 60f, fogEnd = 500f,
                ambientSky = new Color(0.38f, 0.3f, 0.3f), ambientEquator = new Color(0.2f, 0.15f, 0.15f), ambientGround = new Color(0.04f, 0.03f, 0.03f),
                sunColor = new Color(1f, 0.5f, 0.4f), sunIntensity = 0.6f,
                ramp = new Color(0.06f, 0.06f, 0.07f), slab = new Color(0.18f, 0.03f, 0.03f), rampSurface = Surface.Grid,
                glow = new Color(1f, 0.12f, 0.08f), glowAlt = new Color(1f, 0.5f, 0.1f),
                scenery = new Color(0.03f, 0.03f, 0.035f), scenerySurface = Surface.Metal, style = SceneryStyle.Wire,
            },
            new Biome
            {
                name = "GROTTO", sky = new Color(0.02f, 0.14f, 0.16f), skyTop = new Color(0f, 0.02f, 0.03f), skyBottom = new Color(0f, 0.05f, 0.06f),
                fogStart = 50f, fogEnd = 420f,
                ambientSky = new Color(0.22f, 0.5f, 0.55f), ambientEquator = new Color(0.1f, 0.26f, 0.3f), ambientGround = new Color(0.02f, 0.08f, 0.1f),
                sunColor = new Color(0.6f, 0.9f, 1f), sunIntensity = 0.55f,
                ramp = new Color(0.26f, 0.22f, 0.42f), slab = new Color(0.15f, 0.3f, 0.35f), rampSurface = Surface.Stone,
                glow = new Color(0.1f, 1f, 0.8f), glowAlt = new Color(0.7f, 0.3f, 1f),
                scenery = new Color(0.15f, 0.14f, 0.19f), scenerySurface = Surface.Rock, style = SceneryStyle.Grotto,
                floor = new Color(0.2f, 0.25f, 0.3f),
                floorSurface = Surface.Rock,
            },
        };
    }

    // One set of shared materials per biome. Every ramp and scenery piece in the biome uses
    // these, so nothing is created while you play. They're made ahead of time by the scene
    // builder and saved as assets (materials made at runtime confused Unity's render
    // batching, drawing ramps with the scenery's texture).
    [Serializable]
    public class BiomeKit
    {
        public Material ramp, slab, scenery, glow, glowAlt, floor;
        public Material trim => glow;

        public static Material Surface(Material template, Color color, Texture texture, Vector2 tiling)
        {
            var m = new Material(template);
            m.SetTexture("_BaseMap", texture);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetColor("_BaseColor", color);
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.enableInstancing = true;
            return m;
        }

        public static Material Glow(Material template, Color color)
        {
            var m = new Material(template);
            m.SetColor("_BaseColor", color * 0.25f); // let the glow carry the color
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 1.2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.enableInstancing = true;
            return m;
        }
    }

    // Builds the scenery that lines one ramp: shapes out to the sides, well clear of the
    // ramp and of any flight path, made from a shared cube mesh with no colliders and no
    // shadows, so it costs little to draw. Each ramp gets a fixed budget of pieces.
    public static class Scenery
    {
        const float MinSideGap = 70f;   // nothing closer than this to the ramp's line, sideways (outside the buildings)
        const float MaxSideGap = 150f;
        const int PiecesPerRamp = 12;

        public static void Line(RampShapes.RampPath path, Biome biome, BiomeKit kit, Transform parent, System.Random rng, Mesh cube)
        {
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            for (int p = 0; p < PiecesPerRamp; p++)
            {
                int i = rng.Next(path.ridge.Count);
                float side = rng.Next(2) == 0 ? -1f : 1f;
                Vector3 basePoint = path.ridge[i] + path.right[i] * (side * Rand(MinSideGap, MaxSideGap));
                var piece = new GameObject("Scenery").transform;
                piece.SetParent(parent, false);
                piece.localPosition = basePoint;
                piece.localRotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);

                switch (biome.style)
                {
                    case SceneryStyle.Cathedral:
                    {
                        // Dark spires with a red glowing slit
                        float h = Rand(60f, 140f), w = Rand(5f, 10f), top = Rand(0f, 40f);
                        Part(piece, cube, kit.scenery, new Vector3(0f, top - h * 0.5f, 0f), new Vector3(w, h, w));
                        Part(piece, cube, kit.scenery, new Vector3(0f, top + w * 0.5f, 0f), new Vector3(w * 0.7f, w * 0.7f, w * 0.7f), Quaternion.Euler(45f, 0f, 45f));
                        Part(piece, cube, kit.glow, new Vector3(w * 0.5f + 0.1f, top - h * 0.3f, 0f), new Vector3(0.3f, h * 0.3f, 1.2f));
                        break;
                    }
                    case SceneryStyle.Palace:
                    {
                        // Clouds drifting, and now and then a white tower
                        Part(piece, cube, kit.scenery, new Vector3(0f, Rand(-70f, -30f), 0f), new Vector3(Rand(40f, 90f), Rand(6f, 12f), Rand(30f, 70f)));
                        if (rng.Next(3) == 0) Part(piece, cube, kit.scenery, new Vector3(0f, Rand(-40f, 0f), 0f), new Vector3(Rand(6f, 10f), Rand(60f, 120f), Rand(6f, 10f)));
                        if (rng.Next(3) == 0) Floater(piece, cube, kit.scenery, new Vector3(0f, Rand(10f, 35f), 0f), Rand(2f, 5f), rng);
                        break;
                    }
                    case SceneryStyle.Rings:
                    {
                        // Black monoliths edged in cyan, glowing cubes drifting
                        float h = Rand(40f, 110f), top = Rand(-10f, 30f), w = Rand(3f, 6f);
                        Part(piece, cube, kit.scenery, new Vector3(0f, top - h * 0.5f, 0f), new Vector3(w, h, Rand(8f, 16f)));
                        Part(piece, cube, kit.glowAlt, new Vector3(w * 0.5f + 0.1f, top - h * 0.5f, 0f), new Vector3(0.3f, h, 0.4f));
                        if (rng.Next(2) == 0) Floater(piece, cube, kit.glow, new Vector3(0f, Rand(10f, 30f), 0f), Rand(1.5f, 3.5f), rng);
                        break;
                    }
                    case SceneryStyle.Grotto:
                    {
                        // Stalagmites with glowing crystals
                        Part(piece, cube, kit.scenery, new Vector3(0f, Rand(-60f, -20f), 0f), new Vector3(Rand(10f, 22f), Rand(30f, 60f), Rand(10f, 22f)), Quaternion.Euler(Rand(-10f, 10f), Rand(0f, 90f), Rand(-10f, 10f)));
                        Part(piece, cube, rng.Next(2) == 0 ? kit.glow : kit.glowAlt, new Vector3(0f, Rand(-15f, 5f), 0f), new Vector3(1.4f, Rand(10f, 22f), 1.4f), Quaternion.Euler(Rand(-20f, 20f), 45f, Rand(-20f, 20f)));
                        break;
                    }
                    case SceneryStyle.Candy:
                    {
                        // Big floating blocks in flat saturated colors
                        var mats = new[] { kit.ramp, kit.slab, kit.glow, kit.glowAlt, kit.scenery };
                        Part(piece, cube, mats[rng.Next(mats.Length)], new Vector3(0f, Rand(-50f, 30f), 0f), new Vector3(Rand(8f, 30f), Rand(8f, 40f), Rand(8f, 30f)));
                        if (rng.Next(3) == 0) Floater(piece, cube, mats[rng.Next(mats.Length)], new Vector3(0f, Rand(15f, 40f), 0f), Rand(3f, 7f), rng);
                        break;
                    }
                    case SceneryStyle.Forge:
                    {
                        // Chimneys glowing at the top
                        float h = Rand(60f, 130f), w = Rand(5f, 9f), top = Rand(-10f, 25f);
                        Part(piece, cube, kit.slab, new Vector3(0f, top - h * 0.5f, 0f), new Vector3(w, h, w));
                        Part(piece, cube, kit.glow, new Vector3(0f, top + 0.5f, 0f), new Vector3(w - 1f, 1f, w - 1f));
                        break;
                    }
                    case SceneryStyle.Gallery:
                    case SceneryStyle.Sunset:
                    {
                        // Seen through the windows: pale towers and clouds (gallery), or
                        // salmon blocks against the sunset
                        Part(piece, cube, kit.scenery, new Vector3(0f, Rand(-60f, -20f), 0f), new Vector3(Rand(20f, 50f), Rand(40f, 90f), Rand(20f, 50f)));
                        if (rng.Next(2) == 0) Part(piece, cube, kit.scenery, new Vector3(Rand(-20f, 20f), Rand(-80f, -50f), Rand(-20f, 20f)), new Vector3(Rand(50f, 90f), Rand(6f, 12f), Rand(40f, 80f)));
                        break;
                    }
                    case SceneryStyle.Wire:
                    {
                        // Wireframe cubes hanging in the dark
                        float size = Rand(8f, 20f), y = Rand(-40f, 30f);
                        Material m = rng.Next(2) == 0 ? kit.glow : kit.glowAlt;
                        for (int e = 0; e < 12; e++)
                        {
                            int axis = e / 4, n = e % 4;
                            float u = (n & 1) == 0 ? -0.5f : 0.5f, v = (n & 2) == 0 ? -0.5f : 0.5f;
                            Vector3 at = axis == 0 ? new Vector3(0f, u, v) : axis == 1 ? new Vector3(u, 0f, v) : new Vector3(u, v, 0f);
                            Vector3 sz = axis == 0 ? new Vector3(1f, 0.02f, 0.02f) : axis == 1 ? new Vector3(0.02f, 1f, 0.02f) : new Vector3(0.02f, 0.02f, 1f);
                            Part(piece, cube, m, new Vector3(0f, y, 0f) + at * size, sz * size + Vector3.one * 0.3f);
                        }
                        break;
                    }
                }
            }
        }

        static void Part(Transform parent, Mesh cube, Material mat, Vector3 localPosition, Vector3 scale) =>
            Part(parent, cube, mat, localPosition, scale, Quaternion.identity);

        static void Part(Transform parent, Mesh cube, Material mat, Vector3 localPosition, Vector3 scale, Quaternion rotation)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = cube;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // A glowing cube that drifts and spins
        static void Floater(Transform parent, Mesh cube, Material mat, Vector3 localPosition, float size, System.Random rng)
        {
            Part(parent, cube, mat, localPosition, Vector3.one * size, Quaternion.Euler(rng.Next(360), rng.Next(360), rng.Next(360)));
            var f = parent.GetChild(parent.childCount - 1).gameObject.AddComponent<Floaty>();
            f.spin = new Vector3(rng.Next(-40, 40), rng.Next(-40, 40), rng.Next(-40, 40));
            f.bobHeight = 1f;
            f.bobSpeed = 0.5f;
        }
    }
}
