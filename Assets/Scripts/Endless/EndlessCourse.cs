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
    // Zones: every rampsPerBiome ramps the world changes (sky, fog, light, ramp colors,
    // scenery), blending over as you cross into it, and the ramps run through that zone's
    // buildings (Architecture): halls, colonnades, caves, box rooms and more, open at the ends
    // so every flight passes in and out. They go when their ramp does.
    //
    // Stages: each biome's stretch is also a stage with a theme, the way a surf map's stages
    // each have their own character (all original designs): Classic zig-zag blades, Winding
    // S-bends, Spiral turns that keep going the same way, Rollercoaster waves and dives,
    // Canyon holes and corners, Big Air flights, Twin Peaks (ramps side by side, pick your
    // line), and the Curved Room (after the curved rooms of classic flowing maps: ramp after
    // ramp sweeping round the same way at a steady rate, each one leading into the next). A
    // glowing gate marks where each stage starts.
    //
    // Moves: each ramp is one of
    //  - catch:  a wide prism that catches you and dives to rebuild speed (every 4th-5th ramp)
    //  - blade:  a thin, straight prism
    //  - plunge: a prism that dives steeply, like falling, then levels out and launches
    //  - climb:  you land, then ride up a rising ramp that throws you high off the top
    //  - corner: a slab sweeping around a big banked turn
    //  - hole:   a slab with a second wall facing it across a gap, like surfing a canyon
    //  - wave:   a wide prism that rolls up and down, dip after dip
    //  - winding: a prism that sweeps one way then back the other in a long S
    //  - spiral: a long slab that keeps turning into its face as it descends
    //  - twin:   a blade with an identical one alongside
    //  - sweep:  a long prism curving steadily one way, usually into its face, sometimes away
    //            from it (the kind you hold the opposite key on)
    //  - loop:   a long prism curling right round (160-210 degrees) as it drops, wide enough
    //            to hold at full speed
    // Plain blades also bend a little once the course gets going, so it's rarely dead straight.
    // and any flight can be big air: extra long, with a glowing ring at the top to fly
    // through. Harder moves unlock as you go.
    //
    // Tiers: the course breathes. Stage 1 is BEGINNER (wide ramps, gentle hops, long
    // landing hills), then INTERMEDIATE (tighter ramps, bigger gaps and
    // sideways shifts), ADVANCED (tiny ramps, speed sections, short precise landings, more
    // big air), TECHNICAL (every move mixed, twin routes, speed rings) and a BEGINNER
    // breather, round and round, each lap a little harder. Within a tier the difficulty
    // (flight length, shift, landing length, ramp width) climbs from ramp to ramp.
    //
    // Reasons to go again, all in the one run: Void Shards hang on the harder lines
    // (beginner on the riding line, intermediate high near the ridge, advanced in the air,
    // technical on the twin route) and 25 of them make a Void Case; big-air rings boost
    // you when you fly through them; each stage's time is kept per tier (CourseRewards).
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
        [Tooltip("Gradient sky dome; a copy is tinted per biome at runtime")]
        public Material skybox;

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

        const float BiomeBlendSeconds = 5f; // zones melt into each other
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
        Material skyInstance;

        public int CurrentRamp => current;
        public int Level => Mathf.Min(current, rampsToMaxDifficulty);
        public Biome CurrentBiome => Biome.All[BiomeOf(current)];
        public float Progress { get; private set; }
        public int ActiveRamps => segments.Count;
        public int RebuiltRamps { get; private set; }     // ramps rebuilt because the flight check failed
        public int ImpossibleRamps { get; private set; }  // ramps still failing after every rebuild (should stay 0)
        public event Action<Biome> BiomeEntered;

        public static readonly string[] Themes = { "CLASSIC", "WINDING", "SPIRAL", "ROLLERCOASTER", "CANYON", "BIG AIR", "TWIN PEAKS", "CURVED ROOM" };
        readonly Dictionary<int, string> stageThemes = new();
        readonly Dictionary<int, float> stageTurn = new();
        public enum Tier { Beginner, Intermediate, Advanced, Technical }
        public static readonly string[] TierNames = { "BEGINNER", "INTERMEDIATE", "ADVANCED", "TECHNICAL" };
        public static readonly Color[] TierColors =
        {
            new(0.4f, 1f, 0.55f), new(0.35f, 0.75f, 1f), new(1f, 0.58f, 0.2f), new(1f, 0.3f, 0.8f),
        };
        static readonly Tier[] TierCycle = { Tier.Intermediate, Tier.Advanced, Tier.Technical, Tier.Beginner };
        public static Tier TierOf(int stage) => stage <= 0 ? Tier.Beginner : TierCycle[(stage - 1) % TierCycle.Length];
        public Tier CurrentTier => TierOf(CurrentStage);
        public string CurrentTierName => TierNames[(int)CurrentTier];

        [Tooltip("Crystal material for Void Shards")]
        public Material shardMaterial;
        static Mesh shardMesh;

        public int CurrentStage => current / rampsPerBiome;
        public string CurrentStageName => ThemeOf(CurrentStage);

        // Each stage's theme: the first is always Classic; later ones are picked from those the
        // difficulty has unlocked, never the same twice in a row
        string ThemeOf(int stage)
        {
            if (stageThemes.TryGetValue(stage, out var name)) return name;
            if (stage == 0) name = "CLASSIC";
            else
            {
                float t = Difficulty(stage * rampsPerBiome);
                var open = new List<string> { "CLASSIC", "ROLLERCOASTER", "TWIN PEAKS" };
                if (t >= 0.15f) open.Add("CURVED ROOM");
                if (t >= 0.28f) open.Add("WINDING");
                if (t >= 0.28f) open.Add("CANYON");
                if (t >= 0.42f) open.Add("BIG AIR");
                if (t >= 0.42f) open.Add("SPIRAL");
                if (stageThemes.TryGetValue(stage - 1, out var before) && open.Count > 1) open.Remove(before);
                name = open[rng.Next(open.Count)];
            }
            stageThemes[stage] = name;
            stageTurn[stage] = rng.Next(2) == 0 ? -1f : 1f;
            return name;
        }

        void Start()
        {
            QualitySettings.shadowDistance = 150f;
            if (rng == null) ResetCourse(); // unless the run timer already started it
            if (Application.isEditor) LogRenderSetup();
        }

        // Editor-only diagnostic: what the renderer is actually using when you press Play
        void LogRenderSetup()
        {
            var cam = view ? view : Camera.main;
            var sb = new System.Text.StringBuilder("VOIDFLOW RENDER SETUP: ");
            sb.Append($"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} pipeline={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name} ");
            sb.Append($"fog={RenderSettings.fog} mode={RenderSettings.fogMode} start={RenderSettings.fogStartDistance} end={RenderSettings.fogEndDistance} density={RenderSettings.fogDensity} color={RenderSettings.fogColor} ");
            sb.Append($"ambient={RenderSettings.ambientMode} sky={RenderSettings.ambientSkyColor} ");
            if (cam) sb.Append($"camera={cam.name} pos={cam.transform.position} near={cam.nearClipPlane} far={cam.farClipPlane} clear={cam.clearFlags} bg={cam.backgroundColor} ");
            if (sun) sb.Append($"sun={sun.intensity} ");
            int renderers = 0, near = 0;
            foreach (var r in FindObjectsByType<MeshRenderer>())
            {
                renderers++;
                if (cam && (r.bounds.center - cam.transform.position).magnitude < 60f)
                {
                    near++;
                    if (near <= 3 && r.TryGetComponent(out MeshFilter filter))
                        sb.Append($"[{r.name}: mat={(r.sharedMaterial ? r.sharedMaterial.name : "none")} mesh={(filter.sharedMesh ? filter.sharedMesh.name : "none")} enabled={r.enabled}] ");
                }
            }
            sb.Append($"renderers={renderers} within60m={near}");
            Debug.Log(sb.ToString());
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
            lastFrame = null;
            nextProgress = 0f;
            Progress = 0f;
            stageThemes.Clear();
            stageTurn.Clear();
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

        // How hard a ramp is (0..1): its tier's range, climbing through the stage, plus a
        // little more every lap round the tiers
        float Difficulty(int ramp)
        {
            int stage = ramp / rampsPerBiome;
            float within = (ramp % rampsPerBiome) / (float)Mathf.Max(1, rampsPerBiome - 1);
            // The first lap round the tiers eases you in; after that each tier is itself
            int lapIndex = Mathf.Max(0, stage - 1) / TierCycle.Length;
            var (from, to) = (TierOf(stage), lapIndex == 0) switch
            {
                (Tier.Beginner, true) => (0f, 0.1f),
                (Tier.Intermediate, true) => (0.15f, 0.3f),
                (Tier.Advanced, true) => (0.35f, 0.55f),
                (Tier.Technical, true) => (0.5f, 0.7f),
                (Tier.Beginner, _) => (0f, 0.12f),
                (Tier.Intermediate, _) => (0.3f, 0.45f),
                (Tier.Advanced, _) => (0.6f, 0.8f),
                _ => (0.8f, 1f),
            };
            float lap = Mathf.Min(Mathf.Max(0, lapIndex - 1), 3) * 0.05f;
            return Mathf.Clamp01(Mathf.Lerp(from, to, within) + lap);
        }
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
            bool enclose = false;
            Func<float, float> flightArc = null;
            float flightGap = 0f;
            RampShapes.RampPath holeWall = null, twin = null;
            int stage = i / rampsPerBiome;
            string theme = ThemeOf(stage);
            bool stageStart = i > 0 && i % rampsPerBiome == 0;
            Vector3? ringCenter = null;
            Vector3 ringFacing = Vector3.forward;

            airShards.Clear();
            RampShapes.RampPath path;
            if (last == null)
            {
                // The opening drop off the start hall's ledge: wide, dives, levels, rises to launch
                path = RampShapes.Lay(RampShapes.Kind.Prism, 18f, -1f, new Vector3(0f, -1f, 2f), Vector3.forward,
                    new[] { (0f, 0f), (40f, -0.45f), (90f, -0.45f), (180f, 0f), (230f, 0.08f) }, null);
                path.taper = false; // you drop straight onto its start from the hall
                courseStartY = 0f;
            }
            else
            {
                var tier = TierOf(stage);
                var m = ChooseMove(i, t, theme, tier);
                move = m.name;
                enclose = !m.twin && !m.hole;
                flightSpeed = FlightSpeed(last, courseStartY);
                var landing = new RampShapes.Landing
                {
                    speed = flightSpeed,
                    gap = Mathf.Lerp(30f, 50f, t) * Rand(0.9f, 1.1f) * (m.bigAir ? 1.6f : 1f),
                    length = Mathf.Lerp(70f, 45f, t) * (m.bigAir ? 1.3f : 1f),
                    clearStart = Mathf.Lerp(7f, 5f, t) * (m.bigAir ? 1.3f : 1f),
                    clearEnd = 0f,
                    shift = Mathf.Lerp(10f, 22f, t) * Rand(0.85f, 1.1f) * ShiftSign(i, theme, stage),
                };

                // Tiers: beginner hops are short onto long wide hills, intermediate gaps and
                // shifts are bigger, advanced landings are short and must be precise
                var (gapK, lengthK, shiftK, clearK) = tier switch
                {
                    Tier.Beginner => (0.85f, 1.35f, 0.8f, 1.1f),
                    Tier.Intermediate => (1.12f, 1f, 1.15f, 1f),
                    Tier.Advanced => (1.05f, 0.85f, 1f, 0.85f),
                    _ => (1f, 0.95f, 1.05f, 0.9f),
                };
                landing.gap *= gapK;
                landing.length *= lengthK;
                landing.shift *= shiftK;
                landing.clearStart *= clearK;

                // Each stage opens gently, like a map's stage start: a shorter hop onto a long,
                // forgiving landing hill
                if (stageStart)
                {
                    landing.gap *= 0.8f;
                    landing.length *= 1.4f;
                    landing.shift *= 0.7f;
                }

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
                flightArc = RampShapes.Flight(last, flightSpeed).height;
                flightGap = landing.gap;
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
                // Advanced and technical stages hang shards along the flight, a little above the
                // plain arc: take a good line to collect them
                if (tier >= Tier.Advanced && rng.NextDouble() < (tier == Tier.Advanced ? 0.55 : 0.3))
                {
                    var (_, arc, from) = RampShapes.Flight(last, flightSpeed);
                    Vector3 fwd = last.EndForward, side = new Vector3(fwd.z, 0f, -fwd.x);
                    foreach (float f in new[] { 0.3f, 0.45f, 0.6f, 0.75f })
                    {
                        float d = landing.gap * f;
                        Vector3 at = from + fwd * d + side * (landing.shift * f * f);
                        at.y = arc(d) + 1.4f;
                        airShards.Add(at);
                    }
                }
                if (m.hole) holeWall = RampShapes.HoleWall(path, 3f, landing.length + 15f, 25f);
                // The twin stands behind the ramp you land on (seen from the flight), so it
                // never blocks the way in
                if (m.twin) twin = RampShapes.Offset(path, -path.side * (2f * path.width + 7f), 2f, landing.length + 10f, 55f);
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

            seg.root = new GameObject($"Ramp {i}: {move} ({theme}, {biome.name}, {path.width:0}m)");
            seg.root.transform.SetParent(transform, false);

            Material surface = path.kind == RampShapes.Kind.Slab ? kit.slab : kit.ramp;
            AddPart(seg, "Surface", path.ridge[0], RampShapes.BuildMesh(path, $"Ramp {i}"), surface, solid: true);
            AddPart(seg, "Trim", path.ridge[0], RampShapes.TrimMesh(path, $"Trim {i}"), kit.trim, solid: false);
            if (holeWall != null)
            {
                AddPart(seg, "HoleWall", holeWall.ridge[0], RampShapes.BuildMesh(holeWall, $"Hole {i}"), kit.slab, solid: true);
                AddPart(seg, "HoleTrim", holeWall.ridge[0], RampShapes.TrimMesh(holeWall, $"HoleTrim {i}"), kit.trim, solid: false);
            }
            if (twin != null)
            {
                AddPart(seg, "Twin", twin.ridge[0], RampShapes.BuildMesh(twin, $"Twin {i}"), kit.ramp, solid: true);
                AddPart(seg, "TwinTrim", twin.ridge[0], RampShapes.TrimMesh(twin, $"TwinTrim {i}"), kit.trim, solid: false);
            }
            if (stageStart)
            {
                // A gate over the start of the landing hill, marking the new stage
                Vector3 foot = seg.line[0] + Vector3.down * 3f;
                AddPart(seg, "StageGate", foot, RampShapes.GateMesh(foot, path.forward[0], 22f, 16f, $"Gate {i}"), kit.glow, solid: false);
            }
            if (ringCenter is Vector3 ring)
            {
                AddPart(seg, "Ring", ring, RampShapes.RingMesh(ring, ringFacing, 6f, $"Ring {i}"), kit.glowAlt, solid: false);
                var boost = seg.root.transform.Find("Ring").gameObject.AddComponent<SpeedRing>();
                boost.facing = ringFacing;
                boost.radius = 6f;
            }
            if (i > 0) PlaceShards(seg, path, twin, TierOf(stage));

            Scenery.Line(path, biome, kit, seg.root.transform, rng, cube);
            BuildArchitecture(seg, path, biome, kit, i, enclose, stageStart, flightArc, flightGap);
            segments.Add(seg);
            Physics.SyncTransforms();
        }

        readonly List<Vector3> airShards = new();

        // The last cross-section of the previous ramp's building, if it was a continuous one:
        // the next building starts by joining onto it across the flight
        Architecture.Frame? lastFrame;

        void BuildArchitecture(Segment seg, RampShapes.RampPath path, Biome biome, BiomeKit kit, int i, bool enclose, bool stageStart,
            Func<float, float> flightArc, float flightGap)
        {
            var style = biome.style;
            if (!enclose) { lastFrame = null; return; }
            float step = Architecture.StepFor(style);
            if (!Architecture.Continuous(style))
            {
                // Open zones: a building over the middle of the ramp only
                float from = Mathf.Min(path.Length * 0.1f, 30f), to = Mathf.Min(path.Length * 0.88f, path.Length - 25f);
                if (to - from >= 30f)
                    Architecture.Build(Architecture.RampFrames(path, from, to, step), biome, kit, seg.root.transform, seg.meshes, rng, cube);
                lastFrame = null;
                return;
            }
            // Continuous: the whole ramp (the opening drop starts clear of the hall), joined to
            // the building before it across the flight
            var frames = Architecture.RampFrames(path, i == 0 ? 45f : 0f, path.Length, step, Architecture.DepthFor(style));
            if (frames.Count == 0) { lastFrame = null; return; }
            int doorway = stageStart ? 0 : -1;
            if (lastFrame is Architecture.Frame prev && flightArc != null)
            {
                var join = Architecture.FlightFrames(prev, frames[0], flightArc, flightGap, step);
                frames.InsertRange(0, join);
                if (stageStart) doorway = Mathf.Min(1, frames.Count - 1);
            }
            Architecture.Build(frames, biome, kit, seg.root.transform, seg.meshes, rng, cube, doorway);
            lastFrame = frames[^1];
        }

        // Void Shards for this ramp, on the line its tier rewards: beginner on the plain
        // riding line, intermediate high up near the ridge, technical on the twin route, and
        // (advanced, technical) the ones hung along the flight in
        void PlaceShards(Segment seg, RampShapes.RampPath path, RampShapes.RampPath twin, Tier tier)
        {
            foreach (var at in airShards) AddShard(seg, at);
            double chance = tier switch { Tier.Beginner => 0.35, Tier.Intermediate => 0.45, Tier.Advanced => 0.2, _ => 0.4 };
            if (rng.NextDouble() >= chance) return;
            var on = tier == Tier.Technical && twin != null ? twin : path;
            float fraction = tier switch { Tier.Beginner => RampShapes.RideFraction * 0.8f, Tier.Intermediate => 0.07f, _ => 0.2f };
            int count = 4 + rng.Next(2);
            float start = Rand(0.25f, 0.4f);
            for (int k = 0; k < count; k++)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt((start + k * 0.08f) * (on.ridge.Count - 1)), 0, on.ridge.Count - 1);
                Vector3 face = on.FacePoint(n, fraction);
                Vector3 outward = on.right[n] * on.side;
                AddShard(seg, face + Vector3.up * 1.3f + outward * 0.7f);
            }
        }

        void AddShard(Segment seg, Vector3 at)
        {
            if (!shardMaterial) return;
            if (!shardMesh) shardMesh = WeaponBuilder.CrystalMesh(0.9f, 0.22f, 6, 0.4f);
            var go = new GameObject("Void Shard");
            go.transform.SetParent(seg.root.transform, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = shardMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = shardMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<VoidShard>();
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
            public bool hole, bigAir, twin;
        }

        // Which way the next flight shifts sideways: Classic stages zig-zag left and right, Spiral
        // stages keep turning the same way, the rest are random
        float ShiftSign(int i, string theme, int stage) => theme switch
        {
            "CLASSIC" => i % 2 == 0 ? 1f : -1f,
            "SPIRAL" or "CURVED ROOM" => stageTurn[stage],
            _ => rng.Next(2) == 0 ? -1f : 1f,
        };

        // Picks the next ramp's move. Every 4th ramp (5th once it gets hard) is a catch ramp to
        // rebuild speed; the rest are a weighted pick of the moves this stage's theme favours.
        Move ChooseMove(int i, float t, string theme, Tier tier)
        {
            float flat = Mathf.Lerp(120f, 80f, t);
            var blade = new[] { (flat, 0f), (flat + 40f, 0.08f) };
            int catchEvery = t < 0.5f ? 4 : 5;
            if (i % catchEvery == 0)
                return new Move { name = "catch", kind = RampShapes.Kind.Prism, width = 18f, shape = new[] { (70f, -0.26f), (180f, 0f), (230f, 0.08f) } };

            var options = theme switch
            {
                "WINDING" => new List<(string, float)> { ("winding", 5f), ("sweep", 2f), ("blade", 1f) },
                "CURVED ROOM" => new List<(string, float)> { ("sweep", 4f), ("loop", 3f), ("spiral", 1f), ("corner", 1f) },
                "SPIRAL" => new List<(string, float)> { ("spiral", 5f), ("corner", 1f) },
                "ROLLERCOASTER" => new List<(string, float)> { ("wave", 4f), ("plunge", 2f), ("climb", 2f), ("sweep", 1.5f) },
                "CANYON" => new List<(string, float)> { ("hole", 4f), ("corner", 2f), ("sweep", 1f) },
                "TWIN PEAKS" => new List<(string, float)> { ("twin", 5f), ("blade", 1f) },
                "BIG AIR" => new List<(string, float)> { ("blade", 3f), ("plunge", 2f), ("climb", 2f), ("sweep", 1f) },
                _ => new List<(string, float)> { ("blade", 4f), ("sweep", 2.5f), ("loop", 1.5f), ("plunge", 1f), ("climb", 1f) },
            };
            // Advanced adds speed sections (plunges); technical throws every move it has in
            if (tier == Tier.Advanced) options.Add(("plunge", 3f));
            if (tier == Tier.Technical)
                options.AddRange(new[] { ("twin", 3f), ("sweep", 2f), ("loop", 1f), ("plunge", 1.5f), ("wave", 1f), ("corner", 1f), ("climb", 1f), ("winding", 1f), ("hole", 1f) });
            float total = 0f;
            foreach (var o in options) total += o.Item2;
            float pick = Rand(0f, total);
            string name = options[^1].Item1;
            foreach (var o in options)
            {
                if (pick < o.Item2) { name = o.Item1; break; }
                pick -= o.Item2;
            }

            // Loops come once the course gets going
            if (name == "loop" && t < 0.25f) name = "sweep";
            bool calm = name is "hole" or "spiral" or "winding" or "sweep" or "loop";
            double airChance = theme == "BIG AIR" ? 0.7 : tier switch { Tier.Beginner => 0.0, Tier.Advanced or Tier.Technical => 0.4, _ => 0.2 };
            var m = new Move { name = name, bigAir = t >= 0.08f && !calm && rng.NextDouble() < airChance };
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
                case "wave": // rolling dips: down, up, down, up, launch
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(13f, 9f, t);
                    m.shape = new[] { (45f, -0.32f), (100f, 0.04f), (155f, -0.32f), (210f, 0.04f), (245f, -0.1f), (275f, 0.08f) };
                    break;
                case "winding": // a long S: sweeps one way, then back past straight
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(11f, 7f, t);
                    float a = Mathf.Lerp(18f, 30f, t) * Rand(0.85f, 1f);
                    m.shape = new[] { (40f, -0.12f), (300f, -0.12f), (330f, 0.07f) };
                    m.bend = new[] { (20f, 0f), (130f, a), (250f, -a * 0.6f), (330f, -a * 0.6f) };
                    break;
                }
                case "spiral": // a long slab that keeps turning into its face while it descends
                {
                    m.kind = RampShapes.Kind.Slab;
                    m.width = Mathf.Lerp(16f, 13f, t);
                    float degrees = Mathf.Lerp(55f, 85f, t) * Rand(0.9f, 1f);
                    m.shape = new[] { (40f, -0.08f), (360f, -0.08f), (400f, 0.06f) };
                    m.bend = new[] { (20f, 0f), (380f, degrees), (400f, degrees) };
                    break;
                }
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
                case "sweep": // a long prism curving round at a steady rate
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(10f, 7f, t);
                    float length = Rand(200f, 260f);
                    m.shape = new[] { (40f, -0.1f), (length - 30f, -0.1f), (length, 0.07f) };
                    // Into the face, mostly; in the Curved Room always, so the turns keep flowing
                    float way = theme == "CURVED ROOM" || rng.NextDouble() < 0.7 ? 1f : -1f;
                    float degrees = Mathf.Lerp(35f, 75f, t) * Rand(0.8f, 1f) * way;
                    m.bend = new[] { (20f, 0f), (length - 30f, degrees), (length, degrees) };
                    break;
                }
                case "loop": // a long prism curling right round as it drops, into its face
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(11f, 8f, t);
                    float length = Rand(450f, 520f);
                    m.shape = new[] { (40f, -0.15f), (length - 30f, -0.15f), (length, 0.07f) };
                    float degrees = Rand(160f, 210f);
                    m.bend = new[] { (20f, 0f), (length - 30f, degrees), (length, degrees) };
                    break;
                }
                case "twin": // a blade with an identical one alongside
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(8f, 6f, t);
                    m.shape = blade;
                    m.twin = true;
                    break;
                default: // blade, with a gentle bend once the course gets going
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(7f, 5f, t);
                    m.shape = blade;
                    if (t > 0.1f)
                    {
                        float degrees = Rand(8f, 22f) * (rng.NextDouble() < 0.6 ? 1f : -1f);
                        m.bend = new[] { (30f, 0f), (flat + 30f, degrees), (flat + 40f, degrees) };
                    }
                    break;
            }
            // Wide ramps for beginners, tiny ones for advanced
            m.width *= tier switch { Tier.Beginner => 1.2f, Tier.Advanced => 0.8f, Tier.Technical => 0.9f, _ => 1f };
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
            if (lastFrame is Architecture.Frame lf)
            {
                Architecture.Shift(ref lf, delta);
                lastFrame = lf;
            }
            if (startHall)
            {
                startHall.position += delta;
                hallOffset += delta;
            }
            player.ShiftOrigin(delta);
            Physics.SyncTransforms();
        }

        // A spot on the riding line of the ramp `ahead` past the one you're on, `along` (0..1)
        // of the way down it, with its travel directions and the ramp's root: things parented
        // to it move with the floating origin and go when the ramp does
        public bool TrySpotAhead(int ahead, float along, out Vector3 point, out Vector3 forward, out Vector3 right, out Transform root)
        {
            point = forward = right = Vector3.zero;
            root = null;
            Segment seg = Seg(current + ahead);
            if (seg == null || !seg.root || seg.line.Length == 0) return false;
            int n = Mathf.Clamp(Mathf.RoundToInt(along * (seg.line.Length - 1)), 0, seg.line.Length - 1);
            point = transform.TransformPoint(seg.line[n]);
            int m = Mathf.Min(n, seg.path.forward.Count - 1);
            forward = seg.path.forward[m];
            right = seg.path.right[m];
            root = seg.root.transform;
            return true;
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

            if (skybox)
            {
                if (!skyInstance) skyInstance = new Material(skybox) { name = "Sky (runtime)" };
                RenderSettings.skybox = skyInstance;
                skyInstance.SetColor("_Horizon", sky);
                skyInstance.SetColor("_Top", Color.Lerp(a.skyTop, b.skyTop, t));
                skyInstance.SetColor("_Bottom", Color.Lerp(a.skyBottom, b.skyBottom, t));
                skyInstance.SetColor("_SunColor", Color.Lerp(a.sunColor, b.sunColor, t) * 0.9f);
                if (sun) skyInstance.SetVector("_SunDir", -sun.transform.forward);
            }
        }
    }
}
