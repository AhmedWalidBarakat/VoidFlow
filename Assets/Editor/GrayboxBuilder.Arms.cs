using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // The view model's arms, cut from a CC0 rigged character (Quaternius Universal Base
    // Characters, Assets/Models/CC0/Quaternius): each whole arm from the upper arm to the
    // fingertips, still skinned to its own bones so the fingers can move, re-expressed in "arm
    // space" (fingers along +Y, back of the hand +Z, a held handle along X through
    // ViewModel.GripFront). The surface is split into bare forearm, glove and a padded strap
    // round the wrist: three meshes sharing the same vertices along their borders and the same
    // bones, so glove and arm meet in one smooth line. Saved as Resources/Arms/<Side>Arm.
    public static partial class GrayboxBuilder
    {
        const string CharacterPath = "Assets/Models/CC0/Quaternius/Superhero_Male_FullBody.fbx";
        const string ArmsFolder = "Assets/Resources/Arms";
        public static readonly Vector3 HandleAt = new(0f, 0.052f, 0f); // = ViewModel.GripFront

        static readonly string[] FingerNames = { "index", "middle", "ring", "pinky" };

        [MenuItem("VoidFlow/Bake Arms")]
        public static void BakeArms()
        {
            Directory.CreateDirectory(ArmsFolder);
            foreach (var f in Directory.GetFiles(ArmsFolder)) File.Delete(f); // the old still hands
            AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            var rigs = new System.Collections.Generic.List<ArmRig>();
            GloveChart right = default;
            foreach (var side in new[] { "r", "l" })
            {
                var rig = BakeArm(prefab, side, out var chart);
                if (side == "r") right = chart;
                rigs.Add(rig);
                string path = $"{ArmsFolder}/{(side == "r" ? "Right" : "Left")}Arm.asset";
                AssetDatabase.CreateAsset(rig, path);
                AssetDatabase.AddObjectToAsset(rig.skin, rig);
                AssetDatabase.AddObjectToAsset(rig.glove, rig);
                AssetDatabase.AddObjectToAsset(rig.strap, rig);
            }
            // The glove and jacket textures, laid out on the right hand's chart (the left
            // hand's UVs are mirrored onto the same one)
            var (albedo, normal, jacket, jacketNormal, cuffNormal) = BakeGloveTextures(right);
            foreach (var rig in rigs)
            {
                rig.gloveAlbedo = albedo; rig.gloveNormal = normal;
                rig.jacketAlbedo = jacket; rig.jacketNormal = jacketNormal; rig.cuffNormal = cuffNormal;
                rig.gloveGlow = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArmsFolder}/GloveGlow.png");
                EditorUtility.SetDirty(rig);
            }
            AssetDatabase.SaveAssets();
        }

        static ArmRig BakeArm(GameObject prefab, string side, out GloveChart chart)
        {
            var body = Object.Instantiate(prefab);
            try
            {
                SkinnedMeshRenderer smr = null;
                foreach (var s in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (s.sharedMesh.vertexCount > 3000) smr = s;
                var byName = new Dictionary<string, Transform>();
                foreach (var t in body.GetComponentsInChildren<Transform>()) byName[t.name] = t;
                Transform B(string n) => byName[$"{n}_{side}"];

                // The arm's bones, root first
                var kept = new List<Transform> { B("upperarm"), B("lowerarm"), B("hand") };
                foreach (var f in FingerNames.Append("thumb"))
                    for (int j = 1; j <= 3; j++)
                    {
                        var joint = B($"{f}_0{j}");
                        kept.Add(joint);
                        if (j == 3 && joint.childCount > 0) kept.Add(joint.GetChild(0)); // fingertip end
                    }

                // Arm space, taken with the fingers straight (the T pose has the palms down)
                Transform hand = B("hand");
                Vector3 knuckles = Vector3.zero;
                foreach (var f in FingerNames) knuckles += B($"{f}_01").position;
                knuckles /= 4f;
                Vector3 along = (B("middle_02").position - B("middle_01").position).normalized;
                Vector3 back = Vector3.ProjectOnPlane(Vector3.up, along).normalized;
                Vector3 palm = -back;
                Vector3 handle = knuckles + palm * 0.024f - along * 0.012f;
                Quaternion armToWorld = Quaternion.LookRotation(back, along), worldToArm = Quaternion.Inverse(armToWorld);
                Vector3 ToArm(Vector3 w) => worldToArm * (w - handle) + HandleAt;

                // Hinge axes, in world: fingers all turn about the axis across the hand; the thumb
                // swings across the palm, then closes toward the fingers
                Vector3 fingerAxis = SignedAxis(Vector3.Cross(along, palm), B("middle_01"), B("middle_02"), palm);
                Transform t1 = B("thumb_01"), t2 = B("thumb_02"), t3 = B("thumb_03");
                Vector3 toFingers = (B("index_01").position - t1.position).normalized;
                Vector3 swing = SignedAxis(Vector3.Cross(palm, toFingers), t1, t2, palm);
                // (the thumb's own hinge is worked out with the thumb already swung across, as
                // the grips use it)
                Quaternion t1Rest = t1.rotation;
                t1.RotateAround(t1.position, swing, 20f);
                Vector3 thumbAxis = SignedAxis(Vector3.Cross((t2.position - t1.position).normalized, toFingers), t2, t3, toFingers);
                Quaternion t1Swung = t1.rotation, t2Swung = t2.rotation, t3Swung = t3.rotation;
                t1.rotation = t1Rest;

                // The glove's texture chart: wrist, knuckles and fingertips up the hand, and each
                // finger's place across it (mirrored on the left hand)
                float mirror = side == "r" ? 1f : -1f;
                chart = new GloveChart
                {
                    wristY = ToArm(hand.position).y,
                    knuckleY = ToArm(knuckles).y,
                    tipY = FingerNames.Max(f => ToArm(B($"{f}_03").GetChild(0).position).y),
                    fingerX = FingerNames.Select(f => mirror * ToArm(B($"{f}_01").position).x).ToArray(),
                };
                var gloveChart = chart;

                var rig = ScriptableObject.CreateInstance<ArmRig>();
                rig.name = side == "r" ? "RightArm" : "LeftArm";
                int n = kept.Count;
                rig.boneNames = kept.Select(k => k.name).ToArray();
                rig.parents = kept.Select(k => kept.IndexOf(k.parent)).ToArray();
                rig.bindPositions = new Vector3[n];
                rig.bindRotations = new Quaternion[n];
                var armRot = new Quaternion[n];
                var armPos = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    armPos[i] = ToArm(kept[i].position);
                    armRot[i] = worldToArm * kept[i].rotation;
                }
                for (int i = 0; i < n; i++)
                {
                    int p = rig.parents[i];
                    rig.bindPositions[i] = p < 0 ? armPos[i] : Quaternion.Inverse(armRot[p]) * (armPos[i] - armPos[p]);
                    rig.bindRotations[i] = p < 0 ? armRot[i] : Quaternion.Inverse(armRot[p]) * armRot[i];
                }
                Vector3 Local(Transform joint, Vector3 worldAxis, Quaternion? worldRot = null) =>
                    Quaternion.Inverse(worldRot ?? joint.rotation) * worldAxis;
                rig.fingerJoints = new int[12];
                rig.fingerAxes = new Vector3[12];
                for (int f = 0; f < 4; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        var joint = B($"{FingerNames[f]}_0{j + 1}");
                        rig.fingerJoints[f * 3 + j] = kept.IndexOf(joint);
                        rig.fingerAxes[f * 3 + j] = Local(joint, fingerAxis);
                    }
                rig.thumbJoints = new[] { kept.IndexOf(t1), kept.IndexOf(t2), kept.IndexOf(t3) };
                rig.thumbAxes = new[] { Local(t1, thumbAxis, t1Swung), Local(t2, thumbAxis, t2Swung), Local(t3, thumbAxis, t3Swung) };
                rig.thumbSwingAxis = Local(t1, swing);

                // The surface: triangles belonging to the arm, cut off short of the shoulder
                var src = smr.sharedMesh;
                var toWorld = smr.transform.localToWorldMatrix;
                var srcV = src.vertices; var srcN = src.normals; var srcW = src.boneWeights; var srcT = src.triangles;
                var srcBones = smr.bones;
                int Remap(int boneIndex)
                {
                    int k = kept.IndexOf(srcBones[boneIndex]);
                    return k >= 0 ? k : 0; // shoulder and chest weight goes to the upper arm
                }
                Vector3 elbow = B("lowerarm").position, shoulder = B("upperarm").position;
                Vector3 up = (shoulder - elbow).normalized;
                bool OnArm(int v)
                {
                    if (!kept.Contains(srcBones[srcW[v].boneIndex0])) return false;
                    // keep the upper arm only to 12cm above the elbow
                    return Vector3.Dot(toWorld.MultiplyPoint3x4(srcV[v]) - elbow, up) < 0.12f;
                }
                Vector3 wrist = hand.position;
                // Along the forearm, how far from the wrist (+ toward the elbow)
                float FromWrist(Vector3 w) => Vector3.Dot(w - wrist, -along);
                const float GloveTo = 0.02f, StrapTo = 0.048f;
                // Every vertex as a record (world position, normal, bone weights), so triangles can
                // be cut exactly along the glove's and the strap's edges: new vertices on a cut
                // edge are shared by the pieces either side, so nothing cracks
                var recPos = new List<Vector3>(); var recNorm = new List<Vector3>(); var recW = new List<Dictionary<int, float>>();
                var fromSource = new Dictionary<int, int>();
                int Rec(int s)
                {
                    if (fromSource.TryGetValue(s, out int r)) return r;
                    r = fromSource[s] = recPos.Count;
                    recPos.Add(toWorld.MultiplyPoint3x4(srcV[s]));
                    recNorm.Add(toWorld.MultiplyVector(srcN[s]).normalized);
                    var bw = srcW[s];
                    var d = new Dictionary<int, float>();
                    foreach (var (bi, wt) in new[] { (bw.boneIndex0, bw.weight0), (bw.boneIndex1, bw.weight1), (bw.boneIndex2, bw.weight2), (bw.boneIndex3, bw.weight3) })
                    {
                        if (wt <= 0f) continue;
                        int k = Remap(bi);
                        d[k] = (d.TryGetValue(k, out float o) ? o : 0f) + wt;
                    }
                    recW.Add(d);
                    return r;
                }
                var cutCache = new Dictionary<(int, int, float), int>();
                int Cut(int a0, int b0, float plane)
                {
                    var key = a0 < b0 ? (a0, b0, plane) : (b0, a0, plane);
                    if (cutCache.TryGetValue(key, out int r)) return r;
                    int lo = key.Item1, hi = key.Item2; // always interpolate the same way round
                    float da = FromWrist(recPos[lo]), db = FromWrist(recPos[hi]);
                    float f = Mathf.Clamp01((plane - da) / (db - da));
                    r = recPos.Count;
                    recPos.Add(Vector3.Lerp(recPos[lo], recPos[hi], f));
                    recNorm.Add(Vector3.Lerp(recNorm[lo], recNorm[hi], f).normalized);
                    var d = new Dictionary<int, float>();
                    foreach (var kv in recW[lo]) d[kv.Key] = kv.Value * (1f - f);
                    foreach (var kv in recW[hi]) d[kv.Key] = (d.TryGetValue(kv.Key, out float o) ? o : 0f) + kv.Value * f;
                    recW.Add(d);
                    return cutCache[key] = r;
                }
                // Splits a polygon (record indices) at a distance along the arm into the part nearer
                // the hand and the part beyond
                (List<int> near, List<int> far) Split(List<int> poly, float plane)
                {
                    var near = new List<int>(); var far = new List<int>();
                    for (int i = 0; i < poly.Count; i++)
                    {
                        int a0 = poly[i], b0 = poly[(i + 1) % poly.Count];
                        bool an = FromWrist(recPos[a0]) < plane, bn = FromWrist(recPos[b0]) < plane;
                        (an ? near : far).Add(a0);
                        if (an != bn) { int c = Cut(a0, b0, plane); near.Add(c); far.Add(c); }
                    }
                    return (near, far);
                }
                var regions = new[] { new List<int>(), new List<int>(), new List<int>() }; // skin, glove, strap: record indices, 3 per triangle
                void Emit(List<int> poly, int region)
                {
                    for (int i = 1; i + 1 < poly.Count; i++) regions[region].AddRange(new[] { poly[0], poly[i], poly[i + 1] });
                }
                for (int t = 0; t < srcT.Length; t += 3)
                {
                    int a = srcT[t], b = srcT[t + 1], c = srcT[t + 2];
                    if (!OnArm(a) || !OnArm(b) || !OnArm(c)) continue;
                    var (glovePart, rest) = Split(new List<int> { Rec(a), Rec(b), Rec(c) }, GloveTo);
                    var (strapPart, skinPart) = rest.Count >= 3 ? Split(rest, StrapTo) : (new List<int>(), new List<int>());
                    if (glovePart.Count >= 3) Emit(glovePart, 1);
                    if (strapPart.Count >= 3) Emit(strapPart, 2);
                    if (skinPart.Count >= 3) Emit(skinPart, 0);
                }

                // Shared vertices: the glove stands a little proud of the skin and the strap
                // more, easing in and out so the surface stays smooth
                var bindposes = new Matrix4x4[n];
                for (int i = 0; i < n; i++) bindposes[i] = Matrix4x4.TRS(armPos[i], armRot[i], Vector3.one).inverse;
                Mesh Build(List<int> tris, string name)
                {
                    var map = new Dictionary<int, int>();
                    var v = new List<Vector3>(); var nn = new List<Vector3>(); var uv = new List<Vector2>(); var w = new List<BoneWeight>(); var tr = new List<int>();
                    foreach (int s in tris)
                    {
                        if (!map.TryGetValue(s, out int i))
                        {
                            i = map[s] = v.Count;
                            Vector3 world = recPos[s];
                            Vector3 normalW = recNorm[s];
                            float d = FromWrist(world);
                            float proud = 0.0012f * Mathf.Clamp01((GloveTo + 0.004f - d) / 0.004f)
                                + 0.0022f * Mathf.Clamp01(1f - Mathf.Abs(d - (GloveTo + StrapTo) * 0.5f) / ((StrapTo - GloveTo) * 0.5f + 0.003f));
                            Vector3 p = ToArm(world + normalW * proud);
                            Vector3 nA = worldToArm * normalW;
                            v.Add(p); nn.Add(nA);
                            Vector3 an = new(Mathf.Abs(nA.x), Mathf.Abs(nA.y), Mathf.Abs(nA.z));
                            if (name == "Glove") uv.Add(gloveChart.UV(mirror * p.x, p.y, nA.z >= 0f));
                            else uv.Add((an.x >= an.y && an.x >= an.z ? new Vector2(p.z, p.y) : an.y >= an.z ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y)) / 0.08f);
                            var list = recW[s].OrderByDescending(x => x.Value).Select(x => (b: x.Key, wt: x.Value)).Take(4).ToList();
                            while (list.Count < 4) list.Add((0, 0f));
                            float sum = Mathf.Max(1e-5f, list.Sum(x => x.wt));
                            w.Add(new BoneWeight
                            {
                                boneIndex0 = list[0].b, weight0 = list[0].wt / sum,
                                boneIndex1 = list[1].b, weight1 = list[1].wt / sum,
                                boneIndex2 = list[2].b, weight2 = list[2].wt / sum,
                                boneIndex3 = list[3].b, weight3 = list[3].wt / sum,
                            });
                        }
                        tr.Add(i);
                    }
                    var m = new Mesh { name = $"{rig.name}{name}" };
                    m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv);
                    m.boneWeights = w.ToArray();
                    m.bindposes = bindposes;
                    m.SetTriangles(tr, 0);
                    m.RecalculateBounds();
                    m.RecalculateTangents();
                    return m;
                }
                rig.skin = Build(regions[0], "Skin");
                rig.glove = Build(regions[1], "Glove");
                rig.strap = Build(regions[2], "Strap");
                return rig;
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        // An axis to turn `joint` about so `next` moves toward `toward` (the sign checked by
        // trying it)
        static Vector3 SignedAxis(Vector3 axis, Transform joint, Transform next, Vector3 toward)
        {
            axis.Normalize();
            Quaternion rest = joint.rotation;
            Vector3 before = next.position;
            joint.RotateAround(joint.position, axis, 10f);
            bool right = Vector3.Dot(next.position - before, toward) > 0f;
            joint.rotation = rest;
            return right ? axis : -axis;
        }
    }
}
