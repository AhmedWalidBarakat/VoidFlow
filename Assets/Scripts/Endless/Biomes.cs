using System;
using UnityEngine;

namespace VoidFlow
{
    // The looks the endless course cycles through, taken from the kinds of worlds the best
    // surf maps have. Each biome sets the sky, fog and light, the ramp colors, and which
    // scenery lines the course.
    public enum SceneryStyle { Pillars, NeonTowers, Industrial, Ice, Temple, Abyss, Inferno, Monoliths }

    public enum Surface { Grid, Stripes, Hazard, Bricks }

    public class Biome
    {
        public string name;
        public Color sky;                 // background and fog color
        public float fogStart, fogEnd;
        public Color ambientSky, ambientEquator, ambientGround;
        public Color sunColor;
        public float sunIntensity;
        public Color ramp, slab;          // prism and slab colors
        public Surface rampSurface;
        public Color glow, glowAlt;       // trims and scenery lights
        public Color scenery;             // main scenery color
        public Surface scenerySurface;
        public SceneryStyle style;

        public static readonly Biome[] All =
        {
            new Biome
            {
                name = "UTOPIA", sky = new Color(0.62f, 0.76f, 0.92f), fogStart = 150f, fogEnd = 650f,
                ambientSky = new Color(0.75f, 0.8f, 0.9f), ambientEquator = new Color(0.6f, 0.6f, 0.65f), ambientGround = new Color(0.35f, 0.33f, 0.33f),
                sunColor = new Color(1f, 0.96f, 0.9f), sunIntensity = 1.3f,
                ramp = new Color(0.9f, 0.89f, 0.93f), slab = new Color(0.62f, 0.8f, 0.95f), rampSurface = Surface.Grid,
                glow = new Color(1f, 0.33f, 0.02f), glowAlt = new Color(0.2f, 0.35f, 1f),
                scenery = Color.white, scenerySurface = Surface.Stripes, style = SceneryStyle.Pillars,
            },
            new Biome
            {
                name = "NEON CITY", sky = new Color(0.07f, 0.02f, 0.13f), fogStart = 120f, fogEnd = 600f,
                ambientSky = new Color(0.35f, 0.2f, 0.5f), ambientEquator = new Color(0.25f, 0.12f, 0.35f), ambientGround = new Color(0.1f, 0.05f, 0.15f),
                sunColor = new Color(0.9f, 0.82f, 1f), sunIntensity = 0.75f,
                ramp = new Color(1f, 0.85f, 0.1f), slab = new Color(1f, 0.25f, 0.55f), rampSurface = Surface.Grid,
                glow = new Color(0.1f, 0.9f, 1f), glowAlt = new Color(1f, 0.1f, 0.7f),
                scenery = new Color(0.06f, 0.05f, 0.1f), scenerySurface = Surface.Grid, style = SceneryStyle.NeonTowers,
            },
            new Biome
            {
                name = "INDUSTRIAL", sky = new Color(0.13f, 0.13f, 0.15f), fogStart = 100f, fogEnd = 550f,
                ambientSky = new Color(0.45f, 0.45f, 0.5f), ambientEquator = new Color(0.3f, 0.3f, 0.32f), ambientGround = new Color(0.12f, 0.12f, 0.13f),
                sunColor = new Color(0.9f, 0.9f, 1f), sunIntensity = 0.9f,
                ramp = new Color(0.32f, 0.32f, 0.35f), slab = new Color(0.8f, 0.1f, 0.1f), rampSurface = Surface.Grid,
                glow = new Color(1f, 0.08f, 0.05f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.9f, 0.9f, 0.9f), scenerySurface = Surface.Hazard, style = SceneryStyle.Industrial,
            },
            new Biome
            {
                name = "GLACIER", sky = new Color(0.8f, 0.87f, 0.94f), fogStart = 100f, fogEnd = 600f,
                ambientSky = new Color(0.85f, 0.9f, 1f), ambientEquator = new Color(0.7f, 0.75f, 0.82f), ambientGround = new Color(0.5f, 0.52f, 0.58f),
                sunColor = new Color(0.9f, 0.95f, 1f), sunIntensity = 1.1f,
                ramp = new Color(0.95f, 0.97f, 1f), slab = new Color(0.5f, 0.78f, 1f), rampSurface = Surface.Grid,
                glow = new Color(0.3f, 0.85f, 1f), glowAlt = new Color(0.8f, 0.95f, 1f),
                scenery = new Color(0.55f, 0.58f, 0.65f), scenerySurface = Surface.Grid, style = SceneryStyle.Ice,
            },
            new Biome
            {
                name = "TEMPLE", sky = new Color(0.72f, 0.52f, 0.32f), fogStart = 100f, fogEnd = 550f,
                ambientSky = new Color(0.8f, 0.65f, 0.45f), ambientEquator = new Color(0.55f, 0.42f, 0.3f), ambientGround = new Color(0.25f, 0.18f, 0.12f),
                sunColor = new Color(1f, 0.8f, 0.55f), sunIntensity = 1.2f,
                ramp = new Color(0.86f, 0.72f, 0.5f), slab = new Color(0.75f, 0.5f, 0.25f), rampSurface = Surface.Bricks,
                glow = new Color(1f, 0.55f, 0.1f), glowAlt = new Color(1f, 0.85f, 0.1f),
                scenery = new Color(0.8f, 0.66f, 0.45f), scenerySurface = Surface.Bricks, style = SceneryStyle.Temple,
            },
            new Biome
            {
                name = "ABYSS", sky = new Color(0.02f, 0.11f, 0.13f), fogStart = 50f, fogEnd = 420f,
                ambientSky = new Color(0.2f, 0.45f, 0.5f), ambientEquator = new Color(0.1f, 0.28f, 0.3f), ambientGround = new Color(0.03f, 0.1f, 0.12f),
                sunColor = new Color(0.4f, 0.9f, 1f), sunIntensity = 0.7f,
                ramp = new Color(0.35f, 0.6f, 0.62f), slab = new Color(0.25f, 0.85f, 0.9f), rampSurface = Surface.Bricks,
                glow = new Color(0.1f, 1f, 0.9f), glowAlt = new Color(0.2f, 0.5f, 1f),
                scenery = new Color(0.2f, 0.35f, 0.38f), scenerySurface = Surface.Bricks, style = SceneryStyle.Abyss,
            },
            new Biome
            {
                name = "INFERNO", sky = new Color(0.14f, 0.02f, 0.01f), fogStart = 50f, fogEnd = 420f,
                ambientSky = new Color(0.55f, 0.18f, 0.1f), ambientEquator = new Color(0.35f, 0.1f, 0.05f), ambientGround = new Color(0.15f, 0.03f, 0.01f),
                sunColor = new Color(1f, 0.4f, 0.2f), sunIntensity = 0.8f,
                ramp = new Color(0.4f, 0.16f, 0.12f), slab = new Color(0.9f, 0.3f, 0.05f), rampSurface = Surface.Bricks,
                glow = new Color(1f, 0.25f, 0f), glowAlt = new Color(1f, 0.6f, 0.05f),
                scenery = new Color(0.25f, 0.1f, 0.08f), scenerySurface = Surface.Bricks, style = SceneryStyle.Inferno,
            },
            new Biome
            {
                name = "VOID", sky = new Color(0.02f, 0.02f, 0.025f), fogStart = 60f, fogEnd = 480f,
                ambientSky = new Color(0.35f, 0.35f, 0.38f), ambientEquator = new Color(0.2f, 0.2f, 0.22f), ambientGround = new Color(0.05f, 0.05f, 0.06f),
                sunColor = Color.white, sunIntensity = 0.8f,
                ramp = new Color(0.8f, 0.8f, 0.82f), slab = new Color(0.15f, 0.15f, 0.17f), rampSurface = Surface.Grid,
                glow = new Color(1f, 1f, 1f), glowAlt = new Color(0.7f, 0.7f, 0.75f),
                scenery = new Color(0.04f, 0.04f, 0.05f), scenerySurface = Surface.Grid, style = SceneryStyle.Monoliths,
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
        public Material ramp, slab, scenery, glow, glowAlt;
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
        const float MinSideGap = 40f;   // nothing closer than this to the ramp's line, sideways
        const float MaxSideGap = 110f;
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
                    case SceneryStyle.Pillars:
                    {
                        float h = Rand(60f, 140f), w = Rand(4f, 9f);
                        Part(piece, cube, kit.scenery, new Vector3(0f, -h * 0.5f + Rand(0f, 30f), 0f), new Vector3(w, h, w));
                        if (rng.Next(3) == 0) Floater(piece, cube, rng.Next(2) == 0 ? kit.glow : kit.glowAlt, new Vector3(0f, Rand(10f, 35f), 0f), Rand(1.5f, 4f), rng);
                        break;
                    }
                    case SceneryStyle.NeonTowers:
                    {
                        float h = Rand(80f, 180f), w = Rand(6f, 14f), top = Rand(-10f, 40f);
                        Part(piece, cube, kit.scenery, new Vector3(0f, top - h * 0.5f, 0f), new Vector3(w, h, w));
                        Material stripe = rng.Next(2) == 0 ? kit.glow : kit.glowAlt;
                        int bands = rng.Next(3, 7);
                        for (int s = 0; s < bands; s++)
                            Part(piece, cube, stripe, new Vector3(0f, top - Rand(2f, h * 0.8f), 0f), new Vector3(w + 0.3f, 0.6f, w + 0.3f));
                        break;
                    }
                    case SceneryStyle.Industrial:
                    {
                        float h = Rand(70f, 150f), w = Rand(3f, 6f);
                        Part(piece, cube, kit.scenery, new Vector3(0f, Rand(-20f, 20f) - h * 0.5f, 0f), new Vector3(w, h, w));
                        // A girder reaching toward the course, high overhead
                        float reach = Rand(20f, 35f);
                        Part(piece.parent, cube, kit.scenery, basePoint + Vector3.up * Rand(28f, 40f) - path.right[i] * (side * reach * 0.5f),
                            new Vector3(reach, 1.2f, 1.2f), Quaternion.LookRotation(path.forward[i]));
                        Part(piece, cube, kit.glow, new Vector3(0f, Rand(-5f, 15f), 0f), new Vector3(w + 0.4f, 0.8f, w + 0.4f));
                        break;
                    }
                    case SceneryStyle.Ice:
                    {
                        int rocks = rng.Next(2, 4);
                        for (int r = 0; r < rocks; r++)
                            Part(piece, cube, kit.scenery, new Vector3(Rand(-8f, 8f), Rand(-50f, -10f), Rand(-8f, 8f)),
                                new Vector3(Rand(8f, 20f), Rand(10f, 30f), Rand(8f, 20f)), Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 90f), Rand(-25f, 25f)));
                        Part(piece, cube, kit.glowAlt, new Vector3(0f, Rand(-15f, 5f), 0f), new Vector3(1.2f, Rand(20f, 40f), 1.2f),
                            Quaternion.Euler(Rand(-12f, 12f), 0f, Rand(-12f, 12f)));
                        break;
                    }
                    case SceneryStyle.Temple:
                    {
                        float h = Rand(50f, 110f), w = Rand(5f, 8f), top = Rand(-5f, 25f);
                        Part(piece, cube, kit.scenery, new Vector3(0f, top - h * 0.5f, 0f), new Vector3(w, h, w));
                        Part(piece, cube, kit.scenery, new Vector3(0f, top + 1f, 0f), new Vector3(w + 2f, 2f, w + 2f));
                        Part(piece, cube, kit.glow, new Vector3(0f, top + 3f, 0f), new Vector3(1.5f, 2f, 1.5f));
                        break;
                    }
                    case SceneryStyle.Abyss:
                    {
                        Part(piece, cube, kit.scenery, new Vector3(0f, Rand(-60f, -20f), 0f), new Vector3(Rand(10f, 25f), Rand(20f, 50f), Rand(10f, 25f)));
                        // A glowing sheet of water standing on end
                        Part(piece, cube, kit.glow, new Vector3(0f, Rand(-10f, 15f), 0f), new Vector3(Rand(15f, 30f), Rand(20f, 40f), 0.3f));
                        if (rng.Next(2) == 0) Floater(piece, cube, kit.glowAlt, new Vector3(0f, Rand(15f, 30f), 0f), Rand(1f, 2.5f), rng);
                        break;
                    }
                    case SceneryStyle.Inferno:
                    {
                        float h = Rand(60f, 130f), w = Rand(5f, 10f), top = Rand(-10f, 25f);
                        Part(piece, cube, kit.scenery, new Vector3(0f, top - h * 0.5f, 0f), new Vector3(w, h, w));
                        // Lava cracks running up the column
                        Part(piece, cube, kit.glow, new Vector3(w * 0.5f, top - h * 0.4f, 0f), new Vector3(0.4f, h * 0.6f, 0.6f));
                        if (rng.Next(2) == 0) Floater(piece, cube, kit.glowAlt, new Vector3(Rand(-6f, 6f), Rand(-40f, -15f), Rand(-6f, 6f)), Rand(3f, 7f), rng);
                        break;
                    }
                    case SceneryStyle.Monoliths:
                    {
                        float h = Rand(40f, 120f), top = Rand(-10f, 35f), w = Rand(3f, 6f);
                        Part(piece, cube, kit.scenery, new Vector3(0f, top - h * 0.5f, 0f), new Vector3(w, h, Rand(10f, 20f)));
                        // A thin light strip down the face of the monolith
                        Part(piece, cube, kit.glow, new Vector3(w * 0.5f + 0.1f, top - h * 0.5f, 0f), new Vector3(0.3f, h, 0.4f));
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
