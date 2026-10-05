using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Colourways of the Void karambit: the Karambit Rubi model repainted, in our own patterns
    // (after the looks karambits are loved for: blacked out, whited out, a fade, gem-like swirls,
    // fire and ice). The blade's own texture mapping is a patchwork no pattern could follow, so
    // the blade is mapped afresh, flat across its side (it is flat: x is its thickness), u across
    // and v along it, and painted; the finger ring takes an accent colour, the handle a colour.
    public static partial class KarambitPaints
    {
        public sealed class Scheme
        {
            public Color hue;          // its swing trail and embers
            public Color ring, handle; // flat colours (the handle keeps its moulding)
            public float metal, rough, glow;
            public System.Func<float, float, Color> blade; // u across, v along (0 base, 1 point)
        }

        // The model's parts, by their textures
        const string BladeTexture = "lambert2SG", RingTexture = "lambert7SG";
        const int Size = 256;

        static readonly Dictionary<string, Scheme> schemes = new()
        {
            // All black: glossy black steel with a faint ripple in it, black all over
            ["blackout"] = new Scheme
            {
                hue = new Color(0.1f, 0.09f, 0.13f), ring = new Color(0.03f, 0.03f, 0.035f), handle = new Color(0.035f, 0.035f, 0.04f),
                metal = 0.55f, rough = 0.16f, glow = 0f,
                blade = (u, v) => Grey(0.018f + 0.035f * Ripple(u, v, 3)),
            },
            // All white: pearl, a faint pastel sheen through it, white all over
            ["whiteout"] = new Scheme
            {
                hue = new Color(0.95f, 0.96f, 1f), ring = new Color(0.93f, 0.93f, 0.95f), handle = new Color(0.86f, 0.86f, 0.89f),
                metal = 0.08f, rough = 0.2f, glow = 0.05f,
                blade = (u, v) => Color.Lerp(new Color(0.93f, 0.93f, 0.95f), Color.Lerp(new Color(1f, 0.86f, 0.95f), new Color(0.84f, 0.92f, 1f), Warp(u, v, 5)), 0.25f * Noise(u * 3f, v * 3f, 9)),
            },
            // A fade: magenta at the handle through violet to amber and gold at the point
            ["sunset"] = new Scheme
            {
                hue = new Color(1f, 0.45f, 0.45f), ring = new Color(1f, 0.78f, 0.3f), handle = new Color(0.04f, 0.035f, 0.045f),
                metal = 0.6f, rough = 0.14f, glow = 0.22f,
                blade = (u, v) => Ramp(SunsetStops, v * 0.85f + (u - 0.5f) * 0.22f + (Noise(u * 4f, v * 4f, 2) - 0.5f) * 0.08f),
            },
            // Deep blue swirled like a gem, with dark flecks
            ["sapphire"] = new Scheme
            {
                hue = new Color(0.25f, 0.55f, 1f), ring = new Color(0.75f, 0.85f, 1f), handle = new Color(0.035f, 0.04f, 0.06f),
                metal = 0.7f, rough = 0.09f, glow = 0.3f,
                blade = (u, v) => Gem(u, v, SapphireStops, 11),
            },
            // The same in emerald and lime
            ["emerald"] = new Scheme
            {
                hue = new Color(0.2f, 1f, 0.5f), ring = new Color(0.7f, 1f, 0.6f), handle = new Color(0.03f, 0.05f, 0.04f),
                metal = 0.7f, rough = 0.09f, glow = 0.3f,
                blade = (u, v) => Gem(u, v, EmeraldStops, 23),
            },
            // Marbled bands of fire and ice
            ["fireice"] = new Scheme
            {
                hue = new Color(1f, 0.45f, 0.2f), ring = new Color(0.55f, 0.8f, 1f), handle = new Color(0.04f, 0.035f, 0.04f),
                metal = 0.4f, rough = 0.12f, glow = 0.25f,
                blade = FireIce,
            },
        };

        public static bool Has(string id) => id != null && schemes.ContainsKey(id);
        public static Color Hue(string id) => schemes.TryGetValue(id, out var s) ? s.hue : Color.white;

        // Repaints a freshly built karambit (after its materials were copied for it); every
        // material made goes to `made`, to be freed with the weapon
        public static void Apply(GameObject model, string id, System.Action<Material> made)
        {
            if (!schemes.TryGetValue(id, out var s)) return;
            var bladeTex = Painted(id, s);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                var mf = r.GetComponent<MeshFilter>();
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (!src || !src.HasProperty("baseColorTexture")) continue;
                    var tex = src.GetTexture("baseColorTexture");
                    string tn = tex ? tex.name : "";
                    var m = new Material(src) { name = src.name + " (" + id + ")" };
                    made(m);
                    m.SetTexture("metallicRoughnessTexture", null); // (the colourway's own finish)
                    if (tn.Contains(BladeTexture))
                    {
                        if (mf && mf.sharedMesh) mf.sharedMesh = Flattened(mf.sharedMesh, model.transform, mf.transform);
                        m.SetTexture("baseColorTexture", bladeTex);
                        m.SetColor("baseColorFactor", Color.white);
                        m.SetTexture("normalTexture", null); // (it followed the old mapping)
                        m.SetTexture("emissiveTexture", bladeTex);
                        m.SetColor("emissiveFactor", Color.white * s.glow);
                        m.SetFloat("metallicFactor", s.metal);
                        m.SetFloat("roughnessFactor", s.rough);
                    }
                    else
                    {
                        bool ring = tn.Contains(RingTexture);
                        Color c = ring ? s.ring : s.handle;
                        // a light colour can't come from the dark handle texture: flat colour then
                        if (ring || c.grayscale > 0.3f) m.SetTexture("baseColorTexture", null);
                        m.SetColor("baseColorFactor", ring || c.grayscale > 0.3f ? c : c * 4f);
                        m.SetTexture("emissiveTexture", null);
                        m.SetColor("emissiveFactor", Color.black);
                        m.SetFloat("metallicFactor", ring ? 0.6f : 0.15f);
                        m.SetFloat("roughnessFactor", ring ? 0.2f : 0.45f);
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------------ the blade's mapping

        static readonly Dictionary<Mesh, Mesh> flattened = new();

        // A copy of the blade with its texture mapped flat across its side: u across (z), v along
        // it (y), in the model's own space
        static Mesh Flattened(Mesh src, Transform root, Transform part)
        {
            if (flattened.TryGetValue(src, out var done) && done) return done;
            if (!src.isReadable) return src;
            var v = src.vertices;
            var p = new Vector3[v.Length];
            float y0 = float.MaxValue, y1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int i = 0; i < v.Length; i++)
            {
                p[i] = root.InverseTransformPoint(part.TransformPoint(v[i]));
                y0 = Mathf.Min(y0, p[i].y); y1 = Mathf.Max(y1, p[i].y);
                z0 = Mathf.Min(z0, p[i].z); z1 = Mathf.Max(z1, p[i].z);
            }
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
                uv[i] = new Vector2(Mathf.InverseLerp(z0, z1, p[i].z), Mathf.InverseLerp(y0, y1, p[i].y));
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + " (flat)";
            mesh.uv = uv;
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            flattened[src] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ painting

        static readonly Dictionary<string, Texture2D> painted = new();

        static Texture2D Painted(string id, Scheme s)
        {
            if (painted.TryGetValue(id, out var t) && t) return t;
            t = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Karambit " + id, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color[Size * Size];
            for (int j = 0; j < Size; j++)
            for (int i = 0; i < Size; i++)
            {
                var c = s.blade((i + 0.5f) / Size, (j + 0.5f) / Size);
                c.a = 1f;
                px[j * Size + i] = c;
            }
            t.SetPixels(px);
            t.Apply();
            painted[id] = t;
            return t;
        }

        static float Noise(float x, float y, int seed) => Mathf.PerlinNoise(x + seed * 17.13f, y + seed * 7.71f);
        static Color Grey(float g) => new(g, g, g * 1.08f, 1f);

        // Noise pushed through itself: soft swirls rather than blobs
        static float Warp(float u, float v, int seed)
        {
            float a = Noise(u * 2.2f, v * 2.2f, seed), b = Noise(u * 2.2f + 5.2f, v * 2.2f + 1.3f, seed);
            return Noise(u * 3f + a * 2.4f, v * 3f + b * 2.4f, seed + 1);
        }

        // Fine folded lines, like forged steel
        static float Ripple(float u, float v, int seed) => 0.5f + 0.5f * Mathf.Sin((v * 9f + u * 3f + Noise(u * 3f, v * 3f, seed) * 5f) * Mathf.PI * 2f);

        // 0 below e0, 1 above e1, smooth between (a shader's smoothstep)
        static float Edge(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static Color Ramp(Color[] stops, float t)
        {
            t = Mathf.Clamp01(t) * (stops.Length - 1);
            int i = Mathf.Min((int)t, stops.Length - 2);
            return Color.Lerp(stops[i], stops[i + 1], Mathf.SmoothStep(0f, 1f, t - i));
        }

        static readonly Color[] SunsetStops = { new(0.85f, 0.06f, 0.48f), new(0.5f, 0.14f, 0.95f), new(1f, 0.42f, 0.12f), new(1f, 0.86f, 0.36f) };
        static readonly Color[] SapphireStops = { new(0.01f, 0.02f, 0.12f), new(0.04f, 0.17f, 0.7f), new(0.2f, 0.55f, 1f), new(0.78f, 0.95f, 1f) };
        static readonly Color[] EmeraldStops = { new(0f, 0.06f, 0.03f), new(0.02f, 0.38f, 0.16f), new(0.15f, 0.85f, 0.38f), new(0.72f, 1f, 0.58f) };
        static readonly Color[] FireStops = { new(0.45f, 0.02f, 0.02f), new(1f, 0.3f, 0.04f), new(1f, 0.86f, 0.32f) };
        static readonly Color[] IceStops = { new(0f, 0.06f, 0.38f), new(0.04f, 0.42f, 1f), new(0.55f, 0.9f, 1f) };

        // Gem-like: swirled bands of the colour, a few dark flecks
        static Color Gem(float u, float v, Color[] stops, int seed)
        {
            float w = Warp(u, v, seed);
            var c = Ramp(stops, Mathf.Pow(w, 1.25f) * 1.15f);
            float fleck = Noise(u * 11f, v * 11f, seed + 5);
            return Color.Lerp(c, stops[0], 0.75f * Edge(0.76f, 0.82f, fleck));
        }

        // Bands of fire and ice folded through each other, a dark seam where they meet
        static Color FireIce(float u, float v)
        {
            float w = Warp(u, v, 31);
            float band = Mathf.Sin((u * 1.2f + v * 1.8f + w * 2.2f) * Mathf.PI * 1.1f) * 0.5f + 0.5f;
            float heat = Edge(0.46f, 0.54f, band);
            float shade = Mathf.Clamp01(0.15f + Noise(u * 4f, v * 4f, 37) * 0.9f);
            float seam = 1f - 0.65f * Mathf.Exp(-Mathf.Pow((band - 0.5f) / 0.05f, 2f));
            return Color.Lerp(Ramp(IceStops, shade), Ramp(FireStops, shade), heat) * seam;
        }
    }
}
