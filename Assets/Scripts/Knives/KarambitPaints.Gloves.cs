using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Void gloves to match the karambits: the sport glove painted in a karambit's own design.
    // The padded panels (the quilted back of the hand and the vented finger panels) carry the
    // blade's pattern; the leather, knuckle guard, pads, palm, stitching and wrist strap take its
    // colours. Nothing glows: it's all in the paint. Painted onto the glove's chart from the
    // zones baked with it (GrayboxBuilder.Gloves), keeping the leather's grain, so the real
    // glove's relief (its normal map) still lies under every colour.
    public static partial class KarambitPaints
    {
        public sealed class GloveScheme
        {
            public Color hue;                      // its colour (as its karambit's), for cards
            public Color leather, guard, pads, palm, strap, stitch;
            public float leatherGloss = 0.45f, panelGloss = 0.6f;
            public System.Func<float, float, Color> panel; // u across the hand (0..1), v up it (0 the wrist, 1 the knuckles)
        }

        // The zones, as GrayboxBuilder.Gloves bakes them
        const int ZoneLeather = 0, ZonePanel = 1, ZoneFingerPanel = 2, ZoneGuard = 3, ZonePad = 4, ZonePalm = 5, ZoneGrip = 6, ZoneStitch = 7, ZoneAccent = 8, Zones = 9;

        static readonly Dictionary<string, GloveScheme> gloveSchemes = new()
        {
            // Karambit Rubi: hot pink gem panels on black, pink pads and stitching
            ["rubi"] = new GloveScheme
            {
                hue = new Color(1f, 0.25f, 0.72f),
                leather = Grey(0.035f), guard = Grey(0.03f), pads = new Color(0.75f, 0.06f, 0.4f), palm = Grey(0.05f),
                strap = new Color(0.6f, 0.04f, 0.32f), stitch = new Color(1f, 0.4f, 0.78f),
                panel = (u, v) => GloveGem(u * 1.3f, v * 1.1f, RubiStops, 51),
            },
            // Crimson Karambit: dark damascus with embers through it, a red guard and stitching
            ["crimson"] = new GloveScheme
            {
                hue = new Color(1f, 0.12f, 0.1f),
                leather = Grey(0.03f), guard = new Color(0.62f, 0.02f, 0.03f), pads = new Color(0.8f, 0.04f, 0.05f), palm = Grey(0.045f),
                strap = new Color(0.55f, 0.02f, 0.03f), stitch = new Color(0.95f, 0.12f, 0.1f),
                panel = Crimson,
            },
            // Blackout: black on black, the panels glossy forged steel
            ["blackout"] = new GloveScheme
            {
                hue = new Color(0.1f, 0.09f, 0.13f),
                leather = Grey(0.022f), guard = Grey(0.02f), pads = Grey(0.03f), palm = Grey(0.03f),
                strap = Grey(0.025f), stitch = Grey(0.09f), leatherGloss = 0.4f, panelGloss = 0.8f,
                panel = (u, v) => Grey(0.02f + 0.045f * Ripple(u * 0.9f, v * 0.7f, 3)),
            },
            // Whiteout: white leather, pearl panels with a pastel sheen
            ["whiteout"] = new GloveScheme
            {
                hue = new Color(0.95f, 0.96f, 1f),
                leather = new Color(0.7f, 0.7f, 0.72f), guard = new Color(0.82f, 0.82f, 0.84f), pads = new Color(0.9f, 0.9f, 0.92f), palm = new Color(0.56f, 0.56f, 0.58f),
                strap = new Color(0.8f, 0.8f, 0.82f), stitch = new Color(0.42f, 0.42f, 0.46f), leatherGloss = 0.5f, panelGloss = 0.72f,
                panel = (u, v) => Color.Lerp(new Color(0.95f, 0.95f, 0.97f), Color.Lerp(new Color(1f, 0.8f, 0.92f), new Color(0.78f, 0.88f, 1f), Warp(u, v, 5)), 0.55f * Noise(u * 2.5f, v * 2.5f, 9)),
            },
            // Sunset Fade: magenta at the wrist through violet to amber, gold on the fingers
            ["sunset"] = new GloveScheme
            {
                hue = new Color(1f, 0.45f, 0.45f),
                leather = Grey(0.035f), guard = Grey(0.03f), pads = new Color(1f, 0.7f, 0.22f), palm = Grey(0.05f),
                strap = new Color(0.62f, 0.04f, 0.36f), stitch = new Color(1f, 0.62f, 0.3f),
                panel = (u, v) => Ramp(SunsetStops, (v - 0.12f) / 0.78f + (u - 0.5f) * 0.18f + (Noise(u * 3f, v * 3f, 2) - 0.5f) * 0.1f),
            },
            // Abyss Sapphire: deep blue leather, sapphire panels, ice-blue pads and stitching
            ["sapphire"] = new GloveScheme
            {
                hue = new Color(0.25f, 0.55f, 1f),
                leather = new Color(0.02f, 0.03f, 0.075f), guard = new Color(0.02f, 0.03f, 0.07f), pads = new Color(0.62f, 0.78f, 1f), palm = new Color(0.035f, 0.045f, 0.08f),
                strap = new Color(0.03f, 0.12f, 0.42f), stitch = new Color(0.45f, 0.7f, 1f),
                panel = (u, v) => GloveGem(u * 1.3f, v * 1.1f, SapphireStops, 11),
            },
            // Emerald Venom: green-black leather, emerald panels, lime pads and stitching
            ["emerald"] = new GloveScheme
            {
                hue = new Color(0.2f, 1f, 0.5f),
                leather = new Color(0.02f, 0.042f, 0.03f), guard = new Color(0.02f, 0.04f, 0.03f), pads = new Color(0.55f, 0.95f, 0.45f), palm = new Color(0.035f, 0.055f, 0.04f),
                strap = new Color(0.02f, 0.3f, 0.13f), stitch = new Color(0.5f, 1f, 0.55f),
                panel = (u, v) => GloveGem(u * 1.3f, v * 1.1f, EmeraldStops, 23),
            },
            // Velocity (the bhop challenge's prize): speed streaks over midnight panels, cyan
            // pads and stitching, a violet strap
            ["velocity"] = new GloveScheme
            {
                hue = new Color(0.3f, 0.9f, 1f),
                leather = Grey(0.03f), guard = Grey(0.025f), pads = new Color(0.2f, 0.75f, 0.95f), palm = Grey(0.045f),
                strap = new Color(0.35f, 0.12f, 0.7f), stitch = new Color(0.3f, 0.85f, 1f),
                panel = (u, v) => Velocity(u * 1.2f, v * 0.9f),
            },
            // Fire & Ice: bands of fire and ice on the panels, ice-blue pads and strap, fire stitching
            ["fireice"] = new GloveScheme
            {
                hue = new Color(1f, 0.45f, 0.2f),
                leather = Grey(0.032f), guard = Grey(0.03f), pads = new Color(0.45f, 0.75f, 1f), palm = Grey(0.05f),
                strap = new Color(0.08f, 0.32f, 0.75f), stitch = new Color(1f, 0.5f, 0.18f),
                panel = (u, v) => FireIce(u * 0.9f, v * 0.8f),
            },
        };

        public static bool HasGlove(string id) => id != null && gloveSchemes.ContainsKey(id);
        public static Color GloveHue(string id) => gloveSchemes.TryGetValue(id, out var s) ? s.hue : Color.white;
        public static Color GloveStrap(string id) => gloveSchemes.TryGetValue(id, out var s) ? s.strap : Grey(0.05f);

        static readonly Dictionary<string, Texture2D> gloveTextures = new();
        static Color32[] zones;

        // The glove's colour (smoothness in alpha), painted once per design
        public static Texture2D GloveTexture(string id, ArmRig rig)
        {
            if (gloveTextures.TryGetValue(id, out var done) && done) return done;
            if (!gloveSchemes.TryGetValue(id, out var s) || !rig || !rig.gloveZones || !rig.gloveZones.isReadable) return null;
            int w = rig.gloveZones.width, h = rig.gloveZones.height;
            zones ??= rig.gloveZones.GetPixels32();

            // Each zone's mean brightness, so its grain can be laid over any colour
            var mean = new float[Zones];
            var count = new int[Zones];
            for (int k = 0; k < zones.Length; k++)
            {
                int z = Mathf.Min(zones[k].g / 17, Zones - 1);
                mean[z] += Bright(zones[k]);
                count[z]++;
            }
            for (int z = 0; z < Zones; z++) mean[z] = count[z] > 0 ? Mathf.Max(mean[z] / count[z], 0.002f) : 1f;

            Vector4 chart = rig.gloveChart;
            float yW = chart.x, yK = chart.y, cx = chart.w, y0 = yW - 0.03f, spanY = chart.z + 0.012f - y0;
            const float SpanX = 0.16f, HandWidth = 0.07f;
            const float PanelShade = 0.85f; // (printed leather, a shade under the bare blade's colours)
            int half = w / 2;
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                float y = y0 + (j + 0.5f) / h * spanY, v = (y - yW) / (yK - yW);
                for (int i = 0; i < w; i++)
                {
                    int k = j * w + i;
                    var zp = zones[k];
                    int z = Mathf.Min(zp.g / 17, Zones - 1);
                    float grain = Mathf.Clamp(Bright(zp) / mean[z], 0.55f, 1.5f);
                    float gloss = zp.a / 255f;
                    Color c;
                    switch (z)
                    {
                        case ZonePanel:
                        case ZoneFingerPanel:
                        {
                            float x = ((i % half + 0.5f) / half - 0.5f) * SpanX;
                            c = s.panel((x - cx) / HandWidth + 0.5f, v) * (PanelShade * Mathf.Lerp(1f, grain, 0.45f));
                            gloss = Mathf.Lerp(s.panelGloss, gloss, 0.25f);
                            break;
                        }
                        case ZoneGuard: c = s.guard * grain; gloss = 0.32f; break;
                        case ZonePad: c = s.pads * Mathf.Lerp(1f, grain, 0.5f); gloss = 0.45f; break;
                        case ZonePalm: c = s.palm * grain; break;
                        case ZoneGrip: c = s.palm * 1.4f; break;
                        case ZoneStitch: c = s.stitch; break;
                        case ZoneAccent: c = s.stitch * 0.8f; gloss = 0.7f; break;
                        default: c = s.leather * grain; gloss *= s.leatherGloss / 0.5f; break;
                    }
                    c.a = Mathf.Clamp01(gloss);
                    px[k] = c;
                }
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Void Gloves " + id, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, hideFlags = HideFlags.DontUnloadUnusedAsset };
            t.SetPixels32(px);
            t.Apply(true, true); // (its copy in memory freed: only the GPU's is needed)
            return gloveTextures[id] = t;
        }

        // A zone texel's brightness (stored square-rooted, as baked)
        static float Bright(Color32 z)
        {
            float r = z.r / 255f;
            return r * r * 0.6f;
        }

        static readonly Color[] RubiStops = { new(0.14f, 0f, 0.07f), new(0.62f, 0.02f, 0.34f), new(1f, 0.2f, 0.62f), new(1f, 0.74f, 0.9f) };

        // The gem swirl on leather: the blade's, kept to its deeper colours (its palest stop, at
        // full strength over a whole panel, reads as light rather than paint)
        static Color GloveGem(float u, float v, Color[] stops, int seed)
        {
            float w = Warp(u, v, seed);
            var c = Ramp(stops, Mathf.Pow(w, 1.5f) * 1.05f);
            float fleck = Noise(u * 11f, v * 11f, seed + 5);
            return Color.Lerp(c, stops[0], 0.75f * Edge(0.76f, 0.82f, fleck));
        }

        // Dark damascus steel with embers caught in it (the Crimson Karambit's blade)
        static Color Crimson(float u, float v)
        {
            float r = Ripple(u * 0.8f, v * 0.6f, 41);
            var c = Color.Lerp(new Color(0.025f, 0.03f, 0.045f), new Color(0.13f, 0.14f, 0.19f), r * r);
            float ember = Edge(0.7f, 0.78f, Noise(u * 13f, v * 13f, 43));
            return Color.Lerp(c, Color.Lerp(new Color(0.9f, 0.06f, 0.04f), new Color(1f, 0.55f, 0.15f), Noise(u * 5f, v * 5f, 47)), ember);
        }
    }
}
