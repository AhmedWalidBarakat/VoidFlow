using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    public enum BhopKind { Pad, Block, Slope, Bank, Field, Again, Ramp, Trail, Plaza }

    // One piece of the bhop challenge, in the start hall's space (x and z across, its top's
    // height in y). Every block carries the hop that reaches it, for the playtest's table.
    [System.Serializable]
    public struct BhopPiece
    {
        public BhopKind kind;
        public int stage;        // 1..10; 0 the trail and plaza
        public Vector3 center;   // the middle of its top
        public Vector2 size;     // along the route x across it
        public float yaw;        // its heading, degrees
        public float tilt;       // slopes: pitched down ahead; banks: rolled toward the turn
        public Vector3 landing;  // where the route comes down on it
        public float hopLength, airtime, dh, turn, minSpeed, maxSpeed, perfect, expert;
        public bool fall;        // reached by flying off a ramp's end, not by a jump
        public string label;
        public Vector3[] line;   // ramps: the riding line, entry to exit
    }

    // A chamber of a stage's room (a stage's room is one or more, chained along its route):
    // walls round its blocks, a floor below them (touch it and you're back on the stage's
    // pad), open to the sky
    [System.Serializable]
    public struct BhopRoom
    {
        public int stage;
        public Vector3 center;   // the middle of its floor
        public Vector2 size;     // inside the walls: along its yaw x across
        public float yaw;        // degrees
        public float top;        // the walls' top
        public float Floor => center.y;
    }

    // The 10 stage bhop challenge behind the start hall. Each stage is designed as a movement
    // sequence, not a list of blocks: every hop says how hard it is (the speed it asks for as a
    // share of what an expert has by then: 70% strafing efficiency from the stage's pad), how it
    // turns and how its height changes, and the block goes where that hop comes down. Heights
    // set the rhythm (an up-step shortens a hop, a drop lengthens it, at the same speed), tight
    // turns cap the speed you can carry (the tightest air turn has a radius of about v^2/49 m),
    // so the difficulty is speed, timing, angle and momentum, not just small blocks.
    //
    // Like the classic bhop maps, each stage holds a pace: an expert keeps to the stage's
    // cruising speed (strafing less rather than gaining), so the blocks sit a classic distance
    // apart (5 to 9 m between middles) and the hard part is holding your speed inside each
    // block's window. Only the speed stage, the long gaps and the ramps' exits run faster.
    //
    // Every stage is a room of its own: walls round its blocks, a floor a few metres under them,
    // its start pad up on a ledge, and at the far end an exit pad with a portal to the next
    // stage's room. The rooms step down one below another round the middle of the course.
    public static class BhopLayout
    {
        // Source physics in metres (PlayerMovement's)
        const double U = 0.0254, G = 800 * U, VJ = 301.993 * U, A = 30 * U, Run = 250 * U;
        // Where you may land: the player is a capsule, and its round bottom catching a block's edge
        // takes nearly all your speed, so a landing counts with your middle this far in from the edge
        public const double EdgeIn = 0.2;
        const double Expert = 0.7;

        // The rooms: walls this far out from the blocks, the floor this far under the lowest,
        // the walls this high over the highest
        public const float Margin = 4.5f, FloorDepth = 4f, Headroom = 6f, Wall = 0.6f;
        static readonly Vector2 Middle = new(-4f, -230f);
        // Stage 1's pad, on the plaza's far edge, and its way off it (south, away from the hall)
        static readonly Vector2 PlazaPad = new(-4f, -108f);
        const float FirstHeading = 180f;

        internal struct Hop
        {
            public BhopKind kind;
            public float dh, turn, along, across, r, L, tilt, len, drop, curve, pad;
            public bool fall;
            public string label;
        }

        static Hop B(float dh, float turn, float along, float across, float r = -1f, BhopKind kind = BhopKind.Block, float tilt = 0f, float L = -1f, bool fall = false, string label = null) =>
            new() { kind = kind, dh = dh, turn = turn, along = along, across = across, r = r, tilt = tilt, L = L, fall = fall, label = label };
        static Hop Pad(float size, float r = -1f, string label = null) => new() { kind = BhopKind.Pad, along = size, across = size, r = r, label = label };
        static Hop Start(float size) => new() { pad = size };
        static Hop Ramp(float len, float drop, float curve, string label = null, float r = -1f) => new() { kind = BhopKind.Ramp, len = len, drop = drop, curve = curve, label = label, r = r };
        static Hop Again(float r) => new() { kind = BhopKind.Again, along = 0.1f, across = 0.1f, r = r };

        public struct StageDef
        {
            public string name, line; // its name, and what it asks of you
            public float r;           // how hard its hops are, unless a hop says
            public float vmax;        // its cruising speed (m/s): an expert holds it here
            internal Hop[] program;
        }

        public static readonly StageDef[] Stages =
        {
            new() { name = "Sky Steps", line = "warm-up: bhop, strafe, a first long jump", r = 0.75f, vmax = 8f, program = new[]
            {
                Start(6f),
                B(0, 0, 3.5f, 3.5f, label: "first hops"),
                B(0, 0, 3.5f, 3.5f),
                B(0, 0, 3f, 3f),
                B(0, 12, 3f, 3f, label: "a gentle curve"),
                B(0, 12, 3f, 3f),
                B(-1, 0, 3f, 3f, label: "first drop"),
                B(0, -15, 3f, 3f),
                B(0, -15, 3f, 3f),
                B(1, 0, 3f, 3f, label: "step up"),
                B(0, 0, 3f, 3f),
                B(-2, 0, 3.5f, 3.5f, r: 0.85f, label: "long jump"),
                B(0, 20, 3f, 3f, r: 0.7f),
                B(0, 20, 3.5f, 3.5f, r: 0.6f),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Lantern Garden", line = "narrow planks: hold your line, hold your beat", r = 0.8f, vmax = 8.5f, program = new[]
            {
                Start(5f),
                B(-3, 0, 4.5f, 1.2f, label: "drop in: planks"),
                B(0, 0, 4.5f, 1.2f),
                B(0, 6, 4.5f, 1.2f),
                B(0, 6, 4.5f, 1.2f),
                B(0, 0, 1.3f, 4f, r: 0.85f, label: "crosswise beat"),
                B(0, 0, 1.3f, 4f, r: 0.85f),
                B(0, 0, 1.2f, 4f, r: 0.88f),
                B(0, -20, 4f, 1.3f, label: "plank slalom"),
                B(0, 35, 4f, 1.3f),
                B(0, -35, 4f, 1.3f),
                B(0, 20, 4f, 1.3f),
                B(0, 0, 4f, 4f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Red Spires", line = "build speed, then the long gaps", r = 0.88f, vmax = 12f, program = new[]
            {
                Start(5f),
                B(-3, 0, 4f, 4f, r: 0.6f, label: "drop in: build"),
                B(0, 0, 4f, 4f, r: 0.65f),
                B(0, 0, 3.5f, 3.5f, r: 0.7f),
                B(0, 0, 3.5f, 3.5f, r: 0.75f),
                B(-1.5f, 0, 3f, 3f, label: "downhill"),
                B(-1.5f, 0, 3f, 3f),
                B(-1.5f, 6, 3f, 3f),
                B(-1.5f, 6, 2.6f, 2.6f),
                B(-1, 0, 3f, 3f, r: 0.91f, label: "long gap"),
                B(-1, 0, 2.8f, 2.8f, r: 0.91f, label: "long gap"),
                B(-2, 0, 3.2f, 3.2f, r: 0.93f, label: "max gap"),
                B(0, 15, 4f, 4f, r: 0.55f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Banked Bowl", line = "slopes, banks and a curved ramp: turn without losing speed", r = 0.9f, vmax = 10.5f, program = new[]
            {
                Start(5f),
                B(-3, 0, 3.5f, 3f, r: 0.7f, label: "drop in"),
                B(0, 0, 3.5f, 3f, r: 0.75f),
                B(-2f, 0, 4.5f, 3.5f, kind: BhopKind.Slope, tilt: 22, label: "slope boost"),
                B(-1, 0, 3f, 3f),
                B(0, 36, 2f, 2f, r: 0.95f, kind: BhopKind.Bank, tilt: 20, label: "banked 180"),
                B(0, 36, 2f, 2f, r: 0.95f, kind: BhopKind.Bank, tilt: 20),
                B(0, 36, 2f, 2f, r: 0.95f, kind: BhopKind.Bank, tilt: 20),
                B(0, 36, 2f, 2f, r: 0.95f, kind: BhopKind.Bank, tilt: 20),
                B(0, 36, 2f, 2f, r: 0.95f, kind: BhopKind.Bank, tilt: 20),
                Ramp(22, 3, -90, "curved ramp"),
                B(-2.5f, 0, 7f, 3.5f, r: 0.75f, fall: true, label: "ramp to block"),
                B(0, -30, 2.2f, 2.2f, r: 0.85f),
                B(0, 20, 4f, 4f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Null Room", line = "small blocks, heights set the rhythm", r = 0.96f, vmax = 9f, program = new[]
            {
                Start(5f),
                B(-3, 0, 2f, 2f, r: 0.7f, label: "drop in"),
                B(0, 0, 1.8f, 1.8f, r: 0.8f),
                B(1, 0, 1.6f, 1.6f, label: "up"),
                B(-1.5f, 0, 1.6f, 1.6f, label: "down"),
                B(1, 10, 1.5f, 1.5f),
                B(-1.5f, 10, 1.5f, 1.5f),
                B(0, -25, 1.4f, 1.4f, label: "offsets"),
                B(0, 25, 1.4f, 1.4f),
                B(1.2f, -25, 1.3f, 1.3f),
                B(-2, 25, 1.3f, 1.3f),
                B(0, 0, 1.1f, 1.1f, r: 0.92f, label: "tiny"),
                B(-1, 0, 1.1f, 1.1f, r: 0.92f),
                B(0, 0, 3f, 3f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Neon Grid", line = "a speed ramp, 90s at speed, a hairpin you must slow for", r = 0.9f, vmax = 10f, program = new[]
            {
                Start(6f),
                B(-3, 0, 4f, 4f, r: 0.7f, label: "drop in"),
                Ramp(34, 4, 0, "speed ramp"),
                B(-2.5f, 0, 7f, 3.5f, r: 0.8f, fall: true, label: "off the ramp"),
                B(0, 0, 4f, 4f, r: 0.8f),
                B(0, 45, 3.5f, 3.5f, r: 0.84f, label: "90 at speed"),
                B(0, 45, 3.5f, 3.5f, r: 0.84f),
                B(0, -45, 3.5f, 3.5f, r: 0.84f),
                B(0, -45, 3.5f, 3.5f, r: 0.84f),
                B(0, 20, 3f, 3f, L: 9f, label: "check your speed"),
                B(0, 60, 2.5f, 2.5f, L: 6.5f, label: "hairpin: slow down"),
                B(0, 60, 2.5f, 2.5f, L: 6.5f),
                B(0, 60, 2.5f, 2.5f, L: 6.5f),
                B(0, 0, 3f, 3f, r: 0.8f, label: "rebuild"),
                B(0, 0, 3f, 3f),
                Pad(6f, r: 0.6f),
            } },
            new() { name = "Glass Slalom", line = "left, right, left: then a curved ramp's momentum", r = 0.95f, vmax = 10f, program = new[]
            {
                Start(5f),
                B(-3, 0, 3f, 3f, r: 0.7f, label: "drop in"),
                B(0, 0, 3f, 3f, r: 0.8f),
                B(0, 0, 2.5f, 2.5f),
                B(0, 40, 1.6f, 1.6f, label: "slalom"),
                B(0, -80, 1.6f, 1.6f),
                B(0, 80, 1.6f, 1.6f),
                B(0, -80, 1.6f, 1.6f),
                B(0, 80, 1.6f, 1.6f),
                B(0, -40, 1.6f, 1.6f),
                B(0, 0, 3f, 3f, r: 0.75f),
                Ramp(26, 3, 90, "curved ramp"),
                B(-2.5f, 0, 7f, 3f, r: 0.8f, fall: true, label: "ramp to platform"),
                B(0, 60, 2f, 2f, r: 0.72f, label: "momentum transfer"),
                B(0, 0, 4f, 4f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Shard Field", line = "tiny shards, long gaps, speed control", r = 0.95f, vmax = 9.5f, program = new[]
            {
                Start(5f),
                B(-3, 0, 10f, 7f, r: 0.6f, kind: BhopKind.Field, label: "drop in: a wide landing"),
                B(-1, 0, 1.2f, 1.2f, r: 1f, label: "shards"),
                B(-1, 0, 1.1f, 1.1f),
                B(0, 10, 1f, 1f),
                B(-2, 0, 1f, 1f, label: "drop"),
                B(1.2f, 0, 1f, 1f, L: 5f, label: "slow down"),
                B(1.2f, -20, 1f, 1f, L: 5.2f),
                B(0, 0, 1f, 1f),
                B(-2, 0, 1.2f, 1.2f, r: 1f, label: "long gap"),
                B(0, 15, 1f, 1f),
                B(0, 0, 3f, 3f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Gauntlet", line = "everything at once, almost nowhere to rest", r = 1.02f, vmax = 10.5f, program = new[]
            {
                Start(5f),
                B(-3, 0, 4f, 1f, r: 0.75f, label: "drop in: planks"),
                B(0, 0, 4f, 1f, r: 0.85f),
                B(0, 40, 2.5f, 2f, r: 0.9f, kind: BhopKind.Bank, tilt: 20, label: "banks"),
                B(0, 40, 2.5f, 2f, r: 0.9f, kind: BhopKind.Bank, tilt: 20),
                B(0, 40, 2.5f, 2f, r: 0.9f, kind: BhopKind.Bank, tilt: 20),
                B(1.2f, 0, 1.4f, 1.4f, r: 0.88f, label: "climb"),
                B(1.2f, 0, 1.4f, 1.4f, r: 0.88f),
                B(1.2f, 0, 1.4f, 1.4f, r: 0.9f),
                B(1.2f, 0, 1.4f, 1.4f, r: 0.9f),
                B(-2, 0, 1f, 1f, label: "tiny"),
                B(0, -30, 1f, 1f),
                Ramp(24, 3, -60, "ramp"),
                B(-2.5f, 0, 7f, 2f, r: 0.85f, fall: true, label: "ramp to plank"),
                B(0, 60, 2f, 2f, L: 6f, label: "hairpin"), // (turning back the other way from the ramp's curve: the stage swings out, its end clear of the ramp)
                B(0, 60, 2f, 2f, L: 6f),
                B(0, 60, 2f, 2f, L: 6f),
                B(0, 0, 2f, 2f, r: 0.85f),
                B(-2, 0, 2f, 2f, r: 1f, label: "max gap"),
                Pad(6f, r: 0.6f),
            } },
            new() { name = "Ascension", line = "the mastery test", r = 1.02f, vmax = 11f, program = new[]
            {
                Start(6f),
                B(-3, 0, 3f, 3f, r: 0.8f, label: "drop in"),
                B(0, 0, 2.5f, 2.5f, r: 0.9f),
                B(-2.5f, 0, 5f, 3f, r: 0.92f, kind: BhopKind.Slope, tilt: 20, label: "slope boosts"),
                B(-2.5f, 0, 5f, 3f, r: 0.92f, kind: BhopKind.Slope, tilt: 20),
                B(-2.5f, 0, 2.5f, 2.5f, r: 0.92f),
                B(-1, 0, 2f, 2f, label: "max speed"),
                B(-1, 0, 1.8f, 1.8f),
                B(-2, 0, 1.8f, 1.8f),
                B(0, 50, 2f, 2f, label: "fast corner"),
                B(0, 50, 2f, 2f),
                B(1.2f, 0, 1.6f, 1.6f, label: "the climb"),
                B(1.2f, 0, 1.6f, 1.6f),
                B(1.2f, 0, 1.6f, 1.6f),
                B(1.2f, 0, 1.6f, 1.6f),
                B(1.2f, 0, 1.6f, 1.6f),
                B(-1, 0, 1f, 1f, label: "tiny"),
                B(0, -40, 1f, 1f),
                Ramp(32, 6, 90, "final ramp"),
                B(-2.5f, 0, 7f, 3f, r: 0.9f, fall: true, label: "ramp to plank"),
                B(-3, 0, 3f, 3f, L: 17f, label: "final jump"), // (from the plank: about 17 m/s off the final ramp)
                Pad(10f, r: 0.6f, label: "finish"),
            } },
        };

        public static double Airtime(double dh)
        {
            double d = VJ * VJ - 2 * G * dh;
            return d < 0 ? -1 : (VJ + System.Math.Sqrt(d)) / G;
        }

        static int Ticks(double t) => (int)System.Math.Round(t * 64.0, System.MidpointRounding.ToEven);

        // One stage laid from its pad (x, z, top) leaving in heading psi0 (degrees): its start
        // pad, its blocks, and its exit pad last
        static List<BhopPiece> RunStage(int stage, Vector3 start, double psi0, StageDef def)
        {
            var program = def.program;
            double x = start.x, z = start.z, h = start.y, psi = psi0 * System.Math.PI / 180.0;
            double ps = program[0].pad, cap = def.vmax;
            var pieces = new List<BhopPiece>
            {
                new() { kind = BhopKind.Pad, stage = stage, center = new Vector3((float)x, (float)h, (float)z), size = new Vector2((float)ps, (float)ps), yaw = (float)psi0, landing = new Vector3((float)x, (float)h, (float)z), label = "start" },
            };
            double px = x + System.Math.Sin(psi) * (ps / 2 - 0.6), pz = z + System.Math.Cos(psi) * (ps / 2 - 0.6);
            double ePerf = Run, eHum = Run;
            // (speed gained in a hop, held to the stage's cruising speed once it's there)
            double Gain(double e, double g) => System.Math.Min(System.Math.Sqrt(e * e + g), System.Math.Max(e, cap));
            Vector3 lastSolid = pieces[0].center;
            for (int k = 1; k < program.Length; k++)
            {
                var hop = program[k];
                if (hop.kind == BhopKind.Ramp)
                {
                    // a hop onto the ramp's face (a metre down), then its riding line
                    double rT = Airtime(-1.0), rg = A * A * Ticks(rT);
                    double nPerf = Gain(ePerf, rg), nHum = Gain(eHum, rg * Expert);
                    double rPerf = (ePerf + nPerf) / 2, rHum = (eHum + nHum) / 2;
                    double rL = (hop.r > 0 ? hop.r : def.r) * rHum * rT + 1.0;
                    px += System.Math.Sin(psi) * rL;
                    pz += System.Math.Cos(psi) * rL;
                    h -= 1.0;
                    ePerf = nPerf;
                    eHum = nHum;
                    const int n = 12;
                    var line = new Vector3[n + 1];
                    double seg = hop.len / n, cur = hop.curve * System.Math.PI / 180.0 / n;
                    double rx = px, rz = pz, rh = h, ang = psi;
                    line[0] = new Vector3((float)rx, (float)rh, (float)rz);
                    for (int s = 0; s < n; s++)
                    {
                        ang += cur;
                        rx += System.Math.Sin(ang) * seg;
                        rz += System.Math.Cos(ang) * seg;
                        double t1 = (s + 1.0) / n;
                        rh = h - hop.drop * t1 * t1 * (3 - 2 * t1);
                        line[s + 1] = new Vector3((float)rx, (float)rh, (float)rz);
                    }
                    pieces.Add(new BhopPiece
                    {
                        kind = BhopKind.Ramp, stage = stage, center = new Vector3((float)px, (float)h, (float)pz), size = new Vector2(hop.len, 3f),
                        yaw = (float)(psi * 180.0 / System.Math.PI), turn = hop.curve, dh = -1f, hopLength = (float)rL, airtime = (float)rT, line = line, label = hop.label,
                        landing = line[0], perfect = (float)rPerf, expert = (float)rHum, minSpeed = (float)((rL - 1.0) / rT), maxSpeed = 99f,
                    });
                    // (the ramp's drop turned into speed, past the stage's cruising speed)
                    ePerf = System.Math.Sqrt(ePerf * ePerf + 2 * G * hop.drop * 0.92);
                    eHum = System.Math.Sqrt(eHum * eHum + 2 * G * hop.drop * 0.92);
                    px = rx; pz = rz; h = rh; psi = ang;
                    continue;
                }
                double dh = hop.dh, turn = hop.turn * System.Math.PI / 180.0;
                double T = hop.fall ? System.Math.Sqrt(2 * System.Math.Max(-dh, 0.05) / G) : Airtime(dh);
                double g = A * A * Ticks(T);
                double nP = Gain(ePerf, g), nH = Gain(eHum, g * Expert);
                double mPerf = (ePerf + nP) / 2;
                double mHum = (eHum + nH) / 2;
                double along = hop.kind == BhopKind.Pad ? 2.8 : hop.kind == BhopKind.Field ? 4.0 : hop.along; // (a pad's or a field's landing zone)
                double L = hop.L > 0 ? hop.L : (hop.r > 0 ? hop.r : def.r) * mHum * T + along / 2 - EdgeIn;
                double vmin = System.Math.Max(0, (L - along / 2 + EdgeIn) / T);
                double vcap = (L + along / 2 - EdgeIn) / T;
                double c = System.Math.Abs(turn) > 1e-6 ? 2 * (L / System.Math.Abs(turn)) * System.Math.Sin(System.Math.Abs(turn) / 2) : L;
                double d = psi + turn / 2;
                double lx = px + System.Math.Sin(d) * c, lz = pz + System.Math.Cos(d) * c;
                psi += turn;
                h += dh;
                double fx = System.Math.Sin(psi), fz = System.Math.Cos(psi);
                double cx = lx, cz = lz;
                if (hop.kind == BhopKind.Pad)
                {
                    cx = lx + fx * (hop.along / 2 - 1.4);
                    cz = lz + fz * (hop.along / 2 - 1.4);
                }
                else if (hop.fall)
                {
                    // (off a ramp: riders come off a little low and slow, landing short of the
                    // plan, so the strip lies mostly behind the planned landing)
                    cx = lx - fx * hop.along / 4;
                    cz = lz - fz * hop.along / 4;
                }
                else if (hop.kind == BhopKind.Field)
                {
                    // (a field to hop on again and again: it runs on ahead of where you land)
                    cx = lx + fx * (hop.along / 2 - 2.0);
                    cz = lz + fz * (hop.along / 2 - 2.0);
                }
                var piece = new BhopPiece
                {
                    kind = hop.kind, stage = stage, center = new Vector3((float)cx, (float)h, (float)cz), size = new Vector2(hop.along, hop.across),
                    yaw = (float)(psi * 180.0 / System.Math.PI), tilt = hop.tilt, landing = new Vector3((float)lx, (float)h, (float)lz),
                    hopLength = (float)L, airtime = (float)T, dh = (float)dh, turn = hop.turn, minSpeed = (float)vmin, maxSpeed = (float)vcap,
                    perfect = (float)mPerf, expert = (float)mHum, fall = hop.fall, label = hop.label,
                };
                if (hop.kind == BhopKind.Pad && piece.label == null) piece.label = "exit";
                if (hop.kind == BhopKind.Again) piece.center = lastSolid; // (another hop on the same field)
                else lastSolid = piece.center;
                pieces.Add(piece);
                ePerf = System.Math.Min(nP, vcap * 1.03);
                eHum = System.Math.Min(nH, vcap * 1.03);
                px = lx; pz = lz;
                // (the next hop from a pad's middle, or from the middle of a strip off a ramp:
                // riders come off a ramp short of the plan, landing round the strip's middle)
                if (hop.kind == BhopKind.Pad || hop.fall) { px = cx; pz = cz; }
            }
            return pieces;
        }

        // ------------------------------------------------------------------ rooms

        // Some of a stage's pieces as points on the ground (blocks' corners, ramps' bodies), and
        // their height range
        static void Outline(List<BhopPiece> pieces, int from, int to, List<Vector2> pts, ref float lo, ref float hi)
        {
            for (int i = from; i <= to; i++)
            {
                var p = pieces[i];
                if (p.kind == BhopKind.Again) continue;
                if (p.kind == BhopKind.Ramp)
                {
                    for (int k = 0; k < p.line.Length; k++)
                    {
                        var q = p.line[k];
                        var f = p.line[Mathf.Min(k + 1, p.line.Length - 1)] - p.line[Mathf.Max(k - 1, 0)];
                        var side = new Vector2(f.z, -f.x).normalized * 4f;
                        pts.Add(new Vector2(q.x, q.z) + side);
                        pts.Add(new Vector2(q.x, q.z) - side);
                        lo = Mathf.Min(lo, q.y - 4f);
                        hi = Mathf.Max(hi, q.y + 2.5f);
                    }
                    continue;
                }
                float a = p.yaw * Mathf.Deg2Rad;
                Vector2 fw = new(Mathf.Sin(a), Mathf.Cos(a)), rt = new(Mathf.Cos(a), -Mathf.Sin(a));
                var c = new Vector2(p.center.x, p.center.z);
                foreach (var (sf, sr) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
                    pts.Add(c + fw * (p.size.x * 0.5f * sf) + rt * (p.size.y * 0.5f * sr));
                float sink = p.kind == BhopKind.Slope || p.kind == BhopKind.Bank ? Mathf.Max(p.size.x, p.size.y) * Mathf.Sin(p.tilt * Mathf.Deg2Rad) * 0.5f : 0f;
                lo = Mathf.Min(lo, p.center.y - sink);
                hi = Mathf.Max(hi, p.center.y + sink);
            }
        }

        // The smallest rectangle round some points, a margin out (or along `fixedYaw`, its back
        // edge at `back` along it)
        static BhopRoom Rect(List<Vector2> pts, float? fixedYaw, float back)
        {
            float bestArea = float.MaxValue, bestYaw = 0f;
            Vector2 bestC = default, bestS = default;
            for (float yaw = 0f; yaw < 180f; yaw += 2.5f)
            {
                float y = fixedYaw ?? yaw;
                float a = y * Mathf.Deg2Rad;
                Vector2 fw = new(Mathf.Sin(a), Mathf.Cos(a)), rt = new(Mathf.Cos(a), -Mathf.Sin(a));
                float f0 = float.MaxValue, f1 = float.MinValue, r0 = float.MaxValue, r1 = float.MinValue;
                foreach (var q in pts)
                {
                    float f = Vector2.Dot(q, fw), r = Vector2.Dot(q, rt);
                    f0 = Mathf.Min(f0, f); f1 = Mathf.Max(f1, f); r0 = Mathf.Min(r0, r); r1 = Mathf.Max(r1, r);
                }
                f0 -= Margin; f1 += Margin; r0 -= Margin; r1 += Margin;
                if (fixedYaw.HasValue) f0 = back;
                float area = (f1 - f0) * (r1 - r0);
                if (area < bestArea)
                {
                    bestArea = area;
                    bestYaw = y;
                    bestS = new Vector2(f1 - f0, r1 - r0);
                    bestC = fw * ((f0 + f1) * 0.5f) + rt * ((r0 + r1) * 0.5f);
                }
                if (fixedYaw.HasValue) break;
            }
            return new BhopRoom { center = new Vector3(bestC.x, 0f, bestC.y), size = bestS, yaw = bestYaw };
        }

        // A stage's room: a chain of chambers along its route, a new one wherever the route has
        // turned more than 70 degrees or run on 55m, each just big enough round its stretch and
        // overlapping the last (they share a block), so a winding stage gets a winding room
        // rather than one big box. They all share the stage's floor, under its lowest piece, and
        // its walls' top, over its highest. Stage 1's first chamber runs along its heading, its
        // back wall just past its pad's front edge (the pad is out on the plaza, the doorway in
        // that wall).
        static List<BhopRoom> Chambers(List<BhopPiece> pieces, float? fixedYaw)
        {
            int stage = pieces[0].stage;
            var groups = new List<(int from, int to)>();
            int g0 = 0;
            float yaw0 = pieces[0].yaw;
            var from = pieces[0].center;
            for (int i = 1; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p.kind == BhopKind.Again) continue;
                float turned = Mathf.Abs(Mathf.DeltaAngle(yaw0, p.yaw));
                float run = new Vector2(p.center.x - from.x, p.center.z - from.z).magnitude;
                if ((turned > 70f || run > 55f) && i - g0 >= 2 && i < pieces.Count - 1)
                {
                    groups.Add((g0, i));
                    g0 = i;
                    yaw0 = p.yaw;
                    from = p.center;
                }
            }
            groups.Add((g0, pieces.Count - 1));
            float lo = float.MaxValue, hi = float.MinValue;
            var rooms = new List<BhopRoom>();
            for (int k = 0; k < groups.Count; k++)
            {
                var pts = new List<Vector2>();
                bool first = fixedYaw.HasValue && k == 0;
                var (a, b) = groups[k];
                if (first) hi = Mathf.Max(hi, pieces[0].center.y);
                Outline(pieces, first ? 1 : a, b, pts, ref lo, ref hi);
                float back = 0f;
                if (first)
                {
                    var pad = pieces[0];
                    float yr = fixedYaw.Value * Mathf.Deg2Rad;
                    back = Vector2.Dot(new Vector2(pad.center.x, pad.center.z), new Vector2(Mathf.Sin(yr), Mathf.Cos(yr))) + pad.size.x * 0.5f + Wall;
                }
                var r = Rect(pts, first ? fixedYaw : null, back);
                r.stage = stage;
                rooms.Add(r);
            }
            for (int k = 0; k < rooms.Count; k++)
            {
                var r = rooms[k];
                r.center.y = lo - FloorDepth;
                r.top = hi + Headroom;
                rooms[k] = r;
            }
            return rooms;
        }

        // A room's corners on the ground, grown by `grow` all round
        public static Vector2[] Corners(in BhopRoom r, float grow = 0f)
        {
            float a = r.yaw * Mathf.Deg2Rad;
            Vector2 fw = new(Mathf.Sin(a), Mathf.Cos(a)), rt = new(Mathf.Cos(a), -Mathf.Sin(a)), c = new(r.center.x, r.center.z);
            float hf = r.size.x * 0.5f + grow, hr = r.size.y * 0.5f + grow;
            return new[] { c + fw * hf + rt * hr, c + fw * hf - rt * hr, c - fw * hf - rt * hr, c - fw * hf + rt * hr };
        }

        // Is a point on the ground inside a room (`grow` out from its walls' inner faces)?
        public static bool Inside(in BhopRoom r, Vector2 q, float grow = 0f)
        {
            float a = r.yaw * Mathf.Deg2Rad;
            var d = q - new Vector2(r.center.x, r.center.z);
            return Mathf.Abs(Vector2.Dot(d, new Vector2(Mathf.Sin(a), Mathf.Cos(a)))) < r.size.x * 0.5f + grow
                && Mathf.Abs(Vector2.Dot(d, new Vector2(Mathf.Cos(a), -Mathf.Sin(a)))) < r.size.y * 0.5f + grow;
        }

        // Do two rectangles on the ground overlap (separating axes)?
        static bool Overlap(Vector2[] a, Vector2[] b)
        {
            foreach (var poly in new[] { a, b })
                for (int i = 0; i < 4; i++)
                {
                    var e = poly[(i + 1) % 4] - poly[i];
                    var n = new Vector2(-e.y, e.x);
                    float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
                    foreach (var p in a) { float d = Vector2.Dot(p, n); a0 = Mathf.Min(a0, d); a1 = Mathf.Max(a1, d); }
                    foreach (var p in b) { float d = Vector2.Dot(p, n); b0 = Mathf.Min(b0, d); b1 = Mathf.Max(b1, d); }
                    if (a1 < b0 || b1 < a0) return false;
                }
            return true;
        }

        static BhopRoom Moved(in BhopRoom local, Vector3 at, float heading)
        {
            var r = local;
            var c = Quaternion.Euler(0f, heading, 0f) * new Vector3(local.center.x, 0f, local.center.z);
            r.center = new Vector3(at.x + c.x, at.y + local.center.y, at.z + c.z);
            r.top = at.y + local.top;
            r.yaw = local.yaw + heading;
            return r;
        }

        // Where the course may go: behind the hall, clear of the surf course (nothing of its 116
        // stages comes within 450m either side, 800m back), and not over the hall, its terrace,
        // the trail or the plaza
        const float MinX = -300f, MaxX = 300f, MinZ = -560f, MaxZ = -34f;
        static bool InBounds(in BhopRoom r)
        {
            foreach (var p in Corners(r, Wall))
                if (p.x < MinX || p.x > MaxX || p.y < MinZ || p.y > MaxZ) return false;
            return true;
        }

        // All ten stages, each in its own room, then the trail and the plaza
        public static List<BhopPiece> Build(out float[] headings) => Build(out headings, out _);

        public static List<BhopPiece> Build(out float[] headings, out BhopRoom[] rooms)
        {
            var all = new List<BhopPiece>();
            headings = new float[Stages.Length];
            var placed = new List<BhopRoom>();
            // (the hall and its terrace, the trail and the plaza: kept clear)
            var keep = new List<BhopRoom>
            {
                new() { center = new Vector3(-4f, -40f, -27f), size = new Vector2(70f, 60f), top = 60f },
                new() { center = new Vector3(PlazaPad.x, -40f, (-60f + PlazaPad.y) * 0.5f), size = new Vector2(PlazaPad.y * -1f - 60f + 8f, 26f), top = 40f },
            };
            var first = RunStage(1, new Vector3(PlazaPad.x, 0f, PlazaPad.y), FirstHeading, Stages[0]);
            foreach (var r0 in Chambers(first, FirstHeading))
            {
                var r = r0;
                r.top = Mathf.Max(r.top, 10.5f); // (its wall over the plaza carries the sign)
                placed.Add(r);
            }
            headings[0] = FirstHeading;
            all.AddRange(first);
            var exit = first[first.Count - 1].center;
            for (int i = 1; i < Stages.Length; i++)
            {
                // the stage laid out from the origin, heading 0, and its room
                var local = RunStage(i + 1, Vector3.zero, 0.0, Stages[i]);
                var room0 = Chambers(local, null);
                float y0 = exit.y - 2f;
                double bestScore = double.MaxValue;
                Vector3 bestAt = default;
                float bestH = 0f;
                for (float x = MinX; x <= MaxX; x += 4f)
                for (float z = MinZ; z <= MaxZ; z += 4f)
                {
                    var at = new Vector3(x, y0, z);
                    double d = new Vector2(x - exit.x, z - exit.z).magnitude;
                    if (d > 140f) continue;
                    for (float h = 0f; h < 360f; h += 10f)
                    {
                        bool bad = false;
                        double toMid = 0;
                        for (int c = 0; c < room0.Count && !bad; c++)
                        {
                            var room = Moved(room0[c], at, h);
                            if (!InBounds(room)) { bad = true; break; }
                            var corners = Corners(room, Wall + 2f);
                            foreach (var k in keep) if (Overlap(corners, Corners(k))) { bad = true; break; }
                            foreach (var o in placed) if (!bad && Overlap(corners, Corners(o, Wall + 2f))) bad = true;
                            toMid += new Vector2(room.center.x - Middle.x, room.center.z - Middle.y).magnitude / room0.Count;
                        }
                        if (bad) continue;
                        // near the last room's exit (a short way through its portal), the rooms
                        // gathered round the middle
                        double score = d + 0.5 * toMid;
                        if (score < bestScore) { bestScore = score; bestAt = at; bestH = h; }
                    }
                }
                if (bestScore == double.MaxValue) throw new System.Exception($"Bhop: no room for stage {i + 1}");
                var pieces = RunStage(i + 1, bestAt, bestH, Stages[i]);
                foreach (var r in room0) placed.Add(Moved(r, bestAt, bestH));
                headings[i] = bestH;
                all.AddRange(pieces);
                exit = pieces[pieces.Count - 1].center;
            }
            rooms = placed.ToArray();
            AddTrail(all);
            return all;
        }

        // The floor of a stage's room
        public static float FloorOf(BhopRoom[] rooms, int stage)
        {
            foreach (var r in rooms) if (r.stage == stage) return r.Floor;
            return -999f;
        }

        // Each stage's start pad and exit pad (the last stage's exit is the finish), as indices
        public static void Pads(BhopPiece[] pieces, out int[] starts, out int[] exits)
        {
            starts = new int[Stages.Length + 2];
            exits = new int[Stages.Length + 2];
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p.kind != BhopKind.Pad || p.stage < 1 || p.stage > Stages.Length) continue;
                if (p.label == "start") starts[p.stage] = i;
                else exits[p.stage] = i;
            }
        }

        // The way out to it: a terrace outside the hall's back archway, a bhop trail of wide
        // steps anyone can hop at a run, and the plaza (the challenge's sign on stage 1's wall,
        // its prizes either side of the doorway, and stage 1's pad in the doorway)
        public static readonly Vector3 Terrace = new(-4f, 0f, -55.5f);
        public const float PlazaDepth = 18f, PlazaWidth = 22f;
        static void AddTrail(List<BhopPiece> all)
        {
            all.Add(new BhopPiece { kind = BhopKind.Trail, stage = 0, center = Terrace, size = new Vector2(7f, 9f), yaw = 180f, landing = Terrace, label = "terrace" });
            // (the plaza's far edge is stage 1's wall, at its pad's front edge)
            var plaza = new Vector3(PlazaPad.x, 0f, PlazaPad.y - 3f + PlazaDepth * 0.5f);
            Vector3 from = Terrace + Vector3.back * 4.5f, to = new Vector3(plaza.x, 0f, plaza.z + PlazaDepth * 0.5f);
            const int steps = 5;
            for (int k = 1; k <= steps; k++)
            {
                float t = k / (steps + 1f);
                var p = Vector3.Lerp(from, to, t) + Vector3.right * (Mathf.Sin(t * Mathf.PI * 2f) * 3f);
                all.Add(new BhopPiece { kind = BhopKind.Trail, stage = 0, center = p, size = new Vector2(4f, 4f), yaw = 180f, landing = p, label = k == 1 ? "bhop trail" : null });
            }
            all.Add(new BhopPiece { kind = BhopKind.Plaza, stage = 0, center = plaza, size = new Vector2(PlazaDepth, PlazaWidth), yaw = FirstHeading, landing = plaza, label = "plaza" });
        }
    }
}
