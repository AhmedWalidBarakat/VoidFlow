using UnityEngine;

namespace VoidFlow
{
    // The sounds of your own movement, kept subtle and centered so they never mask what
    // matters: footsteps by surface (the hall's tiles and metal, the ramps' metal, stone or
    // ice depending on the biome), a soft scuff when you jump, a thud when you touch down
    // that grows with how hard you hit, the hiss of surfing a ramp and wind that rises with
    // speed.
    public class PlayerFeedback : MonoBehaviour
    {
        public PlayerMovement player;
        public EndlessCourse course;

        AudioSource oneShots, slide, wind;
        float stepDistance, landCooldown;
        int stepVariant;

        void Start()
        {
            if (!player) player = GetComponent<PlayerMovement>();
            if (!course) course = FindAnyObjectByType<EndlessCourse>();
            oneShots = gameObject.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            oneShots.spatialBlend = 0f;
            slide = Loop(WeaponSounds.SlideLoop);
            wind = Loop(WeaponSounds.WindLoop);
            player.Jumped += OnJump;
            player.Landed += OnLand;
        }

        AudioSource Loop(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 0f;
            source.Play();
            return source;
        }

        void OnDestroy()
        {
            if (!player) return;
            player.Jumped -= OnJump;
            player.Landed -= OnLand;
        }

        // What a collider sounds like: the hall by its parts, the course by its biome
        public static WeaponSounds.Surface SurfaceOf(Collider c, EndlessCourse course)
        {
            if (!c) return WeaponSounds.Surface.Metal;
            if (course && course.startHall && c.transform.IsChildOf(course.startHall))
                return c.name.Contains("Floor") ? WeaponSounds.Surface.Stone : WeaponSounds.Surface.Metal;
            return course ? course.CurrentBiome.name switch
            {
                "GLACIER" => WeaponSounds.Surface.Ice,
                "TEMPLE" or "UTOPIA" => WeaponSounds.Surface.Stone,
                _ => WeaponSounds.Surface.Metal,
            } : WeaponSounds.Surface.Metal;
        }

        void Update()
        {
            if (!player) return;
            float dt = Time.deltaTime, speed = player.HorizontalSpeed;
            landCooldown -= dt;

            // A step every couple of metres walked (not while hopping: landings cover that)
            if (player.Grounded && speed > 1.2f)
            {
                stepDistance += speed * dt;
                if (stepDistance > 2f)
                {
                    stepDistance = 0f;
                    Step(0.22f * Mathf.Clamp01(speed / 6f));
                }
            }
            else stepDistance = 0f;

            // Surfing hisses, harder and higher the faster you go; wind rises in the air
            float slideTarget = player.Surfing ? Mathf.Lerp(0.08f, 0.32f, Mathf.Clamp01(speed / 50f)) : 0f;
            slide.volume = Mathf.MoveTowards(slide.volume, slideTarget, dt * (slideTarget > slide.volume ? 2.5f : 1.2f));
            slide.pitch = 0.75f + Mathf.Clamp01(speed / 70f) * 0.6f;
            float windTarget = Mathf.Clamp01((speed - 12f) / 60f) * (player.Grounded ? 0.04f : 0.2f);
            wind.volume = Mathf.Lerp(wind.volume, windTarget, 1f - Mathf.Exp(-3f * dt));
            wind.pitch = 0.7f + Mathf.Clamp01(speed / 80f) * 0.5f;
        }

        void Step(float volume)
        {
            var surface = SurfaceOf(player.Surface, course);
            oneShots.pitch = Random.Range(0.93f, 1.07f);
            oneShots.panStereo = (stepVariant & 1) == 0 ? -0.08f : 0.08f; // left, right
            oneShots.PlayOneShot(WeaponSounds.Step(surface, stepVariant++), volume);
        }

        void OnJump()
        {
            oneShots.pitch = Random.Range(0.94f, 1.06f);
            oneShots.panStereo = 0f;
            oneShots.PlayOneShot(WeaponSounds.Jump, 0.14f);
        }

        void OnLand(float hit)
        {
            if (landCooldown > 0f) return;
            landCooldown = 0.1f;
            float k = Mathf.Clamp01((hit - 2f) / 14f);
            if (k < 0.05f) { Step(0.15f); return; } // a bhop landing is just a step
            var surface = SurfaceOf(player.Surface, course);
            oneShots.pitch = Random.Range(0.95f, 1.05f);
            oneShots.panStereo = 0f;
            oneShots.PlayOneShot(WeaponSounds.Land(surface), Mathf.Lerp(0.18f, 0.6f, k));
        }
    }
}
