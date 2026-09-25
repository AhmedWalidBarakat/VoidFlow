using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow
{
    // A built weapon model and the moving bits its animations need
    public class WeaponParts
    {
        public Transform root;
        public KnifeModel model;
        public SkinRarity rarity;
        public Transform blade, swingHandle, aura, bolt, magazine; // butterfly / Void / rifle parts
        public Transform tip; // the blade's point (knives), for motion trails
        public Vector3 ringCenter, boltRest, magRest;
        public float ringSpin; // talon knife: degrees spun around the finger ring (set by the view)
        public readonly List<Material> glowMaterials = new();
        public readonly List<Color> glowColors = new();
        public readonly List<(Transform t, Vector3 home, float phase)> motes = new();
        public readonly List<(Transform t, float size, float phase, float speed)> sparkles = new();

        public bool IsSword => model is KnifeModel.HollowMoon or KnifeModel.Tidebreaker or KnifeModel.Colossus;

        // The hand raises every knife to show it off (see ViewModel); on top of that the
        // talon knife spins on its finger ring, the butterfly knife does flip tricks, and Void
        // swords flare with glow while pointed
        public float InspectLength => model switch
        {
            KnifeModel.Talon => 2.4f,
            KnifeModel.Butterfly => 2.8f,
            _ => 2.9f,
        };

        // Keeps the model alive: aura motes drift, glow pulses, inspect moves parts.
        // `inspect` is seconds into the inspect, or negative when not inspecting.
        // Butterfly flips, Counter Blox style: the loose handle fans out into an X while the
        // blade whips once around the pin, then everything snaps shut, with the blade now
        // pointing the other way
        public const float FlipPeriod = 0.45f, DrawFlipPeriod = 0.32f;
        const float FlipsFrom = 0.35f, FlipsUntil = FlipsFrom + 4f * FlipPeriod; // four whole flips: the blade ends up

        static (float open, float blade) Flips(float t, float from, float until, float period)
        {
            if (t < from || t >= until) return (0f, 0f);
            float p = (t - from) / period;
            int n = Mathf.FloorToInt(p);
            float f = p - n;
            return (Mathf.Sin(f * Mathf.PI) * 150f, (n + Mathf.SmoothStep(0f, 1f, f)) * 180f);
        }

        // The butterfly inspect routine: a flip one way, a flip back the other way, a rollover
        // (the whole knife travels around the fist with its handles half open), then two quick
        // flips that bring the blade home. Returns the loose handle's opening, the blade's
        // angle around the pin and how far round the hand the knife has travelled.
        public static (float open, float blade, float orbit) ButterflyTrick(float t)
        {
            float Ease(float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
            float Fan(float a, float b) => t > a && t < b ? Mathf.Sin(Mathf.InverseLerp(a, b, t) * Mathf.PI) : 0f;
            float blade = 180f * Ease(0.35f, 0.8f) - 180f * Ease(0.8f, 1.25f) + 180f * Ease(1.95f, 2.2f) + 180f * Ease(2.2f, 2.45f);
            float open = 150f * (Fan(0.35f, 0.8f) + Fan(0.8f, 1.25f) + Fan(1.95f, 2.2f) + Fan(2.2f, 2.45f)) + 70f * Fan(1.25f, 1.95f);
            return (open, blade, 360f * Ease(1.25f, 1.95f));
        }

        // `draw` is seconds since the weapon was drawn (large when it's long out)
        public void Animate(float time, float inspect, float draw = 99f)
        {
            float burst = inspect >= 0f ? (IsSword ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1.1f, inspect)) : Bump(inspect, 0.5f, 1f, 2f)) : 0f;
            for (int i = 0; i < glowMaterials.Count; i++)
            {
                if (!glowMaterials[i]) continue;
                float pulse = 1f + 0.25f * Mathf.Sin(time * 3f + i) + burst * 2.5f;
                glowMaterials[i].SetColor("_EmissionColor", glowColors[i] * pulse);
            }
            // Sparkles flash briefly, each on its own rhythm
            foreach (var (t, size, phase, speed) in sparkles)
            {
                if (!t) continue;
                float flash = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * speed + phase)), 14f);
                t.localScale = Vector3.one * (size * flash);
                t.localRotation = Quaternion.Euler(0f, 0f, time * 90f + phase * 40f);
            }
            if (aura)
            {
                aura.localRotation = Quaternion.Euler(0f, time * 40f, 0f);
                foreach (var (t, home, phase) in motes)
                {
                    if (!t) continue;
                    t.localPosition = home + new Vector3(0f, Mathf.Sin(time * 1.7f + phase) * 0.01f, 0f);
                    t.localScale = Vector3.one * (0.005f * (1f + burst) * (0.7f + 0.3f * Mathf.Sin(time * 4f + phase)));
                }
            }

            if (model == KnifeModel.Talon && root)
            {
                // Spinning around the finger ring (how far is driven by the view: it keeps
                // going for as long as inspect is held)
                var spin = Quaternion.Euler(0f, 0f, inspect >= 0f ? -ringSpin : 0f);
                root.SetLocalPositionAndRotation(ringCenter - spin * ringCenter, spin);
            }
            if (model == KnifeModel.Butterfly && blade && swingHandle)
            {
                // Inspect: a run of flips in rhythm. Draw: two quick flips as it comes up.
                // Flips always come in pairs so the blade ends pointing up again.
                var (open, flip) = Flips(draw, 0.05f, 0.05f + 2f * DrawFlipPeriod, DrawFlipPeriod);
                if (inspect >= 0f) (open, flip, _) = ButterflyTrick(inspect);
                swingHandle.localRotation = Quaternion.Euler(0f, 0f, open);
                blade.localRotation = Quaternion.Euler(0f, 0f, flip);
            }
        }

        static float Bump(float t, float start, float peak, float end) =>
            t < peak ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, peak, t)) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(peak, end, t));
    }

    // Builds weapon models from simple shapes and generated meshes: the knives (in "hand
    // space": handle along +Y from -0.11 to 0, blade above it, edge toward -X, blade flat in
    // the XY plane) and the sniper rifle (+Z along the barrel, origin at the trigger).
    // Used for the first-person view, the hall displays and the cases.
    public class WeaponBuilder
    {
        readonly Material template;
        readonly int layer;
        readonly bool shadows;
        readonly List<Material> materials;
        readonly HashSet<Material> keep = new(); // materials a skin never covers (lenses, glows)

        public WeaponBuilder(Material template, int layer, bool shadows, List<Material> materials)
        {
            this.template = template;
            this.layer = layer;
            this.shadows = shadows;
            this.materials = materials;
        }

        public static void Kill(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        // ---------------------------------------------------------------- knives

        public WeaponParts Knife(Skins.Skin skin, Transform parent)
        {
            var root = new GameObject(skin.name).transform;
            root.SetParent(parent, false);
            var parts = new WeaponParts { root = root, model = skin.model, rarity = skin.rarity };
            Material finish = FinishMaterial(skin.finish, parts);
            // Blades are long and thin: show a matching strip of a photo texture
            if (KnifeFinishes.Get(skin.finish).photo) finish.SetTextureScale("_BaseMap", new Vector2(0.2f, 1f));
            switch (skin.model)
            {
                case KnifeModel.Butterfly: Butterfly(parts, finish); break;
                case KnifeModel.HollowMoon: HollowMoon(parts, finish); break;
                case KnifeModel.Tidebreaker: Tidebreaker(parts, finish); break;
                case KnifeModel.Colossus: Colossus(parts, finish); break;
                default: Talon(parts, finish); break;
            }
            if (skin.rarity == SkinRarity.Void) Aura(parts, skin.finish);
            if (KnifeFinishes.Get(skin.finish).photo) CoverAndSparkle(parts, skin.finish, finish, 6);
            parts.Animate(0f, -1f);
            return parts;
        }

        // A talon knife, held in reverse grip like CS: a big finger ring above the index finger,
        // a contoured grip with finger grooves and skeleton holes, a brass bolster, and a deep
        // hooked blade with a notched (jimped) spine coming out under the pinky
        void Talon(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material grip = Mat(new Color(0.05f, 0.05f, 0.055f), 0.45f, 0f);
            Material brass = Mat(new Color(0.9f, 0.66f, 0.28f), 0.88f, 0.9f);
            Material hole = Mat(new Color(0.01f, 0.01f, 0.012f), 0.2f, 0f);
            // Grip slab, rounded at the ends, with finger grooves along the edge side
            Part(t, PrimitiveType.Cube, grip, new Vector3(0.002f, -0.054f, 0f), new Vector3(0.028f, 0.1f, 0.017f));
            Part(t, PrimitiveType.Capsule, grip, new Vector3(0.002f, -0.054f, 0f), new Vector3(0.03f, 0.056f, 0.017f));
            for (int k = 0; k < 3; k++)
                Part(t, PrimitiveType.Cylinder, hole, new Vector3(-0.0135f, -0.028f - k * 0.024f, 0f), new Vector3(0.012f, 0.0092f, 0.012f), Quaternion.Euler(90f, 0f, 0f));
            foreach (float y in new[] { -0.042f, -0.072f })
                Part(t, PrimitiveType.Cylinder, hole, new Vector3(0.006f, y, 0f), new Vector3(0.009f, 0.0093f, 0.009f), Quaternion.Euler(90f, 0f, 0f));
            foreach (float y in new[] { -0.02f, -0.092f })
                Part(t, PrimitiveType.Cylinder, brass, new Vector3(0.006f, y, 0f), new Vector3(0.006f, 0.0095f, 0.006f), Quaternion.Euler(90f, 0f, 0f));
            Part(t, PrimitiveType.Cube, brass, new Vector3(0f, -0.113f, 0f), new Vector3(0.036f, 0.009f, 0.021f));
            Part(t, PrimitiveType.Cube, brass, new Vector3(0f, 0.004f, 0f), new Vector3(0.032f, 0.006f, 0.02f));
            parts.ringCenter = new Vector3(0.002f, 0.024f, 0f);
            MeshPart(t, brass, Torus(0.019f, 0.0055f, 28, 10), parts.ringCenter, Quaternion.identity);

            // Blade: a deep hook, spine outside the curve, edge inside
            const int n = 24;
            const float radius = 0.095f, sweep = 105f * Mathf.Deg2Rad, top = -0.118f;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, a = s * sweep;
                var c = new Vector2(-radius + radius * Mathf.Cos(a), top - radius * Mathf.Sin(a));
                var outward = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                float w = 0.036f * (1f - Mathf.Pow(s, 1.7f)) + 0.004f * Mathf.Sin(s * Mathf.PI);
                spine[i] = c + outward * w * 0.5f;
                edge[i] = c - outward * w * 0.5f;
            }
            MeshPart(t, finish, RailBlade(spine, edge, 0.0028f, 0.0003f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);
            // Jimping: little notches along the spine near the bolster
            for (int k = 0; k < 5; k++)
            {
                float a = (0.05f + k * 0.035f) * sweep;
                var c = new Vector2(-radius + radius * Mathf.Cos(a), top - radius * Mathf.Sin(a));
                var outward = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                var p = c + outward * 0.0185f;
                Part(t, PrimitiveType.Cube, hole, new Vector3(p.x, p.y, 0f), new Vector3(0.0035f, 0.0025f, 0.0062f), Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg));
            }
        }

        // A butterfly knife: two skeletonized "bite" handles with rounded ends and a latch, pins
        // at the pivot, and a clip-point blade with a swedge; one handle swings free for flips
        void Butterfly(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material steel = Mat(new Color(0.72f, 0.74f, 0.78f), 0.9f, 0.95f);
            Material dark = Mat(new Color(0.05f, 0.05f, 0.06f), 0.3f, 0.2f);
            Transform Handle(string name, float side)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(t, false);
                Part(pivot, PrimitiveType.Cube, steel, new Vector3(0f, -0.06f, side * 0.0095f), new Vector3(0.024f, 0.112f, 0.006f));
                Part(pivot, PrimitiveType.Cylinder, steel, new Vector3(0f, -0.116f, side * 0.0095f), new Vector3(0.024f, 0.003f, 0.024f), Quaternion.Euler(90f, 0f, 0f));
                // Skeleton cutouts: a row of slots
                for (int k = 0; k < 5; k++)
                    Part(pivot, PrimitiveType.Cube, dark, new Vector3(0f, -0.018f - k * 0.02f, side * 0.0126f), new Vector3(0.011f, 0.013f, 0.0012f));
                Part(pivot, PrimitiveType.Cylinder, dark, new Vector3(0f, -0.004f, side * 0.0095f), new Vector3(0.008f, 0.0045f, 0.008f), Quaternion.Euler(90f, 0f, 0f));
                Part(pivot, PrimitiveType.Cylinder, steel, new Vector3(0f, -0.004f, side * 0.0128f), new Vector3(0.005f, 0.0012f, 0.005f), Quaternion.Euler(90f, 0f, 0f));
                return pivot;
            }
            Handle("Handle", 1f);
            parts.swingHandle = Handle("Swing Handle", -1f);
            // Latch at the bottom of the swing handle
            Part(parts.swingHandle, PrimitiveType.Cube, steel, new Vector3(0f, -0.118f, -0.0095f), new Vector3(0.008f, 0.014f, 0.004f), Quaternion.Euler(0f, 0f, 20f));

            parts.blade = new GameObject("Blade").transform;
            parts.blade.SetParent(t, false);
            parts.blade.localPosition = new Vector3(0f, 0.002f, 0f);
            Part(parts.blade, PrimitiveType.Cube, steel, new Vector3(0f, -0.004f, 0f), new Vector3(0.022f, 0.012f, 0.005f));
            Part(parts.blade, PrimitiveType.Cube, dark, new Vector3(0.006f, -0.001f, 0f), new Vector3(0.004f, 0.004f, 0.0055f)); // kicker pin
            const int n = 18;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float y = 0.13f * i / n;
                // Spine runs straight, then a swedge dips it down to the clip point
                spine[i] = new Vector2(y < 0.08f ? 0.0115f : Mathf.Lerp(0.0115f, 0.0005f, Mathf.Pow((y - 0.08f) / 0.05f, 0.8f)), y);
                float k = Mathf.Clamp01((y - 0.055f) / 0.075f);
                edge[i] = new Vector2(-0.0125f * (1f - k * k), y);
            }
            MeshPart(parts.blade, finish, RailBlade(spine, edge, 0.0024f, 0.0003f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(parts.blade, spine[^1]);
        }

        // A curved katana blade: spine on +X, gentle curve toward the spine, rounded tip
        static (Vector2[], Vector2[]) Katana(float length, float width, float curve, float tipLength, float start)
        {
            const int n = 24;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            float tipStart = 1f - tipLength / length;
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, y = start + s * length, cx = curve * s * s;
                float sx = cx + width * 0.38f, ex = cx - width * 0.62f;
                if (s > tipStart)
                {
                    float k = (s - tipStart) / (1f - tipStart);
                    float tipX = curve + width * 0.3f;
                    sx = Mathf.Lerp(sx, tipX, k * k);
                    ex = tipX + (ex - tipX) * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k));
                }
                spine[i] = new Vector2(sx, y);
                edge[i] = new Vector2(ex, y);
            }
            return (spine, edge);
        }

        // Black katana with a small black guard, black grip wrapped with red diamonds, and a
        // chain hanging from the pommel. Faint dark-red edge glow.
        void HollowMoon(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material black = Mat(new Color(0.03f, 0.03f, 0.035f), 0.5f, 0.4f);
            Material red = Mat(new Color(0.8f, 0.04f, 0.05f), 0.35f, 0f);
            Material chain = Mat(new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.8f);
            Part(t, PrimitiveType.Cylinder, black, new Vector3(0f, -0.055f, 0f), new Vector3(0.021f, 0.058f, 0.017f));
            RedDiamonds(t, red);
            Part(t, PrimitiveType.Cube, black, new Vector3(0f, 0.002f, 0f), new Vector3(0.034f, 0.008f, 0.026f));
            Part(t, PrimitiveType.Cube, black, new Vector3(0f, 0.002f, 0f), new Vector3(0.012f, 0.008f, 0.042f), Quaternion.Euler(0f, 45f, 0f));
            Part(t, PrimitiveType.Cube, black, new Vector3(0f, -0.116f, 0f), new Vector3(0.022f, 0.008f, 0.018f));
            // Chain from the pommel, hanging off to the side
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector3(-0.004f - i * 0.009f, -0.124f - i * 0.011f + (i * i) * 0.0004f, 0f);
                MeshPart(t, chain, Torus(0.0055f, 0.0016f, 12, 6), p, Quaternion.Euler(i % 2 == 0 ? 0f : 90f, 0f, 38f));
            }
            var (spine, edge) = Katana(0.25f, 0.022f, 0.022f, 0.03f, 0.006f);
            MeshPart(t, finish, RailBlade(spine, edge, 0.0028f, 0.0005f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);
        }

        // Red diamonds peeking through black wrap, on all four sides of the grip
        void RedDiamonds(Transform t, Material red)
        {
            for (int i = 0; i < 6; i++)
            {
                float y = -0.012f - i * 0.017f;
                foreach (var (offset, turn) in new[] { (new Vector3(0f, 0f, 0.0085f), 0f), (new Vector3(0f, 0f, -0.0085f), 0f), (new Vector3(0.0105f, 0f, 0f), 90f), (new Vector3(-0.0105f, 0f, 0f), 90f) })
                    Part(t, PrimitiveType.Cube, red, offset + Vector3.up * y, new Vector3(0.008f, 0.008f, 0.002f), Quaternion.Euler(0f, turn, 45f));
            }
        }

        // Black katana with a glowing blue wave along the edge, round gold-rimmed guard,
        // navy grip with white wraps
        void Tidebreaker(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material navy = Mat(new Color(0.05f, 0.08f, 0.2f), 0.35f, 0f);
            Material white = Mat(new Color(0.9f, 0.92f, 0.95f), 0.3f, 0f);
            Material black = Mat(new Color(0.04f, 0.04f, 0.05f), 0.6f, 0.5f);
            Material gold = Mat(new Color(0.95f, 0.72f, 0.3f), 0.85f, 0.9f);
            Part(t, PrimitiveType.Cylinder, navy, new Vector3(0f, -0.055f, 0f), new Vector3(0.021f, 0.057f, 0.018f));
            for (int i = 0; i < 6; i++)
                Part(t, PrimitiveType.Cube, white, new Vector3(0f, -0.012f - i * 0.017f, 0f), new Vector3(0.023f, 0.004f, 0.02f), Quaternion.Euler(0f, 0f, i % 2 == 0 ? 20f : -20f));
            Part(t, PrimitiveType.Cylinder, gold, new Vector3(0f, -0.114f, 0f), new Vector3(0.023f, 0.005f, 0.02f));
            Part(t, PrimitiveType.Cylinder, black, new Vector3(0f, 0.001f, 0f), new Vector3(0.05f, 0.003f, 0.046f));
            Part(t, PrimitiveType.Cylinder, gold, new Vector3(0f, 0.001f, 0f), new Vector3(0.053f, 0.0015f, 0.049f));
            Part(t, PrimitiveType.Cube, gold, new Vector3(0f, 0.008f, 0f), new Vector3(0.026f, 0.01f, 0.008f));
            var (spine, edge) = Katana(0.23f, 0.024f, 0.012f, 0.035f, 0.01f);
            MeshPart(t, finish, RailBlade(spine, edge, 0.003f, 0.0005f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);
        }

        // A huge cleaver in miniature: broad straight blade with a chisel tip, two holes with
        // glowing orbs, a wide guard and a long wrapped grip
        void Colossus(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material dark = Mat(new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.8f);
            Material wrap = Mat(new Color(0.18f, 0.1f, 0.06f), 0.3f, 0f);
            Material hole = Mat(new Color(0.01f, 0.01f, 0.015f), 0.2f, 0f);
            Part(t, PrimitiveType.Cylinder, wrap, new Vector3(0f, -0.058f, 0f), new Vector3(0.02f, 0.06f, 0.02f));
            Part(t, PrimitiveType.Cube, dark, new Vector3(0f, -0.122f, 0f), new Vector3(0.016f, 0.012f, 0.016f), Quaternion.Euler(0f, 45f, 0f));
            Part(t, PrimitiveType.Cube, dark, new Vector3(-0.006f, 0.004f, 0f), new Vector3(0.072f, 0.014f, 0.018f));

            const int n = 12;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n;
                spine[i] = new Vector2(0.02f, 0.011f + s * 0.2f);
                // Straight edge, then a chisel line up to the spine at the tip
                float ey = 0.011f + s * 0.2f;
                edge[i] = new Vector2(s < 0.82f ? -0.034f : Mathf.Lerp(-0.034f, 0.02f, (s - 0.82f) / 0.18f), s < 0.82f ? ey : Mathf.Lerp(0.011f + 0.82f * 0.2f, 0.211f, (s - 0.82f) / 0.18f));
            }
            MeshPart(t, finish, RailBlade(spine, edge, 0.0034f, 0.0006f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);

            var orbColors = new[] { new Color(0.3f, 1f, 0.6f), new Color(0.6f, 0.3f, 1f) };
            for (int i = 0; i < 2; i++)
            {
                var p = new Vector3(-0.004f, 0.04f + i * 0.042f, 0f);
                Part(t, PrimitiveType.Cylinder, hole, p, new Vector3(0.019f, 0.0042f, 0.019f), Quaternion.Euler(90f, 0f, 0f));
                Part(t, PrimitiveType.Sphere, Glow(orbColors[i], 3f, null), p, Vector3.one * 0.012f);
            }
        }

        // Void knives: little glowing motes circling the blade
        void Aura(WeaponParts parts, KnifeFinish finish)
        {
            var look = KnifeFinishes.Get(finish);
            Color c = look.glow.maxColorComponent > 0f ? look.glow / look.glow.maxColorComponent : new Color(0.6f, 0.3f, 1f);
            Material mote = Glow(c, 3f, parts);
            parts.aura = new GameObject("Aura").transform;
            parts.aura.SetParent(parts.root, false);
            var random = new System.Random(finish.GetHashCode());
            for (int i = 0; i < 10; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f, r = 0.022f + (float)random.NextDouble() * 0.018f;
                var home = new Vector3(Mathf.Cos(a) * r, 0.02f + (float)random.NextDouble() * 0.2f, Mathf.Sin(a) * r);
                var t = Part(parts.aura, PrimitiveType.Cube, mote, home, Vector3.one * 0.005f, Quaternion.Euler(45f, 45f, 0f));
                parts.motes.Add((t, home, (float)random.NextDouble() * 10f));
            }
        }

        // ---------------------------------------------------------------- sniper rifle

        // The heavy bolt-action rifle. The skin's finish goes on the stock and chassis.
        public WeaponParts Rifle(Skins.Skin skin, Transform parent)
        {
            var t = new GameObject(skin.name).transform;
            t.SetParent(parent, false);
            var parts = new WeaponParts { root = t, model = KnifeModel.Rifle, rarity = skin.rarity };
            Material metal = Mat(new Color(0.1f, 0.1f, 0.11f), 0.5f, 0.6f);
            Material metalLight = Mat(new Color(0.28f, 0.28f, 0.3f), 0.45f, 0.6f);
            Material chassis = skin.finish == KnifeFinish.Polished ? Mat(new Color(0.24f, 0.25f, 0.21f), 0.2f, 0.1f) : FinishMaterial(skin.finish, parts);
            Material rubber = Mat(new Color(0.04f, 0.04f, 0.04f), 0.15f, 0f);
            Material lens = Mat(new Color(0.06f, 0.1f, 0.2f), 0.95f, 0.4f);
            Material red = Mat(new Color(0.85f, 0.1f, 0.06f), 0.4f, 0.1f);

            // Classic arctic-style sniper silhouette (an original build): thumbhole stock, a long
            // squared receiver, a slim forend, a long thin fluted barrel with a muzzle brake, and a
            // long scope with a big objective bell. The skin's finish goes on the stock, receiver
            // shell, forend and scope.
            //
            // Stock: an open frame around the thumbhole
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, 0.034f, -0.2f), new Vector3(0.038f, 0.026f, 0.32f));                                   // comb (top)
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, -0.075f, -0.25f), new Vector3(0.038f, 0.026f, 0.22f), Quaternion.Euler(-9f, 0f, 0f)); // belly (bottom)
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, -0.018f, -0.37f), new Vector3(0.04f, 0.13f, 0.045f));                                   // butt
            Part(t, PrimitiveType.Cube, rubber, new Vector3(0f, -0.018f, -0.397f), new Vector3(0.044f, 0.136f, 0.012f));                                 // butt pad
            Part(t, PrimitiveType.Cube, rubber, new Vector3(0f, 0.052f, -0.26f), new Vector3(0.034f, 0.012f, 0.14f));                                    // cheek riser
            // Pistol grip through the thumbhole, and the trigger guard
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, -0.045f, -0.06f), new Vector3(0.034f, 0.1f, 0.036f), Quaternion.Euler(18f, 0f, 0f));
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, -0.036f, 0.01f), new Vector3(0.008f, 0.006f, 0.07f));
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, -0.022f, 0.043f), new Vector3(0.008f, 0.03f, 0.006f));
            Part(t, PrimitiveType.Cube, metalLight, new Vector3(0f, -0.02f, 0.005f), new Vector3(0.006f, 0.02f, 0.006f), Quaternion.Euler(15f, 0f, 0f));
            // Receiver: a squared shell on a darker core, with an ejection port
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.042f, 0.05f, 0.3f));
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, -0.007f, 0.07f), new Vector3(0.048f, 0.03f, 0.3f));
            Part(t, PrimitiveType.Cube, rubber, new Vector3(0.022f, 0.028f, 0.03f), new Vector3(0.004f, 0.02f, 0.08f));
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, 0.05f, 0.07f), new Vector3(0.024f, 0.01f, 0.34f));                                       // scope rail
            // Forend, slimmer than the receiver, tapering toward the muzzle
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, -0.004f, 0.33f), new Vector3(0.044f, 0.04f, 0.24f));
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, -0.004f, 0.47f), new Vector3(0.038f, 0.034f, 0.05f));
            Part(t, PrimitiveType.Cube, rubber, new Vector3(0f, -0.026f, 0.33f), new Vector3(0.046f, 0.006f, 0.2f));
            // Barrel: long and thin, with flutes, a gas-block collar and a muzzle brake
            Rod(t, metalLight, new Vector3(0f, 0.018f, 0.2f), new Vector3(0f, 0.018f, 1.02f), 0.017f);
            for (int k = 0; k < 6; k++)
                Rod(t, metal, new Vector3(0f, 0.018f, 0.55f + k * 0.065f), new Vector3(0f, 0.018f, 0.57f + k * 0.065f), 0.0195f);
            Rod(t, metal, new Vector3(0f, 0.018f, 1.02f), new Vector3(0f, 0.018f, 1.09f), 0.03f);
            foreach (float z in new[] { 1.035f, 1.06f })
                Part(t, PrimitiveType.Cube, rubber, new Vector3(0f, 0.018f, z), new Vector3(0.033f, 0.01f, 0.01f));
            // Magazine just ahead of the trigger
            parts.magazine = new GameObject("Magazine").transform;
            parts.magazine.SetParent(t, false);
            parts.magRest = new Vector3(0f, -0.03f, 0.1f);
            parts.magazine.localPosition = parts.magRest;
            Part(parts.magazine, PrimitiveType.Cube, metal, new Vector3(0f, -0.022f, 0f), new Vector3(0.03f, 0.05f, 0.075f), Quaternion.Euler(-5f, 0f, 0f));
            // Bolt handle on the right, with a round knob, pivoting around the bore
            parts.bolt = new GameObject("Bolt").transform;
            parts.bolt.SetParent(t, false);
            parts.boltRest = new Vector3(0.022f, 0.032f, -0.035f);
            parts.bolt.localPosition = parts.boltRest;
            Rod(parts.bolt, metalLight, Vector3.zero, new Vector3(0.045f, -0.016f, 0f), 0.008f);
            Part(parts.bolt, PrimitiveType.Sphere, metal, new Vector3(0.05f, -0.018f, 0f), Vector3.one * 0.019f);

            // Scope: two rings, a long tube, big objective bell and lens, turrets and an eyepiece
            const float sy = 0.098f;
            foreach (float z in new[] { -0.02f, 0.14f })
            {
                Part(t, PrimitiveType.Cube, metal, new Vector3(0f, 0.07f, z), new Vector3(0.03f, 0.03f, 0.02f));
                Rod(t, metal, new Vector3(0f, sy, z - 0.01f), new Vector3(0f, sy, z + 0.01f), 0.04f);
            }
            Rod(t, chassis, new Vector3(0f, sy, -0.09f), new Vector3(0f, sy, 0.22f), 0.032f);
            Rod(t, metal, new Vector3(0f, sy, 0.22f), new Vector3(0f, sy, 0.27f), 0.046f);
            Rod(t, metal, new Vector3(0f, sy, 0.27f), new Vector3(0f, sy, 0.34f), 0.066f);
            Rod(t, rubber, new Vector3(0f, sy, 0.335f), new Vector3(0f, sy, 0.345f), 0.069f);
            Rod(t, lens, new Vector3(0f, sy, 0.344f), new Vector3(0f, sy, 0.346f), 0.058f);
            Rod(t, metal, new Vector3(0f, sy, -0.09f), new Vector3(0f, sy, -0.15f), 0.044f);
            Rod(t, rubber, new Vector3(0f, sy, -0.15f), new Vector3(0f, sy, -0.17f), 0.046f);
            Rod(t, lens, new Vector3(0f, sy, -0.17f), new Vector3(0f, sy, -0.171f), 0.036f);
            Rod(t, metal, new Vector3(0f, sy + 0.016f, 0.06f), new Vector3(0f, sy + 0.04f, 0.06f), 0.03f);
            Rod(t, red, new Vector3(0f, sy + 0.03f, 0.06f), new Vector3(0f, sy + 0.034f, 0.06f), 0.032f);
            Rod(t, metal, new Vector3(0.016f, sy, 0.06f), new Vector3(0.04f, sy, 0.06f), 0.028f);
            Rod(t, metal, new Vector3(-0.016f, sy, 0.06f), new Vector3(-0.034f, sy, 0.06f), 0.03f);
            keep.Add(lens);
            keep.Add(red);
            if (KnifeFinishes.Get(skin.finish).photo) CoverAndSparkle(parts, skin.finish, chassis, 14, cover: false); // the barrel, scope ends and metal stay dark
            parts.Animate(0f, -1f);
            return parts;
        }

        // ---------------------------------------------------------------- materials

        public Material Mat(Color color, float smoothness, float metallic)
        {
            var m = new Material(template);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            materials.Add(m);
            return m;
        }

        // Emissive material; if `parts` is given it pulses with the weapon
        public Material Glow(Color color, float intensity, WeaponParts parts)
        {
            var m = Mat(color, 0.2f, 0f);
            keep.Add(m);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            if (parts != null)
            {
                parts.glowMaterials.Add(m);
                parts.glowColors.Add(color * intensity);
            }
            return m;
        }

        Material FinishMaterial(KnifeFinish finish, WeaponParts parts)
        {
            var look = KnifeFinishes.Get(finish);
            // Every finish is polished metal: mirror-smooth and fully metallic, so each one
            // flashes and reflects like the golds do
            var m = Mat(look.tint, Mathf.Max(look.smoothness, 0.95f), Mathf.Max(look.metallic, 0.9f));
            if (look.albedo) m.SetTexture("_BaseMap", look.albedo);
            var emission = look.emission ? look.emission : look.photo ? KnifeFinishes.Glitter : null;
            if (emission)
            {
                // Void finishes glow along their edges; photo finishes get fine glitter
                Color glow = look.emission ? look.glow : Color.white * 0.7f;
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", emission);
                m.SetColor("_EmissionColor", glow);
                parts.glowMaterials.Add(m);
                parts.glowColors.Add(glow);
            }
            return m;
        }

        // Photo skins wrap the whole weapon (everything but lenses and glowing bits) in the
        // finish, and scatter little twinkling star sparkles over it
        void CoverAndSparkle(WeaponParts parts, KnifeFinish finish, Material bladeOrBody, int count, bool cover = true)
        {
            // Blades show a strip of the texture; everything else a full, untouched copy
            Material body = bladeOrBody;
            if (bladeOrBody.GetTextureScale("_BaseMap") != Vector2.one)
            {
                body = FinishMaterial(finish, parts);
            }
            var renderers = parts.root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
                if (cover && r.sharedMaterial != bladeOrBody && !keep.Contains(r.sharedMaterial)) r.sharedMaterial = body;

            Material star = Glow(Color.white, 5f, null);
            var random = new System.Random(finish.GetHashCode() * 31 + count);
            for (int i = 0; i < count; i++)
            {
                var r = renderers[random.Next(renderers.Length)];
                var b = r.localBounds;
                var local = b.center + Vector3.Scale(b.extents, new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f));
                Vector3 p = parts.root.InverseTransformPoint(r.transform.TransformPoint(local));
                var sparkle = new GameObject("Sparkle").transform;
                sparkle.SetParent(parts.root, false);
                sparkle.localPosition = p;
                Part(sparkle, PrimitiveType.Cube, star, Vector3.zero, new Vector3(1f, 0.12f, 0.12f));
                Part(sparkle, PrimitiveType.Cube, star, Vector3.zero, new Vector3(0.12f, 1f, 0.12f));
                float size = parts.model == KnifeModel.Rifle ? 0.03f : 0.016f;
                parts.sparkles.Add((sparkle, size, (float)random.NextDouble() * 20f, 1.2f + (float)random.NextDouble() * 1.6f));
            }
        }

        // ---------------------------------------------------------------- shapes

        public Transform Part(Transform parent, PrimitiveType shape, Material mat, Vector3 position, Vector3 scale) =>
            Part(parent, shape, mat, position, scale, Quaternion.identity);

        public Transform Part(Transform parent, PrimitiveType shape, Material mat, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(shape);
            Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            Finish(go.GetComponent<MeshRenderer>(), mat);
            return go.transform;
        }

        static Transform Tip(Transform parent, Vector2 at)
        {
            var t = new GameObject("Tip").transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        // A cylinder from one point to another
        public Transform Rod(Transform parent, Material mat, Vector3 from, Vector3 to, float diameter)
        {
            Vector3 d = to - from;
            return Part(parent, PrimitiveType.Cylinder, mat, (from + to) * 0.5f, new Vector3(diameter, d.magnitude * 0.5f, diameter),
                Quaternion.FromToRotation(Vector3.up, d));
        }

        public Transform MeshPart(Transform parent, Material mat, Mesh mesh, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Finish(go.AddComponent<MeshRenderer>(), mat);
            return go.transform;
        }

        void Finish(MeshRenderer r, Material mat)
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            r.gameObject.layer = layer;
        }

        // A blade between two rails of points: spine and edge (same count, the last pair
        // meeting at the tip). Thick at the spine, thin at the edge, thinning toward the tip.
        public static Mesh RailBlade(Vector2[] spine, Vector2[] edge, float spineHalf, float edgeHalf)
        {
            int n = spine.Length;
            var along = new float[n];
            for (int i = 1; i < n; i++)
                along[i] = along[i - 1] + Vector2.Distance((spine[i] + edge[i]) * 0.5f, (spine[i - 1] + edge[i - 1]) * 0.5f);
            float total = Mathf.Max(along[n - 1], 1e-5f);
            float Half(int i) => Mathf.Lerp(spineHalf, edgeHalf, Mathf.Pow(along[i] / total, 4f) * 0.9f);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Tri(int a, int b, int c, Vector3 facing)
            {
                var normal = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (Vector3.Dot(normal, facing) < 0f) (b, c) = (c, b);
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 facing, float v0, float v1)
            {
                int s = verts.Count;
                verts.AddRange(new[] { p0, p1, p2, p3 });
                uvs.AddRange(new[] { new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(0f, v1), new Vector2(0f, v0) });
                Tri(s, s + 1, s + 2, facing);
                Tri(s, s + 2, s + 3, facing);
            }

            // Both faces: a flat from the spine to the grind line, then a bevel down to the
            // edge (separate vertices so the grind line is a crisp crease that catches light)
            const float grindAt = 0.42f; // how far from the edge toward the spine the bevel starts
            foreach (float side in new[] { 1f, -1f })
            {
                var facing = new Vector3(0f, 0f, side);
                for (int band = 0; band < 2; band++)
                {
                    int b = verts.Count;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 grind = Vector2.Lerp(edge[i], spine[i], grindAt);
                        float v = along[i] / total, grindHalf = Mathf.Lerp(edgeHalf, Half(i), 0.9f);
                        if (band == 0)
                        {
                            verts.Add(new Vector3(spine[i].x, spine[i].y, side * Half(i)));
                            verts.Add(new Vector3(grind.x, grind.y, side * grindHalf));
                            uvs.Add(new Vector2(1f, v));
                            uvs.Add(new Vector2(grindAt, v));
                        }
                        else
                        {
                            verts.Add(new Vector3(grind.x, grind.y, side * grindHalf));
                            verts.Add(new Vector3(edge[i].x, edge[i].y, side * edgeHalf));
                            uvs.Add(new Vector2(grindAt, v));
                            uvs.Add(new Vector2(0f, v));
                        }
                    }
                    for (int i = 0; i < n - 1; i++)
                    {
                        int s0 = b + i * 2, e0 = s0 + 1, s1 = s0 + 2, e1 = s0 + 3;
                        Tri(s0, e0, e1, facing);
                        Tri(s0, e1, s1, facing);
                    }
                }
            }
            // Spine and edge rims, flat-shaded
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 across = (spine[i] - edge[i]).sqrMagnitude > 1e-10f ? (spine[i] - edge[i]).normalized : Vector2.right;
                float v0 = along[i] / total, v1 = along[i + 1] / total;
                Quad(new Vector3(spine[i].x, spine[i].y, Half(i)), new Vector3(spine[i + 1].x, spine[i + 1].y, Half(i + 1)),
                     new Vector3(spine[i + 1].x, spine[i + 1].y, -Half(i + 1)), new Vector3(spine[i].x, spine[i].y, -Half(i)), across, v0, v1);
                Quad(new Vector3(edge[i].x, edge[i].y, edgeHalf), new Vector3(edge[i + 1].x, edge[i + 1].y, edgeHalf),
                     new Vector3(edge[i + 1].x, edge[i + 1].y, -edgeHalf), new Vector3(edge[i].x, edge[i].y, -edgeHalf), -across, v0, v1);
            }
            // Base cap
            Vector2 back = ((spine[0] + edge[0]) - (spine[1] + edge[1])).normalized;
            Quad(new Vector3(spine[0].x, spine[0].y, Half(0)), new Vector3(edge[0].x, edge[0].y, edgeHalf),
                 new Vector3(edge[0].x, edge[0].y, -edgeHalf), new Vector3(spine[0].x, spine[0].y, -Half(0)), back, 0f, 0f);

            var mesh = new Mesh { name = "Blade" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A ring around the Z axis
        public static Mesh Torus(float radius, float tube, int segments = 24, int sides = 8)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var center = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                var outward = center.normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    verts.Add(center + (outward * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * tube);
                }
            }
            for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
            var mesh = new Mesh { name = "Ring" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // Make sure the faces point outward
            int[] t = mesh.triangles;
            Vector3 n0 = Vector3.Cross(verts[t[1]] - verts[t[0]], verts[t[2]] - verts[t[0]]);
            Vector3 c0 = new Vector3(Mathf.Cos(0f), Mathf.Sin(0f), 0f) * radius;
            if (Vector3.Dot(n0, verts[t[0]] - c0) < 0f)
            {
                for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                mesh.triangles = t;
                mesh.RecalculateNormals();
            }
            return mesh;
        }
    }
}
