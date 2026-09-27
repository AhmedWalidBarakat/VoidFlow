using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Realistic gloved arms, in the style of the classic shooters: a sculpted hand (baked from a
    // CC0 rigged character, see GrayboxBuilder.Arms) with its fingers closed round whatever it
    // holds, a padded cuff strapped round the wrist, a plate over the knuckles and a fabric
    // sleeve running back out of view. Glove skins go on the hand (named "GloveBlock", as the
    // old block glove was, so the skin and squeeze code finds it).
    //
    // Arm space: the hand's centre is near the origin, the fingers point along +Y (the sleeve
    // runs back along -Y), the back of the hand faces +Z, and a held handle runs along X
    // through GripFront.
    public partial class ViewModel
    {
        class BlockArm
        {
            public Transform root, grip;
        }

        // Grips the hand can be baked in (Resources/Arms/<Side><Grip>)
        public const string Fist = "Fist", Trigger = "Trigger", Support = "Support", Relaxed = "Relaxed";

        const float ArmLength = 0.4f;
        // The arm is drawn a bit under life size: at our wide field of view a full-size hand
        // fills the bottom of the screen. It shrinks toward the grip, so held things don't move.
        const float ArmScale = 0.8f;
        static readonly Vector3 GloveSize = new(0.076f, 0.082f, 0.071f); // the hand's rough extent
        // Where a held handle sits: in the closed fist
        static readonly Vector3 GripFront = new(0f, 0.052f, 0f);

        Material gloveRubber, gloveTrim;
        static Mesh sleeveMesh, cuffMesh, rollMesh;

        BlockArm BuildBlockArm(Transform parent, string name, string grip = Fist)
        {
            gloveRubber ??= Make(new Color(0.03f, 0.03f, 0.035f), 0.7f, 0f);
            gloveTrim ??= Make(new Color(0.3f, 0.3f, 0.33f), 0.5f, 0.2f);

            var arm = new BlockArm { root = new GameObject(name).transform };
            arm.root.SetParent(parent, false);
            bool left = name.StartsWith("Left");
            var t = new GameObject("Visual").transform;
            t.SetParent(arm.root, false);
            t.localPosition = GripFront * (1f - ArmScale);
            t.localScale = Vector3.one * ArmScale;

            // The hand
            var hand = Resources.Load<Mesh>($"Arms/{(left ? "Left" : "Right")}{grip}");
            var handGo = new GameObject("GloveBlock");
            handGo.layer = Layer;
            handGo.transform.SetParent(t, false);
            handGo.AddComponent<MeshFilter>().sharedMesh = hand;
            var hr = handGo.AddComponent<MeshRenderer>();
            hr.sharedMaterial = glove;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arms.Add(arm);

            // A padded plate over the knuckles (glows on Void gloves)
            Part(t, PrimitiveType.Cube, gloveRubber, new Vector3(left ? 0.004f : -0.004f, 0.018f, 0.031f), new Vector3(0.058f, 0.03f, 0.008f),
                Quaternion.Euler(-12f, 0f, 0f)).name = "GlovePlate";

            // Wrist: the glove's cuff with a strap across it, then the sleeve's rolled end
            cuffMesh ??= Lathe(new[] { (-0.1f, 0.036f), (-0.098f, 0.039f), (-0.064f, 0.039f), (-0.062f, 0.036f) }, 0.82f, 20, "Cuff");
            rollMesh ??= Lathe(new[] { (-0.132f, 0.043f), (-0.126f, 0.049f), (-0.106f, 0.050f), (-0.098f, 0.044f), (-0.1f, 0.04f) }, 0.85f, 20, "Roll");
            sleeveMesh ??= Lathe(new[] { (-0.7f, 0.056f), (-0.3f, 0.05f), (-0.16f, 0.047f), (-0.128f, 0.045f) }, 0.86f, 20, "Sleeve");
            MeshPart(t, "Cuff", cuffMesh, cuff);
            Part(t, PrimitiveType.Cube, gloveTrim, new Vector3(0f, -0.081f, 0.0325f), new Vector3(0.05f, 0.016f, 0.006f), Quaternion.identity).name = "Strap";
            MeshPart(t, "SleeveRoll", rollMesh, sleeve);
            MeshPart(t, "Sleeve", sleeveMesh, sleeve);

            arm.grip = new GameObject("Grip").transform;
            arm.grip.SetParent(arm.root, false);
            return arm;
        }

        // Swaps an arm's hand to another grip (the same arm moves from knife to rifle to bolt)
        static void SetHandGrip(BlockArm arm, string grip)
        {
            var hand = arm.root.Find("Visual/GloveBlock");
            if (!hand) return;
            bool left = arm.root.name.StartsWith("Left");
            var mesh = Resources.Load<Mesh>($"Arms/{(left ? "Left" : "Right")}{grip}");
            if (mesh) hand.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void MeshPart(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // A tube turned round the arm's Y axis through the given (y, radius) profile, squashed
        // front to back (the arm is flatter than it is wide). UVs wrap twice round, 10cm tall.
        static Mesh Lathe((float y, float r)[] profile, float depth, int sides, string name)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            for (int p = 0; p < profile.Length; p++)
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * profile[p].r, profile[p].y, Mathf.Sin(a) * profile[p].r * depth));
                    uv.Add(new Vector2(2f * s / sides, profile[p].y / 0.1f));
                }
            int row = sides + 1;
            for (int p = 0; p + 1 < profile.Length; p++)
                for (int s = 0; s < sides; s++)
                {
                    int a = p * row + s, b = a + 1, c = a + row, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            // Outward normals whichever way the profile runs
            var n = m.normals;
            for (int i = 0; i < n.Length; i++)
                if (Vector3.Dot(n[i], new Vector3(v[i].x, 0f, v[i].z)) < 0f) n[i] = -n[i];
            m.normals = n;
            m.RecalculateBounds();
            return m;
        }

        // A rotation from where the arm points (arm +Y) and where the back of the hand faces
        // (arm +Z), in the parent's space
        static Quaternion FingersBack(Vector3 fingers, Vector3 back)
        {
            fingers.Normalize();
            back = (back - Vector3.Dot(back, fingers) * fingers).normalized;
            return Quaternion.LookRotation(back, fingers);
        }

        // Put a knife in the fist: its handle (knife space y -0.11 to 0) runs through the closed
        // fingers.
        // Forward grip: blade out of the thumb side (+X). Reverse grip (talon knife): blade out of
        // the other side, curling toward the back of the hand. `roll` turns the knife about its
        // handle, the way fingers roll it during an inspect.
        // The talon knife's own hold: the handle runs across the fist, the finger ring out one
        // side and the hooked blade sweeping the other way, its flat side facing away from the
        // back of the hand (so it shows to you when the palm does)
        static readonly Quaternion TalonGrip = Quaternion.LookRotation(Vector3.back, Vector3.right);
        static readonly Vector3 TalonHandle = new(0f, -0.055f, 0f);

        static void SetTalonGrip(BlockArm arm) =>
            arm.grip.SetLocalPositionAndRotation(GripFront - TalonGrip * TalonHandle, TalonGrip);

        static void SetGrip(BlockArm arm, bool reverse, float roll = 0f)
        {
            Quaternion r = Quaternion.Euler((reverse ? 90f : 0f) + roll, 0f, 0f) * Quaternion.Euler(0f, 0f, -90f);
            arm.grip.SetLocalPositionAndRotation(GripFront - r * new Vector3(0f, -0.055f, 0f), r);
        }
    }
}
