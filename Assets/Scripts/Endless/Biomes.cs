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
    public enum SceneryStyle { Cathedral, Palace, Rings, Grotto, Candy, Forge, Wire, Gallery, Sunset, Library, Spectrum,
        Alpine, Canyon, Glass, Garden, Lab, Ember, Amethyst, Toy, Mine, Synth, Temple }

    public enum Surface { Grid, Stripes, Hazard, Bricks, Tiles, Stone, Metal, Wood, Ice, Hex, Panel, Plaster, Concrete, HexTile, WhiteTile, DarkStone, Plates, Rock, Blocks, Books,
        // Each zone's own ramp design, and wall patterns (8m tiles)
        RampCrimson, RampForge, RampSunset, RampGallery, RampPalace, RampCandy, RampNeon, RampWire, RampGrotto, RampLibrary,
        WallTracery, WallGrate, WallPanels, WallBlocks, WallHexVents, WallWood,
        RampSpectrum, WallGrid, RampCelestial,
        RampSnow, RampWhiteGrid, RampGlass, RampBrick, RampHex, RampEmber, RampAmethyst, RampToy, RampMine, RampSynth, RampSandstone,
        WallHedge, WallLab, WallSandstone,
        // The finale's zones
        RampOmnific, RampCastle, RampHell, RampTorii, RampTomb, RampDeity, RampCorrupt, RampPatchwork, RampRuins, RampPro,
        WallCastle, WallCopper,
        // The real maps' own looks
        RampUtopia, WallUtopia, RampMesa, RampFunhouse, RampDevGrid,
        // The Legend zones
        RampLove, RampCornfield, RampNeonShapes, RampGlacier, RampRedLine, RampBunker, RampCyanCrystal, RampChromeWave, RampStreak, RampGreatWall, RampArcade, RampHazard, RampHexJungle, RampIndustrial, RampQuarry, RampMauve, RampSurfSchool, RampVapor, RampFlags, RampWarehouse, RampSeaMine, RampMoon, RampStripe, RampRaceTrack, RampFruit, RampTown, RampHills }

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
        public Surface slabSurface = Surface.Concrete; // trims and slabs
        public Color glow, glowAlt;       // trims and scenery lights
        public Color scenery;             // main scenery color
        public Surface scenerySurface;
        public Color floor;               // the building's floor (clear: the slab color)
        public Surface floorSurface;
        public Color accent;              // raised wall panels (clear: the slab color)
        public Surface accentSurface;
        public Color shaft;               // light falling in through windows (clear: glowAlt)
        public SceneryStyle style;
        public Color[] hues;              // Spectrum: each ramp in the zone takes the next of these
        // The real maps: their ramps laid out by hand (six per stage) after the map's run, its
        // difficulty tier as players know it, the part of the map this stage is, whether it
        // carries straight on from the stage before (same look, same materials), and whether its
        // start has no checkpoint (a map run in one go, like mesa)
        public RampDef[] script;
        public string tier;
        public string section;
        public bool continues;
        public bool noCheckpoint;

        // The start terrace's sky: bright and celestial, deep blue overhead, warm gold at the
        // horizon, a long clear view over the distant utopia (not a course zone)
        public static readonly Biome Terrace = new()
        {
            name = "CELESTIAL TERRACE", sky = new Color(1f, 0.92f, 0.78f), skyTop = new Color(0.3f, 0.48f, 0.9f), skyBottom = new Color(0.96f, 0.93f, 0.88f),
            fogStart = 350f, fogEnd = 2400f,
            ambientSky = new Color(0.92f, 0.94f, 1f), ambientEquator = new Color(0.9f, 0.84f, 0.74f), ambientGround = new Color(0.55f, 0.5f, 0.45f),
            sunColor = new Color(1f, 0.95f, 0.85f), sunIntensity = 1.3f,
            // The first two ramps, under this sky: white marble lined in gold
            ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampCelestial, slab = new Color(0.95f, 0.93f, 0.88f), slabSurface = Surface.Plaster,
            glow = new Color(1f, 0.78f, 0.4f), glowAlt = new Color(0.6f, 0.8f, 1f),
            scenery = new Color(0.95f, 0.94f, 0.9f), scenerySurface = Surface.Plaster,
        };

        public static readonly Biome[] All =
        {
            new Biome
            {
                name = "CRIMSON HALL", sky = new Color(0.3f, 0.02f, 0.04f), skyTop = new Color(0.05f, 0f, 0.01f), skyBottom = new Color(0.08f, 0f, 0.01f),
                fogStart = 50f, fogEnd = 420f,
                ambientSky = new Color(0.6f, 0.12f, 0.14f), ambientEquator = new Color(0.35f, 0.05f, 0.07f), ambientGround = new Color(0.1f, 0.01f, 0.02f),
                sunColor = new Color(1f, 0.4f, 0.4f), sunIntensity = 0.75f,
                ramp = new Color(0.2f, 0.17f, 0.18f), slab = new Color(0.22f, 0.03f, 0.05f), rampSurface = Surface.RampCrimson,
                glow = new Color(1f, 0.08f, 0.12f), glowAlt = new Color(0.75f, 0.03f, 0.1f),
                scenery = new Color(0.62f, 0.14f, 0.16f), scenerySurface = Surface.WallTracery, style = SceneryStyle.Cathedral,
                floor = new Color(0.16f, 0.1f, 0.11f),
                floorSurface = Surface.Concrete,
                accent = new Color(0.3f, 0.07f, 0.08f),
                accentSurface = Surface.DarkStone,
                shaft = new Color(1f, 0.25f, 0.28f),
            },
            new Biome
            {
                name = "FORGE", sky = new Color(0.45f, 0.15f, 0.03f), skyTop = new Color(0.08f, 0.02f, 0f), skyBottom = new Color(0.15f, 0.04f, 0f),
                fogStart = 70f, fogEnd = 500f,
                ambientSky = new Color(0.72f, 0.38f, 0.16f), ambientEquator = new Color(0.45f, 0.2f, 0.08f), ambientGround = new Color(0.15f, 0.05f, 0.02f),
                sunColor = new Color(1f, 0.6f, 0.3f), sunIntensity = 0.9f,
                ramp = new Color(0.45f, 0.4f, 0.38f), slab = new Color(0.9f, 0.45f, 0.1f), rampSurface = Surface.RampForge,
                glow = new Color(1f, 0.45f, 0.05f), glowAlt = new Color(1f, 0.85f, 0.3f),
                scenery = new Color(0.9f, 0.5f, 0.22f), scenerySurface = Surface.Plates, style = SceneryStyle.Forge,
                floor = new Color(0.2f, 0.12f, 0.08f),
                floorSurface = Surface.Plates,
                accent = new Color(0.5f, 0.28f, 0.14f),
                accentSurface = Surface.WallGrate,
                shaft = new Color(1f, 0.6f, 0.25f),
                slabSurface = Surface.Plates,
            },
            new Biome
            {
                name = "SUNSET ROOMS", sky = new Color(0.95f, 0.55f, 0.4f), skyTop = new Color(0.28f, 0.2f, 0.45f), skyBottom = new Color(0.35f, 0.16f, 0.12f),
                fogStart = 120f, fogEnd = 700f,
                ambientSky = new Color(0.95f, 0.72f, 0.66f), ambientEquator = new Color(0.7f, 0.46f, 0.42f), ambientGround = new Color(0.22f, 0.14f, 0.14f),
                sunColor = new Color(1f, 0.72f, 0.5f), sunIntensity = 1.1f,
                ramp = new Color(0.52f, 0.5f, 0.52f), slab = new Color(0.2f, 0.17f, 0.2f), rampSurface = Surface.RampSunset,
                glow = new Color(1f, 0.5f, 0.18f), glowAlt = new Color(1f, 0.68f, 0.52f),
                scenery = new Color(0.86f, 0.56f, 0.5f), scenerySurface = Surface.Plaster, style = SceneryStyle.Sunset,
                floor = new Color(0.12f, 0.11f, 0.13f), floorSurface = Surface.HexTile,
                accent = new Color(1f, 1f, 1f),
                accentSurface = Surface.Blocks,
                shaft = new Color(1f, 0.72f, 0.45f),
            },
            new Biome
            {
                name = "WHITE GALLERY", sky = new Color(0.82f, 0.9f, 1f), skyTop = new Color(0.35f, 0.6f, 0.95f), skyBottom = new Color(0.9f, 0.93f, 0.98f),
                fogStart = 160f, fogEnd = 800f,
                ambientSky = new Color(0.95f, 0.97f, 1f), ambientEquator = new Color(0.82f, 0.85f, 0.9f), ambientGround = new Color(0.62f, 0.64f, 0.7f),
                sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.25f,
                ramp = new Color(0.5f, 0.51f, 0.54f), slab = new Color(0.62f, 0.63f, 0.67f), rampSurface = Surface.RampGallery,
                glow = new Color(1f, 0.62f, 0.3f), glowAlt = new Color(0.45f, 0.78f, 1f),
                scenery = new Color(0.94f, 0.94f, 0.95f), scenerySurface = Surface.Plaster, style = SceneryStyle.Gallery,
                floor = new Color(0.84f, 0.85f, 0.87f), floorSurface = Surface.WhiteTile,
                accent = new Color(0.97f, 0.97f, 0.98f),
                accentSurface = Surface.WallPanels,
                shaft = new Color(0.82f, 0.92f, 1f),
            },
            new Biome
            {
                name = "SKY PALACE", sky = new Color(0.78f, 0.87f, 0.98f), skyTop = new Color(0.3f, 0.55f, 0.92f), skyBottom = new Color(0.95f, 0.96f, 0.99f),
                fogStart = 220f, fogEnd = 950f,
                ambientSky = new Color(0.95f, 0.97f, 1f), ambientEquator = new Color(0.85f, 0.88f, 0.95f), ambientGround = new Color(0.7f, 0.72f, 0.78f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.3f,
                ramp = new Color(0.11f, 0.11f, 0.15f), slab = new Color(0.92f, 0.92f, 0.95f), rampSurface = Surface.RampPalace,
                glow = new Color(1f, 0.75f, 0.3f), glowAlt = new Color(0.4f, 0.72f, 1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.Grid, style = SceneryStyle.Palace,
                slabSurface = Surface.Metal,
            },
            new Biome
            {
                name = "CANDY BLOCKS", sky = new Color(0.06f, 0.03f, 0.12f), skyTop = new Color(0f, 0f, 0.02f), skyBottom = new Color(0.02f, 0.01f, 0.05f),
                fogStart = 160f, fogEnd = 820f,
                ambientSky = new Color(0.85f, 0.8f, 0.95f), ambientEquator = new Color(0.62f, 0.58f, 0.72f), ambientGround = new Color(0.3f, 0.25f, 0.4f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.2f,
                ramp = new Color(1f, 0.35f, 0.75f), slab = new Color(0.3f, 0.95f, 1f), rampSurface = Surface.RampCandy,
                glow = new Color(1f, 0.85f, 0.2f), glowAlt = new Color(0.2f, 1f, 0.95f),
                scenery = new Color(0.78f, 0.7f, 0.95f), scenerySurface = Surface.WallBlocks, style = SceneryStyle.Candy,
                accent = new Color(0.62f, 0.55f, 0.85f),
                accentSurface = Surface.Plaster,
                shaft = new Color(0.45f, 1f, 0.95f),
                slabSurface = Surface.Plaster,
            },
            new Biome
            {
                name = "NEON RINGS", sky = new Color(0.08f, 0.02f, 0.12f), skyTop = new Color(0f, 0f, 0.01f), skyBottom = new Color(0.02f, 0f, 0.04f),
                fogStart = 90f, fogEnd = 560f,
                ambientSky = new Color(0.4f, 0.22f, 0.55f), ambientEquator = new Color(0.22f, 0.1f, 0.32f), ambientGround = new Color(0.05f, 0.02f, 0.08f),
                sunColor = new Color(0.9f, 0.7f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.3f, 0.24f, 0.4f), slab = new Color(0.2f, 0.06f, 0.28f), rampSurface = Surface.RampNeon,
                glow = new Color(1f, 0.25f, 0.85f), glowAlt = new Color(0.15f, 0.95f, 1f),
                scenery = new Color(0.16f, 0.12f, 0.22f), scenerySurface = Surface.WallHexVents, style = SceneryStyle.Rings,
                slabSurface = Surface.Plates,
            },
            new Biome
            {
                name = "WIREFRAME", sky = new Color(0.04f, 0.02f, 0.02f), skyTop = new Color(0f, 0f, 0f), skyBottom = new Color(0.01f, 0f, 0f),
                fogStart = 60f, fogEnd = 500f,
                ambientSky = new Color(0.38f, 0.3f, 0.3f), ambientEquator = new Color(0.2f, 0.15f, 0.15f), ambientGround = new Color(0.04f, 0.03f, 0.03f),
                sunColor = new Color(1f, 0.5f, 0.4f), sunIntensity = 0.6f,
                ramp = new Color(0.18f, 0.16f, 0.17f), slab = new Color(0.18f, 0.03f, 0.03f), rampSurface = Surface.RampWire,
                glow = new Color(1f, 0.12f, 0.08f), glowAlt = new Color(1f, 0.5f, 0.1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Wire,
                floor = new Color(1f, 1f, 1f), floorSurface = Surface.WallGrid,
            },
            new Biome
            {
                name = "GROTTO", sky = new Color(0.02f, 0.14f, 0.16f), skyTop = new Color(0f, 0.02f, 0.03f), skyBottom = new Color(0f, 0.05f, 0.06f),
                fogStart = 50f, fogEnd = 420f,
                ambientSky = new Color(0.22f, 0.5f, 0.55f), ambientEquator = new Color(0.1f, 0.26f, 0.3f), ambientGround = new Color(0.02f, 0.08f, 0.1f),
                sunColor = new Color(0.6f, 0.9f, 1f), sunIntensity = 0.55f,
                ramp = new Color(0.45f, 0.4f, 0.6f), slab = new Color(0.15f, 0.3f, 0.35f), rampSurface = Surface.RampGrotto,
                glow = new Color(0.1f, 1f, 0.8f), glowAlt = new Color(0.7f, 0.3f, 1f),
                scenery = new Color(0.35f, 0.42f, 0.5f), scenerySurface = Surface.Rock, style = SceneryStyle.Grotto,
                floor = new Color(0.2f, 0.25f, 0.3f),
                floorSurface = Surface.Rock,
                shaft = new Color(0.35f, 1f, 0.85f),
                slabSurface = Surface.Rock,
            },
            new Biome
            {
                name = "LIBRARY", sky = new Color(0.1f, 0.08f, 0.16f), skyTop = new Color(0.02f, 0.02f, 0.06f), skyBottom = new Color(0.06f, 0.04f, 0.08f),
                fogStart = 90f, fogEnd = 600f,
                ambientSky = new Color(0.7f, 0.58f, 0.42f), ambientEquator = new Color(0.45f, 0.34f, 0.24f), ambientGround = new Color(0.16f, 0.12f, 0.1f),
                sunColor = new Color(1f, 0.85f, 0.6f), sunIntensity = 0.9f,
                ramp = new Color(0.55f, 0.5f, 0.45f), slab = new Color(0.62f, 0.46f, 0.22f), rampSurface = Surface.RampLibrary,
                glow = new Color(1f, 0.75f, 0.35f), glowAlt = new Color(0.3f, 0.6f, 1f),
                scenery = new Color(0.5f, 0.36f, 0.24f), scenerySurface = Surface.WallWood, style = SceneryStyle.Library,
                floor = new Color(0.42f, 0.36f, 0.3f), floorSurface = Surface.WhiteTile,
                accent = new Color(1f, 1f, 1f), accentSurface = Surface.Books,
                shaft = new Color(1f, 0.82f, 0.55f),
            },
            new Biome
            {
                name = "SPECTRUM", sky = new Color(0.01f, 0.01f, 0.015f), skyTop = new Color(0f, 0f, 0f), skyBottom = new Color(0f, 0f, 0.005f),
                fogStart = 250f, fogEnd = 1400f,
                ambientSky = new Color(0.22f, 0.22f, 0.26f), ambientEquator = new Color(0.12f, 0.12f, 0.15f), ambientGround = new Color(0.03f, 0.03f, 0.04f),
                sunColor = new Color(0.8f, 0.82f, 0.9f), sunIntensity = 0.35f,
                ramp = new Color(1f, 1f, 1f), slab = new Color(0.08f, 0.08f, 0.1f), rampSurface = Surface.RampSpectrum, slabSurface = Surface.Metal,
                glow = new Color(1f, 1f, 1f), glowAlt = new Color(0.85f, 0.9f, 1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Spectrum,
                shaft = new Color(0.85f, 0.9f, 1f),
                // Red, orange, yellow, green, blue, violet: one per ramp through the zone
                hues = new[] { new Color(1f, 0.1f, 0.12f), new Color(1f, 0.45f, 0.05f), new Color(1f, 0.9f, 0.1f),
                    new Color(0.2f, 1f, 0.3f), new Color(0.15f, 0.55f, 1f), new Color(0.65f, 0.2f, 1f) },
            },
            // The second lap round the tiers: eleven more zones, after the classic surf maps
            new Biome
            {
                name = "ALPINE", sky = new Color(0.72f, 0.84f, 0.98f), skyTop = new Color(0.25f, 0.5f, 0.9f), skyBottom = new Color(0.9f, 0.92f, 0.95f),
                fogStart = 300f, fogEnd = 1800f,
                ambientSky = new Color(0.8f, 0.85f, 0.95f), ambientEquator = new Color(0.75f, 0.78f, 0.82f), ambientGround = new Color(0.55f, 0.55f, 0.6f),
                sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.3f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampSnow, slab = new Color(0.9f, 0.92f, 0.95f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.25f, 0.2f), glowAlt = new Color(0.2f, 0.45f, 1f),
                scenery = new Color(0.42f, 0.4f, 0.42f), scenerySurface = Surface.Rock, style = SceneryStyle.Alpine,
                floor = new Color(0.95f, 0.96f, 0.98f), floorSurface = Surface.Plaster,
                accent = new Color(0.1f, 0.24f, 0.13f), accentSurface = Surface.Plaster,
            },
            new Biome
            {
                name = "CANYON OASIS", sky = new Color(0.95f, 0.75f, 0.55f), skyTop = new Color(0.35f, 0.55f, 0.9f), skyBottom = new Color(0.8f, 0.55f, 0.4f),
                fogStart = 250f, fogEnd = 1500f,
                ambientSky = new Color(0.9f, 0.75f, 0.6f), ambientEquator = new Color(0.75f, 0.55f, 0.4f), ambientGround = new Color(0.4f, 0.3f, 0.22f),
                sunColor = new Color(1f, 0.85f, 0.65f), sunIntensity = 1.3f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampWhiteGrid, slab = new Color(0.85f, 0.62f, 0.4f), slabSurface = Surface.Plaster,
                glow = new Color(0.3f, 0.75f, 1f), glowAlt = new Color(0.4f, 0.9f, 1f),
                scenery = new Color(0.72f, 0.38f, 0.24f), scenerySurface = Surface.Rock, style = SceneryStyle.Canyon,
                floor = new Color(0.9f, 0.72f, 0.48f), floorSurface = Surface.Plaster,
                shaft = new Color(0.5f, 0.85f, 1f),
            },
            new Biome
            {
                name = "GLASS CITY", sky = new Color(0.02f, 0.02f, 0.04f), skyTop = new Color(0f, 0f, 0f), skyBottom = new Color(0.01f, 0.01f, 0.02f),
                fogStart = 200f, fogEnd = 1400f,
                ambientSky = new Color(0.3f, 0.35f, 0.45f), ambientEquator = new Color(0.15f, 0.18f, 0.25f), ambientGround = new Color(0.03f, 0.03f, 0.05f),
                sunColor = new Color(0.8f, 0.9f, 1f), sunIntensity = 0.6f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampGlass, slab = new Color(0.1f, 0.12f, 0.15f), slabSurface = Surface.Metal,
                glow = new Color(0.3f, 0.85f, 1f), glowAlt = new Color(1f, 0.3f, 0.7f),
                scenery = new Color(0.08f, 0.1f, 0.14f), scenerySurface = Surface.Metal, style = SceneryStyle.Glass,
                shaft = new Color(0.5f, 0.8f, 1f),
            },
            new Biome
            {
                name = "MOONLIT GARDEN", sky = new Color(0.08f, 0.12f, 0.2f), skyTop = new Color(0.01f, 0.02f, 0.06f), skyBottom = new Color(0.04f, 0.06f, 0.08f),
                fogStart = 120f, fogEnd = 900f,
                ambientSky = new Color(0.3f, 0.38f, 0.5f), ambientEquator = new Color(0.18f, 0.24f, 0.28f), ambientGround = new Color(0.05f, 0.07f, 0.08f),
                sunColor = new Color(0.6f, 0.7f, 0.95f), sunIntensity = 0.5f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampBrick, slab = new Color(0.85f, 0.86f, 0.88f), slabSurface = Surface.Plaster,
                glow = new Color(0.4f, 1f, 0.6f), glowAlt = new Color(0.3f, 0.9f, 0.85f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WallHedge, style = SceneryStyle.Garden,
                floor = new Color(0.05f, 0.3f, 0.33f), floorSurface = Surface.WhiteTile,
                accent = new Color(0.82f, 0.84f, 0.86f), accentSurface = Surface.Plaster,
                shaft = new Color(0.6f, 0.75f, 1f),
            },
            new Biome
            {
                name = "HEX LAB", sky = new Color(0.75f, 0.85f, 0.8f), skyTop = new Color(0.3f, 0.45f, 0.4f), skyBottom = new Color(0.6f, 0.7f, 0.65f),
                fogStart = 150f, fogEnd = 900f,
                ambientSky = new Color(0.85f, 0.9f, 0.88f), ambientEquator = new Color(0.7f, 0.75f, 0.72f), ambientGround = new Color(0.35f, 0.4f, 0.38f),
                sunColor = new Color(0.95f, 1f, 0.95f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampHex, slab = new Color(0.85f, 0.87f, 0.88f), slabSurface = Surface.Plaster,
                glow = new Color(0.3f, 1f, 0.35f), glowAlt = new Color(0.6f, 1f, 0.7f),
                scenery = new Color(0.92f, 0.94f, 0.94f), scenerySurface = Surface.WallLab, style = SceneryStyle.Lab,
                floor = new Color(0.2f, 0.22f, 0.22f), floorSurface = Surface.Plates,
                accent = new Color(0.25f, 0.3f, 0.28f), accentSurface = Surface.WallHexVents,
                shaft = new Color(0.7f, 1f, 0.75f),
            },
            new Biome
            {
                name = "EMBER SUNSET", sky = new Color(1f, 0.45f, 0.18f), skyTop = new Color(0.35f, 0.08f, 0.12f), skyBottom = new Color(0.25f, 0.08f, 0.04f),
                fogStart = 250f, fogEnd = 1500f,
                ambientSky = new Color(0.8f, 0.45f, 0.3f), ambientEquator = new Color(0.6f, 0.3f, 0.2f), ambientGround = new Color(0.2f, 0.08f, 0.05f),
                sunColor = new Color(1f, 0.6f, 0.3f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampEmber, slab = new Color(0.08f, 0.06f, 0.06f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.5f, 0.1f), glowAlt = new Color(1f, 0.25f, 0.1f),
                scenery = new Color(0.12f, 0.07f, 0.07f), scenerySurface = Surface.Rock, style = SceneryStyle.Ember,
                floor = new Color(0.1f, 0.05f, 0.04f), floorSurface = Surface.Rock,
            },
            new Biome
            {
                name = "AMETHYST", sky = new Color(0.12f, 0.06f, 0.2f), skyTop = new Color(0.02f, 0.01f, 0.05f), skyBottom = new Color(0.06f, 0.02f, 0.1f),
                fogStart = 200f, fogEnd = 1300f,
                ambientSky = new Color(0.45f, 0.35f, 0.6f), ambientEquator = new Color(0.25f, 0.18f, 0.35f), ambientGround = new Color(0.06f, 0.04f, 0.1f),
                sunColor = new Color(0.85f, 0.75f, 1f), sunIntensity = 0.6f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampAmethyst, slab = new Color(0.2f, 0.12f, 0.3f), slabSurface = Surface.Plaster,
                glow = new Color(0.85f, 0.8f, 1f), glowAlt = new Color(0.7f, 0.45f, 1f),
                scenery = new Color(0.55f, 0.4f, 0.85f), scenerySurface = Surface.Plaster, style = SceneryStyle.Amethyst,
            },
            new Biome
            {
                name = "TOY TOWN", sky = new Color(0.6f, 0.85f, 1f), skyTop = new Color(0.2f, 0.5f, 1f), skyBottom = new Color(0.75f, 0.9f, 1f),
                fogStart = 300f, fogEnd = 1800f,
                ambientSky = new Color(0.9f, 0.92f, 1f), ambientEquator = new Color(0.8f, 0.82f, 0.85f), ambientGround = new Color(0.5f, 0.55f, 0.45f),
                sunColor = new Color(1f, 1f, 0.95f), sunIntensity = 1.35f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampToy, slab = new Color(1f, 0.82f, 0.2f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.95f, 0.4f), glowAlt = new Color(0.4f, 0.9f, 1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.Plaster, style = SceneryStyle.Toy,
                floor = new Color(0.35f, 0.8f, 0.3f), floorSurface = Surface.Plaster,
                accent = new Color(0.95f, 0.2f, 0.2f), accentSurface = Surface.Plaster,
            },
            new Biome
            {
                name = "TORCH MINES", sky = new Color(0.1f, 0.05f, 0.02f), skyTop = new Color(0.01f, 0.01f, 0.01f), skyBottom = new Color(0.05f, 0.02f, 0.01f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.55f, 0.35f, 0.2f), ambientEquator = new Color(0.35f, 0.2f, 0.1f), ambientGround = new Color(0.1f, 0.05f, 0.02f),
                sunColor = new Color(1f, 0.6f, 0.3f), sunIntensity = 0.4f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampMine, slab = new Color(0.38f, 0.24f, 0.13f), slabSurface = Surface.Wood,
                glow = new Color(1f, 0.55f, 0.15f), glowAlt = new Color(1f, 0.75f, 0.35f),
                scenery = new Color(0.45f, 0.3f, 0.2f), scenerySurface = Surface.Rock, style = SceneryStyle.Mine,
                floor = new Color(0.25f, 0.16f, 0.1f), floorSurface = Surface.Rock,
                shaft = new Color(1f, 0.6f, 0.3f),
            },
            new Biome
            {
                name = "SYNTHWAVE STATION", sky = new Color(0.1f, 0.05f, 0.2f), skyTop = new Color(0.03f, 0.01f, 0.08f), skyBottom = new Color(0.05f, 0.02f, 0.1f),
                fogStart = 150f, fogEnd = 1000f,
                ambientSky = new Color(0.4f, 0.3f, 0.6f), ambientEquator = new Color(0.25f, 0.18f, 0.4f), ambientGround = new Color(0.05f, 0.03f, 0.1f),
                sunColor = new Color(0.9f, 0.6f, 1f), sunIntensity = 0.6f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampSynth, slab = new Color(0.9f, 0.9f, 0.95f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.2f, 0.6f), glowAlt = new Color(0.4f, 0.6f, 1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Synth,
                floor = new Color(1f, 1f, 1f), floorSurface = Surface.WallGrid,
                accent = new Color(0.15f, 0.1f, 0.35f), accentSurface = Surface.Plates,
            },
            new Biome
            {
                name = "SANDSTONE TEMPLE", sky = new Color(0.95f, 0.7f, 0.45f), skyTop = new Color(0.4f, 0.3f, 0.6f), skyBottom = new Color(0.5f, 0.3f, 0.2f),
                fogStart = 150f, fogEnd = 900f,
                ambientSky = new Color(0.9f, 0.65f, 0.45f), ambientEquator = new Color(0.7f, 0.45f, 0.3f), ambientGround = new Color(0.3f, 0.18f, 0.1f),
                sunColor = new Color(1f, 0.8f, 0.55f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampSandstone, slab = new Color(0.8f, 0.55f, 0.35f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.6f, 0.2f), glowAlt = new Color(1f, 0.8f, 0.45f),
                scenery = new Color(0.85f, 0.52f, 0.34f), scenerySurface = Surface.WallSandstone, style = SceneryStyle.Temple,
                floor = new Color(0.75f, 0.55f, 0.35f), floorSurface = Surface.Plaster,
                accent = new Color(0.7f, 0.4f, 0.26f), accentSurface = Surface.WallPanels,
                shaft = new Color(1f, 0.8f, 0.55f),
            },
            // The finale: ten zones after the legendary hard maps, hardest last, then the finish
            // Real maps: utopia, summer and mesa, laid out after their runs, before the expert stretch
            new Biome
            {
                name = "UTOPIA", sky = new Color(1f, 0.85f, 0.65f), skyTop = new Color(0.45f, 0.6f, 0.85f), skyBottom = new Color(0.95f, 0.85f, 0.75f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.95f, 0.9f, 0.85f), ambientEquator = new Color(0.75f, 0.68f, 0.6f), ambientGround = new Color(0.4f, 0.35f, 0.3f),
                sunColor = new Color(1f, 0.9f, 0.75f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampUtopia, slab = new Color(0.85f, 0.8f, 0.75f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.45f, 0.1f), glowAlt = new Color(0.3f, 0.5f, 0.8f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WallUtopia, style = SceneryStyle.Gallery,
                floor = new Color(0.8f, 0.76f, 0.72f), floorSurface = Surface.Plaster,
                script = MapScripts.Utopia1,
                tier = "TIER 1",
                section = "surf_utopia_njv 1/2",
            },
            new Biome
            {
                name = "UTOPIA", sky = new Color(1f, 0.85f, 0.65f), skyTop = new Color(0.45f, 0.6f, 0.85f), skyBottom = new Color(0.95f, 0.85f, 0.75f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.95f, 0.9f, 0.85f), ambientEquator = new Color(0.75f, 0.68f, 0.6f), ambientGround = new Color(0.4f, 0.35f, 0.3f),
                sunColor = new Color(1f, 0.9f, 0.75f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampUtopia, slab = new Color(0.85f, 0.8f, 0.75f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.45f, 0.1f), glowAlt = new Color(0.3f, 0.5f, 0.8f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WallUtopia, style = SceneryStyle.Gallery,
                floor = new Color(0.8f, 0.76f, 0.72f), floorSurface = Surface.Plaster,
                script = MapScripts.Utopia2,
                tier = "TIER 1",
                section = "surf_utopia_njv 2/2",
                continues = true,
            },
            new Biome
            {
                name = "SUMMER", sky = new Color(0.75f, 0.88f, 1f), skyTop = new Color(0.3f, 0.55f, 0.95f), skyBottom = new Color(0.85f, 0.9f, 0.95f),
                fogStart = 300f, fogEnd = 1800f,
                ambientSky = new Color(0.95f, 0.95f, 1f), ambientEquator = new Color(0.8f, 0.75f, 0.65f), ambientGround = new Color(0.45f, 0.4f, 0.3f),
                sunColor = new Color(1f, 0.95f, 0.85f), sunIntensity = 1.3f,
                ramp = new Color(0.62f, 0.45f, 0.28f), rampSurface = Surface.Wood, slab = new Color(0.85f, 0.75f, 0.55f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.5f, 0.15f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.88f, 0.76f, 0.55f), scenerySurface = Surface.WallSandstone, style = SceneryStyle.Canyon,
                floor = new Color(0.9f, 0.82f, 0.6f), floorSurface = Surface.Plaster,
                script = MapScripts.Summer1,
                tier = "TIER 3",
                section = "surf_summer: beach town and water park",
            },
            new Biome
            {
                name = "SUMMER SKATE PARK", sky = new Color(0.6f, 0.9f, 0.95f), skyTop = new Color(0.25f, 0.6f, 0.95f), skyBottom = new Color(0.4f, 0.75f, 0.85f),
                fogStart = 300f, fogEnd = 1800f,
                ambientSky = new Color(0.95f, 0.97f, 1f), ambientEquator = new Color(0.75f, 0.75f, 0.75f), ambientGround = new Color(0.4f, 0.42f, 0.42f),
                sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.3f,
                ramp = new Color(0.72f, 0.72f, 0.7f), rampSurface = Surface.Concrete, slab = new Color(0.7f, 0.7f, 0.68f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.5f, 0.1f), glowAlt = new Color(1f, 0.4f, 0.7f),
                scenery = new Color(0.7f, 0.7f, 0.7f), scenerySurface = Surface.Concrete, style = SceneryStyle.Palace,
                script = MapScripts.Summer2,
                tier = "TIER 3",
                section = "surf_summer: candy and skate park",
            },
            new Biome
            {
                name = "SUMMER NIGHT", sky = new Color(0.08f, 0.1f, 0.35f), skyTop = new Color(0.02f, 0.03f, 0.15f), skyBottom = new Color(0.1f, 0.12f, 0.3f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.4f, 0.45f, 0.7f), ambientEquator = new Color(0.22f, 0.24f, 0.4f), ambientGround = new Color(0.06f, 0.06f, 0.1f),
                sunColor = new Color(0.7f, 0.75f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.6f, 0.6f, 0.62f), rampSurface = Surface.Plates, slab = new Color(0.3f, 0.25f, 0.2f), slabSurface = Surface.Rock,
                glow = new Color(1f, 0.2f, 0.15f), glowAlt = new Color(1f, 0.85f, 0.4f),
                scenery = new Color(0.25f, 0.2f, 0.18f), scenerySurface = Surface.Rock, style = SceneryStyle.Canyon,
                script = MapScripts.Summer3,
                tier = "TIER 4",
                section = "surf_summer: fort, night canyon, waterslides",
            },
            new Biome
            {
                name = "MESA", sky = new Color(0.12f, 0.08f, 0.06f), skyTop = new Color(0.03f, 0.02f, 0.02f), skyBottom = new Color(0.1f, 0.07f, 0.05f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.55f, 0.45f, 0.4f), ambientEquator = new Color(0.3f, 0.24f, 0.2f), ambientGround = new Color(0.08f, 0.06f, 0.05f),
                sunColor = new Color(1f, 0.85f, 0.7f), sunIntensity = 0.6f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampMesa, slab = new Color(0.45f, 0.35f, 0.28f), slabSurface = Surface.Rock,
                glow = new Color(0.2f, 0.9f, 1f), glowAlt = new Color(1f, 0.45f, 0.1f),
                scenery = new Color(0.42f, 0.3f, 0.22f), scenerySurface = Surface.Rock, style = SceneryStyle.Grotto,
                floor = new Color(0.35f, 0.26f, 0.2f), floorSurface = Surface.Rock,
                script = MapScripts.Mesa1,
                tier = "TIER 2",
                section = "surf_mesa 1/2",
            },
            new Biome
            {
                name = "MESA", sky = new Color(0.12f, 0.08f, 0.06f), skyTop = new Color(0.03f, 0.02f, 0.02f), skyBottom = new Color(0.1f, 0.07f, 0.05f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.55f, 0.45f, 0.4f), ambientEquator = new Color(0.3f, 0.24f, 0.2f), ambientGround = new Color(0.08f, 0.06f, 0.05f),
                sunColor = new Color(1f, 0.85f, 0.7f), sunIntensity = 0.6f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampMesa, slab = new Color(0.45f, 0.35f, 0.28f), slabSurface = Surface.Rock,
                glow = new Color(0.2f, 0.9f, 1f), glowAlt = new Color(1f, 0.45f, 0.1f),
                scenery = new Color(0.42f, 0.3f, 0.22f), scenerySurface = Surface.Rock, style = SceneryStyle.Grotto,
                floor = new Color(0.35f, 0.26f, 0.2f), floorSurface = Surface.Rock,
                script = MapScripts.Mesa2,
                tier = "TIER 2",
                section = "surf_mesa 2/2 (no checkpoint)",
                continues = true,
                noCheckpoint = true,
            },
            new Biome
            {
                name = "OMNIFIC NEON", sky = new Color(0.1f, 0.03f, 0.18f), skyTop = new Color(0.02f, 0f, 0.05f), skyBottom = new Color(0.05f, 0.01f, 0.1f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.45f, 0.35f, 0.6f), ambientEquator = new Color(0.25f, 0.18f, 0.4f), ambientGround = new Color(0.05f, 0.03f, 0.1f),
                sunColor = new Color(0.9f, 0.7f, 1f), sunIntensity = 0.5f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampOmnific, slab = new Color(0.12f, 0.08f, 0.2f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.3f, 0.8f), glowAlt = new Color(0.3f, 0.9f, 1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Spectrum,
                hues = new[] { new Color(1f, 0.2f, 0.7f), new Color(0.3f, 1f, 0.45f), new Color(0.25f, 0.7f, 1f), new Color(0.7f, 0.3f, 1f), new Color(1f, 0.55f, 0.15f), new Color(0.2f, 1f, 0.95f) },
            },
            new Biome
            {
                name = "CASTLE WALLS", sky = new Color(0.35f, 0.3f, 0.3f), skyTop = new Color(0.12f, 0.12f, 0.2f), skyBottom = new Color(0.2f, 0.16f, 0.14f),
                fogStart = 90f, fogEnd = 650f,
                ambientSky = new Color(0.6f, 0.55f, 0.5f), ambientEquator = new Color(0.4f, 0.34f, 0.3f), ambientGround = new Color(0.12f, 0.1f, 0.08f),
                sunColor = new Color(1f, 0.8f, 0.6f), sunIntensity = 0.8f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampCastle, slab = new Color(0.35f, 0.33f, 0.3f), slabSurface = Surface.Stone,
                glow = new Color(1f, 0.6f, 0.25f), glowAlt = new Color(0.2f, 0.9f, 0.85f),
                scenery = new Color(0.62f, 0.6f, 0.56f), scenerySurface = Surface.WallCastle, style = SceneryStyle.Cathedral,
                floor = new Color(0.4f, 0.38f, 0.35f), floorSurface = Surface.WallCastle,
                accent = new Color(0.35f, 0.25f, 0.16f), accentSurface = Surface.WallWood,
                shaft = new Color(1f, 0.8f, 0.55f),
            },
            new Biome
            {
                name = "SIX SIX SIX", sky = new Color(0.25f, 0.03f, 0.03f), skyTop = new Color(0.04f, 0f, 0f), skyBottom = new Color(0.08f, 0.01f, 0.01f),
                fogStart = 60f, fogEnd = 480f,
                ambientSky = new Color(0.6f, 0.2f, 0.18f), ambientEquator = new Color(0.35f, 0.08f, 0.07f), ambientGround = new Color(0.08f, 0.01f, 0.01f),
                sunColor = new Color(1f, 0.4f, 0.3f), sunIntensity = 0.6f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampHell, slab = new Color(0.3f, 0.06f, 0.05f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.2f, 0.1f), glowAlt = new Color(1f, 0.5f, 0.3f),
                scenery = new Color(0.45f, 0.12f, 0.1f), scenerySurface = Surface.Panel, style = SceneryStyle.Mine,
                floor = new Color(0.25f, 0.05f, 0.04f), floorSurface = Surface.Plates,
                accent = new Color(0.5f, 0.14f, 0.1f), accentSurface = Surface.WallPanels,
            },
            new Biome
            {
                name = "JADE SHRINE", sky = new Color(0.6f, 0.75f, 0.72f), skyTop = new Color(0.3f, 0.45f, 0.55f), skyBottom = new Color(0.35f, 0.5f, 0.45f),
                fogStart = 180f, fogEnd = 1100f,
                ambientSky = new Color(0.75f, 0.85f, 0.82f), ambientEquator = new Color(0.5f, 0.6f, 0.55f), ambientGround = new Color(0.2f, 0.28f, 0.24f),
                sunColor = new Color(1f, 0.95f, 0.85f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampTorii, slab = new Color(0.12f, 0.1f, 0.1f), slabSurface = Surface.Wood,
                glow = new Color(1f, 0.25f, 0.15f), glowAlt = new Color(0.3f, 1f, 0.7f),
                scenery = new Color(0.45f, 0.45f, 0.47f), scenerySurface = Surface.Rock, style = SceneryStyle.Canyon,
                floor = new Color(0.15f, 0.4f, 0.33f), floorSurface = Surface.Stone,
            },
            new Biome
            {
                name = "TOMB OF ANUBIS", sky = new Color(0.2f, 0.12f, 0.05f), skyTop = new Color(0.03f, 0.02f, 0.01f), skyBottom = new Color(0.1f, 0.06f, 0.03f),
                fogStart = 70f, fogEnd = 520f,
                ambientSky = new Color(0.75f, 0.5f, 0.3f), ambientEquator = new Color(0.5f, 0.32f, 0.18f), ambientGround = new Color(0.15f, 0.09f, 0.05f),
                sunColor = new Color(1f, 0.7f, 0.4f), sunIntensity = 0.55f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampTomb, slab = new Color(0.55f, 0.38f, 0.22f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.55f, 0.15f), glowAlt = new Color(1f, 0.8f, 0.35f),
                scenery = new Color(0.72f, 0.5f, 0.3f), scenerySurface = Surface.WallSandstone, style = SceneryStyle.Temple,
                floor = new Color(0.55f, 0.4f, 0.25f), floorSurface = Surface.Plaster,
                accent = new Color(0.6f, 0.42f, 0.24f), accentSurface = Surface.WallPanels,
                shaft = new Color(1f, 0.65f, 0.3f),
            },
            new Biome
            {
                name = "DEITY'S SANCTUM", sky = new Color(0.2f, 0.1f, 0.05f), skyTop = new Color(0.04f, 0.02f, 0.01f), skyBottom = new Color(0.1f, 0.05f, 0.02f),
                fogStart = 80f, fogEnd = 560f,
                ambientSky = new Color(0.7f, 0.45f, 0.3f), ambientEquator = new Color(0.45f, 0.27f, 0.16f), ambientGround = new Color(0.12f, 0.07f, 0.04f),
                sunColor = new Color(1f, 0.75f, 0.5f), sunIntensity = 0.7f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampDeity, slab = new Color(0.4f, 0.2f, 0.1f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.6f, 0.3f), glowAlt = new Color(0.35f, 0.9f, 0.75f),
                scenery = new Color(0.62f, 0.36f, 0.2f), scenerySurface = Surface.WallCopper, style = SceneryStyle.Library,
                floor = new Color(0.3f, 0.16f, 0.08f), floorSurface = Surface.Wood,
                accent = new Color(0.55f, 0.3f, 0.16f), accentSurface = Surface.WallCopper,
            },
            new Biome
            {
                name = "CORRUPTION", sky = new Color(0.2f, 0.05f, 0.3f), skyTop = new Color(0.05f, 0.01f, 0.1f), skyBottom = new Color(0.25f, 0.08f, 0.35f),
                fogStart = 150f, fogEnd = 900f,
                ambientSky = new Color(0.6f, 0.45f, 0.8f), ambientEquator = new Color(0.35f, 0.2f, 0.5f), ambientGround = new Color(0.1f, 0.04f, 0.15f),
                sunColor = new Color(0.95f, 0.85f, 1f), sunIntensity = 0.8f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampCorrupt, slab = new Color(0.1f, 0.05f, 0.15f), slabSurface = Surface.Metal,
                glow = new Color(0.6f, 0.15f, 1f), glowAlt = new Color(1f, 0.3f, 0.9f),
                scenery = new Color(0.14f, 0.08f, 0.2f), scenerySurface = Surface.WallHexVents, style = SceneryStyle.Rings,
            },
            new Biome
            {
                name = "SINSANE PATCHWORK", sky = new Color(0.55f, 0.5f, 0.45f), skyTop = new Color(0.3f, 0.35f, 0.45f), skyBottom = new Color(0.4f, 0.36f, 0.32f),
                fogStart = 140f, fogEnd = 800f,
                ambientSky = new Color(0.75f, 0.7f, 0.65f), ambientEquator = new Color(0.5f, 0.46f, 0.42f), ambientGround = new Color(0.2f, 0.18f, 0.16f),
                sunColor = new Color(1f, 0.93f, 0.8f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampPatchwork, slab = new Color(0.5f, 0.46f, 0.4f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.8f, 0.3f), glowAlt = new Color(0.4f, 0.8f, 1f),
                scenery = new Color(0.6f, 0.45f, 0.35f), scenerySurface = Surface.Bricks, style = SceneryStyle.Gallery,
            },
            new Biome
            {
                name = "ESSENTIA RUINS", sky = new Color(1f, 0.8f, 0.8f), skyTop = new Color(0.45f, 0.65f, 0.95f), skyBottom = new Color(0.95f, 0.75f, 0.8f),
                fogStart = 260f, fogEnd = 1500f,
                ambientSky = new Color(0.95f, 0.9f, 0.95f), ambientEquator = new Color(0.85f, 0.75f, 0.8f), ambientGround = new Color(0.55f, 0.5f, 0.55f),
                sunColor = new Color(1f, 0.92f, 0.88f), sunIntensity = 1.25f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampRuins, slab = new Color(0.55f, 0.55f, 0.58f), slabSurface = Surface.Stone,
                glow = new Color(1f, 0.75f, 0.4f), glowAlt = new Color(0.9f, 0.35f, 0.3f),
                scenery = new Color(0.6f, 0.6f, 0.63f), scenerySurface = Surface.Stone, style = SceneryStyle.Palace,
            },
            new Biome
            {
                name = "PRO", sky = new Color(0.02f, 0.02f, 0.02f), skyTop = new Color(0f, 0f, 0f), skyBottom = new Color(0.01f, 0.01f, 0.01f),
                fogStart = 90f, fogEnd = 700f,
                ambientSky = new Color(0.4f, 0.4f, 0.42f), ambientEquator = new Color(0.2f, 0.2f, 0.22f), ambientGround = new Color(0.04f, 0.04f, 0.05f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 0.7f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampPro, slab = new Color(0.06f, 0.06f, 0.06f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.1f, 0.12f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Wire,
            },
            // The Legend zones: after the finale, one for each map of the hard surf playlist,
            // each as hard as the hardest surf maps there are, harder still to the end
            new Biome
            {
                name = "LOVE TUNNEL", sky = new Color(0.3f, 0.08f, 0.3f), skyTop = new Color(0.08f, 0.01f, 0.12f), skyBottom = new Color(0.2f, 0.05f, 0.2f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.7f, 0.4f, 0.7f), ambientEquator = new Color(0.45f, 0.2f, 0.45f), ambientGround = new Color(0.1f, 0.03f, 0.1f),
                sunColor = new Color(1f, 0.7f, 0.9f), sunIntensity = 0.6f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampLove, slab = new Color(0.25f, 0.08f, 0.25f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.35f, 0.75f), glowAlt = new Color(0.7f, 0.3f, 1f),
                scenery = new Color(0.3f, 0.1f, 0.35f), scenerySurface = Surface.Panel, style = SceneryStyle.Rings,
            },
            new Biome
            {
                name = "CORNFIELD SUNSET", sky = new Color(1f, 0.8f, 0.35f), skyTop = new Color(0.55f, 0.6f, 0.85f), skyBottom = new Color(1f, 0.7f, 0.3f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.95f, 0.85f, 0.6f), ambientEquator = new Color(0.7f, 0.6f, 0.35f), ambientGround = new Color(0.25f, 0.22f, 0.1f),
                sunColor = new Color(1f, 0.85f, 0.55f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampCornfield, slab = new Color(0.4f, 0.3f, 0.15f), slabSurface = Surface.Wood,
                glow = new Color(1f, 0.8f, 0.2f), glowAlt = new Color(0.5f, 1f, 0.3f),
                scenery = new Color(0.35f, 0.55f, 0.2f), scenerySurface = Surface.WallHedge, style = SceneryStyle.Garden,
            },
            new Biome
            {
                name = "NEON SHAPES", sky = new Color(0.03f, 0.02f, 0.06f), skyTop = new Color(0f, 0f, 0.02f), skyBottom = new Color(0.02f, 0.01f, 0.05f),
                fogStart = 120f, fogEnd = 800f,
                ambientSky = new Color(0.35f, 0.3f, 0.45f), ambientEquator = new Color(0.18f, 0.14f, 0.25f), ambientGround = new Color(0.03f, 0.02f, 0.05f),
                sunColor = new Color(0.8f, 0.8f, 1f), sunIntensity = 0.5f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampNeonShapes, slab = new Color(0.05f, 0.05f, 0.07f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.3f, 0.8f), glowAlt = new Color(0.3f, 0.95f, 1f),
                scenery = new Color(0.08f, 0.08f, 0.1f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Synth,
            },
            new Biome
            {
                name = "GLACIER CAVE", sky = new Color(0.35f, 0.55f, 0.75f), skyTop = new Color(0.1f, 0.2f, 0.4f), skyBottom = new Color(0.4f, 0.6f, 0.75f),
                fogStart = 150f, fogEnd = 1000f,
                ambientSky = new Color(0.6f, 0.7f, 0.85f), ambientEquator = new Color(0.35f, 0.45f, 0.6f), ambientGround = new Color(0.1f, 0.15f, 0.25f),
                sunColor = new Color(0.9f, 0.95f, 1f), sunIntensity = 0.9f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampGlacier, slab = new Color(0.5f, 0.65f, 0.8f), slabSurface = Surface.Ice,
                glow = new Color(0.5f, 0.9f, 1f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.7f, 0.85f, 0.95f), scenerySurface = Surface.Ice, style = SceneryStyle.Grotto,
            },
            new Biome
            {
                name = "RED LINE LAB", sky = new Color(0.9f, 0.92f, 0.95f), skyTop = new Color(0.7f, 0.75f, 0.8f), skyBottom = new Color(0.95f, 0.95f, 0.95f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.95f, 0.95f, 0.97f), ambientEquator = new Color(0.8f, 0.8f, 0.82f), ambientGround = new Color(0.5f, 0.5f, 0.52f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampRedLine, slab = new Color(0.85f, 0.85f, 0.87f), slabSurface = Surface.WhiteTile,
                glow = new Color(1f, 0.1f, 0.12f), glowAlt = new Color(0.2f, 1f, 0.4f),
                scenery = new Color(0.92f, 0.92f, 0.94f), scenerySurface = Surface.WallLab, style = SceneryStyle.Lab,
            },
            new Biome
            {
                name = "CONCRETE BUNKER", sky = new Color(0.45f, 0.47f, 0.5f), skyTop = new Color(0.25f, 0.27f, 0.3f), skyBottom = new Color(0.4f, 0.4f, 0.42f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.6f, 0.6f, 0.62f), ambientEquator = new Color(0.4f, 0.4f, 0.42f), ambientGround = new Color(0.15f, 0.15f, 0.16f),
                sunColor = new Color(1f, 0.95f, 0.9f), sunIntensity = 0.9f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampBunker, slab = new Color(0.4f, 0.4f, 0.42f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.85f, 0.3f), glowAlt = new Color(0.4f, 0.8f, 1f),
                scenery = new Color(0.5f, 0.5f, 0.52f), scenerySurface = Surface.Concrete, style = SceneryStyle.Mine,
            },
            new Biome
            {
                name = "CYAN CRYSTAL", sky = new Color(0.05f, 0.2f, 0.25f), skyTop = new Color(0.01f, 0.05f, 0.08f), skyBottom = new Color(0.05f, 0.25f, 0.3f),
                fogStart = 120f, fogEnd = 800f,
                ambientSky = new Color(0.4f, 0.8f, 0.85f), ambientEquator = new Color(0.2f, 0.45f, 0.5f), ambientGround = new Color(0.04f, 0.1f, 0.12f),
                sunColor = new Color(0.7f, 1f, 1f), sunIntensity = 0.7f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampCyanCrystal, slab = new Color(0.08f, 0.25f, 0.3f), slabSurface = Surface.DarkStone,
                glow = new Color(0.3f, 1f, 1f), glowAlt = new Color(0.6f, 0.5f, 1f),
                scenery = new Color(0.1f, 0.4f, 0.45f), scenerySurface = Surface.Rock, style = SceneryStyle.Amethyst,
            },
            new Biome
            {
                name = "CHROME WAVE", sky = new Color(0.7f, 0.75f, 0.85f), skyTop = new Color(0.35f, 0.45f, 0.7f), skyBottom = new Color(0.8f, 0.82f, 0.88f),
                fogStart = 220f, fogEnd = 1300f,
                ambientSky = new Color(0.85f, 0.88f, 0.95f), ambientEquator = new Color(0.6f, 0.62f, 0.7f), ambientGround = new Color(0.3f, 0.3f, 0.35f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampChromeWave, slab = new Color(0.55f, 0.57f, 0.62f), slabSurface = Surface.Metal,
                glow = new Color(0.4f, 0.8f, 1f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.7f, 0.72f, 0.78f), scenerySurface = Surface.Metal, style = SceneryStyle.Glass,
            },
            new Biome
            {
                name = "LIGHT STREAKS", sky = new Color(0.02f, 0.04f, 0.15f), skyTop = new Color(0f, 0.01f, 0.05f), skyBottom = new Color(0.03f, 0.06f, 0.2f),
                fogStart = 150f, fogEnd = 900f,
                ambientSky = new Color(0.3f, 0.4f, 0.7f), ambientEquator = new Color(0.15f, 0.2f, 0.4f), ambientGround = new Color(0.02f, 0.03f, 0.08f),
                sunColor = new Color(0.8f, 0.9f, 1f), sunIntensity = 0.5f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampStreak, slab = new Color(0.04f, 0.06f, 0.18f), slabSurface = Surface.Metal,
                glow = new Color(0.6f, 0.8f, 1f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.06f, 0.08f, 0.22f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Wire,
            },
            new Biome
            {
                name = "GREAT WALL", sky = new Color(0.75f, 0.8f, 0.85f), skyTop = new Color(0.45f, 0.6f, 0.8f), skyBottom = new Color(0.8f, 0.8f, 0.78f),
                fogStart = 250f, fogEnd = 1500f,
                ambientSky = new Color(0.85f, 0.85f, 0.85f), ambientEquator = new Color(0.6f, 0.58f, 0.55f), ambientGround = new Color(0.28f, 0.26f, 0.22f),
                sunColor = new Color(1f, 0.95f, 0.85f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampGreatWall, slab = new Color(0.5f, 0.47f, 0.42f), slabSurface = Surface.Stone,
                glow = new Color(1f, 0.4f, 0.2f), glowAlt = new Color(1f, 0.85f, 0.4f),
                scenery = new Color(0.55f, 0.52f, 0.47f), scenerySurface = Surface.WallCastle, style = SceneryStyle.Alpine,
            },
            new Biome
            {
                name = "ARCADE MONSTERS", sky = new Color(0.02f, 0.02f, 0.04f), skyTop = new Color(0f, 0f, 0f), skyBottom = new Color(0.02f, 0.02f, 0.03f),
                fogStart = 120f, fogEnd = 800f,
                ambientSky = new Color(0.35f, 0.4f, 0.35f), ambientEquator = new Color(0.15f, 0.2f, 0.15f), ambientGround = new Color(0.02f, 0.03f, 0.02f),
                sunColor = new Color(0.9f, 1f, 0.9f), sunIntensity = 0.5f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampArcade, slab = new Color(0.05f, 0.05f, 0.06f), slabSurface = Surface.Metal,
                glow = new Color(0.3f, 1f, 0.25f), glowAlt = new Color(1f, 0.2f, 0.2f),
                scenery = new Color(0.08f, 0.08f, 0.1f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Toy,
            },
            new Biome
            {
                name = "HAZARD FOUNDRY", sky = new Color(0.25f, 0.12f, 0.05f), skyTop = new Color(0.05f, 0.02f, 0.01f), skyBottom = new Color(0.15f, 0.07f, 0.03f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.7f, 0.45f, 0.25f), ambientEquator = new Color(0.4f, 0.22f, 0.1f), ambientGround = new Color(0.1f, 0.05f, 0.02f),
                sunColor = new Color(1f, 0.7f, 0.4f), sunIntensity = 0.7f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampHazard, slab = new Color(0.1f, 0.1f, 0.1f), slabSurface = Surface.Plates,
                glow = new Color(1f, 0.5f, 0.05f), glowAlt = new Color(1f, 0.2f, 0.1f),
                scenery = new Color(0.15f, 0.12f, 0.1f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Forge,
            },
            new Biome
            {
                name = "HEX JUNGLE", sky = new Color(0.12f, 0.25f, 0.15f), skyTop = new Color(0.03f, 0.08f, 0.05f), skyBottom = new Color(0.1f, 0.22f, 0.12f),
                fogStart = 100f, fogEnd = 700f,
                ambientSky = new Color(0.45f, 0.7f, 0.5f), ambientEquator = new Color(0.25f, 0.4f, 0.28f), ambientGround = new Color(0.05f, 0.1f, 0.06f),
                sunColor = new Color(0.85f, 1f, 0.85f), sunIntensity = 0.8f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampHexJungle, slab = new Color(0.1f, 0.2f, 0.12f), slabSurface = Surface.HexTile,
                glow = new Color(0.3f, 1f, 0.4f), glowAlt = new Color(1f, 0.9f, 0.3f),
                scenery = new Color(0.2f, 0.4f, 0.25f), scenerySurface = Surface.WallHedge, style = SceneryStyle.Garden,
            },
            new Biome
            {
                name = "DARK FOUNDRY", sky = new Color(0.22f, 0.18f, 0.17f), skyTop = new Color(0.08f, 0.06f, 0.06f), skyBottom = new Color(0.2f, 0.16f, 0.15f),
                fogStart = 120f, fogEnd = 800f,
                ambientSky = new Color(0.75f, 0.65f, 0.62f), ambientEquator = new Color(0.45f, 0.38f, 0.36f), ambientGround = new Color(0.12f, 0.1f, 0.1f),
                sunColor = new Color(1f, 0.75f, 0.65f), sunIntensity = 0.9f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampIndustrial, slab = new Color(0.2f, 0.2f, 0.21f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.55f, 0.45f), glowAlt = new Color(1f, 0.8f, 0.5f),
                scenery = new Color(0.25f, 0.25f, 0.26f), scenerySurface = Surface.WallGrate, style = SceneryStyle.Mine,
            },
            new Biome
            {
                name = "ROCK QUARRY", sky = new Color(0.65f, 0.7f, 0.75f), skyTop = new Color(0.4f, 0.55f, 0.75f), skyBottom = new Color(0.6f, 0.6f, 0.58f),
                fogStart = 180f, fogEnd = 1100f,
                ambientSky = new Color(0.75f, 0.75f, 0.72f), ambientEquator = new Color(0.5f, 0.48f, 0.44f), ambientGround = new Color(0.2f, 0.18f, 0.15f),
                sunColor = new Color(1f, 0.93f, 0.8f), sunIntensity = 1.1f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampQuarry, slab = new Color(0.45f, 0.44f, 0.42f), slabSurface = Surface.Rock,
                glow = new Color(1f, 0.7f, 0.3f), glowAlt = new Color(0.5f, 0.9f, 1f),
                scenery = new Color(0.5f, 0.49f, 0.47f), scenerySurface = Surface.Rock, style = SceneryStyle.Canyon,
            },
            new Biome
            {
                name = "MAUVE TEMPLE", sky = new Color(0.8f, 0.6f, 0.7f), skyTop = new Color(0.45f, 0.35f, 0.6f), skyBottom = new Color(0.85f, 0.65f, 0.72f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.9f, 0.75f, 0.85f), ambientEquator = new Color(0.65f, 0.5f, 0.6f), ambientGround = new Color(0.3f, 0.2f, 0.25f),
                sunColor = new Color(1f, 0.9f, 0.95f), sunIntensity = 1.1f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampMauve, slab = new Color(0.6f, 0.42f, 0.52f), slabSurface = Surface.Stone,
                glow = new Color(1f, 0.6f, 0.85f), glowAlt = new Color(1f, 0.9f, 0.5f),
                scenery = new Color(0.75f, 0.55f, 0.65f), scenerySurface = Surface.WallSandstone, style = SceneryStyle.Temple,
            },
            new Biome
            {
                name = "SURF SCHOOL", sky = new Color(0.85f, 0.85f, 0.85f), skyTop = new Color(0.55f, 0.6f, 0.7f), skyBottom = new Color(0.9f, 0.9f, 0.9f),
                fogStart = 220f, fogEnd = 1300f,
                ambientSky = new Color(0.9f, 0.9f, 0.9f), ambientEquator = new Color(0.65f, 0.65f, 0.65f), ambientGround = new Color(0.3f, 0.3f, 0.3f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampSurfSchool, slab = new Color(0.15f, 0.15f, 0.15f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.85f, 0.1f), glowAlt = new Color(0.2f, 0.6f, 1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.WhiteTile, style = SceneryStyle.Gallery,
            },
            new Biome
            {
                name = "VAPOR GEOMETRY", sky = new Color(0.2f, 0.05f, 0.25f), skyTop = new Color(0.05f, 0.01f, 0.08f), skyBottom = new Color(0.35f, 0.08f, 0.3f),
                fogStart = 150f, fogEnd = 900f,
                ambientSky = new Color(0.6f, 0.35f, 0.7f), ambientEquator = new Color(0.35f, 0.15f, 0.4f), ambientGround = new Color(0.08f, 0.02f, 0.1f),
                sunColor = new Color(1f, 0.7f, 0.95f), sunIntensity = 0.6f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampVapor, slab = new Color(0.12f, 0.05f, 0.18f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.3f, 0.7f), glowAlt = new Color(0.5f, 0.3f, 1f),
                scenery = new Color(0.2f, 0.08f, 0.28f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Synth,
            },
            new Biome
            {
                name = "FLAG HILLS", sky = new Color(0.9f, 0.8f, 0.6f), skyTop = new Color(0.5f, 0.65f, 0.9f), skyBottom = new Color(0.95f, 0.85f, 0.65f),
                fogStart = 250f, fogEnd = 1500f,
                ambientSky = new Color(0.95f, 0.9f, 0.75f), ambientEquator = new Color(0.7f, 0.62f, 0.45f), ambientGround = new Color(0.3f, 0.25f, 0.15f),
                sunColor = new Color(1f, 0.92f, 0.75f), sunIntensity = 1.25f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampFlags, slab = new Color(0.6f, 0.5f, 0.35f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.15f, 0.15f), glowAlt = new Color(1f, 0.9f, 0.4f),
                scenery = new Color(0.7f, 0.6f, 0.42f), scenerySurface = Surface.WallSandstone, style = SceneryStyle.Canyon,
            },
            new Biome
            {
                name = "OLD WAREHOUSE", sky = new Color(0.3f, 0.25f, 0.2f), skyTop = new Color(0.08f, 0.06f, 0.05f), skyBottom = new Color(0.22f, 0.18f, 0.14f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.65f, 0.55f, 0.45f), ambientEquator = new Color(0.4f, 0.32f, 0.25f), ambientGround = new Color(0.1f, 0.08f, 0.06f),
                sunColor = new Color(1f, 0.85f, 0.65f), sunIntensity = 0.8f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampWarehouse, slab = new Color(0.35f, 0.22f, 0.14f), slabSurface = Surface.Wood,
                glow = new Color(1f, 0.85f, 0.3f), glowAlt = new Color(1f, 0.5f, 0.2f),
                scenery = new Color(0.5f, 0.28f, 0.2f), scenerySurface = Surface.Bricks, style = SceneryStyle.Library,
            },
            new Biome
            {
                name = "SEA MINES", sky = new Color(0.55f, 0.8f, 0.95f), skyTop = new Color(0.2f, 0.45f, 0.8f), skyBottom = new Color(0.3f, 0.6f, 0.85f),
                fogStart = 220f, fogEnd = 1300f,
                ambientSky = new Color(0.8f, 0.9f, 1f), ambientEquator = new Color(0.5f, 0.65f, 0.8f), ambientGround = new Color(0.15f, 0.3f, 0.45f),
                sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.25f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampSeaMine, slab = new Color(0.1f, 0.3f, 0.55f), slabSurface = Surface.Tiles,
                glow = new Color(1f, 0.3f, 0.2f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.9f, 0.92f, 0.95f), scenerySurface = Surface.WhiteTile, style = SceneryStyle.Palace,
            },
            new Biome
            {
                name = "MOON CRATER", sky = new Color(0.02f, 0.02f, 0.04f), skyTop = new Color(0f, 0f, 0.01f), skyBottom = new Color(0.05f, 0.05f, 0.07f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.45f, 0.45f, 0.5f), ambientEquator = new Color(0.25f, 0.25f, 0.28f), ambientGround = new Color(0.05f, 0.05f, 0.06f),
                sunColor = new Color(0.95f, 0.95f, 1f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampMoon, slab = new Color(0.35f, 0.35f, 0.37f), slabSurface = Surface.Rock,
                glow = new Color(0.3f, 1f, 0.9f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.45f, 0.45f, 0.47f), scenerySurface = Surface.Rock, style = SceneryStyle.Rings,
            },
            new Biome
            {
                name = "STRIPE PYRAMID", sky = new Color(0.9f, 0.8f, 0.6f), skyTop = new Color(0.55f, 0.6f, 0.75f), skyBottom = new Color(0.9f, 0.75f, 0.55f),
                fogStart = 220f, fogEnd = 1300f,
                ambientSky = new Color(0.9f, 0.85f, 0.7f), ambientEquator = new Color(0.65f, 0.58f, 0.42f), ambientGround = new Color(0.3f, 0.25f, 0.15f),
                sunColor = new Color(1f, 0.9f, 0.7f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampStripe, slab = new Color(0.45f, 0.43f, 0.26f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.75f, 0.25f), glowAlt = new Color(0.4f, 0.9f, 0.6f),
                scenery = new Color(0.55f, 0.52f, 0.32f), scenerySurface = Surface.WallSandstone, style = SceneryStyle.Temple,
            },
            new Biome
            {
                name = "RACE ARENA", sky = new Color(0.15f, 0.15f, 0.18f), skyTop = new Color(0.04f, 0.04f, 0.06f), skyBottom = new Color(0.12f, 0.12f, 0.14f),
                fogStart = 120f, fogEnd = 800f,
                ambientSky = new Color(0.55f, 0.55f, 0.58f), ambientEquator = new Color(0.3f, 0.3f, 0.32f), ambientGround = new Color(0.06f, 0.06f, 0.07f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 0.8f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampRaceTrack, slab = new Color(0.1f, 0.1f, 0.11f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.1f, 0.1f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.2f, 0.2f, 0.22f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Cathedral,
            },
            new Biome
            {
                name = "FRUIT STAGES", sky = new Color(0.85f, 0.95f, 0.7f), skyTop = new Color(0.5f, 0.75f, 0.95f), skyBottom = new Color(0.9f, 0.95f, 0.7f),
                fogStart = 220f, fogEnd = 1300f,
                ambientSky = new Color(0.9f, 0.95f, 0.8f), ambientEquator = new Color(0.65f, 0.75f, 0.5f), ambientGround = new Color(0.3f, 0.35f, 0.2f),
                sunColor = new Color(1f, 1f, 0.9f), sunIntensity = 1.2f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampFruit, slab = new Color(0.35f, 0.55f, 0.15f), slabSurface = Surface.Plaster,
                glow = new Color(0.8f, 0.3f, 1f), glowAlt = new Color(0.6f, 1f, 0.2f),
                scenery = new Color(0.55f, 0.25f, 0.7f), scenerySurface = Surface.Tiles, style = SceneryStyle.Candy,
            },
            new Biome
            {
                name = "SURF TOWN", sky = new Color(0.6f, 0.7f, 0.8f), skyTop = new Color(0.3f, 0.45f, 0.7f), skyBottom = new Color(0.65f, 0.7f, 0.75f),
                fogStart = 200f, fogEnd = 1200f,
                ambientSky = new Color(0.75f, 0.8f, 0.85f), ambientEquator = new Color(0.5f, 0.52f, 0.55f), ambientGround = new Color(0.2f, 0.2f, 0.22f),
                sunColor = new Color(1f, 0.95f, 0.88f), sunIntensity = 1.1f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampTown, slab = new Color(0.2f, 0.2f, 0.21f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.85f, 0.2f), glowAlt = new Color(1f, 0.3f, 0.2f),
                scenery = new Color(0.55f, 0.55f, 0.58f), scenerySurface = Surface.Bricks, style = SceneryStyle.Glass,
            },
            new Biome
            {
                name = "WILD HILLS", sky = new Color(0.6f, 0.8f, 0.95f), skyTop = new Color(0.3f, 0.55f, 0.9f), skyBottom = new Color(0.7f, 0.85f, 0.9f),
                fogStart = 260f, fogEnd = 1500f,
                ambientSky = new Color(0.8f, 0.9f, 0.95f), ambientEquator = new Color(0.55f, 0.65f, 0.5f), ambientGround = new Color(0.2f, 0.28f, 0.15f),
                sunColor = new Color(1f, 0.95f, 0.85f), sunIntensity = 1.3f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampHills, slab = new Color(0.4f, 0.3f, 0.2f), slabSurface = Surface.Rock,
                glow = new Color(1f, 0.9f, 0.4f), glowAlt = new Color(0.4f, 1f, 0.5f),
                scenery = new Color(0.3f, 0.5f, 0.2f), scenerySurface = Surface.WallHedge, style = SceneryStyle.Garden,
            },
            // Real maps from "The Hardest Maps in CS:GO Surf", laid out ramp by ramp after their runs
            // LIMINAL: eight stages after murglegurgle's surf_liminal (my own layouts), between
            // the generated course and the real maps
            new Biome
            {
                name = "NIGHT CITY", sky = new Color(0.12f, 0.13f, 0.25f), skyTop = new Color(0.02f, 0.02f, 0.06f), skyBottom = new Color(0.15f, 0.15f, 0.3f),
                fogStart = 250f, fogEnd = 1500f,
                ambientSky = new Color(0.35f, 0.36f, 0.55f), ambientEquator = new Color(0.25f, 0.25f, 0.4f), ambientGround = new Color(0.1f, 0.1f, 0.15f),
                sunColor = new Color(0.6f, 0.65f, 1.0f), sunIntensity = 0.8f,
                ramp = new Color(0.75f, 0.75f, 0.9f), rampSurface = Surface.RampWhiteGrid, slab = new Color(0.45f, 0.45f, 0.6f), slabSurface = Surface.Concrete,
                glow = new Color(1.0f, 0.75f, 0.35f), glowAlt = new Color(1.0f, 0.4f, 0.3f),
                scenery = new Color(0.55f, 0.55f, 0.75f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Glass,
                floor = new Color(0.2f, 0.2f, 0.28f), floorSurface = Surface.Concrete,
                shaft = new Color(1.0f, 0.8f, 0.5f),
                script = MapScripts.LiminalCity,
                tier = "TIER 5",
                section = "liminal 1/8: the night city",
            },
            new Biome
            {
                name = "THE BACKROOMS", sky = new Color(0.75f, 0.68f, 0.35f), skyTop = new Color(0.6f, 0.55f, 0.3f), skyBottom = new Color(0.7f, 0.62f, 0.35f),
                fogStart = 120f, fogEnd = 700f,
                ambientSky = new Color(0.95f, 0.9f, 0.6f), ambientEquator = new Color(0.8f, 0.72f, 0.4f), ambientGround = new Color(0.45f, 0.4f, 0.25f),
                sunColor = new Color(1.0f, 0.95f, 0.75f), sunIntensity = 0.8f,
                ramp = new Color(0.85f, 0.8f, 0.55f), rampSurface = Surface.RampGallery, slab = new Color(0.8f, 0.72f, 0.45f), slabSurface = Surface.Plaster,
                glow = new Color(1.0f, 0.98f, 0.85f), glowAlt = new Color(0.9f, 0.85f, 0.6f),
                scenery = new Color(0.9f, 0.82f, 0.45f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Gallery,
                floor = new Color(0.6f, 0.55f, 0.38f), floorSurface = Surface.Concrete,
                shaft = new Color(1.0f, 0.95f, 0.75f),
                script = MapScripts.LiminalBackrooms,
                tier = "TIER 5",
                section = "liminal 2/8: the backrooms",
            },
            new Biome
            {
                name = "STARLIT HALL", sky = new Color(0.08f, 0.1f, 0.2f), skyTop = new Color(0.01f, 0.01f, 0.05f), skyBottom = new Color(0.1f, 0.12f, 0.25f),
                fogStart = 250f, fogEnd = 1600f,
                ambientSky = new Color(0.4f, 0.45f, 0.7f), ambientEquator = new Color(0.25f, 0.28f, 0.45f), ambientGround = new Color(0.08f, 0.08f, 0.12f),
                sunColor = new Color(0.7f, 0.75f, 1.0f), sunIntensity = 0.8f,
                ramp = new Color(0.45f, 0.5f, 0.7f), rampSurface = Surface.RampNeon, slab = new Color(0.3f, 0.34f, 0.5f), slabSurface = Surface.Concrete,
                glow = new Color(0.75f, 0.85f, 1.0f), glowAlt = new Color(1.0f, 0.55f, 0.25f),
                scenery = new Color(0.35f, 0.4f, 0.6f), scenerySurface = Surface.WallBlocks, style = SceneryStyle.Gallery,
                floor = new Color(0.18f, 0.2f, 0.3f), floorSurface = Surface.Tiles,
                shaft = new Color(0.75f, 0.85f, 1.0f),
                script = MapScripts.LiminalStarlit,
                tier = "TIER 5",
                section = "liminal 3/8: the starlit hall",
            },
            new Biome
            {
                name = "DARK POOL", sky = new Color(0.06f, 0.05f, 0.08f), skyTop = new Color(0.01f, 0.01f, 0.02f), skyBottom = new Color(0.08f, 0.06f, 0.1f),
                fogStart = 200f, fogEnd = 1300f,
                ambientSky = new Color(0.3f, 0.25f, 0.35f), ambientEquator = new Color(0.2f, 0.15f, 0.22f), ambientGround = new Color(0.05f, 0.05f, 0.06f),
                sunColor = new Color(0.6f, 0.8f, 1.0f), sunIntensity = 0.8f,
                ramp = new Color(0.4f, 0.7f, 0.85f), rampSurface = Surface.RampGlass, slab = new Color(0.25f, 0.22f, 0.28f), slabSurface = Surface.Tiles,
                glow = new Color(0.5f, 0.9f, 1.0f), glowAlt = new Color(1.0f, 0.25f, 0.25f),
                scenery = new Color(0.28f, 0.24f, 0.3f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Gallery,
                floor = new Color(0.1f, 0.3f, 0.4f), floorSurface = Surface.Tiles,
                shaft = new Color(0.5f, 0.9f, 1.0f),
                script = MapScripts.LiminalPool,
                tier = "TIER 5",
                section = "liminal 4/8: the dark pool",
            },
            new Biome
            {
                name = "FLOATING HOUSES", sky = new Color(0.3f, 0.2f, 0.4f), skyTop = new Color(0.08f, 0.05f, 0.15f), skyBottom = new Color(0.35f, 0.25f, 0.4f),
                fogStart = 220f, fogEnd = 1400f,
                ambientSky = new Color(0.6f, 0.5f, 0.75f), ambientEquator = new Color(0.45f, 0.35f, 0.5f), ambientGround = new Color(0.15f, 0.1f, 0.18f),
                sunColor = new Color(1.0f, 0.75f, 0.6f), sunIntensity = 0.8f,
                ramp = new Color(0.75f, 0.65f, 0.85f), rampSurface = Surface.RampWhiteGrid, slab = new Color(0.45f, 0.38f, 0.55f), slabSurface = Surface.Plaster,
                glow = new Color(1.0f, 0.85f, 0.55f), glowAlt = new Color(0.75f, 0.5f, 1.0f),
                scenery = new Color(0.5f, 0.4f, 0.62f), scenerySurface = Surface.WallWood, style = SceneryStyle.Gallery,
                floor = new Color(0.3f, 0.25f, 0.38f), floorSurface = Surface.Concrete,
                shaft = new Color(1.0f, 0.85f, 0.6f),
                script = MapScripts.LiminalHouses,
                tier = "TIER 5",
                section = "liminal 5/8: the floating houses",
            },
            new Biome
            {
                name = "GREEN SHAFT", sky = new Color(0.08f, 0.2f, 0.12f), skyTop = new Color(0.02f, 0.06f, 0.03f), skyBottom = new Color(0.1f, 0.22f, 0.14f),
                fogStart = 180f, fogEnd = 1200f,
                ambientSky = new Color(0.35f, 0.6f, 0.4f), ambientEquator = new Color(0.22f, 0.4f, 0.26f), ambientGround = new Color(0.06f, 0.12f, 0.07f),
                sunColor = new Color(0.6f, 1.0f, 0.7f), sunIntensity = 0.8f,
                ramp = new Color(0.35f, 0.6f, 0.42f), rampSurface = Surface.RampHex, slab = new Color(0.25f, 0.4f, 0.3f), slabSurface = Surface.Tiles,
                glow = new Color(0.35f, 1.0f, 0.5f), glowAlt = new Color(0.9f, 0.35f, 0.3f),
                scenery = new Color(0.3f, 0.5f, 0.36f), scenerySurface = Surface.WallLab, style = SceneryStyle.Lab,
                floor = new Color(0.12f, 0.25f, 0.16f), floorSurface = Surface.Tiles,
                shaft = new Color(0.4f, 1.0f, 0.6f),
                script = MapScripts.LiminalShaft,
                tier = "TIER 6",
                section = "liminal 6/8: the green shaft",
            },
            new Biome
            {
                name = "POOLROOMS", sky = new Color(0.7f, 0.92f, 0.85f), skyTop = new Color(0.5f, 0.8f, 0.75f), skyBottom = new Color(0.75f, 0.95f, 0.9f),
                fogStart = 200f, fogEnd = 1300f,
                ambientSky = new Color(0.9f, 1.0f, 0.95f), ambientEquator = new Color(0.7f, 0.9f, 0.85f), ambientGround = new Color(0.4f, 0.55f, 0.5f),
                sunColor = new Color(0.9f, 1.0f, 0.95f), sunIntensity = 0.8f,
                ramp = new Color(0.85f, 1.0f, 0.95f), rampSurface = Surface.RampWhiteGrid, slab = new Color(0.6f, 0.85f, 0.78f), slabSurface = Surface.WhiteTile,
                glow = new Color(0.95f, 1.0f, 0.98f), glowAlt = new Color(0.4f, 0.9f, 1.0f),
                scenery = new Color(0.7f, 0.95f, 0.87f), scenerySurface = Surface.Tiles, style = SceneryStyle.Gallery,
                floor = new Color(0.45f, 0.75f, 0.75f), floorSurface = Surface.WhiteTile,
                shaft = new Color(0.9f, 1.0f, 1.0f),
                script = MapScripts.LiminalPoolrooms,
                tier = "TIER 6",
                section = "liminal 7/8: the poolrooms",
            },
            new Biome
            {
                name = "NEON STACKS", sky = new Color(0.08f, 0.04f, 0.1f), skyTop = new Color(0.02f, 0.01f, 0.03f), skyBottom = new Color(0.1f, 0.05f, 0.12f),
                fogStart = 220f, fogEnd = 1400f,
                ambientSky = new Color(0.45f, 0.25f, 0.5f), ambientEquator = new Color(0.3f, 0.15f, 0.35f), ambientGround = new Color(0.08f, 0.04f, 0.1f),
                sunColor = new Color(1.0f, 0.4f, 0.7f), sunIntensity = 0.8f,
                ramp = new Color(0.35f, 0.3f, 0.5f), rampSurface = Surface.RampNeon, slab = new Color(0.22f, 0.2f, 0.32f), slabSurface = Surface.Blocks,
                glow = new Color(1.0f, 0.25f, 0.55f), glowAlt = new Color(0.45f, 0.4f, 1.0f),
                scenery = new Color(0.28f, 0.25f, 0.42f), scenerySurface = Surface.WallBlocks, style = SceneryStyle.Synth,
                floor = new Color(0.12f, 0.1f, 0.18f), floorSurface = Surface.Grid,
                shaft = new Color(1.0f, 0.4f, 0.7f),
                script = MapScripts.LiminalNeon,
                tier = "TIER 6",
                section = "liminal 8/8: the neon stacks",
            },
            new Biome
            {
                name = "ESSENTIA", sky = new Color(0.12f, 0.11f, 0.12f), skyTop = new Color(0.03f, 0.03f, 0.04f), skyBottom = new Color(0.6f, 0.4f, 0.4f),
                fogStart = 120f, fogEnd = 800f,
                ambientSky = new Color(0.5f, 0.45f, 0.48f), ambientEquator = new Color(0.28f, 0.25f, 0.27f), ambientGround = new Color(0.08f, 0.07f, 0.07f),
                sunColor = new Color(1f, 0.6f, 0.55f), sunIntensity = 0.6f,
                ramp = new Color(0.72f, 0.72f, 0.74f), rampSurface = Surface.Concrete, slab = new Color(0.18f, 0.18f, 0.2f), slabSurface = Surface.DarkStone,
                glow = new Color(1f, 0.45f, 0.45f), glowAlt = new Color(0.6f, 0.85f, 1f),
                scenery = new Color(0.2f, 0.2f, 0.22f), scenerySurface = Surface.Concrete, style = SceneryStyle.Sunset,
                floor = new Color(0.75f, 0.68f, 0.5f), floorSurface = Surface.Plaster,
                script = MapScripts.Essentia1,
                tier = "TIER 8",
                section = "surf_essentia 1/2",
            },
            new Biome
            {
                name = "ESSENTIA ROCK TUBE", sky = new Color(0.05f, 0.06f, 0.08f), skyTop = new Color(0.01f, 0.01f, 0.02f), skyBottom = new Color(0.04f, 0.05f, 0.07f),
                fogStart = 60f, fogEnd = 450f,
                ambientSky = new Color(0.4f, 0.42f, 0.48f), ambientEquator = new Color(0.2f, 0.21f, 0.25f), ambientGround = new Color(0.05f, 0.05f, 0.06f),
                sunColor = new Color(0.9f, 0.85f, 1f), sunIntensity = 0.5f,
                ramp = new Color(0.65f, 0.65f, 0.68f), rampSurface = Surface.Concrete, slab = new Color(0.2f, 0.2f, 0.22f), slabSurface = Surface.Rock,
                glow = new Color(1f, 0.45f, 0.45f), glowAlt = new Color(0.5f, 0.7f, 1f),
                scenery = new Color(0.18f, 0.19f, 0.22f), scenerySurface = Surface.Rock, style = SceneryStyle.Grotto,
                script = MapScripts.Essentia2,
                tier = "TIER 8",
                section = "surf_essentia 2/2: the last stage",
            },
            new Biome
            {
                name = "STRIKE BONUS", sky = new Color(0f, 0f, 0.01f), skyTop = new Color(0f, 0f, 0f), skyBottom = new Color(0.01f, 0.01f, 0.02f),
                fogStart = 400f, fogEnd = 2500f,
                ambientSky = new Color(0.45f, 0.45f, 0.5f), ambientEquator = new Color(0.25f, 0.25f, 0.3f), ambientGround = new Color(0.05f, 0.05f, 0.06f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 0.8f,
                ramp = new Color(0.32f, 0.32f, 0.34f), rampSurface = Surface.Concrete, slab = new Color(0.3f, 0.3f, 0.32f), slabSurface = Surface.Concrete,
                glow = new Color(0.2f, 0.9f, 1f), glowAlt = new Color(1f, 0.3f, 0.8f),
                scenery = new Color(0.1f, 0.1f, 0.12f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Rings,
                script = MapScripts.Strike,
                tier = "BONUS",
                section = "surf_strike bonus 1",
            },
            new Biome
            {
                name = "BLACKHEART", sky = new Color(0.25f, 0.01f, 0.02f), skyTop = new Color(0.05f, 0f, 0f), skyBottom = new Color(0.1f, 0f, 0.01f),
                fogStart = 40f, fogEnd = 350f,
                ambientSky = new Color(0.6f, 0.1f, 0.1f), ambientEquator = new Color(0.3f, 0.05f, 0.05f), ambientGround = new Color(0.05f, 0.02f, 0.02f),
                sunColor = new Color(1f, 0.2f, 0.2f), sunIntensity = 0.5f,
                ramp = new Color(0.12f, 0.2f, 0.15f), rampSurface = Surface.Rock, slab = new Color(0.2f, 0.18f, 0.08f), slabSurface = Surface.Rock,
                glow = new Color(1f, 0.05f, 0.05f), glowAlt = new Color(1f, 0.3f, 0.2f),
                scenery = new Color(0.15f, 0.18f, 0.08f), scenerySurface = Surface.Rock, style = SceneryStyle.Grotto,
                script = MapScripts.Blackheart,
                tier = "TIER 6",
                section = "surf_blackheart",
            },
            new Biome
            {
                name = "TROFLE", sky = new Color(0.25f, 0.2f, 0.45f), skyTop = new Color(0.05f, 0.04f, 0.1f), skyBottom = new Color(0.2f, 0.18f, 0.35f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.7f, 0.65f, 0.9f), ambientEquator = new Color(0.4f, 0.36f, 0.55f), ambientGround = new Color(0.1f, 0.09f, 0.14f),
                sunColor = new Color(0.85f, 0.8f, 1f), sunIntensity = 0.7f,
                ramp = new Color(0.72f, 0.58f, 0.4f), rampSurface = Surface.Wood, slab = new Color(0.85f, 0.84f, 0.9f), slabSurface = Surface.Plaster,
                glow = new Color(0.75f, 0.65f, 1f), glowAlt = new Color(1f, 0.95f, 0.85f),
                scenery = new Color(0.88f, 0.88f, 0.92f), scenerySurface = Surface.Plaster, style = SceneryStyle.Lab,
                script = MapScripts.Trofle1,
                tier = "TIER 8+",
                section = "surf_trofle 1/3: broken ramps",
            },
            new Biome
            {
                name = "TROFLE", sky = new Color(0.25f, 0.2f, 0.45f), skyTop = new Color(0.05f, 0.04f, 0.1f), skyBottom = new Color(0.2f, 0.18f, 0.35f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.7f, 0.65f, 0.9f), ambientEquator = new Color(0.4f, 0.36f, 0.55f), ambientGround = new Color(0.1f, 0.09f, 0.14f),
                sunColor = new Color(0.85f, 0.8f, 1f), sunIntensity = 0.7f,
                ramp = new Color(0.72f, 0.58f, 0.4f), rampSurface = Surface.Wood, slab = new Color(0.85f, 0.84f, 0.9f), slabSurface = Surface.Plaster,
                glow = new Color(0.75f, 0.65f, 1f), glowAlt = new Color(1f, 0.95f, 0.85f),
                scenery = new Color(0.88f, 0.88f, 0.92f), scenerySurface = Surface.Plaster, style = SceneryStyle.Lab,
                script = MapScripts.Trofle2,
                tier = "TIER 8+",
                section = "surf_trofle 2/3: sinsane",
                continues = true,
            },
            new Biome
            {
                name = "TROFLE HAZE", sky = new Color(0.78f, 0.68f, 0.52f), skyTop = new Color(0.6f, 0.55f, 0.48f), skyBottom = new Color(0.75f, 0.65f, 0.5f),
                fogStart = 20f, fogEnd = 260f,
                ambientSky = new Color(0.85f, 0.78f, 0.65f), ambientEquator = new Color(0.7f, 0.62f, 0.5f), ambientGround = new Color(0.45f, 0.4f, 0.32f),
                sunColor = new Color(1f, 0.92f, 0.8f), sunIntensity = 0.8f,
                ramp = new Color(0.78f, 0.68f, 0.52f), rampSurface = Surface.Plaster, slab = new Color(0.78f, 0.68f, 0.52f), slabSurface = Surface.Plaster,
                glow = new Color(0.78f, 0.68f, 0.52f), glowAlt = new Color(0.5f, 0.7f, 0.3f),
                scenery = new Color(0.78f, 0.68f, 0.52f), scenerySurface = Surface.Plaster, style = SceneryStyle.Palace,
                script = MapScripts.Trofle3,
                tier = "TIER 8+",
                section = "surf_trofle 3/3: you cannot see the ramps",
            },
            new Biome
            {
                name = "SPIN", sky = new Color(0.35f, 0.38f, 0.45f), skyTop = new Color(0.1f, 0.12f, 0.18f), skyBottom = new Color(0.3f, 0.32f, 0.38f),
                fogStart = 200f, fogEnd = 1400f,
                ambientSky = new Color(0.75f, 0.75f, 0.8f), ambientEquator = new Color(0.45f, 0.45f, 0.5f), ambientGround = new Color(0.15f, 0.15f, 0.18f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.Concrete, slab = new Color(0.5f, 0.5f, 0.52f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 1f, 1f), glowAlt = new Color(1f, 0.85f, 0.3f),
                scenery = new Color(0.4f, 0.4f, 0.42f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Spectrum,
                hues = new[] { new Color(0.85f, 0.75f, 0.55f), new Color(0.6f, 1f, 0.2f), new Color(0.3f, 0.85f, 0.9f), new Color(0.95f, 0.92f, 0.85f), new Color(0.9f, 0.9f, 0.92f), new Color(0.95f, 0.75f, 0.2f) },
                script = MapScripts.Spin1,
                tier = "TIER 8+",
                section = "surf_spin 1/2",
            },
            new Biome
            {
                name = "SPIN II", sky = new Color(0.12f, 0.13f, 0.18f), skyTop = new Color(0.02f, 0.02f, 0.04f), skyBottom = new Color(0.1f, 0.1f, 0.14f),
                fogStart = 200f, fogEnd = 1400f,
                ambientSky = new Color(0.75f, 0.75f, 0.8f), ambientEquator = new Color(0.45f, 0.45f, 0.5f), ambientGround = new Color(0.15f, 0.15f, 0.18f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.Concrete, slab = new Color(0.5f, 0.5f, 0.52f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 1f, 1f), glowAlt = new Color(1f, 0.85f, 0.3f),
                scenery = new Color(0.4f, 0.4f, 0.42f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Spectrum,
                hues = new[] { new Color(0.85f, 0.72f, 0.5f), new Color(0.6f, 0.45f, 0.3f), new Color(0.75f, 0.75f, 0.78f), new Color(0.9f, 0.9f, 0.95f), new Color(0.25f, 0.25f, 0.35f), new Color(1f, 1f, 1f) },
                script = MapScripts.Spin2,
                tier = "TIER 8+",
                section = "surf_spin 2/2",
            },
            new Biome
            {
                name = "BEFORE", sky = new Color(0.12f, 0.12f, 0.12f), skyTop = new Color(0.03f, 0.03f, 0.03f), skyBottom = new Color(0.1f, 0.1f, 0.1f),
                fogStart = 60f, fogEnd = 450f,
                ambientSky = new Color(0.5f, 0.5f, 0.5f), ambientEquator = new Color(0.28f, 0.28f, 0.28f), ambientGround = new Color(0.07f, 0.07f, 0.07f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.55f, 0.55f, 0.55f), rampSurface = Surface.Concrete, slab = new Color(0.3f, 0.3f, 0.3f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.15f, 0.1f), glowAlt = new Color(0.7f, 0.7f, 0.7f),
                scenery = new Color(0.4f, 0.4f, 0.4f), scenerySurface = Surface.Concrete, style = SceneryStyle.Mine,
                floor = new Color(0.15f, 0.14f, 0.13f), floorSurface = Surface.Rock,
                script = MapScripts.Before1,
                tier = "TIER 7",
                section = "surf_before: 666 b4, resource b4",
            },
            new Biome
            {
                name = "BEFORE II", sky = new Color(0.06f, 0.12f, 0.12f), skyTop = new Color(0.03f, 0.03f, 0.03f), skyBottom = new Color(0.1f, 0.1f, 0.1f),
                fogStart = 60f, fogEnd = 450f,
                ambientSky = new Color(0.5f, 0.5f, 0.5f), ambientEquator = new Color(0.28f, 0.28f, 0.28f), ambientGround = new Color(0.07f, 0.07f, 0.07f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.45f, 0.6f, 0.6f), rampSurface = Surface.Concrete, slab = new Color(0.3f, 0.42f, 0.42f), slabSurface = Surface.Concrete,
                glow = new Color(0.6f, 1f, 0.95f), glowAlt = new Color(0.7f, 0.7f, 0.7f),
                scenery = new Color(0.35f, 0.5f, 0.5f), scenerySurface = Surface.Concrete, style = SceneryStyle.Mine,
                floor = new Color(0.15f, 0.14f, 0.13f), floorSurface = Surface.Rock,
                script = MapScripts.Before2,
                tier = "TIER 7",
                section = "surf_before: technique b4, goliath b4",
            },
            new Biome
            {
                name = "BEFORE III", sky = new Color(0.14f, 0.1f, 0.09f), skyTop = new Color(0.03f, 0.03f, 0.03f), skyBottom = new Color(0.1f, 0.1f, 0.1f),
                fogStart = 60f, fogEnd = 450f,
                ambientSky = new Color(0.5f, 0.5f, 0.5f), ambientEquator = new Color(0.28f, 0.28f, 0.28f), ambientGround = new Color(0.07f, 0.07f, 0.07f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.62f, 0.5f, 0.48f), rampSurface = Surface.Concrete, slab = new Color(0.5f, 0.4f, 0.38f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.7f, 0.5f), glowAlt = new Color(0.7f, 0.7f, 0.7f),
                scenery = new Color(0.55f, 0.42f, 0.42f), scenerySurface = Surface.Plaster, style = SceneryStyle.Mine,
                floor = new Color(0.15f, 0.14f, 0.13f), floorSurface = Surface.Rock,
                accent = new Color(0.5f, 0.35f, 0.2f), accentSurface = Surface.WallWood,
                script = MapScripts.Before3,
                tier = "TIER 7",
                section = "surf_before: modern b4, facility b4",
            },
            new Biome
            {
                name = "FRAGS NIGHTMARE", sky = new Color(0.15f, 0.15f, 0.2f), skyTop = new Color(0.04f, 0.04f, 0.06f), skyBottom = new Color(0.12f, 0.12f, 0.16f),
                fogStart = 150f, fogEnd = 900f,
                ambientSky = new Color(0.75f, 0.75f, 0.8f), ambientEquator = new Color(0.45f, 0.45f, 0.5f), ambientGround = new Color(0.15f, 0.15f, 0.18f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 0.9f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampDevGrid, slab = new Color(0.15f, 0.25f, 0.65f), slabSurface = Surface.Plaster,
                glow = new Color(0.2f, 0.9f, 0.8f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.15f, 0.25f, 0.65f), scenerySurface = Surface.Plaster, style = SceneryStyle.Lab,
                floor = new Color(0.75f, 0.75f, 0.78f), floorSurface = Surface.Tiles,
                accent = new Color(0.2f, 0.55f, 0.5f), accentSurface = Surface.Plaster,
                script = MapScripts.Frags1,
                tier = "TIER 8+",
                section = "surf_frags_nightmare 1/2",
            },
            new Biome
            {
                name = "FRAGS NIGHTMARE", sky = new Color(0.15f, 0.15f, 0.2f), skyTop = new Color(0.04f, 0.04f, 0.06f), skyBottom = new Color(0.12f, 0.12f, 0.16f),
                fogStart = 150f, fogEnd = 900f,
                ambientSky = new Color(0.75f, 0.75f, 0.8f), ambientEquator = new Color(0.45f, 0.45f, 0.5f), ambientGround = new Color(0.15f, 0.15f, 0.18f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 0.9f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampDevGrid, slab = new Color(0.15f, 0.25f, 0.65f), slabSurface = Surface.Plaster,
                glow = new Color(0.2f, 0.9f, 0.8f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.15f, 0.25f, 0.65f), scenerySurface = Surface.Plaster, style = SceneryStyle.Lab,
                floor = new Color(0.75f, 0.75f, 0.78f), floorSurface = Surface.Tiles,
                accent = new Color(0.2f, 0.55f, 0.5f), accentSurface = Surface.Plaster,
                script = MapScripts.Frags2,
                tier = "TIER 8+",
                section = "surf_frags_nightmare 2/2: the unbeaten spin",
                continues = true,
            },
            new Biome
            {
                name = "SURF CORRUPTION", sky = new Color(0.18f, 0.03f, 0.22f), skyTop = new Color(0.04f, 0f, 0.06f), skyBottom = new Color(0.12f, 0.02f, 0.15f),
                fogStart = 60f, fogEnd = 500f,
                ambientSky = new Color(0.6f, 0.35f, 0.7f), ambientEquator = new Color(0.32f, 0.15f, 0.38f), ambientGround = new Color(0.08f, 0.03f, 0.1f),
                sunColor = new Color(0.95f, 0.8f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.88f, 0.88f, 0.9f), rampSurface = Surface.Concrete, slab = new Color(0.3f, 0.08f, 0.32f), slabSurface = Surface.Stone,
                glow = new Color(0.85f, 0.3f, 1f), glowAlt = new Color(0.3f, 0.9f, 0.9f),
                scenery = new Color(0.35f, 0.1f, 0.38f), scenerySurface = Surface.Bricks, style = SceneryStyle.Grotto,
                script = MapScripts.Corruption1,
                tier = "TIER 8",
                section = "surf_corruption 1/3",
            },
            new Biome
            {
                name = "SURF CORRUPTION II", sky = new Color(0.03f, 0.15f, 0.18f), skyTop = new Color(0.01f, 0.04f, 0.05f), skyBottom = new Color(0.03f, 0.12f, 0.15f),
                fogStart = 60f, fogEnd = 500f,
                ambientSky = new Color(0.35f, 0.6f, 0.65f), ambientEquator = new Color(0.15f, 0.32f, 0.35f), ambientGround = new Color(0.03f, 0.08f, 0.1f),
                sunColor = new Color(0.95f, 0.8f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.88f, 0.88f, 0.9f), rampSurface = Surface.Concrete, slab = new Color(0.08f, 0.3f, 0.32f), slabSurface = Surface.Stone,
                glow = new Color(0.3f, 1f, 0.9f), glowAlt = new Color(0.85f, 0.3f, 1f),
                scenery = new Color(0.1f, 0.35f, 0.38f), scenerySurface = Surface.Rock, style = SceneryStyle.Grotto,
                script = MapScripts.Corruption2,
                tier = "TIER 8",
                section = "surf_corruption 2/3: the water cave",
            },
            new Biome
            {
                name = "SURF CORRUPTION III", sky = new Color(0.7f, 0.6f, 0.9f), skyTop = new Color(0.35f, 0.35f, 0.75f), skyBottom = new Color(0.6f, 0.6f, 0.8f),
                fogStart = 250f, fogEnd = 1500f,
                ambientSky = new Color(0.85f, 0.8f, 0.95f), ambientEquator = new Color(0.6f, 0.55f, 0.7f), ambientGround = new Color(0.3f, 0.25f, 0.35f),
                sunColor = new Color(0.95f, 0.8f, 1f), sunIntensity = 1.1f,
                ramp = new Color(0.88f, 0.88f, 0.9f), rampSurface = Surface.Concrete, slab = new Color(0.3f, 0.08f, 0.32f), slabSurface = Surface.Stone,
                glow = new Color(0.85f, 0.3f, 1f), glowAlt = new Color(0.3f, 0.9f, 0.9f),
                scenery = new Color(0.35f, 0.1f, 0.38f), scenerySurface = Surface.Bricks, style = SceneryStyle.Palace,
                script = MapScripts.Corruption3,
                tier = "TIER 8",
                section = "surf_corruption 3/3: out to the final skip",
            },
            new Biome
            {
                name = "SURF DEITY", sky = new Color(0.15f, 0.08f, 0.04f), skyTop = new Color(0.03f, 0.02f, 0.01f), skyBottom = new Color(0.1f, 0.05f, 0.03f),
                fogStart = 70f, fogEnd = 520f,
                ambientSky = new Color(0.75f, 0.5f, 0.35f), ambientEquator = new Color(0.45f, 0.28f, 0.18f), ambientGround = new Color(0.12f, 0.07f, 0.04f),
                sunColor = new Color(1f, 0.7f, 0.45f), sunIntensity = 0.7f,
                ramp = new Color(0.85f, 0.5f, 0.28f), rampSurface = Surface.Metal, slab = new Color(0.35f, 0.2f, 0.12f), slabSurface = Surface.Bricks,
                glow = new Color(1f, 0.6f, 0.25f), glowAlt = new Color(1f, 0.85f, 0.5f),
                scenery = new Color(0.4f, 0.22f, 0.14f), scenerySurface = Surface.Bricks, style = SceneryStyle.Library,
                floor = new Color(0.3f, 0.18f, 0.1f), floorSurface = Surface.Wood,
                accent = new Color(0.45f, 0.28f, 0.15f), accentSurface = Surface.WallWood,
                script = MapScripts.Deity1,
                tier = "TIER 8",
                section = "surf_deity 1/2",
            },
            new Biome
            {
                name = "SURF DEITY", sky = new Color(0.15f, 0.08f, 0.04f), skyTop = new Color(0.03f, 0.02f, 0.01f), skyBottom = new Color(0.1f, 0.05f, 0.03f),
                fogStart = 70f, fogEnd = 520f,
                ambientSky = new Color(0.75f, 0.5f, 0.35f), ambientEquator = new Color(0.45f, 0.28f, 0.18f), ambientGround = new Color(0.12f, 0.07f, 0.04f),
                sunColor = new Color(1f, 0.7f, 0.45f), sunIntensity = 0.7f,
                ramp = new Color(0.85f, 0.5f, 0.28f), rampSurface = Surface.Metal, slab = new Color(0.35f, 0.2f, 0.12f), slabSurface = Surface.Bricks,
                glow = new Color(1f, 0.6f, 0.25f), glowAlt = new Color(1f, 0.85f, 0.5f),
                scenery = new Color(0.4f, 0.22f, 0.14f), scenerySurface = Surface.Bricks, style = SceneryStyle.Library,
                floor = new Color(0.3f, 0.18f, 0.1f), floorSurface = Surface.Wood,
                accent = new Color(0.45f, 0.28f, 0.15f), accentSurface = Surface.WallWood,
                script = MapScripts.Deity2,
                tier = "TIER 8",
                section = "surf_deity 2/2",
                continues = true,
            },
            new Biome
            {
                name = "SURF ANUBIS", sky = new Color(0.25f, 0.16f, 0.08f), skyTop = new Color(0.05f, 0.03f, 0.01f), skyBottom = new Color(0.15f, 0.09f, 0.04f),
                fogStart = 60f, fogEnd = 450f,
                ambientSky = new Color(0.85f, 0.6f, 0.35f), ambientEquator = new Color(0.5f, 0.34f, 0.18f), ambientGround = new Color(0.14f, 0.09f, 0.05f),
                sunColor = new Color(1f, 0.75f, 0.45f), sunIntensity = 0.7f,
                ramp = new Color(0.78f, 0.6f, 0.38f), rampSurface = Surface.Plaster, slab = new Color(0.62f, 0.46f, 0.28f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.6f, 0.2f), glowAlt = new Color(1f, 0.8f, 0.4f),
                scenery = new Color(0.7f, 0.52f, 0.32f), scenerySurface = Surface.WallSandstone, style = SceneryStyle.Temple,
                floor = new Color(0.05f, 0.04f, 0.03f), floorSurface = Surface.DarkStone,
                script = MapScripts.Anubis1,
                tier = "TIER 8",
                section = "surf_anubis 1/2",
            },
            new Biome
            {
                name = "SURF ANUBIS", sky = new Color(0.25f, 0.16f, 0.08f), skyTop = new Color(0.05f, 0.03f, 0.01f), skyBottom = new Color(0.15f, 0.09f, 0.04f),
                fogStart = 60f, fogEnd = 450f,
                ambientSky = new Color(0.85f, 0.6f, 0.35f), ambientEquator = new Color(0.5f, 0.34f, 0.18f), ambientGround = new Color(0.14f, 0.09f, 0.05f),
                sunColor = new Color(1f, 0.75f, 0.45f), sunIntensity = 0.7f,
                ramp = new Color(0.78f, 0.6f, 0.38f), rampSurface = Surface.Plaster, slab = new Color(0.62f, 0.46f, 0.28f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.6f, 0.2f), glowAlt = new Color(1f, 0.8f, 0.4f),
                scenery = new Color(0.7f, 0.52f, 0.32f), scenerySurface = Surface.WallSandstone, style = SceneryStyle.Temple,
                floor = new Color(0.05f, 0.04f, 0.03f), floorSurface = Surface.DarkStone,
                script = MapScripts.Anubis2,
                tier = "TIER 8",
                section = "surf_anubis 2/2",
                continues = true,
            },
            // The hardest maps found besides (tier 7 on SurfHeaven, fewest completions), then the finale
            new Biome
            {
                name = "SURF ORIGINAL", sky = new Color(0.4f, 0.42f, 0.45f), skyTop = new Color(0.2f, 0.3f, 0.5f), skyBottom = new Color(0.35f, 0.36f, 0.38f),
                fogStart = 150f, fogEnd = 1000f,
                ambientSky = new Color(0.75f, 0.75f, 0.75f), ambientEquator = new Color(0.45f, 0.45f, 0.45f), ambientGround = new Color(0.15f, 0.15f, 0.15f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.Plaster, slab = new Color(0.45f, 0.45f, 0.45f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.9f, 0.6f), glowAlt = new Color(0.4f, 1f, 0.4f),
                scenery = new Color(0.5f, 0.5f, 0.5f), scenerySurface = Surface.Concrete, style = SceneryStyle.Gallery,
                hues = new[] { new Color(0.6f, 0.6f, 0.6f), new Color(0.6f, 0.12f, 0.1f), new Color(0.85f, 0.7f, 0.35f), new Color(0.35f, 0.22f, 0.12f), new Color(0.8f, 0.65f, 0.45f), new Color(0.3f, 0.4f, 0.2f) },
                script = MapScripts.Original1,
                tier = "TIER 7",
                section = "surf_original 1/2",
            },
            new Biome
            {
                name = "SURF ORIGINAL II", sky = new Color(0.4f, 0.42f, 0.45f), skyTop = new Color(0.2f, 0.3f, 0.5f), skyBottom = new Color(0.35f, 0.36f, 0.38f),
                fogStart = 150f, fogEnd = 1000f,
                ambientSky = new Color(0.75f, 0.75f, 0.75f), ambientEquator = new Color(0.45f, 0.45f, 0.45f), ambientGround = new Color(0.15f, 0.15f, 0.15f),
                sunColor = new Color(1f, 1f, 1f), sunIntensity = 1.0f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.Plaster, slab = new Color(0.45f, 0.45f, 0.45f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.9f, 0.6f), glowAlt = new Color(0.4f, 1f, 0.4f),
                scenery = new Color(0.5f, 0.5f, 0.5f), scenerySurface = Surface.Concrete, style = SceneryStyle.Gallery,
                hues = new[] { new Color(0.6f, 0.45f, 0.28f), new Color(0.6f, 0.3f, 0.22f), new Color(1f, 0.45f, 0.15f), new Color(0.3f, 0.9f, 0.4f), new Color(0.15f, 0.15f, 0.15f), new Color(0.55f, 0.55f, 0.55f) },
                script = MapScripts.Original2,
                tier = "TIER 7",
                section = "surf_original 2/2",
            },
            new Biome
            {
                name = "NOT SO ZEN", sky = new Color(0.75f, 0.75f, 0.72f), skyTop = new Color(0.5f, 0.55f, 0.62f), skyBottom = new Color(0.8f, 0.75f, 0.65f),
                fogStart = 150f, fogEnd = 1000f,
                ambientSky = new Color(0.8f, 0.8f, 0.78f), ambientEquator = new Color(0.55f, 0.55f, 0.52f), ambientGround = new Color(0.25f, 0.24f, 0.22f),
                sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.0f,
                ramp = new Color(0.18f, 0.18f, 0.2f), rampSurface = Surface.Concrete, slab = new Color(0.7f, 0.7f, 0.68f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 1f, 1f), glowAlt = new Color(0.85f, 0.7f, 0.5f),
                scenery = new Color(0.72f, 0.72f, 0.7f), scenerySurface = Surface.Concrete, style = SceneryStyle.Temple,
                floor = new Color(0.8f, 0.68f, 0.5f), floorSurface = Surface.Plaster,
                script = MapScripts.Zen1,
                tier = "TIER 7",
                section = "surf_not_so_zen 1/2",
            },
            new Biome
            {
                name = "NOT SO ZEN", sky = new Color(0.75f, 0.75f, 0.72f), skyTop = new Color(0.5f, 0.55f, 0.62f), skyBottom = new Color(0.8f, 0.75f, 0.65f),
                fogStart = 150f, fogEnd = 1000f,
                ambientSky = new Color(0.8f, 0.8f, 0.78f), ambientEquator = new Color(0.55f, 0.55f, 0.52f), ambientGround = new Color(0.25f, 0.24f, 0.22f),
                sunColor = new Color(1f, 0.97f, 0.9f), sunIntensity = 1.0f,
                ramp = new Color(0.18f, 0.18f, 0.2f), rampSurface = Surface.Concrete, slab = new Color(0.7f, 0.7f, 0.68f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 1f, 1f), glowAlt = new Color(0.85f, 0.7f, 0.5f),
                scenery = new Color(0.72f, 0.72f, 0.7f), scenerySurface = Surface.Concrete, style = SceneryStyle.Temple,
                floor = new Color(0.8f, 0.68f, 0.5f), floorSurface = Surface.Plaster,
                script = MapScripts.Zen2,
                tier = "TIER 7",
                section = "surf_not_so_zen 2/2",
                continues = true,
            },
            new Biome
            {
                name = "NOT SO FUNHOUSE", sky = new Color(0.5f, 0.12f, 0.05f), skyTop = new Color(0.2f, 0.03f, 0.02f), skyBottom = new Color(0.4f, 0.1f, 0.05f),
                fogStart = 70f, fogEnd = 500f,
                ambientSky = new Color(0.7f, 0.3f, 0.25f), ambientEquator = new Color(0.4f, 0.15f, 0.12f), ambientGround = new Color(0.1f, 0.03f, 0.03f),
                sunColor = new Color(1f, 0.5f, 0.35f), sunIntensity = 0.7f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampFunhouse, slab = new Color(0.1f, 0.02f, 0.02f), slabSurface = Surface.DarkStone,
                glow = new Color(0.2f, 1f, 1f), glowAlt = new Color(1f, 0.15f, 0.1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.RampFunhouse, style = SceneryStyle.Synth,
                script = MapScripts.Funhouse1,
                tier = "TIER 7",
                section = "surf_not_so_funhouse 1/2",
            },
            new Biome
            {
                name = "NOT SO FUNHOUSE", sky = new Color(0.5f, 0.12f, 0.05f), skyTop = new Color(0.2f, 0.03f, 0.02f), skyBottom = new Color(0.4f, 0.1f, 0.05f),
                fogStart = 70f, fogEnd = 500f,
                ambientSky = new Color(0.7f, 0.3f, 0.25f), ambientEquator = new Color(0.4f, 0.15f, 0.12f), ambientGround = new Color(0.1f, 0.03f, 0.03f),
                sunColor = new Color(1f, 0.5f, 0.35f), sunIntensity = 0.7f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampFunhouse, slab = new Color(0.1f, 0.02f, 0.02f), slabSurface = Surface.DarkStone,
                glow = new Color(0.2f, 1f, 1f), glowAlt = new Color(1f, 0.15f, 0.1f),
                scenery = new Color(1f, 1f, 1f), scenerySurface = Surface.RampFunhouse, style = SceneryStyle.Synth,
                script = MapScripts.Funhouse2,
                tier = "TIER 7",
                section = "surf_not_so_funhouse 2/2",
                continues = true,
            },
            new Biome
            {
                name = "TENSOR", sky = new Color(0.12f, 0.16f, 0.22f), skyTop = new Color(0.03f, 0.05f, 0.08f), skyBottom = new Color(0.1f, 0.13f, 0.18f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.5f, 0.6f, 0.72f), ambientEquator = new Color(0.28f, 0.34f, 0.42f), ambientGround = new Color(0.07f, 0.08f, 0.1f),
                sunColor = new Color(0.85f, 0.92f, 1f), sunIntensity = 0.7f,
                ramp = new Color(0.55f, 0.6f, 0.66f), rampSurface = Surface.Concrete, slab = new Color(0.35f, 0.42f, 0.5f), slabSurface = Surface.Concrete,
                glow = new Color(0.85f, 0.95f, 1f), glowAlt = new Color(0.6f, 0.45f, 0.3f),
                scenery = new Color(0.4f, 0.48f, 0.56f), scenerySurface = Surface.Concrete, style = SceneryStyle.Lab,
                floor = new Color(0.2f, 0.2f, 0.22f), floorSurface = Surface.Rock,
                script = MapScripts.Tensor1,
                tier = "TIER 7",
                section = "surf_tensor2 1/3",
            },
            new Biome
            {
                name = "TENSOR", sky = new Color(0.12f, 0.16f, 0.22f), skyTop = new Color(0.03f, 0.05f, 0.08f), skyBottom = new Color(0.1f, 0.13f, 0.18f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.5f, 0.6f, 0.72f), ambientEquator = new Color(0.28f, 0.34f, 0.42f), ambientGround = new Color(0.07f, 0.08f, 0.1f),
                sunColor = new Color(0.85f, 0.92f, 1f), sunIntensity = 0.7f,
                ramp = new Color(0.55f, 0.6f, 0.66f), rampSurface = Surface.Concrete, slab = new Color(0.35f, 0.42f, 0.5f), slabSurface = Surface.Concrete,
                glow = new Color(0.85f, 0.95f, 1f), glowAlt = new Color(0.6f, 0.45f, 0.3f),
                scenery = new Color(0.4f, 0.48f, 0.56f), scenerySurface = Surface.Concrete, style = SceneryStyle.Lab,
                floor = new Color(0.2f, 0.2f, 0.22f), floorSurface = Surface.Rock,
                script = MapScripts.Tensor2,
                tier = "TIER 7",
                section = "surf_tensor2 2/3",
                continues = true,
            },
            new Biome
            {
                name = "TENSOR", sky = new Color(0.12f, 0.16f, 0.22f), skyTop = new Color(0.03f, 0.05f, 0.08f), skyBottom = new Color(0.1f, 0.13f, 0.18f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.5f, 0.6f, 0.72f), ambientEquator = new Color(0.28f, 0.34f, 0.42f), ambientGround = new Color(0.07f, 0.08f, 0.1f),
                sunColor = new Color(0.85f, 0.92f, 1f), sunIntensity = 0.7f,
                ramp = new Color(0.55f, 0.6f, 0.66f), rampSurface = Surface.Concrete, slab = new Color(0.35f, 0.42f, 0.5f), slabSurface = Surface.Concrete,
                glow = new Color(0.85f, 0.95f, 1f), glowAlt = new Color(0.6f, 0.45f, 0.3f),
                scenery = new Color(0.4f, 0.48f, 0.56f), scenerySurface = Surface.Concrete, style = SceneryStyle.Lab,
                floor = new Color(0.2f, 0.2f, 0.22f), floorSurface = Surface.Rock,
                script = MapScripts.Tensor3,
                tier = "TIER 7",
                section = "surf_tensor2 3/3",
                continues = true,
            },
            new Biome
            {
                name = "SHADE", sky = new Color(0.08f, 0.1f, 0.16f), skyTop = new Color(0.02f, 0.03f, 0.06f), skyBottom = new Color(0.15f, 0.17f, 0.22f),
                fogStart = 120f, fogEnd = 800f,
                ambientSky = new Color(0.35f, 0.4f, 0.5f), ambientEquator = new Color(0.2f, 0.22f, 0.28f), ambientGround = new Color(0.05f, 0.06f, 0.08f),
                sunColor = new Color(0.75f, 0.8f, 1f), sunIntensity = 0.45f,
                ramp = new Color(0.6f, 0.5f, 0.28f), rampSurface = Surface.Plaster, slab = new Color(0.25f, 0.25f, 0.27f), slabSurface = Surface.Concrete,
                glow = new Color(1f, 0.85f, 0.5f), glowAlt = new Color(0.4f, 0.9f, 0.3f),
                scenery = new Color(0.28f, 0.28f, 0.3f), scenerySurface = Surface.Concrete, style = SceneryStyle.Garden,
                floor = new Color(0.25f, 0.55f, 0.15f), floorSurface = Surface.WallHedge,
                script = MapScripts.Shade,
                tier = "TIER 7",
                section = "surf_shade",
            },
            new Biome
            {
                name = "TECHSLOP", sky = new Color(0.06f, 0.07f, 0.1f), skyTop = new Color(0.01f, 0.01f, 0.02f), skyBottom = new Color(0.05f, 0.06f, 0.08f),
                fogStart = 60f, fogEnd = 450f,
                ambientSky = new Color(0.45f, 0.48f, 0.55f), ambientEquator = new Color(0.24f, 0.26f, 0.3f), ambientGround = new Color(0.06f, 0.06f, 0.08f),
                sunColor = new Color(0.85f, 0.9f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.5f, 0.5f, 0.5f), rampSurface = Surface.Stone, slab = new Color(0.12f, 0.14f, 0.22f), slabSurface = Surface.Panel,
                glow = new Color(0.3f, 1f, 0.5f), glowAlt = new Color(0.3f, 0.9f, 1f),
                scenery = new Color(0.15f, 0.17f, 0.25f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Wire,
                script = MapScripts.Techslop1,
                tier = "TIER 7",
                section = "surf_techslop 1/2",
            },
            new Biome
            {
                name = "TECHSLOP RED ROOM", sky = new Color(0.1f, 0.03f, 0.03f), skyTop = new Color(0.01f, 0.01f, 0.02f), skyBottom = new Color(0.05f, 0.06f, 0.08f),
                fogStart = 60f, fogEnd = 450f,
                ambientSky = new Color(0.45f, 0.48f, 0.55f), ambientEquator = new Color(0.24f, 0.26f, 0.3f), ambientGround = new Color(0.06f, 0.06f, 0.08f),
                sunColor = new Color(0.85f, 0.9f, 1f), sunIntensity = 0.6f,
                ramp = new Color(0.5f, 0.5f, 0.5f), rampSurface = Surface.Stone, slab = new Color(0.35f, 0.08f, 0.08f), slabSurface = Surface.Panel,
                glow = new Color(1f, 0.2f, 0.15f), glowAlt = new Color(0.3f, 0.9f, 1f),
                scenery = new Color(0.3f, 0.08f, 0.08f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Wire,
                script = MapScripts.Techslop2,
                tier = "TIER 7",
                section = "surf_techslop 2/2",
            },
            new Biome
            {
                name = "GIGAPEDE", sky = new Color(0.12f, 0.08f, 0.04f), skyTop = new Color(0.03f, 0.02f, 0.01f), skyBottom = new Color(0.1f, 0.06f, 0.03f),
                fogStart = 70f, fogEnd = 500f,
                ambientSky = new Color(0.55f, 0.4f, 0.25f), ambientEquator = new Color(0.3f, 0.22f, 0.14f), ambientGround = new Color(0.08f, 0.05f, 0.03f),
                sunColor = new Color(1f, 0.75f, 0.5f), sunIntensity = 0.6f,
                ramp = new Color(0.85f, 0.7f, 0.45f), rampSurface = Surface.Plaster, slab = new Color(0.3f, 0.24f, 0.14f), slabSurface = Surface.Stone,
                glow = new Color(1f, 0.1f, 0.05f), glowAlt = new Color(1f, 0.4f, 0.1f),
                scenery = new Color(0.32f, 0.26f, 0.16f), scenerySurface = Surface.Blocks, style = SceneryStyle.Forge,
                script = MapScripts.Gigapede1,
                tier = "TIER 7",
                section = "surf_gigapede 1/2",
            },
            new Biome
            {
                name = "GIGAPEDE", sky = new Color(0.12f, 0.08f, 0.04f), skyTop = new Color(0.03f, 0.02f, 0.01f), skyBottom = new Color(0.1f, 0.06f, 0.03f),
                fogStart = 70f, fogEnd = 500f,
                ambientSky = new Color(0.55f, 0.4f, 0.25f), ambientEquator = new Color(0.3f, 0.22f, 0.14f), ambientGround = new Color(0.08f, 0.05f, 0.03f),
                sunColor = new Color(1f, 0.75f, 0.5f), sunIntensity = 0.6f,
                ramp = new Color(0.85f, 0.7f, 0.45f), rampSurface = Surface.Plaster, slab = new Color(0.3f, 0.24f, 0.14f), slabSurface = Surface.Stone,
                glow = new Color(1f, 0.1f, 0.05f), glowAlt = new Color(1f, 0.4f, 0.1f),
                scenery = new Color(0.32f, 0.26f, 0.16f), scenerySurface = Surface.Blocks, style = SceneryStyle.Forge,
                script = MapScripts.Gigapede2,
                tier = "TIER 7",
                section = "surf_gigapede 2/2",
                continues = true,
            },
            new Biome
            {
                name = "HELLJUMPER", sky = new Color(0.05f, 0.1f, 0.18f), skyTop = new Color(0.01f, 0.02f, 0.05f), skyBottom = new Color(0.04f, 0.08f, 0.14f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.4f, 0.6f, 0.75f), ambientEquator = new Color(0.22f, 0.32f, 0.42f), ambientGround = new Color(0.05f, 0.07f, 0.1f),
                sunColor = new Color(0.8f, 0.9f, 1f), sunIntensity = 0.7f,
                ramp = new Color(0.55f, 0.58f, 0.62f), rampSurface = Surface.Metal, slab = new Color(0.6f, 0.1f, 0.1f), slabSurface = Surface.Panel,
                glow = new Color(0.2f, 0.8f, 1f), glowAlt = new Color(1f, 0.15f, 0.1f),
                scenery = new Color(0.45f, 0.5f, 0.55f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Wire,
                script = MapScripts.Helljumper1,
                tier = "TIER 7",
                section = "surf_helljumper 1/2",
            },
            new Biome
            {
                name = "HELLJUMPER", sky = new Color(0.05f, 0.1f, 0.18f), skyTop = new Color(0.01f, 0.02f, 0.05f), skyBottom = new Color(0.04f, 0.08f, 0.14f),
                fogStart = 80f, fogEnd = 600f,
                ambientSky = new Color(0.4f, 0.6f, 0.75f), ambientEquator = new Color(0.22f, 0.32f, 0.42f), ambientGround = new Color(0.05f, 0.07f, 0.1f),
                sunColor = new Color(0.8f, 0.9f, 1f), sunIntensity = 0.7f,
                ramp = new Color(0.55f, 0.58f, 0.62f), rampSurface = Surface.Metal, slab = new Color(0.6f, 0.1f, 0.1f), slabSurface = Surface.Panel,
                glow = new Color(0.2f, 0.8f, 1f), glowAlt = new Color(1f, 0.15f, 0.1f),
                scenery = new Color(0.45f, 0.5f, 0.55f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Wire,
                script = MapScripts.Helljumper2,
                tier = "TIER 7",
                section = "surf_helljumper 2/2",
                continues = true,
            },
            new Biome
            {
                name = "EXONIC", sky = new Color(0.15f, 0.22f, 0.3f), skyTop = new Color(0.04f, 0.06f, 0.1f), skyBottom = new Color(0.1f, 0.3f, 0.45f),
                fogStart = 100f, fogEnd = 800f,
                ambientSky = new Color(0.55f, 0.65f, 0.75f), ambientEquator = new Color(0.3f, 0.36f, 0.42f), ambientGround = new Color(0.08f, 0.1f, 0.12f),
                sunColor = new Color(0.8f, 0.9f, 1f), sunIntensity = 0.8f,
                ramp = new Color(0.5f, 0.56f, 0.62f), rampSurface = Surface.Metal, slab = new Color(0.3f, 0.35f, 0.4f), slabSurface = Surface.Plates,
                glow = new Color(0.4f, 0.75f, 1f), glowAlt = new Color(1f, 0.5f, 0.2f),
                scenery = new Color(0.35f, 0.4f, 0.45f), scenerySurface = Surface.WallGrate, style = SceneryStyle.Mine,
                script = MapScripts.Exonic1,
                tier = "TIER 7",
                section = "surf_exonic 1/2",
            },
            new Biome
            {
                name = "EXONIC", sky = new Color(0.15f, 0.22f, 0.3f), skyTop = new Color(0.04f, 0.06f, 0.1f), skyBottom = new Color(0.1f, 0.3f, 0.45f),
                fogStart = 100f, fogEnd = 800f,
                ambientSky = new Color(0.55f, 0.65f, 0.75f), ambientEquator = new Color(0.3f, 0.36f, 0.42f), ambientGround = new Color(0.08f, 0.1f, 0.12f),
                sunColor = new Color(0.8f, 0.9f, 1f), sunIntensity = 0.8f,
                ramp = new Color(0.5f, 0.56f, 0.62f), rampSurface = Surface.Metal, slab = new Color(0.3f, 0.35f, 0.4f), slabSurface = Surface.Plates,
                glow = new Color(0.4f, 0.75f, 1f), glowAlt = new Color(1f, 0.5f, 0.2f),
                scenery = new Color(0.35f, 0.4f, 0.45f), scenerySurface = Surface.WallGrate, style = SceneryStyle.Mine,
                script = MapScripts.Exonic2,
                tier = "TIER 7",
                section = "surf_exonic 2/2",
                continues = true,
            },
            new Biome
            {
                name = "RAPHAELLO", sky = new Color(1f, 0.6f, 0.4f), skyTop = new Color(0.35f, 0.25f, 0.4f), skyBottom = new Color(1f, 0.55f, 0.35f),
                fogStart = 150f, fogEnd = 1000f,
                ambientSky = new Color(0.95f, 0.75f, 0.65f), ambientEquator = new Color(0.6f, 0.42f, 0.36f), ambientGround = new Color(0.2f, 0.13f, 0.12f),
                sunColor = new Color(1f, 0.7f, 0.5f), sunIntensity = 1.0f,
                ramp = new Color(0.95f, 0.72f, 0.6f), rampSurface = Surface.Plaster, slab = new Color(0.6f, 0.4f, 0.38f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.55f, 0.25f), glowAlt = new Color(0.35f, 0.6f, 1f),
                scenery = new Color(0.85f, 0.55f, 0.45f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Sunset,
                script = MapScripts.Raphaello1,
                tier = "TIER 7",
                section = "surf_raphaello 1/2",
            },
            new Biome
            {
                name = "RAPHAELLO", sky = new Color(1f, 0.6f, 0.4f), skyTop = new Color(0.35f, 0.25f, 0.4f), skyBottom = new Color(1f, 0.55f, 0.35f),
                fogStart = 150f, fogEnd = 1000f,
                ambientSky = new Color(0.95f, 0.75f, 0.65f), ambientEquator = new Color(0.6f, 0.42f, 0.36f), ambientGround = new Color(0.2f, 0.13f, 0.12f),
                sunColor = new Color(1f, 0.7f, 0.5f), sunIntensity = 1.0f,
                ramp = new Color(0.95f, 0.72f, 0.6f), rampSurface = Surface.Plaster, slab = new Color(0.6f, 0.4f, 0.38f), slabSurface = Surface.Plaster,
                glow = new Color(1f, 0.55f, 0.25f), glowAlt = new Color(0.35f, 0.6f, 1f),
                scenery = new Color(0.85f, 0.55f, 0.45f), scenerySurface = Surface.WallPanels, style = SceneryStyle.Sunset,
                script = MapScripts.Raphaello2,
                tier = "TIER 7",
                section = "surf_raphaello 2/2",
                continues = true,
            },
            new Biome
            {
                name = "THE FINALE", sky = new Color(0.01f, 0.01f, 0.015f), skyTop = new Color(0f, 0f, 0f), skyBottom = new Color(0.02f, 0.015f, 0.01f),
                fogStart = 300f, fogEnd = 2000f,
                ambientSky = new Color(0.45f, 0.42f, 0.38f), ambientEquator = new Color(0.25f, 0.23f, 0.2f), ambientGround = new Color(0.06f, 0.05f, 0.04f),
                sunColor = new Color(1f, 0.85f, 0.6f), sunIntensity = 0.7f,
                ramp = new Color(1f, 1f, 1f), rampSurface = Surface.RampCelestial, slab = new Color(0.08f, 0.07f, 0.06f), slabSurface = Surface.Metal,
                glow = new Color(1f, 0.78f, 0.35f), glowAlt = new Color(1f, 1f, 1f),
                scenery = new Color(0.1f, 0.09f, 0.08f), scenerySurface = Surface.WallGrid, style = SceneryStyle.Rings,
                script = MapScripts.Finale,
                tier = "FINALE",
                section = "almost impossible, never impossible",
            },
        };

        // Where the finale's zones start in All (the course ends after the last of them)
        public const int FinaleFrom = 29;
        // Where the Legend zones start: after the finale, as hard as the hardest surf maps
        public const int LegendFrom = 39;
        // Where the real hard maps start (after the Legend zones), up to the finale at the end
        public const int MapsFrom = 74;
    }

    // One set of shared materials per biome. Every ramp and scenery piece in the biome uses
    // these, so nothing is created while you play. They're made ahead of time by the scene
    // builder and saved as assets (materials made at runtime confused Unity's render
    // batching, drawing ramps with the scenery's texture).
    [Serializable]
    public class BiomeKit
    {
        public Material ramp, slab, scenery, glow, glowAlt, floor, accent, shaft, pool, skyPool;
        public Material trim => glow;
        public Material[] rampHues, glowHues; // Spectrum: a ramp and a glow per hue

        // This kit as one hue of a Spectrum zone: the ramp, its outline and every glow in that color
        public BiomeKit Hue(int i)
        {
            if (rampHues == null || rampHues.Length == 0) return this;
            var k = (BiomeKit)MemberwiseClone();
            k.ramp = rampHues[i % rampHues.Length];
            k.glow = glowHues[i % glowHues.Length];
            return k;
        }

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
            if (biome.style != SceneryStyle.Palace) return; // the other open zones build their own landscape

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
                        // Clouds drifting far below
                        Part(piece, cube, kit.scenery, new Vector3(0f, Rand(-70f, -30f), 0f), new Vector3(Rand(40f, 90f), Rand(6f, 12f), Rand(30f, 70f)));

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
