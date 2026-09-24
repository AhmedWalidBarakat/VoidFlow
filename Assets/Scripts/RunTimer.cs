using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace VoidFlow
{
    // Start zone -> end zone timer with a best time saved per map, plus the HUD.
    public class RunTimer : MonoBehaviour
    {
        public PlayerMovement player;
        public BoxCollider startZone;
        public BoxCollider endZone;
        public Transform spawnPoint;
        public float killHeight = -200f;
        [Tooltip("Stops players building speed inside the start zone (Source units per second).")]
        public float startZoneSpeedCap = 350f;

        enum State { InStart, Running, Finished }

        State state;
        float startTime, finalTime, best;
        bool newBest;
        string bestKey;
        GUIStyle bigStyle, smallStyle;

        void Start()
        {
            bestKey = "VoidFlow.best." + SceneManager.GetActiveScene().name;
            best = PlayerPrefs.GetFloat(bestKey, 0f);
            Respawn();
        }

        void Respawn()
        {
            player.Teleport(spawnPoint.position, spawnPoint.eulerAngles.y);
            state = State.InStart;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) { Respawn(); return; }

            Vector3 p = player.Position;
            if (p.y < killHeight) { Respawn(); return; }

            Vector3 body = p + Vector3.up * 0.9f;
            bool inStart = startZone.bounds.Contains(body);

            if (inStart)
            {
                state = State.InStart;
                player.LimitHorizontalSpeed(startZoneSpeedCap * PlayerMovement.SourceUnit);
            }
            else if (state == State.InStart)
            {
                state = State.Running;
                startTime = Time.time;
                newBest = false;
            }
            else if (state == State.Running && endZone.bounds.Contains(body))
            {
                state = State.Finished;
                finalTime = Time.time - startTime;
                if (best <= 0f || finalTime < best)
                {
                    best = finalTime;
                    newBest = true;
                    PlayerPrefs.SetFloat(bestKey, best);
                    PlayerPrefs.Save();
                }
            }
        }

        static string Format(float t) => $"{(int)(t / 60f)}:{t % 60f:00.000}";

        void OnGUI()
        {
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            }

            float w = Screen.width, h = Screen.height;

            string timer = state switch
            {
                State.Running => Format(Time.time - startTime),
                State.Finished => Format(finalTime) + (newBest ? "  NEW BEST" : ""),
                _ => "Leave the start zone to begin",
            };
            GUI.Label(new Rect(0, 20, w, 40), timer, bigStyle);

            float speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;
            GUI.Label(new Rect(0, h - 90, w, 40), $"{speed:0} u/s", bigStyle);

            if (best > 0f) GUI.Label(new Rect(w - 200, 20, 190, 24), "Best  " + Format(best), smallStyle);

            string help = Cursor.lockState == CursorLockMode.Locked
                ? "WASD move · Space jump (hold to bhop) · R restart · Esc release mouse\nOn ramps: let go of W, hold A or D toward the ramp, and steer with the mouse"
                : "Click to capture the mouse";
            GUI.Label(new Rect(12, h - 48, w - 24, 44), help, smallStyle);
        }
    }
}
