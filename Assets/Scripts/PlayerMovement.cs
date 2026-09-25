using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // Source-style movement: ground friction, air strafing and ramp sliding.
    // Defaults are CS:GO surf-server values converted from Source units (1u = 0.0254m).
    // Simulation runs on a fixed 64 tick like CS2; the transform is interpolated between ticks.
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerMovement : MonoBehaviour
    {
        public const float SourceUnit = 0.0254f;

        [Header("Look")]
        public Transform cameraPivot;
        public float sensitivity = 0.08f;

        [Header("Movement")]
        public float gravity = 800f * SourceUnit;
        public float jumpSpeed = 301.993f * SourceUnit;
        public float maxSpeed = 250f * SourceUnit; // knife speed
        public float accelerate = 5.5f;
        public float airAccelerate = 150f;
        public float airWishCap = 30f * SourceUnit;
        public float friction = 5.2f;
        public float stopSpeed = 80f * SourceUnit;
        public float maxVelocity = 3500f * SourceUnit;
        public bool autoHop = true;

        [Header("Collision")]
        [Tooltip("Surfaces whose normal.y is below this are too steep to stand on, so they become surf ramps.")]
        public float groundNormalY = 0.7f;
        public LayerMask collisionMask = ~(1 << 2);
        public float tickRate = 64f; // CS2 servers run 64 tick

        const float Skin = 0.015f;
        const float GroundProbe = 0.06f;
        const float MaxGroundedRiseSpeed = 140f * SourceUnit;
        const int MaxSlides = 4;

        CapsuleCollider capsule;
        Vector3 position, prevPosition, velocity;
        bool grounded, jumpQueued;
        float yaw, pitch, accumulator;
        readonly Collider[] overlaps = new Collider[16];
        readonly Vector3[] planes = new Vector3[MaxSlides];

        public struct MoveInput
        {
            public Vector2 move; // x = strafe (D positive), y = forward (W positive)
            public bool jumpHeld;
            public bool jumpPressed;
        }

        public Vector3 Position => position;
        public Vector3 Velocity => velocity;
        public bool Grounded => grounded;
        public float HorizontalSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;
        public float Yaw { get => yaw; set => yaw = value; }

        void Awake() => Init();

        void Init()
        {
            if (capsule) return;
            capsule = GetComponent<CapsuleCollider>();
            gameObject.layer = 2; // Ignore Raycast, so our own casts never hit the player
            position = prevPosition = transform.position;
            yaw = transform.eulerAngles.y;
        }

        // Advances one tick with the given input. Update() drives this from the keyboard;
        // editor tools call it directly to test movement without playing.
        public void Simulate(MoveInput input, float dt)
        {
            Init();
            prevPosition = position;
            Tick(input, dt);
        }

        public void Teleport(Vector3 pos, float newYaw)
        {
            Init();
            position = prevPosition = pos;
            velocity = Vector3.zero;
            yaw = newYaw;
            pitch = 0f;
            accumulator = 0f;
            transform.position = pos;
        }

        // Teleport keeping a given velocity, e.g. a respawn that should keep you moving
        public void Teleport(Vector3 pos, float newYaw, Vector3 newVelocity)
        {
            Teleport(pos, newYaw);
            velocity = newVelocity;
        }

        // Moves the player with the world when it's recentered, keeping all motion
        public void ShiftOrigin(Vector3 delta)
        {
            position += delta;
            prevPosition += delta;
            transform.position += delta;
        }

        public void LimitHorizontalSpeed(float max)
        {
            var h = new Vector3(velocity.x, 0f, velocity.z);
            if (h.magnitude <= max) return;
            h = h.normalized * max;
            velocity = new Vector3(h.x, velocity.y, h.z);
        }

        void Update()
        {
            Look();

            var kb = Inventory.IsOpen ? null : Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) jumpQueued = true;

            var input = new MoveInput
            {
                move = ReadMoveInput(kb),
                jumpHeld = kb != null && kb.spaceKey.isPressed,
            };

            float dt = 1f / tickRate;
            accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            while (accumulator >= dt)
            {
                input.jumpPressed = jumpQueued;
                jumpQueued = false;
                Simulate(input, dt);
                accumulator -= dt;
            }

            transform.SetPositionAndRotation(
                Vector3.Lerp(prevPosition, position, accumulator / dt),
                Quaternion.Euler(0f, yaw, 0f));
            if (cameraPivot) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void Look()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || Inventory.IsOpen) return;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Vector2 delta = mouse.delta.ReadValue() * sensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
        }

        void Tick(MoveInput input, float dt)
        {
            Vector3 wish = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.move.x, 0f, input.move.y);
            float wishSpeed = wish.magnitude * maxSpeed;
            Vector3 wishDir = wish.sqrMagnitude > 0f ? wish.normalized : Vector3.zero;

            CategorizePosition();

            bool wantJump = input.jumpPressed || (autoHop && input.jumpHeld);
            if (grounded && wantJump)
            {
                // Jumping before friction is what makes bhop keep its speed
                velocity.y = jumpSpeed;
                grounded = false;
            }

            if (grounded)
            {
                velocity.y = 0f;
                ApplyFriction(dt);
                Accelerate(wishDir, wishSpeed, dt);
            }
            else
            {
                AirAccelerate(wishDir, wishSpeed, dt);
                velocity.y -= gravity * dt;
            }

            velocity = Vector3.ClampMagnitude(velocity, maxVelocity);

            SlideMove(dt);
            Depenetrate();
            CategorizePosition();
            if (grounded && velocity.y < 0f) velocity.y = 0f;
        }

        static Vector2 ReadMoveInput(Keyboard kb)
        {
            if (kb == null) return Vector2.zero;
            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        void ApplyFriction(float dt)
        {
            float speed = velocity.magnitude;
            if (speed < 0.001f)
            {
                velocity = Vector3.zero;
                return;
            }
            float control = Mathf.Max(speed, stopSpeed);
            float newSpeed = Mathf.Max(speed - control * friction * dt, 0f);
            velocity *= newSpeed / speed;
        }

        void Accelerate(Vector3 wishDir, float wishSpeed, float dt)
        {
            float add = wishSpeed - Vector3.Dot(velocity, wishDir);
            if (add <= 0f) return;
            velocity += wishDir * Mathf.Min(accelerate * wishSpeed * dt, add);
        }

        // Only the projection onto wishDir is capped, so strafing sideways while
        // turning keeps adding speed. This is the whole trick behind air strafing.
        void AirAccelerate(Vector3 wishDir, float wishSpeed, float dt)
        {
            float add = Mathf.Min(wishSpeed, airWishCap) - Vector3.Dot(velocity, wishDir);
            if (add <= 0f) return;
            velocity += wishDir * Mathf.Min(airAccelerate * wishSpeed * dt, add);
        }

        void CategorizePosition()
        {
            grounded = false;
            if (velocity.y > MaxGroundedRiseSpeed) return;
            if (!Cast(position, Vector3.down, GroundProbe + Skin, out RaycastHit hit)) return;
            if (SurfaceNormal(hit).y < groundNormalY) return; // too steep: this is a surf ramp

            grounded = true;
            position.y -= Mathf.Max(hit.distance - Skin, 0f);
        }

        // Collide-and-slide, like Source's TryPlayerMove.
        void SlideMove(float dt)
        {
            Vector3 original = velocity;
            float timeLeft = dt;
            int planeCount = 0;

            for (int i = 0; i < MaxSlides && timeLeft > 0f; i++)
            {
                Vector3 move = velocity * timeLeft;
                float dist = move.magnitude;
                if (dist < 1e-5f) break;
                Vector3 dir = move / dist;

                if (!Cast(position, dir, dist + Skin, out RaycastHit hit))
                {
                    position += move;
                    break;
                }

                // Grazing a surface we're already moving away from (common on curved ramps):
                // the sweep still reports it, but it isn't blocking. Step off it and carry on,
                // otherwise we'd sit still every tick with full speed.
                if (Vector3.Dot(dir, hit.normal) > -0.001f && hit.distance < Skin * 2f)
                {
                    position += hit.normal * Skin;
                    continue;
                }

                float travel = Mathf.Max(hit.distance - Skin, 0f);
                position += dir * travel;
                timeLeft -= timeLeft * (travel / dist);

                Vector3 n = hit.normal;
                if (planeCount < planes.Length) planes[planeCount++] = n;
                velocity = Clip(velocity, n);

                // Wedged between two planes: slide along the seam between them
                for (int p = 0; p < planeCount - 1; p++)
                {
                    // Neighbouring triangles of a curved ramp are nearly the same plane, not
                    // a corner; treating them as one would stop you dead (a "ramp bug")
                    if (Vector3.Dot(planes[p], n) > 0.99f) continue;
                    if (Vector3.Dot(velocity, planes[p]) >= 0f) continue;
                    Vector3 crease = Vector3.Cross(planes[p], n);
                    velocity = crease.sqrMagnitude < 1e-6f
                        ? Vector3.zero
                        : crease.normalized * Vector3.Dot(crease.normalized, velocity);
                    break;
                }

                if (planeCount > 1 && Vector3.Dot(velocity, original) <= 0f)
                {
                    velocity = Vector3.zero;
                    break;
                }
            }
        }

        void Depenetrate()
        {
            GetCapsulePoints(position, out Vector3 a, out Vector3 b);
            int count = Physics.OverlapCapsuleNonAlloc(a, b, capsule.radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (other == capsule) continue;
                if (Physics.ComputePenetration(capsule, position, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float depth))
                {
                    position += dir * (depth + Skin * 0.5f);
                    velocity = Clip(velocity, dir);
                }
            }
        }

        // A capsule resting on a sharp edge (like the peak of a surf ramp) reports a rounded,
        // nearly vertical contact normal, which would count as floor. Raycast straight down
        // at the contact point to get the normal of the face actually underneath instead.
        Vector3 SurfaceNormal(RaycastHit hit)
        {
            const float probe = 0.05f;
            return Physics.Raycast(hit.point + Vector3.up * probe, Vector3.down, out RaycastHit face,
                probe * 2f, collisionMask, QueryTriggerInteraction.Ignore)
                ? face.normal
                : hit.normal;
        }

        static Vector3 Clip(Vector3 v, Vector3 normal)
        {
            float backoff = Vector3.Dot(v, normal);
            return backoff >= 0f ? v : v - normal * backoff;
        }

        bool Cast(Vector3 pos, Vector3 dir, float dist, out RaycastHit hit)
        {
            GetCapsulePoints(pos, out Vector3 a, out Vector3 b);
            return Physics.CapsuleCast(a, b, capsule.radius, dir, out hit, dist, collisionMask, QueryTriggerInteraction.Ignore);
        }

        void GetCapsulePoints(Vector3 pos, out Vector3 top, out Vector3 bottom)
        {
            Vector3 center = pos + capsule.center;
            float half = Mathf.Max(capsule.height * 0.5f - capsule.radius, 0f);
            top = center + Vector3.up * half;
            bottom = center - Vector3.up * half;
        }
    }
}
