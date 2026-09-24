using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Headless movement test: walks off the start platform, then faces straight down the
    // map (yaw 0) and only holds the strafe key toward the ramp, the way you surf in CS.
    // Writes a trace to Logs/surfbot.txt so movement changes can be checked without playing.
    public static class SurfBot
    {
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";

        [MenuItem("VoidFlow/Run Surf Bot Test")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();

            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var spawn = GameObject.Find("Spawn").transform;
            Bounds finish = GameObject.Find("FinishZone").GetComponent<BoxCollider>().bounds;

            player.Teleport(spawn.position, 0f);
            float dt = 1f / player.tickRate;
            bool dropped = false;
            float topSpeed = 0f;
            var log = new StringBuilder("time   x       y        z      speed(u/s)  vy     grounded\n");
            string result = "timed out";

            for (int i = 0; i < player.tickRate * 90; i++)
            {
                var input = new PlayerMovement.MoveInput();
                if (!dropped)
                {
                    input.move = new Vector2(0f, 1f);
                    dropped = !player.Grounded && player.Position.y < -0.5f;
                }
                else if (Mathf.Abs(player.Position.x) > 4f)
                {
                    // Low on the face: hold the key toward the ramp to climb back up
                    input.move = new Vector2(player.Position.x < 0f ? 1f : -1f, 0f);
                }

                player.Simulate(input, dt);

                Vector3 p = player.Position, v = player.Velocity;
                float speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;
                topSpeed = Mathf.Max(topSpeed, speed);
                if (i % 64 == 0)
                    log.AppendLine($"{i * dt,5:0.00} {p.x,7:0.0} {p.y,8:0.0} {p.z,7:0.0} {speed,9:0} {v.y,8:0.0}   {player.Grounded}");

                if (p.y < -140f) { result = $"FELL at z={p.z:0} y={p.y:0}"; break; }
                if (finish.Contains(p + Vector3.up * 0.9f)) { result = $"FINISHED in {i * dt:0.00}s"; break; }
            }

            log.AppendLine($"RESULT: {result}, top speed {topSpeed:0} u/s");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/surfbot.txt", log.ToString());
            Debug.Log("SurfBot: " + result);
        }
    }
}
