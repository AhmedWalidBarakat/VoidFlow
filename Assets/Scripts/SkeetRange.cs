using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // A disc you can shoot. Its trigger collider is what sniper shots look for.
    public class SkeetTarget : MonoBehaviour
    {
        public SkeetRange range;
        public void Break() => range.Hit(this);
    }

    // Skeet in the start hall: walk up to the start button and press E. The launcher throws
    // glowing clay discs up and across the hall one after another; snipe each one before it
    // hits the floor. Shows your hits as you go, then your score and best.
    public class SkeetRange : MonoBehaviour
    {
        [Tooltip("Discs leave from here, thrown along its forward direction and tipped up")]
        public Transform launcher;
        [Tooltip("Stand near this and press E to start")]
        public Transform startButton;
        [Tooltip("Any URP Lit material; the disc materials are made from it")]
        public Material template;
        public int discsPerRound = 10;
        public float interval = 1.6f;
        public float useRange = 2.8f;

        const string BestKey = "VoidFlow.skeetBest";

        class Disc { public Transform t; public Vector3 velocity; public float spin; }
        class Shard { public Transform t; public Vector3 velocity; public float age; }

        readonly List<Disc> discs = new();
        readonly List<Shard> shards = new();
        Material discMat, rimMat, shardMat;
        AudioSource audioSource;
        PlayerMovement player;
        GUIStyle bigStyle, smallStyle, promptStyle;
        bool running, near;
        int launched, hits, misses, best;
        float nextLaunch, resultTime = -99f;

        void Start() => best = PlayerPrefs.GetInt(BestKey, 0);

        void OnDestroy()
        {
            foreach (var m in new[] { discMat, rimMat, shardMat }) if (m) Destroy(m);
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
                    Burst(d.t.position, 4, 3f);
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

            if (running && launched >= discsPerRound && discs.Count == 0)
            {
                running = false;
                resultTime = Time.time;
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
            launched = hits = misses = 0;
            nextLaunch = Time.time + 1.2f; // a moment to get ready
            resultTime = -99f;
        }

        void Launch()
        {
            if (!discMat)
            {
                discMat = MakeMaterial(new Color(1f, 0.45f, 0.1f), 1.6f);
                rimMat = MakeMaterial(new Color(0.8f, 0.3f, 1f), 2.4f);
                shardMat = MakeMaterial(new Color(1f, 0.55f, 0.2f), 2f);
            }
            launched++;
            // A random throw: up 38 to 52 degrees, up to 22 degrees either side, varying speed
            Quaternion aim = Quaternion.Euler(-Random.Range(38f, 52f), Random.Range(-22f, 22f), 0f);
            Vector3 velocity = launcher.rotation * aim * Vector3.forward * Random.Range(13f, 16.5f);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Skeet Disc";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(transform, true);
            disc.transform.position = launcher.position;
            disc.transform.localScale = new Vector3(0.36f, 0.035f, 0.36f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = discMat;
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(rim.GetComponent<Collider>());
            rim.transform.SetParent(disc.transform, false);
            rim.transform.localScale = new Vector3(1.08f, 0.5f, 1.08f);
            rim.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
            // A generous hit sphere so a clean shot always counts
            var trigger = disc.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.75f;
            disc.AddComponent<SkeetTarget>().range = this;
            discs.Add(new Disc { t = disc.transform, velocity = velocity, spin = Random.Range(600f, 900f) });
            Play(WeaponSounds.Launch, 0.8f);
        }

        public void Hit(SkeetTarget target)
        {
            int i = discs.FindIndex(d => d.t == target.transform);
            if (i < 0) return;
            hits++;
            Burst(target.transform.position, 10, 6f);
            Play(WeaponSounds.Shatter, 0.9f);
            Destroy(target.gameObject);
            discs.RemoveAt(i);
        }

        void Burst(Vector3 at, int count, float speed)
        {
            for (int k = 0; k < count; k++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(shard.GetComponent<Collider>());
                shard.transform.SetParent(transform, true);
                shard.transform.position = at;
                shard.GetComponent<MeshRenderer>().sharedMaterial = shardMat ? shardMat : discMat;
                shards.Add(new Shard { t = shard.transform, velocity = Random.onUnitSphere * speed + Vector3.up * 2f });
            }
        }

        Material MakeMaterial(Color color, float glow)
        {
            var m = new Material(template);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * glow);
            return m;
        }

        void Play(AudioClip clip, float volume)
        {
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            audioSource.PlayOneShot(clip, volume);
        }

        void OnGUI()
        {
            bigStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            promptStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            float w = Screen.width;
            if (near)
            {
                promptStyle.normal.textColor = new Color(0.85f, 0.55f, 1f);
                GUI.Label(new Rect(0f, Screen.height * 0.62f, w, 30f), $"[E]  start skeet  ·  {discsPerRound} discs  ·  switch to the sniper (2)", promptStyle);
            }
            if (running)
            {
                bigStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(0f, 90f, w, 34f), $"SKEET   {hits} / {discsPerRound}", bigStyle);
                GUI.Label(new Rect(0f, 122f, w, 22f), $"disc {Mathf.Min(launched, discsPerRound)} of {discsPerRound}   ·   missed {misses}   ·   best {best}", smallStyle);
            }
            else if (Time.time - resultTime < 5f)
            {
                bigStyle.normal.textColor = hits >= best && hits > 0 ? new Color(0.85f, 0.55f, 1f) : Color.white;
                GUI.Label(new Rect(0f, 90f, w, 34f), $"SKEET   {hits} / {discsPerRound}" + (hits >= best && hits > 0 ? "   NEW BEST" : ""), bigStyle);
                GUI.Label(new Rect(0f, 122f, w, 22f), $"best {best}   ·   press E at the button to go again", smallStyle);
            }
        }
    }
}
