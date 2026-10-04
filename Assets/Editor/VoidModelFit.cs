using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Fits the Void weapons made from Sketchfab models (CC-BY, see CREDITS.md) into the game's
    // weapon spaces and saves each as a prefab (Resources/VoidModels/<name>/fitted.prefab):
    //  - blades in hand space: handle along +Y below the guard (the fist grips y -0.11..0), the
    //    blade up +Y, its flat in the XY plane
    //  - rifles in rifle space: +Z along the barrel, the trigger at the origin, up +Y
    // Each model's long axis is found from its geometry (principal axis); a blade's handle is the
    // end nearest its widest point (the guard), a scythe's the end away from its head, a rifle's
    // muzzle its thinner end. Flat display props (pedestals, backdrops) are dropped. Per-model
    // overrides fix anything the measuring gets wrong.
    public static class VoidModelFit
    {
        public enum Kind { Blade, Scythe, Rifle }

        public class Fit
        {
            public string folder;
            public Kind kind;
            public float length;          // overall length in weapon space
            public bool flip;             // swap the ends the measuring picked
            public float roll;            // turn about the long axis (degrees)
            public string recolor;        // a repainted colour texture in the model's textures folder, worn as polished metal
            public float grip = -1f;      // where the guard / trigger is, 0..1 from the back end (when the measuring misses it)
            public string[] drop = new string[0]; // parts to leave out (name contains)
            public float isolate;         // > 0: keep only the main connected piece and the pieces along its axis within this share of its length
            public bool isolateStraight;  // the main piece is the most elongated one, not the longest (a looping chain can be longer than the blade)
            public Fit(string folder, Kind kind, float length) { this.folder = folder; this.kind = kind; this.length = length; }
        }

        public static readonly Fit[] Fits =
        {
            new("01_gold_skull_glory_sword", Kind.Blade, 0.62f),
            new("02_desolate_devil_scythe", Kind.Scythe, 0.85f) { grip = 0.3f },
            new("03_bloody_rose_sword", Kind.Blade, 0.62f),
            new("05_abyssal_heart", Kind.Blade, 0.62f),
            new("06_demonic_twinblades", Kind.Blade, 0.4f) { isolate = 0.08f, isolateStraight = true, flip = true, grip = 0.45f },
            new("07_golden_blood", Kind.Blade, 0.66f) { recolor = "GoldCrimson_baseColor.png" }, // gold and crimson, as the name says
            new("08_steampunk_sword", Kind.Blade, 0.66f),
            new("09_jade_sword", Kind.Blade, 0.62f),
            new("10_shattered_crystal", Kind.Blade, 0.58f),
            new("11_demon_sword", Kind.Blade, 0.68f),
            new("12_soulsucker", Kind.Blade, 0.58f) { drop = new[] { "vfx" } },
            new("13_primordial_lance", Kind.Blade, 0.6f) { drop = new[] { "gear" } },
            new("14_gradient_sword", Kind.Blade, 0.62f),
            new("15_cyber_blade", Kind.Blade, 0.58f),
            new("16_divine_reaper", Kind.Scythe, 0.85f) { grip = 0.3f },
            new("17_squid_dagger", Kind.Blade, 0.36f),
            new("18_autumn_sword", Kind.Blade, 0.6f),
            // Void knives
            new("28_ice_cyclone", Kind.Blade, 0.4f),
            new("29_crystal_fantasy", Kind.Blade, 0.4f),
            new("30_karambit_rubi", Kind.Blade, 0.34f) { roll = 90f },
            new("31_cyberpunk_knife", Kind.Blade, 0.38f) { roll = 90f },
            new("32_miraigata_kunai", Kind.Blade, 0.38f) { flip = true, grip = 0.28f },
            new("33_fel_whisper", Kind.Blade, 0.4f) { flip = true, roll = 90f, grip = 0.25f },
            new("34_crystal_dagger", Kind.Blade, 0.4f) { roll = 90f },
            new("35_karambit_red", Kind.Blade, 0.34f) { roll = 90f },
            new("19_scifi_sniper", Kind.Rifle, 1.15f) { roll = -90f },
            new("21_futuristic_sniper", Kind.Rifle, 1.15f) { roll = 90f },
            new("24_renegade_railgun", Kind.Rifle, 1.15f) { flip = true, grip = 0.4f }, // (the barrel shroud is as deep as the stock: the measuring picks the wrong end)
            new("26_nexus_railgun", Kind.Rifle, 1.15f) { flip = true, grip = 0.4f },
        };

        // the downloaded models stay out of Resources (everything in there ships in the build); only
        // the fitted weapons, with just the meshes they show, go in
        const string Sources = "Assets/VoidModelSources", Root = "Assets/Resources/VoidModels";

        [MenuItem("VoidFlow/Fit Void Models")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/Resources", "VoidModels");
            var log = new StringBuilder("VOIDFIT\n");
            foreach (var fit in Fits) log.AppendLine(FitOne(fit));
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static string FitOne(Fit fit)
        {
            string dir = $"{Root}/{fit.folder}";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Sources}/{fit.folder}/scene.gltf");
            if (!source) return $"{fit.folder}: not imported";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, fit.folder);
            foreach (var old in AssetDatabase.FindAssets("t:Mesh t:Material", new[] { dir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(old));
            var model = (GameObject)Object.Instantiate(source);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;

            if (fit.isolate > 0f) Isolate(model, fit, dir);

            // Everything's points in model space, dropping flat display props
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds all = default; bool any = false;
            foreach (var r in renderers) { if (any) all.Encapsulate(r.bounds); else { all = r.bounds; any = true; } }
            float extent = all.size.magnitude;
            var points = new List<Vector3>();
            int dropped = 0;
            foreach (var r in renderers)
            {
                var b = r.bounds;
                float lo = Mathf.Min(b.size.x, Mathf.Min(b.size.y, b.size.z)), hi = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                float mid = b.size.x + b.size.y + b.size.z - lo - hi;
                // a stand or backdrop is a wide flat piece that fills its outline; a blade modelled
                // as one flat sheet (even a curved one, whose outline is squarish) covers little of it
                bool flatProp = lo < extent * 0.004f && mid > extent * 0.25f && Coverage(r) > 0.5f;
                bool named = System.Array.Exists(fit.drop, d => r.name.Contains(d));
                if (flatProp || named) { Object.DestroyImmediate(r.gameObject); dropped++; continue; }
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) { mesh = new Mesh(); smr.BakeMesh(mesh, true); }
                else if (r.TryGetComponent(out MeshFilter mf)) mesh = mf.sharedMesh;
                if (!mesh) continue;
                var v = mesh.vertices;
                int step = Mathf.Max(1, v.Length / 6000);
                for (int i = 0; i < v.Length; i += step) points.Add(r.transform.TransformPoint(v[i]));
            }
            if (points.Count < 10) { Object.DestroyImmediate(model); return $"{fit.folder}: no geometry"; }

            // Principal axes
            Vector3 c = Vector3.zero;
            foreach (var p in points) c += p;
            c /= points.Count;
            var m = new float[3, 3];
            foreach (var p in points)
            {
                Vector3 d = p - c;
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) m[i, j] += d[i] * d[j];
            }
            Vector3 a1 = PowerAxis(m, Vector3.zero, Vector3.zero);
            Vector3 a2 = PowerAxis(m, a1, Vector3.zero);
            Vector3 a3 = Vector3.Cross(a1, a2).normalized;

            // Along the long axis: the spread (width) of the model in 40 slices
            float min = float.MaxValue, max = float.MinValue;
            foreach (var p in points) { float s = Vector3.Dot(p - c, a1); min = Mathf.Min(min, s); max = Mathf.Max(max, s); }
            const int Bins = 40;
            var wlo = new float[Bins]; var whi = new float[Bins];
            for (int i = 0; i < Bins; i++) { wlo[i] = float.MaxValue; whi[i] = float.MinValue; }
            foreach (var p in points)
            {
                float s = Vector3.Dot(p - c, a1);
                int k = Mathf.Clamp((int)((s - min) / (max - min) * Bins), 0, Bins - 1);
                float w = Vector3.Dot(p - c, a2);
                wlo[k] = Mathf.Min(wlo[k], w); whi[k] = Mathf.Max(whi[k], w);
            }
            float Width(int k) => whi[k] > wlo[k] ? whi[k] - wlo[k] : 0f;
            float SumWidth(int from, int to) { float t = 0f; for (int k = from; k < to; k++) t += Width(k); return t; }

            Vector3 forward; // from the grip end toward the tip / muzzle
            float gripAt;    // where the grip (blade) or trigger (rifle) is, along `forward`, 0..1 from the back end
            if (fit.kind == Kind.Rifle)
            {
                // The muzzle is the thinner end
                bool muzzleAtMax = SumWidth(Bins - 6, Bins) < SumWidth(0, 6);
                forward = muzzleAtMax ? a1 : -a1;
                gripAt = 0.36f;
            }
            else
            {
                int widest = 0;
                for (int k = 1; k < Bins; k++) if (Width(k) > Width(widest)) widest = k;
                bool guardNearMin = widest < Bins / 2;
                // a blade's handle is at the guard's end; a scythe's at the end away from its head
                bool handleAtMin = fit.kind == Kind.Blade ? guardNearMin : !guardNearMin;
                forward = handleAtMin ? a1 : -a1;
                float guard = handleAtMin ? widest / (float)Bins : 1f - (widest + 1) / (float)Bins;
                gripAt = fit.kind == Kind.Blade ? Mathf.Clamp(guard, 0.06f, 0.35f) : 0.18f;
            }
            if (fit.flip) { forward = -forward; if (fit.kind != Kind.Rifle) gripAt = 1f - gripAt; } // (a rifle's trigger share is from the stock whichever end that is)
            if (fit.grip >= 0f) gripAt = fit.grip;

            // Rotate: blades' long axis to +Y with the blade's width along X; rifles' to +Z with
            // up the axis nearest the model's own up
            Quaternion rot;
            if (fit.kind == Kind.Rifle)
            {
                Vector3 up = Mathf.Abs(Vector3.Dot(a2, Vector3.up)) > Mathf.Abs(Vector3.Dot(a3, Vector3.up)) ? a2 : a3;
                if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;
                rot = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            }
            else
            {
                // the blade's flat: its widest direction measured over the blade alone (past the
                // guard), so a big guard or pommel doesn't decide it
                float sxx = 0f, sxy = 0f, syy = 0f;
                foreach (var p in points)
                {
                    float along = Vector3.Dot(p - c, forward), from = Vector3.Dot(forward, a1) > 0 ? along - min : along + max;
                    if (from / (max - min) < gripAt + 0.12f) continue;
                    float x = Vector3.Dot(p - c, a2), y = Vector3.Dot(p - c, a3);
                    sxx += x * x; sxy += x * y; syy += y * y;
                }
                float ang = 0.5f * Mathf.Atan2(2f * sxy, sxx - syy);
                Vector3 width = (a2 * Mathf.Cos(ang) + a3 * Mathf.Sin(ang)).normalized;
                rot = Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(width, forward).normalized, forward));
            }
            rot = Quaternion.AngleAxis(fit.roll, fit.kind == Kind.Rifle ? Vector3.forward : Vector3.up) * rot;

            float span = max - min, scale = fit.length / span;
            // where the back end and the grip land after rotating and scaling
            Vector3 back = c + forward * (Vector3.Dot(forward, a1) > 0 ? min : -max);
            Vector3 grip = back + forward * (gripAt * span);
            var root = new GameObject(fit.folder);
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * scale;
            model.transform.localRotation = rot;
            Vector3 gripNow = rot * grip * scale;
            // blades: the guard just above the fist (y 0), the handle down through it; rifles: the
            // trigger at the origin, the body centred on the bore
            Vector3 target = fit.kind == Kind.Rifle ? Vector3.zero : new Vector3(0f, 0.005f, 0f);
            model.transform.localPosition = target - gripNow;
            if (fit.kind == Kind.Rifle)
            {
                Bounds rb = default; bool f = false;
                foreach (var r in root.GetComponentsInChildren<Renderer>()) { if (f) rb.Encapsulate(r.bounds); else { rb = r.bounds; f = true; } }
                model.transform.localPosition += Vector3.up * (0.01f - rb.center.y);
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            SaveMeshes(root, dir);
            SaveMaterials(root, dir, fit);
            string path = $"{dir}/fitted.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return $"{fit.folder}: span {span:0.###} -> {fit.length} (x{scale:0.####}), grip at {gripAt:0.00}, dropped {dropped}";
        }

        // For models that are one mesh holding more than the weapon (a second blade, a chain): bakes
        // each mesh, splits it into connected pieces and keeps the longest piece plus the pieces lying
        // along its axis (handle, guard), saving the result as a mesh asset beside the model
        static void Isolate(GameObject model, Fit fit, string dir)
        {
            int n = 0;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh src; string rname = r.name;
                if (r is SkinnedMeshRenderer smr) { src = new Mesh(); smr.BakeMesh(src, true); }
                else if (r.TryGetComponent(out MeshFilter mf)) src = mf.sharedMesh;
                else continue;
                var v = src.vertices; var tri = src.triangles;
                // weld by position, then union the triangles' corners
                var parent = new int[v.Length];
                for (int i = 0; i < v.Length; i++) parent[i] = i;
                int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
                void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
                var weld = new Dictionary<Vector3Int, int>();
                float q = src.bounds.size.magnitude * 1e-4f;
                for (int i = 0; i < v.Length; i++)
                {
                    var k = Vector3Int.RoundToInt(v[i] / q);
                    if (weld.TryGetValue(k, out int j)) Union(i, j); else weld[k] = i;
                }
                for (int t = 0; t < tri.Length; t += 3) { Union(tri[t], tri[t + 1]); Union(tri[t], tri[t + 2]); }
                var pieces = new Dictionary<int, Bounds>();
                for (int i = 0; i < v.Length; i++)
                {
                    int c = Find(i);
                    if (pieces.TryGetValue(c, out var b)) { b.Encapsulate(v[i]); pieces[c] = b; } else pieces[c] = new Bounds(v[i], Vector3.zero);
                }
                int main = -1; float best = 0f;
                foreach (var kv in pieces)
                {
                    var sz = kv.Value.size;
                    float hi3 = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)), lo3 = Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)), mid3 = sz.x + sz.y + sz.z - hi3 - lo3;
                    float score = fit.isolateStraight ? hi3 * hi3 / Mathf.Max(mid3, hi3 * 0.01f) : sz.magnitude;
                    if (score > best) { best = score; main = kv.Key; }
                }
                // the main piece's axis
                Vector3 c0 = Vector3.zero; int cnt = 0;
                for (int i = 0; i < v.Length; i++) if (Find(i) == main) { c0 += v[i]; cnt++; }
                c0 /= cnt;
                var m = new float[3, 3];
                for (int i = 0; i < v.Length; i++)
                {
                    if (Find(i) != main) continue;
                    Vector3 d = v[i] - c0;
                    for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) m[a, b] += d[a] * d[b];
                }
                Vector3 axis = PowerAxis(m, Vector3.zero, Vector3.zero);
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < v.Length; i++) if (Find(i) == main) { float s = Vector3.Dot(v[i] - c0, axis); lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s); }
                float span = hi - lo, radius = span * fit.isolate;
                // a piece is kept when it's near the axis: wholly, a little past the main piece's ends, or
                // (when the main piece is just the blade) by its centre, up to half a blade length past
                // either end, which takes in the handle and guard but not a chain hanging off it
                var far = new HashSet<int>();
                if (fit.isolateStraight)
                    foreach (var kv in pieces)
                    {
                        if (kv.Key == main) continue;
                        Vector3 d = kv.Value.center - c0; float s = Vector3.Dot(d, axis);
                        if ((d - axis * s).magnitude > radius || s < lo - span * 0.5f || s > hi + span * 0.5f) far.Add(kv.Key);
                    }
                else
                    for (int i = 0; i < v.Length; i++)
                    {
                        Vector3 d = v[i] - c0; float s = Vector3.Dot(d, axis);
                        if (Find(i) != main && ((d - axis * s).magnitude > radius || s < lo - span * 0.3f || s > hi + span * 0.3f)) far.Add(Find(i));
                    }
                var keep = new List<int>();
                for (int t = 0; t < tri.Length; t += 3) if (!far.Contains(Find(tri[t]))) { keep.Add(tri[t]); keep.Add(tri[t + 1]); keep.Add(tri[t + 2]); }
                // only the kept triangles' vertices, so the bounds and the fitting see just the weapon
                var remap = new Dictionary<int, int>();
                var kv2 = new List<Vector3>(); var kn = new List<Vector3>(); var kt = new List<Vector4>(); var ku = new List<Vector2>();
                var n0 = src.normals; var t0 = src.tangents; var u0 = src.uv;
                for (int i = 0; i < keep.Count; i++)
                {
                    if (!remap.TryGetValue(keep[i], out int j))
                    {
                        j = remap[keep[i]] = kv2.Count;
                        kv2.Add(v[keep[i]]);
                        if (n0.Length > 0) kn.Add(n0[keep[i]]);
                        if (t0.Length > 0) kt.Add(t0[keep[i]]);
                        if (u0.Length > 0) ku.Add(u0[keep[i]]);
                    }
                    keep[i] = j;
                }
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = rname + "_isolated" };
                mesh.SetVertices(kv2); mesh.SetNormals(kn); mesh.SetTangents(kt); mesh.SetUVs(0, ku);
                mesh.subMeshCount = 1; mesh.SetTriangles(keep, 0);
                mesh.RecalculateBounds();
                string path = $"{dir}/isolated{n++}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = r.gameObject; var mats = r.sharedMaterials;
                if (r is SkinnedMeshRenderer)
                {
                    // the baked mesh is in the renderer's space without its scale
                    Object.DestroyImmediate(r);
                    go.transform.localScale = Vector3.one;
                    var lossy = go.transform.lossyScale;
                    go.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = new[] { mats[0] };
                }
                else { go.GetComponent<MeshFilter>().sharedMesh = mesh; r.sharedMaterials = new[] { mats[0] }; }
                Debug.Log($"VOIDFIT isolate {fit.folder} {rname}: {pieces.Count} pieces, kept {keep.Count / 3} of {tri.Length / 3} triangles");
            }
            // the bones and animation no longer matter
            foreach (var a in model.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
        }

        // Gives the fitted weapon its own compressed copies of the meshes it shows (skinned ones baked
        // still), so the build carries those and not the whole downloaded model
        static void SaveMeshes(GameObject root, string dir)
        {
            int n = 0;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh(); smr.BakeMesh(baked, true);
                var go = smr.gameObject; var mats = smr.sharedMaterials;
                var was = smr.bounds; // where it showed, to put the still copy in the same place
                Object.DestroyImmediate(smr);
                go.AddComponent<MeshFilter>().sharedMesh = baked;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats;
                Match(go.transform, mr, was);
            }
            foreach (var a in root.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
            foreach (var a in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            var saved = new Dictionary<Mesh, Mesh>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var src = mf.sharedMesh;
                if (!src) continue;
                if (!saved.TryGetValue(src, out var copy))
                {
                    if (AssetDatabase.GetAssetPath(src).StartsWith(dir)) copy = src; // already ours (isolated)
                    else
                    {
                        copy = Object.Instantiate(src);
                        copy.name = src.name;
                        AssetDatabase.CreateAsset(copy, $"{dir}/mesh{n++}.asset");
                    }
                    MeshUtility.SetMeshCompression(copy, ModelImporterMeshCompression.Medium);
                    EditorUtility.SetDirty(copy);
                    saved[src] = copy;
                }
                mf.sharedMesh = copy;
            }
        }

        // Gives the weapon its own copies of its materials, made solid all round: every face drawn
        // from both sides (thin parts and single sheets otherwise vanish from behind), and lit
        // see-through ("blend") surfaces turned solid with cut-out edges (see-through bodies
        // sort wrongly and look hollow); unlit see-through glows stay see-through
        static void SaveMaterials(GameObject root, string dir, Fit fit)
        {
            var paint = fit.recolor != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Sources}/{fit.folder}/textures/{fit.recolor}") : null;
            int n = 0;
            var made = new Dictionary<Material, Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (!src) continue;
                    if (!made.TryGetValue(src, out var m))
                    {
                        m = new Material(src) { name = src.name };
                        SetFloat(m, "_Cull", 0f);
                        SetFloat(m, "_BUILTIN_CullMode", 0f);
                        bool glow = src.shader.name.Contains("unlit");
                        if (src.renderQueue >= 3000 && !glow)
                        {
                            SetFloat(m, "_Surface", 0f); SetFloat(m, "_BUILTIN_Surface", 0f);
                            SetFloat(m, "_Blend", 0f);
                            SetFloat(m, "_SrcBlend", 1f); SetFloat(m, "_DstBlend", 0f);
                            SetFloat(m, "_SrcBlendAlpha", 1f); SetFloat(m, "_DstBlendAlpha", 0f);
                            SetFloat(m, "_ZWrite", 1f); SetFloat(m, "_BUILTIN_ZWrite", 1f);
                            SetFloat(m, "_AlphaClip", 1f); SetFloat(m, "_BUILTIN_AlphaClip", 1f);
                            SetFloat(m, "alphaCutoff", 0.5f);
                            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                            m.DisableKeyword("_TRANSMISSION");
                            m.EnableKeyword("_ALPHATEST_ON");
                            m.SetOverrideTag("RenderType", "TransparentCutout");
                            m.renderQueue = 2450;
                        }
                        if (paint)
                        {
                            m.SetTexture("baseColorTexture", paint);
                            m.SetColor("baseColorFactor", Color.white);
                            SetFloat(m, "metallicFactor", 1f);
                            SetFloat(m, "roughnessFactor", 0.28f);
                        }
                        AssetDatabase.CreateAsset(m, $"{dir}/mat{n++}.mat");
                        made[src] = m;
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        static void SetFloat(Material m, string name, float value)
        {
            if (m.HasProperty(name)) m.SetFloat(name, value);
        }

        // Scales and moves a renderer's object so it covers the given world bounds again (baking a
        // skinned mesh loses track of its bones' scale)
        static void Match(Transform t, Renderer r, Bounds was)
        {
            var now = r.bounds;
            float k = was.size.magnitude / Mathf.Max(now.size.magnitude, 1e-9f);
            t.localScale *= k;
            t.position += was.center - r.bounds.center;
        }

        // How much of its bounding box's largest face a renderer's surface covers (one side)
        static float Coverage(Renderer r)
        {
            Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
            if (!mesh) return 1f;
            var v = mesh.vertices; var tri = mesh.triangles;
            var t = r.transform;
            float area = 0f;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = t.TransformPoint(v[tri[i]]), b = t.TransformPoint(v[tri[i + 1]]), c = t.TransformPoint(v[tri[i + 2]]);
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            var s = r.bounds.size;
            float hi = Mathf.Max(s.x, Mathf.Max(s.y, s.z)), lo = Mathf.Min(s.x, Mathf.Min(s.y, s.z)), mid = s.x + s.y + s.z - hi - lo;
            return area / Mathf.Max(hi * mid, 1e-9f);
        }

        // The dominant axis of a symmetric 3x3 matrix, orthogonal to the given ones
        static Vector3 PowerAxis(float[,] m, Vector3 not1, Vector3 not2)
        {
            Vector3 v = new Vector3(0.577f, 0.581f, 0.574f);
            for (int it = 0; it < 64; it++)
            {
                if (not1 != Vector3.zero) v -= Vector3.Dot(v, not1) * not1;
                if (not2 != Vector3.zero) v -= Vector3.Dot(v, not2) * not2;
                var n = new Vector3(
                    m[0, 0] * v.x + m[0, 1] * v.y + m[0, 2] * v.z,
                    m[1, 0] * v.x + m[1, 1] * v.y + m[1, 2] * v.z,
                    m[2, 0] * v.x + m[2, 1] * v.y + m[2, 2] * v.z);
                if (n.sqrMagnitude < 1e-20f) break;
                v = n.normalized;
            }
            if (not1 != Vector3.zero) v = (v - Vector3.Dot(v, not1) * not1).normalized;
            return v;
        }
    }
}
