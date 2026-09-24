using UnityEngine;

namespace VoidFlow
{
    // Works out where a flight comes down, using the same physics as the movement: in the
    // air you fall under gravity with nothing to change it, and the only control is air
    // strafing, which bends your path sideways (up to AirSteer m/s² of sideways push, a
    // conservative figure for sustained strafing; the raw cap is ~48 m/s² per tick).
    //
    // Used two ways:
    //  - The course checks every new ramp before it appears: a player at a realistic slow
    //    speed must be able to land it. (Faster players can always air-brake down to that
    //    speed, so this proves the gap is possible for everyone.) If not, the ramp is rebuilt
    //    with a shorter gap or smaller shift.
    //  - The surf bot predicts its current flight and air-brakes only when it would overshoot.
    public static class FlightCheck
    {
        public const float AirSteer = 20f;

        public enum Outcome
        {
            Lands,  // comes down onto the ramp's face, within reach sideways
            Short,  // drops below the face before reaching it: into its front or underneath
            Wide,   // comes down at the right height, but too far to the side to strafe to
            Long,   // still above the ramp at the end of the part checked: overshoots
        }

        // Where a flight starting at `start` with `velocity` comes down onto `next`, checking
        // the ramp's first `checkLength` metres. The flight is treated as heading straight on
        // at its current horizontal speed, with sideways strafing reach growing as ½·a·t².
        public static Outcome Predict(Vector3 start, Vector3 velocity, RampShapes.RampPath next, float checkLength)
        {
            Vector3 flat = new(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            if (speed < 1f) return Outcome.Short;
            Vector3 dir = flat / speed;
            bool sawFace = false;

            for (int i = 0; i < next.ridge.Count && next.distance[i] <= checkLength; i++)
            {
                // When do we reach this cross-section of the ramp, and how high are we then?
                float ahead = Vector3.Dot(next.ridge[i] - start, dir);
                if (ahead <= 0f) continue;
                float t = ahead / speed;
                float y = start.y + velocity.y * t - 0.5f * RampShapes.Gravity * t * t;

                float top = next.ridge[i].y, bottom = next.FacePoint(i, 0.95f).y;
                if (y > top) continue; // still above the ridge
                sawFace = true;
                if (y < bottom) return Outcome.Short;

                // We're at face height: find the spot on the face at our height and see
                // whether strafing can carry us there sideways in time
                Vector3 straight = start + dir * ahead;
                float bestReach = float.MaxValue;
                for (int k = 0; k <= 10; k++)
                {
                    float f = 0.05f + 0.9f * k / 10f;
                    Vector3 spot = next.FacePoint(i, f);
                    if (Mathf.Abs(spot.y - y) > (top - bottom) / 10f + 0.3f) continue;
                    Vector3 off = spot - straight;
                    off.y = 0f;
                    off -= dir * Vector3.Dot(off, dir);
                    bestReach = Mathf.Min(bestReach, off.magnitude);
                }
                return bestReach <= 0.5f * AirSteer * t * t ? Outcome.Lands : Outcome.Wide;
            }
            return sawFace ? Outcome.Short : Outcome.Long;
        }

        // Can a player launching off the end of `from` at up to `maxSpeed` (m/s) land on
        // `next`? They may launch from the middle of the face or swing up near the top
        // (launching with extra upward speed), and may be anywhere down to 55% of maxSpeed
        // (air-braking if they were faster). True if any of those lands.
        public static bool Possible(RampShapes.RampPath from, RampShapes.RampPath next, float maxSpeed, float checkLength)
        {
            int end = from.ridge.Count - 1;
            float slope = from.EndSlope;
            var launches = new[]
            {
                (from.FacePoint(end, RampShapes.RideFraction), 0f),
                (from.FacePoint(end, RampShapes.SwingFraction), 4f),
                (from.FacePoint(end, RampShapes.SwingFraction), 8f),
            };
            foreach (var (point, swing) in launches)
            for (int s = 0; s <= 6; s++)
            {
                float speed = maxSpeed * Mathf.Lerp(0.55f, 1f, s / 6f);
                Vector3 velocity = from.EndForward * speed + Vector3.up * (speed * slope + swing);
                if (Predict(point, velocity, next, checkLength) == Outcome.Lands) return true;
            }
            return false;
        }
    }
}
