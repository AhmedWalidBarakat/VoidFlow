using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Void Beasts: every minute or so while you surf the course, a flaming monster appears in
    // the air off to the side of the ramp ahead. Snipe it before you pass it and a Void Case
    // drops straight into your inventory. A marker shows where it is (an arrow at the screen
    // edge when it's out of view); if you fly past, it escapes.
    public class VoidBeasts : MonoBehaviour
    {
        public EndlessCourse course;
        public PlayerMovement player;
        [Tooltip("Any URP Lit material; the beasts' materials are made from it")]
        public Material template;
        [Tooltip("Seconds on the course between beasts (random in this range)")]
        public Vector2 interval = new(45f, 80f);
        public float firstAfter = 20f;
        public float lifetime = 45f;

        VoidBeast beast;
        float timer, bannerTime = -99f, escapeTime = -99f;
        string banner = "";
        Color bannerColor = Color.white;
        AudioSource audioSource;

        void Start() => timer = firstAfter;

        void Update()
        {
            if (!course || !player) return;
            if (beast)
            {
                if (beast.Dead) return;
                // Flown past, or around too long: it escapes
                Vector3 to = player.Position - beast.transform.position;
                bool passed = Vector3.Dot(to, beast.Forward) > 80f;
                if (passed || beast.Age > lifetime || course.CurrentRamp < 1)
                {
                    beast.Vanish();
                    beast = null;
                    escapeTime = Time.time;
                }
                return;
            }
            if (course.CurrentRamp < 1) return; // only out on the course
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Spawn() ? Random.Range(interval.x, interval.y) : 3f;
        }

        bool Spawn()
        {
            if (!template) return false;
            if (!course.TrySpotAhead(1, Random.Range(0.45f, 0.85f), out var point, out var forward, out var right, out var root)) return false;
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 at = point + right * side * Random.Range(22f, 32f) + Vector3.up * Random.Range(16f, 24f);
            // Keep it out in the open air, clear of the ramps
            for (int tries = 0; tries < 3 && Physics.CheckSphere(at, 12f, player.collisionMask, QueryTriggerInteraction.Ignore); tries++)
                at += Vector3.up * 6f;
            if (Physics.CheckSphere(at, 12f, player.collisionMask, QueryTriggerInteraction.Ignore)) return false;

            var go = new GameObject("Void Beast");
            go.transform.SetParent(root, true);
            go.transform.position = at;
            beast = go.AddComponent<VoidBeast>();
            beast.Build(template, Random.Range(0, VoidBeast.Kinds.Length), forward, player, this);
            banner = $"{("AEIOU".IndexOf(beast.beastName[0]) >= 0 ? "AN" : "A")} {beast.beastName} APPEARED";
            bannerColor = beast.hue;
            bannerTime = Time.time;
            Play(WeaponSounds.Launch, 0.9f, 0.45f);
            Play(WeaponSounds.VoidReveal, 0.35f, 0.6f);
            return true;
        }

        public void Slain(VoidBeast b)
        {
            Inventory.AddCase($"{b.beastName} slain");
            Play(WeaponSounds.Shatter, 1f, 0.55f);
            Play(WeaponSounds.VoidReveal, 0.7f, 1f);
            if (b == beast) beast = null;
        }

        public void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (!clip) return;
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            if (Mathf.Approximately(pitch, 1f)) { audioSource.PlayOneShot(clip, volume); return; }
            var one = gameObject.AddComponent<AudioSource>();
            one.spatialBlend = 0f;
            one.pitch = pitch;
            one.PlayOneShot(clip, volume);
            Destroy(one, clip.length / pitch + 0.1f);
        }

        // ------------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (Inventory.IsOpen) return;
            GUI.depth = -5;
            float k = Mathf.Clamp(Screen.height / 800f, 0.6f, 2.2f);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float W = Screen.width / k, H = Screen.height / k, now = Time.time;

            // The announcement slides down from the top
            float bt = now - bannerTime;
            if (bt < 4f)
            {
                float a = Mathf.Clamp01(bt / 0.25f) * (1f - Mathf.Clamp01((bt - 3.4f) / 0.6f));
                float y = 120f - (1f - UiArt.BackOut(Mathf.Clamp01(bt / 0.45f))) * 60f;
                var r = new Rect(W / 2f - 300f, y, 600f, 74f);
                UiArt.Rounded(r, new Color(0.04f, 0.01f, 0.05f, 0.85f * a), 16f);
                UiArt.Blob(new Vector2(r.center.x, r.center.y), 420f, bannerColor, 0.35f * a);
                UiArt.Rounded(r, new Color(bannerColor.r, bannerColor.g, bannerColor.b, a), 16f, 2f);
                UiArt.Text(new Rect(r.x, r.y + 8f, r.width, 34f), banner, 26, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(r.x, r.y + 42f, r.width, 22f), "snipe it for a VOID CASE", 15, new Color(Mathf.Lerp(bannerColor.r, 1f, 0.4f), Mathf.Lerp(bannerColor.g, 1f, 0.4f), Mathf.Lerp(bannerColor.b, 1f, 0.4f), a), TextAnchor.MiddleCenter);
            }
            float et = now - escapeTime;
            if (et < 2.5f)
            {
                float a = Mathf.Clamp01(et / 0.2f) * (1f - Mathf.Clamp01((et - 2f) / 0.5f));
                UiArt.Text(new Rect(0f, 130f, W, 26f), "the Void Beast escaped...", 18, new Color(0.8f, 0.7f, 0.9f, a), TextAnchor.MiddleCenter);
            }

            // Where it is: brackets around it when on screen, an arrow at the edge when not
            var cam = Camera.main;
            if (beast && !beast.Dead && cam)
            {
                Vector3 target = beast.transform.position;
                Vector3 sp = cam.WorldToScreenPoint(target);
                float dist = Vector3.Distance(cam.transform.position, target);
                Color c = beast.hue;
                var p = new Vector2(sp.x / k, (Screen.height - sp.y) / k);
                bool onScreen = sp.z > 0f && p.x > 30f && p.x < W - 30f && p.y > 30f && p.y < H - 30f;
                if (onScreen)
                {
                    float size = Mathf.Clamp(2400f / Mathf.Max(dist, 1f), 26f, 90f) * (1f + 0.08f * Mathf.Sin(now * 6f));
                    float arm = size * 0.35f, th = 3f;
                    GUI.color = new Color(c.r, c.g, c.b, 0.9f);
                    foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                    {
                        float cx = p.x + sx * size / 2f, cy = p.y + sy * size / 2f;
                        GUI.DrawTexture(new Rect(sx < 0 ? cx : cx - arm, cy - th / 2f, arm, th), Texture2D.whiteTexture);
                        GUI.DrawTexture(new Rect(cx - th / 2f, sy < 0 ? cy : cy - arm, th, arm), Texture2D.whiteTexture);
                    }
                    UiArt.Text(new Rect(p.x - 150f, p.y - size / 2f - 30f, 300f, 22f), $"{beast.beastName}  ·  {dist:0}m", 14, new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleCenter);
                }
                else
                {
                    // Direction on screen, flipped when it's behind you
                    Vector2 dir = new Vector2(sp.x / k - W / 2f, (Screen.height - sp.y) / k - H / 2f);
                    if (sp.z < 0f) dir = -dir;
                    if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                    dir.Normalize();
                    float scale = Mathf.Min((W / 2f - 60f) / Mathf.Max(Mathf.Abs(dir.x), 0.001f), (H / 2f - 60f) / Mathf.Max(Mathf.Abs(dir.y), 0.001f));
                    var at = new Vector2(W / 2f, H / 2f) + dir * scale;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    float bob = 1f + 0.12f * Mathf.Sin(now * 7f);
                    var m = GUI.matrix;
                    // Rotate about the arrow's spot (in screen pixels, since the GUI is scaled)
                    GUI.matrix = m * Matrix4x4.TRS(at, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(-at, Quaternion.identity, Vector3.one);
                    GUI.color = new Color(c.r, c.g, c.b, 0.35f);
                    GUI.DrawTexture(new Rect(at.x - 40f * bob, at.y - 40f * bob, 80f * bob, 80f * bob), UiArt.Glow);
                    GUI.color = new Color(Mathf.Lerp(c.r, 1f, 0.3f), Mathf.Lerp(c.g, 1f, 0.3f), Mathf.Lerp(c.b, 1f, 0.3f), 1f);
                    GUI.DrawTexture(new Rect(at.x - 22f * bob, at.y - 22f * bob, 44f * bob, 44f * bob), UiArt.Arrow);
                    GUI.matrix = m;
                    UiArt.Text(new Rect(at.x - 100f - dir.x * 50f, at.y - 11f - dir.y * 44f, 200f, 22f), $"VOID BEAST  {dist:0}m", 13, Color.white, TextAnchor.MiddleCenter);
                }
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }

    // One Void Beast: a floating horned skull-creature with burning eyes, bat wings, a snapping
    // jaw full of teeth, a spinning rune ring and flames rolling off it. It turns to face you.
    // One sniper hit kills it.
    public class VoidBeast : MonoBehaviour, ISnipeTarget
    {
        public static readonly (string name, Color hue)[] Kinds =
        {
            ("VOID WRAITH", new Color(0.7f, 0.3f, 1f)),
            ("EMBER FIEND", new Color(1f, 0.42f, 0.08f)),
            ("FROST MAW", new Color(0.3f, 0.85f, 1f)),
            ("VENOM HORROR", new Color(0.45f, 1f, 0.25f)),
            ("BLOOD DEMON", new Color(1f, 0.12f, 0.2f)),
        };
        const float Size = 4f; // a giant in the sky: easy to spot from far off

        public string beastName;
        public Color hue;
        public bool Dead { get; private set; }
        public float Age => Time.time - born;
        public Vector3 Forward { get; private set; }

        PlayerMovement player;
        VoidBeasts owner;
        Transform body, jaw, wingL, wingR, ring;
        readonly List<Transform> eyes = new();
        readonly List<Material> materials = new();
        WeaponParts parts;
        Light glow;
        SphereCollider hitSphere;
        float born, endTime = -1f, phase;
        bool vanishing;

        public void Build(Material template, int kind, Vector3 forward, PlayerMovement player, VoidBeasts owner)
        {
            (beastName, hue) = Kinds[kind];
            Forward = forward;
            this.player = player;
            this.owner = owner;
            born = Time.time;
            phase = Random.value * 10f;
            var b = new WeaponBuilder(template, gameObject.layer, false, materials);
            parts = new WeaponParts { root = transform, hue = hue, rarity = SkinRarity.Void };

            Material hide = b.Mat(Color.Lerp(new Color(0.09f, 0.06f, 0.12f), hue, 0.12f), 0.92f, 0.55f);
            Material plate = b.Mat(Color.Lerp(new Color(0.1f, 0.09f, 0.12f), hue, 0.18f), 0.92f, 0.9f);
            Material bone = b.Mat(new Color(0.9f, 0.86f, 0.78f), 0.7f, 0.1f);
            Material burn = b.Glow(hue, 4f, parts);
            Material eye = b.Glow(Color.Lerp(hue, Color.white, 0.55f), 8f, parts);

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            // Skull and armoured brow
            b.Part(body, PrimitiveType.Sphere, hide, Vector3.zero, new Vector3(2.2f, 1.9f, 2f));
            b.Part(body, PrimitiveType.Cube, plate, new Vector3(0f, 0.45f, -0.72f), new Vector3(1.6f, 0.36f, 0.7f), Quaternion.Euler(-18f, 0f, 0f));
            b.Part(body, PrimitiveType.Cube, plate, new Vector3(0f, 0.75f, -0.2f), new Vector3(0.4f, 0.3f, 1.3f), Quaternion.Euler(-10f, 0f, 0f));
            // Slanted burning eyes
            foreach (float sx in new[] { -1f, 1f })
                eyes.Add(b.Part(body, PrimitiveType.Sphere, eye, new Vector3(sx * 0.42f, 0.22f, -0.9f), new Vector3(0.44f, 0.2f, 0.2f), Quaternion.Euler(0f, 0f, sx * -20f)));
            // Glowing cracks across the skull
            var random = new System.Random(kind * 31 + 7);
            for (int k = 0; k < 9; k++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f, y = ((float)random.NextDouble() - 0.3f) * 1.4f;
                var at = new Vector3(Mathf.Cos(a) * 1.02f, y * 0.6f, Mathf.Sin(a) * 0.95f);
                if (at.z < -0.6f && Mathf.Abs(at.x) < 0.7f) continue; // not across the face
                b.Part(body, PrimitiveType.Cube, burn, at, new Vector3(0.08f, 0.5f + (float)random.NextDouble() * 0.5f, 0.08f),
                    Quaternion.LookRotation(at) * Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 90f));
            }
            // Horns sweeping up and back, spikes down the spine
            foreach (float sx in new[] { -1f, 1f })
            {
                b.MeshPart(body, bone, WeaponBuilder.CrystalMesh(1.6f, 0.22f, 7, 0.5f), new Vector3(sx * 0.72f, 0.7f, -0.15f), Quaternion.Euler(-30f, 0f, sx * -38f));
                b.MeshPart(body, plate, WeaponBuilder.CrystalMesh(0.8f, 0.14f, 6, 0.3f), new Vector3(sx * 0.95f, 0.2f, 0.1f), Quaternion.Euler(0f, 0f, sx * -80f));
            }
            for (int k = 0; k < 5; k++)
                b.MeshPart(body, plate, WeaponBuilder.CrystalMesh(0.75f - k * 0.1f, 0.15f, 5, 0.2f), new Vector3(0f, 0.85f - k * 0.3f, 0.35f + k * 0.25f), Quaternion.Euler(35f + k * 16f, 0f, 0f));
            // Snapping jaw with teeth, and the fire inside the mouth
            b.Part(body, PrimitiveType.Cube, burn, new Vector3(0f, -0.4f, -0.72f), new Vector3(0.95f, 0.14f, 0.4f));
            for (int k = -3; k <= 3; k++)
                b.MeshPart(body, bone, WeaponBuilder.CrystalMesh(0.3f, 0.065f, 4, 0f), new Vector3(k * 0.14f, -0.33f, -0.92f + Mathf.Abs(k) * 0.03f), Quaternion.Euler(180f, 0f, 0f));
            jaw = new GameObject("Jaw").transform;
            jaw.SetParent(body, false);
            jaw.localPosition = new Vector3(0f, -0.5f, -0.2f);
            b.Part(jaw, PrimitiveType.Cube, hide, new Vector3(0f, -0.15f, -0.5f), new Vector3(1.25f, 0.28f, 0.95f));
            for (int k = -3; k <= 3; k++)
                b.MeshPart(jaw, bone, WeaponBuilder.CrystalMesh(0.24f, 0.055f, 4, 0f), new Vector3(k * 0.15f, 0f, -0.85f + Mathf.Abs(k) * 0.03f), Quaternion.identity);
            // Bat wings with burning edges
            foreach (float sx in new[] { -1f, 1f })
            {
                var pivot = new GameObject(sx < 0 ? "Wing L" : "Wing R").transform;
                pivot.SetParent(body, false);
                pivot.localPosition = new Vector3(sx * 0.95f, 0.3f, 0.35f);
                var spine = new[] { new Vector2(0f, 0f), new Vector2(1.2f, 0.9f), new Vector2(2.6f, 1.25f), new Vector2(3.5f, 0.5f) };
                var edge = new[] { new Vector2(0f, -0.35f), new Vector2(1.1f, -0.7f), new Vector2(2.2f, -0.35f), new Vector2(3.5f, 0.5f) };
                var wing = new GameObject("Membrane").transform;
                wing.SetParent(pivot, false);
                wing.localRotation = Quaternion.Euler(0f, sx < 0 ? 180f : 0f, 0f);
                b.MeshPart(wing, hide, WeaponBuilder.RailBlade(spine, edge, 0.04f, 0.02f), Vector3.zero, Quaternion.identity);
                for (int k = 0; k < spine.Length - 1; k++)
                    b.Rod(wing, burn, spine[k], spine[k + 1], 0.07f);
                if (sx < 0) wingL = pivot; else wingR = pivot;
            }
            // A ring of burning runes spinning round it
            ring = b.MeshPart(transform, burn, WeaponBuilder.Torus(2.3f, 0.05f, 64, 6), Vector3.zero, Quaternion.identity);
            // Flames rolling off the skull, horns and wings
            var flameAt = new List<Vector3>
            {
                new(0f, 0.95f, 0f), new(-0.6f, 0.8f, 0.2f), new(0.6f, 0.8f, 0.2f), new(0f, 0.7f, 0.7f),
                new(-1.3f, 1.3f, -0.2f), new(1.3f, 1.3f, -0.2f), new(-2.6f, 1.2f, 0.3f), new(2.6f, 1.2f, 0.3f),
                new(0f, -0.6f, -0.8f),
            };
            b.AddFlames(parts, flameAt, 1.1f, 1.5f);

            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.type = LightType.Point;
            glow.color = hue;
            glow.range = 45f;
            glow.intensity = 4f;

            hitSphere = gameObject.AddComponent<SphereCollider>();
            hitSphere.isTrigger = true;
            hitSphere.radius = 1.6f; // about the skull; the wings don't count
            transform.localScale = Vector3.one * 0.01f;
            FxLibrary.Celebrate(transform.position, hue, true);
            FxLibrary.Sparkle(transform, hue, 9f, 18f); // embers drifting round it
        }

        void Update()
        {
            float t = Time.time - born, now = Time.time + phase;
            // Pops in, and on death swells then bursts; escaping, it shrinks away in smoke
            float scale;
            if (endTime >= 0f)
            {
                float e = (Time.time - endTime) / (vanishing ? 0.7f : 0.35f);
                if (e >= 1f) { Destroy(gameObject); return; }
                scale = vanishing ? 1f - e * e : 1f + Mathf.Sin(e * Mathf.PI) * 0.35f - e * e;
                body.Rotate(0f, (vanishing ? 360f : 900f) * Time.deltaTime, 0f, Space.Self);
            }
            else scale = UiArt.ElasticOut(Mathf.Clamp01(t / 0.8f));
            transform.localScale = Vector3.one * Mathf.Max(0.01f, scale) * Size;

            // Turn to face you
            if (player && endTime < 0f)
            {
                Vector3 to = player.Position - transform.position;
                to.y *= 0.4f;
                if (to.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-to), 1f - Mathf.Exp(-2.5f * Time.deltaTime));
            }
            body.localPosition = new Vector3(Mathf.Sin(now * 0.7f) * 0.3f, Mathf.Sin(now * 1.3f) * 0.45f, 0f);
            if (endTime < 0f) body.localRotation = Quaternion.Euler(Mathf.Sin(now * 0.9f) * 7f, 0f, Mathf.Sin(now * 1.1f) * 6f);
            float chomp = Mathf.Pow(Mathf.Abs(Mathf.Sin(now * 1.7f)), 3f);
            jaw.localRotation = Quaternion.Euler(-32f * chomp, 0f, 0f);
            float flap = Mathf.Sin(now * 3.2f) * 28f;
            wingR.localRotation = Quaternion.Euler(0f, -18f, 8f + flap);
            wingL.localRotation = Quaternion.Euler(0f, 18f, -8f - flap);
            ring.localRotation = Quaternion.Euler(72f + Mathf.Sin(now * 0.8f) * 10f, now * 70f, 0f);
            float flicker = 0.85f + 0.15f * Mathf.Sin(now * 13f) + (Mathf.Sin(now * 2.3f) > 0.97f ? -0.6f : 0f);
            foreach (var e in eyes) e.localScale = new Vector3(0.44f, 0.2f * Mathf.Max(0.15f, flicker), 0.2f);
            glow.intensity = 4f + Mathf.Sin(now * 5f) * 1f;
            parts.Animate(now, -1f);
            if (parts.flames) parts.flames.boost = endTime >= 0f ? 1f : 0.35f + 0.25f * chomp;
        }

        public void Break()
        {
            if (Dead || endTime >= 0f) return;
            Dead = true;
            endTime = Time.time;
            hitSphere.enabled = false;
            Vector3 at = transform.position;
            // A giant goes out big: bursts all over its body
            for (int k = 0; k < 7; k++)
                FxLibrary.Shatter(at + Random.insideUnitSphere * Size * 1.4f, k % 2 == 0 ? hue : Color.Lerp(hue, Color.white, 0.4f));
            FxLibrary.Celebrate(at, hue, true);
            FxLibrary.Celebrate(at + Vector3.up * Size, Color.Lerp(hue, Color.white, 0.3f), true);
            FxLibrary.Puff(at, new Color(hue.r * 0.3f, hue.g * 0.3f, hue.b * 0.3f, 0.9f));
            if (owner) owner.Slain(this);
        }

        public void Vanish()
        {
            if (endTime >= 0f) return;
            vanishing = true;
            endTime = Time.time;
            hitSphere.enabled = false;
            FxLibrary.Puff(transform.position, new Color(0.1f, 0.05f, 0.15f, 0.9f));
        }

        void OnDestroy()
        {
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh && mf.sharedMesh.name is "Blade" or "Crystal" or "Ring") Destroy(mf.sharedMesh);
            foreach (var m in materials) if (m) Destroy(m);
        }
    }
}
