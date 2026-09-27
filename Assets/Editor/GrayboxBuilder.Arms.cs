using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Realistic hands for the view model, cut from a CC0 rigged character (Quaternius
    // Universal Base Characters, Assets/Models/CC0/Quaternius). For each side and each grip
    // the fingers are curled into shape on the character's own finger bones, then the hand is
    // baked into a still mesh in "arm space" (the space the view model's arms move in):
    // fingers along +Y, the back of the hand facing +Z, and a held handle's centre at
    // HandleAt, running along X. Only the hand is kept; the view model adds the sleeve.
    public static partial class GrayboxBuilder
    {
        const string CharacterPath = "Assets/Models/CC0/Quaternius/Superhero_Male_FullBody.fbx";
        const string ArmsFolder = "Assets/Resources/Arms";
        public static readonly Vector3 HandleAt = new(0f, 0.052f, 0f); // = ViewModel.GripFront

        // Joint bends (degrees) for the four fingers' three joints, and the thumb's
        public struct Grip
        {
            public string name;
            public Vector3 index, fingers, thumb; // x, y, z = joint 1, 2, 3
            public float thumbAcross;             // thumb swung across the palm
        }

        public static readonly Grip[] Grips =
        {
            new() { name = "Fist", index = new(75f, 90f, 45f), fingers = new(80f, 95f, 50f), thumb = new(15f, 40f, 30f), thumbAcross = 25f },
            new() { name = "Trigger", index = new(20f, 55f, 30f), fingers = new(80f, 95f, 50f), thumb = new(15f, 40f, 30f), thumbAcross = 25f },
            new() { name = "Support", index = new(40f, 50f, 30f), fingers = new(45f, 55f, 35f), thumb = new(10f, 15f, 10f), thumbAcross = 10f },
            new() { name = "Relaxed", index = new(30f, 35f, 20f), fingers = new(40f, 45f, 25f), thumb = new(8f, 15f, 10f), thumbAcross = 15f },
        };

        static readonly string[] FingerNames = { "index", "middle", "ring", "pinky" };

        [MenuItem("VoidFlow/Bake Arms")]
        public static void BakeArms()
        {
            Directory.CreateDirectory(ArmsFolder);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            foreach (var side in new[] { "r", "l" })
                foreach (var grip in Grips)
                {
                    var mesh = BakeHand(prefab, side, grip);
                    string path = $"{ArmsFolder}/{(side == "r" ? "Right" : "Left")}{grip.name}.asset";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(mesh, path);
                }
            AssetDatabase.SaveAssets();
        }

        static Mesh BakeHand(GameObject prefab, string side, Grip grip)
        {
            var body = Object.Instantiate(prefab);
            try
            {
                SkinnedMeshRenderer smr = null;
                foreach (var s in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (s.sharedMesh.vertexCount > 3000) smr = s;
                var bones = new Dictionary<string, Transform>();
                foreach (var t in body.GetComponentsInChildren<Transform>()) bones[t.name] = t;
                Transform B(string n) => bones[$"{n}_{side}"];

                // Arm space, taken with the fingers straight: along the middle finger, and the
                // back of the hand (in the T pose the palms face down)
                Transform hand = B("hand");
                Vector3 knuckles = Vector3.zero;
                foreach (var f in FingerNames) knuckles += B($"{f}_01").position;
                knuckles /= 4f;
                Vector3 along = (B("middle_02").position - B("middle_01").position).normalized;
                Vector3 back = Vector3.ProjectOnPlane(Vector3.up, along).normalized;
                Vector3 across = Vector3.Cross(along, back); // arm +X in world
                Vector3 palm = -back;

                // Curl: finger joints are hinges, all turning about the same axis across the hand
                // (worked out once, with the fingers straight). Positive bends toward the palm.
                void Hinge(Transform joint, Vector3 axis, float degrees) => joint.RotateAround(joint.position, axis, degrees);
                Vector3 fingerAxis = Vector3.Cross(along, palm).normalized;
                {
                    // Which way round is toward the palm?
                    Transform j1 = B("middle_01"), j2 = B("middle_02");
                    Vector3 before = j2.position;
                    Hinge(j1, fingerAxis, 10f);
                    bool right = Vector3.Dot(j2.position - before, palm) > 0f;
                    Hinge(j1, fingerAxis, -10f);
                    if (!right) fingerAxis = -fingerAxis;
                }
                foreach (var f in FingerNames)
                {
                    Vector3 bend = f == "index" ? grip.index : grip.fingers;
                    Hinge(B($"{f}_01"), fingerAxis, bend.x);
                    Hinge(B($"{f}_02"), fingerAxis, bend.y);
                    Hinge(B($"{f}_03"), fingerAxis, bend.z);
                }
                {
                    // The thumb swings across in front of the palm, then its joints close toward
                    // the fingers
                    Transform t1 = B("thumb_01"), t2 = B("thumb_02"), t3 = B("thumb_03");
                    Vector3 toFingers = (B("index_01").position - t1.position).normalized;
                    Vector3 swing = Vector3.Cross(palm, toFingers).normalized;
                    {
                        Vector3 before = t2.position;
                        Hinge(t1, swing, 10f);
                        bool right = Vector3.Dot(t2.position - before, palm) > 0f;
                        Hinge(t1, swing, -10f);
                        if (!right) swing = -swing;
                    }
                    Hinge(t1, swing, grip.thumbAcross);
                    Vector3 dir = (t2.position - t1.position).normalized;
                    Vector3 thumbAxis = Vector3.Cross(dir, toFingers).normalized;
                    {
                        Vector3 before = t3.position;
                        Hinge(t2, thumbAxis, 10f);
                        bool right = Vector3.Dot(t3.position - before, toFingers) > 0f;
                        Hinge(t2, thumbAxis, -10f);
                        if (!right) thumbAxis = -thumbAxis;
                    }
                    Hinge(t1, thumbAxis, grip.thumb.x);
                    Hinge(t2, thumbAxis, grip.thumb.y);
                    Hinge(t3, thumbAxis, grip.thumb.z);
                }

                // Where the handle goes: in the palm, just below the knuckles toward the wrist, where
                // the curled fingers close round it
                Vector3 handle = knuckles + palm * 0.024f - along * 0.012f;

                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var toWorld = smr.transform.localToWorldMatrix;

                // Keep the hand: triangles whose vertices all belong mostly to the hand or fingers
                var weights = smr.sharedMesh.boneWeights;
                var names = new string[smr.bones.Length];
                for (int i = 0; i < names.Length; i++) names[i] = smr.bones[i].name;
                bool Keep(int v)
                {
                    string n = names[weights[v].boneIndex0];
                    if (!n.EndsWith("_" + side)) return false;
                    if (n.StartsWith("hand") || n.StartsWith("thumb") || n.StartsWith("index") || n.StartsWith("middle") || n.StartsWith("ring") || n.StartsWith("pinky")) return true;
                    // the wrist end of the forearm, so the glove has a cuff to meet
                    return n.StartsWith("lowerarm") && Vector3.Dot(toWorld.MultiplyPoint3x4(baked.vertices[v]) - hand.position, -along) < 0.05f;
                }

                // Arm-space frame (a plain rotation: each hand stays a real right or left hand;
                // the right thumb ends up on +X, the left on -X)
                Vector3 X = across;
                var map = new Dictionary<int, int>();
                var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
                var srcV = baked.vertices; var srcN = baked.normals; var srcUv = baked.uv; var srcT = baked.triangles;
                Vector3 ToArm(Vector3 w) { Vector3 d = w - handle; return new Vector3(Vector3.Dot(d, X), Vector3.Dot(d, along), Vector3.Dot(d, back)) + HandleAt; }
                Vector3 DirToArm(Vector3 w) => new(Vector3.Dot(w, X), Vector3.Dot(w, along), Vector3.Dot(w, back));
                int Map(int v)
                {
                    if (map.TryGetValue(v, out int i)) return i;
                    i = verts.Count;
                    verts.Add(ToArm(toWorld.MultiplyPoint3x4(srcV[v])));
                    norms.Add(DirToArm(toWorld.MultiplyVector(srcN[v])).normalized);
                    // Box-projected UVs (one tile per 8cm), so glove finishes wrap the hand evenly
                    Vector3 pa = verts[i], na = norms[i];
                    Vector3 an = new(Mathf.Abs(na.x), Mathf.Abs(na.y), Mathf.Abs(na.z));
                    uvs.Add((an.x >= an.y && an.x >= an.z ? new Vector2(pa.z, pa.y) : an.y >= an.z ? new Vector2(pa.x, pa.z) : new Vector2(pa.x, pa.y)) / 0.08f);
                    return map[v] = i;
                }
                for (int t = 0; t < srcT.Length; t += 3)
                {
                    int a = srcT[t], b = srcT[t + 1], c = srcT[t + 2];
                    if (!Keep(a) || !Keep(b) || !Keep(c)) continue;
                    int ia = Map(a), ib = Map(b), ic = Map(c);
                    tris.AddRange(new[] { ia, ib, ic });
                }
                var mesh = new Mesh { name = $"{(side == "r" ? "Right" : "Left")}{grip.name}" };
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                Object.DestroyImmediate(baked);
                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }
    }
}
