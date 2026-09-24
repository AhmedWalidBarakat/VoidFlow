using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Headless movement test that surfs the map with plain CS technique, never touching W.
    // On a ramp: look along it, hold the key toward the ramp while below the target height,
    // let go to dip (never press away, which brakes you in CS). To transfer, ride off the
    // bottom. In the air: look along your velocity and strafe toward the target, which
    // turns without losing speed.
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
                    while (waypoint < route.Count - 1 && p.z >= route[waypoint].y) waypoint++;
                    Vector2 target = route[waypoint];

                    float heading = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                    float wanted = Mathf.Atan2(target.x - p.x, target.y - p.z) * Mathf.Rad2Deg;
                    float error = Mathf.DeltaAngle(heading, wanted);

                    Vector3 feet = p + Vector3.up * 0.4f;
                    Collider[] touching = player.Grounded ? new Collider[0]
                        : Physics.OverlapSphere(feet, 0.5f, player.collisionMask, QueryTriggerInteraction.Ignore);

                    if (touching.Length > 0)
                    {
                        // On a ramp: look along it and manage height on the face. Hold the key
                        // toward the ramp while below the target line, let go when above it.
                        // A target on the other wall means ride down and slide off to transfer.
                        Vector3 toRamp = touching[0].ClosestPoint(feet) - feet;
                        toRamp.y = 0f;
                        toRamp.Normalize();
                        Vector3 along = Vector3.Cross(Vector3.up, toRamp);
                        if (Vector3.Dot(along, v) < 0f) along = -along;
                        player.Yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;

                        float belowTarget = Vector3.Dot(new Vector3(target.x - p.x, 0f, 0f), toRamp);
                        float rampSide = Mathf.Sign(Vector3.Dot(toRamp, Quaternion.Euler(0f, player.Yaw, 0f) * Vector3.right));
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
    }
}
