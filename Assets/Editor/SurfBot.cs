using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Headless movement test that surfs the map the way pros do in the reference videos,
    // using only the mouse and A/D (never W, never the key away from the ramp):
    //
    //  - Swinging: on a ramp it rides up and down the face around a line along the ramp.
    //    It lets go and dips down the face to build speed, then strafes into the ramp,
    //    turning with it, to swing back up. Repeat.
    //  - Launch: over the last stretch of a ramp the line moves up toward the peak, so the
    //    final swing carries it up and off the end into a long flight.
    //  - Air strafing: looking along its motion, it strafes toward the next ramp, which turns
    //    it without losing speed. Flying straight, it alternates A/D like a sync strafe.
    //
    // Writes Logs/surfbot.txt so map or movement changes can be checked without playing.
    public static class SurfBot
    {
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";
        const float TurnDeadZone = 2f;  // degrees
        const float SwingBand = 2.5f;   // metres above/below the line each swing reaches
        const int SyncStrafeTicks = 24; // air strafe switches sides this often flying straight

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
            bool dropped = false, swingingUp = false;
            int waypoint = 0, swings = 0;
            float topSpeed = 0f;
            string result = "timed out", phase = "walk";
            var log = new StringBuilder("time     x       y       z   speed(u/s)  phase\n");

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
                        // On a ramp, look along the ramp. The strafe key then points straight
                        // across it: any drift away from the ramp is directly against the key,
                        // and CS cancels that drift hard, which is how pros stay on at speed.
                        Vector2 along = route[Mathf.Min(waypoint + 1, route.Count - 1)] - route[waypoint];
                        player.Yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
                        Vector3 right = Quaternion.Euler(0f, player.Yaw, 0f) * Vector3.right;

                        // Swing around the line: how far below it are we (toward the bottom
                        // of the face is "below")?
                        float rampSide = Mathf.Sign(Vector3.Dot(toRamp, right));
                        float below = Vector3.Dot(new Vector3(target.x - p.x, 0f, target.y - p.z), right * rampSide);

                        if (swingingUp && below < -SwingBand) swingingUp = false;
                        else if (!swingingUp && below > SwingBand) { swingingUp = true; swings++; }

                        if (swingingUp) input.move = new Vector2(rampSide, 0f);
                        phase = swingingUp ? "ramp: swing up" : "ramp: dip";
                    }
                    else
                    {
                        // In the air, look along our motion: the strafe keys then push exactly
                        // sideways to it, which turns us without braking. Curve toward the next
                        // ramp; flying straight, sync strafe.
                        player.Yaw = heading;
                        float side = Mathf.Abs(error) > TurnDeadZone ? Mathf.Sign(error)
                            : (i / SyncStrafeTicks) % 2 == 0 ? 1f : -1f;
                        input.move = new Vector2(side, 0f);
                        phase = "air strafe";
                    }
                }

                player.Simulate(input, dt);

                p = player.Position;
                float speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;
                topSpeed = Mathf.Max(topSpeed, speed);
                if (i % 32 == 0)
                    log.AppendLine($"{i * dt,5:0.0} {p.x,7:0.0} {p.y,7:0.0} {p.z,7:0.0} {speed,8:0}    {phase}");

                if (p.y < timer.killHeight) { result = $"FELL at z={p.z:0}"; break; }
                if (finish.Contains(p + Vector3.up * 0.9f)) { result = $"FINISHED in {i * dt:0.00}s"; break; }
            }

            log.Insert(0, $"RESULT: {result}, top speed {topSpeed:0} u/s, {swings} swings\n\n");
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
