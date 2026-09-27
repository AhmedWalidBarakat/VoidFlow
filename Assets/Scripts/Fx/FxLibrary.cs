using UnityEngine;

namespace VoidFlow
{
    // All the particle effects, built from CC0 sprites (Kenney's Particle Pack, Assets/Fx):
    //  - ambient particles drifting around you: a look per biome (snow on the glacier, embers
    //    in the inferno, rising bubbles in the abyss...) and purple magic in the start hall
    //  - speed streaks rushing past when you surf fast
    //  - one-shot bursts: bullet impacts, skeet hits and misses, muzzle smoke, case reveals,
    //    and gentle sparkles rising off the hall displays
    // Everything is Unity's own particle system with small particle counts, so it stays cheap
    // enough for WebGL.
    public class FxLibrary : MonoBehaviour
    {
        public static FxLibrary Instance { get; private set; }

        [Header("Particle materials (URP Particles/Unlit)")]
        public Material spark, flare, star, smoke, dirt, magic, mote, ring, streak, muzzle, scorch;

        [Header("What the effects follow")]
        public PlayerMovement player;
        public EndlessCourse course;
        public RunTimer timer;

        ParticleSystem sparks, flares, stars, smokes, dirts, rings, ambient, ambientGlow, speedLines;
        string profile;
        Transform view;

        void Awake()
        {
            Instance = this;
            sparks = Make("Sparks", spark, 200, gravity: 1.4f, stretch: true);
            flares = Make("Flares", flare, 40);
            stars = Make("Stars", star, 120, gravity: 0.3f);
            smokes = Make("Smoke", smoke, 80, gravity: -0.05f);
            dirts = Make("Dirt", dirt, 40, gravity: 0.6f);
            rings = Make("Rings", ring, 20);
            var ringSize = rings.sizeOverLifetime;
            ringSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 2.5f)));
            ambient = Make("Ambient", mote, 220);
            ambientGlow = Make("Ambient Glow", magic, 60);
            view = player ? player.cameraPivot : null;

            // Speed streaks live in the camera's own space and rush toward you
            if (view)
            {
                speedLines = Make("Speed Lines", streak, 120, stretch: true, local: true);
                speedLines.transform.SetParent(view, false);
                var r = speedLines.GetComponent<ParticleSystemRenderer>();
                r.velocityScale = 0.035f;
                r.lengthScale = 1f;
            }
        }

        // For editor previews: set everything up outside play mode and show one look
        public void BuildNow() { if (!ambient) Awake(); }
        public void ForceLook(string name)
        {
            profile = name;
            var look = LookFor(name);
            Apply(ambient, look.mat, look.a, look.b, look.size, look.rate, look.gravity, look.drift, look.life);
            Apply(ambientGlow, look.glowMat, look.glow, Color.Lerp(look.glow, Color.white, 0.4f), look.size * 1.6f, look.glowRate, look.gravity, look.drift, look.life);
        }
        public void Advance(float seconds, bool restartAmbient)
        {
            foreach (var ps in new[] { sparks, flares, stars, smokes, dirts })
                if (ps) ps.Simulate(seconds, true, false);
            if (restartAmbient)
            {
                ambient.Simulate(seconds, true, true);
                ambientGlow.Simulate(seconds, true, true);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static ParticleSystem MakeStatic(Transform parent, string name, Material mat, int max, float gravity = 0f, bool stretch = false, bool local = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            // Fade in quickly, fade out slowly; shrink toward the end
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.6f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = 0.06f;
            r.lengthScale = 1.2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.6f;
            ps.Play();
            return ps;
        }

        ParticleSystem Make(string name, Material mat, int max, float gravity = 0f, bool stretch = false, bool local = false) =>
            MakeStatic(transform, name, mat, max, gravity, stretch, local);

        static void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, float size, Color color, float life, float spin = 0f)
        {
            if (!ps) return;
            var p = new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startSize = size, startColor = color, startLifetime = life,
                rotation = Random.Range(0f, 360f), angularVelocity = spin,
            };
            ps.Emit(p, 1);
        }

        // ------------------------------------------------------------------ one-shot bursts

        // A bullet hitting a surface: hot sparks, a flash and a little smoke
        public static void Impact(Vector3 at, Vector3 normal)
        {
            var fx = Instance;
            if (!fx) return;
            for (int i = 0; i < 14; i++)
                Emit(fx.sparks, at, (normal + Random.insideUnitSphere * 0.9f).normalized * Random.Range(4f, 11f), Random.Range(0.04f, 0.08f),
                    new Color(1f, Random.Range(0.55f, 0.85f), 0.3f), Random.Range(0.2f, 0.45f));
            Emit(fx.flares, at + normal * 0.05f, Vector3.zero, 0.9f, new Color(1f, 0.8f, 0.5f), 0.12f);
            for (int i = 0; i < 3; i++)
                Emit(fx.smokes, at + normal * 0.1f, normal * Random.Range(0.3f, 0.8f) + Random.insideUnitSphere * 0.3f, Random.Range(0.4f, 0.7f),
                    new Color(0.55f, 0.55f, 0.58f, 0.6f), Random.Range(0.8f, 1.4f), Random.Range(-30f, 30f));
        }

        // A skeet disc shattering: bright shards, stars and a flash in its color
        public static void Shatter(Vector3 at, Color color)
        {
            var fx = Instance;
            if (!fx) return;
            for (int i = 0; i < 18; i++)
                Emit(fx.sparks, at, Random.onUnitSphere * Random.Range(4f, 9f) + Vector3.up * 2f, Random.Range(0.06f, 0.12f), color, Random.Range(0.35f, 0.7f));
            for (int i = 0; i < 8; i++)
                Emit(fx.stars, at, Random.onUnitSphere * Random.Range(1f, 3f), Random.Range(0.25f, 0.45f), Color.Lerp(color, Color.white, 0.5f), Random.Range(0.4f, 0.8f), Random.Range(-180f, 180f));
            Emit(fx.flares, at, Vector3.zero, 2.2f, color, 0.18f);
            Emit(fx.smokes, at, Vector3.up * 0.4f, 1.1f, new Color(0.5f, 0.45f, 0.45f, 0.5f), 1.2f);
        }

        // One glittering spark flung from a knife swing
        public static void Sparkle(Vector3 at, Vector3 velocity, Color color)
        {
            var fx = Instance;
            if (!fx) return;
            Emit(fx.stars, at, velocity, Random.Range(0.08f, 0.16f), Color.Lerp(color, Color.white, 0.3f), Random.Range(0.25f, 0.5f), Random.Range(-360f, 360f));
            Emit(fx.sparks, at, velocity * 1.3f, Random.Range(0.03f, 0.05f), color, Random.Range(0.15f, 0.3f));
        }

        // A sniper round's wake: a line of glowing embers and wispy smoke left hanging in the
        // air along its path, a ring of pressure at the muzzle, and a bright spark at the hit
        public enum ShotStyle { Normal, Glow, Beam, Fire }

        public static void BulletTrail(Vector3 from, Vector3 to, bool hit) => BulletTrail(from, to, hit, ShotStyle.Normal, Color.white);

        // Void rounds: Glow leaves colored embers, Beam leaves a crackling energy line with
        // rings along it, Fire leaves a trail of flame puffs and embers
        public static void BulletTrail(Vector3 from, Vector3 to, bool hit, ShotStyle style, Color tint)
        {
            if (style == ShotStyle.Normal) { NormalTrail(from, to, hit); return; }
            var fx = Instance;
            if (!fx) return;
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.1f) return;
            Vector3 dir = d / length;
            int steps = Mathf.Min(70, Mathf.CeilToInt(length / 1f));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = from + dir * (length * i / steps);
                float near = 1f - (float)i / steps;
                switch (style)
                {
                    case ShotStyle.Beam:
                        Emit(fx.sparks, p, Random.insideUnitSphere * 1.5f, Random.Range(0.03f, 0.06f), Color.Lerp(tint, Color.white, 0.4f), Random.Range(0.25f, 0.5f));
                        if (i % 4 == 0) Emit(fx.rings, p, Vector3.zero, 0.35f, new Color(tint.r, tint.g, tint.b, 0.6f), 0.35f);
                        if (i % 2 == 0) Emit(fx.flares, p, Vector3.zero, 0.5f + near * 0.4f, tint, 0.2f);
                        break;
                    case ShotStyle.Fire:
                        Emit(fx.flares, p, Random.insideUnitSphere * 0.4f + Vector3.up * 0.6f, Random.Range(0.3f, 0.6f), Color.Lerp(tint, new Color(1f, 0.85f, 0.3f), Random.value), Random.Range(0.3f, 0.6f));
                        Emit(fx.sparks, p, Random.insideUnitSphere * 2f + Vector3.up * 1.5f, Random.Range(0.03f, 0.05f), new Color(1f, Random.Range(0.4f, 0.8f), 0.1f), Random.Range(0.4f, 0.8f));
                        if (i % 2 == 0) Emit(fx.smokes, p, Vector3.up * 0.5f, Random.Range(0.3f, 0.5f), new Color(0.2f, 0.18f, 0.18f, 0.35f), Random.Range(0.8f, 1.3f), Random.Range(-40f, 40f));
                        break;
                    default:
                        Emit(fx.sparks, p, Random.insideUnitSphere * 1f, Random.Range(0.03f, 0.05f), tint, Random.Range(0.25f, 0.45f));
                        if (i % 3 == 0) Emit(fx.stars, p, Random.insideUnitSphere * 0.3f, 0.2f, Color.Lerp(tint, Color.white, 0.5f), 0.5f, Random.Range(-200f, 200f));
                        break;
                }
            }
            Emit(fx.rings, from + dir * 0.3f, dir * 3f, 0.3f, new Color(tint.r, tint.g, tint.b, 0.8f), 0.22f);
            if (hit)
            {
                Emit(fx.flares, to, Vector3.zero, 2f, tint, 0.15f);
                for (int i = 0; i < 12; i++)
                    Emit(fx.sparks, to, Random.onUnitSphere * Random.Range(3f, 8f), Random.Range(0.04f, 0.07f), Color.Lerp(tint, Color.white, 0.3f), Random.Range(0.25f, 0.5f));
            }
        }

        static void NormalTrail(Vector3 from, Vector3 to, bool hit)
        {
            var fx = Instance;
            if (!fx) return;
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.1f) return;
            Vector3 dir = d / length;
            int steps = Mathf.Min(60, Mathf.CeilToInt(length / 1.2f));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = from + dir * (length * i / steps);
                float near = 1f - (float)i / steps;
                Emit(fx.smokes, p, Random.insideUnitSphere * 0.25f + Vector3.up * 0.15f, Random.Range(0.18f, 0.32f),
                    new Color(0.85f, 0.85f, 0.9f, 0.18f + 0.12f * near), Random.Range(0.6f, 1.1f), Random.Range(-40f, 40f));
                if (i % 2 == 0)
                    Emit(fx.sparks, p, dir * Random.Range(1f, 4f) + Random.insideUnitSphere * 0.6f, Random.Range(0.025f, 0.045f),
                        new Color(1f, Random.Range(0.6f, 0.85f), 0.35f), Random.Range(0.15f, 0.35f));
            }
            Emit(fx.rings, from + dir * 0.3f, dir * 3f, 0.25f, new Color(1f, 0.85f, 0.6f, 0.7f), 0.18f);
            if (hit) Emit(fx.flares, to, Vector3.zero, 1.4f, new Color(1f, 0.85f, 0.55f), 0.1f);
        }

        // A puff of dust where something hits the floor
        public static void Puff(Vector3 at, Color color)
        {
            var fx = Instance;
            if (!fx) return;
            for (int i = 0; i < 4; i++)
                Emit(fx.dirts, at, (Vector3.up + Random.insideUnitSphere * 0.8f) * Random.Range(1f, 2.5f), Random.Range(0.3f, 0.5f), color, Random.Range(0.5f, 0.9f), Random.Range(-90f, 90f));
            Emit(fx.smokes, at + Vector3.up * 0.1f, Vector3.up * 0.3f, 0.8f, new Color(0.5f, 0.5f, 0.52f, 0.5f), 1f);
        }

        // Gun smoke drifting off a muzzle
        public static void MuzzleSmoke(Vector3 at, Vector3 forward)
        {
            var fx = Instance;
            if (!fx) return;
            for (int i = 0; i < 3; i++)
                Emit(fx.smokes, at + forward * (0.1f * i), forward * Random.Range(0.6f, 1.4f) + Vector3.up * 0.3f + Random.insideUnitSphere * 0.2f,
                    Random.Range(0.25f, 0.45f), new Color(0.75f, 0.75f, 0.78f, 0.45f), Random.Range(0.7f, 1.2f), Random.Range(-40f, 40f));
        }

        // A big celebratory burst (case reveals)
        public static void Celebrate(Vector3 at, Color color, bool big)
        {
            var fx = Instance;
            if (!fx) return;
            int n = big ? 40 : 20;
            for (int i = 0; i < n; i++)
                Emit(fx.stars, at, Random.onUnitSphere * Random.Range(1.5f, big ? 5f : 3f) + Vector3.up * 1.5f, Random.Range(0.2f, 0.5f),
                    Color.Lerp(color, Color.white, Random.value * 0.5f), Random.Range(0.8f, 1.6f), Random.Range(-200f, 200f));
            Emit(fx.flares, at, Vector3.zero, big ? 4f : 2.5f, color, 0.35f);
        }

        // Soft sparkles drifting up off a display or case, forever (play mode only)
        public static void Sparkle(Transform around, Color color, float radius, float rate)
        {
            var fx = Instance;
            if (!fx || !Application.isPlaying) return;
            var ps = MakeStatic(around, "Sparkles", fx.star, 30, gravity: -0.08f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.6f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        // ------------------------------------------------------------------ ambience

        struct Look
        {
            public Material mat, glowMat;
            public Color a, b, glow;
            public float size, rate, glowRate, gravity;
            public Vector3 drift;
            public float life;
        }

        Look LookFor(string name) => name switch
        {
            // The start terrace stays clear: nothing drifting around in it
            "HALL" => new Look { mat = mote, glowMat = mote, a = Color.white, b = Color.white, glow = Color.white,
                size = 0.1f, rate = 0f, glowRate = 0f, drift = Vector3.zero, life = 1f },
            "CRIMSON HALL" => new Look { mat = mote, glowMat = spark, a = new Color(1f, 0.15f, 0.2f), b = new Color(0.6f, 0.02f, 0.06f), glow = new Color(1f, 0.2f, 0.25f),
                size = 0.14f, rate = 50f, glowRate = 10f, gravity = -0.05f, drift = new Vector3(0.1f, 0.4f, 0f), life = 6f },
            "SKY PALACE" => new Look { mat = star, glowMat = mote, a = new Color(1f, 0.95f, 0.8f), b = new Color(0.7f, 0.85f, 1f), glow = new Color(1f, 0.85f, 0.5f),
                size = 0.3f, rate = 16f, glowRate = 8f, drift = new Vector3(0.4f, 0.15f, 0f), life = 6f },
            "NEON RINGS" => new Look { mat = mote, glowMat = ring, a = new Color(1f, 0.3f, 0.85f), b = new Color(0.2f, 0.95f, 1f), glow = new Color(1f, 0.35f, 0.9f),
                size = 0.3f, rate = 26f, glowRate = 6f, drift = new Vector3(0f, 0.6f, 0f), life = 5f },
            "GROTTO" => new Look { mat = ring, glowMat = mote, a = new Color(0.2f, 1f, 0.85f, 0.8f), b = new Color(0.7f, 0.35f, 1f, 0.8f), glow = new Color(0.15f, 1f, 0.85f),
                size = 0.3f, rate = 26f, glowRate = 12f, gravity = -0.1f, drift = new Vector3(0f, 0.7f, 0f), life = 6f },
            "CANDY BLOCKS" => new Look { mat = star, glowMat = mote, a = new Color(1f, 0.4f, 0.8f), b = new Color(0.3f, 0.95f, 1f), glow = new Color(1f, 0.9f, 0.3f),
                size = 0.3f, rate = 20f, glowRate = 8f, drift = new Vector3(0.3f, 0.3f, 0f), life = 5f },
            "FORGE" => new Look { mat = mote, glowMat = spark, a = new Color(1f, 0.5f, 0.05f), b = new Color(1f, 0.2f, 0f), glow = new Color(1f, 0.65f, 0.15f),
                size = 0.14f, rate = 80f, glowRate = 14f, gravity = -0.25f, drift = new Vector3(0.3f, 1.5f, 0f), life = 4f },
            "WHITE GALLERY" => new Look { mat = mote, glowMat = star, a = new Color(1f, 0.95f, 0.85f), b = new Color(0.75f, 0.88f, 1f), glow = new Color(1f, 0.8f, 0.5f),
                size = 0.12f, rate = 30f, glowRate = 5f, drift = new Vector3(0.1f, 0.12f, 0f), life = 7f },
            "SUNSET ROOMS" => new Look { mat = mote, glowMat = star, a = new Color(1f, 0.7f, 0.5f), b = new Color(1f, 0.5f, 0.35f), glow = new Color(1f, 0.6f, 0.3f),
                size = 0.12f, rate = 30f, glowRate = 6f, drift = new Vector3(0.15f, 0.12f, 0f), life = 7f },
            "LIBRARY" => new Look { mat = mote, glowMat = star, a = new Color(1f, 0.85f, 0.5f), b = new Color(0.45f, 0.7f, 1f), glow = new Color(1f, 0.8f, 0.4f),
                size = 0.12f, rate = 34f, glowRate = 8f, gravity = -0.01f, drift = new Vector3(0.1f, 0.15f, 0f), life = 8f },
            "ALPINE" => new Look { mat = mote, glowMat = star, a = Color.white, b = new Color(0.85f, 0.92f, 1f), glow = Color.white,
                size = 0.14f, rate = 45f, glowRate = 4f, gravity = 0f, drift = new Vector3(0.3f, -0.6f, 0f), life = 8f },
            "EMBER SUNSET" or "TORCH MINES" => new Look { mat = spark, glowMat = mote, a = new Color(1f, 0.55f, 0.15f), b = new Color(1f, 0.25f, 0.05f), glow = new Color(1f, 0.45f, 0.1f),
                size = 0.1f, rate = 30f, glowRate = 8f, gravity = -0.02f, drift = new Vector3(0f, 0.35f, 0f), life = 6f },
            "AMETHYST" => new Look { mat = spark, glowMat = star, a = new Color(0.85f, 0.75f, 1f), b = new Color(0.6f, 0.4f, 1f), glow = new Color(0.8f, 0.7f, 1f),
                size = 0.1f, rate = 28f, glowRate = 10f, gravity = -0.01f, drift = new Vector3(0f, 0.2f, 0f), life = 7f },
            "SPECTRUM" => new Look { mat = spark, glowMat = star, a = new Color(1f, 0.3f, 0.3f), b = new Color(0.3f, 0.6f, 1f), glow = Color.white,
                size = 0.1f, rate = 26f, glowRate = 10f, gravity = -0.01f, drift = new Vector3(0f, 0.25f, 0f), life = 7f },
            "WIREFRAME" => new Look { mat = spark, glowMat = mote, a = new Color(1f, 0.2f, 0.1f), b = new Color(1f, 0.55f, 0.15f), glow = new Color(1f, 0.25f, 0.1f),
                size = 0.12f, rate = 30f, glowRate = 8f, gravity = -0.02f, drift = new Vector3(0f, 0.3f, 0f), life = 6f },
            _ => new Look { mat = magic, glowMat = star, a = new Color(0.85f, 0.85f, 0.95f), b = new Color(0.5f, 0.4f, 0.8f), glow = Color.white,
                size = 0.3f, rate = 12f, glowRate = 8f, gravity = -0.03f, drift = new Vector3(0f, 0.2f, 0f), life = 6f },
        };

        void Apply(ParticleSystem ps, Material mat, Color a, Color b, float size, float rate, float gravity, Vector3 drift, float life)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 1.1f, size * 1.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationMultiplier = 1f;
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 12f, 22f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(drift.x * 0.5f, drift.x * 1.2f + 0.2f);
            vel.y = new ParticleSystem.MinMaxCurve(drift.y * 0.5f, drift.y * 1.2f + 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(drift.z * 0.5f - 0.2f, drift.z * 1.2f + 0.2f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.25f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        void LateUpdate()
        {
            if (!view) return;
            // The ambient particles are made in a big box around you, and drift in the world
            Vector3 around = view.position;
            ambient.transform.position = around;
            ambientGlow.transform.position = around;

            string want = timer && !timer.Running ? "HALL" : course ? course.CurrentBiome.name : "VOID";
            if (want != profile)
            {
                profile = want;
                var look = LookFor(want);
                Apply(ambient, look.mat, look.a, look.b, look.size, look.rate, look.gravity, look.drift, look.life);
                Apply(ambientGlow, look.glowMat, look.glow, Color.Lerp(look.glow, Color.white, 0.4f), look.size * 1.6f, look.glowRate, look.gravity, look.drift, look.life);
            }

            // Speed streaks: faster than a run and they start rushing past
            if (speedLines && player)
            {
                float speed = player.Velocity.magnitude;
                float rate = Mathf.Clamp((speed - 13f) * 4f, 0f, 90f);
                int count = Mathf.FloorToInt(rate * Time.deltaTime + Random.value);
                Vector3 flow = view.InverseTransformDirection(-player.Velocity);
                for (int i = 0; i < count; i++)
                {
                    Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(2.5f, 7f);
                    var at = new Vector3(ring.x, ring.y, Random.Range(10f, 22f));
                    Emit(speedLines, at, flow * 1.1f, Random.Range(0.05f, 0.1f), new Color(1f, 1f, 1f, Mathf.Clamp01((speed - 13f) / 25f) * 0.5f), Random.Range(0.25f, 0.45f));
                }
            }
        }
    }
}
