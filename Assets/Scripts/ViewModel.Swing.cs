using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow
{
    // Knife swing effects: every cut throws a glowing crescent across the screen that sweeps
    // along with the blade and fades, in the knife's own color (Void swords get a bigger, hotter
    // arc), with a spray of sparkles along it. If the cut reaches a surface it bites in with
    // sparks and a clink.
    public partial class ViewModel
    {
        const float ArcSweep = 0.13f, ArcFade = 0.22f, SlashReach = 2.4f;

        class Arc
        {
            public Transform t;
            public Material mat;
            public float age, side, size;
            public Color color;
        }

        readonly List<Arc> arcs = new();
        Mesh arcMesh;
        Texture2D arcTexture;

        // A crescent: a ring segment from -65 to 65 degrees, u along it, v across it
        Mesh ArcMesh()
        {
            if (arcMesh) return arcMesh;
            const int n = 32;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n, a = Mathf.Lerp(-65f, 65f, u) * Mathf.Deg2Rad;
                // Thickest in the middle, pointed at both ends
                float thick = Mathf.Sin(u * Mathf.PI);
                Vector3 dir = new(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts.Add(dir * (1f - 0.5f * thick));
                verts.Add(dir);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
                if (i < n)
                {
                    int b = i * 2;
                    tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3, b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                }
            }
            arcMesh = new Mesh { name = "Slash Arc" };
            arcMesh.SetVertices(verts);
            arcMesh.SetUVs(0, uvs);
            arcMesh.SetTriangles(tris, 0);
            return arcMesh;
        }

        // Bright along the outer edge, soft inside; the head (u = 1) bright, the tail fading
        Texture2D ArcTexture()
        {
            if (arcTexture) return arcTexture;
            const int w = 128, h = 32;
            arcTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f), v = y / (h - 1f);
                float across = 0.3f + 0.7f * Mathf.Pow(v, 1.4f) + (v > 0.88f ? 0.5f : 0f);
                float along = Mathf.Pow(u, 1.2f) * Mathf.Clamp01((1f - u) * 12f + 0.2f);
                float a = Mathf.Clamp01(across * along * 1.5f);
                float core = v > 0.8f ? 1f : Mathf.Lerp(0.6f, 1f, v);
                arcTexture.SetPixel(x, y, new Color(core, core, core, a));
            }
            arcTexture.Apply();
            return arcTexture;
        }

        Color SwingColor()
        {
            var skin = Skins.Knives[knifeSkin];
            if (skin.rarity == SkinRarity.Void)
                return knife.model switch
                {
                    KnifeModel.HollowMoon => new Color(1f, 0.12f, 0.15f),
                    KnifeModel.Tidebreaker => new Color(0.3f, 0.65f, 1f),
                    _ => new Color(0.7f, 0.35f, 1f),
                };
            if (skin.rarity == SkinRarity.Mythic) return Color.Lerp(Skins.RarityColor(skin.rarity), Color.white, 0.45f);
            return new Color(0.85f, 0.9f, 1f);
        }

        // Called on each slash (side = which way this cut goes)
        void SwingFx(float side)
        {
            bool big = knife.IsSword;
            Color color = SwingColor();
            var go = new GameObject("Slash Arc");
            go.layer = Layer;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = ArcMesh();
            var r = go.AddComponent<MeshRenderer>();
            var mat = MakeTransparent(new Material(fadeTemplate ? fadeTemplate : template));
            mat.SetTexture("_BaseMap", ArcTexture());
            mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 1f));
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", ArcTexture());
            mat.SetColor("_EmissionColor", color * (big ? 3f : 2.2f));
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            arcs.Add(new Arc { t = go.transform, mat = mat, side = side, size = big ? 0.26f : 0.19f, color = color });
            UpdateArcs(0f);

            // Sparkles flung along the cut, out in the world in front of you
            var fx = FxLibrary.Instance;
            if (fx)
            {
                Transform v = transform;
                for (int i = 0; i < (big ? 14 : 8); i++)
                {
                    float u = Random.value;
                    Vector3 across = (v.right * side * Mathf.Lerp(1f, -1f, u) + v.up * Mathf.Lerp(0.6f, -0.5f, u)) * 0.9f;
                    FxLibrary.Sparkle(v.position + v.forward * 1.6f + across, v.right * -side * Random.Range(1.5f, 4f) + Random.insideUnitSphere, color);
                }
            }

            // Bite into whatever the cut reaches
            if (Physics.Raycast(transform.position, transform.forward, out var hit, SlashReach, player ? player.collisionMask : (LayerMask)~0, QueryTriggerInteraction.Ignore)
                && !(player && hit.collider.transform.IsChildOf(player.transform)))
            {
                FxLibrary.Impact(hit.point, hit.normal);
                Play(WeaponSounds.Dry, 0.9f);
            }
        }

        void UpdateArcs(float dt)
        {
            for (int i = arcs.Count - 1; i >= 0; i--)
            {
                var a = arcs[i];
                a.age += dt;
                if (a.age > ArcSweep + ArcFade || !a.t)
                {
                    if (a.t) Kill(a.t.gameObject);
                    Kill(a.mat);
                    arcs.RemoveAt(i);
                    continue;
                }
                // Sweeps diagonally (upper right to lower left, mirrored on alternate cuts)
                // while it grows, then fades out
                float sweep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a.age / ArcSweep));
                float fade = 1f - Mathf.Clamp01((a.age - ArcSweep) / ArcFade);
                float roll = a.side * (125f - sweep * 95f);
                a.t.SetLocalPositionAndRotation(new Vector3(0.03f * a.side, -0.05f, 0.42f),
                    Quaternion.Euler(0f, a.side < 0f ? 180f : 0f, 0f) * Quaternion.Euler(18f, 0f, roll));
                float s = a.size * (0.85f + 0.25f * sweep);
                a.t.localScale = new Vector3(s, s, s);
                var c = a.mat.GetColor("_BaseColor");
                c.a = 0.75f * fade * Mathf.Clamp01(a.age / 0.03f);
                a.mat.SetColor("_BaseColor", c);
            }
        }
    }
}
