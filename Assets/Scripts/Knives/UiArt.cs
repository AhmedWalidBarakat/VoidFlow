using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // The look shared by every item card (case strip, inventory, rewards): the item's rendered
    // picture on a card glowing in its rarity color, with rays turning slowly behind it, a
    // soft halo, twinkles, and a shine that sweeps across now and then. The Void Case has its
    // own card: drifting violet, magenta and amber light with embers rising through it.
    // All the art here is generated in code; nothing is imported.
    public static class UiArt
    {
        public static readonly Color CaseViolet = new(0.58f, 0.18f, 1f), CasePink = new(1f, 0.28f, 0.66f), CaseAmber = new(1f, 0.62f, 0.18f);

        static Texture2D glow, dot, fade, shine, arrow;
        static Texture2D[] rays;
        const int RayFrames = 8, RayCount = 14;
        static readonly Dictionary<(int, bool, TextAnchor), GUIStyle> styles = new();

        // ------------------------------------------------------------------ textures

        public static Texture2D Glow => glow ? glow : glow = Radial(64, 2.2f);
        public static Texture2D Dot => dot ? dot : dot = Radial(32, 0.7f);
        public static Texture Star => FxLibrary.Instance && FxLibrary.Instance.star ? FxLibrary.Instance.star.GetTexture("_BaseMap") : Dot;

        // White, opaque at the bottom and clear at the top
        public static Texture2D Fade
        {
            get
            {
                if (fade) return fade;
                fade = NewTexture(1, 64);
                for (int y = 0; y < 64; y++) fade.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - y / 63f, 1.4f)));
                fade.Apply();
                return fade;
            }
        }

        // A soft diagonal band, for shine sweeping across a card
        public static Texture2D Shine
        {
            get
            {
                if (shine) return shine;
                shine = NewTexture(64, 64);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = (x - y) / (63f * 1.414f);
                    shine.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Exp(-d * d / 0.012f)));
                }
                shine.Apply();
                return shine;
            }
        }

        // A soft chevron pointing right, for off-screen markers
        public static Texture2D Arrow
        {
            get
            {
                if (arrow) return arrow;
                arrow = NewTexture(64, 64);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float u = x / 63f, v = Mathf.Abs(y / 63f - 0.5f) * 2f;
                    // distance to a ">" shape made of two strokes
                    float edge = Mathf.Abs(u - (0.85f - v * 0.55f));
                    float a = Mathf.Clamp01(1f - edge / 0.12f) * Mathf.Clamp01((1f - v) * 6f);
                    arrow.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                arrow.Apply();
                return arrow;
            }
        }

        static Texture2D[] Rays
        {
            get
            {
                if (rays != null && rays[0]) return rays;
                rays = new Texture2D[RayFrames];
                const int size = 128;
                float half = (size - 1) * 0.5f;
                for (int f = 0; f < RayFrames; f++)
                {
                    var tex = NewTexture(size, size);
                    float offset = f / (float)RayFrames * Mathf.PI * 2f / RayCount;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - half) / half, dy = (y - half) / half;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Atan2(dy, dx) + offset;
                        float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(a * RayCount), 5f);
                        float falloff = Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f) * Mathf.SmoothStep(0f, 1f, r / 0.12f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, ray * falloff));
                    }
                    tex.Apply();
                    rays[f] = tex;
                }
                return rays;
            }
        }

        static Texture2D Radial(int size, float power)
        {
            var tex = NewTexture(size, size);
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - d, power)));
            }
            tex.Apply();
            return tex;
        }

        static Texture2D NewTexture(int w, int h) =>
            new(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };

        // ------------------------------------------------------------------ drawing helpers

        public static void Rounded(Rect r, Color c, float radius, float border = 0f) =>
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, border, radius);

        public static void Blob(Vector2 center, float size, Color c, float alpha)
        {
            GUI.color = new Color(c.r, c.g, c.b, alpha);
            GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), Glow);
        }

        // Turning rays: crossfades between pre-turned frames, so no GUI rotation is needed
        public static void DrawRays(Rect r, float turns, Color c)
        {
            var frames = Rays;
            float p = Mathf.Repeat(turns, 1f) * RayFrames;
            int a = (int)p % RayFrames, b = (a + 1) % RayFrames;
            float k = p - Mathf.Floor(p);
            GUI.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
            GUI.DrawTexture(r, frames[a]);
            GUI.color = new Color(c.r, c.g, c.b, c.a * k);
            GUI.DrawTexture(r, frames[b]);
        }

        public static GUIStyle Style(int size, bool bold = true, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var key = (size, bold, anchor);
            if (styles.TryGetValue(key, out var s)) return s;
            s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, alignment = anchor,
                clipping = TextClipping.Clip, wordWrap = false, padding = new RectOffset(0, 0, 0, 0),
            };
            styles[key] = s;
            return s;
        }

        // Text with a soft drop shadow so it reads on anything
        public static void Text(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = true)
        {
            var style = Style(size, bold, anchor);
            GUI.color = Color.white;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.65f * color.a);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 2f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        // A rounded pill with centered text
        public static void Pill(Rect r, string text, int size, Color fill, Color textColor)
        {
            Rounded(r, fill, r.height / 2f);
            Text(r, text, size, textColor, TextAnchor.MiddleCenter);
        }

        public static Rect Grow(Rect r, float scale)
        {
            float w = r.width * scale, h = r.height * scale;
            return new Rect(r.center.x - w / 2f, r.center.y - h / 2f, w, h);
        }

        public static float Hash(int n)
        {
            float s = Mathf.Sin(n * 12.9898f + 78.233f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        public static float ElasticOut(float x) =>
            x >= 1f ? 1f : x <= 0f ? 0f : Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;

        public static float BackOut(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        static Color Lighter(Color c, float k) => new(Mathf.Lerp(c.r, 1f, k), Mathf.Lerp(c.g, 1f, k), Mathf.Lerp(c.b, 1f, k), c.a);

        public static string FinishName(Skins.Skin skin)
        {
            int bar = skin.name.IndexOf('|');
            return bar >= 0 ? skin.name.Substring(bar + 1).Trim() : skin.name;
        }

        // ------------------------------------------------------------------ cards

        // One item: picture, rays, halo, twinkles, shine, and its names. `hover` (0..1) lifts
        // and brightens it; `top` replaces the kind label in the corner.
        public static void Card(Rect r, Skins.Skin skin, Texture icon, float alpha, float hover, float time, int seed, string top = null)
        {
            if (alpha <= 0.01f || r.width < 8f) return;
            var old = GUI.color;
            Color rar = Skins.RarityColor(skin.rarity);
            bool isVoid = skin.rarity == SkinRarity.Void, fancy = skin.rarity != SkinRarity.Default;
            float radius = Mathf.Min(14f, r.height * 0.09f);

            // Deep base tinted by the rarity, its color rising from the bottom
            Rounded(r, new Color(0.035f + rar.r * 0.09f, 0.03f + rar.g * 0.06f, 0.06f + rar.b * 0.11f, 0.97f * alpha), radius);
            GUI.DrawTexture(r, Fade, ScaleMode.StretchToFill, true, 0f, new Color(rar.r, rar.g, rar.b, (0.4f + 0.2f * hover) * alpha), 0f, radius);

            GUI.BeginGroup(r);
            float w = r.width, h = r.height, s = Mathf.Min(w, h);
            var c = new Vector2(w * 0.5f, h * 0.43f);
            if (fancy)
            {
                float big = s * (isVoid ? 1.7f : 1.35f);
                float breathe = 0.8f + 0.2f * Mathf.Sin(time * 2f + seed);
                DrawRays(new Rect(c.x - big / 2f, c.y - big / 2f, big, big), time * (isVoid ? 0.3f : 0.12f) + Hash(seed),
                    new Color(Lighter(rar, 0.35f).r, Lighter(rar, 0.35f).g, Lighter(rar, 0.35f).b, (isVoid ? 0.55f : 0.32f) * breathe * alpha));
            }
            float hs = s * (1f + 0.14f * hover + 0.04f * Mathf.Sin(time * 2.4f + seed));
            GUI.color = new Color(rar.r, rar.g, rar.b, (fancy ? 0.65f : 0.35f) * alpha);
            GUI.DrawTexture(new Rect(c.x - hs * 0.85f, c.y - hs * 0.5f, hs * 1.7f, hs), Glow);

            int twinkles = isVoid ? 7 : fancy ? 3 : 0;
            for (int k = 0; k < twinkles; k++)
            {
                float hx = Hash(seed * 7 + k * 13), hy = Hash(seed * 11 + k * 5 + 3), ph = Hash(seed + k * 17) * 6.28f;
                float tw = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * (1.5f + hx) + ph)), 4f);
                float size = (7f + 9f * hy) * tw * Mathf.Clamp(h / 160f, 0.6f, 1.6f);
                if (size < 0.5f) continue;
                GUI.color = new Color(Lighter(rar, 0.6f).r, Lighter(rar, 0.6f).g, Lighter(rar, 0.6f).b, tw * alpha);
                GUI.DrawTexture(new Rect(w * (0.08f + 0.84f * hx) - size, h * (0.08f + 0.5f * hy) - size, size * 2f, size * 2f), Star);
            }

            // The item, lifting on hover, with a soft shadow under it
            if (icon)
            {
                float lift = hover * 5f + Mathf.Sin(time * 1.7f + seed) * 1.5f;
                var ir = Grow(new Rect(w * 0.05f, h * 0.1f, w * 0.9f, h * 0.62f), 1f + 0.08f * hover);
                ir.y -= lift;
                GUI.color = new Color(0f, 0f, 0f, 0.45f * alpha);
                GUI.DrawTexture(new Rect(ir.x + 3f, ir.y + 7f + lift * 0.6f, ir.width, ir.height), icon, ScaleMode.ScaleToFit);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(ir, icon, ScaleMode.ScaleToFit);
            }

            // A shine sweeps across every few seconds, faster while hovered
            float u = Mathf.Repeat(time * (hover > 0.5f ? 0.8f : 0.28f) + Hash(seed + 99), 1f);
            if (u < 0.4f)
            {
                float bw = h * 1.1f, x = Mathf.Lerp(-bw, w, u / 0.4f);
                GUI.color = new Color(1f, 1f, 1f, (0.14f + 0.16f * hover) * alpha);
                GUI.DrawTexture(new Rect(x, 0f, bw, h), Shine);
            }

            // Names over a dark fade at the bottom
            GUI.color = new Color(0f, 0f, 0f, 0.65f * alpha);
            GUI.DrawTexture(new Rect(0f, h * 0.6f, w, h * 0.4f), Fade);
            float fs = Mathf.Clamp(h / 160f, 0.75f, 1.5f);
            string kind = top ?? (fancy ? "★ " : "") + Skins.KindName(skin);
            Text(new Rect(9f, 6f, w - 18f, 16f * fs), kind, Mathf.RoundToInt(11f * fs), new Color(1f, 1f, 1f, 0.85f * alpha));
            Text(new Rect(9f, h - 40f * fs, w - 18f, 21f * fs), FinishName(skin), Mathf.RoundToInt(16f * fs), new Color(1f, 1f, 1f, alpha));
            Text(new Rect(9f, h - 20f * fs, w - 18f, 15f * fs), Skins.RarityName(skin.rarity).ToUpper() + (skin.credit != null ? "  model by " + skin.credit : ""), Mathf.RoundToInt(11f * fs), new Color(rar.r, rar.g, rar.b, alpha));
            GUI.EndGroup();

            Rounded(r, new Color(rar.r, rar.g, rar.b, (0.45f + 0.5f * hover) * alpha), radius, 1.5f + 1.5f * hover);
            GUI.color = old;
        }

        // The Void Case: its own rich card with drifting colored light and rising embers
        public static void CaseCard(Rect r, Texture icon, int count, float alpha, float hover, float time, bool openButton)
        {
            if (alpha <= 0.01f || r.width < 8f) return;
            var old = GUI.color;
            float radius = Mathf.Min(16f, r.height * 0.09f);
            Rounded(r, new Color(0.07f, 0.02f, 0.13f, 0.98f * alpha), radius);

            GUI.BeginGroup(r);
            float w = r.width, h = r.height, s = Mathf.Min(w, h);
            Blob(new Vector2(w * (0.28f + 0.14f * Mathf.Sin(time * 0.7f)), h * (0.3f + 0.1f * Mathf.Cos(time * 0.9f))), w * 1.15f, CaseViolet, 0.85f * alpha);
            Blob(new Vector2(w * (0.78f + 0.12f * Mathf.Cos(time * 0.6f)), h * (0.62f + 0.1f * Mathf.Sin(time * 0.8f))), w * 1.05f, CasePink, 0.7f * alpha);
            Blob(new Vector2(w * (0.5f + 0.25f * Mathf.Sin(time * 0.45f + 2f)), h * 1.02f), w * 0.9f, CaseAmber, 0.55f * alpha);
            var c = new Vector2(w * 0.5f, h * 0.42f);
            float big = s * 1.8f;
            DrawRays(new Rect(c.x - big / 2f, c.y - big / 2f, big, big), time * 0.25f, new Color(1f, 0.75f, 0.95f, 0.4f * alpha));

            // Embers drifting up
            for (int k = 0; k < 16; k++)
            {
                float speed = 0.12f + 0.12f * Hash(k * 3 + 1);
                float u = Mathf.Repeat(time * speed + Hash(k * 7), 1f);
                float x = w * Hash(k * 11 + 5) + Mathf.Sin(time * 1.3f + k) * 7f;
                float y = h * (1.05f - u * 1.1f);
                float size = (2.5f + 4f * Hash(k * 5)) * Mathf.Clamp(h / 180f, 0.6f, 1.6f);
                GUI.color = new Color(1f, Mathf.Lerp(0.45f, 0.8f, Hash(k)), Mathf.Lerp(0.3f, 0.7f, Hash(k + 3)), Mathf.Sin(u * Mathf.PI) * alpha);
                GUI.DrawTexture(new Rect(x - size, y - size, size * 2f, size * 2f), Dot);
            }

            float hs = s * (1.05f + 0.15f * hover + 0.05f * Mathf.Sin(time * 2.2f));
            GUI.color = new Color(1f, 0.55f, 0.95f, 0.55f * alpha);
            GUI.DrawTexture(new Rect(c.x - hs * 0.8f, c.y - hs * 0.5f, hs * 1.6f, hs), Glow);
            if (icon)
            {
                float lift = hover * 6f + Mathf.Sin(time * 1.6f) * 3f;
                var ir = Grow(new Rect(w * 0.1f, h * 0.06f, w * 0.8f, h * 0.62f), 1f + 0.08f * hover);
                ir.y -= lift;
                GUI.color = new Color(0f, 0f, 0f, 0.45f * alpha);
                GUI.DrawTexture(new Rect(ir.x + 3f, ir.y + 8f + lift * 0.6f, ir.width, ir.height), icon, ScaleMode.ScaleToFit);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(ir, icon, ScaleMode.ScaleToFit);
            }
            float u2 = Mathf.Repeat(time * (hover > 0.5f ? 0.8f : 0.3f), 1f);
            if (u2 < 0.4f)
            {
                float bw = h * 1.1f, x = Mathf.Lerp(-bw, w, u2 / 0.4f);
                GUI.color = new Color(1f, 1f, 1f, (0.16f + 0.16f * hover) * alpha);
                GUI.DrawTexture(new Rect(x, 0f, bw, h), Shine);
            }

            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(new Rect(0f, h * 0.55f, w, h * 0.45f), Fade);
            float fs = Mathf.Clamp(h / 180f, 0.75f, 1.6f);
            Text(new Rect(10f, 7f, w - 20f, 16f * fs), "★ CASE", Mathf.RoundToInt(11f * fs), new Color(1f, 0.85f, 1f, 0.9f * alpha));
            float titleY = openButton ? h - 70f * fs : h - 44f * fs;
            Text(new Rect(10f, titleY, w - 20f, 24f * fs), "VOID CASE", Mathf.RoundToInt(19f * fs), new Color(1f, 1f, 1f, alpha));
            Text(new Rect(10f, titleY + 22f * fs, w - 20f, 16f * fs), "knives · snipers · gloves", Mathf.RoundToInt(11f * fs), new Color(1f, 0.7f, 0.95f, alpha), TextAnchor.MiddleLeft, false);
            if (count > 1) Pill(new Rect(w - 52f * fs, 8f, 44f * fs, 22f * fs), $"×{count}", Mathf.RoundToInt(13f * fs), new Color(0f, 0f, 0f, 0.55f * alpha), new Color(1f, 1f, 1f, alpha));
            if (openButton)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f);
                var b = new Rect(10f, h - 30f * fs, w - 20f, 22f * fs);
                Rounded(Grow(b, 1f + 0.03f * hover), Color.Lerp(CasePink, CaseAmber, pulse * 0.4f + hover * 0.3f) * new Color(1f, 1f, 1f, alpha), 11f * fs);
                Text(b, "OPEN", Mathf.RoundToInt(13f * fs), new Color(1f, 1f, 1f, alpha), TextAnchor.MiddleCenter);
            }
            GUI.EndGroup();

            Color edge = Color.Lerp(CaseViolet, CasePink, 0.5f + 0.5f * Mathf.Sin(time * 1.5f));
            Rounded(r, new Color(edge.r, edge.g, edge.b, (0.7f + 0.3f * hover) * alpha), radius, 2f + 1.5f * hover);
            GUI.color = old;
        }
    }
}
