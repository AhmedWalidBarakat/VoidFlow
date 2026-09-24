using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Headless movement tests. Each scenario walks off the start platform, faces straight
    // down the map holding W, and does something at 7s (mid ramp 1, ~1000 u/s): taps a
    // wrong key, jumps, etc. Writes Logs/surfbot.txt so movement changes can be checked
    // without playing.
    public static class SurfBot
    {
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";
        const float EventTime = 7f;

        // The bot surfs the left face of each ramp, so the ramp is to its right (+x)
        static readonly (string name, Action<PlayerMovement> setup, Func<float, PlayerMovement.MoveInput> during, float yaw, float length)[] Scenarios =
        {
            ("hold W the whole way", null, t => Hold(0f, 1f), 0f, 0.3f),
            ("tap S mid ramp", null, t => Hold(0f, -1f), 0f, 0.3f),
            ("same, pure CS braking", p => p.airBrakeLimit = 0f, t => Hold(0f, -1f), 0f, 0.3f),
            ("look 30 left + tap A", null, t => Hold(-1f, 0f), -30f, 0.3f),
            ("same, pure CS braking", p => p.airBrakeLimit = 0f, t => Hold(-1f, 0f), -30f, 0.3f),
            ("jump on ramp", null, t => new PlayerMovement.MoveInput { move = new Vector2(0f, 1f), jumpPressed = t < 0.01f, jumpHeld = true }, 0f, 0.3f),
            ("slide to bottom of ramp", null, t => Hold(-1f, 0f), 0f, 1.2f),
        };

        static PlayerMovement.MoveInput Hold(float x, float y) => new() { move = new Vector2(x, y) };

        [MenuItem("VoidFlow/Run Surf Bot Test")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();

            var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            var spawn = GameObject.Find("Spawn").transform;
            Bounds finish = GameObject.Find("FinishZone").GetComponent<BoxCollider>().bounds;
            float defaultBrake = player.airBrakeLimit;
            float killHeight = UnityEngine.Object.FindAnyObjectByType<RunTimer>().killHeight;

            var report = new StringBuilder();
            var trace = new StringBuilder();

            foreach (var s in Scenarios)
            {
                player.airBrakeLimit = defaultBrake;
                s.setup?.Invoke(player);
                player.Teleport(spawn.position, 0f);

                float dt = 1f / player.tickRate;
                bool dropped = false;
                float topSpeed = 0f, before = 0f, lowestAfter = float.MaxValue;
                string result = "timed out";
                trace.AppendLine($"== {s.name}");

                for (int i = 0; i < player.tickRate * 60; i++)
                {
                    float t = i * dt;
                    var input = Hold(0f, 1f);
                    player.Yaw = 0f;
                    if (dropped && t >= EventTime && t < EventTime + s.length)
                    {
                        input = s.during(t - EventTime);
                        player.Yaw = s.yaw;
                    }
                    if (!dropped) dropped = !player.Grounded && player.Position.y < -0.5f;

                    player.Simulate(input, dt);

                    Vector3 p = player.Position;
                    float speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;
                    topSpeed = Mathf.Max(topSpeed, speed);
                    if (t < EventTime) before = speed;
                    else if (t < EventTime + 1f) lowestAfter = Mathf.Min(lowestAfter, speed);
                    if (i % 64 == 0)
                        trace.AppendLine($"{t,5:0.0} x{p.x,6:0.0} y{p.y,7:0.0} z{p.z,6:0} {speed,5:0}u/s vy{player.Velocity.y,6:0.0}");

                    if (p.y < killHeight) { result = $"FELL at z={p.z:0}"; break; }
                    if (finish.Contains(p + Vector3.up * 0.9f)) { result = $"finished {t:0.00}s"; break; }
                }

                report.AppendLine($"{s.name,-26} {result,-16} speed at 7s {before,5:0} -> lowest next 1s {lowestAfter,5:0}   top {topSpeed,5:0}");
            }

            player.airBrakeLimit = defaultBrake;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/surfbot.txt", report + "\n" + trace);
            Debug.Log("SurfBot:\n" + report);
        }
    }
}
