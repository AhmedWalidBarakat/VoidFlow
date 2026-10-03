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
    //  - loop:   a long prism curling round (130-170 degrees) as it drops, wide enough
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
        const float CourseView = 500f;      // view distance on the course (metres)
        const int MaxRebuilds = 6;
        const float FallMargin = 40f;

        class Segment
        {
            public int index, biome;
            public RampShapes.RampPath path;
            public Vector3[] line;          // riding line, course-local
            public Vector3[] flight;        // the designed flight into this ramp, course-local (may be empty)
            public GameObject root;
            public string move;
            public readonly List<Mesh> meshes = new();
            public float startProgress, flightSpeed, lowestY;
            public Transform pad;           // the checkpoint's drop-in platform (checkpoint ramps only)
        }

        readonly List<Segment> segments = new();
        System.Random rng;
        int nextIndex, current, checkpoint;
        bool inTerrace; // still under the start terrace's sky
        const float CheckpointReach = 45f; // metres from the riding line that count as reaching the ramp
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
        public event Action CheckpointReached;
        public bool HasCheckpoint => checkpoint > 0;

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
        // The very last stage is the finale; the Legend zones before it, and the expert zones
        // before those (hard, but not the end), carry their own names
        public string CurrentTierName => CurrentStage == FinalRamp / rampsPerBiome - 1 ? "FINALE" : CurrentBiome.tier ?? (InLegend(CurrentStage) ? "LEGEND"
            : InFinale(CurrentStage) ? "EXPERT" : TierNames[(int)CurrentTier]);

        [Tooltip("Crystal material for Void Shards")]
        public Material shardMaterial;
        static Mesh shardMesh;

        public int CurrentStage => current / rampsPerBiome;
        bool InLegend(int stage) => stage >= Biome.LegendFrom && stage < Biome.MapsFrom;
        // A checkpoint where each stage (zone) begins, so every stage is ridden in one go
        public bool IsCheckpoint(int ramp) => ramp > 0 && ramp % rampsPerBiome == 0 && !Biome.All[BiomeOf(ramp)].noCheckpoint;
        // How far into the Legend zones a ramp is (0 at their start, 1 at the very end)
        float Hardness(int ramp) => Mathf.Clamp01((ramp - Biome.LegendFrom * rampsPerBiome) / (float)Mathf.Max(1, Biome.MapsFrom * rampsPerBiome - 1 - Biome.LegendFrom * rampsPerBiome));
        // The course ends after the last zone: the final ramp throws you onto the finish
        public int FinalRamp => Biome.All.Length * rampsPerBiome;
        bool InFinale(int stage) => stage >= Biome.FinaleFrom;
        public bool Finished { get; private set; }
        Transform finish;
        public Vector3 FinishSpawn => finish ? finish.position + Vector3.up * 1.5f : Vector3.zero;
        public float FinishYaw => finish ? finish.eulerAngles.y : 0f;
        public event Action CourseFinished;
        public string CurrentStageName => CurrentBiome.section ?? ThemeOf(CurrentStage);

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
            else StartCoroutine(WarmUp());
        }

        // Every zone's materials drawn once, out of sight, while you stand in the start area:
        // browsers prepare each material's shader the first time it's drawn, and without this
        // that happened mid-run, the moment a new zone or wall came into view (a hitch as you
        // turned). A hidden camera with the view's settings draws a tile of each.
        const int WarmLayer = 29;
        System.Collections.IEnumerator WarmUp()
        {
            yield return null;
            if (!view) yield break;
            var mats = new HashSet<Material>();
            void Add(Material m) { if (m) mats.Add(m); }
            foreach (var k in kits)
            {
                if (k == null) continue;
                Add(k.ramp); Add(k.slab); Add(k.scenery); Add(k.glow); Add(k.glowAlt); Add(k.floor); Add(k.accent); Add(k.shaft); Add(k.pool); Add(k.skyPool);
                if (k.rampHues != null) foreach (var m in k.rampHues) Add(m);
                if (k.glowHues != null) foreach (var m in k.glowHues) Add(m);
            }
            Add(shardMaterial);
            var vm = FindAnyObjectByType<ViewModel>();
            if (vm && vm.keepVariants != null) foreach (var m in vm.keepVariants) Add(m);
            if (vm) Add(vm.template);

            var rt = new RenderTexture(256, 256, 24);
            var cam = new GameObject("WarmUp").AddComponent<Camera>();
            cam.CopyFrom(view);
            cam.cullingMask = 1 << WarmLayer;
            cam.targetTexture = rt;
            cam.enabled = false;
            var tiles = new List<GameObject>();
            int n = Mathf.CeilToInt(Mathf.Sqrt(mats.Count)), i = 0;
            foreach (var m in mats)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Kill(q.GetComponent<Collider>());
                q.layer = WarmLayer;
                q.transform.SetParent(cam.transform, false);
                q.transform.localPosition = new Vector3((i % n - n * 0.5f) * 0.2f, (i / n - n * 0.5f) * 0.2f, 4f);
                q.transform.localScale = Vector3.one * 0.18f;
                q.GetComponent<MeshRenderer>().sharedMaterial = m;
                tiles.Add(q);
                i++;
            }
            cam.Render();
            foreach (var t in tiles) Kill(t);
            Kill(cam.gameObject);
            rt.Release();
            Kill(rt);
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
        // The course is one fixed map (the same layout every time, zone after zone), built in
        // full once, then only shown and hidden around you: nothing is built while you play, so
        // no frame ever has to. In the game it builds behind a progress bar a slice per frame;
        // tests (outside play mode) build it all at once.
        public const int CourseSeed = 777;
        public bool Ready { get; private set; }
        public float BuildProgress { get; private set; }
        int builtSeed = int.MinValue;
        Vector3 totalShift; // how far the world has been recentred since it was built
        Coroutine building;
        double buildWork; // seconds spent building (logged once built)
        int buildFrames;

        int SeedToUse => seed != 0 ? seed : CourseSeed;

        public void ResetCourse()
        {
            if (Ready && builtSeed == SeedToUse) { Restart(); return; }
            if (!Application.isPlaying) { BeginBuild(); while (!BuildSlice(float.MaxValue)) { } return; }
            if (building == null) building = StartCoroutine(BuildAll());
        }

        System.Collections.IEnumerator BuildAll()
        {
            BeginBuild();
            // (you are held still while it builds, so each frame can spend a good while on it)
            float started = Time.realtimeSinceStartup;
            while (!BuildSlice(0.1f)) yield return null;
            Debug.Log($"VoidFlow: course built, {FinalRamp} ramps, {buildWork:0.0}s of work over {buildFrames} frames, {Time.realtimeSinceStartup - started:0.0}s in all");
            building = null;
        }

        void BeginBuild()
        {
            Ready = false;
            BuildProgress = 0f;
            buildWork = 0;
            buildFrames = 0;
            builtSeed = SeedToUse;
            Rebuild(0, 0f, stream: false);
            totalShift = Vector3.zero;
        }

        // Builds ramps (each with its building, scenery and collision) until the time is up;
        // true once the whole course is built
        bool BuildSlice(float seconds)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            buildFrames++;
            while (nextIndex < FinalRamp)
            {
                Generate();
                while (pending.Count > 0) pending.Dequeue()();
                var seg = segments[^1];
                ClearBuildings(seg);
                // Only the last few stay active while building (later ramps check against them)
                if (segments.Count > 8 && segments[^9].root) segments[^9].root.SetActive(false);
                BuildProgress = nextIndex / (float)FinalRamp;
                if (clock.Elapsed.TotalSeconds > seconds) { buildWork += clock.Elapsed.TotalSeconds; return false; }
            }
            BuildHalls();
            SettleGround();
            TrimMeshes();
            buildWork += clock.Elapsed.TotalSeconds;
            Ready = true;
            BuildProgress = 1f;
            Restart();
            return true;
        }

        // Buildings give way to ramps (see RampClearance): the new ramp's building against its
        // own ramp and the few before, and those ramps' buildings against the new ramp
        public int ClearedFaces { get; private set; }
        public double ClearSeconds { get; private set; }
        readonly Dictionary<Segment, RampClearance.Ramps> rampTris = new();

        RampClearance.Ramps RampsOf(Segment s)
        {
            if (s?.root == null) return null;
            if (!rampTris.TryGetValue(s, out var r))
                rampTris[s] = r = RampClearance.Prepare(System.Array.FindAll(s.root.GetComponentsInChildren<MeshFilter>(true),
                    mf => mf.name.StartsWith("Surface") || mf.name.StartsWith("Trim") || mf.name == "Twin" || mf.name == "TwinTrim"));
            return r;
        }

        static List<MeshFilter> BuildingsOf(Segment s)
        {
            var list = new List<MeshFilter>();
            if (s?.root)
                foreach (var mf in s.root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name == "Architecture" && mf.transform.parent && mf.transform.parent.name != "HoleWall") list.Add(mf);
            return list;
        }

        void ClearBuildings(Segment seg)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            const int Back = 5; // long, deep flights can pass under buildings a few ramps back
            var near = new List<RampClearance.Ramps> { RampsOf(seg) };
            var older = new List<MeshFilter>();
            for (int k = 1; k <= Back; k++)
                if (Seg(seg.index - k) is Segment o) { near.Add(RampsOf(o)); older.AddRange(BuildingsOf(o)); }
            ClearedFaces += RampClearance.Cut(BuildingsOf(seg), near);
            // and any building further back that this ramp reaches (a loop or a long dive can come
            // round to one built many ramps before): a quick check of its bounds first
            var mine = RampsOf(seg);
            if (mine != null && mine.Count > 0)
                for (int k = Back + 1; k <= 30; k++)
                    if (Seg(seg.index - k) is Segment far && far.root)
                        foreach (var mf in BuildingsOf(far))
                            if (mf.GetComponent<Renderer>() is Renderer r && r.bounds.Intersects(mine.bounds)) older.Add(mf);
            ClearedFaces += RampClearance.Cut(older, new List<RampClearance.Ramps> { mine });
            // ramps further back are done with
            if (Seg(seg.index - Back - 1) is Segment gone) rampTris.Remove(gone);
            ClearSeconds += clock.Elapsed.TotalSeconds;
        }

        // ------------------------------------------------------------------ stage halls

        readonly List<(StageHalls.Volume v, GameObject go)> halls = new();
        Vector3 hallShift; // how far the world has moved since the halls were built

        void BuildHalls()
        {
            foreach (var (_, go) in halls) Kill(go);
            halls.Clear();
            hallShift = Vector3.zero;
            int stages = FinalRamp / rampsPerBiome;
            var plans = new StageHalls.Volume[stages];
            for (int st = 0; st < stages; st++)
            {
                var biome = Biome.All[BiomeOf(st * rampsPerBiome)];
                if (!Architecture.Continuous(biome.style)) continue;
                var pts = new List<Vector3>();
                foreach (var sg in segments)
                {
                    if (sg.index / rampsPerBiome != st || (st == 0 && sg.index < 2)) continue; // (the start terrace has its own sky)
                    pts.AddRange(sg.line);
                    pts.AddRange(sg.flight);
                }
                if (pts.Count > 0) plans[st] = StageHalls.Plan(st, pts, Mathf.Max(30f, Architecture.DepthFor(biome.style)), Architecture.OpenTop(biome.style), kits[BiomeOf(st * rampsPerBiome)]);
            }
            for (int st = 0; st < stages; st++)
            {
                if (plans[st] == null) continue;
                var near = new List<StageHalls.Volume>();
                for (int o = st - 2; o <= st + 2; o++)
                    if (o != st && o >= 0 && o < stages && plans[o] != null) near.Add(plans[o]);
                var meshes = new List<Mesh>();
                var go = StageHalls.Build(plans[st], near, transform, meshes, cube);
                // Wherever a ramp, a rider on it or a flight passes, the panels give way
                var cutters = new List<RampClearance.Ramps>();
                var tube = new List<Vector3>();
                foreach (var sg in segments)
                {
                    int sst = sg.index / rampsPerBiome;
                    if (sst < st - 1 || sst > st + 1) continue;
                    cutters.Add(RampsOf(sg));
                    tube.AddRange(StageHalls.FlightTube(sg.line, 16f));
                    tube.AddRange(StageHalls.FlightTube(sg.flight, 36f));
                }
                cutters.Add(RampClearance.FromTriangles(tube));
                ClearedFaces += RampClearance.Cut(new List<MeshFilter>(go.GetComponentsInChildren<MeshFilter>(true)), cutters);
                // Solid, like a map's walls: you can't fall through or fly out
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                halls.Add((plans[st], go));
            }
            rampTris.Clear();
            ShowHalls();
        }

        // The halls of the stage you're on and the ones either side (they share open sides)
        void ShowHalls()
        {
            int a = Mathf.Max(0, current - rampsBehind) / rampsPerBiome - 1, b = (current + 2) / rampsPerBiome + 1;
            foreach (var (v, go) in halls)
            {
                bool show = v.stage >= a && v.stage <= b;
                if (go && go.activeSelf != show) go.SetActive(show);
            }
        }

        // The floor of the hall you're over (the lowest where halls overlap), if any
        float? HallFloorAt(Vector3 p)
        {
            Vector3 q = p - hallShift;
            float? floor = null;
            foreach (var (v, go) in halls)
            {
                if (!go || !go.activeSelf) continue;
                var probe = new Vector3(q.x, (v.floor + v.ceiling) * 0.5f, q.z);
                if (v.Contains(probe, 0f)) floor = floor is float f ? Mathf.Min(f, v.floor) : v.floor;
            }
            return floor;
        }

        // Open zones' ground (snowfields, sunset plains, toy-town grass): once the whole course is
        // built, each stage's ground sheets are put at one height, below every bit of course
        // that can be on screen with them, so no sheet floats across the view or cuts through a ramp and they lie flat
        // as one. The trees, peaks and towers standing on a sheet move with it.
        void SettleGround()
        {
            const float Depth = 80f;
            for (int stage = 0; stage * rampsPerBiome < FinalRamp; stage++)
            {
                // Only ramps that can be on screen with this stage's ground count: its own, and a
                // few either side (the rest of the course is hidden while you're here)
                int first = stage * rampsPerBiome - rampsBehind - 2, last = (stage + 1) * rampsPerBiome + 12; // (long flights reach well into the next stage)
                var line = new List<Vector3>();
                foreach (var sg in segments)
                {
                    if (sg.index < first || sg.index > last) continue;
                    foreach (var p in sg.line) line.Add(transform.TransformPoint(p));
                    foreach (var p in sg.flight) line.Add(transform.TransformPoint(p));
                }
                var sheets = new List<(Segment seg, Transform ground, float y)>();
                Bounds area = default;
                foreach (var sg in segments)
                {
                    if (sg.index / rampsPerBiome != stage || !sg.root) continue;
                    var g = sg.root.transform.Find(Architecture.OpenGround);
                    var r = g ? g.GetComponentInChildren<Renderer>(true) : null;
                    if (!r) continue;
                    if (sheets.Count == 0) area = r.bounds; else area.Encapsulate(r.bounds);
                    sheets.Add((sg, g, r.bounds.center.y));
                }
                if (sheets.Count == 0) continue;
                // Below every point of the course over the stage's ground, and a little apart
                // from the stages either side, so two zones' grounds never sit exactly level
                float low = float.MaxValue;
                foreach (var p in line)
                    if (p.x >= area.min.x && p.x <= area.max.x && p.z >= area.min.z && p.z <= area.max.z) low = Mathf.Min(low, p.y);
                if (low == float.MaxValue) continue;
                float target = low - Depth - (stage % 3) * 1.5f;
                foreach (var (sg, g, y) in sheets)
                {
                    Vector3 delta = Vector3.up * (target - y);
                    if (Mathf.Abs(delta.y) < 0.01f) continue;
                    g.position += delta;
                    foreach (Transform piece in sg.root.transform)
                    {
                        if (piece.name != Architecture.ScenePiece) continue;
                        var pr = piece.GetComponentInChildren<Renderer>(true);
                        if (pr && pr.bounds.min.y < y + 25f) piece.position += delta; // (stands on the ground: a tree, a peak)
                    }
                }
                // What moved down with the ground gives way to any ramp it now runs into
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var near = new List<RampClearance.Ramps>();
                foreach (var sg in segments)
                    if (sg.index >= first && sg.index <= last + 24) near.Add(RampsOf(sg)); // (and ramps a while on, that can come round under it)
                var moved = new List<MeshFilter>();
                foreach (var (sg, _, _) in sheets) moved.AddRange(BuildingsOf(sg));
                ClearedFaces += RampClearance.Cut(moved, near);
                rampTris.Clear();
                ClearSeconds += clock.Elapsed.TotalSeconds;
            }
        }

        // In the game, once the whole course is built, each ramp's own meshes keep only their
        // copy on the graphics card. Shared meshes (the cube every piece is cut from, and the
        // like) and meshes with collision (physics needs them) are left alone.
        void TrimMeshes()
        {
            if (Application.isEditor) return;
            var uses = new Dictionary<Mesh, int>();
            var solid = new HashSet<Mesh>();
            foreach (var seg in segments)
            {
                if (!seg.root) continue;
                foreach (var c in seg.root.GetComponentsInChildren<MeshCollider>(true)) if (c.sharedMesh) solid.Add(c.sharedMesh);
                foreach (var mf in seg.root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh) uses[mf.sharedMesh] = uses.TryGetValue(mf.sharedMesh, out int n) ? n + 1 : 1;
            }
            foreach (var (mesh, n) in uses)
                if (n == 1 && !solid.Contains(mesh) && mesh.isReadable && mesh != cube)
                    mesh.UploadMeshData(true);
        }

        // Back to the start of the built course: the world moved back to where it was built,
        // collected shards and used rings back, the start terrace's sky
        void Restart()
        {
            ShiftWorld(-totalShift);
            if (startHall) startHall.gameObject.SetActive(true);
            current = 0;
            checkpoint = 0;
            Finished = false;
            if (padInUse != null && padInUse.pad) { SetPadSolid(padInUse, false); padInUse.pad.gameObject.SetActive(false); }
            padInUse = null;
            Progress = 0f;
            VoidShard.RestoreAll();
            foreach (var r in FindObjectsByType<SpeedRing>(FindObjectsInactive.Include)) r.usedAt = -99f;
            envFrom = envTo = Biome.Terrace;
            envBlend = 1f;
            inTerrace = true;
            ApplyEnvironment(envTo, envTo, 1f);
            Stream(all: false);
        }

        // Carries on from a checkpoint reached before (after a refresh, or a restart): the same
        // course, straight onto that checkpoint's platform
        public bool ResumeAt(int ramp, float progress, PlayerMovement p)
        {
            ramp = ramp / rampsPerBiome * rampsPerBiome; // (a save from mid-stage, before checkpoints moved to stage starts)
            if (!Ready || !IsCheckpoint(ramp) || Seg(ramp) == null) return false;
            Restart();
            current = ramp;
            checkpoint = ramp;
            inTerrace = false;
            if (startHall) startHall.gameObject.SetActive(false);
            if (!RespawnAtCheckpoint(p)) return false;
            envFrom = envTo = Biome.All[BiomeOf(current)];
            envBlend = 1f;
            ApplyEnvironment(envTo, envTo, 1f);
            BiomeEntered?.Invoke(envTo);
            return true;
        }

        public int LastCheckpoint => checkpoint;
        public Biome BiomeAt(int ramp) => Biome.All[BiomeOf(Mathf.Clamp(ramp, 0, FinalRamp - 1))];

        void Rebuild(int from, float progress, bool stream = true)
        {
            foreach (var seg in segments) DestroySegment(seg);
            segments.Clear();
            foreach (var (_, go) in halls) Kill(go);
            halls.Clear();
            pending.Clear();
            if (startHall)
            {
                startHall.position -= hallOffset;
                startHall.gameObject.SetActive(true);
            }
            hallOffset = Vector3.zero;

            rng = new System.Random(SeedToUse);
            nextIndex = from;
            current = from;
            checkpoint = 0;
            Finished = false;
            padInUse = null;
            last = null;
            lastFrame = null;
            afterLaunch = afterTwin = false;
            lastHoleWall = -99;
            nextProgress = progress;
            Progress = progress;
            stageThemes.Clear();
            stageTurn.Clear();
            RebuiltRamps = 0;
            ImpossibleRamps = 0;
            ScriptFixes.Clear();

            if (!cube)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube = temp.GetComponent<MeshFilter>().sharedMesh;
                Kill(temp);
            }

            // The start terrace has its own sky, over the first two ramps too; the first zone's
            // blends in as you reach its building
            envFrom = envTo = from < 2 ? Biome.Terrace : Biome.All[BiomeOf(from)];
            envBlend = 1f;
            inTerrace = from < 2;
            ApplyEnvironment(envTo, envTo, 1f);
            if (stream) Stream(all: true);
        }

        // Advances the course around the player. Called every frame; the surf bot calls it
        // directly when testing outside play mode.
        public void Step(float dt)
        {
            if (!player || !Ready) return; // (still building the course behind the progress bar)
            UpdateCurrent(player.Position);
            UpdatePad(dt);
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
            // The course is built in order from its first ramp, so a ramp's place is its number
            if (segments.Count > 0)
            {
                int k = index - segments[0].index;
                if (k >= 0 && k < segments.Count && segments[k].index == index) return segments[k];
            }
            foreach (var s in segments)
                if (s.index == index) return s;
            return null;
        }

        // Building work put off to later frames (see Generate), one piece a frame
        readonly Queue<Action> pending = new();

        void Stream(bool all)
        {
            if (all) { while (pending.Count > 0) pending.Dequeue()(); }
            // A frame does one piece of put-off building, or makes one new ramp; either way the
            // ramps behind are still tidied away below every frame
            bool busy = !all && pending.Count > 0;
            if (busy) pending.Dequeue()();
            while (!busy && nextIndex <= current + rampsAhead && nextIndex < FinalRamp)
            {
                Generate();
                if (!all) break; // at most one new ramp per frame, so there are no hitches
                // Building everything at once (a new course, carrying on from a checkpoint): each
                // ramp's building goes up before the next ramp, as the buildings chain together
                while (pending.Count > 0) pending.Dequeue()();
            }
            if (all) { while (pending.Count > 0) pending.Dequeue()(); }
            // (Ramps behind you are only hidden: the whole course stays built)
            // Only what's near is drawn: from the ramp behind to two ahead (built three ahead), and
            // from the start area just its own two white-and-gold ramps, not the course beyond
            foreach (var s in segments)
            {
                bool show = s.index >= current - rampsBehind && s.index <= current + (current < 1 ? 1 : 2);
                if (s.root && s.root.activeSelf != show) s.root.SetActive(show);
            }
            if (startHall) startHall.gameObject.SetActive(current < 2);
            ShowHalls();
        }

        // How hard a ramp is (0..1): its tier's range, climbing through the stage, plus a
        // little more every lap round the tiers
        float Difficulty(int ramp)
        {
            int stage = ramp / rampsPerBiome;
            // The finale climbs from hard to the hardest there is, zone by zone
            if (InLegend(stage) || Biome.All[BiomeOf(ramp)].script != null) return 1f;
            if (InFinale(stage))
                return Mathf.Lerp(0.8f, 1f, (ramp - Biome.FinaleFrom * rampsPerBiome) / (float)Mathf.Max(1, Biome.LegendFrom * rampsPerBiome - 1 - Biome.FinaleFrom * rampsPerBiome));
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
            // The first two ramps fly out under the start terrace's sky, and wear its white and gold
            if (i < 2 && kits.Length > Biome.All.Length) kit = kits[Biome.All.Length];
            // Spectrum zones run through their hues, one ramp at a time
            if (kit.rampHues != null && kit.rampHues.Length > 0)
                kit = kit.Hue(i % rampsPerBiome * kit.rampHues.Length / rampsPerBiome);
            float t = Difficulty(i);
            float flightSpeed = 0f;
            string move = "opening drop";
            bool enclose = false;
            float extraMargin = 0f;
            Func<float, float> flightArc = null;
            Vector3[] flightPoints = Array.Empty<Vector3>();
            float landingEnd = 0f;
            holePlan = null;
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
                // A real map's ramps are laid out by hand; the rest are picked by theme
                var def = biome.script != null ? biome.script[i % rampsPerBiome] : null;
                var m = def != null ? FromDef(def) : ChooseMove(i, t, theme, tier);
                move = m.name;
                // Every ramp is enclosed; twin and canyon ramps get a wider building so the second
                // ramp or the canyon wall is inside it too
                enclose = true;
                extraMargin = m.twin ? 14f : m.hole ? 20f : 0f;
                flightSpeed = FlightSpeed(last, courseStartY);
                var landing = new RampShapes.Landing
                {
                    speed = flightSpeed,
                    gap = Mathf.Lerp(30f, 50f, t) * Rand(0.9f, 1.1f) * (m.bigAir ? 1.85f : 1f),
                    length = Mathf.Lerp(70f, 45f, t) * (m.bigAir ? 1.3f : 1f),
                    clearStart = Mathf.Lerp(7f, 5f, t) * (m.bigAir ? 1.3f : 1f),
                    clearEnd = 0f,
                    shift = Mathf.Lerp(10f, 22f, t) * Rand(0.85f, 1.1f) * ShiftSign(i, theme, stage),
                };

                if (def != null)
                    landing = new RampShapes.Landing { speed = flightSpeed, gap = def.gap, length = def.land, clearStart = def.clear, clearEnd = 0f, shift = def.shift };
                // Off a launch ramp's kicker you fly high and far: the next ramp waits well down
                // the arc, past its peak
                else if (afterLaunch)
                {
                    landing.gap *= 2.3f;
                    landing.length *= 1.2f;
                    move += " (after launch)";
                }

                // Tiers: beginner hops are short onto long wide hills, intermediate gaps and
                // shifts are bigger, advanced landings are short and must be precise
                var (gapK, lengthK, shiftK, clearK) = tier switch
                {
                    Tier.Beginner => (0.85f, 1.35f, 0.8f, 1.1f),
                    Tier.Intermediate => (1.12f, 1f, 1.15f, 1f),
                    Tier.Advanced => (1.05f, 0.85f, 1f, 0.85f),
                    _ => (1f, 0.95f, 1.05f, 0.9f),
                };
                if (def == null)
                {
                    landing.gap *= gapK;
                    landing.length *= lengthK;
                    landing.shift *= shiftK;
                    landing.clearStart *= clearK;
                }

                // Legend zones: past anything before them, climbing to the very end: longer gaps
                // with bigger sideways shifts onto short, narrow landings that need the exact line
                // and speed (every flight is still proved possible below)
                if (InLegend(stage) && def == null)
                {
                    float h = Hardness(i);
                    landing.gap *= 1.15f + 0.3f * h;
                    landing.shift *= 1.25f + 0.45f * h;
                    landing.length *= 0.78f - 0.2f * h;
                    landing.clearStart *= 0.8f;
                    m.width *= 0.9f - 0.2f * h;
                }

                // Hard all the way, after the hardest maps (sinsane and the classics stitched into
                // it): longer gaps and bigger sideways shifts onto shorter, narrower landings with
                // less room to board, from the very first stages and climbing to the real maps
                if (def == null)
                {
                    float b = Brutal(i);
                    landing.gap *= 1.18f + 0.22f * b;
                    landing.shift *= 1.3f + 0.35f * b;
                    landing.length *= 0.8f - 0.12f * b;
                    landing.clearStart *= 0.8f;
                    m.width = Mathf.Max(m.width * (0.9f - 0.1f * b), 4.5f);
                }

                // Off a snipe you flick far out to the side
                if (def == null && last != null && segments.Count > 0 && segments[^1].move.StartsWith("snipe")) landing.shift *= 1.3f;

                // The first stages open gently, like a map's stage start: a shorter hop onto a
                // long, forgiving landing hill
                if (stageStart && def == null && stage <= 2)
                {
                    landing.gap *= 0.8f;
                    landing.length *= 1.4f;
                    landing.shift *= 0.7f;
                }

                // Prove the flight can be landed; if not, shorten the gap and shift and retry
                float slowest = SlowestLikelySpeed(flightSpeed);
                path = RampShapes.LandingRamp(last, landing, m.kind, m.width, m.shape, m.bend, m.bank > 0f ? m.bank : RampShapes.SlabBank);
                for (int attempt = 0; !FlightCheck.Possible(last, path, slowest, landing.length + 20f); attempt++)
                {
                    if (attempt == MaxRebuilds) { ImpossibleRamps++; Debug.LogWarning($"VoidFlow: ramp {i} ({m.name}, after {(segments.Count > 0 ? segments[^1].move : "-")}) could not be made landable"); break; }
                    RebuiltRamps++;
                    if (def != null && attempt == 0) ScriptFixes.Add($"ramp {i} ({biome.name}, {def.name}): gap {def.gap:0} shift {def.shift:0} too far");
                    landing.gap *= 0.85f;
                    landing.shift *= 0.8f;
                    path = RampShapes.LandingRamp(last, landing, m.kind, m.width, m.shape, m.bend, m.bank > 0f ? m.bank : RampShapes.SlabBank);
                }
                flightArc = RampShapes.Flight(last, flightSpeed).height;
                flightGap = landing.gap;
                holePlan = def != null ? (def.window ? PlanHoleWall(i, tier, m, biome, landing, flightSpeed, forced: true) : null)
                    : PlanHoleWall(i, tier, m, biome, landing, flightSpeed);
                {
                    var (_, arcY, launch) = RampShapes.Flight(last, flightSpeed);
                    Vector3 fwd = last.EndForward, side = new Vector3(fwd.z, 0f, -fwd.x);
                    const int samples = 24;
                    flightPoints = new Vector3[samples + 1];
                    for (int k = 0; k <= samples; k++)
                    {
                        float f = (float)k / samples, d = landing.gap * f;
                        Vector3 at = launch + fwd * d + side * (landing.shift * f * f);
                        at.y = arcY(d);
                        flightPoints[k] = at;
                    }
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
                landingEnd = landing.length;
                if (m.hole) holeWall = RampShapes.HoleWall(path, 3f, landing.length + 15f, 25f);
                // The twin stands behind the ramp you land on (seen from the flight), so it
                // never blocks the way in
                if (m.twin) twin = RampShapes.Offset(path, -path.side * (2f * path.width + 7f), 2f, landing.length + 10f, 55f);
                nextProgress += landing.gap;
            }
            last = path;
            brokenInto = biome.script != null && last != null ? biome.script[i % rampsPerBiome].pieces : BrokenPieces(i, move, twin != null);
            afterLaunch = move.StartsWith("launch");
            afterTwin = twin != null;

            var seg = new Segment
            {
                index = i,
                biome = biomeIndex,
                move = move,
                path = path,
                flight = flightPoints,
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

            Material surface = kit.ramp;
            if (brokenInto > 1 && i > 0)
            {
                // Broken into separate pieces with gaps between (ramp strafes): each one ridden
                // by itself
                const float Gap = 2.2f;
                for (int k = 0; k < brokenInto; k++)
                {
                    float d0 = path.Length * k / brokenInto + (k > 0 ? Gap * 0.5f : 0f);
                    float d1 = path.Length * (k + 1) / brokenInto - (k < brokenInto - 1 ? Gap * 0.5f : 0f);
                    var piece = RampShapes.Slice(path, d0, d1);
                    if (piece == null) continue;
                    // Each piece after the first starts a little low and blends up to the line, so
                    // across the gap you drop onto it instead of meeting its square end
                    if (k > 0)
                        for (int j = 0; j < piece.ridge.Count; j++)
                            piece.ridge[j] += Vector3.down * (0.5f * Mathf.Max(0f, 1f - piece.distance[j] / 8f));
                    AddPart(seg, $"Surface {k}", piece.ridge[0], RampShapes.BuildMesh(piece, $"Ramp {i}.{k}"), surface, solid: true);
                    AddPart(seg, $"Trim {k}", piece.ridge[0], RampShapes.TrimMesh(piece, $"Trim {i}.{k}"), kit.trim, solid: false);
                }
            }
            else
            {
                AddPart(seg, "Surface", path.ridge[0], RampShapes.BuildMesh(path, $"Ramp {i}"), surface, solid: true);
                AddPart(seg, "Trim", path.ridge[0], RampShapes.TrimMesh(path, $"Trim {i}"), kit.trim, solid: false);
            }
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
            // (Checkpoints and new stages have no gates to see: you just get the notice as you
            // reach the ramp)
            if (IsCheckpoint(i)) BuildPad(seg, path, landingEnd, kit);
            if (biome.script == null && twin == null) BuildGates(seg, path, i, kit);
            if (ringCenter is Vector3 ring)
            {
                AddPart(seg, "Ring", ring, RampShapes.RingMesh(ring, ringFacing, 6f, $"Ring {i}"), kit.glowAlt, solid: false);
                var boost = seg.root.transform.Find("Ring").gameObject.AddComponent<SpeedRing>();
                boost.facing = ringFacing;
                boost.radius = 6f;
            }
            if (i > 0) PlaceShards(seg, path, twin, TierOf(stage));

            // Outside scenery only for the open-air Sky Palace (its clouds); every other zone is
            // one enclosed interior
            // Decoration has its own random numbers (from the ramp's number), so changing how the
            // buildings look never reshuffles the course itself
            // The scenery and the building round the ramp come a frame or two later, each in a
            // frame of its own, so no single frame has to build a whole ramp and its world (that
            // made the game hitch in the browser whenever a new ramp appeared ahead)
            var decor = new System.Random(unchecked(i * 7919 + 13));
            bool encloseIt = enclose && i > 1;
            var plan = holePlan; // this ramp's hole wall (the field moves on with the next ramp)
            if (!Architecture.Continuous(biome.style))
                pending.Enqueue(() => { if (seg.root) Scenery.Line(path, biome, kit, seg.root.transform, decor, cube); });
            pending.Enqueue(() =>
            {
                if (!seg.root) return;
                var next = holePlan;
                holePlan = plan;
                BuildArchitecture(seg, path, biome, kit, i, encloseIt, stageStart, flightArc, flightGap, extraMargin, decor);
                holePlan = next;
                ClearScenery(seg);
                ClearHoleWalls(seg);
                Physics.SyncTransforms();
            });
            segments.Add(seg);
            if (i == FinalRamp - 1) BuildFinish(seg, kit);
            Physics.SyncTransforms();
        }

        // A wall across a flight with a hole to jump through: where it stands and how big
        struct HolePlan { public Vector3 center, facing; public float hw, hh; }
        HolePlan? holePlan;
        int lastHoleWall = -99;
        const int HoleWallSpacing = 5; // at least this many ramps between hole walls

        // Now and then (never for beginners, never on big air or right after a launch kicker or
        // a twin, where you could take off from either ramp; only indoors) the flight into this ramp goes through a hole in a wall. The wall stands
        // early in the flight, where fast and slow riders are still close together, and the hole
        // is sized to take both, with room to spare.
        // `forced`: a real map's window or doorway, wherever its layout has one
        HolePlan? PlanHoleWall(int i, Tier tier, Move m, Biome biome, RampShapes.Landing landing, float flightSpeed, bool forced = false)
        {
            if (!Architecture.Continuous(biome.style) || afterTwin) return null;
            if (!forced)
            {
                if (i < 6 || m.bigAir || afterLaunch) return null;
                bool legend = InLegend(i / rampsPerBiome);
                if (i - lastHoleWall < (legend ? 2 : 3) || landing.gap < 36f) return null;
                if ((unchecked((uint)i * 2654435761u) >> 8) % 100 >= (legend ? 80 : 60)) return null; // its own dice: the course itself isn't reshuffled
            }
            float d = Mathf.Clamp(landing.gap * 0.3f, 18f, 32f), f = d / landing.gap;
            var (_, design, launch) = RampShapes.Flight(last, flightSpeed);
            var slow = RampShapes.Flight(last, SlowestLikelySpeed(flightSpeed)).height;
            var fast = RampShapes.Flight(last, Mathf.Max(flightSpeed * 1.8f, 3600f * PlayerMovement.SourceUnit)).height;
            float lo = Mathf.Min(slow(d), Mathf.Min(fast(d), design(d))), hi = Mathf.Max(slow(d), Mathf.Max(fast(d), design(d)));
            if (hi - lo > (forced ? 26f : 16f)) return null; // too spread out to make a fair hole
            Vector3 fwd = last.EndForward, side = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 center = launch + fwd * d + side * (landing.shift * f * f);
            center.y = (lo + hi) * 0.5f + 1f; // the body rides above the arc of the feet
            lastHoleWall = i;
            return new HolePlan
            {
                center = center,
                facing = (fwd + side * (2f * landing.shift * f / landing.gap)).normalized,
                hw = forced ? 12f : Mathf.Lerp(14f, 11f, Brutal(i)), // smaller windows further in (wide enough for every line off the ramp)
                hh = Mathf.Clamp((hi - lo) * 0.5f + (forced ? 7f : 8f), forced ? 9f : 10f, forced ? 21f : 18f),
            };
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        // A new ramp takes out any open-zone scenery (from the ramps before it) standing in its
        // riding line or its flight in
        void ClearScenery(Segment seg)
        {
            var pts = new List<Vector3>();
            foreach (var p in seg.line) pts.Add(transform.TransformPoint(p));
            foreach (var p in seg.flight) pts.Add(transform.TransformPoint(p));
            foreach (var other in segments)
            {
                if (!other.root) continue;
                foreach (Transform piece in other.root.transform)
                {
                    if (piece.name != Architecture.ScenePiece) continue;
                    Bounds? box = null;
                    foreach (var r in piece.GetComponentsInChildren<Renderer>())
                    {
                        if (box is Bounds bb) { bb.Encapsulate(r.bounds); box = bb; }
                        else box = r.bounds;
                    }
                    if (box is not Bounds area) continue;
                    area.Expand(32f); // (a ramp's face reaches well out from its riding line)
                    foreach (var p in pts)
                        if (area.Contains(p)) { Kill(piece.gameObject); break; }
                }
            }
        }

        readonly List<Vector3> airShards = new();
        bool afterLaunch; // the ramp just built ends in a launch kicker
        bool afterTwin;   // ...has a twin beside it (you might launch from either)

        // The last cross-section of the previous ramp's building, if it was a continuous one:
        // the next building starts by joining onto it across the flight
        Architecture.Frame? lastFrame;

        void BuildArchitecture(Segment seg, RampShapes.RampPath path, Biome biome, BiomeKit kit, int i, bool enclose, bool stageStart,
            Func<float, float> flightArc, float flightGap, float extraMargin, System.Random decor)
        {
            var style = biome.style;
            if (!enclose) { lastFrame = null; return; }
            float step = Architecture.StepFor(style);
            if (!Architecture.Continuous(style))
            {
                // Open zones: a building over the middle of the ramp only
                float from = Mathf.Min(path.Length * 0.1f, 30f), to = Mathf.Min(path.Length * 0.88f, path.Length - 25f);
                if (to - from >= 30f)
                {
                    // What the scenery must keep clear of: this ramp, the flight into it, the ramp before
                    Architecture.KeepOut.Clear();
                    for (int k = 0; k < seg.line.Length; k += 3) Architecture.KeepOut.Add(seg.line[k]);
                    Architecture.KeepOut.AddRange(seg.flight);
                    if (Seg(i - 1) is Segment before)
                        for (int k = 0; k < before.line.Length; k += 3) Architecture.KeepOut.Add(before.line[k]);
                    Architecture.Build(Architecture.RampFrames(path, from, to, step, 24f, extraMargin), biome, kit, seg.root.transform, seg.meshes, decor, cube);
                    Architecture.KeepOut.Clear();
                }
                lastFrame = null;
                return;
            }
            // Enclosed zones: the stage's hall (StageHalls) goes up once the whole course is
            // built. A window is a great wall standing across the flight with the hole in it,
            // far too wide and tall to fly round
            if (holePlan is HolePlan plan)
            {
                Vector3 across = new Vector3(plan.facing.z, 0f, -plan.facing.x).normalized;
                var fr = new Architecture.Frame
                {
                    p = plan.center, f = plan.facing, right = across,
                    A = plan.center - across * 60f, B = plan.center + across * 60f,
                    level = plan.center.y, bottom = plan.center.y - 45f, top = plan.center.y + 30f,
                };
                var wall = Architecture.HoleWall(fr, plan.center, plan.facing, plan.hw, plan.hh, kit, seg.root.transform, seg.meshes, cube);
                // it gives way wherever a ramp (this one or the two before) runs through it, so you
                // never meet it while riding, only in the air
                var lines = new List<Vector3>();
                for (int k = 0; k <= 2; k++)
                    if ((k == 0 ? seg : Seg(seg.index - k)) is Segment sg)
                        lines.AddRange(StageHalls.FlightTube(System.Array.ConvertAll(sg.line, q => transform.TransformPoint(q)), 14f));
                RampClearance.Cut(new List<MeshFilter>(wall.GetComponentsInChildren<MeshFilter>(true)), new List<RampClearance.Ramps> { RampClearance.FromTriangles(lines) });
                foreach (var mc in wall.GetComponentsInChildren<MeshCollider>(true)) { mc.sharedMesh = null; mc.sharedMesh = mc.GetComponent<MeshFilter>().sharedMesh; }
                if (FlightClears(seg, wall)) seg.move += " + hole in the wall";
                else Kill(wall);
            }
            lastFrame = null;
            return;
#pragma warning disable CS0162
            var frames = Architecture.RampFrames(path, i == 0 ? 45f : 0f, path.Length, step, Architecture.DepthFor(style), extraMargin);
            if (frames.Count == 0) { lastFrame = null; return; }
            int doorway = stageStart || lastFrame == null ? 0 : -1; // a doorway wherever a building begins
            if (lastFrame is Architecture.Frame prev && flightArc != null)
            {
                var join = Architecture.FlightFrames(prev, frames[0], flightArc, flightGap, step);
                frames.InsertRange(0, join);
                if (holePlan is HolePlan hp)
                {
                    // The wall fits the cross-section of the building nearest to it
                    var near = join[0];
                    foreach (var jf in join)
                        if (Flat(jf.p - hp.center).sqrMagnitude < Flat(near.p - hp.center).sqrMagnitude) near = jf;
                    var wall = Architecture.HoleWall(near, hp.center, hp.facing, hp.hw, hp.hh, kit, seg.root.transform, seg.meshes, cube);
                    // Never a dead end: a player-sized sweep along the jump must get through the
                    // hole, or the wall comes down (a clear flight beats a covered hole)
                    if (FlightClears(seg, wall)) seg.move += " + hole in the wall";
                    else Kill(wall);
                }
                if (stageStart) doorway = Mathf.Min(1, frames.Count - 1);
            }
            Architecture.Build(frames, biome, kit, seg.root.transform, seg.meshes, decor, cube, doorway);
            lastFrame = frames[^1];
#pragma warning restore CS0162
        }

        // A ramp that curls back (a loop, a spiral) can pass through an earlier ramp's hole wall,
        // which spans its whole building; any such wall in this ramp's way comes down
        void ClearHoleWalls(Segment seg)
        {
            foreach (var other in segments)
            {
                if (other == seg || !other.root) continue;
                foreach (Transform child in other.root.transform)
                    if (child.name == "HoleWall" && (!Sweeps(seg.line, child.gameObject) || !Sweeps(seg.flight, child.gameObject)))
                        Kill(child.gameObject);
            }
        }

        // A new hole wall must clear the jump through it and every ramp and flight around it (a
        // ramp before it may curl back through it)
        bool FlightClears(Segment seg, GameObject wall)
        {
            if (!Sweeps(seg.flight, wall) || !Sweeps(seg.line, wall)) return false;
            foreach (var other in segments)
                if (other != seg && other.root && (!Sweeps(other.line, wall) || !Sweeps(other.flight, wall))) return false;
            return true;
        }

        // Does a player-sized sweep along these course-local points get past this object?
        bool Sweeps(Vector3[] points, GameObject wall)
        {
            Physics.SyncTransforms();
            const float body = 0.9f; // a player's half width, and a little room
            for (int k = 0; k + 1 < points.Length; k++)
            {
                Vector3 a = transform.TransformPoint(points[k]) + Vector3.up, b = transform.TransformPoint(points[k + 1]) + Vector3.up;
                Vector3 d = b - a;
                if (d.sqrMagnitude < 1e-4f) continue;
                foreach (var hit in Physics.SphereCastAll(a, body, d.normalized, d.magnitude))
                    if (hit.collider && hit.collider.transform.IsChildOf(wall.transform)) return false;
            }
            return true;
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
        void AddPart(Segment seg, string name, Vector3 position, Mesh mesh, Material mat, bool solid)
        {
            seg.meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(seg.root.transform, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            // Its collision comes a frame later, in a frame of its own (cooking it is costly),
            // long before anyone reaches a ramp built three ahead
            if (solid) pending.Enqueue(() => { if (go) { go.AddComponent<MeshCollider>().sharedMesh = mesh; Physics.SyncTransforms(); } });
            else
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        int brokenInto; // the ramp being built is in this many pieces (0 = whole)

        // How far along the generated course a ramp is, 0 at the start to 1 where the real
        // maps begin (how hard the hard-everywhere changes bite)
        float Brutal(int ramp) => Mathf.Clamp01(ramp / (float)Mathf.Max(1, Biome.MapsFrom * rampsPerBiome));

        // Each ramp's own dice (the rest of the course isn't reshuffled by them)
        static float Dice(int ramp, uint salt) => (unchecked(((uint)ramp + salt * 7919u) * 2654435761u) >> 8) % 1000 / 1000f;

        // Generated ramps break into pieces more and more often (ramp strafes: ride each piece
        // and drop onto the next across the gap), never the opening ramps, a stage's first
        // ramp (its checkpoint), a launch or a twin
        int BrokenPieces(int i, string move, bool twin)
        {
            if (i < 4 || i % rampsPerBiome == 0 || twin || move.StartsWith("launch") || move.Contains("big air")) return 0;
            float b = Brutal(i);
            if (Dice(i, 3) >= 0.15f + 0.35f * b) return 0;
            return 2 + (int)(Dice(i, 4) * (b < 0.4f ? 2f : 3f)); // 2-3 pieces, up to 4 later
        }

        // Gates on the face you ride (after the obstacle runs in the hardest maps): pairs of
        // solid blocks standing on the ramp above and below the line, leaving a narrow lane to
        // thread. Ride high or low and you smack into one. They start well after the landing
        // and stop before the launch swing, so you board and launch as ever.
        void BuildGates(Segment seg, RampShapes.RampPath path, int i, BiomeKit kit)
        {
            if (i < 3 || i % rampsPerBiome == 0) return;
            float b = Brutal(i);
            if (Dice(i, 9) >= 0.25f + 0.45f * b) return;
            float from = 40f, to = path.Length - RampShapes.SwingLength - 15f;
            if (to - from < 20f) return;
            float spacing = Mathf.Lerp(45f, 28f, b), lane = Mathf.Lerp(7f, 4.5f, b); // half the lane, across the face
            const float Thick = 1.6f, Tall = 3.2f;
            var gates = new GameObject("Gates").transform;
            gates.SetParent(seg.root.transform, false);
            int made = 0;
            for (float d = from + spacing * 0.5f * Dice(i, 10); d <= to; d += spacing)
            {
                int k = Mathf.Clamp(Mathf.RoundToInt(d / path.Length * (path.ridge.Count - 1)), 0, path.ridge.Count - 2);
                // only where the ramp runs fairly straight: in a hard turn you ride higher or lower
                // on the face to hold it, and a lane round the middle line would be a wall
                int k0 = Mathf.Max(0, k - 8), k1 = Mathf.Min(path.ridge.Count - 1, k + 8);
                if (Vector3.Angle(path.forward[k0], path.forward[k1]) > 5f) continue;
                Vector3 along = (path.ridge[k + 1] - path.ridge[k]).normalized;
                Vector3 top = path.FacePoint(k, 0f), bottom = path.FacePoint(k, 1f);
                Vector3 across = (bottom - top).normalized;
                float faceLen = Vector3.Distance(top, bottom);
                Vector3 normal = Vector3.Cross(along, across).normalized;
                if (Vector3.Dot(normal, Vector3.up + path.right[k] * path.side) < 0f) normal = -normal;
                float line = RampShapes.RideFraction, laneFrac = lane / Mathf.Max(faceLen, 1f);
                foreach (var (f0, f1) in new[] { (0.03f, line - laneFrac), (line + laneFrac, 0.97f) })
                {
                    if ((f1 - f0) * faceLen < 1.5f) continue;
                    Vector3 a = path.FacePoint(k, f0), c = path.FacePoint(k, f1);
                    Vector3 center = (a + c) * 0.5f + normal * (Tall * 0.5f);
                    var block = new GameObject("Gate");
                    block.transform.SetParent(gates, false);
                    block.transform.SetLocalPositionAndRotation(center, Quaternion.LookRotation(along, normal));
                    block.transform.localScale = new Vector3(Vector3.Distance(a, c), Tall, Thick);
                    block.AddComponent<MeshFilter>().sharedMesh = cube;
                    block.AddComponent<MeshRenderer>().sharedMaterial = kit.slab;
                    block.AddComponent<BoxCollider>();
                    // a glowing cap, so you read the lane from the landing
                    var cap = new GameObject("GateGlow");
                    cap.transform.SetParent(block.transform, false);
                    cap.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    cap.transform.localScale = new Vector3(1.02f, 0.06f, 1.1f);
                    cap.AddComponent<MeshFilter>().sharedMesh = cube;
                    var cr = cap.AddComponent<MeshRenderer>();
                    cr.sharedMaterial = kit.glow;
                    cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    made++;
                }
            }
            if (made == 0) Kill(gates.gameObject);
            else seg.move += $" + gates ({made} blocks)";
        }
        public readonly List<string> ScriptFixes = new(); // hand-made ramps the flight check had to bring closer

        static Move FromDef(RampDef d) => new()
        {
            name = d.name, kind = d.kind, width = d.width, shape = d.shape, bend = d.bend,
            bigAir = d.bigAir, twin = d.twin, bank = d.bank,
        };

        struct Move
        {
            public string name;
            public RampShapes.Kind kind;
            public float width;
            public (float, float)[] shape, bend;
            public bool hole, bigAir, twin;
            public float bank; // slabs (0 = the usual)
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
                "CURVED ROOM" => new List<(string, float)> { ("loop", 4f), ("sweep", 3f), ("spiral", 1f), ("corner", 1.5f) },
                "SPIRAL" => new List<(string, float)> { ("spiral", 5f), ("corner", 1f) },
                "ROLLERCOASTER" => new List<(string, float)> { ("wave", 4f), ("plunge", 2f), ("climb", 2f), ("launch", 1.5f), ("loop", 1f), ("sweep", 1.5f) },
                "CANYON" => new List<(string, float)> { ("hole", 4f), ("corner", 2f), ("sweep", 1f) },
                "TWIN PEAKS" => new List<(string, float)> { ("twin", 5f), ("blade", 1f) },
                "BIG AIR" => new List<(string, float)> { ("launch", 4f), ("blade", 2f), ("plunge", 2f), ("climb", 2f), ("sweep", 1f) },
                _ => new List<(string, float)> { ("blade", 3f), ("sweep", 3f), ("loop", 2f), ("corner", 1.5f), ("launch", 1.5f), ("plunge", 1f), ("climb", 1f) },
            };
            // Shapes from the hard maps, everywhere once the course gets going: snipes flicked
            // off sideways, drops, stepped dives, kickers in the middle of a ramp, quick zigzags
            if (i >= 8)
                options.AddRange(new[] { ("snipe", 1.6f), ("drop", 1.1f), ("steps", 1.3f), ("kicker", 1.1f), ("zigzag", 1f) });
            // Advanced adds speed sections (plunges); technical throws every move it has in
            if (tier == Tier.Advanced) options.Add(("plunge", 3f));
            if (tier == Tier.Technical)
                options.AddRange(new[] { ("twin", 3f), ("sweep", 2f), ("loop", 2f), ("launch", 1.5f), ("plunge", 1.5f), ("wave", 1f), ("corner", 1f), ("climb", 1f), ("winding", 1f), ("hole", 1f) });
            float total = 0f;
            foreach (var o in options) total += o.Item2;
            float pick = Rand(0f, total);
            string name = options[^1].Item1;
            foreach (var o in options)
            {
                if (pick < o.Item2) { name = o.Item1; break; }
                pick -= o.Item2;
            }

            bool calm = name is "hole" or "spiral" or "winding" or "sweep" or "loop" or "snipe"; // (a snipe's flick is its own long flight)
            double airChance = theme == "BIG AIR" ? 0.8 : tier switch { Tier.Beginner => 0.2, Tier.Advanced or Tier.Technical => 0.5, _ => 0.35 };
            var m = new Move { name = name, bigAir = name == "launch" || (!calm && rng.NextDouble() < airChance) };
            switch (name)
            {
                case "launch": // a dip for speed, then a steep kicker that throws you high into big air
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(11f, 7f, t);
                    m.shape = new[] { (50f, -0.18f), (150f, -0.18f), (205f, 0f), (235f, 0.32f), (250f, 0.32f) };
                    break;
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
                    float degrees = Mathf.Lerp(55f, 95f, t) * Rand(0.85f, 1f);
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
                    // Beginners only get curves into the face (the easy way), and gentler ones
                    bool intoFace = rng.NextDouble() < 0.7;
                    float way = theme == "CURVED ROOM" || tier == Tier.Beginner || intoFace ? 1f : -1f;
                    float degrees = Mathf.Lerp(45f, 100f, t) * Rand(0.8f, 1f) * way * (tier == Tier.Beginner ? 0.8f : 1f);
                    m.bend = new[] { (20f, 0f), (length - 30f, degrees), (length, degrees) };
                    break;
                }
                case "loop": // a long prism curling right round (200-280 degrees) at a steady radius as it drops
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(11f, 8f, t);
                    float degrees = Rand(200f, 280f), radius = Rand(165f, 195f);
                    float length = degrees * Mathf.Deg2Rad * radius + 50f;
                    m.shape = new[] { (40f, -0.13f), (length - 30f, -0.13f), (length, 0.07f) };
                    m.bend = new[] { (20f, 0f), (length - 30f, degrees), (length, degrees) };
                    break;
                }
                case "twin": // a blade with an identical one alongside
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(8f, 6f, t);
                    m.shape = blade;
                    m.twin = true;
                    break;
                case "snipe": // short and sharp: hit it fast and flick off it, far to the side
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(9f, 6f, t);
                    float length = Rand(130f, 170f);
                    m.shape = new[] { (length * 0.5f, -0.1f), (length, 0.09f) };
                    break;
                }
                case "drop": // a steep slide that throws you down into a long fall
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(10f, 7f, t);
                    float length = Rand(100f, 130f);
                    m.shape = new[] { (15f, -0.95f), (length - 15f, -0.95f), (length, -0.3f) };
                    break;
                }
                case "steps": // a dive in steps: steep, level, steep, level, then launch
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(11f, 8f, t);
                    m.shape = new[] { (35f, -0.5f), (70f, -0.5f), (100f, -0.04f), (135f, -0.04f), (165f, -0.5f), (200f, -0.5f), (235f, -0.02f), (270f, 0.08f) };
                    break;
                }
                case "kicker": // a bump halfway that pops you up off the face: hold the ramp through it
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(10f, 7f, t);
                    m.shape = new[] { (40f, -0.2f), (100f, -0.2f), (125f, 0.12f), (140f, 0.12f), (165f, -0.25f), (220f, -0.25f), (250f, 0f), (275f, 0.08f) };
                    break;
                }
                case "zigzag": // left, right, left: a quick S and back, long enough to hold at speed
                {
                    m.kind = RampShapes.Kind.Prism;
                    m.width = Mathf.Lerp(10f, 7f, t);
                    float a = Mathf.Lerp(16f, 26f, t) * Rand(0.85f, 1f);
                    m.shape = new[] { (40f, -0.12f), (360f, -0.12f), (390f, 0.07f) };
                    m.bend = new[] { (20f, 0f), (110f, a), (210f, -a * 0.7f), (310f, a * 0.5f), (390f, a * 0.5f) };
                    break;
                }
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
            // ...and no two alike: each ramp a little wider or thinner, longer or shorter
            m.width *= Rand(0.8f, 1.25f);
            if (m.kind == RampShapes.Kind.Prism && name is not ("loop" or "winding" or "zigzag" or "sweep"))
            {
                float stretch = Rand(0.85f, 1.3f);
                m.shape = System.Array.ConvertAll(m.shape, k => (k.Item1 * stretch, k.Item2));
                if (m.bend != null) m.bend = System.Array.ConvertAll(m.bend, k => (k.Item1 * stretch, k.Item2));
            }
            return m;
        }

        public string CurrentMove => Seg(current)?.move ?? "";
        // (for the test bot: banked slabs, spins among them, are ridden with feathered strafes)
        public bool CurrentIsSlab => Seg(current)?.path.kind == RampShapes.Kind.Slab;
        // (for the test bot: how far above (+) or below (-) the riding line you are, right here)
        public float HeightOverLine(Vector3 p)
        {
            Segment seg = Seg(current);
            if (seg == null) return 0f;
            DistanceToLine(seg, p, out int nearest);
            return p.y - seg.line[nearest].y;
        }

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
                if (inTerrace && current >= 2) // the first two ramps fly under the terrace sky
                {
                    inTerrace = false;
                    envFrom = Biome.Terrace;
                    envTo = Biome.All[next.biome];
                    envBlend = 0f;
                }
                else if (next.biome != here.biome)
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
                float off = DistanceToLine(seg, p, out int nearest);
                Progress = seg.startProgress + seg.path.distance[nearest];
                // A checkpoint counts once you're actually at its ramp: within reach of its riding
                // line (there's no gate to hit or miss), and not flying in noclip
                bool reached = off < CheckpointReach && !player.Flying;
                if (IsCheckpoint(current) && current > checkpoint && reached)
                {
                    checkpoint = current;
                    CheckpointReached?.Invoke();
                }
                // Off the end of the very last ramp: the course is done
                if (!Finished && current == FinalRamp - 1 && nearest >= seg.line.Length - 3 && reached)
                {
                    Finished = true;
                    CourseFinished?.Invoke();
                }
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
            if (Finished) return p.y < FinishSpawn.y - 60f;
            Segment here = Seg(current), next = Seg(current + 1);
            if (here == null) return false;
            float floor = here.lowestY;
            if (next != null) floor = Mathf.Min(floor, next.lowestY);
            // down on a hall's floor counts as falling, as in a map
            if (HallFloorAt(p) is float hall && p.y < hall + 2f) return true;
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

        // The drop-in platform over a checkpoint ramp: it floats 6m over the riding line just
        // past the landing hill, where flights have already come down and riders are sliding
        // along the surface under it. It only appears (and is only solid) while someone
        // respawns on it, so it is never in the way of a run; step off any edge and you are
        // sent down the ramp at the speed it was built for.
        const float PadHeight = 6f, PadWidth = 7f, PadLength = 9f;


        void BuildPad(Segment seg, RampShapes.RampPath path, float landingEnd, BiomeKit kit)
        {
            float want = Mathf.Min(landingEnd + 15f, path.Length * 0.4f);
            int k = 0;
            while (k < path.distance.Count - 1 && path.distance[k] < want) k++;
            Vector3 fwd = path.forward[k];
            var pad = new GameObject("CheckpointPad").transform;
            pad.SetParent(seg.root.transform, false);
            pad.SetLocalPositionAndRotation(seg.line[k] + Vector3.up * PadHeight, Quaternion.LookRotation(fwd, Vector3.up));
            void Block(string name, Vector3 at, Vector3 size, Quaternion turn, Material mat, bool solid = false)
            {
                var go = new GameObject(name);
                go.transform.SetParent(pad, false);
                go.transform.SetLocalPositionAndRotation(at, turn);
                go.transform.localScale = size;
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (solid) go.AddComponent<BoxCollider>().enabled = false;
            }
            Block("Deck", new Vector3(0f, -0.4f, 0f), new Vector3(PadWidth, 0.8f, PadLength), Quaternion.identity, kit.slab, solid: true);
            // A glowing rim, and chevrons pointing down the ramp
            foreach (float x in new[] { -1f, 1f })
                Block("Rim", new Vector3(x * PadWidth * 0.5f, 0.02f, 0f), new Vector3(0.25f, 0.1f, PadLength), Quaternion.identity, kit.glow);
            foreach (float z in new[] { -1f, 1f })
                Block("Rim", new Vector3(0f, 0.02f, z * PadLength * 0.5f), new Vector3(PadWidth, 0.1f, 0.25f), Quaternion.identity, kit.glow);
            for (int c = 0; c < 2; c++)
                foreach (float x in new[] { -1f, 1f })
                    Block("Chevron", new Vector3(x * 0.55f, 0.03f, 0.8f + c * 1.8f), new Vector3(0.25f, 0.06f, 1.6f), Quaternion.Euler(0f, -x * 45f, 0f), kit.glowAlt);
            Block("Glow", new Vector3(0f, -0.85f, 0f), new Vector3(PadWidth - 1f, 0.1f, PadLength - 1f), Quaternion.identity, kit.glow);
            seg.pad = pad;
            pad.gameObject.SetActive(false); // only there while someone respawns on it
        }

        // The finish, past the end of the final ramp: a huge plaza wide and long enough to catch
        // any launch off it, lined in the zone's glow, with a gate of light at its far end
        void BuildFinish(Segment seg, BiomeKit kit)
        {
            Vector3 end = seg.line[^1];
            Vector3 dir = seg.line[^1] - seg.line[Mathf.Max(0, seg.line.Length - 6)];
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            const float Length = 520f, Width = 320f;
            float top = seg.lowestY - 12f;
            finish = new GameObject("Finish").transform;
            finish.SetParent(seg.root.transform, false);
            finish.SetLocalPositionAndRotation(new Vector3(end.x, top, end.z) + dir * (Length * 0.5f + 10f), Quaternion.LookRotation(dir, Vector3.up));
            void Block(Vector3 at, Vector3 size, Material mat, bool solid)
            {
                var go = new GameObject(solid ? "FinishDeck" : "FinishGlow");
                go.transform.SetParent(finish, false);
                go.transform.localPosition = at;
                go.transform.localScale = size;
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                if (solid) go.AddComponent<BoxCollider>();
            }
            Block(new Vector3(0f, -2f, 0f), new Vector3(Width, 4f, Length), kit.slab, true);
            foreach (float x in new[] { -1f, 1f })
                Block(new Vector3(x * Width * 0.5f, 0.1f, 0f), new Vector3(1.5f, 0.4f, Length), kit.glow, false);
            for (int k = 0; k < 12; k++)
                Block(new Vector3(0f, 0.05f, -Length * 0.5f + 30f + k * 40f), new Vector3(Width * 0.8f, 0.2f, 1.2f), kit.glowAlt, false);
            // The gate of light at the far end
            float gz = Length * 0.5f - 20f;
            foreach (float x in new[] { -1f, 1f })
                Block(new Vector3(x * 60f, 40f, gz), new Vector3(6f, 80f, 6f), kit.glow, false);
            Block(new Vector3(0f, 82f, gz), new Vector3(126f, 6f, 6f), kit.glow, false);
        }

        Segment padInUse;
        bool padLeft;
        float padTimer;

        // While someone's on a checkpoint platform: once they leave it, send them down the ramp,
        // and a moment later make the platform ghostly again
        void UpdatePad(float dt)
        {
            if (padInUse == null || !padInUse.pad) { padInUse = null; return; }
            var pad = padInUse.pad;
            Vector3 local = pad.InverseTransformPoint(player.Position);
            bool on = Mathf.Abs(local.x) < PadWidth * 0.5f + 0.6f && Mathf.Abs(local.z) < PadLength * 0.5f + 0.6f && local.y > -1.5f;
            if (!padLeft && !on)
            {
                padLeft = true;
                Vector3 v = player.Velocity;
                float speed = Mathf.Max(padInUse.flightSpeed * 0.9f, new Vector2(v.x, v.z).magnitude);
                Vector3 h = pad.forward * speed;
                player.SetVelocity(new Vector3(h.x, Mathf.Min(v.y, 0f), h.z));
            }
            if (padLeft && (padTimer += dt) > 1.2f)
            {
                SetPadSolid(padInUse, false);
                padInUse.pad.gameObject.SetActive(false);
                padInUse = null;
            }
        }

        static void SetPadSolid(Segment seg, bool solid)
        {
            if (!seg.pad) return;
            foreach (var c in seg.pad.GetComponentsInChildren<Collider>(true)) c.enabled = solid;
        }

        // Back to the last checkpoint after a fall: the ramps since it come back, and you're
        // put on its drop-in platform to go again when you're ready. False if you haven't
        // reached one yet.
        public bool RespawnAtCheckpoint(PlayerMovement p)
        {
            if (checkpoint <= 0 || Seg(checkpoint) == null) return false;
            int from = current;
            current = checkpoint;
            foreach (var s in segments)
                if (s.root) s.root.SetActive(s.index >= current - rampsBehind && s.index <= current + 2);
            ShowHalls();
            if (Seg(from) is Segment was && was.biome != Seg(current).biome)
            {
                envFrom = envTo = Biome.All[Seg(current).biome];
                envBlend = 1f;
                ApplyEnvironment(envTo, envTo, 1f);
            }
            var seg = Seg(current);
            if (padInUse != null && padInUse.pad) { SetPadSolid(padInUse, false); padInUse.pad.gameObject.SetActive(false); }
            if (seg.pad)
            {
                seg.pad.gameObject.SetActive(true);
                SetPadSolid(seg, true);
                padInUse = seg;
                padLeft = false;
                padTimer = 0f;
                Physics.SyncTransforms();
                p.Teleport(seg.pad.position + Vector3.up * 0.1f, Mathf.Atan2(seg.pad.forward.x, seg.pad.forward.z) * Mathf.Rad2Deg);
            }
            else RespawnPlayer(p);
            Progress = seg.startProgress;
            Physics.SyncTransforms();
            return true;
        }

        // For tests: every live ramp's number, move, riding line, flight in, and root
        public IEnumerable<(int index, string move, Vector3[] line, Vector3[] flight, GameObject root)> LiveRamps()
        {
            foreach (var s in segments) yield return (s.index, s.move, s.line, s.flight, s.root);
        }

        // Keeps the world near the origin: once the player is recenterDistance out, shift
        // everything (ramps, their data, the hall, the player) back by that much
        void Recenter()
        {
            Vector3 p = player.Position;
            if (p.magnitude < recenterDistance) return;
            Vector3 delta = -p;
            ShiftWorld(delta);
            player.ShiftOrigin(delta);
            Physics.SyncTransforms();
        }

        void ShiftWorld(Vector3 delta)
        {
            if (delta == Vector3.zero) return;
            totalShift += delta;
            foreach (var seg in segments)
            {
                seg.root.transform.localPosition += delta;
                for (int k = 0; k < seg.path.ridge.Count; k++) seg.path.ridge[k] += delta;
                for (int k = 0; k < seg.line.Length; k++) seg.line[k] += delta;
                for (int k = 0; k < seg.flight.Length; k++) seg.flight[k] += delta;
                seg.lowestY += delta.y;
            }
            courseStartY += delta.y;
            hallShift += delta;
            foreach (var (_, go) in halls) if (go) go.transform.localPosition += delta;
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
            // How far you see: the course stops at CourseView (fog closing in just before it), so
            // turning round never has to draw a kilometre of buildings; the start terrace keeps
            // its long view over the distant islands
            float Reach(Biome z) => z == Biome.Terrace ? 2500f : CourseView;
            float reach = Mathf.Lerp(Reach(a), Reach(b), t);
            float fogEnd = Mathf.Min(Mathf.Lerp(a.fogEnd, b.fogEnd, t), reach - 40f);
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.fogStartDistance = Mathf.Min(Mathf.Lerp(a.fogStart, b.fogStart, t), fogEnd * 0.6f);
            if (view) view.farClipPlane = reach;
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
