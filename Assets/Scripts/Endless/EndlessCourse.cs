using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoidFlow
{
    // The one course: endless, generated as you go, getting harder the further you get.
    //
    // Streaming: only a few ramps exist at a time, rampsAhead in front of you (hidden in fog)
    // and rampsBehind behind. Each new ramp is built when you move on, at most one per frame,
    // and old ones are destroyed along with their meshes, so memory stays flat however far
    // you go.
    //
    // Biomes: every rampsPerBiome ramps the world changes (sky, fog, light, ramp colors,
    // scenery), blending over as you cross into it.
    //
    // Moves: each ramp is one of
    //  - catch:  a wide prism that catches you and dives to rebuild speed (every 4th-5th ramp)
    //  - blade:  a thin, straight prism
    //  - plunge: a prism that dives steeply, like falling, then levels out and launches
    //  - climb:  you land, then ride up a rising ramp that throws you high off the top
    //  - corner: a slab sweeping around a big banked turn
    //  - hole:   a slab with a second wall facing it across a gap, like surfing a canyon
    // and any flight can be big air: extra long, with a glowing ring at the top to fly
    // through. Harder moves unlock as you go.
    //
    // Difficulty: over the first rampsToMaxDifficulty ramps the flights get longer, the
    // sideways shifts bigger, the landing hills shorter, and the ramps narrower.
    //
    // Every flight is checked before its ramp appears (see FlightCheck): a player at the
    // slowest speed a decent run has by then must be able to land it. If not, the ramp is
    // rebuilt with a shorter gap and smaller shift, so the course never asks the impossible.
    //
    // Floating origin: the whole world is shifted back near zero every recenterDistance
    // metres, so long runs never lose floating point precision.
    public class EndlessCourse : MonoBehaviour
    {
        [Header("Scene")]
        public PlayerMovement player;
        public Transform startHall;
        public Light sun;
        public Camera view;
        [Tooltip("Materials for each biome, in the same order as Biome.All")]
        public BiomeKit[] kits;

        [Header("Streaming")]
        public int rampsAhead = 3;
        public int rampsBehind = 1;
        public float recenterDistance = 2000f;
        public int rampsPerBiome = 6;
        [Tooltip("0 makes a new random course every run")]
        public int seed;

        [Header("Difficulty")]
        public int rampsToMaxDifficulty = 40;
        [Tooltip("Speed (u/s) flights are designed around. Faster runs fly further and must air-brake or land later on the landing hill.")]
        public float designSpeed = 2300f;

        const float BiomeBlendSeconds = 3f;
        const int MaxRebuilds = 6;
        const float FallMargin = 40f;

        class Segment
        {
            public int index, biome;
            public RampShapes.RampPath path;
            public Vector3[] line;          // riding line, course-local
            public GameObject root;
            public string move;
            public readonly List<Mesh> meshes = new();
            public float startProgress, flightSpeed, lowestY;
        }

        readonly List<Segment> segments = new();
        System.Random rng;
        int nextIndex, current;
        float nextProgress;
        RampShapes.RampPath last;
        Mesh cube;
        Vector3 hallOffset;
        Biome envFrom, envTo;
        float envBlend = 1f;

        public int CurrentRamp => current;
        public int Level => Mathf.Min(current, rampsToMaxDifficulty);
        public Biome CurrentBiome => Biome.All[BiomeOf(current)];
        public float Progress { get; private set; }
        public int ActiveRamps => segments.Count;
        public int RebuiltRamps { get; private set; }     // ramps rebuilt because the flight check failed
        public int ImpossibleRamps { get; private set; }  // ramps still failing after every rebuild (should stay 0)
        public event Action<Biome> BiomeEntered;

        void Start()
        {
            QualitySettings.shadowDistance = 150f;
            if (rng == null) ResetCourse(); // unless the run timer already started it
        }

        void Update() => Step(Time.deltaTime);

        // Builds a fresh course from the start hall
        public void ResetCourse()
        {
            foreach (var seg in segments) DestroySegment(seg);
            segments.Clear();
            if (startHall)
            {
                startHall.position -= hallOffset;
                startHall.gameObject.SetActive(true);
            }
            hallOffset = Vector3.zero;

            rng = new System.Random(seed != 0 ? seed : Environment.TickCount);
            nextIndex = 0;
            current = 0;
            last = null;
            nextProgress = 0f;
            Progress = 0f;
            RebuiltRamps = 0;
            ImpossibleRamps = 0;

            if (!cube)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube = temp.GetComponent<MeshFilter>().sharedMesh;
                Kill(temp);
            }

            envFrom = envTo = Biome.All[0];
            envBlend = 1f;
            ApplyEnvironment(envTo, envTo, 1f);
            Stream(all: true);
        }

        // Advances the course around the player. Called every frame; the surf bot calls it
        // directly when testing outside play mode.
        public void Step(float dt)
        {
            if (!player) return;
            UpdateCurrent(player.Position);
            Stream(all: false);
            Recenter();
            if (envBlend < 1f)
            {
                envBlend = Mathf.Min(1f, envBlend + dt / BiomeBlendSeconds);
                ApplyEnvironment(envFrom, envTo, Mathf.SmoothStep(0f, 1f, envBlend));
            }
        }

        int BiomeOf(int ramp) => ramp / rampsPerBiome % Biome.All.Length;

        Segment Seg(int index)
        {
            foreach (var s in segments)
                if (s.index == index) return s;
            return null;
        }

        void Stream(bool all)
        {
            while (nextIndex <= current + rampsAhead)
            {
                Generate();
                if (!all) break; // at most one new ramp per frame, so there are no hitches
            }
            while (segments.Count > 0 && segments[0].index < current - rampsBehind)
            {
                DestroySegment(segments[0]);
                segments.RemoveAt(0);
            }
            if (startHall) startHall.gameObject.SetActive(current < 2);
        }

        float Difficulty(int ramp) => Mathf.Clamp01(ramp / (float)rampsToMaxDifficulty);
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // Flight speed (m/s) off the end of a ramp: what you'd have from the height dropped
        // so far, up to designSpeed
        float FlightSpeed(RampShapes.RampPath from, float startY)
        {
            float drop = startY - (from.End.y - RampShapes.FaceDepth(from.width, from.width * RampShapes.RideFraction));
            float fromDrop = Mathf.Sqrt(2f * RampShapes.Gravity * Mathf.Max(drop, 1f)) * 0.95f;
            return Mathf.Min(fromDrop, designSpeed * PlayerMovement.SourceUnit);
        }

        // The speed (u/s) the next flight ahead of `p` was designed for: the flight onto the
        // first ramp whose start is still in front of you. Faster than that, you'd overshoot
        // the landing hill; the surf bot air-brakes down to it like a skilled player would.
        public float SpeedNeededAhead(Vector3 p)
        {
            Segment target = TargetSegment(p);
            return target != null ? target.flightSpeed / PlayerMovement.SourceUnit : float.MaxValue;
        }

        // How far (m) you are from the end of the ramp you're on
        public float DistanceToEnd(Vector3 p)
        {
            Segment seg = Seg(current);
            if (seg == null) return float.MaxValue;
            DistanceToLine(seg, p, out int nearest);
            return seg.path.Length - seg.path.distance[nearest];
        }

        // The ramp you're flying toward (or will fly to next): the first one whose start is
        // still in front of you
        public RampShapes.RampPath TargetRamp(Vector3 p) => TargetSegment(p)?.path;

        Segment TargetSegment(Vector3 p)
        {
            for (int k = current; k <= current + 1; k++)
            {
                Segment seg = Seg(k);
                if (seg != null && seg.index > 0 && Vector3.Dot(seg.line[0] - p, seg.path.forward[0]) > 0f)
                    return seg;
            }
            return Seg(current + 1);
        }

        // The slowest a decent run is likely to be going off a ramp whose flight is designed
        // for `flightSpeed` (m/s): the flight check proves a player this slow can land it
        static float SlowestLikelySpeed(float flightSpeed) =>
            Mathf.Clamp(flightSpeed * 0.8f, 1300f * PlayerMovement.SourceUnit, 1900f * PlayerMovement.SourceUnit);

        float courseStartY;

        void Generate()
        {
            int i = nextIndex++;
            int biomeIndex = BiomeOf(i);
            Biome biome = Biome.All[biomeIndex];
            BiomeKit kit = kits[biomeIndex];
            float t = Difficulty(i);
            float flightSpeed = 0f;
            string move = "opening drop";
            RampShapes.RampPath holeWall = null;
            Vector3? ringCenter = null;
            Vector3 ringFacing = Vector3.forward;

            RampShapes.RampPath path;
            if (last == null)
            {
                // The opening drop off the start hall's ledge: wide, dives, levels, rises to launch
                path = RampShapes.Lay(RampShapes.Kind.Prism, 18f, -1f, new Vector3(0f, -1f, 2f), Vector3.forward,
                    new[] { (0f, 0f), (40f, -0.45f), (90f, -0.45f), (180f, 0f), (230f, 0.08f) }, null);
                courseStartY = 0f;
            }
            else
            {
                var m = ChooseMove(i, t);
                move = m.name;
                flightSpeed = FlightSpeed(last, courseStartY);
                var landing = new RampShapes.Landing
                {
                    speed = flightSpeed,
                    gap = Mathf.Lerp(30f, 50f, t) * Rand(0.9f, 1.1f) * (m.bigAir ? 1.6f : 1f),
                    length = Mathf.Lerp(70f, 45f, t) * (m.bigAir ? 1.3f : 1f),
                    clearStart = Mathf.Lerp(7f, 5f, t) * (m.bigAir ? 1.3f : 1f),
                    clearEnd = 0f,
                    shift = Mathf.Lerp(10f, 22f, t) * Rand(0.85f, 1.1f) * (rng.Next(2) == 0 ? -1f : 1f),
                };

                // Prove the flight can be landed; if not, shorten the gap and shift and retry
                float slowest = SlowestLikelySpeed(flightSpeed);
                path = RampShapes.LandingRamp(last, landing, m.kind, m.width, m.shape, m.bend);
                for (int attempt = 0; !FlightCheck.Possible(last, path, slowest, landing.length + 20f); attempt++)
                {
                    if (attempt == MaxRebuilds) { ImpossibleRamps++; break; }
                    RebuiltRamps++;
                    landing.gap *= 0.85f;
                    landing.shift *= 0.8f;
                    path = RampShapes.LandingRamp(last, landing, m.kind, m.width, m.shape, m.bend);
                }
                if (m.bigAir)
                {
                    move += " + big air";
                    var (_, arcY, launch) = RampShapes.Flight(last, flightSpeed);
                    Vector3 fwd = last.EndForward, side = new Vector3(fwd.z, 0f, -fwd.x);
                    float d = landing.gap * 0.5f;
                    Vector3 center = launch + fwd * d + side * (landing.shift * 0.5f);
                    center.y = arcY(d) + 0.9f;
                    ringCenter = center;
                    ringFacing = fwd;
                }
                if (m.hole) holeWall = RampShapes.HoleWall(path, 3f, landing.length + 15f, 25f);
                nextProgress += landing.gap;
            }
            last = path;

            var seg = new Segment
            {
                index = i,
                biome = biomeIndex,
                move = move,
                path = path,
                flightSpeed = flightSpeed,
                startProgress = nextProgress,
                lowestY = float.MaxValue,
                line = new Vector3[path.ridge.Count],
            };
            for (int k = 0; k < path.ridge.Count; k++)
            {
                seg.line[k] = path.RideLine(k);
                seg.lowestY = Mathf.Min(seg.lowestY, path.ridge[k].y - path.Depth);
            }
            nextProgress += path.Length;

            seg.root = new GameObject($"Ramp {i}: {move} ({biome.name}, {path.width:0}m)");
            seg.root.transform.SetParent(transform, false);

            Material surface = path.kind == RampShapes.Kind.Slab ? kit.slab : kit.ramp;
            AddPart(seg, "Surface", path.ridge[0], RampShapes.BuildMesh(path, $"Ramp {i}"), surface, solid: true);
            AddPart(seg, "Trim", path.ridge[0], RampShapes.TrimMesh(path, $"Trim {i}"), kit.trim, solid: false);
            if (holeWall != null)
            {
                AddPart(seg, "HoleWall", holeWall.ridge[0], RampShapes.BuildMesh(holeWall, $"Hole {i}"), kit.slab, solid: true);
                AddPart(seg, "HoleTrim", holeWall.ridge[0], RampShapes.TrimMesh(holeWall, $"HoleTrim {i}"), kit.trim, solid: false);
            }
            if (ringCenter is Vector3 ring)
                AddPart(seg, "Ring", ring, RampShapes.RingMesh(ring, ringFacing, 6f, $"Ring {i}"), kit.glowAlt, solid: false);

            Scenery.Line(path, biome, kit, seg.root.transform, rng, cube);
            segments.Add(seg);
            Physics.SyncTransforms();
        }

        // A mesh object under a segment. Solid ones are surf surfaces with collision and
        // shadows; the rest are glow and decoration, drawn cheaply.
        static void AddPart(Segment seg, string name, Vector3 position, Mesh mesh, Material mat, bool solid)
        {
            seg.meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(seg.root.transform, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            else
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        struct Move
        {
            public string name;
            public RampShapes.Kind kind;
            public float width;
            public (float, float)[] shape, bend;
            public bool hole, bigAir;
        }

        // Picks the next ramp's move. Every 4th ramp (5th once it gets hard) is a catch ramp;
        // the rest are a weighted pick of the moves unlocked so far.
        Move ChooseMove(int i, float t)
        {
            float flat = Mathf.Lerp(120f, 80f, t);
            var blade = new[] { (flat, 0f), (flat + 40f, 0.08f) };
            if (i % (t < 0.5f ? 4 : 5) == 0)
                return new Move { name = "catch", kind = RampShapes.Kind.Prism, width = 18f, shape = new[] { (80f, -0.35f), (200f, 0f), (250f, 0.08f) } };

            var options = new List<(string name, float weight)> { ("blade", 3f), ("plunge", 2f), ("climb", 2f) };
            if (t >= 0.1f) options.Add(("corner", 2.5f));
            if (t >= 0.2f) options.Add(("hole", 2f));
            float total = 0f;
            foreach (var o in options) total += o.weight;
            float pick = Rand(0f, total);
            string name = options[^1].name;
            foreach (var o in options)
            {
                if (pick < o.weight) { name = o.name; break; }
                pick -= o.weight;
            }

            var m = new Move { name = name, bigAir = t >= 0.08f && name != "hole" && rng.NextDouble() < 0.25 };
            switch (name)
            {
                case "plunge": // dive at ~31 degrees, level out over a long 150m bend, launch
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(10f, 6f, t);
                    m.shape = new[] { (30f, -0.6f), (90f, -0.6f), (240f, 0f), (270f, 0.08f) };
                    break;
                case "climb": // level, then rise to ~11 degrees and launch high
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(10f, 6f, t);
                    m.shape = new[] { (50f, 0f), (130f, 0.2f), (150f, 0.2f) };
                    break;
                case "corner": // a slab sweeping 40-65 degrees around, banked into the turn
                {
                    m.kind = RampShapes.Kind.Slab;
                    m.width = Mathf.Lerp(13f, 10f, t);
                    m.shape = new[] { (220f, 0f), (250f, 0.06f) };
                    float degrees = Mathf.Lerp(40f, 65f, t) * Rand(0.85f, 1f);
                    m.bend = new[] { (20f, 0f), (220f, degrees), (250f, degrees) };
                    break;
                }
                case "hole": // a slab with a wall facing it, bending up to 35 degrees
                {
                    m.kind = RampShapes.Kind.Slab;
                    m.width = Mathf.Lerp(12f, 9f, t);
                    m.shape = blade;
                    m.hole = true;
                    float degrees = Mathf.Lerp(0f, 35f, t) * Rand(0.5f, 1f);
                    m.bend = new[] { (30f, 0f), (flat + 30f, degrees), (flat + 40f, degrees) };
                    break;
                }
                default: // blade
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(7f, 5f, t);
                    m.shape = blade;
                    break;
            }
            return m;
        }

        public string CurrentMove => Seg(current)?.move ?? "";

        // For testing: where the current and next ramps' riding lines are relative to `p`
        // (nearest point, as offset right/up/forward in that ramp's frame)
        public string DescribeAround(Vector3 p)
        {
            var sb = new System.Text.StringBuilder();
            for (int k = current; k <= current + 1; k++)
            {
                Segment seg = Seg(k);
                if (seg == null) continue;
                DistanceToLine(seg, p, out int n);
                Vector3 d = seg.line[n] - p;
                sb.Append($"ramp {k + 1}: nearest line pt #{n}/{seg.line.Length} is {Vector3.Dot(d, seg.path.right[n]):0.0} right, {d.y:0.0} up, {Vector3.Dot(d, seg.path.forward[n]):0.0} ahead; ");
            }
            return sb.ToString();
        }

        void DestroySegment(Segment seg)
        {
            Kill(seg.root);
            foreach (var m in seg.meshes) Kill(m);
        }

        static void Kill(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // Which ramp are we on? Move on to the next one once we're closer to its riding line
        // than to this one's (usually partway through the flight onto it).
        void UpdateCurrent(Vector3 p)
        {
            while (true)
            {
                Segment here = Seg(current), next = Seg(current + 1);
                if (here == null || next == null) break;
                if (DistanceToLine(next, p, out _) >= DistanceToLine(here, p, out _)) break;
                current++;
                if (next.biome != here.biome)
                {
                    envFrom = Biome.All[here.biome];
                    envTo = Biome.All[next.biome];
                    envBlend = 0f;
                    BiomeEntered?.Invoke(envTo);
                }
            }
            Segment seg = Seg(current);
            if (seg != null)
            {
                DistanceToLine(seg, p, out int nearest);
                Progress = seg.startProgress + seg.path.distance[nearest];
            }
        }

        static float DistanceToLine(Segment seg, Vector3 p, out int nearest)
        {
            float best = float.MaxValue;
            nearest = 0;
            for (int k = 0; k < seg.line.Length; k++)
            {
                float d = (seg.line[k] - p).sqrMagnitude;
                if (d < best) { best = d; nearest = k; }
            }
            return best;
        }

        public bool IsFallen(Vector3 p)
        {
            Segment here = Seg(current), next = Seg(current + 1);
            if (here == null) return false;
            float floor = here.lowestY;
            if (next != null) floor = Mathf.Min(floor, next.lowestY);
            return p.y < floor - FallMargin;
        }

        // Puts the player back at the start of the ramp they were on, moving along it at the
        // speed its flight was designed for, so the run keeps flowing
        public void RespawnPlayer(PlayerMovement p)
        {
            Segment seg = Seg(current);
            if (seg == null) return;
            var path = seg.path;
            int i = Mathf.Min(4, path.ridge.Count - 2);
            Vector3 pos = path.FacePoint(i, RampShapes.RideFraction) + Vector3.up * 1.2f;
            Vector3 along = path.ridge[i + 1] - path.ridge[i];
            float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
            p.Teleport(pos, yaw, along.normalized * (seg.flightSpeed * 0.9f));
        }

        // Keeps the world near the origin: once the player is recenterDistance out, shift
        // everything (ramps, their data, the hall, the player) back by that much
        void Recenter()
        {
            Vector3 p = player.Position;
            if (p.magnitude < recenterDistance) return;
            Vector3 delta = -p;
            foreach (var seg in segments)
            {
                seg.root.transform.localPosition += delta;
                for (int k = 0; k < seg.path.ridge.Count; k++) seg.path.ridge[k] += delta;
                for (int k = 0; k < seg.line.Length; k++) seg.line[k] += delta;
                seg.lowestY += delta.y;
            }
            courseStartY += delta.y;
            if (startHall)
            {
                startHall.position += delta;
                hallOffset += delta;
            }
            player.ShiftOrigin(delta);
            Physics.SyncTransforms();
        }

        // The bot's guide: a point `lookahead` metres ahead along the riding line from where
        // we are, and the line's direction here (both flat, world x/z)
        public bool TryGetGuide(Vector3 p, float lookahead, out Vector2 target, out Vector2 along)
        {
            target = along = Vector2.zero;
            Segment seg = Seg(current);
            if (seg == null) return false;
            DistanceToLine(seg, p, out int nearest);
            int next = Mathf.Min(nearest + 1, seg.line.Length - 1);
            Vector3 dir = seg.line[next] - seg.line[Mathf.Max(next - 1, 0)];
            along = new Vector2(dir.x, dir.z);

            float want = seg.path.distance[nearest] + lookahead;
            Vector3 point;
            if (want <= seg.path.Length)
            {
                int k = nearest;
                while (k < seg.line.Length - 1 && seg.path.distance[k] < want) k++;
                point = seg.line[k];
            }
            else
            {
                Segment after = Seg(current + 1);
                if (after == null) point = seg.line[^1];
                else
                {
                    float into = Mathf.Max(0f, want - seg.path.Length - 40f);
                    int k = 0;
                    while (k < after.line.Length - 1 && after.path.distance[k] < into) k++;
                    point = after.line[k];
                }
            }
            target = new Vector2(point.x, point.z);
            return true;
        }

        void ApplyEnvironment(Biome a, Biome b, float t)
        {
            Color sky = Color.Lerp(a.sky, b.sky, t);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = Mathf.Lerp(a.fogStart, b.fogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(a.fogEnd, b.fogEnd, t);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, t);
            if (sun)
            {
                sun.color = Color.Lerp(a.sunColor, b.sunColor, t);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
            }
            if (view) view.backgroundColor = sky;
        }
    }
}
