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
    public partial class RunTimer : MonoBehaviour
    {
        public PlayerMovement player;
        public EndlessCourse course;
        public BoxCollider startZone;
        public Transform spawnPoint;
        [Tooltip("Stops players building speed inside the start zone (Source units per second).")]
        public float startZoneSpeedCap = 350f;

        const string BestKey = "VoidFlow.bestDistance";
        // The last checkpoint reached, to carry on from (after a refresh too)
        const string SaveRamp = "VoidFlow.checkpointRamp", SaveProgress = "VoidFlow.checkpointProgress", SaveTime = "VoidFlow.checkpointTime", SaveFalls = "VoidFlow.checkpointFalls", SaveFrom = "VoidFlow.checkpointFrom";
        const string RunBestKey = "VoidFlow.runBest"; // the whole course, from stage 1
        static bool HasSave => PlayerPrefs.GetInt(SaveRamp, 0) > 0;
        const float BannerSeconds = 3f;

        bool running;
        public bool Running => running;
        public event System.Action Respawned, RunReset; // (for the stage clock)
        public int Falls => falls;
        public float RunTime => FinishedRun ? finalTime : running ? Time.time - startTime : 0f;
        float startTime, best;
        float lastRun;
        string banner;
        float bannerTime = -99f;
        string note;
        float noteTime = -99f;
        int falls;

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
                PlayerPrefs.SetInt(SaveFrom, runFrom);
                PlayerPrefs.Save();
            };
            course.CourseFinished += Finish;
            course.BiomeEntered += b => { banner = $"{b.name}\nSTAGE {course.CurrentStage + 1}  ·  {course.CurrentTierName}  ·  {course.CurrentStageName}"; bannerTime = Time.time; };
            Restart();
        }

        void Restart()
        {
            EndRun();
            player.Teleport(spawnPoint.position, spawnPoint.eulerAngles.y);
        }

        // The run stops where it is and the course goes back to its start (the start hall shown,
        // the world recentred), for a fresh run from the hall; the saved checkpoint stays for C.
        // Leaving for the bhop challenge from the stage menu ends a run this way.
        public void EndRun()
        {
            course.ResetCourse();
            running = false;
            falls = 0;
            runStage = 0;
            runFrom = 0;
            topSpeed = 0f;
            RunReset?.Invoke();
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
            runFrom = PlayerPrefs.GetInt(SaveFrom, 0);
            topSpeed = 0f;
            RunReset?.Invoke();
            finishTime = -99f;
            runStage = course.CurrentStage; // (no retroactive cases for stages already passed)
            Note("CONTINUING FROM YOUR LAST CHECKPOINT");
        }

        // From the stage menu: a fresh run from the start of a stage you've reached before (its
        // checkpoint platform), saving as you go like any run. Stage 0 is the start hall.
        public bool StartAtStage(int stage)
        {
            if (!course.Ready) return false;
            if (stage <= 0) { Restart(); Note("BACK TO THE START"); return true; }
            if (!course.ResumeAt(stage * course.rampsPerBiome, 0f, player)) return false;
            running = true;
            startTime = Time.time;
            falls = 0;
            runFrom = stage;
            topSpeed = 0f;
            RunReset?.Invoke();
            Practice = false;
            finishTime = -99f;
            runStage = course.CurrentStage;
            banner = $"{course.CurrentBiome.name}\nSTAGE {course.CurrentStage + 1}  ·  {course.CurrentTierName}  ·  {course.CurrentStageName}";
            bannerTime = Time.time;
            return true;
        }

        // The end of the course: the time stops, the congratulations go up, and every item in
        // the game is yours
        float finishTime = -99f, finalTime;
        int runStage; // the stage this run is on, for the free Void Case every 10 stages
        float hudAt;
        int hudSpeedValue = -1;
        // Noclip used this run: it's practice from then on (nothing saved or rewarded)
        public bool Practice { get; private set; }
        int runFrom;           // the stage this run started from (0: the whole course)
        float topSpeed;        // u/s, this run
        float prevRunBest;     // the whole-course best before this run
        bool newRunBest;
        void Finish()
        {
            finalTime = Time.time - startTime;
            finishTime = Time.time;
            prevRunBest = PlayerPrefs.GetFloat(RunBestKey, 0f);
            newRunBest = false;
            if (Practice) return; // (a noclip run doesn't win anything)
            if (runFrom == 0 && (prevRunBest <= 0f || finalTime < prevRunBest))
            {
                newRunBest = true;
                PlayerPrefs.SetFloat(RunBestKey, finalTime);
            }
            // The course is beaten: nothing left to carry on from
            PlayerPrefs.DeleteKey(SaveRamp);
            PlayerPrefs.Save();
            Inventory.GrantEverything();
        }
        bool FinishedRun => course.Finished && finishTime > 0f;

        void Update()
        {
            var kb = Keyboard.current;
            if (BhopChallenge.Active && !running) return; // (out at the bhop challenge: it has its own rules)
            if (kb != null && kb.rKey.wasPressedThisFrame && !ViewModel.InputBlocked) { Restart(); return; }

            Vector3 p = player.Position;
            if (!course.Ready)
            {
                // The course is still being built: stay on the spawn pad until it's done
                if ((p - spawnPoint.position).sqrMagnitude > 1f) player.Teleport(spawnPoint.position, spawnPoint.eulerAngles.y);
                return;
            }
            if (!running && HasSave && kb != null && kb.cKey.wasPressedThisFrame && !ViewModel.InputBlocked)
            {
                Continue();
                return;
            }
            if (running && !FinishedRun && !player.Flying)
                topSpeed = Mathf.Max(topSpeed, player.HorizontalSpeed / PlayerMovement.SourceUnit);
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

            // T: back to this stage's checkpoint, its clock started again (as surf servers' !r)
            if (running && !FinishedRun && kb != null && kb.tKey.wasPressedThisFrame && !ViewModel.InputBlocked && course.RespawnAtCheckpoint(player))
            {
                Respawned?.Invoke();
                Note("STAGE RESTARTED");
                return;
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
                    Respawned?.Invoke();
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
            float w = Screen.width, h = Screen.height;
            if (!course.Ready)
            {
                // Building the course: a black screen with just the logo, the progress bar and a
                // tip (once, when the game opens), so the stutter of building behind it never shows
                DrawLoading(w, h, Mathf.Clamp(h / 1080f, 0.6f, 2f), course.BuildProgress);
                return;
            }

            // The readouts are rebuilt ten times a second, not on every GUI pass (making new
            // strings every frame fed the garbage collector, whose pauses were hitches)
            float px = Mathf.Clamp(h / 1080f, 0.6f, 2f);
            if (Time.unscaledTime >= hudAt)
            {
                hudAt = Time.unscaledTime + 0.1f;
                int speedNow = Mathf.RoundToInt(player.HorizontalSpeed / PlayerMovement.SourceUnit);
                if (speedNow != hudSpeedValue) { hudSpeedValue = speedNow; hudSpeedText = speedNow.ToString(); }
                RefreshHud();
            }
            bool menu = PauseMenu.Showing || Inventory.IsOpen || StageMenu.IsOpen;

            bool bhop = BhopChallenge.Active && !running;
            if (running) DrawRunHud(w, h, px);
            else if (!menu && !bhop) DrawHallHud(w, h, px);

            float age = Time.time - bannerTime;
            if (banner != null && age < BannerSeconds)
                DrawBanner(w, h, px, banner, Mathf.Clamp01(Mathf.Min(age * 3f, (BannerSeconds - age) * 1.5f)));

            float noteAge = Time.time - noteTime;
            if (note != null && noteAge < 1.6f)
                DrawNote(w, h, px, note, Mathf.Clamp01(Mathf.Min(noteAge * 5f, (1.6f - noteAge) * 2f)));

            if (FinishedRun && !menu) DrawResults(w, h, px, Time.time - finishTime);

            if (menu) return;
            if (running || bhop) DrawSpeed(w, h, px); // (in the hall the controls card sits there)
            if (GameSettings.ShowKeys && (running || bhop) && !FinishedRun) DrawKeys(w, h, px); // (the results card sits there at the end)

            if (player.Flying)
                UiArt.Text(new Rect(0, 120f * px, w, 24f * px), "NOCLIP   ·   WASD fly · SPACE up · CTRL down · SHIFT fast · double tap SPACE to land", Mathf.RoundToInt(13 * px), Gold, TextAnchor.MiddleCenter);
            // The controls only show in the hall; once you drop in the screen is clear for the run
            if (!running && !bhop) DrawHelp(w, h, px);
        }
    }
}
