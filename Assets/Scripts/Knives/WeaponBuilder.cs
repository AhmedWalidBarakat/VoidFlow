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
        public bool talonLike; // held, and spun on its ring, like the talon knife
        public float ringSpin; // talon knife: degrees spun around the finger ring (set by the view)
        public readonly List<Material> glowMaterials = new();
        public readonly List<Color> glowColors = new();
        public readonly List<(Transform t, Vector3 home, float phase)> motes = new();
        public readonly List<(Transform t, float size, float phase, float speed)> sparkles = new();
        public readonly List<GameObject> attached = new(); // Void glove bits hung on finger bones (not under root)

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
        public bool pairs = true; // twin blades built as the pair (the view builds one per hand instead)
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
            if (skin.asset != null) return FromModel(skin, parent);
            var root = new GameObject(skin.name).transform;
            root.SetParent(parent, false);
            var parts = new WeaponParts { root = root, model = skin.model, rarity = skin.rarity, hue = HueOf(skin.finish) };
            Material finish = FinishMaterial(skin.finish, parts);
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
                if (KnifeFinishes.Get(skin.finish).emission)
                {
                    var veined = FinishMaterial(skin.finish, parts);
                    veined.SetTexture("_EmissionMap", KnifeFinishes.GloveVeins);
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                        if (r.sharedMaterial == finish && r.GetComponent<MeshFilter>().sharedMesh.name != "Blade") r.sharedMaterial = veined;
                }
                Aura(parts, skin.finish);
                // Flames along the blade, from its base to its point
                var at = new List<Vector3>();
                Vector3 from = parts.model == KnifeModel.Talon ? new Vector3(-0.02f, -0.11f, 0f) : new Vector3(0f, 0.03f, 0f);
                Vector3 to = parts.tip ? parts.root.InverseTransformPoint(parts.tip.position) : new Vector3(0f, 0.22f, 0f);
                if (parts.model == KnifeModel.Reaper) from = new Vector3(0f, 0.14f, 0f);
                for (int k = 0; k < 5; k++) at.Add(Vector3.Lerp(from, to, (k + 0.5f) / 5f));
                AddFlames(parts, at, 0.04f, 0.03f); // small and close to the blade, out of the way of the view
            }
            if (KnifeFinishes.Get(skin.finish).photo) CoverAndSparkle(parts, skin.finish, finish, 6);
            // Skins flow across the whole knife as one piece, from the heel to the point
            if (skin.rarity != SkinRarity.Void)
            {
                Vector3 tip = parts.tip ? parts.root.InverseTransformPoint(parts.tip.position) : Vector3.up * 0.2f;
                LayFinish(parts, skin.finish, tip.normalized, Vector3.right);
            }
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
            Part(t, PrimitiveType.Cube, grip, new Vector3(0.002f, -0.04f, 0f), new Vector3(0.026f, 0.072f, 0.016f));
            Part(t, PrimitiveType.Capsule, grip, new Vector3(0.002f, -0.04f, 0f), new Vector3(0.028f, 0.041f, 0.016f));
            for (int k = 0; k < 3; k++)
                Part(t, PrimitiveType.Cylinder, hole, new Vector3(-0.0125f, -0.02f - k * 0.0175f, 0f), new Vector3(0.0105f, 0.0087f, 0.0105f), Quaternion.Euler(90f, 0f, 0f));
            foreach (float y in new[] { -0.031f, -0.053f })
                Part(t, PrimitiveType.Cylinder, hole, new Vector3(0.006f, y, 0f), new Vector3(0.0075f, 0.0088f, 0.0075f), Quaternion.Euler(90f, 0f, 0f));
            foreach (float y in new[] { -0.015f, -0.067f })
                Part(t, PrimitiveType.Cylinder, brass, new Vector3(0.006f, y, 0f), new Vector3(0.005f, 0.009f, 0.005f), Quaternion.Euler(90f, 0f, 0f));
            Part(t, PrimitiveType.Cube, brass, new Vector3(0f, -0.081f, 0f), new Vector3(0.032f, 0.007f, 0.019f));
            Part(t, PrimitiveType.Cube, brass, new Vector3(0f, 0.004f, 0f), new Vector3(0.032f, 0.006f, 0.02f));
            parts.ringCenter = new Vector3(0.002f, 0.024f, 0f);
            MeshPart(t, brass, Torus(0.019f, 0.0055f, 28, 10), parts.ringCenter, Quaternion.identity);

            // Blade: a deep hook, spine outside the curve, edge inside
            const int n = 24;
            const float radius = 0.1f, sweep = 105f * Mathf.Deg2Rad, top = -0.085f;
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

        // A butterfly knife (balisong), after the classic shooter one: two long, gently curved
        // channel handles side by side, each swinging on its own pivot pin at the blade's tang
        // (screw heads on both faces). Each handle is a pair of plates with the blade's slot
        // between them, cut with round holes at both ends and a long pill-shaped window showing
        // a satin inlay; the swing handle carries the wire latch at its end. The blade is long
        // and narrow with a clip point, jimping notches on the spine by the pivot and a hooked
        // kicker on the edge side. The finish covers the handles and blade as one piece.
        void Butterfly(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material steel = Mat(new Color(0.78f, 0.79f, 0.82f), 0.85f, 0.95f);
            Material satin = Mat(new Color(0.6f, 0.61f, 0.64f), 0.55f, 0.9f);
            Material dark = Mat(new Color(0.04f, 0.04f, 0.045f), 0.35f, 0.3f);
            const float Pin = 0.0065f, L = 0.125f, W = 0.0062f, Bend = 0.0035f, PlateZ = 0.0036f;
            // The handle's centre line (handle space: its pin at the origin, running down -y),
            // bowed gently toward the edge side like the real thing
            float Cx(float y) => -Bend * Mathf.Sin(Mathf.PI * Mathf.Clamp01(-y / L));
            Vector2[] Capsule(float y0, float y1, float r, int n = 10)
            {
                var pts = new List<Vector2>();
                for (int k = 0; k <= n; k++) { float a = Mathf.PI * k / n; pts.Add(new Vector2(Cx(y0) + Mathf.Cos(a) * r, y0 + Mathf.Sin(a) * r)); }
                for (int k = 0; k <= n; k++) { float a = Mathf.PI + Mathf.PI * k / n; pts.Add(new Vector2(Cx(y1) + Mathf.Cos(a) * r, y1 + Mathf.Sin(a) * r)); }
                return pts.ToArray();
            }
            Vector2[] HandleOutline()
            {
                var pts = new List<Vector2>();
                const int n = 20;
                for (int k = 0; k <= n; k++) { float y = -L * k / n; pts.Add(new Vector2(Cx(y) + W, y)); }         // one side, down
                for (int k = 1; k < 12; k++) { float a = -Mathf.PI * k / 12f; pts.Add(new Vector2(Cx(-L) + Mathf.Cos(a) * W, -L + Mathf.Sin(a) * W)); } // round end
                for (int k = n; k >= 0; k--) { float y = -L * k / n; pts.Add(new Vector2(Cx(y) - W, y)); }         // other side, up
                for (int k = 1; k < 12; k++) { float a = Mathf.PI - Mathf.PI * k / 12f; pts.Add(new Vector2(Mathf.Cos(a) * W, Mathf.Sin(a) * W)); } // round top, about the pin
                return pts.ToArray();
            }
            var holes = new[]
            {
                Circle(new Vector2(Cx(-0.014f), -0.014f), 0.0019f, 14), Circle(new Vector2(Cx(-0.0225f), -0.0225f), 0.0019f, 14),
                Capsule(-0.034f, -0.083f, 0.0026f),
                Circle(new Vector2(Cx(-0.095f), -0.095f), 0.0019f, 14), Circle(new Vector2(Cx(-0.104f), -0.104f), 0.0019f, 14),
            };
            Transform Handle(string name, float pinX, bool latch)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(t, false);
                pivot.localPosition = new Vector3(pinX, 0f, 0f);
                foreach (float z in new[] { -PlateZ, PlateZ })
                {
                    var plate = new GameObject("Plate").transform;
                    plate.SetParent(pivot, false);
                    plate.localPosition = new Vector3(0f, 0f, z);
                    Flat(plate, finish, HandleOutline(), holes, 0.0011f, 0.0004f, "Handle");
                    // Screw heads on the pin and at the far end
                    Part(pivot, PrimitiveType.Cylinder, steel, new Vector3(0f, 0f, z * 1.36f), new Vector3(0.0068f, 0.0007f, 0.0068f), Quaternion.Euler(90f, 0f, 0f));
                    Part(pivot, PrimitiveType.Cylinder, dark, new Vector3(0f, 0f, z * 1.47f), new Vector3(0.0028f, 0.0002f, 0.0028f), Quaternion.Euler(90f, 0f, 0f));
                    Part(pivot, PrimitiveType.Cylinder, steel, new Vector3(Cx(-L + 0.0045f), -L + 0.0045f, z * 1.36f), new Vector3(0.0042f, 0.0006f, 0.0042f), Quaternion.Euler(90f, 0f, 0f));
                }
                // The satin inlay seen through the long window, and the channel's dark back
                var inlay = new GameObject("Inlay").transform;
                inlay.SetParent(pivot, false);
                Flat(inlay, satin, Capsule(-0.036f, -0.081f, 0.0021f), null, 0.0044f, 0.0003f, "Inlay");
                for (int k = 0; k < 6; k++)
                {
                    float y0 = -0.03f - k * 0.016f, y1 = y0 - 0.016f;
                    Rod(pivot, dark, new Vector3(Cx(y0) - W * 0.8f, y0, 0f), new Vector3(Cx(y1) - W * 0.8f, y1, 0f), 0.0036f);
                }
                if (latch)
                {
                    Rod(pivot, dark, new Vector3(Cx(-L) + 0.002f, -L + 0.003f, 0f), new Vector3(Cx(-L) + 0.006f, -L - 0.011f, 0f), 0.0012f);
                    Rod(pivot, dark, new Vector3(Cx(-L) + 0.006f, -L - 0.011f, 0f), new Vector3(Cx(-L) + 0.0035f, -L - 0.015f, 0f), 0.0012f);
                }
                return pivot;
            }
            Handle("Handle", -Pin, false);
            parts.swingHandle = Handle("Swing Handle", Pin, true);

            parts.blade = new GameObject("Blade").transform;
            parts.blade.SetParent(t, false);
            // The tang: rounded round both pins, jimping on the spine, the hooked kicker on the edge side
            var tang = new List<Vector2> { new(-0.0115f, 0.0105f) };
            tang.AddRange(new Vector2[] { new(-0.0115f, 0.0075f), new(-0.0168f, 0.0048f), new(-0.0192f, 0.0022f), new(-0.0188f, -0.0012f), new(-0.0172f, 0.0016f), new(-0.0148f, 0.0026f), new(-0.0105f, 0.0005f) });
            for (int k = 0; k <= 8; k++) { float a = Mathf.PI + Mathf.PI * 0.5f * k / 8f; tang.Add(new Vector2(-Pin + Mathf.Cos(a) * 0.0042f, Mathf.Sin(a) * 0.0042f)); }
            for (int k = 0; k <= 8; k++) { float a = Mathf.PI * 1.5f + Mathf.PI * 0.5f * k / 8f; tang.Add(new Vector2(Pin + Mathf.Cos(a) * 0.0042f, Mathf.Sin(a) * 0.0042f)); }
            for (int k = 0; k < 4; k++) { float y = 0.0035f + k * 0.0018f; tang.Add(new Vector2(0.0107f, y)); tang.Add(new Vector2(0.0124f, y + 0.0006f)); tang.Add(new Vector2(0.0107f, y + 0.0012f)); }
            tang.Add(new Vector2(0.0107f, 0.0105f));
            Flat(parts.blade, finish, tang.ToArray(), null, 0.0023f, 0.0004f, "Tang");
            const int n = 28;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float y = 0.009f + 0.12f * i / n;
                // Straight spine, then the clip sweeping down to a point in line with the upper blade
                spine[i] = new Vector2(y < 0.082f ? 0.0107f : 0.0107f - 0.0072f * Mathf.Pow((y - 0.082f) / 0.047f, 1.6f), y);
                float k = Mathf.Clamp01((y - 0.058f) / 0.071f);
                edge[i] = new Vector2(-0.0115f + 0.015f * Mathf.Pow(k, 2.2f), y);
            }
            MeshPart(parts.blade, finish, RailBlade(spine, edge, 0.0023f, 0.0003f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(parts.blade, spine[^1]);
        }

        // A Void weapon made from a real model (Sketchfab, CC-BY, see CREDITS.md): the fitted
        // prefab (VoidModelFit put it in this weapon space), on the view's layer, its point marked
        // for trails
        WeaponParts FromModel(Skins.Skin skin, Transform parent)
        {
            var t = new GameObject(skin.name).transform;
            t.SetParent(parent, false);
            var parts = new WeaponParts { root = t, model = skin.model, rarity = skin.rarity, hue = Skins.VoidHue(skin.asset) };
            var prefab = Resources.Load<GameObject>($"VoidModels/{skin.asset}/fitted");
            if (!prefab) { Debug.LogWarning($"VoidFlow: model missing for {skin.name}"); return parts; }
            var model = Object.Instantiate(prefab, t, false);
            model.name = "Model";
            if (skin.model == KnifeModel.ModelDual && pairs)
            {
                // both blades, splayed from the handles like a V
                model.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
                var twin = Object.Instantiate(prefab, t, false);
                twin.name = "Twin";
                twin.transform.SetLocalPositionAndRotation(new Vector3(0.03f, 0f, 0f), Quaternion.Euler(0f, 180f, -22f));
            }
            Vivid(model, skin.asset != null && skin.asset.Length >= 2 ? skin.asset.Substring(0, 2) : "");
            // Long swords shrunk to a knife's length go thin as needles: those are filled out
            // across the blade (and a little in thickness) toward a sword's proportions
            if (skin.model == KnifeModel.ModelBlade)
            {
                // (measured in the model's own space, x across the blade and y along it, over the
                // middle of the blade only: a wide guard doesn't make a needle a sword)
                var pts = new List<Vector3>();
                float reach = float.MinValue;
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
                    var v = mf.sharedMesh.vertices;
                    for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 3000))
                    {
                        var p = model.transform.InverseTransformPoint(mf.transform.TransformPoint(v[i]));
                        pts.Add(p); reach = Mathf.Max(reach, p.y);
                    }
                }
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var p in pts)
                    if (p.y > reach * 0.35f && p.y < reach * 0.75f) { lo = Mathf.Min(lo, p.x); hi = Mathf.Max(hi, p.x); }
                bool any = hi > lo && reach > 0f;
                float width = hi - lo, length = Mathf.Max(reach, 1e-4f);
                const float Wanted = 0.11f; // the blade's width to the length above the guard, a broad sword's
                if (any && width / length < Wanted)
                {
                    float k = Mathf.Min(2.2f, Wanted / Mathf.Max(width / length, 0.02f));
                    model.transform.localScale = new Vector3(k, 1f, Mathf.Sqrt(k));
                }
            }
            Transform karambitTip = Skins.IsKarambit(skin.asset) ? TalonFit(parts, model) : null;
            if (!Skins.IsRifle(skin.model)) Outline(parts, model, Skins.VoidHue(skin.asset));
            float top = 0f;
            foreach (var tr in model.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = shadows;
                top = Mathf.Max(top, t.InverseTransformPoint(r.bounds.center + Vector3.up * r.bounds.extents.y).y);
            }
            if (karambitTip) parts.tip = karambitTip;
            else if (!Skins.IsRifle(skin.model)) parts.tip = Tip(t, new Vector2(0f, top));
            else
            {
                // the model's own bolt and magazine don't move: empty stand-ins where the hand
                // works the bolt and the reload drops the magazine
                parts.magazine = new GameObject("Magazine").transform;
                parts.magazine.SetParent(t, false);
                parts.magRest = new Vector3(0f, -0.03f, 0.1f);
                parts.magazine.localPosition = parts.magRest;
                parts.bolt = new GameObject("Bolt").transform;
                parts.bolt.SetParent(t, false);
                parts.boltRest = new Vector3(0.022f, 0.032f, -0.035f);
                parts.bolt.localPosition = parts.boltRest;
            }
            parts.Animate(0f, -1f);
            return parts;
        }

        // The model's points in the weapon's space
        static List<Vector3> PointsOf(Transform space, GameObject model)
        {
            var pts = new List<Vector3>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
                var v = mf.sharedMesh.vertices;
                for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 3000)) pts.Add(space.InverseTransformPoint(mf.transform.TransformPoint(v[i])));
            }
            return pts;
        }

        // A karambit laid into the talon knife's space: turned over so the finger ring is at the
        // top (y 0.024, above the index finger) and the hooked blade below the fist, the handle
        // running down through the fist between them. Returns its point, for the trail.
        Transform TalonFit(WeaponParts parts, GameObject model)
        {
            var t = parts.root;
            var pts = PointsOf(t, model);
            if (pts.Count < 10) return null;
            float yMin = float.MaxValue;
            foreach (var p in pts) yMin = Mathf.Min(yMin, p.y);
            const float Guard = 0.005f; // (where the fitting put the top of the handle)
            float k = 0.128f / Mathf.Max(Guard - yMin, 0.02f), off = -0.085f + Guard * k;
            var pivot = new GameObject("Talon Hold").transform;
            pivot.SetParent(t, false);
            pivot.SetLocalPositionAndRotation(new Vector3(0f, off, 0f), Quaternion.Euler(0f, 0f, 180f));
            pivot.localScale = Vector3.one * k;
            model.transform.SetParent(pivot, false);
            // the ring: the points above the fist; the point: the lowest
            pts = PointsOf(t, model);
            Vector3 ring = Vector3.zero; int n = 0; Vector3 low = Vector3.up;
            foreach (var p in pts)
            {
                if (p.y > 0f) { ring += p; n++; }
                if (p.y < low.y) low = p;
            }
            parts.ringCenter = n > 0 ? new Vector3(ring.x / n, ring.y / n, 0f) : new Vector3(0f, 0.024f, 0f);
            parts.talonLike = true;
            return Tip(t, new Vector2(low.x, low.y));
        }

        // A thin shell of light round the weapon in its own colour (the inside of a copy pushed
        // out along the surface): it outlines the weapon against any background, so its shape
        // reads in the darkest zone and the brightest
        void Outline(WeaponParts parts, GameObject model, Color hue)
        {
            var glow = Glow(hue, 1.8f, parts);
            glow.SetFloat("_Cull", 1f); // (front faces culled: only the rim round the weapon shows)
            materials?.Add(glow);
            const float Thick = 0.0016f; // in the weapon's space
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var src = mf.sharedMesh;
                if (!src || !src.isReadable) continue;
                var v = src.vertices; var nrm = src.normals;
                if (nrm == null || nrm.Length != v.Length) continue;
                // smooth normals (welded by position), so the shell doesn't crack at hard edges
                var sum = new Dictionary<Vector3Int, Vector3>();
                Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
                for (int i = 0; i < v.Length; i++) { var key = Key(v[i]); sum[key] = (sum.TryGetValue(key, out var s0) ? s0 : Vector3.zero) + nrm[i]; }
                float local = Thick / Mathf.Max(1e-6f, parts.root.InverseTransformVector(mf.transform.TransformVector(Vector3.one.normalized)).magnitude);
                var moved = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) moved[i] = v[i] + sum[Key(v[i])].normalized * local;
                var shell = new Mesh { name = "Outline", indexFormat = src.indexFormat };
                shell.vertices = moved;
                var tris = new List<int>();
                for (int sm = 0; sm < src.subMeshCount; sm++) tris.AddRange(src.GetTriangles(sm));
                shell.SetTriangles(tris, 0);
                shell.RecalculateNormals();
                shell.RecalculateBounds();
                var go = new GameObject("Outline");
                go.transform.SetParent(mf.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = shell;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = glow;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // The models' metal only mirrors the zone's sky, so in a dark zone their colours sank
        // into black: each weapon gets copies of its materials that glow a little with their own
        // colours (their own colour map, or their own glow maps made brighter), metal that's
        // part paint rather than all mirror, and a glossier finish, so the colours and the
        // details read in any light
        static readonly Dictionary<string, float> brighter = new() { { "03", 1.3f } };

        void Vivid(GameObject model, string asset)
        {
            var made = new Dictionary<Material, Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (!src) continue;
                    if (!made.TryGetValue(src, out var m))
                    {
                        m = new Material(src) { name = src.name + " (vivid)" };
                        keep.Add(m); materials?.Add(m); // (freed with the weapon)
                        made[src] = m;
                        bool hasGlow = m.HasProperty("emissiveTexture") && m.GetTexture("emissiveTexture");
                        Color glow = m.HasProperty("emissiveFactor") ? m.GetColor("emissiveFactor") : Color.black;
                        Color tint = m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : Color.white;
                        // Freshly made, not dug up: the baked-in grime and shadow (occlusion)
                        // left off, the colours brighter, the surface polished
                        if (m.HasProperty("occlusionTexture")) m.SetTexture("occlusionTexture", null);
                        if (m.HasProperty("occlusionTexture_strength")) m.SetFloat("occlusionTexture_strength", 0f);
                        float lift = brighter.TryGetValue(asset, out float b) ? b : 1.12f; // (a few models are painted darker than the rest)
                        if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", new Color(tint.r * lift, tint.g * lift, tint.b * lift, tint.a));
                        if (hasGlow && glow.maxColorComponent > 0.01f)
                            m.SetColor("emissiveFactor", glow * 1.1f); // its own lights, a touch brighter
                        else if (m.HasProperty("baseColorTexture") && m.HasProperty("emissiveTexture"))
                        {
                            // its colours lit from within, a little
                            m.SetTexture("emissiveTexture", m.GetTexture("baseColorTexture"));
                            m.SetColor("emissiveFactor", new Color(tint.r, tint.g, tint.b, 1f) * 0.28f);
                        }
                        if (m.HasProperty("metallicFactor")) m.SetFloat("metallicFactor", Mathf.Min(m.GetFloat("metallicFactor"), 0.25f)); // (more mirror than this and the sky drowns their colours at a glancing angle)
                        if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", Mathf.Clamp(m.GetFloat("roughnessFactor") * 0.5f, 0.07f, 0.4f));
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
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

        // A skeleton knife, after the classic: a slim spear-point blade with a ridge down its
        // middle and a row of four holes near its base, a pointed guard at the heel of the
        // blade holding a big finger ring (the index finger goes through it, and the knife
        // spins on it), and a flat tang wrapped tight in black cord. The finish covers the
        // blade and guard.
        void Skeleton(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material cord = Mat(new Color(0.045f, 0.045f, 0.05f), 0.25f, 0f);
            Material cordHi = Mat(new Color(0.09f, 0.09f, 0.1f), 0.3f, 0f);
            Material hole = Mat(new Color(0.01f, 0.01f, 0.012f), 0.2f, 0f);

            // Blade: two halves meeting on a centre ridge, so both sides read as edges
            const int n = 30;
            const float from = 0.03f, length = 0.155f;
            float Half(float s) => 0.0145f * Mathf.Sin(Mathf.Min(s / 0.32f, 1f) * Mathf.PI * 0.5f) * (1f - Mathf.Pow(Mathf.Max(0f, (s - 0.32f) / 0.68f), 1.6f)) + 0.0005f * (1f - s);
            foreach (float side in new[] { 1f, -1f })
            {
                var ridge = new Vector2[n + 1];
                var edge = new Vector2[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    float s = (float)i / n, y = from + length * s;
                    ridge[i] = new Vector2(0f, y);
                    edge[i] = new Vector2(side * Mathf.Max(Half(s) + (s < 0.05f ? 0.006f * (1f - s / 0.05f) : 0f), 0f), y);
                }
                edge[n] = ridge[n];
                MeshPart(t, finish, RailBlade(ridge, edge, 0.0024f, 0.0003f), Vector3.zero, Quaternion.identity);
            }
            parts.tip = Tip(t, new Vector2(0f, from + length));
            // The row of holes down the middle near the base
            for (int k = 0; k < 4; k++)
                Part(t, PrimitiveType.Cylinder, hole, new Vector3(0f, 0.041f + k * 0.0085f, 0f), new Vector3(0.0034f, 0.0028f, 0.0034f), Quaternion.Euler(90f, 0f, 0f));

            // The guard: pointed wings either side of the finger ring
            var c = new Vector2(0f, 0.012f);
            Flat(t, finish, new Vector2[]
            {
                new(0f, 0.036f), new(0.012f, 0.031f), new(0.02f, 0.024f), new(0.029f, 0.013f), new(0.021f, 0.005f),
                new(0.015f, -0.004f), new(0.0115f, -0.009f), new(-0.0115f, -0.009f), new(-0.015f, -0.004f),
                new(-0.021f, 0.005f), new(-0.029f, 0.013f), new(-0.02f, 0.024f), new(-0.012f, 0.031f),
            }, new[] { Circle(c, 0.0115f, 24) }, 0.0026f, 0.0012f, "Guard");
            parts.ringCenter = new Vector3(c.x, c.y, 0f);

            // The tang, wrapped in cord, with a rounded end
            Flat(t, finish, new Vector2[] { new(0.0105f, -0.006f), new(0.0105f, -0.1f), new(0.006f, -0.106f), new(-0.006f, -0.106f), new(-0.0105f, -0.1f), new(-0.0105f, -0.006f) }, null, 0.0022f, 0.001f, "Tang");
            for (int w = 0; w < 17; w++)
            {
                float y = -0.011f - w * 0.0052f;
                Part(t, PrimitiveType.Cube, w % 2 == 0 ? cord : cordHi, new Vector3(0f, y, 0f), new Vector3(0.0235f, 0.0046f, 0.0078f), Quaternion.Euler(0f, 0f, w % 2 == 0 ? 8f : -8f));
            }
        }

        // A kukri, after the classic: a broad leaf-shaped blade angled forward toward its
        // edge, swelling into a deep belly before the point; at its heel a toothed ricasso
        // with a little spur on the spine; a black grip with finger grooves, a hooked guard
        // and a flared pommel
        void KukriKnife(WeaponParts parts, Material finish)
        {
            var t = parts.root;
            Material grip = Mat(new Color(0.04f, 0.04f, 0.045f), 0.35f, 0f);
            Material steel = Mat(new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.8f);

            const int n = 56;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, y = 0.002f + 0.205f * s;
                float sx, ex;
                if (y < 0.036f)
                {
                    // The ricasso: narrow, with three teeth on the edge side
                    float k = (y - 0.002f) / 0.034f;
                    sx = 0.008f + (y > 0.027f ? (y - 0.027f) * 0.9f : 0f); // the spur rising on the spine
                    float tooth = Mathf.Abs(k * 3f % 1f - 0.5f) * 2f;
                    ex = -0.011f - tooth * 0.004f;
                }
                else
                {
                    float b = (y - 0.036f) / 0.171f;
                    float bend = -0.05f * Mathf.Pow(b, 1.5f);
                    float w = 0.017f + 0.036f * Mathf.Sin(Mathf.Min(b / 0.7f, 1f) * Mathf.PI * 0.5f);
                    float tip = Mathf.Clamp01((b - 0.78f) / 0.22f);
                    sx = bend + 0.008f - tip * tip * 0.022f;
                    ex = bend + 0.008f - w;
                    ex = Mathf.Lerp(ex, sx - 0.0008f, Mathf.Pow(tip, 0.85f));
                    y += tip * tip * 0.004f;
                }
                spine[i] = new Vector2(sx, y);
                edge[i] = new Vector2(ex, y - 0.01f * Mathf.Clamp01((y - 0.036f) / 0.17f));
            }
            MeshPart(t, finish, RailBlade(spine, edge, 0.0036f, 0.0004f), Vector3.zero, Quaternion.identity);
            parts.tip = Tip(t, spine[^1]);

            // Guard: a short hook curling toward the edge side over the fingers
            Flat(t, steel, new Vector2[] { new(0.012f, 0.004f), new(0.012f, -0.004f), new(-0.02f, -0.004f), new(-0.028f, -0.012f), new(-0.031f, -0.008f), new(-0.024f, 0.004f) }, null, 0.005f, 0.0015f, "Guard");
            // Grip with finger grooves on the edge side, flaring and hooking into the pommel
            Flat(t, grip, new Vector2[]
            {
                new(0.0115f, -0.004f), new(0.013f, -0.04f), new(0.0115f, -0.08f), new(0.013f, -0.098f), new(0.017f, -0.11f), new(0.014f, -0.118f),
                new(0.002f, -0.119f), new(-0.013f, -0.114f), new(-0.02f, -0.118f), new(-0.018f, -0.106f), new(-0.0125f, -0.095f),
                new(-0.0145f, -0.083f), new(-0.0115f, -0.071f), new(-0.0145f, -0.059f), new(-0.0115f, -0.047f), new(-0.0145f, -0.035f), new(-0.012f, -0.022f), new(-0.013f, -0.004f),
            }, null, 0.0105f, 0.0045f, "Grip");
            Part(t, PrimitiveType.Cube, steel, new Vector3(-0.001f, -0.1195f, 0f), new Vector3(0.026f, 0.0025f, 0.016f));
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

        // The skin's material on its own (for gloves). Void finishes are painted for blades
        // (a glowing edge down one side); on a glove they glow only in fine veins.
        public Material SkinMaterial(KnifeFinish finish) => GloveMaterial(finish, new WeaponParts());

        Material GloveMaterial(KnifeFinish finish, WeaponParts parts)
        {
            var rig = Resources.Load<ArmRig>("Arms/RightArm");
            var look = KnifeFinishes.Get(finish);
            if (look.emission && rig && rig.gloveGlow)
            {
                // Void: the real leather glove in its colour, glowing along its seams
                var v = Mat(Color.white, 0.5f, 0f);
                Color hue = HueOf(finish);
                rig.DressVoidGlove(v, hue);
                parts.glowMaterials.Add(v);
                parts.glowColors.Add(hue * 2.4f);
                return v;
            }
            var m = FinishMaterial(finish, parts);
            if (KnifeFinishes.Get(finish).emission) m.SetTexture("_EmissionMap", KnifeFinishes.GloveVeins);
            return m;
        }

        // A glove on its own, for displays and cases: the same glove as on the arms (and its
        // wrist strap), in the skin, relaxed with the fingers a little curled, plus any Void
        // add-ons fitted to it. Baked to plain meshes; centered with the fingers up (+Y) and
        // the back of the hand toward +Z.
        public WeaponParts GloveModel(Skins.Skin skin, Transform parent)
        {
            var root = new GameObject(skin.name).transform;
            root.SetParent(parent, false);
            var parts = new WeaponParts { root = root, model = skin.model, rarity = skin.rarity, hue = HueOf(skin.finish) };
            Material body = skin.finish == KnifeFinish.Polished ? Mat(new Color(0.07f, 0.07f, 0.08f), 0.4f, 0f) : GloveMaterial(skin.finish, parts);
            Material cuff = Mat(new Color(0.05f, 0.05f, 0.055f), 0.35f, 0f);
            var rig = Resources.Load<ArmRig>("Arms/RightArm");
            if (rig) rig.DressGlove(body, skin.finish == KnifeFinish.Polished);
            var glove = new GameObject("Glove").transform;
            glove.SetParent(root, false);
            if (!rig) return parts;
            var bones = new Transform[rig.boneNames.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = new GameObject(rig.boneNames[i]).transform;
                bone.SetParent(rig.parents[i] < 0 ? glove : bones[rig.parents[i]], false);
                bone.SetLocalPositionAndRotation(rig.bindPositions[i], rig.bindRotations[i]);
                bones[i] = bone;
            }
            SkinnedMeshRenderer Skin(string name, Mesh mesh)
            {
                var go = new GameObject(name);
                go.transform.SetParent(glove, false);
                var r = go.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh = mesh;
                r.bones = bones;
                r.rootBone = bones[0];
                return r;
            }
            var gloveSkin = Skin("GloveBlock", rig.glove);
            var strapSkin = Skin("GloveStrap", rig.strap);
            var fit = HandFit.Measure(rig, bones, gloveSkin, strapSkin);
            var kit = new GameObject("Kit").transform;
            kit.SetParent(glove, false);
            BuildGloveKit(skin, parts, kit, fit);
            rig.Pose(bones, ArmRig.Relaxed);

            // Baked into plain meshes in the relaxed pose; the finger bits come back off the bones
            Bounds bounds = default;
            foreach (var (r, mat) in new[] { (gloveSkin, body), (strapSkin, cuff) })
            {
                var mesh = new Mesh { name = r.name };
                r.BakeMesh(mesh);
                mesh.RecalculateBounds();
                if (r == gloveSkin) bounds = mesh.bounds;
                var go = new GameObject(r.name);
                go.transform.SetParent(glove, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                Finish(go.AddComponent<MeshRenderer>(), mat);
            }
            foreach (var bit in parts.attached) bit.transform.SetParent(kit, true);
            parts.attached.Clear();
            Kill(gloveSkin.gameObject);
            Kill(strapSkin.gameObject);
            Kill(bones[0].gameObject);
            if (parts.flames) parts.flames.transform.SetParent(glove, false);
            glove.localPosition = -bounds.center;
            parts.Animate(0f, -1f);
            return parts;
        }

        // Only the Void add-ons, built onto an arm's real glove
        public WeaponParts GloveKit(Skins.Skin skin, HandFit fit)
        {
            var root = new GameObject("Glove Kit").transform;
            root.SetParent(fit.space, false);
            var parts = new WeaponParts { root = root, model = skin.model, rarity = skin.rarity, hue = HueOf(skin.finish) };
            BuildGloveKit(skin, parts, root, fit);
            parts.Animate(0f, -1f);
            return parts;
        }

        // The Void add-ons, fitted to the real glove (see HandFit): built in arm space with the
        // fingers straight, and anything on a finger hung on that finger's bone so it curls
        // with it. Nothing is a loose box: plates, scales and prongs lie on the back of the
        // hand, rings and wraps follow the wrist's and fingers' own outline.
        void BuildGloveKit(Skins.Skin skin, WeaponParts parts, Transform t, HandFit fit)
        {
            if (skin.rarity != SkinRarity.Void || fit == null) return;
            Material finish = FinishMaterial(skin.finish, parts);
            Material glow = Glow(parts.hue, 2.6f, parts);
            Material dark = Mat(new Color(0.08f, 0.08f, 0.09f), 0.9f, 0.95f);
            // Polished metal in the glove's color, for plates and scales that stand out from it
            // Brushed metal (a CC0 scan) tinted in the glove's colour, for plates and scales
            Material shell = Mat(Color.Lerp(parts.hue, new Color(0.55f, 0.55f, 0.58f), 0.45f), 0.8f, 0.75f);
            var brushed = Resources.Load<Texture2D>("SkinTextures/Metal038");
            if (brushed) { shell.SetTexture("_BaseMap", brushed); shell.SetTextureScale("_BaseMap", new Vector2(0.3f, 0.3f)); }
            float yW = fit.wristY, yK = fit.knuckleY, span = yK - yW;
            var palm = fit.Section(Mathf.Lerp(yW, yK, 0.55f));

            // A piece lying on the back of the hand at (x, y), turned to the surface
            Quaternion OnBack(float x, float y) => Quaternion.LookRotation(fit.Normal(x, y), Vector3.up);
            Vector3 Above(float x, float y, float lift) => fit.Surface(x, y) + fit.Normal(x, y) * lift;
            // Hangs a piece on a finger bone (it keeps its place, and curls with the finger)
            void Hang(Transform piece, int finger, int joint)
            {
                piece.SetParent(fit.fingers[finger, joint], true);
                parts.attached.Add(piece.gameObject);
            }
            // A ring round the hand (or wrist, or forearm) at height y, a little outside the glove
            Transform Ring(Material m, float y, float gap, float tube, float flat = 1f, float tilt = 0f)
            {
                var s = fit.Section(y);
                return MeshPart(t, m, HandFit.Loop(s.rx + gap, s.rz + gap, tube, flat), new Vector3(s.center.x, y, s.center.y), Quaternion.Euler(tilt, 0f, 0f));
            }
            // A ring round a finger, partway along its first bone
            void FingerRing(Material m, int f, float along, float tube)
            {
                var (a, b) = fit.Segment(f, 0);
                Vector3 c = Vector3.Lerp(a, b, along);
                float r = Mathf.Max(0.006f, fit.Surface(c.x, c.y, 0.004f).z - c.z) + tube * 0.6f;
                var ring = MeshPart(t, m, HandFit.Loop(r, r, tube), c, Quaternion.FromToRotation(Vector3.up, b - a));
                Hang(ring, f, 0);
            }

            switch (skin.model)
            {
                case KnifeModel.GloveArmor:
                {
                    // An armoured plate over each finger and a bracer round the wrist (the glove's
                    // own quilted panel glows between them)
                    for (int f = 0; f < 4; f++)
                    {
                        var (a, b) = fit.Segment(f, 0);
                        Vector3 c = Vector3.Lerp(a, b, 0.5f);
                        var top = fit.Surface(c.x, c.y, 0.004f);
                        var plate = Part(t, PrimitiveType.Sphere, shell, top + Vector3.forward * 0.0008f, new Vector3(0.013f, (b - a).magnitude * 0.85f, 0.004f), Quaternion.FromToRotation(Vector3.up, b - a));
                        Hang(plate, f, 0);
                    }
                    Ring(shell, yW - 0.012f, 0.002f, 0.0035f, 2.5f);
                    Ring(glow, yW - 0.001f, 0.002f, 0.0015f);
                    break;
                }
                case KnifeModel.GloveClaws:
                    // Three curved talons out of the fingertips, curling toward the palm
                    for (int f = 0; f < 3; f++)
                    {
                        const int n = 14;
                        var spine = new Vector2[n + 1];
                        var edge = new Vector2[n + 1];
                        for (int i = 0; i <= n; i++)
                        {
                            float s = (float)i / n, a = s * 70f * Mathf.Deg2Rad;
                            var center = new Vector2(-0.065f + 0.065f * Mathf.Cos(a), 0.065f * Mathf.Sin(a));
                            var outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            float w = 0.014f * (1f - Mathf.Pow(s, 1.4f));
                            spine[i] = center + outward * w * 0.5f;
                            edge[i] = center - outward * w * 0.5f;
                        }
                        Vector3 tip = fit.At(fit.fingers[f, 3]);
                        var claw = MeshPart(t, finish, RailBlade(spine, edge, 0.002f, 0.0004f), tip + new Vector3(0f, -0.008f, 0.004f), Quaternion.Euler(0f, -90f, 0f));
                        Hang(claw, f, 2);
                        var band = MeshPart(t, glow, HandFit.Loop(0.0085f, 0.0085f, 0.0012f), tip + Vector3.down * 0.012f, Quaternion.identity);
                        Hang(band, f, 2);
                    }
                    break;
                case KnifeModel.GloveRunes:
                {
                    // A ring of glowing runes orbiting the wrist, and a finer one round the knuckles
                    Ring(glow, yW - 0.004f, 0.012f, 0.0018f);
                    var s = fit.Section(yW - 0.004f);
                    var holder = new GameObject("Aura").transform;
                    holder.SetParent(t, false);
                    holder.localPosition = new Vector3(s.center.x, yW - 0.004f, s.center.y);
                    parts.aura = holder;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI * 2f / 8f;
                        var home = new Vector3(Mathf.Cos(a) * (s.rx + 0.02f), (k % 2) * 0.006f, Mathf.Sin(a) * (s.rz + 0.02f));
                        var rune = Part(holder, PrimitiveType.Cube, glow, home, new Vector3(0.007f, 0.01f, 0.0015f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f));
                        parts.motes.Add((rune, home, k * 1.3f));
                    }
                    break;
                }
                case KnifeModel.GloveScales:
                    // Small overlapping scales lying on the back of the hand and up each finger,
                    // a glow showing between them
                    for (int row = 0; row < 2; row++)
                    {
                        float y = yW + span * (0.02f + row * 0.1f); // a band of scales at the wrist
                        float half = fit.Section(y).rx * 0.78f;
                        int cols = 5 - row % 2;
                        for (int col = 0; col < cols; col++)
                        {
                            float x = Mathf.Lerp(-half, half, cols == 1 ? 0.5f : col / (float)(cols - 1)) + fit.Section(y).center.x;
                            Part(t, PrimitiveType.Sphere, shell, Above(x, y, 0.001f), new Vector3(0.017f, 0.021f, 0.0045f), OnBack(x, y) * Quaternion.Euler(-18f, 0f, 0f));
                            Part(t, PrimitiveType.Sphere, glow, Above(x, y + 0.008f, 0.0f), new Vector3(0.008f, 0.004f, 0.002f), OnBack(x, y));
                        }
                    }
                    for (int f = 0; f < 4; f++)
                        for (int k = 0; k < 2; k++)
                        {
                            var (a, b) = fit.Segment(f, 0);
                            Vector3 c = Vector3.Lerp(a, b, 0.3f + k * 0.4f);
                            var scale = Part(t, PrimitiveType.Sphere, shell, fit.Surface(c.x, c.y, 0.004f) + Vector3.forward * 0.001f, new Vector3(0.013f, 0.016f, 0.0035f), Quaternion.Euler(-18f, 0f, 0f));
                            Hang(scale, f, 0);
                        }
                    break;
                case KnifeModel.GloveKnuckles:
                    // Glowing rings on each finger and a coil of energy up the wrist
                    for (int f = 0; f < 4; f++) FingerRing(glow, f, 0.45f, 0.0022f);
                    for (int k = 0; k < 3; k++) Ring(glow, yW - 0.004f - k * 0.013f, 0.004f, 0.0018f);
                    break;
                case KnifeModel.GloveBone:
                {
                    // Bones laid over the back of the hand and along every finger, and a small
                    // skull at the wrist with burning eyes
                    Material bone = Mat(new Color(0.88f, 0.84f, 0.74f), 0.6f, 0.1f);
                    for (int f = 0; f < 4; f++)
                    {
                        Vector3 k0 = fit.At(fit.fingers[f, 0]);
                        Vector3 from = Above(k0.x * 0.6f, yW + span * 0.2f, 0.002f), to = Above(k0.x, yK - 0.004f, 0.002f);
                        Rod(t, bone, from, to, 0.005f);
                        Part(t, PrimitiveType.Sphere, bone, to, Vector3.one * 0.0075f);
                        for (int j = 0; j < 3; j++)
                        {
                            var (a, b) = fit.Segment(f, j);
                            Vector3 lift = Vector3.forward * (fit.Surface(a.x, a.y, 0.004f).z - a.z + 0.0015f);
                            var phalanx = Rod(t, bone, a + lift + (b - a) * 0.15f, b + lift - (b - a) * 0.15f, 0.0045f);
                            Hang(phalanx, f, j);
                        }
                    }
                    Vector3 skull = Above(0f, yW + span * 0.1f, 0.009f);
                    Part(t, PrimitiveType.Sphere, bone, skull, new Vector3(0.022f, 0.022f, 0.018f));
                    foreach (float x in new[] { -0.005f, 0.005f })
                        Part(t, PrimitiveType.Sphere, glow, skull + new Vector3(x, 0.002f, 0.008f), Vector3.one * 0.005f);
                    break;
                }
                case KnifeModel.GloveCrystal:
                {
                    // Crystal shards breaking out of the back of the hand
                    var random = new System.Random(3);
                    for (int k = 0; k < 6; k++)
                    {
                        float x = palm.center.x + ((float)random.NextDouble() - 0.5f) * palm.rx * 1.1f;
                        float y = yW + span * (0.2f + (float)random.NextDouble() * 0.6f);
                        var tilt = OnBack(x, y) * Quaternion.Euler(90f + ((float)random.NextDouble() - 0.5f) * 40f, 0f, ((float)random.NextDouble() - 0.5f) * 50f);
                        MeshPart(t, k % 2 == 0 ? glow : shell, CrystalMesh(0.032f + (float)random.NextDouble() * 0.02f, 0.009f, 5, 0.3f), fit.Surface(x, y) - fit.Normal(x, y) * 0.002f, tilt);
                    }
                    break;
                }
                case KnifeModel.GloveWings:
                {
                    // Small feathered wings swept back from either side of the wrist, and a halo
                    var s = fit.Section(yW + 0.004f);
                    foreach (float side in new[] { -1f, 1f })
                        for (int f = 0; f < 3; f++)
                        {
                            float len = 0.05f - f * 0.009f;
                            var a = new[] { new Vector2(0f, 0f), new Vector2(len * 0.5f, 0.007f), new Vector2(len, 0.0015f) };
                            var b = new[] { new Vector2(0f, -0.008f), new Vector2(len * 0.5f, -0.004f), new Vector2(len, 0.0015f) };
                            MeshPart(t, f == 1 ? glow : finish, RailBlade(a, b, 0.0015f, 0.0006f),
                                new Vector3(s.center.x + side * (s.rx + 0.001f), yW + 0.004f + f * 0.006f, s.center.y), Quaternion.Euler(0f, side > 0f ? 0f : 180f, -70f - f * 12f));
                        }
                    Ring(glow, yW - 0.012f, 0.008f, 0.0016f);
                    break;
                }
                case KnifeModel.GloveStorm:
                    // Coils round the wrist, and prongs standing on the knuckles, crackling at the tips
                    foreach (float y in new[] { yW - 0.006f, yW - 0.018f })
                        Ring(finish, y, 0.003f, 0.0028f);
                    for (int f = 0; f < 4; f++)
                    {
                        float x = fit.At(fit.fingers[f, 0]).x;
                        float y = yK - span * 0.12f;
                        Vector3 foot = fit.Surface(x, y), up = fit.Normal(x, y);
                        Rod(t, dark, foot, foot + up * 0.014f, 0.0028f);
                        Part(t, PrimitiveType.Sphere, glow, foot + up * 0.016f, Vector3.one * 0.0055f);
                    }
                    break;
                case KnifeModel.GloveWraps:
                {
                    // Dark bandage wraps round the hand and wrist, one burning, and a hooked blade
                    // off the side of the wrist
                    Material cloth = Mat(new Color(0.12f, 0.1f, 0.1f), 0.2f, 0f);
                    // Round the wrist, and round the top of the palm above the thumb
                    var wrapAt = new[] { yW - 0.022f, yW - 0.011f, yW, yK - span * 0.28f, yK - span * 0.14f };
                    for (int k = 0; k < 5; k++)
                    {
                        float y = wrapAt[k];
                        Ring(k == 2 ? glow : cloth, y, 0.0008f, 0.001f, 4f, k % 2 == 0 ? 6f : -6f);
                    }
                    var s = fit.Section(yW);
                    var sp = new[] { new Vector2(0f, 0f), new Vector2(0.022f, 0.009f), new Vector2(0.04f, 0.004f), new Vector2(0.048f, -0.011f) };
                    var ed = new[] { new Vector2(0f, -0.01f), new Vector2(0.022f, -0.003f), new Vector2(0.037f, -0.004f), new Vector2(0.048f, -0.011f) };
                    MeshPart(t, finish, RailBlade(sp, ed, 0.002f, 0.0004f), new Vector3(s.center.x + s.rx + 0.003f, yW, s.center.y), Quaternion.Euler(0f, 0f, -80f));
                    break;
                }
            }
            // Flames licking up off the knuckles
            var flames = new List<Vector3>();
            for (int f = 0; f < 3; f++)
            {
                float x = fit.At(fit.fingers[f, 0]).x;
                flames.Add(fit.Surface(x, yK - span * 0.1f));
            }
            AddFlames(parts, flames, 0.024f, 0.03f); // small, licking off the knuckles
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
            if (skin.asset != null) return FromModel(skin, parent);
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
            if (skin.finish != KnifeFinish.Polished && skin.rarity != SkinRarity.Void) LayFinish(parts, skin.finish, Vector3.forward, Vector3.up);
            if (burn.Count > 0) AddFlames(parts, burn, 0.05f, 0.04f);
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
            // Every finish is glossy, but only the real metals are fully metallic: painted and
            // anodised skins are a coloured coat over the steel (as in the classic shooters), so
            // their colours show from every angle instead of going dark away from the light
            var m = Mat(look.tint, Mathf.Max(look.smoothness, 0.85f), look.metallic);
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

        // Lays a skin over the whole weapon as one continuous piece, the way the classic
        // shooters' skins wrap a model: every part wearing the finish is mapped from the same
        // side-on projection (v running `along` the weapon from its back end to its front, u
        // `across` it), at one scale, so the pattern and the fade run on unbroken from part to
        // part instead of each part squeezing in, or restarting, the whole texture
        void LayFinish(WeaponParts parts, KnifeFinish finish, Vector3 along, Vector3 across)
        {
            var look = KnifeFinishes.Get(finish);
            if (!look.albedo) return;
            along.Normalize();
            across = (across - Vector3.Dot(across, along) * along).normalized;
            var filters = new List<(MeshFilter f, Vector3[] p)>();
            float lo = float.MaxValue, hi = float.MinValue, wide = 0f;
            foreach (var r in parts.root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.sharedMaterial || r.sharedMaterial.GetTexture("_BaseMap") != look.albedo) continue;
                var f = r.GetComponent<MeshFilter>();
                if (!f || !f.sharedMesh) continue;
                var verts = f.sharedMesh.vertices;
                var p = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    p[i] = parts.root.InverseTransformPoint(f.transform.TransformPoint(verts[i]));
                    float a = Vector3.Dot(p[i], along);
                    lo = Mathf.Min(lo, a); hi = Mathf.Max(hi, a);
                    wide = Mathf.Max(wide, Mathf.Abs(Vector3.Dot(p[i], across)));
                }
                filters.Add((f, p));
            }
            if (filters.Count == 0) return;
            float length = Mathf.Max(hi - lo, 1e-3f);
            // The textures are painted for a width a third of their length; wider weapons get
            // a little more room so nothing runs off the side
            float width = Mathf.Max(length / 3f, wide * 2.2f);
            foreach (var (f, p) in filters)
            {
                var mesh = Object.Instantiate(f.sharedMesh);
                var uv = new Vector2[p.Length];
                for (int i = 0; i < p.Length; i++)
                    uv[i] = new Vector2(0.5f + Vector3.Dot(p[i], across) / width, (Vector3.Dot(p[i], along) - lo) / length);
                mesh.uv = uv;
                f.sharedMesh = mesh;
            }
            // Photos stay square: the length shows several copies' worth
            if (look.photo)
                foreach (var (f, _) in filters)
                    f.GetComponent<MeshRenderer>().sharedMaterial.SetTextureScale("_BaseMap", new Vector2(1f, length / width));
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
