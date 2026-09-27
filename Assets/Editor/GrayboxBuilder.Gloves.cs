using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // The glove and jacket textures, baked from CC0 leather and fabric scans (ambientCG, see
    // Assets/Editor/CC0Textures) into a sport-glove design in the style of the classic
    // shooters' gloves (original, not copied): a quilted padded panel over the back of the
    // hand edged in stitching with a red accent, a rubber knuckle guard with a raised pad per
    // finger, padded vented panels on the fingers, a perforated palm and grippy dotted
    // fingertips, all on smooth black leather. Colour (smoothness in alpha) and normal maps.
    //
    // The glove's UVs are a side-on chart (GloveChart): the left half of the texture is the
    // back of the hand, the right half the palm, x across and y up the hand in metres of arm
    // space, so the design lands on the actual knuckles and fingers.
    public static partial class GrayboxBuilder
    {
        public struct GloveChart
        {
            public float wristY, knuckleY, tipY;
            public float[] fingerX; // index, middle, ring, pinky knuckles (right hand)
            public const float SpanX = 0.16f;
            public float Y0 => wristY - 0.03f;
            public float SpanY => tipY + 0.012f - Y0;
            // Arm-space point (x mirrored for the left hand) to chart UV
            public Vector2 UV(float x, float y, bool back) => new((back ? 0f : 0.5f) + 0.5f * (0.5f + x / SpanX), (y - Y0) / SpanY);
        }

        const string SourceFolder = "Assets/Editor/CC0Textures";

        class Src
        {
            readonly Color[] px;
            readonly int size;
            public Src(string name)
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes($"{SourceFolder}/{name}.png"));
                px = t.GetPixels();
                size = t.width;
                Object.DestroyImmediate(t);
            }
            // Tiled every `tile` metres
            public Color At(float x, float y, float tile)
            {
                int i = ((int)(x / tile * size) % size + size) % size, j = ((int)(y / tile * size) % size + size) % size;
                return px[j * size + i];
            }
            public Vector3 Normal(float x, float y, float tile, float strength = 1f)
            {
                var c = At(x, y, tile);
                return new Vector3((c.r * 2f - 1f) * strength, (c.g * 2f - 1f) * strength, c.b * 2f - 1f);
            }
        }

        // Rounded-rectangle distance (negative inside)
        static float Box(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float dx = Mathf.Abs(x - cx) - hx + r, dy = Mathf.Abs(y - cy) - hy + r;
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }
        static float Lum(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;

        public static (Texture2D albedo, Texture2D normal, Texture2D jacket, Texture2D jacketNormal, Texture2D cuffNormal) BakeGloveTextures(GloveChart chart)
        {
            var quilt = new Src("Leather034C_Color"); var quiltN = new Src("Leather034C_Normal");
            var leather = new Src("Leather026_Color"); var leatherN = new Src("Leather026_Normal"); var leatherR = new Src("Leather026_Roughness");
            var perf = new Src("Leather018_Color"); var perfN = new Src("Leather018_Normal");
            var fabric = new Src("Fabric082A_Color"); var fabricN = new Src("Fabric082A_Normal");

            const int W = 1024, H = 1024;
            var col = new Color[W * H];
            var height = new float[W * H];
            var detail = new Vector3[W * H];
            var glow = new float[W * H]; // Void gloves: what lights up (stitching, grooves, the accent strip)
            float cx = 0f;
            foreach (var f in chart.fingerX) cx += f / 4f;
            float yW = chart.wristY, yK = chart.knuckleY;

            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                bool back = i < W / 2;
                float u = (i % (W / 2) + 0.5f) / (W / 2);
                float x = (u - 0.5f) * GloveChart.SpanX, y = chart.Y0 + (j + 0.5f) / H * chart.SpanY;
                // Smooth black leather everywhere to start
                Color c = leather.At(x, y, 0.06f) * 1.15f;
                float smooth = 0.75f - leatherR.At(x, y, 0.06f).r * 0.45f;
                float h = 0f, g = 0f;
                Vector3 n = leatherN.Normal(x, y, 0.06f, 0.8f);
                void Stitch(float d, float inset)
                {
                    if (d > -inset - 0.0006f && d < -inset + 0.0006f && Mathf.Repeat((x + y) / 0.0036f, 1f) < 0.6f)
                    {
                        c = new Color(0.42f, 0.42f, 0.44f); smooth = 0.3f; h += 0.00025f; g = 1f;
                    }
                }
                if (back)
                {
                    // Quilted padded panel over the back of the hand, stitched round, a red accent under it
                    float panel = Box(x, y, cx, (yW + 0.015f + yK - 0.016f) * 0.5f, 0.03f, (yK - 0.016f - yW - 0.015f) * 0.5f, 0.008f);
                    if (panel < 0f)
                    {
                        var q = quilt.At(x, y, 0.05f);
                        float l = Lum(q);
                        c = new Color(0.07f, 0.07f, 0.08f) + new Color(l, l, l) * 0.45f;
                        smooth = 0.55f;
                        n = quiltN.Normal(x, y, 0.05f, 1.2f);
                        h += 0.0009f * Mathf.Clamp01(-panel / 0.0025f);
                        Stitch(panel, 0.0028f);
                    }
                    float accentY = yK - 0.019f;
                    if (Mathf.Abs(y - accentY) < 0.0011f && Mathf.Abs(x - cx) < 0.031f) { c = new Color(0.6f, 0.05f, 0.05f); smooth = 0.7f; h += 0.0003f; g = 1f; }
                    // Rubber knuckle guard: a bar with a raised, grooved pad over each knuckle
                    float bar = Box(x, y, cx, yK - 0.008f, 0.035f, 0.004f, 0.003f);
                    if (bar < 0f) { c = new Color(0.05f, 0.05f, 0.055f); smooth = 0.25f; h += 0.0006f * Mathf.Clamp01(-bar / 0.002f); n = Vector3.forward; }
                    foreach (float fx in chart.fingerX)
                    {
                        float pad = Box(x, y, fx, yK + 0.002f, 0.0085f, 0.008f, 0.004f);
                        float glowHere = 0f;
                        if (pad < 0f)
                        {
                            c = new Color(0.055f, 0.055f, 0.06f) * (1f + 0.15f * Mathf.Clamp01(-pad / 0.004f));
                            smooth = 0.3f; n = Vector3.forward;
                            h += 0.0012f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-pad / 0.003f));
                            foreach (float gy in new[] { -0.003f, 0.003f })
                                if (Mathf.Abs(y - (yK + 0.002f + gy)) < 0.0005f) { h -= 0.0006f; glowHere = 0.8f; }
                        }
                        g = Mathf.Max(g, glowHere);
                        // A padded, vented panel on each finger, stitched round
                        float fp = Box(x, y, fx, yK + 0.03f, 0.0075f, 0.012f, 0.004f);
                        if (fp < 0f)
                        {
                            var p = perf.At(x, y, 0.04f);
                            float l = Lum(p);
                            c = new Color(0.06f, 0.06f, 0.07f) + new Color(l, l, l) * 0.12f;
                            n = perfN.Normal(x, y, 0.04f, 1.3f);
                            smooth = 0.4f;
                            h += 0.0006f * Mathf.Clamp01(-fp / 0.002f);
                            Stitch(fp, 0.0018f);
                        }
                        // Creases at the finger joints
                        foreach (float jy in new[] { yK + 0.047f, yK + 0.072f })
                            if (Mathf.Abs(x - fx) < 0.008f && Mathf.Abs(y - jy) < 0.0012f) h -= 0.0004f * (1f - Mathf.Abs(y - jy) / 0.0012f);
                    }
                }
                else
                {
                    // Perforated palm pad, stitched round
                    float palm = Box(x, y, cx, (yW + 0.01f + yK - 0.004f) * 0.5f, 0.033f, (yK - 0.004f - yW - 0.01f) * 0.5f, 0.01f);
                    if (palm < 0f)
                    {
                        var p = perf.At(x, y, 0.05f);
                        float l = Lum(p);
                        c = new Color(0.08f, 0.08f, 0.09f) + new Color(l, l, l) * 0.1f;
                        n = perfN.Normal(x, y, 0.05f, 1.4f);
                        smooth = 0.45f;
                        h += 0.0007f * Mathf.Clamp01(-palm / 0.003f);
                        Stitch(palm, 0.0025f);
                    }
                    // Grippy raised dots on the fingers
                    foreach (float fx in chart.fingerX)
                    {
                        if (Mathf.Abs(x - fx) > 0.0075f || y < yK + 0.012f) continue;
                        float gx = Mathf.Repeat(x - fx, 0.0024f) - 0.0012f, gy = Mathf.Repeat(y, 0.0024f) - 0.0012f;
                        float d = Mathf.Sqrt(gx * gx + gy * gy);
                        if (d < 0.0007f) { h += 0.0004f * (1f - d / 0.0007f); c *= 1.35f; smooth = 0.2f; }
                    }
                }
                c.a = Mathf.Clamp01(smooth);
                col[j * W + i] = c;
                height[j * W + i] = h;
                glow[j * W + i] = g;
                detail[j * W + i] = n;
            }

            // Normal: the relief's slope, blended with the scanned surface detail (whiteout)
            var nrm = new Color[W * H];
            float mx = GloveChart.SpanX / (W / 2), my = chart.SpanY / H;
            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                int half = i < W / 2 ? 0 : W / 2;
                int il = Mathf.Clamp(i - 1, half, half + W / 2 - 1), ir = Mathf.Clamp(i + 1, half, half + W / 2 - 1);
                int jd = Mathf.Max(j - 1, 0), ju = Mathf.Min(j + 1, H - 1);
                float dx = (height[j * W + ir] - height[j * W + il]) / ((ir - il) * mx);
                float dy = (height[ju * W + i] - height[jd * W + i]) / ((ju - jd) * my);
                var d = detail[j * W + i];
                var b = new Vector3(-dx + d.x, -dy + d.y, d.z).normalized;
                nrm[j * W + i] = new Color(b.x * 0.5f + 0.5f, b.y * 0.5f + 0.5f, b.z * 0.5f + 0.5f, 1f);
            }

            // Jacket: the woven fabric, near black with a cold blue cast; and the cuff's knit ribs
            const int J = 512;
            var jac = new Color[J * J];
            var jacN = new Color[J * J];
            for (int j = 0; j < J; j++)
            for (int i = 0; i < J; i++)
            {
                float x = (i + 0.5f) / J * 0.08f, y = (j + 0.5f) / J * 0.08f;
                float l = Lum(fabric.At(x, y, 0.04f));
                jac[j * J + i] = new Color(0.035f + l * 0.09f, 0.037f + l * 0.09f, 0.045f + l * 0.1f, 0.32f);
                var fn = fabricN.Normal(x, y, 0.04f, 1.2f).normalized;
                jacN[j * J + i] = new Color(fn.x * 0.5f + 0.5f, fn.y * 0.5f + 0.5f, fn.z * 0.5f + 0.5f, 1f);
            }
            var rib = new Color[256 * 64];
            for (int j = 0; j < 64; j++)
            for (int i = 0; i < 256; i++)
            {
                float s = Mathf.Cos((i + 0.5f) / 256f * Mathf.PI * 2f * 16f);
                var v = new Vector3(s * 0.6f, 0f, 1f).normalized;
                rib[j * 256 + i] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
            }

            // The glow mask, softened a little so seams light up with a soft edge
            var glowPx = new Color[W * H];
            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                float sum = 0f;
                for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                    sum += glow[Mathf.Clamp(j + dj, 0, H - 1) * W + Mathf.Clamp(i + di, 0, W - 1)];
                float v = Mathf.Max(glow[j * W + i], sum / 9f * 0.8f);
                glowPx[j * W + i] = new Color(v, v, v, 1f);
            }
            Write("GloveGlow", glowPx, W, H, false);
            return (Write("GloveAlbedo", col, W, H, false), Write("GloveNormal", nrm, W, H, true),
                Write("JacketAlbedo", jac, J, J, false), Write("JacketNormal", jacN, J, J, true), Write("CuffNormal", rib, 256, 64, true));
        }

        static Texture2D Write(string name, Color[] px, int w, int h, bool normal)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            string path = $"{ArmsFolder}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
