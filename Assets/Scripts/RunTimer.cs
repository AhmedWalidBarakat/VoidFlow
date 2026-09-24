using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // Runs the endless mode and draws the HUD. The run starts when you leave the start
    // hall. Fall off and you're put back on the ramp you were on (it counts as a fall);
    // R restarts from the hall with a fresh course. Tracks distance, time, and your best
    // distance, and announces each biome as you enter it.
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
        float startTime, best;
        int falls;
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
            falls = 0;
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
                falls++;
                course.RespawnPlayer(player);
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
                    $"ramp {course.CurrentRamp + 1}   ·   level {course.Level + 1}   ·   {course.CurrentBiome.name}   ·   {Format(Time.time - startTime)}   ·   falls {falls}",
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

            string help = Cursor.lockState == CursorLockMode.Locked
                ? "WASD move · Space jump (hold to bhop) · R restart · Esc release mouse · F inspect knife\nOn ramps: let go of W, hold A or D toward the ramp, and steer with the mouse"
                : "Click to capture the mouse";
            GUI.Label(new Rect(12, h - 48, w - 24, 44), help, smallStyle);
        }
    }
}
