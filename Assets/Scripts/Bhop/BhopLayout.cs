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

    // The 10 stage bhop challenge behind the start hall. Each stage is designed as a movement
    // sequence, not a list of blocks: every hop says how hard it is (the speed it asks for as a
    // share of what an expert has by then: 70% strafing efficiency from the stage's pad), how it
    // turns and how its height changes, and the block goes where that hop comes down. Heights
    // set the rhythm (an up-step shortens a hop, a drop lengthens it, at the same speed), tight
    // turns cap the speed you can carry (the tightest air turn has a radius of about v^2/49 m),
    // so the difficulty is speed, timing, angle and momentum, not just small blocks.
    //
    // The stages are then folded into a descending spiral round a central spire: each stage
    // leaves its checkpoint pad in the heading that keeps it on a ring round the spire, clear of
    // every other stage at a similar height (and of the hall), always on round the same way.
    // Each stage after the first opens with a drop-in off its pad, 8 m down.
    public static class BhopLayout
    {
        // Source physics in metres (PlayerMovement's)
        const double U = 0.0254, G = 800 * U, VJ = 301.993 * U, A = 30 * U, Run = 250 * U;
        // Where you may land: the player is a capsule, and its round bottom catching a block's edge
        // takes nearly all your speed, so a landing counts with your middle this far in from the edge
        public const double EdgeIn = 0.2;
        const double Expert = 0.7;

        public static readonly Vector2 Spire = new(-4f, -185f);
        public const float SpireRadius = 6f;
        const double Ring = 75, Clear = 13, Vert = 16, SpireKeep = 20;
        static readonly Vector2 PlazaPad = new(-4f, -98f);

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
            public float r;
            internal Hop[] program;
        }

        public static readonly StageDef[] Stages =
        {
            new() { name = "Sky Steps", line = "warm-up: bhop, strafe, a first long jump", r = 0.6f, program = new[]
            {
                Start(8f),
                B(0, 0, 4f, 3.5f, label: "first hops"),
                B(0, 8, 4f, 3.5f),
                B(0, 8, 3.5f, 3f),
                B(-1, 0, 3.5f, 3f, label: "first drop"),
                B(0, -12, 3f, 3f),
                B(0, -12, 3f, 3f),
                B(-2, 0, 4f, 4f, r: 0.7f, label: "long jump"),
                B(0, 18, 3.5f, 3.5f, r: 0.55f),
                B(0, 18, 4f, 4f, r: 0.5f),
                Pad(8f, r: 0.45f),
            } },
            new() { name = "Lantern Garden", line = "narrow planks: hold your line, hold your beat", r = 0.64f, program = new[]
            {
                Start(6f),
                B(-8, 0, 4.5f, 1.2f, label: "drop in: planks"),
                B(0, 0, 4.5f, 1.2f),
                B(0, 6, 4.5f, 1.1f),
                B(0, 6, 4.5f, 1.1f),
                B(0, 0, 1.3f, 4f, r: 0.74f, label: "crosswise beat"),
                B(0, 0, 1.3f, 4f, r: 0.74f),
                B(0, 0, 1.2f, 4f, r: 0.76f),
                B(0, -20, 4f, 1.3f, label: "plank slalom"),
                B(0, 35, 4f, 1.3f),
                B(0, -35, 4f, 1.3f),
                B(0, 20, 4f, 1.3f),
                B(0, 0, 5f, 4f, r: 0.55f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Red Spires", line = "build speed, then the long gaps", r = 0.86f, program = new[]
            {
                Start(6f),
                B(-8, 0, 5f, 5f, r: 0.6f, label: "drop in: build"),
                B(0, 0, 5f, 5f, r: 0.65f),
                B(0, 0, 4.5f, 4.5f, r: 0.7f),
                B(0, 0, 4f, 4f, r: 0.75f),
                B(-2, 0, 3.5f, 3.5f, label: "downhill"),
                B(-2, 0, 3f, 3f),
                B(-2, 5, 3f, 3f),
                B(-2, 5, 2.6f, 2.6f),
                B(-1, 0, 3f, 3f, r: 0.92f, label: "long gap"),
                B(-1, 0, 2.8f, 2.8f, r: 0.9f, label: "long gap"),
                B(-2, 0, 4f, 4f, r: 0.9f, label: "max gap"),
                B(0, 15, 4f, 4f, r: 0.55f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Banked Bowl", line = "slopes, banks and a curved ramp: turn without losing speed", r = 0.8f, program = new[]
            {
                Start(6f),
                B(-8, 0, 4f, 3f, r: 0.65f, label: "drop in"),
                B(0, 0, 3.5f, 3f, r: 0.7f),
                B(-2.5f, 0, 5f, 4f, kind: BhopKind.Slope, tilt: 22, label: "slope boost"),
                B(-1, 0, 3f, 3f),
                B(0, 36, 3f, 2.5f, kind: BhopKind.Bank, tilt: 20, label: "banked 180"),
                B(0, 36, 3f, 2.5f, kind: BhopKind.Bank, tilt: 20),
                B(0, 36, 3f, 2.5f, kind: BhopKind.Bank, tilt: 20),
                B(0, 36, 3f, 2.5f, kind: BhopKind.Bank, tilt: 20),
                B(0, 36, 3f, 2.5f, kind: BhopKind.Bank, tilt: 20),
                Ramp(26, 4, -90, "curved ramp"),
                B(-2.5f, 0, 8f, 3.5f, r: 0.75f, fall: true, label: "ramp to block"),
                B(0, -30, 3f, 2.5f, r: 0.72f),
                B(0, 20, 4f, 4f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Null Room", line = "small blocks, heights set the rhythm", r = 0.9f, program = new[]
            {
                Start(6f),
                B(-8, 0, 2.2f, 2.2f, r: 0.7f, label: "drop in"),
                B(0, 0, 2f, 2f, r: 0.75f),
                B(1, 0, 1.8f, 1.8f, label: "up"),
                B(-1.5f, 0, 1.8f, 1.8f, label: "down"),
                B(1, 10, 1.6f, 1.6f),
                B(-1.5f, 10, 1.6f, 1.6f),
                B(0, -25, 1.5f, 1.5f, label: "offsets"),
                B(0, 25, 1.4f, 1.4f),
                B(1.2f, -25, 1.3f, 1.3f),
                B(-2, 25, 1.3f, 1.3f),
                B(0, 0, 1.35f, 1.35f, r: 0.85f, label: "tiny"),
                B(-1, 0, 1.35f, 1.35f, r: 0.85f),
                B(0, 0, 3.5f, 3.5f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Neon Grid", line = "a speed ramp, 90s at speed, a hairpin you must slow for", r = 0.88f, program = new[]
            {
                Start(8f),
                B(-8, 0, 4f, 4f, r: 0.7f, label: "drop in"),
                Ramp(40, 8, 0, "speed ramp"),
                B(-2.5f, 0, 8f, 3.5f, r: 0.8f, fall: true, label: "off the ramp"),
                B(0, 0, 4f, 4f, r: 0.8f),
                B(0, 45, 4f, 4f, r: 0.82f, label: "90 at speed"),
                B(0, 45, 4f, 4f, r: 0.82f),
                B(0, -45, 4f, 4f, r: 0.82f),
                B(0, -45, 4f, 4f, r: 0.82f),
                B(0, 20, 3f, 3f, L: 11f, label: "check your speed"),
                B(0, 60, 2.5f, 2.5f, L: 7f, label: "hairpin: slow down"),
                B(0, 60, 2.5f, 2.5f, L: 7f),
                B(0, 60, 2.5f, 2.5f, L: 7f),
                B(0, 0, 3f, 3f, r: 0.8f, label: "rebuild"),
                B(0, 0, 3f, 3f),
                B(0, 0, 3f, 3f),
                Pad(6f, r: 0.6f),
            } },
            new() { name = "Glass Slalom", line = "left, right, left: then a curved ramp's momentum", r = 0.9f, program = new[]
            {
                Start(6f),
                B(-8, 0, 3f, 3f, r: 0.7f, label: "drop in"),
                B(0, 0, 3f, 3f, r: 0.8f),
                B(0, 0, 2.5f, 2.5f),
                B(0, 40, 2f, 2f, label: "slalom"),
                B(0, -80, 2f, 2f),
                B(0, 80, 2f, 2f),
                B(0, -80, 2f, 2f),
                B(0, 80, 2f, 2f),
                B(0, -40, 2f, 2f),
                B(0, 0, 3f, 3f, r: 0.75f),
                Ramp(30, 6, 90, "curved ramp"),
                B(-2.5f, 0, 8f, 3f, r: 0.8f, fall: true, label: "ramp to platform"),
                B(0, 60, 2f, 2f, r: 0.72f, label: "momentum transfer"),
                B(0, 0, 4f, 4f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Shard Field", line = "tiny shards, long gaps, speed control", r = 0.95f, program = new[]
            {
                Start(6f),
                B(-8, 0, 12f, 8f, r: 0.6f, kind: BhopKind.Field, label: "drop in: a wide landing"),
                B(-1, 0, 1.2f, 1.2f, r: 1.05f, label: "shards"),
                B(-1, 0, 1.1f, 1.1f),
                B(0, 10, 1f, 1f),
                B(-3, 0, 1f, 1f, label: "drop"),
                B(1.2f, 0, 1f, 1f, L: 6f, label: "slow down"),
                B(1.2f, -20, 1f, 1f, L: 6.2f),
                B(0, 0, 1f, 1f),
                B(-2, 0, 1.2f, 1.2f, r: 0.97f, label: "long gap"),
                B(0, 15, 1f, 1f),
                B(0, 0, 3f, 3f, r: 0.6f, label: "rest"),
                Pad(6f, r: 0.5f),
            } },
            new() { name = "Gauntlet", line = "everything at once, almost nowhere to rest", r = 0.97f, program = new[]
            {
                Start(4f),
                B(-8, 0, 4f, 1f, r: 0.75f, label: "drop in: planks"),
                B(0, 0, 4f, 1f, r: 0.85f),
                B(0, 40, 2.5f, 2f, r: 0.9f, kind: BhopKind.Bank, tilt: 20, label: "banks"),
                B(0, 40, 2.5f, 2f, r: 0.9f, kind: BhopKind.Bank, tilt: 20),
                B(0, 40, 2.5f, 2f, r: 0.9f, kind: BhopKind.Bank, tilt: 20),
                B(1.2f, 0, 1.6f, 1.6f, r: 0.88f, label: "climb"),
                B(1.2f, 0, 1.6f, 1.6f, r: 0.88f),
                B(1.2f, 0, 1.6f, 1.6f, r: 0.9f),
                B(1.2f, 0, 1.6f, 1.6f, r: 0.9f),
                B(-2, 0, 1f, 1f, label: "tiny"),
                B(0, -30, 1f, 1f),
                Ramp(26, 5, -60, "ramp"),
                B(-2.5f, 0, 8f, 2f, r: 0.85f, fall: true, label: "ramp to plank"),
                B(0, -60, 2f, 2f, L: 6f, label: "hairpin"),
                B(0, -60, 2f, 2f, L: 6f),
                B(0, -60, 2f, 2f, L: 6f),
                B(0, 0, 2f, 2f, r: 0.85f),
                B(-2, 0, 2f, 2f, r: 1f, label: "max gap"),
                Pad(6f, r: 0.6f),
            } },
            new() { name = "Ascension", line = "the mastery test", r = 1.02f, program = new[]
            {
                Start(6f),
                B(-8, 0, 3f, 3f, r: 0.8f, label: "drop in"),
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
                Ramp(38, 8, 90, "final ramp"),
                B(-2.5f, 0, 8f, 3f, r: 0.9f, fall: true, label: "ramp to plank"),
                B(-3, 0, 3f, 3f, L: 25f, label: "final jump"), // (from the plank: about 23 m/s off the final ramp)
                Pad(10f, r: 0.6f, label: "finish"),
            } },
        };

        public static double Airtime(double dh)
        {
            double d = VJ * VJ - 2 * G * dh;
            return d < 0 ? -1 : (VJ + System.Math.Sqrt(d)) / G;
        }

        static int Ticks(double t) => (int)System.Math.Round(t * 64.0, System.MidpointRounding.ToEven);

        // One stage laid from its pad (x, z, top) leaving in heading psi0 (degrees)
        static List<BhopPiece> RunStage(int stage, Vector3 start, double psi0, StageDef def, out Vector3 end, out double endHeading)
        {
            var program = def.program;
            double x = start.x, z = start.z, h = start.y, psi = psi0 * System.Math.PI / 180.0;
            double ps = program[0].pad;
            var pieces = new List<BhopPiece>
            {
                new() { kind = BhopKind.Pad, stage = stage, center = new Vector3((float)x, (float)h, (float)z), size = new Vector2((float)ps, (float)ps), yaw = (float)psi0, landing = new Vector3((float)x, (float)h, (float)z) },
            };
            double px = x + System.Math.Sin(psi) * (ps / 2 - 0.6), pz = z + System.Math.Cos(psi) * (ps / 2 - 0.6);
            double ePerf = Run, eHum = Run;
            Vector3 lastSolid = pieces[0].center;
            for (int k = 1; k < program.Length; k++)
            {
                var hop = program[k];
                if (hop.kind == BhopKind.Ramp)
                {
                    // a hop onto the ramp's face (a metre down), then its riding line
                    double rT = Airtime(-1.0), rg = A * A * Ticks(rT);
                    double rPerf = (ePerf + System.Math.Sqrt(ePerf * ePerf + rg)) / 2, rHum = (eHum + System.Math.Sqrt(eHum * eHum + rg * Expert)) / 2;
                    double rL = (hop.r > 0 ? hop.r : def.r) * rHum * rT + 1.0;
                    px += System.Math.Sin(psi) * rL;
                    pz += System.Math.Cos(psi) * rL;
                    h -= 1.0;
                    ePerf = System.Math.Sqrt(ePerf * ePerf + rg);
                    eHum = System.Math.Sqrt(eHum * eHum + rg * Expert);
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
                    ePerf = System.Math.Sqrt(ePerf * ePerf + 2 * G * hop.drop * 0.92);
                    eHum = System.Math.Sqrt(eHum * eHum + 2 * G * hop.drop * 0.92);
                    px = rx; pz = rz; h = rh; psi = ang;
                    continue;
                }
                double dh = hop.dh, turn = hop.turn * System.Math.PI / 180.0;
                double T = hop.fall ? System.Math.Sqrt(2 * System.Math.Max(-dh, 0.05) / G) : Airtime(dh);
                double g = A * A * Ticks(T);
                double mPerf = (ePerf + System.Math.Sqrt(ePerf * ePerf + g)) / 2;
                double mHum = (eHum + System.Math.Sqrt(eHum * eHum + g * Expert)) / 2;
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
                if (hop.kind == BhopKind.Again) piece.center = lastSolid; // (another hop on the same field)
                else lastSolid = piece.center;
                pieces.Add(piece);
                ePerf = System.Math.Min(System.Math.Sqrt(ePerf * ePerf + g), vcap * 1.03);
                eHum = System.Math.Min(System.Math.Sqrt(eHum * eHum + g * Expert), vcap * 1.03);
                px = lx; pz = lz;
                // (the next hop from a pad's middle, or from the middle of a strip off a ramp:
                // riders come off a ramp short of the plan, landing round the strip's middle)
                if (hop.kind == BhopKind.Pad || hop.fall) { px = cx; pz = cz; }
            }
            var last = pieces[pieces.Count - 1];
            end = last.center;
            endHeading = psi * 180.0 / System.Math.PI;
            return pieces;
        }

        static IEnumerable<Vector3> Points(List<BhopPiece> pieces, int from)
        {
            for (int i = from; i < pieces.Count; i++)
            {
                if (pieces[i].kind == BhopKind.Ramp) foreach (var q in pieces[i].line) yield return q;
                else yield return pieces[i].center;
            }
        }

        static bool Conflict(List<Vector3> body, List<Vector3> placed, Vector3 shared)
        {
            foreach (var p in body)
            {
                if (p.x > -30f && p.x < 22f && p.z > -60f && p.z < 5f) return true; // the hall and its back terrace
                if (Vector2.Distance(new Vector2(p.x, p.z), Spire) < SpireKeep) return true;
                bool near = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(shared.x, shared.z)) < 22f;
                foreach (var q in placed)
                {
                    if (near && Vector2.Distance(new Vector2(q.x, q.z), new Vector2(shared.x, shared.z)) < 22f) continue; // (both by the pad they share)
                    if (Mathf.Abs(q.y - p.y) < Vert && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)) < Clear) return true;
                }
            }
            return false;
        }

        // All ten stages, folded round the spire, then the trail and the plaza
        public static List<BhopPiece> Build(out float[] headings)
        {
            var all = new List<BhopPiece>();
            var placed = new List<Vector3>();
            var pos = new Vector3(PlazaPad.x, 0f, PlazaPad.y);
            headings = new float[Stages.Length];
            for (int i = 0; i < Stages.Length; i++)
            {
                double bestScore = double.MaxValue;
                List<BhopPiece> best = null;
                Vector3 bestEnd = default;
                for (int h = 0; h < 360; h += 5)
                {
                    var pieces = RunStage(i + 1, pos, h, Stages[i], out var end, out _);
                    var body = new List<Vector3>(Points(pieces, i > 0 ? 1 : 0));
                    if (Conflict(body, placed, pos)) continue;
                    double sum = 0, rmax = 0;
                    foreach (var p in body)
                    {
                        double r = Vector2.Distance(new Vector2(p.x, p.z), Spire);
                        sum += System.Math.Abs(r - Ring);
                        rmax = System.Math.Max(rmax, r);
                    }
                    double a0 = System.Math.Atan2(pos.x - Spire.x, pos.z - Spire.y), a1 = System.Math.Atan2(end.x - Spire.x, end.z - Spire.y);
                    double da = (a1 - a0 + System.Math.PI) % (2 * System.Math.PI);
                    if (da < 0) da += 2 * System.Math.PI;
                    da -= System.Math.PI;
                    double wrong = System.Math.Max(0, -da);
                    double score = sum / body.Count + 0.3 * System.Math.Max(0, rmax - Ring - 25) + 120 * wrong;
                    if (score < bestScore) { bestScore = score; best = pieces; bestEnd = end; headings[i] = h; }
                }
                if (best == null) throw new System.Exception($"Bhop: no room for stage {i + 1}");
                placed.AddRange(Points(best, 0));
                all.AddRange(i == 0 ? best : best.GetRange(1, best.Count - 1));
                pos = bestEnd;
            }
            AddTrail(all, headings[0]);
            return all;
        }

        // The way out to it: a terrace outside the hall's back archway, a bhop trail of wide
        // steps anyone can hop at a run, and the plaza (the challenge's sign, its prizes and
        // stage 1's pad, which sits on the plaza's edge where the course leaves it)
        public static readonly Vector3 Terrace = new(-4f, 0f, -55.5f);
        static void AddTrail(List<BhopPiece> all, float exitHeading)
        {
            all.Add(new BhopPiece { kind = BhopKind.Trail, stage = 0, center = Terrace, size = new Vector2(7f, 9f), yaw = 180f, landing = Terrace, label = "terrace" });
            float a = exitHeading * Mathf.Deg2Rad;
            var plaza = new Vector3(PlazaPad.x - Mathf.Sin(a) * 9f, 0f, PlazaPad.y - Mathf.Cos(a) * 9f);
            Vector3 from = Terrace + Vector3.back * 4.5f, to = plaza + Vector3.forward * 8.6f;
            const int steps = 5;
            for (int k = 1; k <= steps; k++)
            {
                float t = k / (steps + 1f);
                var p = Vector3.Lerp(from, to, t) + Vector3.right * (Mathf.Sin(t * Mathf.PI * 2f) * 3f);
                all.Add(new BhopPiece { kind = BhopKind.Trail, stage = 0, center = p, size = new Vector2(4f, 4f), yaw = 180f, landing = p, label = k == 1 ? "bhop trail" : null });
            }
            all.Add(new BhopPiece { kind = BhopKind.Plaza, stage = 0, center = plaza, size = new Vector2(18f, 18f), yaw = exitHeading, landing = plaza, label = "plaza" });
        }
    }
}
