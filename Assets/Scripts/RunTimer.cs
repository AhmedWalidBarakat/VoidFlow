using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // Runs the endless mode and draws the HUD. The run starts when you leave the start
    // hall. Every few ramps a gate marks a checkpoint: fall and you're put back on its drop-in
    // platform with the run still going. Fall before the first one (or press R) and you're back in the hall with a
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
        // The last checkpoint reached, to carry on from (after a refresh too)
        const string SaveRamp = "VoidFlow.checkpointRamp", SaveProgress = "VoidFlow.checkpointProgress", SaveTime = "VoidFlow.checkpointTime", SaveFalls = "VoidFlow.checkpointFalls";
        static bool HasSave => PlayerPrefs.GetInt(SaveRamp, 0) > 0;
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
            course.CheckpointReached += () =>
            {
                if (!running) return;
                Note(Practice ? "CHECKPOINT" : "CHECKPOINT  ·  saved");
                if (Practice) return;
                PlayerPrefs.SetInt(SaveRamp, course.LastCheckpoint);
                PlayerPrefs.SetFloat(SaveProgress, course.Progress);
                PlayerPrefs.SetFloat(SaveTime, Time.time - startTime);
                PlayerPrefs.SetInt(SaveFalls, falls);
                PlayerPrefs.Save();
            };
            course.CourseFinished += Finish;
            course.BiomeEntered += b => { banner = $"{b.name}\nSTAGE {course.CurrentStage + 1}  ·  {course.CurrentTierName}  ·  {course.CurrentStageName}"; bannerTime = Time.time; };
            Restart();
        }

        void Restart()
        {
            course.ResetCourse();
            player.Teleport(spawnPoint.position, spawnPoint.eulerAngles.y);
            running = false;
            falls = 0;
            runStage = 0;
            Practice = false;
            finishTime = -99f;
        }

        void Note(string text) { note = text; noteTime = Time.time; }

        // From the hall: carry on from the saved checkpoint, clock and falls as they were
        void Continue()
        {
            if (!course.ResumeAt(PlayerPrefs.GetInt(SaveRamp, 0), PlayerPrefs.GetFloat(SaveProgress, 0f), player)) return;
            running = true;
            startTime = Time.time - PlayerPrefs.GetFloat(SaveTime, 0f);
            falls = PlayerPrefs.GetInt(SaveFalls, 0);
            finishTime = -99f;
            runStage = course.CurrentStage; // (no retroactive cases for stages already passed)
            Note("CONTINUING FROM YOUR LAST CHECKPOINT");
        }

        // The end of the course: the time stops, the congratulations go up, and every item in
        // the game is yours
        float finishTime = -99f, finalTime;
        int runStage; // the stage this run is on, for the free Void Case every 10 stages
        // Noclip used this run: it's practice from then on (nothing saved or rewarded)
        public bool Practice { get; private set; }
        void Finish()
        {
            finalTime = Time.time - startTime;
            finishTime = Time.time;
            if (Practice) return; // (a noclip run doesn't win anything)
            // The course is beaten: nothing left to carry on from
            PlayerPrefs.DeleteKey(SaveRamp);
            PlayerPrefs.Save();
            Inventory.GrantEverything();
        }
        bool FinishedRun => course.Finished && finishTime > 0f;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && !ViewModel.InputBlocked) { Restart(); return; }

            Vector3 p = player.Position;
            if (!running && HasSave && kb != null && kb.cKey.wasPressedThisFrame && !ViewModel.InputBlocked)
            {
                Continue();
                return;
            }
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

            // Past the finish: falling off the plaza just puts you back on it
            if (FinishedRun && course.IsFallen(p) && !player.Flying)
            {
                player.Teleport(course.FinishSpawn, course.FinishYaw);
                return;
            }
            if (course.IsFallen(p) && !player.Flying)
            {
                if (running && course.RespawnAtCheckpoint(player))
                {
                    falls++;
                    Note("CHECKPOINT  ·  drop in when you're ready");
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

            // A free Void Case on reaching stage 10, 20 and 30
            if (running && player.Flying && !Practice)
            {
                Practice = true;
                Note("NOCLIP  ·  practice run, nothing saved");
            }
            if (running && course.CurrentStage != runStage)
            {
                int stage = course.CurrentStage;
                if (stage > runStage && (stage + 1) % 10 == 0 && !Practice)
                    Inventory.AddCase($"You reached stage {stage + 1}!");
                runStage = stage;
            }

            if (running && !player.Flying && !Practice && course.Progress > best)
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
                    $"stage {course.CurrentStage + 1}: {course.CurrentTierName} {course.CurrentStageName}   ·   ramp {course.CurrentRamp + 1}   ·   {course.CurrentBiome.name}   ·   {Format(FinishedRun ? finalTime : Time.time - startTime)}" + (falls > 0 ? $"   ·   {falls} fall{(falls == 1 ? "" : "s")}" : ""),
                    centeredStyle);
            }
            else
            {
                GUI.Label(new Rect(0, 20, w, 40), "Drop in to start from the beginning", bigStyle);
                if (HasSave)
                {
                    int ramp = PlayerPrefs.GetInt(SaveRamp, 0);
                    GUI.Label(new Rect(0, 58, w, 30), $"or press C to continue from your last checkpoint  ·  ramp {ramp + 1}  ·  {course.BiomeAt(ramp).name}  ·  {Format(PlayerPrefs.GetFloat(SaveTime, 0f))}", bigStyle);
                }
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

            if (FinishedRun)
            {
                // CONGRATS, pulsing through the colors, for as long as you stand at the finish
                float since = Time.time - finishTime;
                var old = GUI.color;
                GUI.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.25f, 1f), 0.55f, 1f) * new Color(1f, 1f, 1f, Mathf.Clamp01(since * 2f));
                float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 5f);
                var big = new GUIStyle(bannerStyle) { fontSize = Mathf.RoundToInt(80 * pulse) };
                GUI.Label(new Rect(0, h * 0.18f, w, 120), "CONGRATS!!!!!!!!!", big);
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(since * 2f - 0.5f));
                GUI.Label(new Rect(0, h * 0.18f + 110, w, 40), $"You beat the whole course  ·  {Format(finalTime)}" + (falls > 0 ? $"  ·  {falls} fall{(falls == 1 ? "" : "s")}" : "  ·  no falls!"), bigStyle);
                GUI.Label(new Rect(0, h * 0.18f + 150, w, 40), Practice ? "practice run (noclip used): beat it without noclip to win every item" : "EVERY ITEM IN THE GAME IS NOW IN YOUR INVENTORY  (press I)", bigStyle);
                GUI.Label(new Rect(0, h * 0.18f + 190, w, 24), "Press R to run it again", centeredStyle);
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
