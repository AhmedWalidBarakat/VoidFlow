using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // One ramp of a hand-made map: its shape and the flight onto it, laid out by hand after
    // the real map's run (studied frame by frame) instead of picked at random. Distances in
    // metres. `gap` is flown before the landing hill, `shift` is how far sideways it lands
    // (+ right), `land` is the landing hill's length; the shape and bend follow, measured
    // from the end of the hill (see RampShapes.LandingRamp).
    public sealed class RampDef
    {
        public string name = "ramp";
        public RampShapes.Kind kind = RampShapes.Kind.Prism;
        public float width = 8f;
        public (float s, float v)[] shape, bend;
        public float gap = 36f, shift = 12f, land = 60f, clear = 6f;
        public float bank = RampShapes.SlabBank; // slabs: how steeply the face leans
        public float spinRadius;                  // spins: their radius
        public bool bigAir, twin, window;
        public int pieces; // broken into this many separate pieces (ramp strafes), 0 = whole

        // The flight onto it: gap, sideways shift, landing hill length
        public RampDef At(float gap, float shift, float land) { this.gap = gap; this.shift = shift; this.land = land; return this; }
        public RampDef Air() { bigAir = true; return this; }        // a ring hung over the flight
        public RampDef Window() { window = true; return this; }     // the flight goes through a hole in a wall
        public RampDef Broken(int n) { pieces = n; return this; }   // ridden piece by piece
        public RampDef Named(string n) { name = n; return this; }
    }

    // The hand-made maps: each stage is six RampDefs, after the map's own run
    public static class MapScripts
    {
        // ------------------------------------------------------------------ ramp kinds

        // A straight prism: dives at `slope`, then rises to launch
        public static RampDef Line(float width, float length, float slope = -0.12f) => new()
        {
            name = "line", width = width,
            shape = new[] { (30f, slope), (length - 30f, slope), (length, 0.07f) },
        };

        // A ramp turning `degrees` (+ into its face) over its length; slab = a banked panel
        public static RampDef Curve(float width, float length, float degrees, bool slab = false, float slope = -0.1f)
        {
            // A banked curve is a spin in small: the same wall and at least the same radius,
            // or at top speed it throws you off (see Spin)
            if (slab) length = Mathf.Max(length, Mathf.Abs(degrees) * Mathf.Deg2Rad * MinSpinRadius + 70f);
            return CurveOf(width, length, degrees, slab, slope);
        }

        static RampDef CurveOf(float width, float length, float degrees, bool slab, float slope) => new()
        {
            name = slab ? "banked curve" : "curve", width = width, bank = slab ? SpinBank : RampShapes.SlabBank,
            kind = slab ? RampShapes.Kind.Slab : RampShapes.Kind.Prism,
            shape = new[] { (40f, slope), (length - 30f, slope), (length, 0.07f) },
            bend = new[] { (20f, 0f), (length - 30f, degrees), (length, degrees) },
        };

        // Spins lean at 62 degrees, like the steep walls of real spin maps. Tested with Source
        // surf physics (SpinLab): with feathered strafes a 110m spin at 62 degrees holds at any
        // speed from 2000 to 2900 u/s, an 80m one up to ~2600; too much strafe and you fly over
        // the top, too little and you sink off the bottom.
        public const float SpinBank = 62f;
        public const float MinSpinRadius = 140f;

        // A spin: a banked slab curling round and round at `radius`, held all the way
        public static RampDef Spin(float width, float degrees, float radius, float slope = -0.08f)
        {
            // Tested (SpinLab): at 62 degrees a 140m spin holds 540 degrees at every speed from
            // 1800 to 3500 u/s (the cap); tighter ones throw you off near top speed
            radius = Mathf.Max(radius, MinSpinRadius);
            float length = degrees * Mathf.Deg2Rad * radius + 50f;
            return new RampDef
            {
                name = $"spin {degrees:0}", width = width, kind = RampShapes.Kind.Slab, bank = SpinBank, spinRadius = radius,
                shape = new[] { (40f, slope), (length - 20f, slope), (length, 0.06f) },
                bend = new[] { (25f, 0f), (length - 25f, degrees), (length, degrees) },
            };
        }

        // A snipe: a tiny, narrow ramp hit from a long air
        public static RampDef Snipe(float width, float length) => new()
        {
            name = "snipe", width = width,
            shape = new[] { (length * 0.5f, -0.08f), (length, 0.08f) },
        };

        // A drop: a short steep slide that throws you down into a long fall (gravity maps)
        public static RampDef Drop(float width, float length) => new()
        {
            name = "drop", width = width,
            shape = new[] { (15f, -1.0f), (length - 15f, -1.0f), (length, -0.35f) },
        };

        // Rolling dips: down, up, down, up, launch
        public static RampDef Wave(float width) => new()
        {
            name = "wave", width = width,
            shape = new[] { (45f, -0.32f), (100f, 0.04f), (155f, -0.32f), (210f, 0.04f), (245f, -0.1f), (275f, 0.08f) },
        };

        // A dive at ~31 degrees, levelling out over a long bend, then launch
        public static RampDef Plunge(float width) => new()
        {
            name = "plunge", width = width,
            shape = new[] { (30f, -0.6f), (90f, -0.6f), (240f, 0f), (270f, 0.08f) },
        };

        // Level, then rising to launch high (an uphill boost)
        public static RampDef Climb(float width) => new()
        {
            name = "climb", width = width,
            shape = new[] { (50f, 0f), (130f, 0.2f), (150f, 0.2f) },
        };

        // A dip for speed, then a steep kicker that throws you into big air
        public static RampDef Launch(float width) => new()
        {
            name = "launch", width = width,
            shape = new[] { (50f, -0.18f), (150f, -0.18f), (205f, 0f), (235f, 0.32f), (250f, 0.32f) },
        };

        // A blade with an identical one alongside
        public static RampDef Twin(float width, float length = 160f)
        {
            var d = Line(width, length);
            d.name = "twin";
            d.twin = true;
            return d;
        }

        // ------------------------------------------------------------------ the maps

        // surf_utopia_njv: long off-white halls lined with orange and blue stripes, wide
        // centre prisms, framed windows to fly through, a bowl, a winding hall; tier 1
        public static readonly RampDef[] Utopia1 =
        {
            Line(17f, 230f).At(30f, 8f, 80f).Named("first hall"),
            Line(16f, 200f).At(34f, -10f, 75f).Named("striped hall"),
            Curve(16f, 220f, 70f).At(32f, 10f, 75f).Named("the bowl"),
            Line(15f, 180f).At(36f, -12f, 70f).Window().Named("orange window"),
            Curve(15f, 240f, 60f, true).At(32f, 10f, 72f).Named("winding hall"),
            Curve(15f, 240f, 55f, true).At(30f, -10f, 72f).Named("winding back"),
        };
        public static readonly RampDef[] Utopia2 =
        {
            Line(15f, 200f).At(34f, 12f, 72f).Named("long hall"),
            Line(15f, 180f).At(36f, -10f, 70f).Window().Named("square portal"),
            Line(16f, 190f).At(32f, 10f, 74f).Named("centre prism"),
            Curve(15f, 220f, 65f).At(34f, -12f, 70f).Named("turning prism"),
            Line(14f, 190f).At(36f, 12f, 68f).Named("across the stripes"),
            Line(16f, 160f).At(30f, -8f, 80f).Named("into the end room"),
        };

        // surf_mesa: underground mine, grey seamed ramps with cyan trim, tunnels bored through
        // rock, crystals, a blue pool chamber, a lava room at the end; one continuous run
        public static readonly RampDef[] Mesa1 =
        {
            Line(15f, 200f).At(30f, 10f, 75f).Named("off the tiled start"),
            Line(13f, 190f).At(34f, -12f, 68f).Named("into the tunnel"),
            Line(13f, 180f).At(36f, 12f, 66f).Window().Named("through the rock"),
            Curve(13f, 220f, 60f).At(34f, -12f, 66f).Named("crystal bend"),
            Line(12f, 210f).At(36f, 12f, 64f).Named("long grey prism"),
            Line(12f, 190f).At(36f, -12f, 64f).Window().Named("tunnel hole"),
        };
        public static readonly RampDef[] Mesa2 =
        {
            Line(12f, 200f).At(36f, 12f, 64f).Named("crystal cave"),
            Curve(12f, 230f, -55f).At(36f, -14f, 62f).Named("canyon cave"),
            Line(12f, 210f).At(38f, 14f, 62f).Named("stalactites"),
            Plunge(11f).At(36f, -12f, 62f).Named("down to the blue light"),
            Line(12f, 190f).At(38f, 12f, 62f).Named("pool chamber"),
            Line(14f, 170f).At(34f, -10f, 70f).Named("into the lava room"),
        };

        // surf_summer: beach town boardwalks, a water park of giant life rings, a candy
        // straight, a skate park, a sandstone fort, a night canyon, steel, waterslides
        public static readonly RampDef[] Summer1 =
        {
            Line(13f, 200f).At(32f, 10f, 68f).Named("boardwalk"),
            Curve(12f, 220f, 50f).At(36f, -12f, 62f).Named("sandstone street"),
            Line(12f, 190f).At(38f, 14f, 60f).Air().Named("over the barrier"),
            Line(12f, 200f).At(38f, -14f, 60f).Air().Named("through the life ring"),
            Curve(11f, 240f, 80f, true).At(38f, 14f, 60f).Named("round the ring"),
            Line(11f, 200f).At(40f, -14f, 58f).Named("ice cream pier"),
        };
        public static readonly RampDef[] Summer2 =
        {
            Line(11f, 220f).At(40f, 14f, 58f).Named("popsicle straight"),
            Plunge(10f).At(40f, -14f, 58f).Named("pastel dive"),
            Curve(11f, 220f, -70f, true).At(38f, 14f, 58f).Named("half-pipe bowl"),
            Wave(11f).At(40f, -14f, 56f).Named("skate park rollers"),
            Line(10f, 200f).At(42f, 15f, 56f).Named("pier boardwalk"),
            Twin(9f).At(40f, -14f, 56f).Named("beach huts"),
        };
        public static readonly RampDef[] Summer3 =
        {
            Curve(10f, 230f, 60f).At(42f, 15f, 55f).Named("sandstone fort"),
            Line(10f, 210f).At(44f, -16f, 54f).Named("night canyon"),
            Launch(9f).At(42f, 15f, 55f).Named("moonlit kicker"),
            Line(9f, 200f).At(46f, -16f, 52f).Named("diamond-plate steel"),
            Spin(12f, 180f, 140f).At(42f, 16f, 54f).Named("red waterslide"),
            Line(11f, 170f).At(40f, -12f, 60f).Named("by the beach ball"),
        };

        // surf_essentia: dark concrete rooms with pink neon, sand dunes far below, strange
        // narrow slabs, units; the last stage is a dark rock tube (after syria again's end)
        public static readonly RampDef[] Essentia1 =
        {
            Line(7f, 190f, -0.06f).At(48f, 20f, 42f).Named("sand room blade"),
            Curve(6f, 170f, -35f, true, -0.04f).At(52f, -22f, 40f).Named("strange slab"),
            Curve(6f, 200f, 40f, true, -0.05f).At(50f, 22f, 40f).Named("grid hall"),
            Line(6f, 180f, -0.04f).At(54f, -24f, 38f).Window().Named("oval window"),
            Line(6f, 200f, -0.03f).At(52f, 22f, 38f).Named("flat and long: units"),
            Climb(6f).At(50f, -22f, 40f).Named("uphill boost"),
        };
        public static readonly RampDef[] Essentia2 =
        {
            Line(5f, 220f, -0.03f).At(56f, 24f, 36f).Named("into the rock tube"),
            Curve(5f, 240f, 60f).At(56f, -26f, 34f).Named("tight tube turn"),
            Line(5f, 200f, -0.02f).At(58f, 26f, 34f).Named("long thin ramp"),
            Curve(5f, 240f, -70f).At(56f, -26f, 34f).Named("turn back"),
            Climb(5f).At(58f, 26f, 34f).Named("slope up"),
            Line(5f, 220f, -0.03f).At(60f, -28f, 32f).Named("the hardest ramp"),
        };

        // surf_strike bonus: black space, faceted spins with rainbow edges at full speed,
        // ending in a really tight flick
        public static readonly RampDef[] Strike =
        {
            Spin(12f, 540f, 110f).At(46f, 20f, 40f).Named("faceted spin"),
            Spin(11f, 540f, 100f).At(50f, -22f, 38f).Named("second spin"),
            Spin(10f, 450f, 90f).At(52f, 24f, 36f).Named("third spin"),
            Spin(9f, 360f, 85f).At(54f, -26f, 34f).Named("last spin"),
            Line(4f, 80f).At(50f, 30f, 26f).Named("white beam"),
            Snipe(4f, 40f).At(56f, -32f, 24f).Named("the flick"),
        };

        // surf_blackheart: inside a heart: you fall more than you surf, steering down shafts
        // and through valve holes, off thorny ledges, to the heart at the bottom
        public static readonly RampDef[] Blackheart =
        {
            Drop(8f, 90f).At(30f, 6f, 40f).Named("down the first shaft"),
            Drop(7f, 80f).At(60f, -10f, 36f).Window().Named("through the valve"),
            Drop(7f, 80f).At(65f, 12f, 36f).Named("thorn ledge"),
            Drop(6f, 70f).At(70f, -14f, 34f).Window().Named("membrane hole"),
            Drop(6f, 70f).At(70f, 14f, 34f).Named("last drop"),
            Line(8f, 120f).At(60f, -10f, 40f).Named("into the heart"),
        };

        // surf_trofle: broken spiral ramps in a violet room, then the whole of sinsane, then a
        // last stage whose ramps are the colour of the haze
        public static readonly RampDef[] Trofle1 =
        {
            Spin(10f, 300f, 100f).Broken(6).At(40f, 16f, 40f).Named("broken spiral staircase"),
            Spin(10f, 300f, 95f).Broken(6).At(46f, -18f, 38f).Named("broken again"),
            Line(6f, 180f).At(50f, 20f, 38f).Named("dark corridor"),
            Line(6f, 190f).At(52f, -22f, 36f).Named("white hall"),
            Curve(5f, 200f, 50f).At(54f, 24f, 34f).Named("porthole room"),
            Line(5f, 190f).At(56f, -24f, 34f).Named("sinsane blade"),
        };
        public static readonly RampDef[] Trofle2 =
        {
            Line(5f, 200f).At(56f, 24f, 34f).Named("sinsane hall"),
            Curve(5f, 220f, -60f).At(56f, -26f, 34f).Named("sinsane turn"),
            Twin(5f).At(54f, 24f, 34f).Named("sandman"),
            Spin(9f, 360f, 95f).At(52f, -26f, 34f).Named("royal spin"),
            Line(5f, 190f).At(58f, 26f, 32f).Named("gold truss"),
            Snipe(4f, 50f).At(56f, -28f, 30f).Named("nightmare bonus"),
        };
        public static readonly RampDef[] Trofle3 =
        {
            Line(6f, 220f).At(54f, 24f, 34f).Named("into the haze"),
            Curve(6f, 240f, 50f).At(56f, -26f, 34f).Named("unseen turn"),
            Line(5f, 220f).At(58f, 26f, 32f).Named("unseen straight"),
            Curve(5f, 240f, -60f).At(58f, -28f, 32f).Named("unseen turn back"),
            Line(5f, 220f).At(60f, 28f, 30f).Named("last unseen ramp"),
            Line(7f, 140f).At(50f, -20f, 40f).Named("to the grass platform"),
        };

        // surf_spin: famous spins back to back, each in its own map's colours
        public static readonly RampDef[] Spin1 =
        {
            Spin(12f, 360f, 120f).At(44f, 18f, 40f).Named("others bonus"),
            Spin(12f, 540f, 110f).At(48f, -20f, 38f).Named("diminishing b2"),
            Spin(11f, 630f, 100f).At(50f, 22f, 36f).Named("minecraft b1"),
            Spin(10f, 450f, 95f).At(50f, -22f, 36f).Named("reprise hard"),
            Spin(10f, 540f, 90f).At(52f, 24f, 34f).Named("rocco b2"),
            Spin(10f, 540f, 90f).At(52f, -24f, 34f).Named("royal spin"),
        };
        public static readonly RampDef[] Spin2 =
        {
            Spin(10f, 450f, 95f).At(52f, 24f, 34f).Named("syria again"),
            Spin(10f, 540f, 90f).At(54f, -26f, 34f).Named("royal again"),
            Line(5f, 260f).At(56f, 26f, 32f).Named("surf_map linear"),
            Spin(10f, 630f, 90f).At(54f, -26f, 32f).Named("spaceship spin"),
            Spin(9f, 540f, 85f).At(56f, 28f, 32f).Named("distance b3"),
            Snipe(4f, 60f).At(58f, -30f, 26f).Named("strike flick"),
        };

        // surf_before: five famous bonus-4s back to back, stripped to plain textures
        public static readonly RampDef[] Before1 =
        {
            Line(6f, 170f).At(46f, 20f, 40f).Named("666 b4"),
            Line(5f, 160f).At(50f, -22f, 38f).Named("over the red pit"),
            Curve(5f, 190f, 70f).At(50f, 22f, 38f).Named("666 corner"),
            Line(5f, 170f).At(52f, -22f, 36f).Named("resource b4"),
            Line(5f, 160f).At(52f, 24f, 36f).Named("rubble halls"),
            Curve(5f, 200f, -80f).At(52f, -24f, 36f).Named("tight corner"),
        };
        public static readonly RampDef[] Before2 =
        {
            Line(5f, 170f).At(52f, 24f, 36f).Named("technique b4"),
            Wave(5f).At(52f, -24f, 36f).Named("stepped ledges"),
            Line(5f, 180f).At(54f, 24f, 34f).Named("teal hall"),
            Line(5f, 220f).At(56f, -26f, 34f).Named("goliath b4"),
            Curve(5f, 200f, 95f).At(54f, 26f, 34f).Named("really tight turn"),
            Line(5f, 170f).At(56f, -26f, 34f).Named("over the ramps below"),
        };
        public static readonly RampDef[] Before3 =
        {
            Line(5f, 200f, -0.03f).At(58f, 26f, 32f).Named("modern b4"),
            Line(5f, 200f, -0.03f).At(60f, -28f, 32f).Named("optimum"),
            Line(5f, 200f, -0.03f).At(60f, 28f, 30f).Named("optimum"),
            Line(4.5f, 200f, -0.03f).At(62f, -28f, 30f).Named("optimum"),
            Line(4.5f, 190f).At(62f, 30f, 30f).Named("facility b4"),
            Line(4.5f, 190f).At(64f, -30f, 28f).Named("no-fail end"),
        };

        // surf_frags_nightmare: plain dev rooms, a run of tight snipes, then the spin nobody
        // has done
        public static readonly RampDef[] Frags1 =
        {
            Snipe(5f, 50f).At(55f, 24f, 30f).Named("snipe"),
            Snipe(4.5f, 45f).At(60f, -26f, 28f).Named("snipe"),
            Snipe(4.5f, 45f).At(62f, 28f, 28f).Window().Named("past the corner"),
            Snipe(4f, 45f).At(64f, -30f, 28f).Named("snipe"),
            Snipe(4f, 45f).At(64f, 30f, 26f).Named("snipe"),
            Snipe(4f, 45f).At(66f, -30f, 26f).Named("snipe"),
        };
        public static readonly RampDef[] Frags2 =
        {
            Snipe(4f, 45f).At(66f, 30f, 26f).Named("snipe"),
            Snipe(4f, 40f).At(68f, -32f, 26f).Named("snipe"),
            Snipe(4f, 40f).At(68f, 32f, 26f).Named("snipe"),
            Snipe(3.8f, 40f).At(70f, -32f, 24f).Named("tightest snipe"),
            Line(5f, 140f).At(60f, 30f, 28f).Named("into the last room"),
            Spin(8f, 720f, 80f).At(58f, -30f, 30f).Named("the unbeaten spin"),
        };

        // surf_corruption: dark purple caverns, thin white ribbons that bank and roll, over
        // two minutes of perfect speed, out under the clouds to the final skip
        public static readonly RampDef[] Corruption1 =
        {
            Curve(6f, 300f, 90f, true).At(54f, 24f, 36f).Named("purple cavern ribbon"),
            Curve(6f, 300f, -100f, true).At(56f, -26f, 34f).Named("J-curve"),
            Line(5f, 280f).At(58f, 26f, 34f).Named("long white ribbon"),
            Curve(5f, 320f, 120f, true).At(58f, -26f, 34f).Named("banking ribbon"),
            Curve(5f, 300f, -90f).At(60f, 28f, 32f).Named("rolling ribbon"),
            Line(5f, 280f).At(60f, -28f, 32f).Named("into the dark"),
        };
        public static readonly RampDef[] Corruption2 =
        {
            Curve(5f, 300f, 110f, true).At(60f, 28f, 32f).Named("teal water cave"),
            Curve(5f, 320f, -120f, true).At(60f, -28f, 32f).Named("ribbon"),
            Line(5f, 300f).At(62f, 28f, 30f).Named("long ribbon"),
            Curve(5f, 320f, 130f, true).At(62f, -30f, 30f).Named("roll"),
            Line(4.5f, 280f).At(64f, 30f, 30f).Named("narrow ribbon"),
            Curve(5f, 320f, -110f, true).At(62f, -30f, 30f).Named("roll back"),
        };
        public static readonly RampDef[] Corruption3 =
        {
            Line(4.5f, 300f).At(64f, 30f, 30f).Named("last cavern"),
            Curve(5f, 320f, 120f, true).At(64f, -30f, 30f).Named("ribbon"),
            Line(4.5f, 300f).At(66f, 30f, 28f).Named("towards the light"),
            Curve(5f, 330f, -120f, true).At(64f, -30f, 28f).Named("out under the clouds"),
            Launch(5f).At(64f, 28f, 30f).Named("the skip ramp"),
            Line(8f, 160f).At(150f, 0f, 40f).Air().Named("the final skip"),
        };

        // surf_deity: copper and brick halls, royal and nightmare-bonus parts
        public static readonly RampDef[] Deity1 =
        {
            Line(5f, 210f).At(56f, 24f, 34f).Named("copper hall"),
            Line(5f, 200f).At(58f, -26f, 32f).Named("brick hall"),
            Spin(9f, 360f, 95f).At(54f, 24f, 34f).Named("royal"),
            Line(5f, 200f).At(58f, -26f, 32f).Window().Named("under the beams"),
            Curve(5f, 220f, 85f).At(58f, 26f, 32f).Named("lamplit turn"),
            Line(4.5f, 200f).At(60f, -28f, 30f).Named("narrow hall"),
        };
        public static readonly RampDef[] Deity2 =
        {
            Snipe(4.5f, 50f).At(58f, 26f, 30f).Named("nightmare b1"),
            Line(4.5f, 200f).At(60f, -28f, 30f).Named("wood panel hall"),
            Spin(9f, 450f, 90f).At(56f, 28f, 32f).Named("royal again"),
            Curve(4.5f, 220f, -90f).At(60f, -28f, 30f).Named("sharp turn"),
            Line(4.5f, 210f).At(62f, 30f, 28f).Named("last hall"),
            Line(6f, 150f).At(56f, -24f, 36f).Named("into the fire room"),
        };

        // surf_anubis: a sandstone tomb: short, punishing, technical
        public static readonly RampDef[] Anubis1 =
        {
            Line(5f, 150f).At(54f, 26f, 30f).Named("tomb"),
            Curve(5f, 170f, 100f, true).At(56f, -28f, 28f).Named("turns back on itself"),
            Drop(5f, 70f).At(56f, 26f, 30f).Named("into the pit"),
            Line(4.5f, 150f).At(58f, -28f, 28f).Window().Named("black doorway"),
            Curve(4.5f, 170f, -110f, true).At(58f, 30f, 28f).Named("torchlit turn"),
            Snipe(4f, 45f).At(60f, -30f, 26f).Named("precise transfer"),
        };
        public static readonly RampDef[] Anubis2 =
        {
            Line(4.5f, 150f).At(60f, 30f, 28f).Named("lower tomb"),
            Drop(4.5f, 70f).At(60f, -28f, 28f).Named("down the shaft"),
            Curve(4.5f, 170f, 120f, true).At(60f, 30f, 26f).Named("tight turn"),
            Line(4.5f, 160f).At(62f, -30f, 26f).Window().Named("doorway"),
            Snipe(4f, 40f).At(62f, 30f, 26f).Named("snipe"),
            Line(6f, 120f).At(54f, -24f, 34f).Named("out of the tomb"),
        };

        // surf_raphaello: salmon and peach halls with sunset windows and mosaic ceilings
        public static readonly RampDef[] Raphaello1 =
        {
            Curve(7f, 260f, 70f, true).At(50f, 22f, 40f).Named("salmon hall"),
            Line(6f, 240f).At(52f, -24f, 38f).Window().Named("sunset window"),
            Curve(6f, 280f, -80f, true).At(52f, 24f, 38f).Named("bevelled turn"),
            Line(6f, 240f).At(54f, -24f, 36f).Named("peach straight"),
            Curve(6f, 280f, 90f).At(54f, 24f, 36f).Named("mosaic ceiling"),
            Line(6f, 220f).At(56f, -26f, 36f).Window().Named("window pass"),
        };
        public static readonly RampDef[] Raphaello2 =
        {
            Curve(6f, 280f, -90f, true).At(56f, 26f, 36f).Named("terracotta turn"),
            Line(5.5f, 240f).At(58f, -26f, 34f).Named("long hall"),
            Curve(6f, 300f, 100f, true).At(56f, 26f, 34f).Named("sweeping ramp"),
            Line(5.5f, 240f).At(58f, -28f, 34f).Window().Named("window pass"),
            Curve(5.5f, 280f, -90f).At(58f, 28f, 34f).Named("last turn"),
            Line(7f, 160f).At(52f, -22f, 40f).Named("into the lattice room"),
        };

        // surf_exonic: cold steel shafts over blue water, long narrow blades at extreme speed
        public static readonly RampDef[] Exonic1 =
        {
            Line(4.5f, 300f, -0.15f).At(58f, 26f, 32f).Named("steel shaft"),
            Line(4.5f, 300f, -0.15f).At(60f, -28f, 32f).Named("steel shaft"),
            Plunge(4.5f).At(60f, 28f, 32f).Named("dive"),
            Line(4.5f, 280f).At(62f, -28f, 30f).Named("over the water"),
            Line(4.5f, 300f, -0.15f).At(62f, 28f, 30f).Named("streaking blade"),
            Plunge(4.5f).At(62f, -28f, 30f).Named("dive"),
        };
        public static readonly RampDef[] Exonic2 =
        {
            Line(4.5f, 300f).At(64f, 30f, 30f).Named("iron gates"),
            Curve(4.5f, 260f, 60f).At(62f, -28f, 30f).Named("steel turn"),
            Line(4.2f, 300f).At(64f, 30f, 28f).Named("rusty ramp"),
            Plunge(4.2f).At(64f, -30f, 28f).Named("dive"),
            Line(4.2f, 300f).At(66f, 30f, 28f).Named("last shaft"),
            Line(6f, 140f).At(56f, -24f, 36f).Named("into the white"),
        };

        // surf_helljumper: a sci-fi base: drops down shafts, doorways, hard 90-degree turns
        public static readonly RampDef[] Helljumper1 =
        {
            Drop(6f, 70f).At(40f, 10f, 34f).Named("down the cyan shaft"),
            Line(5f, 140f).At(56f, -24f, 30f).Window().Named("doorway"),
            Curve(5f, 160f, 90f, true).At(56f, 26f, 30f).Named("corridor turn"),
            Drop(5f, 70f).At(60f, -14f, 30f).Named("red floor drop"),
            Line(5f, 140f).At(58f, 26f, 30f).Window().Named("doorway"),
            Curve(5f, 160f, -95f, true).At(58f, -28f, 28f).Named("corridor turn"),
        };
        public static readonly RampDef[] Helljumper2 =
        {
            Drop(5f, 70f).At(62f, 16f, 30f).Named("lower shaft"),
            Line(4.5f, 140f).At(60f, -28f, 28f).Window().Named("doorway"),
            Curve(4.5f, 160f, 95f, true).At(60f, 28f, 28f).Named("corridor turn"),
            Drop(4.5f, 70f).At(64f, -16f, 28f).Named("last drop"),
            Line(4.5f, 150f).At(62f, 28f, 26f).Named("grated floor"),
            Line(6f, 110f).At(54f, -22f, 34f).Named("out"),
        };

        // surf_gigapede: a long chain of segments through a dark stone dungeon lit red
        public static readonly RampDef[] Gigapede1 =
        {
            Line(6.5f, 260f).At(50f, 22f, 40f).Named("segment"),
            Wave(6f).At(52f, -22f, 40f).Named("segment"),
            Line(6f, 260f).At(52f, 24f, 38f).Named("segment"),
            Curve(6f, 280f, 70f).At(52f, -24f, 38f).Named("segment"),
            Line(6f, 260f).At(54f, 24f, 38f).Window().Named("red portal"),
            Wave(6f).At(54f, -24f, 38f).Named("segment"),
        };
        public static readonly RampDef[] Gigapede2 =
        {
            Line(6f, 260f).At(54f, 24f, 38f).Named("segment"),
            Curve(6f, 280f, -80f).At(54f, -24f, 36f).Named("segment"),
            Line(5.5f, 260f).At(56f, 26f, 36f).Named("segment"),
            Wave(5.5f).At(56f, -26f, 36f).Named("segment"),
            Line(5.5f, 260f).At(58f, 26f, 36f).Named("tail"),
            Line(7f, 160f).At(50f, -22f, 40f).Named("the head"),
        };

        // surf_techslop: polygon tubes: short angular slabs with hard direction changes
        public static readonly RampDef[] Techslop1 =
        {
            Curve(5f, 140f, 90f, true).At(40f, 18f, 30f).Named("tube wall"),
            Curve(5f, 140f, -100f, true).At(42f, -20f, 30f).Named("tube wall"),
            Curve(5f, 150f, 110f, true).At(44f, 20f, 30f).Named("tube ceiling"),
            Snipe(4.5f, 45f).At(46f, -22f, 28f).Named("transfer"),
            Curve(5f, 150f, -110f, true).At(46f, 22f, 28f).Named("tube wall"),
            Curve(5f, 150f, 100f, true).At(48f, -22f, 28f).Named("rust room"),
        };
        public static readonly RampDef[] Techslop2 =
        {
            Curve(4.5f, 150f, -110f, true).At(48f, 22f, 28f).Named("cyan room"),
            Snipe(4.5f, 40f).At(50f, -24f, 26f).Named("transfer"),
            Curve(4.5f, 150f, 120f, true).At(50f, 24f, 26f).Named("tube wall"),
            Curve(4.5f, 150f, -120f, true).At(52f, -24f, 26f).Named("tube wall"),
            Snipe(4.2f, 40f).At(52f, 26f, 26f).Named("transfer"),
            Line(6f, 120f).At(46f, -20f, 34f).Named("red room"),
        };

        // surf_shade: moonlit concrete and grass, narrow ramps between walls and fences
        public static readonly RampDef[] Shade =
        {
            Line(4.5f, 150f).At(56f, 26f, 30f).Named("moonlit ramp"),
            Line(4.5f, 140f).At(58f, -28f, 28f).Named("between the walls"),
            Twin(4.5f).At(58f, 28f, 28f).Named("by the fences"),
            Line(4.2f, 140f).At(60f, -30f, 28f).Named("over the grass"),
            Snipe(4f, 45f).At(62f, 30f, 26f).Named("snipe"),
            Line(6f, 120f).At(54f, -24f, 34f).Named("to the lamp post"),
        };

        // surf_tensor2: cold concrete rooms with fluorescent lights, grey prisms, endurance
        public static readonly RampDef[] Tensor1 =
        {
            Line(5.5f, 200f).At(52f, 24f, 36f).Named("room 1"),
            Line(5.5f, 190f).At(54f, -24f, 36f).Window().Named("barred window"),
            Curve(5.5f, 210f, 70f).At(54f, 24f, 36f).Named("room 2"),
            Line(5f, 200f).At(56f, -26f, 34f).Named("under the lights"),
            Line(5f, 190f).At(56f, 26f, 34f).Window().Named("doorway"),
            Curve(5f, 210f, -80f).At(56f, -26f, 34f).Named("room 3"),
        };
        public static readonly RampDef[] Tensor2 =
        {
            Line(5f, 200f).At(58f, 26f, 34f).Named("dirt ramp room"),
            Line(5f, 190f).At(58f, -26f, 34f).Window().Named("doorway"),
            Curve(5f, 210f, 85f).At(58f, 26f, 32f).Named("room 4"),
            Line(5f, 200f).At(58f, -28f, 32f).Named("prism"),
            Line(4.8f, 190f).At(60f, 28f, 32f).Window().Named("doorway"),
            Curve(4.8f, 210f, -85f).At(60f, -28f, 32f).Named("room 5"),
        };
        public static readonly RampDef[] Tensor3 =
        {
            Line(4.8f, 200f).At(60f, 28f, 32f).Named("room 6"),
            Line(4.8f, 190f).At(60f, -28f, 32f).Window().Named("doorway"),
            Curve(4.8f, 210f, 90f).At(60f, 28f, 30f).Named("room 7"),
            Line(4.5f, 200f).At(62f, -30f, 30f).Named("prism"),
            Line(4.5f, 190f).At(62f, 30f, 30f).Window().Named("last doorway"),
            Line(6f, 140f).At(54f, -24f, 36f).Named("end room"),
        };

        // surf_not_so_funhouse: a red and black striped funhouse, tight curves in tunnels
        public static readonly RampDef[] Funhouse1 =
        {
            Curve(5.5f, 220f, 80f, true).At(50f, 22f, 36f).Named("striped tunnel"),
            Curve(5.5f, 220f, -90f, true).At(52f, -24f, 34f).Named("striped tunnel"),
            Line(5f, 200f).At(54f, 24f, 34f).Named("cyan edges"),
            Spin(9f, 360f, 95f).At(52f, -24f, 34f).Named("the carousel"),
            Curve(5f, 230f, 100f, true).At(54f, 24f, 34f).Named("checkered hall"),
            Line(5f, 200f).At(56f, -26f, 32f).Named("red room"),
        };
        public static readonly RampDef[] Funhouse2 =
        {
            Curve(5f, 230f, -100f, true).At(56f, 26f, 32f).Named("mirror room"),
            Line(5f, 200f).At(58f, -26f, 32f).Window().Named("through the frame"),
            Spin(9f, 450f, 90f).At(54f, 26f, 32f).Named("the carousel again"),
            Curve(4.8f, 230f, 110f, true).At(58f, -28f, 32f).Named("striped tunnel"),
            Line(4.8f, 200f).At(60f, 28f, 30f).Named("last stripe"),
            Line(6f, 140f).At(54f, -24f, 36f).Named("the end room"),
        };

        // surf_not_so_zen: concrete and raked sand, long narrow dark prisms, small doorways
        public static readonly RampDef[] Zen1 =
        {
            Line(5f, 240f).At(54f, 24f, 34f).Named("concrete hall"),
            Line(5f, 230f).At(56f, -26f, 34f).Window().Named("small doorway"),
            Line(5f, 240f).At(56f, 26f, 34f).Named("over the raked sand"),
            Curve(5f, 240f, 60f).At(56f, -26f, 34f).Named("bonsai hall"),
            Line(4.8f, 240f).At(58f, 26f, 32f).Window().Named("small doorway"),
            Line(4.8f, 230f).At(58f, -28f, 32f).Named("long dark prism"),
        };
        public static readonly RampDef[] Zen2 =
        {
            Line(4.8f, 240f).At(60f, 28f, 32f).Named("grate hall"),
            Curve(4.8f, 240f, -70f).At(60f, -28f, 32f).Named("concrete turn"),
            Line(4.5f, 240f).At(60f, 28f, 30f).Window().Named("small doorway"),
            Line(4.5f, 230f).At(62f, -30f, 30f).Named("over the sand"),
            Line(4.5f, 230f).At(62f, 30f, 30f).Named("last prism"),
            Line(6f, 140f).At(54f, -24f, 36f).Named("to the symbol"),
        };

        // surf_original: a collab: every ramp by a different hand, in a different look
        public static readonly RampDef[] Original1 =
        {
            Line(5.5f, 200f).At(52f, 24f, 36f).Named("grey concrete"),
            Curve(5.5f, 220f, 80f, true).At(54f, -24f, 34f).Named("red room"),
            Curve(5f, 220f, -90f).At(54f, 24f, 34f).Named("sandstone tunnel"),
            Line(5f, 200f).At(56f, -26f, 34f).Named("dark wood hall"),
            Wave(5f).At(56f, 26f, 34f).Named("desert room"),
            Line(5f, 200f).At(58f, -26f, 32f).Named("swamp garden"),
        };
        public static readonly RampDef[] Original2 =
        {
            Line(5f, 200f).At(58f, 26f, 32f).Named("plank ramps"),
            Launch(5f).At(56f, -26f, 32f).Named("brick tower"),
            Curve(4.8f, 220f, 90f).At(58f, 26f, 32f).Named("lava hall"),
            Line(4.8f, 200f).At(60f, -28f, 30f).Named("green glass room"),
            Snipe(4.2f, 45f).At(60f, 28f, 28f).Named("laser room"),
            Line(6f, 140f).At(54f, -24f, 36f).Named("grey end"),
        };

        // The finale: the hardest stage there is, built from the worst of everything above:
        // a broken spin, tiny snipes, a drop through a hole, a tight spin and a last thin
        // ramp. Every flight is still proved possible.
        public static readonly RampDef[] Finale =
        {
            Spin(7f, 720f, 140f).Broken(10).At(64f, 32f, 26f).Named("broken spin"),
            Snipe(3.2f, 30f).At(76f, -38f, 20f).Named("snipe"),
            Drop(3.5f, 70f).At(74f, 32f, 22f).Window().Named("through the hole"),
            Spin(7f, 630f, 140f).Broken(6).At(68f, -36f, 22f).Named("broken spin again"),
            Snipe(3.2f, 30f).At(80f, 40f, 18f).Window().Named("snipe through the wall"),
            Line(3.2f, 200f).At(82f, -42f, 18f).Named("the last ramp"),
        };
    }
}
