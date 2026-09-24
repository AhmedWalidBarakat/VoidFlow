using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace VoidFlow.EditorTools
{
    // Headless test of the endless course, surfed the way pros do in the reference videos,
    // using only the mouse and A/D (never W, never the key away from the ramp):
    //
    //  - Swinging: on a ramp it rides up and down the face around the riding line. It lets
    //    go and dips down the face to build speed, then strafes into the ramp to swing back
    //    up. Near the end the line rises toward the top, so the last swing launches it.
    //  - Air strafing: looking along its motion, it strafes toward the next ramp, which turns
    //    it without losing speed. Flying straight, it alternates A/D like a sync strafe.
    //
    //  - Speed control: it knows the speed the next flight was designed for. Far faster than
    //    that in the air, it would overshoot the landing, so it air-brakes with tiny strafe
    //    taps angled a few degrees against its motion, alternating sides so its direction
    //    doesn't change.
    //
    // It surfs TargetRamps ramps on a fixed seed, logging each ramp's move, biome and speed,
    // every fall, and memory and object counts (which must stay flat for the course to run
    // on any machine). Writes Logs/surfbot.txt.
    public static class SurfBot
    {
        const int TargetRamps = 50;
        const int TestSeed = 777;
        const float TurnDeadZone = 2f;  // degrees
        const float SwingBand = 0.9f;   // metres above/below the line each swing reaches
        const int SyncStrafeTicks = 24; // air strafe switches sides this often flying straight
        const float Lookahead = 18f;    // metres ahead along the line the bot aims for
        const float BrakeAngle = 3f;    // degrees against its motion an air-brake tap points

        [MenuItem("VoidFlow/Run Surf Bot Test")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (Application.isBatchMode) GrayboxBuilder.Build();
            EditorSceneManager.OpenScene(GrayboxBuilder.ScenePath);
            Physics.SyncTransforms();

            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var timer = Object.FindAnyObjectByType<RunTimer>();
            var course = Object.FindAnyObjectByType<EndlessCourse>();
            course.seed = TestSeed;
            course.ResetCourse();
            player.Teleport(timer.spawnPoint.position, timer.spawnPoint.eulerAngles.y);

            float dt = 1f / player.tickRate;
            bool dropped = false, swingingUp = false;
            int swings = 0, lastRamp = -1, maxActive = 0, maxObjects = 0, recenters = 0;
            float topSpeed = 0f, rampStartTime = 0f;
            Vector3 lastCheck = player.Position;
            var falls = new List<string>();
            int fallCount = 0;
            var recent = new Queue<string>(); // flight recorder: the last few seconds, printed on a fall
            string phase = "walk";
            var log = new StringBuilder("time    ramp  move                       biome         speed  swings  active  objects\n");
            long memoryStart = Profiler.GetTotalAllocatedMemoryLong();
            string result = "timed out";

            for (int i = 0; i < player.tickRate * 1200; i++)
            {
                float time = i * dt;
                Vector3 before = player.Position;
                course.Step(dt);
                if ((player.Position - before).sqrMagnitude > 1f) { recenters++; lastCheck = player.Position; }

                Vector3 p = player.Position, v = player.Velocity;
                var input = new PlayerMovement.MoveInput();
                float speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;

                if (!dropped)
                {
                    input.move = new Vector2(0f, 1f); // walk off the start hall's drop edge
                    dropped = !player.Grounded && p.y < timer.spawnPoint.position.y - 1f;
                }
                else if (course.TryGetGuide(p, Lookahead, out Vector2 target, out Vector2 along))
                {
                    float heading = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                    float wanted = Mathf.Atan2(target.x - p.x, target.y - p.z) * Mathf.Rad2Deg;
                    float error = Mathf.DeltaAngle(heading, wanted);

                    if (!player.Grounded && FindRamp(player, p, out Vector3 toRamp))
                    {
                        // On a ramp, look along the ramp. The strafe key then points straight
                        // across it: any drift away from the ramp is directly against the key,
                        // and CS cancels that drift hard, which is how pros stay on at speed.
                        player.Yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
                        Vector3 right = Quaternion.Euler(0f, player.Yaw, 0f) * Vector3.right;
                        float rampSide = Mathf.Sign(Vector3.Dot(toRamp, right));
                        float below = Vector3.Dot(new Vector3(target.x - p.x, 0f, target.y - p.z), right * rampSide);

                        if (swingingUp && below < -SwingBand) swingingUp = false;
                        else if (!swingingUp && below > SwingBand) { swingingUp = true; swings++; }
                        if (swingingUp) input.move = new Vector2(rampSide, 0f);
                        phase = $"ramp {(swingingUp ? "up" : "dip")} below={below:0.0}";
                    }
                    else
                    {
                        // In the air, look along our motion and curve toward the next ramp;
                        // flying straight, sync strafe. Too fast for the landing ahead:
                        // air-brake with taps angled just against the motion, alternating
                        // sides every tick so the turning cancels out.
                        // The landing hills handle flights well above their design speed;
                        // only far above it would you fly clean over
                        float needed = course.SpeedNeededAhead(p) * 1.3f;
                        if (speed > needed && Mathf.Abs(error) < 20f)
                        {
                            float side = i % 2 == 0 ? 1f : -1f;
                            player.Yaw = heading + side * BrakeAngle;
                            input.move = new Vector2(side, 0f);
                            phase = $"air brake {speed:0}->{needed:0}";
                        }
                        else
                        {
                            player.Yaw = heading;
                            float side = Mathf.Abs(error) > TurnDeadZone ? Mathf.Sign(error)
                                : (i / SyncStrafeTicks) % 2 == 0 ? 1f : -1f;
                            input.move = new Vector2(side, 0f);
                            phase = $"air err={error:0}";
                        }
                    }
                }

                player.Simulate(input, dt);
                p = player.Position;
                speed = player.HorizontalSpeed / PlayerMovement.SourceUnit;
                topSpeed = Mathf.Max(topSpeed, speed);
                if (i % 16 == 0 && !player.Grounded && phase.StartsWith("air") && recent.Count > 0 && i % 64 == 0)
                    recent.Enqueue("        " + course.DescribeAround(p));
                if (i % 16 == 0)
                {
                    recent.Enqueue($"      {time,6:0.00} p=({p.x:0.0},{p.y:0.0},{p.z:0.0}) v=({player.Velocity.x:0.0},{player.Velocity.y:0.0},{player.Velocity.z:0.0}) {speed:0}u/s {phase}");
                    if (recent.Count > 30) recent.Dequeue();
                }

                if (course.CurrentRamp != lastRamp)
                {
                    int objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
                    maxActive = Mathf.Max(maxActive, course.ActiveRamps);
                    maxObjects = Mathf.Max(maxObjects, objects);
                    lastRamp = course.CurrentRamp;
                    log.AppendLine($"{time,6:0.0}  {lastRamp + 1,4}  {course.CurrentMove,-25}  {course.CurrentBiome.name,-12} {speed,6:0}  {swings,6}  {course.ActiveRamps,6}  {objects,7}");
                    rampStartTime = time;
                    if (lastRamp % course.rampsPerBiome == 2) Photograph(player, $"Logs/biome_{lastRamp / course.rampsPerBiome + 1}_{course.CurrentBiome.name.Replace(' ', '_')}.png");
                    if (lastRamp + 1 >= TargetRamps) { result = $"reached ramp {TargetRamps} in {time:0.0}s"; break; }
                }

                bool stuck = i % 64 == 0 && (p - lastCheck).sqrMagnitude < 0.01f && dropped;
                if (i % 64 == 0) lastCheck = p;
                if (course.IsFallen(p) || stuck || time - rampStartTime > 60f)
                {
                    falls.Add($"ramp {course.CurrentRamp + 1} ({course.CurrentMove}, {course.CurrentBiome.name}) at {speed:0} u/s{(stuck ? " STUCK" : "")}{(time - rampStartTime > 60f ? " TIMEOUT" : "")}");
                    fallCount++;
                    falls.AddRange(recent);
                    recent.Clear();
                    course.RespawnPlayer(player);
                    rampStartTime = time;
                    lastCheck = player.Position;
                    swingingUp = false;
                }
            }

            long memoryEnd = Profiler.GetTotalAllocatedMemoryLong();
            var report = new StringBuilder();
            report.AppendLine($"RESULT: {result}, top speed {topSpeed:0} u/s, {swings} swings, {fallCount} falls, {recenters} recenters");
            report.AppendLine($"Streaming: at most {maxActive} ramps alive, at most {maxObjects} objects in the scene, memory {memoryStart / 1048576}MB -> {memoryEnd / 1048576}MB");
            foreach (var f in falls) report.AppendLine("  fell: " + f);
            report.AppendLine();

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/surfbot.txt", report.ToString() + log);
            Debug.Log("SurfBot:\n" + report);
        }

        // Renders what the player sees right now (looking along their yaw) to a PNG
        static void Photograph(PlayerMovement player, string path)
        {
            player.transform.SetPositionAndRotation(player.Position, Quaternion.Euler(0f, player.Yaw, 0f));
            var cam = player.GetComponentInChildren<Camera>();
            cam.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var rt = new RenderTexture(1280, 720, 24);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
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
