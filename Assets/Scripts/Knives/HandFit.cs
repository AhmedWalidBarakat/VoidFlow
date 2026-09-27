using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // The shape of a real glove, measured so Void glove add-ons can be fitted to it: plates and
    // scales that lie on the back of the hand, rings that wrap the actual wrist and fingers,
    // claws out of the actual fingertips. Everything is in arm space (the rig's root: fingers
    // along +Y, back of the hand toward +Z), measured with the fingers straight, so a piece on a
    // finger can be hung on that finger's bone and curl with it.
    public class HandFit
    {
        public Transform space, hand;
        public readonly Transform[,] fingers = new Transform[4, 4]; // index, middle, ring, pinky: 01, 02, 03, leaf
        public float wristY, knuckleY;
        float xLo = -1f, xHi = 1f; // the hand's width (the thumb sticking out left out)
        Vector3[] verts;

        static readonly string[] FingerNames = { "index", "middle", "ring", "pinky" };

        public static HandFit Measure(ArmRig rig, Transform[] bones, params SkinnedMeshRenderer[] meshes)
        {
            var fit = new HandFit { space = bones[0].parent };
            for (int i = 0; i < bones.Length; i++)
            {
                string n = rig.boneNames[i];
                if (n.StartsWith("hand_")) fit.hand = bones[i];
                for (int f = 0; f < 4; f++)
                    for (int j = 0; j < 4; j++)
                        if (n.StartsWith($"{FingerNames[f]}_0{j + 1}")) fit.fingers[f, j] = bones[i];
            }
            // Measured with the fingers out straight
            rig.Pose(bones, default);
            var all = new List<Vector3>();
            var baked = new Mesh();
            foreach (var m in meshes)
            {
                if (!m) continue;
                m.BakeMesh(baked);
                var toSpace = fit.space.worldToLocalMatrix * m.transform.localToWorldMatrix;
                // BakeMesh leaves out the renderer's own scale; put it back
                var unscale = Matrix4x4.Scale(new Vector3(1f / m.transform.lossyScale.x, 1f / m.transform.lossyScale.y, 1f / m.transform.lossyScale.z));
                var mat = toSpace * unscale;
                foreach (var v in baked.vertices) all.Add(mat.MultiplyPoint3x4(v));
            }
            Object.DestroyImmediate(baked);
            fit.verts = all.ToArray();
            fit.wristY = fit.At(fit.hand).y;
            float k = 0f;
            for (int f = 0; f < 4; f++) k += fit.At(fit.fingers[f, 0]).y;
            fit.knuckleY = k / 4f;
            float a = fit.At(fit.fingers[0, 0]).x, b = fit.At(fit.fingers[3, 0]).x;
            fit.xLo = Mathf.Min(a, b) - 0.016f;
            fit.xHi = Mathf.Max(a, b) + 0.016f;
            return fit;
        }

        public Vector3 At(Transform t) => space.InverseTransformPoint(t.position);

        // The glove's outline across the hand at this height: its middle and half-widths
        public (Vector2 center, float rx, float rz) Section(float y, float band = 0.006f)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var v in verts)
            {
                if (Mathf.Abs(v.y - y) > band || v.x < xLo || v.x > xHi) continue;
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x);
                z0 = Mathf.Min(z0, v.z); z1 = Mathf.Max(z1, v.z);
            }
            if (x0 > x1) return (Vector2.zero, 0.04f, 0.02f);
            return (new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f), (x1 - x0) * 0.5f, (z1 - z0) * 0.5f);
        }

        // The top of the glove (back of the hand or of a finger) over this point
        public Vector3 Surface(float x, float y, float radius = 0.006f)
        {
            float top = float.MinValue;
            foreach (var v in verts)
                if (Mathf.Abs(v.x - x) < radius && Mathf.Abs(v.y - y) < radius) top = Mathf.Max(top, v.z);
            if (top == float.MinValue) top = Section(y).center.y + Section(y).rz;
            return new Vector3(x, y, top);
        }

        // Which way the surface faces there (tilted by its slope along and across the hand)
        public Vector3 Normal(float x, float y)
        {
            const float d = 0.01f;
            float dzx = (Surface(x + d, y).z - Surface(x - d, y).z) / (2f * d);
            float dzy = (Surface(x, y + d).z - Surface(x, y - d).z) / (2f * d);
            return new Vector3(-Mathf.Clamp(dzx, -1.5f, 1.5f), -Mathf.Clamp(dzy, -1.5f, 1.5f), 1f).normalized;
        }

        // A finger's knuckle-to-knuckle segment (joint j to j+1), in arm space
        public (Vector3 from, Vector3 to) Segment(int finger, int j) => (At(fingers[finger, j]), At(fingers[finger, j + 1]));

        // A ring (or flat band) following an ellipse round the hand, in the XZ plane
        public static Mesh Loop(float rx, float rz, float tube, float flat = 1f, int segments = 40, int sides = 8)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var c = new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
                var outward = new Vector3(Mathf.Cos(a) * rz, 0f, Mathf.Sin(a) * rx).normalized;
                for (int s = 0; s <= sides; s++)
                {
                    float b = s * Mathf.PI * 2f / sides;
                    var dir = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b) * flat;
                    v.Add(c + outward * Mathf.Cos(b) * tube + Vector3.up * Mathf.Sin(b) * tube * flat);
                    n.Add(dir.normalized);
                    uv.Add(new Vector2((float)i / segments, (float)s / sides));
                }
            }
            for (int i = 0; i < segments; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * (sides + 1) + s, b = a + sides + 1;
                    tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            var mesh = new Mesh { name = "Loop" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
