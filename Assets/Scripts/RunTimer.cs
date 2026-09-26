using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // Runs the endless mode and draws the HUD. The run starts when you leave the start
    // hall. Every few ramps a gate marks a checkpoint: fall and you're put back there with the
    // run still going. Fall before the first one (or press R) and you're back in the hall with a
    // fresh course.
    // Tracks distance, time, your last and best distance, and announces each biome as you
    // enter it.
    public class RunTimer : MonoBehaviour
    {
        public PlayerMovement player;
        public EndlessCourse course;
        public BoxCollider startZone;
        public Transform spawnPoint;
        [Tooltip("Stops players building speed inside the start zone (Source units per second).")]
        public float startZoneSpeedCap = 350f;

        const string BestKey = "VoidFlow.bestDistance";
        const float BannerSeconds = 3f;

        bool running;
        public bool Running => running;
        float startTime, best;
        float lastRun;
        string banner;
        float bannerTime = -99f;
        string note;
        float noteTime = -99f;
        int falls;
        GUIStyle bigStyle, smallStyle, centeredStyle, bannerStyle;

        void Start()
        {
            best = PlayerPrefs.GetFloat(BestKey, 0f);
            course.CheckpointReached += () => Note("CHECKPOINT");
            course.BiomeEntered += b => { banner = $"{b.name}\nSTAGE {course.CurrentStage + 1}  ·  {course.CurrentTierName}  ·  {course.CurrentStageName}"; bannerTime = Time.time; };
            Restart();
        }

        void Restart()
        {
            course.ResetCourse();
            player.Teleport(spawnPoint.position, spawnPoint.eulerAngles.y);
            running = false;
            falls = 0;
        }

        void Note(string text) { note = text; noteTime = Time.time; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && !ViewModel.InputBlocked) { Restart(); return; }

            Vector3 p = player.Position;
            if (!running)
            {
                if (startZone.bounds.Contains(p + Vector3.up * 0.9f))
                {
                    if (!player.Flying) player.LimitHorizontalSpeed(startZoneSpeedCap * PlayerMovement.SourceUnit);
                }
                else
                {
                    running = true;
                    startTime = Time.time;
                    banner = $"{course.CurrentBiome.name}\nSTAGE 1  ·  {course.CurrentTierName}  ·  {course.CurrentStageName}";
                    bannerTime = Time.time;
                }
            }

            if (course.IsFallen(p) && !player.Flying)
            {
                if (running && course.RespawnAtCheckpoint(player))
                {
                    falls++;
                    Note("BACK TO CHECKPOINT");
                    return;
                }
                if (running)
                {
                    lastRun = course.Progress;
                    banner = "FELL  ·  " + Distance(lastRun);
                    bannerTime = Time.time;
                }
                Restart();
                return;
            }

            if (running && !player.Flying && course.Progress > best)
            {
                best = course.Progress;
                PlayerPrefs.SetFloat(BestKey, best);
            }
        }

        void OnApplicationQuit() => PlayerPrefs.Save();

        static string Distance(float metres) => metres < 1000f ? $"{metres:0} m" : $"{metres / 1000f:0.00} km";
        static string Format(float t) => $"{(int)(t / 60f)}:{t % 60f:00.0}";

        void OnGUI()
        {
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                centeredStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
                bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            float w = Screen.width, h = Screen.height;

            if (running)
            {
                GUI.Label(new Rect(0, 16, w, 40), Distance(course.Progress), bigStyle);
                GUI.Label(new Rect(0, 52, w, 24),
                    $"stage {course.CurrentStage + 1}: {course.CurrentTierName} {course.CurrentStageName}   ·   ramp {course.CurrentRamp + 1}   ·   {course.CurrentBiome.name}   ·   {Format(Time.time - startTime)}" + (falls > 0 ? $"   ·   {falls} fall{(falls == 1 ? "" : "s")}" : ""),
                    centeredStyle);
            }
            else
            {
                GUI.Label(new Rect(0, 20, w, 40), "Drop in to start", bigStyle);
            }

            float age = Time.time - bannerTime;
            if (banner != null && age < BannerSeconds)
            {
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Min(age * 3f, (BannerSeconds - age) * 1.5f)));
                GUI.Label(new Rect(0, h * 0.24f, w, 140), banner, bannerStyle);
                GUI.color = old;
            }

            float noteAge = Time.time - noteTime;
            if (note != null && noteAge < 1.6f)
            {
                var old = GUI.color;
                GUI.color = new Color(0.75f, 1f, 0.8f, Mathf.Clamp01(Mathf.Min(noteAge * 5f, (1.6f - noteAge) * 2f)));
                GUI.Label(new Rect(0, h * 0.16f, w, 40), note, bigStyle);
                GUI.color = old;
            }

            float speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;
            GUI.Label(new Rect(0, h - 90, w, 40), $"{speed:0} u/s", bigStyle);

            if (best > 0f) GUI.Label(new Rect(w - 200, 20, 190, 24), "Best  " + Distance(best), smallStyle);
            if (lastRun > 0f) GUI.Label(new Rect(w - 200, 40, 190, 24), "Last  " + Distance(lastRun), smallStyle);

            if (player.Flying)
                GUI.Label(new Rect(0, 84, w, 24), "NOCLIP   ·   WASD fly · Space up · Ctrl down · Shift fast · double tap Space to land", centeredStyle);
            string help = Cursor.lockState == CursorLockMode.Locked
                ? "WASD move · Space jump (hold to bhop) · R restart the run · double tap Space noclip · Esc release mouse\n1 sniper · 2 knife · Q last weapon · Click fire · Right click scope · F inspect · E use · I inventory\nOn ramps: let go of W, hold A or D toward the ramp, and steer with the mouse"
                : "Click to capture the mouse";
            // The controls only show in the hall; once you drop in the screen is clear for the run
            if (!running || Cursor.lockState != CursorLockMode.Locked) GUI.Label(new Rect(12, h - 66, w - 24, 62), help, smallStyle);
        }
    }
}
