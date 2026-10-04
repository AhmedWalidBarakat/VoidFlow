using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // A Void Shard hanging over the course; CourseRewards spins it and picks it up
    public class VoidShard : MonoBehaviour
    {
        public static readonly List<VoidShard> All = new();
        public static readonly List<VoidShard> Taken = new(); // collected this run (hidden, back on a restart)
        public float phase;

        public static void RestoreAll()
        {
            foreach (var s in Taken) if (s) s.gameObject.SetActive(true);
            Taken.Clear();
        }
        void OnEnable() { All.Add(this); phase = Random.value * 10f; }
        void OnDisable() => All.Remove(this);
    }

    // A big-air ring that boosts you when you fly through it
    public class SpeedRing : MonoBehaviour
    {
        public static readonly List<SpeedRing> All = new();
        public Vector3 facing = Vector3.forward;
        public float radius = 6f;
        public float usedAt = -99f;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }

    // The course's reasons to go again, all in the one run, no modes:
    //  - Void Shards: crystals on the harder lines of each tier. Every 25 make a Void Case.
    //    Picking them up in quick succession climbs a little scale of chimes.
    //  - Speed rings: fly through a big-air ring for a push of speed.
    //  - Stage times: each stage you clear is timed and kept against your best for its tier.
    public class CourseRewards : MonoBehaviour
    {
        public EndlessCourse course;
        public PlayerMovement player;
        public RunTimer timer;
        public int shardsPerCase = 25;
        public float pickupRadius = 2.8f;
        public float ringBoost = 1.08f;

        // Session only, like the inventory
        static int shards;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => shards = 0;

        AudioSource audioSource;
        int stage = -1, stageShards, combo;
        float stageStart, lastShard = -99f, shardPop = -99f, boostTime = -99f, clearTime = -99f;
        Vector3 lastPosition;
        string clearText = "", clearDetail = "";
        Color clearColor = Color.white;
        readonly List<VoidShard> picked = new();
        // Frames a second (when SHOW FPS is on), counted over half a second at a time
        int fpsFrames, fpsShown;
        float fpsTime;
        string fpsText = "";

        void Start()
        {
            if (!course) course = FindAnyObjectByType<EndlessCourse>();
            if (!player) player = FindAnyObjectByType<PlayerMovement>();
            if (!timer) timer = FindAnyObjectByType<RunTimer>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (player) lastPosition = player.Position;
        }

        void Update()
        {
            fpsFrames++;
            fpsTime += Time.unscaledDeltaTime;
            if (fpsTime >= 0.5f)
            {
                fpsShown = Mathf.RoundToInt(fpsFrames / fpsTime);
                fpsText = $"{fpsShown} FPS";
                fpsFrames = 0;
                fpsTime = 0f;
            }
            if (!player || !course) return;
            float now = Time.time;
            Vector3 p = player.Position;

            // Shards spin and bob; close enough and they're yours
            picked.Clear();
            foreach (var s in VoidShard.All)
            {
                var t = s.transform;
                t.localRotation = Quaternion.Euler(0f, (now + s.phase) * 90f, 0f);
                if ((t.position - p).sqrMagnitude < pickupRadius * pickupRadius) picked.Add(s);
            }
            // (none for noclip or a practice run)
            if (!player.Flying && !(timer && timer.Practice))
                foreach (var s in picked) Collect(s, now);

            // Rings: crossing a ring's plane inside it gives a boost (once per ring)
            foreach (var ring in SpeedRing.All)
            {
                if (now - ring.usedAt < 2f) continue;
                Vector3 c = ring.transform.position;
                float before = Vector3.Dot(lastPosition - c, ring.facing), after = Vector3.Dot(p - c, ring.facing);
                if (before < 0f && after >= 0f)
                {
                    Vector3 onPlane = p - ring.facing * after;
                    if ((onPlane - c).magnitude < ring.radius)
                    {
                        ring.usedAt = now;
                        boostTime = now;
                        player.Boost(ringBoost);
                        audioSource.PlayOneShot(WeaponSounds.Boost, 0.55f);
                        FxLibrary.Celebrate(c, new Color(0.55f, 0.85f, 1f), false);
                    }
                }
            }
            lastPosition = p;

            // Stage times: the run's stage changes as you cross each gate
            bool running = timer && timer.Running;
            if (!running) { stage = -1; return; }
            if (stage < 0) { stage = course.CurrentStage; stageStart = now; stageShards = 0; return; }
            if (course.CurrentStage != stage)
            {
                StageCleared(stage, now - stageStart);
                stage = course.CurrentStage;
                stageStart = now;
                stageShards = 0;
            }
        }

        void Collect(VoidShard s, float now)
        {
            Vector3 at = s.transform.position;
            s.gameObject.SetActive(false);
            VoidShard.Taken.Add(s);
            shards++;
            stageShards++;
            combo = now - lastShard < 1.5f ? combo + 1 : 0;
            lastShard = shardPop = now;
            // A little rising scale while you keep picking them up
            var one = gameObject.AddComponent<AudioSource>();
            one.spatialBlend = 0f;
            one.pitch = Mathf.Pow(1.0595f, Mathf.Min(combo, 8) * 2);
            one.PlayOneShot(WeaponSounds.Shard, 0.4f);
            Destroy(one, 1f);
            FxLibrary.Sparkle(at, Vector3.up * 2f, new Color(1f, 0.45f, 0.9f));
            if (shards % shardsPerCase == 0) Inventory.AddCase($"{shardsPerCase} Void Shards collected");
        }

        void StageCleared(int cleared, float seconds)
        {
            var tier = EndlessCourse.TierOf(cleared);
            string key = $"VoidFlow.stageBest.{tier}";
            float best = PlayerPrefs.GetFloat(key, 0f);
            bool record = best <= 0f || seconds < best;
            if (record)
            {
                PlayerPrefs.SetFloat(key, seconds);
                PlayerPrefs.Save();
            }
            clearText = $"STAGE {cleared + 1} CLEAR   ·   {EndlessCourse.TierNames[(int)tier]}";
            clearDetail = $"{Format(seconds)}" + (record ? "   ·   NEW BEST" : $"   ·   best {Format(best)}") + (stageShards > 0 ? $"   ·   ◆ {stageShards}" : "");
            clearColor = EndlessCourse.TierColors[(int)tier];
            clearTime = Time.time;
            audioSource.PlayOneShot(record ? WeaponSounds.Reveal : WeaponSounds.Tick, record ? 0.45f : 0.6f);
        }

        static string Format(float t) => $"{(int)(t / 60f)}:{t % 60f:00.0}";

        void OnGUI()
        {
            if (Inventory.IsOpen || !player) return;
            float k = Mathf.Clamp(Screen.height / 800f, 0.6f, 2.2f);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float W = Screen.width / k, now = Time.time;

            // Frames a second, in the corner (green smooth, amber fair, red struggling)
            float top = 16f;
            if (GameSettings.ShowFps && course && course.Ready) // (not over the loading screen, where frames are slow on purpose)
            {
                var fr = new Rect(16f, top, 96f, 26f);
                UiArt.Rounded(fr, new Color(0.05f, 0.02f, 0.09f, 0.75f), 10f);
                Color fc = fpsShown >= 55 ? new Color(0.45f, 1f, 0.6f) : fpsShown >= 30 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f);
                UiArt.Text(fr, fpsText, 14, fc, TextAnchor.MiddleCenter);
                top += 34f;
            }

            // Shard counter, top left (under the frame rate), popping on each pickup
            bool running = timer && timer.Running;
            if (running || shards > 0)
            {
                float pop = 1f + 0.25f * Mathf.Max(0f, 1f - (now - shardPop) / 0.25f);
                var r = UiArt.Grow(new Rect(16f, top, 200f, 44f), pop);
                UiArt.Rounded(r, new Color(0.05f, 0.02f, 0.09f, 0.75f), 12f);
                int into = shards % shardsPerCase;
                var bar = new Rect(r.x + 10f, r.yMax - 10f, (r.width - 20f), 4f);
                UiArt.Rounded(bar, new Color(1f, 1f, 1f, 0.1f), 2f);
                UiArt.Rounded(new Rect(bar.x, bar.y, bar.width * into / shardsPerCase, 4f), new Color(1f, 0.45f, 0.9f), 2f);
                UiArt.Text(new Rect(r.x + 12f, r.y + 4f, r.width - 20f, 26f), $"◆ {into} / {shardsPerCase}", 17, Color.white);
                UiArt.Text(new Rect(r.x + 12f, r.y + 4f, r.width - 22f, 26f), "VOID SHARDS", 11, new Color(1f, 0.6f, 0.9f), TextAnchor.MiddleRight);
            }

            // A pill that slides in when you clear a stage
            float ct = now - clearTime;
            if (ct < 4f)
            {
                float a = Mathf.Clamp01(ct / 0.2f) * (1f - Mathf.Clamp01((ct - 3.4f) / 0.6f));
                float slide = 1f - UiArt.BackOut(Mathf.Clamp01(ct / 0.45f));
                var r = new Rect(W / 2f - 260f + slide * 80f, 430f, 520f, 64f); // below the stage banner that shows at the same moment
                UiArt.Rounded(r, new Color(0.04f, 0.02f, 0.07f, 0.85f * a), 18f);
                UiArt.Rounded(r, new Color(clearColor.r, clearColor.g, clearColor.b, a), 18f, 2f);
                UiArt.Text(new Rect(r.x, r.y + 6f, r.width, 28f), clearText, 20, new Color(clearColor.r, clearColor.g, clearColor.b, a), TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(r.x, r.y + 34f, r.width, 22f), clearDetail, 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
            }

            // BOOST flashes by the crosshair
            float bt = now - boostTime;
            if (bt < 0.8f)
            {
                float a = 1f - bt / 0.8f;
                UiArt.Text(new Rect(0f, Screen.height / k * 0.58f - bt * 30f, W, 30f), "BOOST", 22, new Color(0.6f, 0.9f, 1f, a), TextAnchor.MiddleCenter);
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }
}
