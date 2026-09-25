using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // Runs the endless mode and draws the HUD. The run starts when you leave the start
    // hall. Fall off (or press R) and it's over: you're back in the hall with a fresh course.
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
        GUIStyle bigStyle, smallStyle, centeredStyle, bannerStyle;

        void Start()
        {
            best = PlayerPrefs.GetFloat(BestKey, 0f);
            course.BiomeEntered += b => { banner = b.name; bannerTime = Time.time; };
            Restart();
        }

        void Restart()
        {
            course.ResetCourse();
            player.Teleport(spawnPoint.position, spawnPoint.eulerAngles.y);
            running = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) { Restart(); return; }

            Vector3 p = player.Position;
            if (!running)
            {
                if (startZone.bounds.Contains(p + Vector3.up * 0.9f))
                    player.LimitHorizontalSpeed(startZoneSpeedCap * PlayerMovement.SourceUnit);
                else
                {
                    running = true;
                    startTime = Time.time;
                    banner = course.CurrentBiome.name;
                    bannerTime = Time.time;
                }
            }

            if (course.IsFallen(p))
            {
                // No checkpoints: a fall ends the run
                if (running)
                {
                    lastRun = course.Progress;
                    banner = "FELL  ·  " + Distance(lastRun);
                    bannerTime = Time.time;
                }
                Restart();
                return;
            }

            if (running && course.Progress > best)
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
                    $"ramp {course.CurrentRamp + 1}   ·   level {course.Level + 1}   ·   {course.CurrentBiome.name}   ·   {Format(Time.time - startTime)}",
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
                GUI.Label(new Rect(0, h * 0.28f, w, 70), banner, bannerStyle);
                GUI.color = old;
            }

            float speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;
            GUI.Label(new Rect(0, h - 90, w, 40), $"{speed:0} u/s", bigStyle);

            if (best > 0f) GUI.Label(new Rect(w - 200, 20, 190, 24), "Best  " + Distance(best), smallStyle);
            if (lastRun > 0f) GUI.Label(new Rect(w - 200, 40, 190, 24), "Last  " + Distance(lastRun), smallStyle);

            string help = Cursor.lockState == CursorLockMode.Locked
                ? "WASD move · Space jump (hold to bhop) · R restart · Esc release mouse\n1 knife · 2 sniper · Q last weapon · Click fire · Right click scope · F inspect · E use\nOn ramps: let go of W, hold A or D toward the ramp, and steer with the mouse"
                : "Click to capture the mouse";
            GUI.Label(new Rect(12, h - 66, w - 24, 62), help, smallStyle);
        }
    }
}
