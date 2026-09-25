using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // A weapon case in the start hall: an open briefcase with striped sides, light shooting
    // up out of it and its star weapon floating above. Walk up and press E to open it: a
    // strip of possible drops spins past and slows to a stop on what you got (Void 6% of the
    // time, otherwise Mythic), which is then equipped.
    //
    // The model is built when the scene loads (and in the editor), not saved into the scene.
    [ExecuteAlways]
    public class CaseStation : MonoBehaviour
    {
        public string title = "KNIFE CASE";
        public bool sniperCase;
        [Tooltip("Which skin floats above the case")]
        public int showcaseSkin = 1;
        public Color stripeA = new(0.08f, 0.06f, 0.12f), stripeB = new(0.55f, 0.25f, 1f), rayColor = new(1f, 0.75f, 0.25f);
        [Tooltip("Any URP Lit material; the case's materials are made from it")]
        public Material template;
        public float useRange = 2.8f;

        Transform model, showcaseSpin;
        WeaponParts showcase;
        readonly List<(Transform t, float height, float phase)> rays = new();
        readonly List<Material> materials = new();
        Texture2D stripes;

        static readonly Vector3 ShowcaseSpot = new(0f, 1.75f, -0.25f); // above the case, in front of the lid

        Skins.Skin[] Pool => sniperCase ? Skins.Snipers : Skins.Knives;

        void OnEnable() => Build();

        void OnDisable()
        {
            Clear();
            if (opening) ViewModel.InputBlocked = false;
        }

        // ------------------------------------------------------------------ model

        void Build()
        {
            Clear();
            if (!template) return;
            var b = new WeaponBuilder(template, gameObject.layer, true, materials);
            model = new GameObject("Case Model").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * 1.35f;

            stripes = StripeTexture(stripeA, stripeB);
            Material shell = b.Mat(Color.white, 0.55f, 0.3f);
            shell.SetTexture("_BaseMap", stripes);
            Material trim = b.Mat(new Color(0.75f, 0.77f, 0.82f), 0.85f, 0.9f);
            Material foam = b.Mat(new Color(0.05f, 0.05f, 0.07f), 0.1f, 0f);
            Material glow = b.Glow(rayColor, 2.2f, null);
            Material ray = b.Glow(rayColor, 2f, null);

            const float w = 1.6f, d = 1f, h = 0.42f, wall = 0.06f;
            var t = model;
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(0f, 0.025f, 0f), new Vector3(w, 0.05f, d));
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(0f, h / 2f, -d / 2f + wall / 2f), new Vector3(w, h, wall));
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(0f, h / 2f, d / 2f - wall / 2f), new Vector3(w, h, wall));
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(-w / 2f + wall / 2f, h / 2f, 0f), new Vector3(wall, h, d));
            b.Part(t, PrimitiveType.Cube, shell, new Vector3(w / 2f - wall / 2f, h / 2f, 0f), new Vector3(wall, h, d));
            // Metal rim around the top and corners
            b.Part(t, PrimitiveType.Cube, trim, new Vector3(0f, h, -d / 2f + wall / 2f), new Vector3(w + 0.02f, 0.03f, wall + 0.02f));
            b.Part(t, PrimitiveType.Cube, trim, new Vector3(0f, h, d / 2f - wall / 2f), new Vector3(w + 0.02f, 0.03f, wall + 0.02f));
            b.Part(t, PrimitiveType.Cube, trim, new Vector3(-w / 2f + wall / 2f, h, 0f), new Vector3(wall + 0.02f, 0.03f, d + 0.02f));
            b.Part(t, PrimitiveType.Cube, trim, new Vector3(w / 2f - wall / 2f, h, 0f), new Vector3(wall + 0.02f, 0.03f, d + 0.02f));
            foreach (float x in new[] { -w / 2f, w / 2f })
            foreach (float z in new[] { -d / 2f, d / 2f })
                b.Part(t, PrimitiveType.Cube, trim, new Vector3(x, h / 2f, z), new Vector3(0.07f, h + 0.02f, 0.07f));
            // Handle and latches on the front
            b.Rod(t, trim, new Vector3(-0.22f, h * 0.55f, -d / 2f - 0.07f), new Vector3(0.22f, h * 0.55f, -d / 2f - 0.07f), 0.035f);
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                b.Part(t, PrimitiveType.Cube, trim, new Vector3(x, h * 0.55f, -d / 2f - 0.035f), new Vector3(0.04f, 0.04f, 0.07f));
                b.Part(t, PrimitiveType.Cube, trim, new Vector3(x * 2.4f, h * 0.8f, -d / 2f - 0.01f), new Vector3(0.1f, 0.07f, 0.03f));
            }
            // Glowing inside
            b.Part(t, PrimitiveType.Cube, foam, new Vector3(0f, 0.06f, 0f), new Vector3(w - 2f * wall, 0.03f, d - 2f * wall));
            b.Part(t, PrimitiveType.Cube, glow, new Vector3(0f, 0.08f, 0f), new Vector3(w - 0.3f, 0.01f, d - 0.3f));

            // Lid, hinged at the back and flipped open
            var lid = new GameObject("Lid").transform;
            lid.SetParent(t, false);
            lid.SetLocalPositionAndRotation(new Vector3(0f, h, d / 2f), Quaternion.Euler(105f, 0f, 0f));
            b.Part(lid, PrimitiveType.Cube, shell, new Vector3(0f, 0.06f, -d / 2f), new Vector3(w, 0.12f, d));
            b.Part(lid, PrimitiveType.Cube, foam, new Vector3(0f, -0.005f, -d / 2f), new Vector3(w - 0.1f, 0.02f, d - 0.1f));
            b.Part(lid, PrimitiveType.Cube, glow, new Vector3(0f, -0.018f, -d / 2f), new Vector3(w - 0.5f, 0.01f, 0.05f));
            b.Part(lid, PrimitiveType.Cube, trim, new Vector3(0f, 0.12f, -d / 2f), new Vector3(w + 0.02f, 0.02f, d + 0.02f));

            // Rays of light shooting up out of the case
            var random = new System.Random(title.GetHashCode());
            for (int i = 0; i < 16; i++)
            {
                float height = 0.5f + (float)random.NextDouble() * 2.2f;
                var p = new Vector3(((float)random.NextDouble() - 0.5f) * (w - 0.3f), 0.08f, ((float)random.NextDouble() - 0.5f) * (d - 0.3f));
                var r = b.Part(t, PrimitiveType.Cube, ray, p, new Vector3(0.022f, height, 0.022f));
                rays.Add((r, height, (float)random.NextDouble() * 10f));
            }

            // The star weapon, floating above at a jaunty angle
            showcaseSpin = new GameObject("Showcase").transform;
            showcaseSpin.SetParent(t, false);
            showcaseSpin.localPosition = ShowcaseSpot;
            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(showcaseSpin, false);
            tilt.localRotation = Quaternion.Euler(0f, 0f, sniperCase ? 15f : -35f);
            tilt.localScale = Vector3.one * (sniperCase ? 1.5f : 6.5f);
            var offset = new GameObject("Offset").transform;
            offset.SetParent(tilt, false);
            offset.localPosition = sniperCase ? new Vector3(0f, -0.02f, -0.25f) : new Vector3(0f, -0.06f, 0f);
            int skin = Mathf.Clamp(showcaseSkin, 0, Pool.Length - 1);
            showcase = sniperCase ? b.Rifle(Pool[skin], offset) : b.Knife(Pool[skin], offset);

            foreach (var tr in model.GetComponentsInChildren<Transform>(true)) tr.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        void Clear()
        {
            if (model) WeaponBuilder.Kill(model.gameObject);
            model = null;
            showcase = null;
            rays.Clear();
            foreach (var m in materials) WeaponBuilder.Kill(m);
            materials.Clear();
            WeaponBuilder.Kill(stripes);
        }

        // Bold diagonal bands with thin bright pinstripes between them
        static Texture2D StripeTexture(Color a, Color b)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float s = Mathf.Repeat((x + y) / (float)size * 3f, 1f);
                Color c = s < 0.45f ? a : b;
                if (Mathf.Abs(s - 0.45f) < 0.03f || s > 0.97f) c = Color.white;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ opening

        PlayerMovement player;
        ViewModel viewModel;
        AudioSource audioSource;
        GUIStyle promptStyle, tileTop, tileName, bigStyle, smallStyle;
        bool near, opening, revealed;
        float openTime, scrollTarget;
        int lastTick, winner;
        readonly List<int> strip = new();

        const int StripLength = 50, WinnerSlot = 42;
        const float TileWidth = 170f, TileHeight = 120f, Gap = 8f, SpinTime = 5.5f;

        void Update()
        {
            float time = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;
            if (showcaseSpin) showcaseSpin.localRotation = Quaternion.Euler(0f, time * 35f, 0f);
            if (showcaseSpin) showcaseSpin.localPosition = ShowcaseSpot + Vector3.up * (Mathf.Sin(time * 1.3f) * 0.06f);
            showcase?.Animate(time, -1f);
            foreach (var (t, height, phase) in rays)
            {
                if (!t) continue;
                float k = 0.75f + 0.25f * Mathf.Sin(time * 2.2f + phase);
                t.localScale = new Vector3(0.022f, height * k, 0.022f);
                t.localPosition = new Vector3(t.localPosition.x, 0.08f + height * k * 0.5f, t.localPosition.z);
            }
            if (!Application.isPlaying) return;

            if (!player) player = FindAnyObjectByType<PlayerMovement>();
            var kb = Keyboard.current;
            bool ePressed = kb != null && kb.eKey.wasPressedThisFrame;
            if (opening)
            {
                float s = (Time.time - openTime) / SpinTime;
                if (!revealed)
                {
                    int tick = Mathf.FloorToInt(Scroll(s) / (TileWidth + Gap));
                    if (tick != lastTick) { lastTick = tick; Play(WeaponSounds.Tick, 0.5f); }
                    if (s >= 1f) Reveal();
                }
                else if (ePressed || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                {
                    opening = false;
                    ViewModel.InputBlocked = false;
                }
                return;
            }
            if (!player) return;
            Vector3 d = player.Position - transform.position;
            d.y = 0f;
            near = d.magnitude < useRange && !ViewModel.InputBlocked;
            if (near && ePressed) Open();
        }

        void Open()
        {
            // Fill the strip with random drops, then put the real one where it will stop
            strip.Clear();
            for (int i = 0; i < StripLength; i++) strip.Add(Skins.Roll(Pool));
            winner = Skins.Roll(Pool);
            strip[WinnerSlot] = winner;
            scrollTarget = WinnerSlot * (TileWidth + Gap) + TileWidth * Random.Range(0.1f, 0.9f);
            openTime = Time.time;
            lastTick = 0;
            opening = true;
            revealed = false;
            ViewModel.InputBlocked = true;
        }

        // How far the strip has moved at s (0..1 of the spin): fast, then a long slow-down
        float Scroll(float s) => scrollTarget * (1f - Mathf.Pow(1f - Mathf.Clamp01(s), 4f));

        void Reveal()
        {
            revealed = true;
            if (!viewModel) viewModel = FindAnyObjectByType<ViewModel>();
            if (viewModel)
            {
                if (sniperCase) viewModel.EquipSniperSkin(winner);
                else viewModel.EquipKnifeSkin(winner);
            }
            Play(Pool[winner].rarity == SkinRarity.Void ? WeaponSounds.VoidReveal : WeaponSounds.Reveal, 0.8f);
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
            if (!Application.isPlaying) return;
            promptStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (!opening)
            {
                if (!near) return;
                promptStyle.normal.textColor = rayColor;
                GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f), $"[E]  open  {title}", promptStyle);
                return;
            }

            tileTop ??= new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(0.7f, 0.72f, 0.78f) } };
            tileName ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true, normal = { textColor = Color.white } };
            bigStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.85f, 0.86f, 0.9f) } };

            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            promptStyle.normal.textColor = rayColor;
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, Screen.height * 0.5f - TileHeight / 2f - 60f, Screen.width, 30f), title, promptStyle);

            // The strip, clipped to a window with a marker down the middle
            float stripWidth = Mathf.Min(Screen.width * 0.92f, 1150f);
            var window = new Rect((Screen.width - stripWidth) / 2f, Screen.height * 0.5f - TileHeight / 2f, stripWidth, TileHeight);
            GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.9f);
            GUI.DrawTexture(new Rect(window.x - 6f, window.y - 6f, window.width + 12f, window.height + 12f), Texture2D.whiteTexture);
            float scroll = Scroll((Time.time - openTime) / SpinTime) - stripWidth / 2f;
            GUI.BeginGroup(window);
            for (int i = 0; i < strip.Count; i++)
            {
                float x = i * (TileWidth + Gap) - scroll;
                if (x > stripWidth || x + TileWidth < 0f) continue;
                DrawTile(new Rect(x, 0f, TileWidth, TileHeight), Pool[strip[i]]);
            }
            GUI.EndGroup();
            GUI.color = rayColor;
            GUI.DrawTexture(new Rect(Screen.width / 2f - 1.5f, window.y - 12f, 3f, window.height + 24f), Texture2D.whiteTexture);

            if (revealed)
            {
                var skin = Pool[winner];
                float since = Time.time - openTime - SpinTime;
                if (skin.rarity == SkinRarity.Void && since < 1.2f)
                {
                    GUI.color = new Color(0.6f, 0.25f, 1f, 0.5f * (1f - since / 1.2f));
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
                bigStyle.normal.textColor = Skins.RarityColor(skin.rarity);
                float y = window.yMax + 30f;
                GUI.Label(new Rect(0f, y, Screen.width, 40f), $"★ {skin.name}", bigStyle);
                GUI.Label(new Rect(0f, y + 42f, Screen.width, 24f), $"{Skins.RarityName(skin.rarity).ToUpper()}   ·   equipped", smallStyle);
                GUI.Label(new Rect(0f, y + 70f, Screen.width, 24f), "[E] or click to close", smallStyle);
            }
            GUI.color = old;
        }

        void DrawTile(Rect r, Skins.Skin skin)
        {
            Color rarity = Skins.RarityColor(skin.rarity);
            GUI.color = skin.rarity == SkinRarity.Void ? new Color(0.16f, 0.06f, 0.28f, 1f) : new Color(0.1f, 0.1f, 0.13f, 1f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = rarity * new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height * 0.55f, r.width, r.height * 0.45f), Texture2D.whiteTexture);
            GUI.color = rarity;
            GUI.DrawTexture(new Rect(r.x, r.yMax - 6f, r.width, 6f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string kind = skin.model switch
            {
                KnifeModel.Talon => "TALON KNIFE",
                KnifeModel.Butterfly => "BUTTERFLY KNIFE",
                KnifeModel.Rifle => "LONGREACH SNIPER",
                _ => "VOID BLADE",
            };
            GUI.Label(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, 18f), $"★ {kind}", tileTop);
            int bar = skin.name.IndexOf('|');
            string finish = bar >= 0 ? skin.name.Substring(bar + 1).Trim() : skin.name;
            GUI.Label(new Rect(r.x + 10f, r.y + 28f, r.width - 20f, 60f), finish, tileName);
            tileTop.normal.textColor = rarity;
            GUI.Label(new Rect(r.x + 10f, r.yMax - 28f, r.width - 20f, 18f), Skins.RarityName(skin.rarity).ToUpper(), tileTop);
            tileTop.normal.textColor = new Color(0.7f, 0.72f, 0.78f);
        }
    }
}
