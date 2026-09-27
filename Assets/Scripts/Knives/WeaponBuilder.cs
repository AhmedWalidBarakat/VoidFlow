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
        public WeaponFlames flames;
        // Moving parts: things that turn (gears, rings, orbits), things that crackle on and off
        // (lightning, holograms) and things that glitch out of place and snap back
        public readonly List<(Transform t, Quaternion rest, Vector3 rate)> spinners = new();
        public readonly List<(Transform t, float phase, float rate)> flickers = new();
        public readonly List<(Transform t, Vector3 home, float amount)> jitters = new();
        // Spectrum: every glow cycles through the colors
        public bool cycleHue;
        public readonly List<(Material m, float alpha, float intensity)> tintMaterials = new();

        static float Hash(float n)
        {
            float s = Mathf.Sin(n * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
        public Color hue = Color.white; // Void weapons: the color they burn in

        // The hand raises every knife to show it off (see ViewModel); on top of that the
        // talon knife spins on its finger ring, the butterfly knife does flip tricks, and Void
        // swords flare with glow while pointed
        public float InspectLength => model switch
        {
            KnifeModel.Talon => 2.4f,
            KnifeModel.Butterfly => 2.8f,
            KnifeModel.Bayonet or KnifeModel.Skeleton or KnifeModel.KukriKnife => 3.3f,
            KnifeModel.Reaper or KnifeModel.Saber or KnifeModel.Shardfang or KnifeModel.Kukri or KnifeModel.Claws
                or KnifeModel.Axe or KnifeModel.Sai or KnifeModel.Spear or KnifeModel.Kris => 3.2f,
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
            if (flames) flames.boost = inspect >= 0f ? 1f : 0f;
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

            foreach (var (t, rest, rate) in spinners)
                if (t) t.localRotation = rest * Quaternion.Euler(rate * time);
            foreach (var (t, phase, rate) in flickers)
                if (t) t.gameObject.SetActive(Hash(Mathf.Floor(time * rate + phase)) > 0.4f);
            for (int i = 0; i < jitters.Count; i++)
            {
                var (t, home, amount) = jitters[i];
                if (!t) continue;
                float step = Mathf.Floor(time * 10f + i * 3.7f);
                bool slip = Hash(step + i * 17f) > 0.82f;
                t.localPosition = slip ? home + new Vector3(Hash(step) - 0.5f, Hash(step + 1f) - 0.5f, Hash(step + 2f) - 0.5f) * amount * 2f : home;
            }
            if (cycleHue)
            {
                Color h = Color.HSVToRGB(Mathf.Repeat(time * 0.12f, 1f), 0.85f, 1f);
                hue = h;
                if (flames) flames.hue = h;
                for (int i = 0; i < glowMaterials.Count; i++)
                {
                    if (!glowMaterials[i]) continue;
                    float pulse = 1f + 0.25f * Mathf.Sin(time * 3f + i) + burst * 2.5f;
                    glowMaterials[i].SetColor("_EmissionColor", h * glowColors[i].maxColorComponent * pulse);
                }
                foreach (var (m, alpha, intensity) in tintMaterials)
                {
                    if (!m) continue;
                    m.SetColor("_BaseColor", new Color(h.r, h.g, h.b, alpha));
                    m.SetColor("_EmissionColor", h * intensity);
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
    public partial class WeaponBuilder
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

        // The size of a weapon's meshes in its root's space (flames and aura left out)
        public static Vector3 MeshSize(WeaponParts parts)
        {
            Vector3 lo = Vector3.one * 99f, hi = -lo;
            foreach (var mf in parts.root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || mf.name == "Flame" || (parts.aura && mf.transform.IsChildOf(parts.aura))) continue;
                var b = mf.sharedMesh.bounds;
                var m = parts.root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    lo = Vector3.Min(lo, p);
                    hi = Vector3.Max(hi, p);
                }
            }
            return hi.x >= lo.x ? hi - lo : Vector3.zero;
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
            var parts = new WeaponParts { root = root, model = skin.model, rarity = skin.rarity, hue = HueOf(skin.finish) };
            Material finish = FinishMaterial(skin.finish, parts);
            // Blades are long and thin: show a matching strip of a photo texture
            if (KnifeFinishes.Get(skin.finish).photo) finish.SetTextureScale("_BaseMap", new Vector2(0.2f, 1f));
            switch (skin.model)
            {
                case KnifeModel.Butterfly: Butterfly(parts, finish); break;
                case KnifeModel.Bayonet: Bayonet(parts, finish); break;
                case KnifeModel.Skeleton: Skeleton(parts, finish); break;
                case KnifeModel.KukriKnife: KukriKnife(parts, finish); break;
                case KnifeModel.HollowMoon: HollowMoon(parts, finish); break;
                case KnifeModel.Tidebreaker: Tidebreaker(parts, finish); break;
                case KnifeModel.Colossus: Colossus(parts, finish); break;
                case KnifeModel.Reaper: Reaper(parts, finish); break;
                case KnifeModel.Saber: Saber(parts, finish); break;
                case KnifeModel.Shardfang: Shard(parts, finish); break;
                case KnifeModel.Kukri: Kukri(parts, finish); break;
                case KnifeModel.Claws: Claws(parts, finish); break;
                case KnifeModel.Axe: Axe(parts, finish); break;
                case KnifeModel.Sai: Sai(parts, finish); break;
                case KnifeModel.Spear: Spear(parts, finish); break;
                case KnifeModel.Kris: Kris(parts, finish); break;
                default: Talon(parts, finish); break;
            }
            if (skin.rarity == SkinRarity.Void)
            {
                Aura(parts, skin.finish);
                // Flames along the blade, from its base to its point
                var at = new List<Vector3>();
                Vector3 from = parts.model == KnifeModel.Talon ? new Vector3(-0.02f, -0.14f, 0f) : new Vector3(0f, 0.03f, 0f);
                Vector3 to = parts.tip ? parts.root.InverseTransformPoint(parts.tip.position) : new Vector3(0f, 0.22f, 0f);
                if (parts.model == KnifeModel.Reaper) from = new Vector3(0f, 0.14f, 0f);
                for (int k = 0; k < 5; k++) at.Add(Vector3.Lerp(from, to, (k + 0.5f) / 5f));
                AddFlames(parts, at, 0.085f, 0.09f);
            }
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

        // A flat part cut from its side outline in the knife's own plane (x across the blade,
        // y along it), `half` thick either side of the middle
        Transform Flat(Transform t, Material mat, Vector2[] outline, Vector2[][] holes, float half, float bevel, string name) =>
            MeshPart(t, mat, ProfileMesh.Extrude(outline, holes, half, bevel, name), Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

        static Vector2[] Circle(Vector2 c, float r, int n = 20)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }

        // An M9-style bayonet: a long clip-point blade with a sawback along the spine and the
        // wire-cutter slot at its base, a steel crossguard with the muzzle ring over the spine,
        // a ribbed oval grip and a steel pommel with its release latch
        void Bayonet(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material steel = Mat(new Color(0.55f, 0.56f, 0.58f), 0.8f, 0.95f);
            Material grip = Mat(new Color(0.045f, 0.045f, 0.05f), 0.25f, 0f);
            Material rib = Mat(new Color(0.015f, 0.015f, 0.018f), 0.15f, 0f);
            Material hole = Mat(new Color(0.01f, 0.01f, 0.012f), 0.2f, 0f);

            const int n = 64;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float y = 0.006f + 0.182f * i / n;
                float sx = 0.012f;
                if (y > 0.035f && y < 0.1f && i % 2 == 0) sx = 0.0155f; // saw teeth
                if (y > 0.132f) sx = Mathf.Lerp(0.012f, -0.003f, Mathf.Pow((y - 0.132f) / 0.056f, 0.85f)); // clip
                float k = Mathf.Clamp01((y - 0.125f) / 0.063f);
                spine[i] = new Vector2(sx, y);
                edge[i] = new Vector2(Mathf.Lerp(-0.0195f, -0.003f, Mathf.SmoothStep(0f, 1f, k)), y);
            }
            edge[n] = spine[n];
            MeshPart(t, finish, RailBlade(spine, edge, 0.0027f, 0.0003f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);
            // Wire-cutter slot through the blade near its base
            foreach (float y in new[] { 0.024f, 0.036f })
                Part(t, PrimitiveType.Cylinder, hole, new Vector3(-0.002f, y, 0f), new Vector3(0.0085f, 0.0034f, 0.0085f), Quaternion.Euler(90f, 0f, 0f));
            Part(t, PrimitiveType.Cube, hole, new Vector3(-0.002f, 0.03f, 0f), new Vector3(0.0085f, 0.012f, 0.0068f));
            // Fuller: a dark groove down both flats
            foreach (float side in new[] { 1f, -1f })
                Part(t, PrimitiveType.Cube, rib, new Vector3(0.006f, 0.085f, side * 0.0024f), new Vector3(0.0022f, 0.07f, 0.0006f));

            // Crossguard, with the muzzle ring standing over the spine
            Flat(t, steel, new Vector2[]
            {
                new(-0.029f, 0.006f), new(0.018f, 0.006f), new(0.021f, 0.002f), new(0.018f, -0.006f), new(-0.02f, -0.006f), new(-0.03f, -0.002f),
            }, null, 0.0085f, 0.0025f, "Crossguard");
            MeshPart(t, steel, Torus(0.0105f, 0.0032f, 24, 10), new Vector3(0.031f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f));

            // Grip: an oval section, swelling a little in the middle, ribbed across
            Flat(t, grip, new Vector2[]
            {
                new(0.011f, -0.006f), new(0.0125f, -0.04f), new(0.012f, -0.075f), new(0.011f, -0.104f),
                new(-0.012f, -0.104f), new(-0.0145f, -0.075f), new(-0.0135f, -0.04f), new(-0.012f, -0.006f),
            }, null, 0.0115f, 0.0055f, "Grip");
            for (int r = 0; r < 9; r++)
            {
                float y = -0.016f - r * 0.0098f;
                Part(t, PrimitiveType.Cube, rib, new Vector3(-0.0005f, y, 0f), new Vector3(0.0265f, 0.0022f, 0.0215f));
            }
            // Pommel: steel cap, release latch on the spine side, the lug slot underneath
            Flat(t, steel, new Vector2[]
            {
                new(0.012f, -0.103f), new(0.014f, -0.112f), new(0.01f, -0.121f), new(-0.013f, -0.121f), new(-0.016f, -0.112f), new(-0.013f, -0.103f),
            }, null, 0.0105f, 0.003f, "Pommel");
            Part(t, PrimitiveType.Cube, steel, new Vector3(0.016f, -0.106f, 0f), new Vector3(0.006f, 0.012f, 0.008f), Quaternion.Euler(0f, 0f, -12f));
            Part(t, PrimitiveType.Cube, hole, new Vector3(-0.0005f, -0.1215f, 0f), new Vector3(0.016f, 0.0015f, 0.004f));
        }

        // A skeleton knife: a wide drop-point blade whose steel runs on into an open-frame handle
        // (the finish covers all of it) ending in a finger ring, the frame wrapped in cord
        void Skeleton(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material cord = Mat(new Color(0.06f, 0.06f, 0.055f), 0.2f, 0f);
            Material cord2 = Mat(new Color(0.1f, 0.095f, 0.085f), 0.2f, 0f);
            Material hole = Mat(new Color(0.01f, 0.01f, 0.012f), 0.2f, 0f);

            const int n = 30;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, y = 0.002f + 0.158f * s;
                float drop = Mathf.Clamp01((s - 0.62f) / 0.38f);
                float sx = Mathf.Lerp(0.0135f, 0.001f, drop * drop);
                float belly = Mathf.Clamp01((s - 0.55f) / 0.45f);
                float ex = Mathf.Lerp(-0.0215f, 0.001f, Mathf.Pow(belly, 1.7f));
                spine[i] = new Vector2(sx, y);
                edge[i] = new Vector2(ex, y);
            }
            MeshPart(t, finish, RailBlade(spine, edge, 0.0026f, 0.0003f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);
            // Jimping along the spine at the thumb
            for (int k = 0; k < 6; k++)
                Part(t, PrimitiveType.Cube, hole, new Vector3(0.0135f, 0.012f + k * 0.0045f, 0f), new Vector3(0.0028f, 0.0018f, 0.0056f));

            // The frame: spine side down to the ring, round the ring, and up the edge side past a
            // finger choil to the heel of the blade
            var c = new Vector2(0f, -0.128f);
            const float R = 0.0195f;
            var frame = new List<Vector2> { new(-0.0215f, 0.006f), new(0.0135f, 0.006f), new(0.0125f, -0.02f), new(0.0118f, -0.07f), new(0.011f, -0.105f) };
            float a0 = Mathf.Acos(0.011f / R), a1 = Mathf.PI - a0 - Mathf.PI * 2f;
            for (int i = 0; i <= 20; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / 20f);
                frame.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R);
            }
            frame.AddRange(new Vector2[] { new(-0.0115f, -0.098f), new(-0.013f, -0.08f), new(-0.0115f, -0.058f), new(-0.013f, -0.038f), new(-0.019f, -0.014f), new(-0.0215f, -0.004f) });
            var slot = new Vector2[] { new(0.004f, -0.014f), new(0.0045f, -0.092f), new(-0.004f, -0.096f), new(-0.0055f, -0.014f) };
            Flat(t, finish, frame.ToArray(), new[] { slot, Circle(c, 0.0118f) }, 0.0024f, 0.0011f, "Frame");
            parts.ringCenter = new Vector3(c.x, c.y, 0f);

            // Cord wraps round the middle of the frame, leaving the slot showing at either end
            for (int w = 0; w < 11; w++)
            {
                float y = -0.026f - w * 0.0055f;
                Part(t, PrimitiveType.Cube, w % 2 == 0 ? cord : cord2, new Vector3(0f, y, 0f), new Vector3(0.0265f, 0.0048f, 0.0075f), Quaternion.Euler(0f, 0f, w % 2 == 0 ? 10f : -10f));
            }
        }

        // A kukri: the heavy blade bends forward toward its edge and swells into a wide belly
        // before the point, with the little notch at the base of the edge; a steel bolster, a
        // ringed grip, and the flared hooked pommel
        void KukriKnife(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material steel = Mat(new Color(0.5f, 0.5f, 0.52f), 0.75f, 0.95f);
            Material grip = Mat(new Color(0.05f, 0.045f, 0.045f), 0.3f, 0f);
            Material hole = Mat(new Color(0.01f, 0.01f, 0.012f), 0.2f, 0f);

            const int n = 40;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, y = 0.008f + 0.19f * s;
                float bend = -0.06f * Mathf.Pow(s, 1.6f);
                float w = 0.02f + 0.036f * Mathf.Sin(Mathf.Min(s / 0.7f, 1f) * Mathf.PI * 0.5f);
                float tip = Mathf.Clamp01((s - 0.8f) / 0.2f);
                float sx = bend + 0.009f - tip * tip * 0.02f;
                float ex = bend + 0.009f - w;
                ex = Mathf.Lerp(ex, sx - 0.001f, Mathf.Pow(tip, 0.9f));
                // The notch at the base of the edge
                float notch = Mathf.Clamp01(1f - Mathf.Abs(y - 0.024f) / 0.007f);
                ex += notch * 0.007f;
                spine[i] = new Vector2(sx, y + tip * tip * 0.004f);
                edge[i] = new Vector2(ex, y - 0.012f * s);
            }
            MeshPart(t, finish, RailBlade(spine, edge, 0.0036f, 0.0004f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);

            // Bolster
            Flat(t, steel, new Vector2[] { new(0.013f, 0.009f), new(0.014f, -0.004f), new(-0.016f, -0.004f), new(-0.013f, 0.009f) }, null, 0.0105f, 0.003f, "Bolster");
            // Grip: swelling in the middle, then flaring out and hooking down to the pommel
            Flat(t, grip, new Vector2[]
            {
                new(0.0115f, -0.004f), new(0.0135f, -0.04f), new(0.012f, -0.08f), new(0.013f, -0.1f), new(0.017f, -0.112f), new(0.015f, -0.12f),
                new(0.004f, -0.121f), new(-0.012f, -0.117f), new(-0.021f, -0.121f), new(-0.019f, -0.11f), new(-0.013f, -0.098f), new(-0.0115f, -0.08f),
                new(-0.0145f, -0.045f), new(-0.013f, -0.004f),
            }, null, 0.0112f, 0.005f, "Grip");
            foreach (float y in new[] { -0.018f, -0.024f })
                Part(t, PrimitiveType.Cube, steel, new Vector3(-0.0008f, y, 0f), new Vector3(0.0275f, 0.0025f, 0.0225f));
            Part(t, PrimitiveType.Cube, steel, new Vector3(-0.001f, -0.1215f, 0f), new Vector3(0.028f, 0.003f, 0.018f));
            Part(t, PrimitiveType.Cylinder, hole, new Vector3(0.001f, -0.1105f, 0f), new Vector3(0.0045f, 0.0115f, 0.0045f), Quaternion.Euler(90f, 0f, 0f));
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

        // Living flames at the given points (weapon space), in the weapon's burning color
        public void AddFlames(WeaponParts parts, List<Vector3> at, float size, float rise)
        {
            var holder = new GameObject("Flames");
            holder.layer = layer;
            holder.transform.SetParent(parts.root, false);
            parts.flames = holder.AddComponent<WeaponFlames>();
            parts.flames.Setup(template, at, parts.hue, size, rise);
        }

        static Color HueOf(KnifeFinish finish)
        {
            var glow = KnifeFinishes.Get(finish).glow;
            return glow.maxColorComponent > 0f ? glow / glow.maxColorComponent : new Color(0.7f, 0.35f, 1f);
        }

        // A see-through glowing material (plasma halos)
        Material SeeThroughGlow(Color color, float alpha, float intensity, WeaponParts tinted)
        {
            var m = SeeThroughGlow(color, alpha, intensity);
            tinted.tintMaterials.Add((m, alpha, intensity));
            return m;
        }

        Material SeeThroughGlow(Color color, float alpha, float intensity)
        {
            var m = ViewModel.MakeTransparent(new Material(template));
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            materials.Add(m);
            keep.Add(m);
            return m;
        }

        // A faceted crystal: a bipyramid with `sides` flat faces, widest a third of the way up,
        // flat-shaded so every facet catches the light on its own
        public static Mesh CrystalMesh(float height, float radius, int sides, float twist)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float wide = height * 0.3f;
            Vector3 bottom = Vector3.zero, top = Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i + twist) * Mathf.PI * 2f / sides, a1 = (i + 1 + twist) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, wide, Mathf.Sin(a0) * radius * 0.45f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, wide, Mathf.Sin(a1) * radius * 0.45f);
                foreach (var (a, b, c) in new[] { (bottom, p1, p0), (p0, p1, top) })
                {
                    int s = verts.Count;
                    verts.Add(a); verts.Add(b); verts.Add(c);
                    tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                }
            }
            var mesh = new Mesh { name = "Crystal" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // Make every face point outward
            var t = mesh.triangles;
            var n = mesh.normals;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 centroid = (verts[t[i]] + verts[t[i + 1]] + verts[t[i + 2]]) / 3f;
                Vector3 outward = centroid - new Vector3(0f, centroid.y, 0f);
                Vector3 normal = Vector3.Cross(verts[t[i + 1]] - verts[t[i]], verts[t[i + 2]] - verts[t[i]]);
                if (Vector3.Dot(normal, outward) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            mesh.triangles = t;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A reaper's scythe in miniature: a long black haft with glowing bands and a spiked
        // pommel, a skull-and-collar head, and a big crescent blade sweeping out and down with a
        // back spike, its edge burning
        void Reaper(WeaponParts parts, Material finish)
        {
            // Held a third of the way up the haft, so the head sits in view
            var t = new GameObject("Scythe").transform;
            t.SetParent(parts.root, false);
            t.localPosition = new Vector3(0f, -0.11f, 0f);
            Material haft = Mat(new Color(0.04f, 0.035f, 0.035f), 0.75f, 0.3f);
            Material bone = Mat(new Color(0.85f, 0.82f, 0.74f), 0.6f, 0.1f);
            Material metal = Mat(new Color(0.12f, 0.12f, 0.13f), 0.9f, 0.95f);
            Material band = Glow(parts.hue, 2.4f, parts);
            Rod(t, haft, new Vector3(0f, -0.14f, 0f), new Vector3(0f, 0.25f, 0f), 0.016f);
            foreach (float y in new[] { -0.06f, 0.06f, 0.17f })
                Rod(t, band, new Vector3(0f, y - 0.004f, 0f), new Vector3(0f, y + 0.004f, 0f), 0.019f);
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, -0.155f, 0f), new Vector3(0.012f, 0.03f, 0.012f), Quaternion.Euler(0f, 45f, 45f));
            // Head: a collar and a small skull with glowing eyes
            Rod(t, metal, new Vector3(0f, 0.245f, 0f), new Vector3(0f, 0.275f, 0f), 0.026f);
            Part(t, PrimitiveType.Sphere, bone, new Vector3(0.012f, 0.29f, 0f), new Vector3(0.03f, 0.032f, 0.028f));
            Part(t, PrimitiveType.Cube, bone, new Vector3(0.015f, 0.274f, 0f), new Vector3(0.02f, 0.012f, 0.02f));
            foreach (float z in new[] { -0.006f, 0.006f })
                Part(t, PrimitiveType.Sphere, band, new Vector3(0.024f, 0.293f, z), Vector3.one * 0.006f);
            // Crescent blade: an arc out to the edge side and down
            const int n = 28;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            Vector2 center = new(-0.02f, 0.075f);
            const float radius = 0.2f;
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, a = Mathf.Lerp(80f, 178f, s) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float w = 0.056f * (1f - Mathf.Pow(s, 1.6f)) + 0.004f * Mathf.Sin(s * Mathf.PI);
                spine[i] = center + dir * (radius + w * 0.45f);
                edge[i] = center + dir * (radius - w * 0.55f);
            }
            MeshPart(t, finish, RailBlade(spine, edge, 0.0034f, 0.0004f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);
            // Back spike on the other side of the head
            var bs = new[] { new Vector2(0.02f, 0.285f), new Vector2(0.05f, 0.29f), new Vector2(0.075f, 0.3f), new Vector2(0.09f, 0.315f) };
            var be = new[] { new Vector2(0.02f, 0.26f), new Vector2(0.05f, 0.272f), new Vector2(0.075f, 0.29f), new Vector2(0.09f, 0.315f) };
            MeshPart(t, finish, RailBlade(bs, be, 0.003f, 0.0005f), Vector3.zero, Quaternion.identity);
        }

        // A plasma saber: a machined hilt with grip rings and an emitter, and a blade of light
        // (a white-hot core inside soft glowing halos)
        void Saber(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material metal = Mat(new Color(0.75f, 0.77f, 0.8f), 0.95f, 1f);
            Material dark = Mat(new Color(0.05f, 0.05f, 0.06f), 0.7f, 0.5f);
            Material band = Glow(parts.hue, 2.2f, parts);
            Rod(t, metal, new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.0f, 0f), 0.022f);
            for (int k = 0; k < 5; k++)
                Rod(t, dark, new Vector3(0f, -0.1f + k * 0.018f, 0f), new Vector3(0f, -0.092f + k * 0.018f, 0f), 0.025f);
            Rod(t, dark, new Vector3(0f, -0.128f, 0f), new Vector3(0f, -0.12f, 0f), 0.02f);
            MeshPart(t, metal, Torus(0.015f, 0.004f, 24, 8), new Vector3(0f, 0.004f, 0f), Quaternion.Euler(90f, 0f, 0f));
            Part(t, PrimitiveType.Cube, metal, new Vector3(0.016f, -0.01f, 0f), new Vector3(0.008f, 0.024f, 0.006f));
            Part(t, PrimitiveType.Cube, band, new Vector3(-0.012f, -0.04f, 0f), new Vector3(0.004f, 0.012f, 0.004f));
            // The blade
            Rod(t, finish, new Vector3(0f, 0.006f, 0f), new Vector3(0f, 0.3f, 0f), 0.011f);
            Part(t, PrimitiveType.Sphere, finish, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.011f);
            Rod(t, SeeThroughGlow(parts.hue, 0.35f, 2.5f), new Vector3(0f, 0.004f, 0f), new Vector3(0f, 0.306f, 0f), 0.02f);
            Rod(t, SeeThroughGlow(parts.hue, 0.14f, 2f), new Vector3(0f, 0.002f, 0f), new Vector3(0f, 0.312f, 0f), 0.034f);
            parts.tip = Tip(t, new Vector2(0f, 0.3f));
        }

        // A crystal dagger: a long faceted shard for a blade, a guard of smaller shards, a
        // wrapped grip and a crystal pommel, all glowing along their seams
        void Shard(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material wrap = Mat(new Color(0.08f, 0.05f, 0.1f), 0.5f, 0.2f);
            Material metal = Mat(new Color(0.2f, 0.18f, 0.24f), 0.9f, 0.9f);
            Rod(t, wrap, new Vector3(0f, -0.11f, 0f), new Vector3(0f, -0.005f, 0f), 0.02f);
            for (int k = 0; k < 4; k++)
                Rod(t, metal, new Vector3(0f, -0.1f + k * 0.026f, 0f), new Vector3(0f, -0.096f + k * 0.026f, 0f), 0.022f);
            MeshPart(t, finish, CrystalMesh(0.05f, 0.012f, 5, 0.2f), new Vector3(0f, -0.155f, 0f), Quaternion.identity);
            MeshPart(t, finish, CrystalMesh(0.26f, 0.024f, 6, 0f), new Vector3(0f, -0.004f, 0f), Quaternion.identity);
            foreach (float side in new[] { -1f, 1f })
            {
                MeshPart(t, finish, CrystalMesh(0.07f, 0.01f, 5, 0.3f), new Vector3(side * 0.012f, 0f, 0f), Quaternion.Euler(0f, 0f, -side * 62f));
                MeshPart(t, finish, CrystalMesh(0.045f, 0.008f, 5, 0.1f), new Vector3(side * 0.008f, 0.01f, 0f), Quaternion.Euler(0f, 0f, -side * 28f));
            }
            parts.tip = Tip(t, new Vector2(0f, 0.256f));
        }

        // The skin's material on its own (for gloves)
        public Material SkinMaterial(KnifeFinish finish) => FinishMaterial(finish, new WeaponParts());

        // A glove on its own, for displays and cases: the chunky glove block with its knuckle
        // plate, trim, cuff and a short sleeve, in the skin, plus any Void add-ons
        public WeaponParts GloveModel(Skins.Skin skin, Transform parent)
        {
            var root = new GameObject(skin.name).transform;
            root.SetParent(parent, false);
            var parts = new WeaponParts { root = root, model = skin.model, rarity = skin.rarity, hue = HueOf(skin.finish) };
            var size = new Vector3(0.076f, 0.082f, 0.071f);
            Material body = skin.finish == KnifeFinish.Polished ? Mat(new Color(0.07f, 0.07f, 0.08f), 0.4f, 0f) : FinishMaterial(skin.finish, parts);
            Material plate = skin.rarity == SkinRarity.Void ? Glow(parts.hue, 2.4f, parts) : Mat(new Color(0.03f, 0.03f, 0.035f), 0.7f, 0f);
            Material trim = Mat(new Color(0.3f, 0.3f, 0.33f), 0.5f, 0.2f);
            Material cuff = Mat(new Color(0.05f, 0.05f, 0.055f), 0.35f, 0f);
            Material sleeve = Mat(new Color(0.17f, 0.17f, 0.2f), 0.2f, 0f);
            Part(root, PrimitiveType.Cube, body, Vector3.zero, size);
            Part(root, PrimitiveType.Cube, plate, new Vector3(0f, 0.016f, size.z * 0.5f + 0.002f), new Vector3(0.06f, 0.036f, 0.006f));
            Part(root, PrimitiveType.Cube, trim, new Vector3(0f, -0.019f, 0f), new Vector3(size.x + 0.003f, 0.007f, size.z + 0.003f));
            Part(root, PrimitiveType.Cube, cuff, new Vector3(0f, -0.05f, 0f), new Vector3(0.083f, 0.021f, 0.078f));
            Part(root, PrimitiveType.Cube, sleeve, new Vector3(0f, -0.12f, 0f), new Vector3(0.069f, 0.12f, 0.064f));
            BuildGloveKit(skin, parts, root);
            parts.Animate(0f, -1f);
            return parts;
        }

        // Only the Void add-ons, built onto an existing arm (arm space: glove block centered on
        // the origin, fingers along +Y, back of the hand toward +Z)
        public WeaponParts GloveKit(Skins.Skin skin, Transform arm)
        {
            var root = new GameObject("Glove Kit").transform;
            root.SetParent(arm, false);
            var parts = new WeaponParts { root = root, model = skin.model, rarity = skin.rarity, hue = HueOf(skin.finish) };
            BuildGloveKit(skin, parts, root);
            parts.Animate(0f, -1f);
            return parts;
        }

        void BuildGloveKit(Skins.Skin skin, WeaponParts parts, Transform t)
        {
            if (skin.rarity != SkinRarity.Void) return;
            Material finish = FinishMaterial(skin.finish, parts);
            Material glow = Glow(parts.hue, 2.6f, parts);
            Material dark = Mat(new Color(0.08f, 0.08f, 0.09f), 0.9f, 0.95f);
            const float back = 0.0355f, top = 0.041f;
            switch (skin.model)
            {
                case KnifeModel.GloveArmor:
                    // Layered armour plates over the back, spiked knuckles, a bracer
                    for (int k = 0; k < 3; k++)
                        Part(t, PrimitiveType.Cube, k == 1 ? glow : finish, new Vector3(0f, -0.02f + k * 0.02f, back + 0.004f + k * 0.001f), new Vector3(0.074f - k * 0.006f, 0.024f, 0.006f), Quaternion.Euler(-12f, 0f, 0f));
                    for (int k = 0; k < 4; k++)
                        MeshPart(t, finish, CrystalMesh(0.028f, 0.006f, 4, 0.5f), new Vector3(-0.027f + k * 0.018f, 0.03f, back + 0.004f), Quaternion.Euler(70f, 0f, 0f));
                    Part(t, PrimitiveType.Cube, finish, new Vector3(0f, -0.05f, 0f), new Vector3(0.09f, 0.028f, 0.086f));
                    Part(t, PrimitiveType.Cube, glow, new Vector3(0f, -0.05f, 0.0435f), new Vector3(0.06f, 0.006f, 0.002f));
                    break;
                case KnifeModel.GloveClaws:
                    // Three curved talons out of the knuckles, curling toward the palm
                    for (int c = -1; c <= 1; c++)
                    {
                        const int n = 14;
                        var spine = new Vector2[n + 1];
                        var edge = new Vector2[n + 1];
                        for (int i = 0; i <= n; i++)
                        {
                            float s = (float)i / n, a = s * 75f * Mathf.Deg2Rad;
                            var center = new Vector2(-0.07f + 0.07f * Mathf.Cos(a), 0.07f * Mathf.Sin(a));
                            var outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            float w = 0.014f * (1f - Mathf.Pow(s, 1.4f));
                            spine[i] = center + outward * w * 0.5f;
                            edge[i] = center - outward * w * 0.5f;
                        }
                        MeshPart(t, finish, RailBlade(spine, edge, 0.0025f, 0.0004f), new Vector3(c * 0.022f, top - 0.004f, 0.012f), Quaternion.Euler(0f, -90f, 0f));
                    }
                    Part(t, PrimitiveType.Cube, glow, new Vector3(0f, top - 0.006f, back + 0.002f), new Vector3(0.06f, 0.004f, 0.003f));
                    break;
                case KnifeModel.GloveRunes:
                {
                    // A ring of glowing runes orbiting the wrist, and a smaller one at the knuckles
                    MeshPart(t, glow, Torus(0.066f, 0.0025f, 36, 6), new Vector3(0f, -0.055f, 0f), Quaternion.Euler(90f, 0f, 0f));
                    MeshPart(t, glow, Torus(0.05f, 0.002f, 32, 6), new Vector3(0f, 0.03f, 0f), Quaternion.Euler(90f, 0f, 0f));
                    var holder = new GameObject("Aura").transform;
                    holder.SetParent(t, false);
                    parts.aura = holder;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI * 2f / 8f;
                        var home = new Vector3(Mathf.Cos(a) * 0.066f, -0.055f + (k % 2) * 0.01f, Mathf.Sin(a) * 0.066f);
                        var rune = Part(holder, PrimitiveType.Cube, glow, home, new Vector3(0.012f, 0.016f, 0.003f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f));
                        parts.motes.Add((rune, home, k * 1.3f));
                    }
                    break;
                }
                case KnifeModel.GloveScales:
                    // Overlapping scales down the back, glowing between them
                    Part(t, PrimitiveType.Cube, glow, new Vector3(0f, 0f, back + 0.001f), new Vector3(0.07f, 0.075f, 0.001f));
                    for (int row = 0; row < 4; row++)
                    for (int col = 0; col < 4 - row % 2; col++)
                    {
                        float x = -0.027f + col * 0.018f + (row % 2) * 0.009f;
                        Part(t, PrimitiveType.Sphere, finish, new Vector3(x, 0.03f - row * 0.02f, back + 0.004f), new Vector3(0.02f, 0.024f, 0.008f), Quaternion.Euler(-20f, 0f, 0f));
                    }
                    break;
                case KnifeModel.GloveKnuckles:
                    // Glowing rings across the knuckles and a coil of energy up the wrist
                    for (int k = 0; k < 4; k++)
                        MeshPart(t, glow, Torus(0.009f, 0.003f, 20, 6), new Vector3(-0.027f + k * 0.018f, top - 0.004f, 0.02f), Quaternion.Euler(90f, 0f, 0f));
                    foreach (float y in new[] { -0.045f, -0.075f, -0.105f })
                        MeshPart(t, glow, Torus(0.05f, 0.003f, 32, 6), new Vector3(0f, y, 0f), Quaternion.Euler(90f, 0f, 0f));
                    Part(t, PrimitiveType.Cube, dark, new Vector3(0f, top - 0.006f, 0.03f), new Vector3(0.078f, 0.012f, 0.014f));
                    break;
                case KnifeModel.GloveBone:
                {
                    // Finger bones over the back and a small skull at the wrist with burning eyes
                    Material bone = Mat(new Color(0.88f, 0.84f, 0.74f), 0.6f, 0.1f);
                    for (int k = 0; k < 4; k++)
                    {
                        float x = -0.027f + k * 0.018f;
                        Rod(t, bone, new Vector3(x, -0.02f, back + 0.004f), new Vector3(x, 0.036f, back + 0.004f), 0.007f);
                        Part(t, PrimitiveType.Sphere, bone, new Vector3(x, 0.038f, back + 0.004f), Vector3.one * 0.011f);
                    }
                    Part(t, PrimitiveType.Sphere, bone, new Vector3(0f, -0.045f, back + 0.012f), new Vector3(0.034f, 0.034f, 0.03f));
                    foreach (float x in new[] { -0.008f, 0.008f })
                        Part(t, PrimitiveType.Sphere, glow, new Vector3(x, -0.042f, back + 0.026f), Vector3.one * 0.008f);
                    break;
                }
                case KnifeModel.GloveCrystal:
                {
                    // Crystal shards bursting out of the back of the hand
                    var random = new System.Random(3);
                    for (int k = 0; k < 7; k++)
                    {
                        var at = new Vector3(((float)random.NextDouble() - 0.5f) * 0.06f, ((float)random.NextDouble() - 0.5f) * 0.06f, back);
                        var tilt = Quaternion.Euler(90f + ((float)random.NextDouble() - 0.5f) * 50f, 0f, ((float)random.NextDouble() - 0.5f) * 60f);
                        MeshPart(t, finish, CrystalMesh(0.025f + (float)random.NextDouble() * 0.03f, 0.007f, 5, 0.3f), at, tilt);
                    }
                    break;
                }
                case KnifeModel.GloveWings:
                    // Small feathered wings swept back from the wrist, and a halo around it
                    foreach (float side in new[] { -1f, 1f })
                        for (int f = 0; f < 3; f++)
                        {
                            float len = 0.07f - f * 0.012f;
                            var a = new[] { new Vector2(0f, 0f), new Vector2(len * 0.5f, 0.01f), new Vector2(len, 0.002f) };
                            var b = new[] { new Vector2(0f, -0.012f), new Vector2(len * 0.5f, -0.006f), new Vector2(len, 0.002f) };
                            MeshPart(t, f == 1 ? glow : finish, RailBlade(a, b, 0.002f, 0.0008f),
                                new Vector3(side * 0.042f, -0.04f + f * 0.008f, 0f), Quaternion.Euler(0f, side > 0f ? 0f : 180f, -70f - f * 12f));
                        }
                    MeshPart(t, glow, Torus(0.06f, 0.0025f, 36, 6), new Vector3(0f, -0.07f, 0f), Quaternion.Euler(90f, 0f, 0f));
                    break;
                case KnifeModel.GloveStorm:
                    // Coils on the wrist and prongs on the knuckles, crackling at the tips
                    foreach (float y in new[] { -0.045f, -0.065f })
                        MeshPart(t, finish, Torus(0.048f, 0.005f, 32, 8), new Vector3(0f, y, 0f), Quaternion.Euler(90f, 0f, 0f));
                    for (int k = 0; k < 4; k++)
                    {
                        float x = -0.027f + k * 0.018f;
                        Rod(t, dark, new Vector3(x, top - 0.004f, back - 0.004f), new Vector3(x, top + 0.02f, back + 0.006f), 0.004f);
                        Part(t, PrimitiveType.Sphere, glow, new Vector3(x, top + 0.021f, back + 0.006f), Vector3.one * 0.008f);
                    }
                    break;
                case KnifeModel.GloveWraps:
                {
                    // Dark bandage wraps with a burning stripe, and a hooked blade off the wrist
                    Material cloth = Mat(new Color(0.12f, 0.1f, 0.1f), 0.2f, 0f);
                    for (int k = 0; k < 4; k++)
                        Part(t, PrimitiveType.Cube, k == 2 ? glow : cloth, new Vector3(0f, -0.03f + k * 0.02f, 0f), new Vector3(0.08f, 0.008f, 0.075f), Quaternion.Euler(0f, 0f, k % 2 == 0 ? 12f : -12f));
                    var s = new[] { new Vector2(0f, 0f), new Vector2(0.03f, 0.012f), new Vector2(0.055f, 0.005f), new Vector2(0.065f, -0.015f) };
                    var e = new[] { new Vector2(0f, -0.014f), new Vector2(0.03f, -0.004f), new Vector2(0.05f, -0.006f), new Vector2(0.065f, -0.015f) };
                    MeshPart(t, finish, RailBlade(s, e, 0.0025f, 0.0005f), new Vector3(0.04f, -0.05f, 0f), Quaternion.Euler(0f, 0f, -80f));
                    break;
                }
            }
            // Flames licking up off the knuckles
            AddFlames(parts, new List<Vector3> { new(-0.025f, top, back), new(0f, top + 0.004f, back), new(0.025f, top, back) }, 0.05f, 0.06f);
        }

        // The Void Case, for its picture: a dark armoured crate with glowing edges, a burning
        // emblem on the front, chrome corners and handle, crystals breaking out of the lid seam
        // and violet flames licking up around the top
        public WeaponParts VoidCaseModel(Transform parent)
        {
            var root = new GameObject("Void Case").transform;
            root.SetParent(parent, false);
            var parts = new WeaponParts { root = root, rarity = SkinRarity.Void, hue = new Color(0.85f, 0.3f, 1f) };
            Color violet = new(0.62f, 0.2f, 1f), pink = new(1f, 0.3f, 0.7f);
            Material shell = Mat(new Color(0.1f, 0.05f, 0.18f), 0.93f, 0.8f);
            Material panel = Mat(new Color(0.05f, 0.03f, 0.08f), 0.7f, 0.5f);
            Material chrome = Mat(new Color(0.88f, 0.88f, 0.95f), 0.97f, 1f);
            Material edge = Glow(violet, 3.4f, parts);
            Material hot = Glow(pink, 4f, parts);
            Material white = Glow(new Color(1f, 0.85f, 1f), 5f, parts);
            const float w = 1f, h = 0.62f, d = 0.6f, e = 0.028f;
            var t = root;
            Part(t, PrimitiveType.Cube, shell, Vector3.zero, new Vector3(w, h, d));
            // Inset front and side panels
            Part(t, PrimitiveType.Cube, panel, new Vector3(0f, -0.05f, -d / 2f - 0.004f), new Vector3(w - 0.14f, h * 0.55f, 0.01f));
            foreach (float sx in new[] { -1f, 1f })
                Part(t, PrimitiveType.Cube, panel, new Vector3(sx * (w / 2f + 0.004f), -0.05f, 0f), new Vector3(0.01f, h * 0.55f, d - 0.14f));
            // Lid seam
            Part(t, PrimitiveType.Cube, hot, new Vector3(0f, h * 0.2f, 0f), new Vector3(w + 0.014f, 0.032f, d + 0.014f));
            // Glowing edges all round
            foreach (float a in new[] { -1f, 1f })
            foreach (float b in new[] { -1f, 1f })
            {
                Part(t, PrimitiveType.Cube, edge, new Vector3(0f, a * h / 2f, b * d / 2f), new Vector3(w + e, e, e));
                Part(t, PrimitiveType.Cube, edge, new Vector3(a * w / 2f, 0f, b * d / 2f), new Vector3(e, h + e, e));
                Part(t, PrimitiveType.Cube, edge, new Vector3(a * w / 2f, b * h / 2f, 0f), new Vector3(e, e, d + e));
            }
            // Chrome corner caps
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                Part(t, PrimitiveType.Cube, chrome, new Vector3(x * w / 2f, y * h / 2f, z * d / 2f), Vector3.one * 0.075f);
            // Emblem: a burning ring with a V inside
            var front = new Vector3(0f, -0.05f, -d / 2f - 0.016f);
            MeshPart(t, hot, Torus(0.15f, 0.018f, 40, 8), front, Quaternion.identity);
            MeshPart(t, edge, Torus(0.19f, 0.006f, 40, 6), front, Quaternion.identity);
            foreach (float sx in new[] { -1f, 1f })
                Part(t, PrimitiveType.Cube, white, front + new Vector3(sx * 0.042f, 0.012f, -0.004f), new Vector3(0.034f, 0.19f, 0.02f), Quaternion.Euler(0f, 0f, sx * 22f));
            // Latches and handle
            foreach (float sx in new[] { -0.3f, 0.3f })
                Part(t, PrimitiveType.Cube, chrome, new Vector3(sx, h * 0.2f, -d / 2f - 0.02f), new Vector3(0.07f, 0.1f, 0.035f));
            foreach (float sx in new[] { -0.18f, 0.18f }) Rod(t, chrome, new Vector3(sx, h / 2f, 0f), new Vector3(sx, h / 2f + 0.08f, 0f), 0.03f);
            Rod(t, chrome, new Vector3(-0.2f, h / 2f + 0.08f, 0f), new Vector3(0.2f, h / 2f + 0.08f, 0f), 0.032f);
            // Crystals breaking out of the seam
            var random = new System.Random(7);
            for (int k = 0; k < 6; k++)
            {
                float x = (k % 3 - 1) * 0.36f + ((float)random.NextDouble() - 0.5f) * 0.08f;
                float z = (k < 3 ? -1f : 1f) * (d / 2f - 0.04f);
                var tiltBy = Quaternion.Euler((k < 3 ? -1f : 1f) * (25f + (float)random.NextDouble() * 20f), 0f, ((float)random.NextDouble() - 0.5f) * 50f);
                MeshPart(t, k % 2 == 0 ? hot : edge, CrystalMesh(0.16f + (float)random.NextDouble() * 0.14f, 0.035f, 5, 0.3f), new Vector3(x, h * 0.22f, z), tiltBy);
            }
            // Flames around the top
            var at = new List<Vector3>();
            for (int k = 0; k < 7; k++) at.Add(new Vector3(-0.42f + k * 0.14f, h / 2f, -d / 2f + 0.06f));
            for (int k = 0; k < 5; k++) at.Add(new Vector3(-0.36f + k * 0.18f, h / 2f, d / 2f - 0.06f));
            AddFlames(parts, at, 0.26f, 0.3f);
            return parts;
        }

        // A plain dark grip with glowing bands and a pommel, for the Void specials
        void VoidGrip(WeaponParts parts, Transform t)
        {
            Material wrap = Mat(new Color(0.05f, 0.045f, 0.05f), 0.6f, 0.3f);
            Material metal = Mat(new Color(0.16f, 0.16f, 0.18f), 0.95f, 1f);
            Material band = Glow(parts.hue, 2.4f, parts);
            Rod(t, wrap, new Vector3(0f, -0.115f, 0f), new Vector3(0f, -0.004f, 0f), 0.021f);
            foreach (float y in new[] { -0.09f, -0.055f, -0.02f })
                Rod(t, band, new Vector3(0f, y - 0.003f, 0f), new Vector3(0f, y + 0.003f, 0f), 0.023f);
            Part(t, PrimitiveType.Sphere, metal, new Vector3(0f, -0.122f, 0f), new Vector3(0.026f, 0.018f, 0.026f));
        }

        // A kukri: a heavy blade that bends forward with a big belly, a notch by the guard
        void Kukri(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            VoidGrip(parts, t);
            Part(t, PrimitiveType.Cube, Mat(new Color(0.2f, 0.2f, 0.22f), 0.95f, 1f), new Vector3(0f, 0.003f, 0f), new Vector3(0.05f, 0.008f, 0.024f));
            const int n = 24;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, y = 0.008f + s * 0.24f;
                float bend = -0.07f * s * s;
                float w = (0.026f + 0.024f * Mathf.Sin(Mathf.Min(s * 1.3f, 1f) * Mathf.PI)) * (1f - Mathf.Pow(s, 4f));
                spine[i] = new Vector2(bend + 0.012f, y);
                edge[i] = new Vector2(bend + 0.012f - w, y - 0.01f * s);
            }
            MeshPart(t, finish, RailBlade(spine, edge, 0.0036f, 0.0004f), Vector3.zero, Quaternion.identity);
            Part(t, PrimitiveType.Cylinder, Mat(new Color(0.02f, 0.02f, 0.02f), 0.2f, 0f), new Vector3(-0.012f, 0.02f, 0f), new Vector3(0.008f, 0.004f, 0.008f), Quaternion.Euler(90f, 0f, 0f));
            parts.tip = Tip(t, spine[^1]);
        }

        // Dragon claws: three curved talons fanning out of a knuckle guard
        void Claws(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            VoidGrip(parts, t);
            Material metal = Mat(new Color(0.14f, 0.13f, 0.14f), 0.95f, 1f);
            Part(t, PrimitiveType.Cube, metal, new Vector3(-0.004f, 0.008f, 0f), new Vector3(0.05f, 0.014f, 0.042f));
            Part(t, PrimitiveType.Cube, Glow(parts.hue, 2.4f, parts), new Vector3(-0.004f, 0.008f, 0.0215f), new Vector3(0.04f, 0.004f, 0.002f));
            for (int c = -1; c <= 1; c++)
            {
                const int n = 18;
                var spine = new Vector2[n + 1];
                var edge = new Vector2[n + 1];
                float length = 0.2f - Mathf.Abs(c) * 0.035f;
                for (int i = 0; i <= n; i++)
                {
                    float s = (float)i / n, a = s * 70f * Mathf.Deg2Rad;
                    var center = new Vector2(-0.12f + 0.12f * Mathf.Cos(a), 0.015f + 0.12f * Mathf.Sin(a) * length / 0.2f);
                    var outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float w = 0.02f * (1f - Mathf.Pow(s, 1.4f));
                    spine[i] = center + outward * w * 0.5f;
                    edge[i] = center - outward * w * 0.5f;
                }
                var claw = MeshPart(t, finish, RailBlade(spine, edge, 0.003f, 0.0004f), new Vector3(0f, 0f, c * 0.015f), Quaternion.Euler(c * 8f, 0f, 0f));
                if (c == 0) parts.tip = Tip(claw, spine[^1]);
            }
        }

        // A double-headed battle axe: the haft runs on up past the hand to two crescent heads and
        // a spike
        void Axe(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            VoidGrip(parts, t);
            Material metal = Mat(new Color(0.14f, 0.13f, 0.15f), 0.95f, 1f);
            Rod(t, Mat(new Color(0.05f, 0.04f, 0.04f), 0.7f, 0.3f), new Vector3(0f, -0.004f, 0f), new Vector3(0f, 0.2f, 0f), 0.016f);
            Rod(t, metal, new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0.2f, 0f), 0.024f);
            foreach (float side in new[] { -1f, 1f })
            {
                const int n = 20;
                var outer = new Vector2[n + 1];
                var inner = new Vector2[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    float s = (float)i / n, a = Mathf.Lerp(-70f, 70f, s) * Mathf.Deg2Rad;
                    float r = 0.085f;
                    outer[i] = new Vector2(side * (0.01f + r * Mathf.Cos(a) * (0.55f + 0.45f * Mathf.Cos(a))), 0.17f + r * Mathf.Sin(a));
                    inner[i] = new Vector2(side * 0.012f, 0.17f + Mathf.Lerp(-0.035f, 0.035f, s));
                }
                MeshPart(t, finish, RailBlade(inner, outer, 0.004f, 0.0005f), Vector3.zero, Quaternion.identity);
            }
            MeshPart(t, finish, CrystalMesh(0.08f, 0.012f, 4, 0.5f), new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            parts.tip = Tip(t, new Vector2(0f, 0.28f));
        }

        // A sai: a long central prong and two curved side prongs
        void Sai(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            VoidGrip(parts, t);
            MeshPart(t, finish, CrystalMesh(0.25f, 0.009f, 4, 0.5f), new Vector3(0f, 0f, 0f), Quaternion.identity);
            foreach (float side in new[] { -1f, 1f })
            {
                Rod(t, finish, new Vector3(0f, 0.004f, 0f), new Vector3(side * 0.035f, 0.02f, 0f), 0.008f);
                Rod(t, finish, new Vector3(side * 0.035f, 0.02f, 0f), new Vector3(side * 0.04f, 0.07f, 0f), 0.007f);
                MeshPart(t, finish, CrystalMesh(0.03f, 0.005f, 4, 0.5f), new Vector3(side * 0.04f, 0.07f, 0f), Quaternion.identity);
            }
            MeshPart(t, Glow(parts.hue, 2.4f, parts), Torus(0.013f, 0.003f, 20, 6), new Vector3(0f, 0.004f, 0f), Quaternion.Euler(90f, 0f, 0f));
            parts.tip = Tip(t, new Vector2(0f, 0.25f));
        }

        // A short spear: the haft runs on up to a leaf-shaped head with swept wings and a
        // glowing ring
        void Spear(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            VoidGrip(parts, t);
            Rod(t, Mat(new Color(0.06f, 0.05f, 0.05f), 0.7f, 0.3f), new Vector3(0f, -0.004f, 0f), new Vector3(0f, 0.13f, 0f), 0.014f);
            MeshPart(t, Glow(parts.hue, 2.4f, parts), Torus(0.012f, 0.0035f, 20, 6), new Vector3(0f, 0.12f, 0f), Quaternion.Euler(90f, 0f, 0f));
            const int n = 20;
            var left = new Vector2[n + 1];
            var right = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, y = 0.13f + s * 0.17f;
                float w = 0.03f * Mathf.Sin(Mathf.Pow(s, 0.7f) * Mathf.PI) * (1f - s * 0.2f);
                right[i] = new Vector2(w, y);
                left[i] = new Vector2(-w, y);
            }
            MeshPart(t, finish, RailBlade(right, left, 0.0035f, 0.0035f), Vector3.zero, Quaternion.identity);
            foreach (float side in new[] { -1f, 1f })
            {
                var a = new[] { new Vector2(side * 0.006f, 0.125f), new Vector2(side * 0.03f, 0.115f), new Vector2(side * 0.05f, 0.095f) };
                var b = new[] { new Vector2(side * 0.006f, 0.14f), new Vector2(side * 0.028f, 0.128f), new Vector2(side * 0.05f, 0.095f) };
                MeshPart(t, finish, RailBlade(a, b, 0.003f, 0.001f), Vector3.zero, Quaternion.identity);
            }
            parts.tip = Tip(t, new Vector2(0f, 0.3f));
        }

        // A kris: a wavy, rippling blade flaring wide at the guard
        void Kris(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            VoidGrip(parts, t);
            const int n = 32;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, y = 0.006f + s * 0.25f;
                float wave = Mathf.Sin(s * Mathf.PI * 5f) * 0.008f * (1f - s * 0.5f);
                float w = Mathf.Lerp(0.04f, 0.018f, Mathf.Sqrt(s)) * (1f - Mathf.Pow(s, 5f));
                spine[i] = new Vector2(wave + w * 0.5f, y);
                edge[i] = new Vector2(wave - w * 0.5f, y);
            }
            MeshPart(t, finish, RailBlade(spine, edge, 0.0028f, 0.0006f), Vector3.zero, Quaternion.identity);
            Part(t, PrimitiveType.Cube, Mat(new Color(0.16f, 0.15f, 0.17f), 0.95f, 1f), new Vector3(0.006f, 0.004f, 0f), new Vector3(0.06f, 0.01f, 0.02f), Quaternion.Euler(0f, 0f, -12f));
            parts.tip = Tip(t, spine[^1]);
        }

        // Every Void rifle gets a glowing body: seams along the receiver, stock, forend and
        // scope, rings around the scope, a burning lens, inlays on the stock, and motes
        // orbiting the gun
        void VoidRifleKit(WeaponParts parts, Transform t)
        {
            Material seam = Glow(parts.hue, 2.6f, parts);
            Material soft = Glow(parts.hue, 1.6f, parts);
            foreach (float x in new[] { -1f, 1f })
            {
                Part(t, PrimitiveType.Cube, seam, new Vector3(x * 0.0245f, 0.007f, 0.07f), new Vector3(0.0025f, 0.0035f, 0.3f));
                Part(t, PrimitiveType.Cube, seam, new Vector3(x * 0.0215f, 0.044f, 0.06f), new Vector3(0.0025f, 0.003f, 0.3f));
                Part(t, PrimitiveType.Cube, seam, new Vector3(x * 0.0195f, 0.043f, -0.2f), new Vector3(0.0025f, 0.003f, 0.32f));
                Part(t, PrimitiveType.Cube, seam, new Vector3(x * 0.0195f, -0.068f, -0.25f), new Vector3(0.0025f, 0.003f, 0.22f), Quaternion.Euler(-9f, 0f, 0f));
                Part(t, PrimitiveType.Cube, seam, new Vector3(x * 0.0205f, -0.018f, -0.37f), new Vector3(0.0025f, 0.12f, 0.003f));
                Part(t, PrimitiveType.Cube, seam, new Vector3(x * 0.0225f, -0.016f, 0.33f), new Vector3(0.0025f, 0.003f, 0.24f));
                Part(t, PrimitiveType.Cube, soft, new Vector3(x * 0.0198f, -0.02f, -0.27f), new Vector3(0.002f, 0.007f, 0.12f), Quaternion.Euler(32f, 0f, 0f));
                Part(t, PrimitiveType.Cube, soft, new Vector3(x * 0.0198f, -0.005f, -0.24f), new Vector3(0.002f, 0.005f, 0.07f), Quaternion.Euler(-28f, 0f, 0f));
                Part(t, PrimitiveType.Cube, seam, new Vector3(x * 0.0165f, 0.098f, 0.065f), new Vector3(0.002f, 0.003f, 0.3f));
            }
            foreach (float z in new[] { -0.06f, 0.1f, 0.2f })
                MeshPart(t, seam, Torus(0.0175f, 0.0028f, 24, 6), new Vector3(0f, 0.098f, z), Quaternion.identity);
            MeshPart(t, seam, Torus(0.034f, 0.003f, 28, 6), new Vector3(0f, 0.098f, 0.34f), Quaternion.identity);
            Rod(t, SeeThroughGlow(parts.hue, 0.8f, 3f, parts), new Vector3(0f, 0.098f, 0.346f), new Vector3(0f, 0.098f, 0.35f), 0.056f);
            Rod(t, SeeThroughGlow(parts.hue, 0.8f, 3f, parts), new Vector3(0f, 0.098f, -0.172f), new Vector3(0f, 0.098f, -0.175f), 0.034f);
            // Motes orbiting the rifle
            var holder = new GameObject("Aura").transform;
            holder.SetParent(t, false);
            holder.localPosition = new Vector3(0f, 0.03f, 0.25f);
            parts.aura = holder;
            Material mote = Glow(parts.hue, 3f, parts);
            var random = new System.Random(parts.hue.GetHashCode());
            for (int i = 0; i < 14; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f, r = 0.08f + (float)random.NextDouble() * 0.06f;
                var home = new Vector3(Mathf.Cos(a) * r, ((float)random.NextDouble() - 0.5f) * 0.08f, Mathf.Sin(a) * r * 3f);
                var m = Part(holder, PrimitiveType.Cube, mote, home, Vector3.one * 0.008f, Quaternion.Euler(45f, 45f, 0f));
                parts.motes.Add((m, home, (float)random.NextDouble() * 10f));
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
            var parts = new WeaponParts { root = t, model = skin.model, rarity = skin.rarity, hue = HueOf(skin.finish) };
            bool rail = skin.model == KnifeModel.Railgun, hell = skin.model == KnifeModel.Hellfire, lance = skin.model == KnifeModel.Lance;
            Material metal = Mat(new Color(0.1f, 0.1f, 0.11f), 0.5f, 0.6f);
            Material metalLight = Mat(new Color(0.28f, 0.28f, 0.3f), 0.45f, 0.6f);
            Material chassis = skin.finish == KnifeFinish.Polished ? Mat(new Color(0.3f, 0.33f, 0.2f), 0.25f, 0.05f) : FinishMaterial(skin.finish, parts);
            Material rubber = Mat(new Color(0.04f, 0.04f, 0.04f), 0.15f, 0f);
            Material lens = Mat(new Color(0.06f, 0.1f, 0.2f), 0.95f, 0.4f);
            Material red = Mat(new Color(0.85f, 0.1f, 0.06f), 0.4f, 0.1f);

            // Classic arctic-style sniper silhouette (an original build): thumbhole stock, a long
            // squared receiver, a slim forend, a long thin fluted barrel with a muzzle brake, and a
            // long scope with a big objective bell. The skin's finish goes on the stock, receiver
            // shell, forend and scope.
            //
            // Stock: one moulded piece cut from its side outline, the classic thumbhole sniper
            // stock: a pistol grip standing in the thumbhole, a skeleton butt with an open window
            // under the cheek rest, a trigger-guard opening, and a long flat forend
            var stockOutline = new Vector2[]
            {
                new(0.49f, -0.028f), new(0.5f, -0.01f), new(0.49f, 0.008f), new(0.2f, 0.01f), new(-0.1f, 0.01f),
                new(-0.13f, 0.03f), new(-0.16f, 0.052f), new(-0.2f, 0.06f), new(-0.36f, 0.06f), new(-0.395f, 0.055f),
                new(-0.405f, 0.03f), new(-0.405f, -0.075f), new(-0.395f, -0.1f), new(-0.36f, -0.105f), new(-0.25f, -0.1f),
                new(-0.14f, -0.11f), new(-0.1f, -0.118f), new(-0.07f, -0.118f), new(-0.055f, -0.1f), new(-0.042f, -0.045f),
                new(-0.036f, -0.012f), new(0.03f, -0.012f), new(0.036f, -0.03f), new(0.12f, -0.035f), new(0.3f, -0.04f), new(0.45f, -0.036f),
            };
            var thumbhole = new Vector2[]
            {
                new(-0.095f, -0.02f), new(-0.1f, -0.07f), new(-0.115f, -0.085f), new(-0.2f, -0.08f), new(-0.22f, -0.06f),
                new(-0.215f, 0f), new(-0.19f, 0.03f), new(-0.14f, 0.02f), new(-0.105f, 0.005f),
            };
            var buttWindow = new Vector2[]
            {
                new(-0.37f, -0.07f), new(-0.372f, 0.02f), new(-0.35f, 0.032f), new(-0.3f, 0.032f), new(-0.262f, 0.02f), new(-0.255f, -0.06f), new(-0.27f, -0.075f),
            };
            MeshPart(t, chassis, ProfileMesh.Extrude(stockOutline, new[] { thumbhole, buttWindow }, 0.022f, 0.005f, "Stock"), Vector3.zero, Quaternion.identity);
            Part(t, PrimitiveType.Cube, rubber, new Vector3(0f, -0.022f, -0.41f), new Vector3(0.046f, 0.16f, 0.012f));                                   // butt pad
            Part(t, PrimitiveType.Cube, rubber, new Vector3(0f, 0.064f, -0.28f), new Vector3(0.034f, 0.01f, 0.14f));                                     // cheek rest
            // Trigger guard
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, -0.036f, 0.01f), new Vector3(0.008f, 0.006f, 0.07f));
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, -0.022f, 0.043f), new Vector3(0.008f, 0.03f, 0.006f));
            Part(t, PrimitiveType.Cube, metalLight, new Vector3(0f, -0.02f, 0.005f), new Vector3(0.006f, 0.02f, 0.006f), Quaternion.Euler(15f, 0f, 0f));
            // Receiver: a round steel action sitting in the stock, with an ejection port and the
            // scope rail on top
            Rod(t, metal, new Vector3(0f, 0.027f, -0.1f), new Vector3(0f, 0.027f, 0.21f), 0.04f);
            Part(t, PrimitiveType.Sphere, metal, new Vector3(0f, 0.027f, -0.1f), new Vector3(0.04f, 0.04f, 0.03f));
            Part(t, PrimitiveType.Cube, rubber, new Vector3(0.019f, 0.03f, 0.03f), new Vector3(0.004f, 0.018f, 0.08f));
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, 0.05f, 0.07f), new Vector3(0.024f, 0.01f, 0.34f));                                       // scope rail
            var burn = new List<Vector3>();
            if (lance)
            {
                // Plasma lance: the barrel is a beam of light, a white-hot core in glowing
                // halos, held by emitter rings and ending in a forked crown
                Material core = Glow(Color.Lerp(parts.hue, Color.white, 0.6f), 4f, parts);
                Rod(t, metal, new Vector3(0f, 0.018f, 0.2f), new Vector3(0f, 0.018f, 0.34f), 0.024f);
                Rod(t, core, new Vector3(0f, 0.018f, 0.34f), new Vector3(0f, 0.018f, 1.08f), 0.01f);
                Rod(t, SeeThroughGlow(parts.hue, 0.4f, 2.6f), new Vector3(0f, 0.018f, 0.34f), new Vector3(0f, 0.018f, 1.09f), 0.022f);
                Rod(t, SeeThroughGlow(parts.hue, 0.16f, 2f), new Vector3(0f, 0.018f, 0.34f), new Vector3(0f, 0.018f, 1.1f), 0.04f);
                for (int k = 0; k < 5; k++)
                    MeshPart(t, metal, Torus(0.02f, 0.004f, 24, 8), new Vector3(0f, 0.018f, 0.4f + k * 0.16f), Quaternion.identity);
                for (int k = 0; k < 3; k++)
                {
                    var dir = Quaternion.Euler(0f, 0f, k * 120f) * Vector3.up;
                    Rod(t, metal, new Vector3(0f, 0.018f, 1.0f) + dir * 0.012f, new Vector3(0f, 0.018f, 1.12f) + dir * 0.03f, 0.006f);
                }
                for (int k = 0; k < 5; k++) burn.Add(new Vector3(0f, 0.03f, 0.4f + k * 0.16f));
            }
            else if (rail)
            {
                // Railgun: two long rails with a glowing energy core between them, wrapped in
                // coil rings, ending in forked emitter prongs
                Material coil = Glow(parts.hue, 2.6f, parts);
                foreach (float y in new[] { 0.036f, 0.0f })
                    Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, y, 0.62f), new Vector3(0.02f, 0.012f, 0.84f));
                Rod(t, coil, new Vector3(0f, 0.018f, 0.2f), new Vector3(0f, 0.018f, 1.02f), 0.007f);
                Rod(t, SeeThroughGlow(parts.hue, 0.25f, 2f, parts), new Vector3(0f, 0.018f, 0.2f), new Vector3(0f, 0.018f, 1.02f), 0.02f);
                for (int k = 0; k < 8; k++)
                {
                    float z = 0.3f + k * 0.09f;
                    MeshPart(t, k % 2 == 0 ? coil : metal, Torus(0.024f, 0.0045f, 24, 8), new Vector3(0f, 0.018f, z), Quaternion.identity);
                }
                foreach (float s in new[] { -1f, 1f })
                    Rod(t, metal, new Vector3(s * 0.012f, 0.018f, 1.0f), new Vector3(s * 0.022f, 0.018f, 1.1f), 0.008f);
                for (int k = 0; k < 5; k++) burn.Add(new Vector3(0f, 0.03f, 0.3f + k * 0.18f));
            }
            else
            {
                // Barrel: long and thin, with flutes, a gas-block collar and a muzzle brake
                Rod(t, metalLight, new Vector3(0f, 0.018f, 0.2f), new Vector3(0f, 0.018f, 1.02f), 0.017f);
                for (int k = 0; k < 6; k++)
                    Rod(t, hell ? Glow(parts.hue, 2.2f, parts) : metal, new Vector3(0f, 0.018f, 0.55f + k * 0.065f), new Vector3(0f, 0.018f, 0.57f + k * 0.065f), 0.0195f);
                if (hell)
                {
                    // Hellfire: a flared dragon-mouth muzzle with fangs, gold trim and horns
                    Material gold = Mat(new Color(1f, 0.72f, 0.25f), 0.95f, 1f); keep.Add(gold);
                    Rod(t, gold, new Vector3(0f, 0.018f, 1.0f), new Vector3(0f, 0.018f, 1.04f), 0.03f);
                    Rod(t, metal, new Vector3(0f, 0.018f, 1.04f), new Vector3(0f, 0.018f, 1.1f), 0.042f);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * 60f;
                        var dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
                        Part(t, PrimitiveType.Cube, gold, new Vector3(0f, 0.018f, 1.105f) + dir * 0.02f, new Vector3(0.006f, 0.014f, 0.02f), Quaternion.Euler(0f, 0f, a) * Quaternion.Euler(30f, 0f, 0f));
                    }
                    Part(t, PrimitiveType.Cube, gold, new Vector3(0f, 0.022f, 0.07f), new Vector3(0.05f, 0.004f, 0.3f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Rod(t, gold, new Vector3(s * 0.018f, 0.05f, -0.34f), new Vector3(s * 0.03f, 0.085f, -0.4f), 0.01f);
                        Rod(t, gold, new Vector3(s * 0.03f, 0.085f, -0.4f), new Vector3(s * 0.028f, 0.11f, -0.44f), 0.006f);
                    }
                    for (int k = 0; k < 6; k++) burn.Add(new Vector3(0f, 0.03f, 0.55f + k * 0.065f));
                    burn.Add(new Vector3(0f, 0.03f, 1.1f));
                }
                else
                {
                    Rod(t, metal, new Vector3(0f, 0.018f, 1.02f), new Vector3(0f, 0.018f, 1.09f), 0.03f);
                    foreach (float z in new[] { 1.035f, 1.06f })
                        Part(t, PrimitiveType.Cube, rubber, new Vector3(0f, 0.018f, z), new Vector3(0.033f, 0.01f, 0.01f));
                    if (skin.rarity == SkinRarity.Void) for (int k = 0; k < 4; k++) burn.Add(new Vector3(0f, 0.03f, 0.35f + k * 0.2f));
                }
            }
            if (skin.model == KnifeModel.Prism)
            {
                // Crystal rifle: shards bursting out along the barrel and stock, a big crystal
                // at the muzzle
                var random = new System.Random(7);
                for (int k = 0; k < 9; k++)
                {
                    float z = 0.25f + k * 0.085f;
                    var tilt = Quaternion.Euler(((float)random.NextDouble() - 0.5f) * 60f, 0f, ((float)random.NextDouble() - 0.5f) * 80f);
                    MeshPart(t, chassis, CrystalMesh(0.05f + (float)random.NextDouble() * 0.05f, 0.009f, 5, 0.3f), new Vector3(0f, 0.025f, z), tilt);
                }
                MeshPart(t, chassis, CrystalMesh(0.1f, 0.022f, 6, 0f), new Vector3(0f, 0.018f, 1.06f), Quaternion.Euler(90f, 0f, 0f));
                foreach (float x in new[] { -1f, 1f })
                    MeshPart(t, chassis, CrystalMesh(0.07f, 0.012f, 5, 0.2f), new Vector3(x * 0.02f, 0.05f, -0.3f), Quaternion.Euler(-60f, 0f, -x * 40f));
                for (int k = 0; k < 4; k++) burn.Add(new Vector3(0f, 0.05f, 0.3f + k * 0.2f));
            }
            if (skin.model == KnifeModel.Bone)
            {
                // Bone rifle: vertebrae wrapped along the barrel, a skull at the muzzle with
                // burning eyes, and spines down the stock
                Material bone = Mat(new Color(0.88f, 0.84f, 0.74f), 0.6f, 0.1f); keep.Add(bone);
                Material eye = Glow(parts.hue, 3f, parts);
                for (int k = 0; k < 11; k++)
                    Part(t, PrimitiveType.Sphere, bone, new Vector3(0f, 0.018f, 0.3f + k * 0.065f), new Vector3(0.032f, 0.028f, 0.03f));
                Part(t, PrimitiveType.Sphere, bone, new Vector3(0f, 0.03f, 1.1f), new Vector3(0.06f, 0.06f, 0.07f));
                Part(t, PrimitiveType.Cube, bone, new Vector3(0f, 0.005f, 1.12f), new Vector3(0.04f, 0.02f, 0.04f));
                foreach (float x in new[] { -1f, 1f })
                    Part(t, PrimitiveType.Sphere, eye, new Vector3(x * 0.013f, 0.035f, 1.132f), Vector3.one * 0.012f);
                for (int k = 0; k < 5; k++)
                    MeshPart(t, bone, CrystalMesh(0.04f, 0.007f, 4, 0.5f), new Vector3(0f, 0.045f, -0.34f + k * 0.07f), Quaternion.Euler(-35f, 0f, 0f));
                burn.Add(new Vector3(0f, 0.05f, 1.1f));
                for (int k = 0; k < 3; k++) burn.Add(new Vector3(0f, 0.04f, 0.4f + k * 0.2f));
            }
            if (skin.model == KnifeModel.Seraph)
            {
                // Seraph: swept wings of feathered blades off the forend, and a halo hovering
                // over the scope
                Material gold = Mat(new Color(1f, 0.82f, 0.4f), 0.95f, 1f); keep.Add(gold);
                foreach (float x in new[] { -1f, 1f })
                    for (int f = 0; f < 4; f++)
                    {
                        float len = 0.2f - f * 0.035f;
                        var a = new[] { new Vector2(0f, 0f), new Vector2(len * 0.5f, 0.02f), new Vector2(len, 0.005f) };
                        var b = new[] { new Vector2(0f, -0.022f), new Vector2(len * 0.5f, -0.012f), new Vector2(len, 0.005f) };
                        MeshPart(t, f % 2 == 0 ? chassis : gold, RailBlade(a, b, 0.003f, 0.001f),
                            new Vector3(x * 0.028f, -0.005f + f * 0.012f, 0.4f - f * 0.03f), Quaternion.Euler(0f, x * (100f + f * 10f), x * (12f + f * 7f)));
                    }
                MeshPart(t, Glow(parts.hue, 3f, parts), Torus(0.045f, 0.004f, 32, 8), new Vector3(0f, 0.16f, 0.07f), Quaternion.Euler(90f, 0f, 0f));
                for (int k = 0; k < 4; k++) burn.Add(new Vector3(0f, 0.04f, 0.35f + k * 0.18f));
            }
            BuildVoidRifle(skin, parts, t, chassis, metal, burn);
            if (skin.rarity == SkinRarity.Void) VoidRifleKit(parts, t);
            parts.cycleHue = skin.name == "Spectrum";

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
            Rod(t, chassis, new Vector3(0f, sy, 0.22f), new Vector3(0f, sy, 0.27f), 0.046f);
            Rod(t, chassis, new Vector3(0f, sy, 0.27f), new Vector3(0f, sy, 0.34f), 0.066f);
            Rod(t, rubber, new Vector3(0f, sy, 0.335f), new Vector3(0f, sy, 0.345f), 0.069f);
            Rod(t, lens, new Vector3(0f, sy, 0.344f), new Vector3(0f, sy, 0.346f), 0.058f);
            Rod(t, chassis, new Vector3(0f, sy, -0.09f), new Vector3(0f, sy, -0.15f), 0.044f);
            Rod(t, rubber, new Vector3(0f, sy, -0.15f), new Vector3(0f, sy, -0.17f), 0.046f);
            Rod(t, lens, new Vector3(0f, sy, -0.17f), new Vector3(0f, sy, -0.171f), 0.036f);
            Rod(t, metal, new Vector3(0f, sy + 0.016f, 0.06f), new Vector3(0f, sy + 0.04f, 0.06f), 0.03f);
            Rod(t, red, new Vector3(0f, sy + 0.03f, 0.06f), new Vector3(0f, sy + 0.034f, 0.06f), 0.032f);
            Rod(t, metal, new Vector3(0.016f, sy, 0.06f), new Vector3(0.04f, sy, 0.06f), 0.028f);
            Rod(t, metal, new Vector3(-0.016f, sy, 0.06f), new Vector3(-0.034f, sy, 0.06f), 0.03f);
            keep.Add(lens);
            keep.Add(red);
            if (skin.finish != KnifeFinish.Polished) CoverAndSparkle(parts, skin.finish, chassis, KnifeFinishes.Get(skin.finish).photo ? 14 : 8);
            if (burn.Count > 0) AddFlames(parts, burn, 0.1f, 0.1f);
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
