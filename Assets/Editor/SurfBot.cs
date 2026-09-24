using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Headless movement test that surfs the map with plain CS technique, never touching W.
    // It follows a line along the left face of every ramp. On a ramp: look along your
    // motion, hold the strafe key on the ramp's side while below the line, let go to dip
    // (never press away, which brakes you in CS). In the air: look along your velocity and
    // strafe toward the line, which turns without losing speed.
    // Writes Logs/surfbot.txt so map or movement changes can be checked without playing.
    public static class SurfBot
    {
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";
        const float TurnDeadZone = 1.5f; // degrees

        [MenuItem("VoidFlow/Run Surf Bot Test")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (GrayboxBuilder.BotRoute.Count == 0) GrayboxBuilder.Build(); // route is filled in by the builder
            EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();

            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var timer = Object.FindAnyObjectByType<RunTimer>();
            Bounds finish = timer.endZone.bounds;
            var route = GrayboxBuilder.BotRoute;

            player.Teleport(timer.spawnPoint.position, 0f);
            float dt = 1f / player.tickRate;
            bool dropped = false;
            int waypoint = 0;
            float topSpeed = 0f;
            string result = "timed out";
            var log = new StringBuilder("time     x       y       z   speed(u/s)  target\n");

            for (int i = 0; i < player.tickRate * 60; i++)
            {
                Vector3 p = player.Position, v = player.Velocity;
                var input = new PlayerMovement.MoveInput();

                if (!dropped)
                {
                    input.move = new Vector2(0f, 1f); // walk off the start platform
                    dropped = !player.Grounded && p.y < timer.spawnPoint.position.y - 1f;
                }
                else
                {
                    // Advance past route points we've gone by, then aim a few points ahead
                    var flat = new Vector2(p.x, p.z);
                    while (waypoint < route.Count - 1 && Vector2.Dot(flat - route[waypoint], route[waypoint + 1] - route[waypoint]) > 0f)
                        waypoint++;
                    Vector2 target = route[Mathf.Min(waypoint + 3, route.Count - 1)];

                    float heading = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                    float wanted = Mathf.Atan2(target.x - p.x, target.y - p.z) * Mathf.Rad2Deg;
                    float error = Mathf.DeltaAngle(heading, wanted);

                    if (!player.Grounded && FindRamp(player, p, out Vector3 toRamp))
                    {
                        // On a ramp: look along your motion and manage height on the face.
                        // Hold the strafe key on the ramp's side while below the target line,
                        // let go when above it. The key is exactly sideways to your motion, so
                        // it never brakes you (on a downhill ramp, "straight at the ramp" does).
                        player.Yaw = heading;
                        Vector3 right = Quaternion.Euler(0f, heading, 0f) * Vector3.right;
                        float rampSide = Mathf.Sign(Vector3.Dot(toRamp, right));
                        float belowTarget = Vector3.Dot(new Vector3(target.x - p.x, 0f, target.y - p.z), right * rampSide);
                        if (belowTarget > 0.5f) input.move = new Vector2(rampSide, 0f);
                    }
                    else
                    {
                        // In the air: look along velocity and strafe toward the target
                        player.Yaw = heading;
                        if (Mathf.Abs(error) > TurnDeadZone) input.move = new Vector2(Mathf.Sign(error), 0f);
                    }
                }

                player.Simulate(input, dt);

                p = player.Position;
                float speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;
                topSpeed = Mathf.Max(topSpeed, speed);
                if (i % 32 == 0)
                    log.AppendLine($"{i * dt,5:0.0} {p.x,7:0.0} {p.y,7:0.0} {p.z,7:0.0} {speed,8:0}      #{waypoint}");

                if (p.y < timer.killHeight) { result = $"FELL at z={p.z:0} heading for point #{waypoint}"; break; }
                if (finish.Contains(p + Vector3.up * 0.9f)) { result = $"FINISHED in {i * dt:0.00}s"; break; }
            }

            log.Insert(0, $"RESULT: {result}, top speed {topSpeed:0} u/s\n\n");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/surfbot.txt", log.ToString());
            Debug.Log("SurfBot: " + result);
        }

        // Are we touching a surf ramp, and which way (horizontally) is it? Fans short rays
        // out and down from the body; ClosestPoint can't be used on curved mesh colliders.
        static bool FindRamp(PlayerMovement player, Vector3 p, out Vector3 toRamp)
        {
            toRamp = Vector3.zero;
            Vector3 center = p + Vector3.up * 0.9f;
            float best = 1.4f;
            for (int a = 0; a < 16; a++)
            {
                Vector3 dir = Quaternion.Euler(0f, a * 22.5f, 0f) * new Vector3(0f, -1f, 1f).normalized;
                if (!Physics.Raycast(center, dir, out RaycastHit hit, best, player.collisionMask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y > 0.7f) continue; // floor, not a ramp
                best = hit.distance;
                toRamp = new Vector3(-hit.normal.x, 0f, -hit.normal.z).normalized;
            }
            return toRamp != Vector3.zero;
        }
    }
}
