using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // Something a sniper shot breaks (skeet discs, Void Beasts): shots look for these on
    // trigger colliders
    public interface ISnipeTarget { void Break(); }

    // A disc you can shoot. Its trigger collider is what sniper shots look for.
    public class SkeetTarget : MonoBehaviour, ISnipeTarget
    {
        public SkeetRange range;
        public void Break() => range.Hit(this);
    }

    // Skeet in the start hall: walk up to the start button and press E. The launcher throws
    // glowing discs, each its own color with a light trail, up and across the hall one after
    // another; snipe each one before it hits the floor. The scoreboard shows a pip per disc,
    // your streak, and a goal marker: hit 4 and a Void Case drops into your inventory.
    public class SkeetRange : MonoBehaviour
    {
        [Tooltip("Discs leave from here, thrown along its forward direction and tipped up")]
        public Transform launcher;
        [Tooltip("Stand near this and press E to start")]
        public Transform startButton;
        [Tooltip("Any URP Lit material; the disc materials are made from it")]
        public Material template;
        public int discsPerRound = 10;
        public int hitsForCase = 4;
        public float interval = 1.6f;
        public float useRange = 2.8f;

        const string BestKey = "VoidFlow.skeetBest";
        static readonly Color[] Palette =
        {
            new(1f, 0.45f, 0.1f), new(1f, 0.25f, 0.7f), new(0.3f, 0.85f, 1f), new(0.55f, 1f, 0.25f), new(1f, 0.82f, 0.2f), new(0.7f, 0.35f, 1f),
        };

        class Disc { public Transform t; public Vector3 velocity; public float spin; public int n; }
        class Shard { public Transform t; public Vector3 velocity; public float age; }
        class Popup { public string text; public Color color; public Vector2 at; public float time, size; }

        readonly List<Disc> discs = new();
        readonly List<Shard> shards = new();
        readonly List<Popup> popups = new();
        readonly Dictionary<int, (Material body, Material rim)> discMats = new();
        readonly Dictionary<int, Material> trailMats = new();
        AudioSource audioSource;
        PlayerMovement player;
        bool running, near, caseEarned;
        int launched, hits, misses, best, streak, bestStreak;
        int[] results = new int[10]; // 0 waiting, 1 hit, 2 missed
        float nextLaunch, resultTime = -99f, scoreTime = -99f, startTime = -99f;

        void Start() => best = PlayerPrefs.GetInt(BestKey, 0);

        void OnDestroy()
        {
            foreach (var (body, rim) in discMats.Values) { if (body) Destroy(body); if (rim) Destroy(rim); }
            foreach (var m in trailMats.Values) if (m) Destroy(m);
        }

        void Update()
        {
            if (!player) player = FindAnyObjectByType<PlayerMovement>();
            float dt = Time.deltaTime;

            if (!running && player && startButton)
            {
                Vector3 d = player.Position - startButton.position;
                d.y = 0f;
                near = d.magnitude < useRange && !ViewModel.InputBlocked;
                var kb = Keyboard.current;
                if (near && kb != null && kb.eKey.wasPressedThisFrame) StartRound();
            }
            else near = false;

            if (running && launched < discsPerRound && Time.time >= nextLaunch)
            {
                Launch();
                nextLaunch = Time.time + interval;
            }

            // Discs fly under gravity and spin; touching the floor is a miss
            float floor = transform.position.y + 0.05f;
            for (int i = discs.Count - 1; i >= 0; i--)
            {
                var d = discs[i];
                if (!d.t) { discs.RemoveAt(i); continue; }
                d.velocity += Physics.gravity * dt;
                d.t.position += d.velocity * dt;
                d.t.Rotate(0f, d.spin * dt, 0f, Space.Self);
                if (d.t.position.y < floor)
                {
                    misses++;
                    streak = 0;
                    if (d.n < results.Length) results[d.n] = 2;
                    if (FxLibrary.Instance) FxLibrary.Puff(d.t.position, new Color(0.9f, 0.5f, 0.2f, 0.8f));
                    else Burst(d.t.position, 4, 3f, d.n);
                    AddPopup("MISS", new Color(1f, 0.35f, 0.35f), d.t.position, 18f);
                    Destroy(d.t.gameObject);
                    discs.RemoveAt(i);
                }
            }
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var s = shards[i];
                s.age += dt;
                if (s.age > 0.8f || !s.t) { if (s.t) Destroy(s.t.gameObject); shards.RemoveAt(i); continue; }
                s.velocity += Physics.gravity * dt;
                s.t.position += s.velocity * dt;
                s.t.Rotate(400f * dt, 300f * dt, 0f);
                s.t.localScale = Vector3.one * 0.06f * (1f - s.age / 0.8f);
            }
            popups.RemoveAll(p => Time.time - p.time > 1.1f);

            if (running && launched >= discsPerRound && discs.Count == 0)
            {
                running = false;
                resultTime = Time.time;
                Play(hits >= hitsForCase ? WeaponSounds.Reveal : WeaponSounds.Tick, 0.6f);
                if (hits > best)
                {
                    best = hits;
                    PlayerPrefs.SetInt(BestKey, best);
                    PlayerPrefs.Save();
                }
            }
        }

        void StartRound()
        {
            running = true;
            launched = hits = misses = streak = bestStreak = 0;
            caseEarned = false;
            if (results.Length != discsPerRound) results = new int[discsPerRound];
            System.Array.Clear(results, 0, results.Length);
            nextLaunch = Time.time + 1.2f; // a moment to get ready
            startTime = Time.time;
            resultTime = -99f;
            Play(WeaponSounds.BoltBack, 0.7f);
        }

        (Material body, Material rim) DiscMaterials(int n)
        {
            int c = n % Palette.Length;
            if (discMats.TryGetValue(c, out var m)) return m;
            m = (MakeMaterial(Palette[c], 1.8f), MakeMaterial(Color.Lerp(Palette[c], Color.white, 0.55f), 3.2f));
            discMats[c] = m;
            return m;
        }

        GameObject Piece(Transform parent, Material mat, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        void Launch()
        {
            int n = launched++;
            var (body, rim) = DiscMaterials(n);
            Color color = Palette[n % Palette.Length];
            // A random throw: up 38 to 52 degrees, up to 22 degrees either side, varying speed
            Quaternion aim = Quaternion.Euler(-Random.Range(38f, 52f), Random.Range(-22f, 22f), 0f);
            Vector3 velocity = launcher.rotation * aim * Vector3.forward * Random.Range(13f, 16.5f);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Skeet Disc";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(transform, true);
            disc.transform.position = launcher.position;
            disc.transform.localScale = new Vector3(0.36f, 0.035f, 0.36f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = body;
            Piece(disc.transform, rim, new Vector3(1.1f, 0.5f, 1.1f));   // white-hot rim
            Piece(disc.transform, rim, new Vector3(0.4f, 1.3f, 0.4f));   // and core
            // A glowing trail in its color
            if (FxLibrary.Instance && FxLibrary.Instance.spark)
            {
                if (!trailMats.TryGetValue(n % Palette.Length, out var trailMat))
                {
                    // The spark sprite, tinted bright in the disc's color
                    trailMat = new Material(FxLibrary.Instance.spark);
                    trailMat.SetColor("_BaseColor", color * 2.5f);
                    trailMats[n % Palette.Length] = trailMat;
                }
                var trail = disc.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMat;
                trail.time = 0.35f;
                trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.22f, 1f, 0f);
                trail.startColor = new Color(color.r, color.g, color.b, 0.9f);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                trail.numCapVertices = 4;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // A generous hit sphere so a clean shot always counts
            var trigger = disc.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.75f;
            disc.AddComponent<SkeetTarget>().range = this;
            discs.Add(new Disc { t = disc.transform, velocity = velocity, spin = Random.Range(600f, 900f), n = n });
            Play(WeaponSounds.Launch, 0.8f);
            FxLibrary.MuzzleSmoke(launcher.position, velocity.normalized);
        }

        public void Hit(SkeetTarget target)
        {
            int i = discs.FindIndex(d => d.t == target.transform);
            if (i < 0) return;
            var disc = discs[i];
            Color color = Palette[disc.n % Palette.Length];
            hits++;
            streak++;
            bestStreak = Mathf.Max(bestStreak, streak);
            if (disc.n < results.Length) results[disc.n] = 1;
            scoreTime = Time.time;
            Vector3 at = target.transform.position;
            if (FxLibrary.Instance)
            {
                FxLibrary.Shatter(at, color);
                if (streak >= 3) FxLibrary.Celebrate(at, color, false);
            }
            else Burst(at, 10, 6f, disc.n);
            Play(WeaponSounds.Shatter, 0.9f);
            AddPopup(streak >= 2 ? $"HIT  x{streak}" : "HIT", color, at, 20f + Mathf.Min(streak, 6) * 2f);
            if (hits >= hitsForCase && !caseEarned)
            {
                caseEarned = true;
                Inventory.AddCase($"skeet: {hitsForCase} discs hit");
            }
            Destroy(target.gameObject);
            discs.RemoveAt(i);
        }

        void AddPopup(string text, Color color, Vector3 world, float size)
        {
            var cam = Camera.main;
            if (!cam) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            float k = UiScale;
            popups.Add(new Popup { text = text, color = color, at = new Vector2(sp.x / k, (Screen.height - sp.y) / k), time = Time.time, size = size });
        }

        void Burst(Vector3 at, int count, float speed, int n)
        {
            for (int k = 0; k < count; k++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(shard.GetComponent<Collider>());
                shard.transform.SetParent(transform, true);
                shard.transform.position = at;
                shard.GetComponent<MeshRenderer>().sharedMaterial = DiscMaterials(n).body;
                shards.Add(new Shard { t = shard.transform, velocity = Random.onUnitSphere * speed + Vector3.up * 2f });
            }
        }

        Material MakeMaterial(Color color, float glow)
        {
            var m = new Material(template);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.9f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * glow);
            return m;
        }

        void Play(AudioClip clip, float volume)
        {
            if (!clip) return;
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            audioSource.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ GUI

        static float UiScale => Mathf.Clamp(Screen.height / 800f, 0.6f, 2.2f);

        static (string, Color) Tier(int hits, int of) =>
            hits >= of ? ("PERFECT!", new Color(1f, 0.85f, 0.25f))
            : hits >= of - 1 ? ("DEADEYE", new Color(1f, 0.4f, 0.75f))
            : hits >= of * 0.7f ? ("SHARPSHOOTER", new Color(0.7f, 0.4f, 1f))
            : hits >= 4 ? ("NICE SHOOTING", new Color(0.35f, 0.9f, 1f))
            : ("KEEP PRACTICING", new Color(0.8f, 0.8f, 0.85f));

        void OnGUI()
        {
            if (Inventory.IsOpen) return;
            GUI.depth = -5;
            float k = UiScale;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float W = Screen.width / k, H = Screen.height / k, now = Time.time;

            if (near)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(now * 4f);
                var r = new Rect(W / 2f - 330f, H * 0.6f, 660f, 46f);
                UiArt.Rounded(r, new Color(0.04f, 0.02f, 0.08f, 0.8f), 23f);
                UiArt.Rounded(r, new Color(0.75f, 0.4f, 1f, pulse), 23f, 2f);
                UiArt.Text(r, $"[E]  START SKEET   ·   {discsPerRound} discs   ·   hit {hitsForCase} for a VOID CASE   ·   1 = sniper", 16, Color.white, TextAnchor.MiddleCenter);
            }

            float since = now - resultTime;
            bool showResult = !running && since < 6f;
            if (running || showResult)
            {
                float appear = running ? UiArt.BackOut(Mathf.Clamp01((now - startTime) / 0.4f)) : 1f;
                float a = running ? 1f : 1f - Mathf.Clamp01((since - 5.4f) / 0.6f);
                float pw = discsPerRound * 44f + 60f;
                var panel = new Rect(W / 2f - pw / 2f, 14f - (1f - appear) * 60f, pw, 112f);
                UiArt.Rounded(panel, new Color(0.04f, 0.02f, 0.08f, 0.85f * a), 18f);
                UiArt.Blob(new Vector2(panel.center.x, panel.y + 10f), pw * 0.9f, new Color(0.6f, 0.25f, 1f), 0.25f * a);
                UiArt.Rounded(panel, new Color(0.7f, 0.4f, 1f, 0.8f * a), 18f, 1.5f);
                UiArt.Text(new Rect(panel.x + 18f, panel.y + 8f, 120f, 20f), "SKEET", 13, new Color(0.8f, 0.65f, 1f, a));
                if (streak >= 2 && running)
                    UiArt.Text(new Rect(panel.xMax - 160f, panel.y + 8f, 142f, 20f), $"STREAK  x{streak}", 13, new Color(1f, 0.6f, 0.85f, a), TextAnchor.MiddleRight);

                // Score, popping on every hit
                float pop = 1f + 0.35f * Mathf.Max(0f, 1f - (now - scoreTime) / 0.3f);
                var scoreRect = UiArt.Grow(new Rect(panel.x, panel.y + 4f, panel.width, 44f), pop);
                UiArt.Text(scoreRect, $"{hits} / {discsPerRound}", Mathf.RoundToInt(30f * pop), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);

                // A pip per disc
                float px = panel.x + 37f;
                for (int i = 0; i < discsPerRound; i++)
                {
                    var pip = new Rect(px + i * 44f, panel.y + 58f, 30f, 30f);
                    Color c = Palette[i % Palette.Length];
                    int state = i < results.Length ? results[i] : 0;
                    bool flying = running && state == 0 && i < launched;
                    if (state == 1)
                    {
                        GUI.color = new Color(c.r, c.g, c.b, 0.55f * a);
                        GUI.DrawTexture(UiArt.Grow(pip, 2f), UiArt.Glow);
                        UiArt.Rounded(pip, new Color(c.r, c.g, c.b, a), 15f);
                        UiArt.Rounded(UiArt.Grow(pip, 0.4f), new Color(1f, 1f, 1f, 0.9f * a), 6f);
                    }
                    else if (state == 2)
                    {
                        UiArt.Rounded(pip, new Color(0.35f, 0.08f, 0.1f, 0.8f * a), 15f);
                        UiArt.Text(pip, "×", 20, new Color(1f, 0.4f, 0.4f, a), TextAnchor.MiddleCenter);
                    }
                    else
                    {
                        float pulse = flying ? 0.6f + 0.4f * Mathf.Sin(now * 10f) : 0.35f;
                        UiArt.Rounded(pip, new Color(c.r, c.g, c.b, pulse * a), 15f, flying ? 3f : 2f);
                    }
                    // The Void Case goal sits under the disc that earns it
                    if (i == hitsForCase - 1)
                    {
                        bool got = hits >= hitsForCase;
                        var tag = new Rect(pip.center.x - 50f, pip.yMax + 2f, 100f, 18f);
                        Color tc = got ? Color.Lerp(UiArt.CasePink, UiArt.CaseAmber, 0.5f + 0.5f * Mathf.Sin(now * 5f)) : new Color(0.4f, 0.25f, 0.55f);
                        UiArt.Pill(tag, got ? "CASE EARNED" : $"{hitsForCase} = VOID CASE", 10, new Color(tc.r, tc.g, tc.b, 0.95f * a), new Color(1f, 1f, 1f, a));
                    }
                }
            }

            if (showResult)
            {
                var (label, color) = Tier(hits, discsPerRound);
                float pop = UiArt.ElasticOut(Mathf.Clamp01(since / 0.6f));
                float a = 1f - Mathf.Clamp01((since - 5.4f) / 0.6f);
                var r = UiArt.Grow(new Rect(W / 2f - 250f, 262f, 500f, 50f), Mathf.Max(0.01f, pop));
                UiArt.Text(r, label, Mathf.Max(1, Mathf.RoundToInt(36f * pop)), new Color(color.r, color.g, color.b, a), TextAnchor.MiddleCenter);
                string line = (hits >= best && hits > 0 ? "NEW BEST   ·   " : $"best {best}   ·   ") + $"best streak {bestStreak}" + (caseEarned ? "   ·   +1 VOID CASE" : "");
                UiArt.Text(new Rect(0f, 314f, W, 24f), line, 16, new Color(1f, 1f, 1f, 0.9f * a), TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(0f, 340f, W, 22f), "press E at the button to go again", 14, new Color(0.8f, 0.7f, 1f, 0.8f * a), TextAnchor.MiddleCenter, false);
            }

            // HIT / MISS popping up where the disc was
            foreach (var p in popups)
            {
                float t = now - p.time;
                float a = 1f - Mathf.Clamp01((t - 0.6f) / 0.5f);
                float s = UiArt.BackOut(Mathf.Clamp01(t / 0.25f));
                var r = new Rect(p.at.x - 100f, p.at.y - 20f - t * 60f, 200f, 40f);
                UiArt.Text(r, p.text, Mathf.Max(1, Mathf.RoundToInt(p.size * s)), new Color(p.color.r, p.color.g, p.color.b, a), TextAnchor.MiddleCenter);
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }
}
