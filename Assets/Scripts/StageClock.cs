using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VoidFlow
{
    // Stage times and ghosts, the way surf servers time a run: each stage is timed from the
    // moment you enter it (or drop back in after a fall) to the moment you reach the next
    // stage's checkpoint. Your best time on every stage is kept, with the path you took: the next
    // time you ride that stage, a glowing ghost of your best run rides it with you, and on
    // reaching the next checkpoint you see your split against it (green faster, red slower,
    // gold for a new best). Noclip (practice) runs aren't timed.
    public class StageClock : MonoBehaviour
    {
        const float SampleRate = 15f;           // ghost positions a second
        const string BestKey = "VoidFlow.pb.";  // + stage: best time in seconds

        RunTimer timer;
        EndlessCourse course;
        PlayerMovement player;

        // the stage being ridden
        int stage = -1;
        float startedAt;
        readonly List<Vector3> path = new();
        float nextSample;

        // the ghost of the best run on this stage
        List<Vector3> ghostPath;
        Transform ghost;
        TrailRenderer ghostTrail;
        Material ghostMat;

        public static StageClock Instance { get; private set; }

        // For the HUD
        public bool Timing => stage >= 0;
        public float StageTime => Timing ? Time.time - startedAt : 0f;
        public int Stage => stage;
        public float BestFor(int s) => PlayerPrefs.GetFloat(BestKey + s, 0f);
        public (int stage, float time, float delta, bool best, float at) LastSplit { get; private set; } = (-1, 0f, 0f, false, -99f);
        // This run's stage times, for the results at the finish: each stage's time and the best
        // it was up against (0 the first time)
        public readonly Dictionary<int, (float time, float prior)> RunSplits = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            var t = FindAnyObjectByType<RunTimer>();
            if (!t) return;
            var go = new GameObject("StageClock");
            var c = go.AddComponent<StageClock>();
            c.timer = t;
            c.course = t.course;
            c.player = t.player;
        }

        void Awake() => Instance = this;

        void Start()
        {
            timer.Respawned += () => { if (Timing) Begin(stage); };
            timer.RunReset += () => { Stop(); RunSplits.Clear(); };
            course.CourseFinished += () => { if (Timing) Finish(stage); Stop(); };
        }

        void Update()
        {
            bool timed = timer.Running && !timer.Practice && !player.Flying && course.Ready && !course.Finished;
            if (!timed) { if (Timing) Stop(); return; }
            int now = course.CurrentStage;
            if (!Timing) Begin(now);
            else if (now != stage)
            {
                if (now == stage + 1) Finish(stage);
                Begin(now);
            }
            // (on a fixed schedule from the stage's start, so the ghost's clock never drifts from
            // the real one: a sample is never more than a frame late, and lateness never adds up)
            while (Time.time >= nextSample)
            {
                nextSample += 1f / SampleRate;
                path.Add(course.ToCourse(player.Position));
            }
            UpdateGhost();
        }

        // Starts timing a stage (and its ghost, if there's a best run of it)
        void Begin(int s)
        {
            stage = s;
            startedAt = Time.time;
            path.Clear();
            nextSample = startedAt;
            ghostPath = GameSettings.Ghost ? Load(s) : null;
            if (ghostTrail) ghostTrail.Clear();
        }

        void Stop()
        {
            stage = -1;
            path.Clear();
            ghostPath = null;
            if (ghost) ghost.gameObject.SetActive(false);
        }

        // Reached the next stage: the split, and a new best kept with its path
        void Finish(int s)
        {
            float t = Time.time - startedAt;
            if (t < 1f) return; // (a teleport, not a ride)
            float best = BestFor(s);
            bool isBest = best <= 0f || t < best;
            LastSplit = (s, t, best > 0f ? t - best : 0f, isBest, Time.time);
            RunSplits[s] = (t, best);
            if (!isBest) return;
            PlayerPrefs.SetFloat(BestKey + s, t);
            PlayerPrefs.Save();
            path.Add(course.ToCourse(player.Position));
            Save(s, path);
        }

        // ------------------------------------------------------------------ the ghost

        void UpdateGhost()
        {
            if (ghostPath == null || ghostPath.Count < 2 || !GameSettings.Ghost)
            {
                if (ghost) ghost.gameObject.SetActive(false);
                return;
            }
            if (!ghost) MakeGhost();
            float f = (Time.time - startedAt) * SampleRate;
            if (f >= ghostPath.Count - 1)
            {
                ghost.gameObject.SetActive(false); // (it's finished the stage)
                return;
            }
            int i = Mathf.FloorToInt(f);
            Vector3 local = Vector3.Lerp(ghostPath[i], ghostPath[i + 1], f - i);
            Vector3 at = course.FromCourse(local) + Vector3.up * 0.9f;
            // (a jump of more than 40m between samples is a respawn in the recording: no streak)
            if (!ghost.gameObject.activeSelf || (ghost.position - at).sqrMagnitude > 1600f) { ghost.position = at; ghostTrail.Clear(); }
            ghost.gameObject.SetActive(true);
            ghost.position = at;
            // it pulses gently, brighter when you're close to it
            float near = Mathf.InverseLerp(60f, 4f, Vector3.Distance(at, player.Position));
            ghostMat.SetColor("_EmissionColor", GhostColor * (1.6f + 1.2f * near + 0.3f * Mathf.Sin(Time.time * 6f)));
        }

        static readonly Color GhostColor = new(0.65f, 0.35f, 1f);

        void MakeGhost()
        {
            var vm = FindAnyObjectByType<ViewModel>();
            ghostMat = vm && vm.template ? new Material(vm.template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ghostMat.name = "Ghost";
            ghostMat.SetColor("_BaseColor", GhostColor * 0.4f);
            ghostMat.EnableKeyword("_EMISSION");
            ghostMat.SetColor("_EmissionColor", GhostColor * 2f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "Ghost";
            go.transform.localScale = Vector3.one * 0.7f;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ghostMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostTrail = go.AddComponent<TrailRenderer>();
            ghostTrail.sharedMaterial = ghostMat;
            ghostTrail.time = 0.7f;
            ghostTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 0f));
            ghostTrail.minVertexDistance = 0.5f;
            ghostTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghost = go.transform;
        }

        // ------------------------------------------------------------------ saved paths

        static string PathFor(int s) => Path.Combine(Application.persistentDataPath, $"ghost_{s}.bin");

        static void Save(int s, List<Vector3> points)
        {
            try
            {
                using var w = new BinaryWriter(File.Open(PathFor(s), FileMode.Create));
                w.Write(1); // (format)
                w.Write(points.Count);
                foreach (var p in points) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
            }
            catch (System.Exception e) { Debug.LogWarning($"VoidFlow: couldn't save the ghost of stage {s}: {e.Message}"); }
        }

        static List<Vector3> Load(int s)
        {
            try
            {
                string file = PathFor(s);
                if (!File.Exists(file)) return null;
                using var r = new BinaryReader(File.OpenRead(file));
                if (r.ReadInt32() != 1) return null;
                int n = r.ReadInt32();
                if (n < 2 || n > 100000) return null;
                var points = new List<Vector3>(n);
                for (int i = 0; i < n; i++) points.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
                return points;
            }
            catch { return null; }
        }

        // For the stage menu: forget every best time and ghost
        public static void ResetAll(int stages)
        {
            for (int s = 0; s < stages; s++)
            {
                PlayerPrefs.DeleteKey(BestKey + s);
                try { if (File.Exists(PathFor(s))) File.Delete(PathFor(s)); } catch { }
            }
            PlayerPrefs.Save();
        }
    }
}
