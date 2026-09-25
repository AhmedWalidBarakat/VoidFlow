using UnityEngine;

namespace VoidFlow
{
    // CS2-style gloved hands for the knives: a rounded palm, three-jointed fingers and a
    // two-jointed thumb that curl into poses, a padded back panel, a knuckle guard and finger
    // pads, the wrist cuff with its strap, and a bare forearm.
    //
    // Hand space: wrist at the origin, fingers along +Y, back of the hand toward +Z, thumb
    // toward +X. The left hand is the same hand mirrored.
    public partial class ViewModel
    {
        class GloveHand
        {
            public Transform root, grip;
            public readonly Transform[,] joints = new Transform[4, 3];
            public readonly Transform[] thumb = new Transform[2];
        }

        // Index, middle, ring, pinky
        static readonly float[] FingerX = { 0.028f, 0.0095f, -0.009f, -0.0265f };
        static readonly float[] FingerBase = { 0.084f, 0.087f, 0.085f, 0.079f };
        static readonly float[,] FingerLength = { { 0.04f, 0.025f, 0.021f }, { 0.045f, 0.028f, 0.022f }, { 0.042f, 0.026f, 0.021f }, { 0.033f, 0.02f, 0.018f } };
        static readonly float[] FingerWidth = { 0.019f, 0.0195f, 0.0185f, 0.0165f };
        static readonly float[] ThumbLength = { 0.036f, 0.029f };

        // Back from the wrist (-Y), down from the back of the hand (-Z), out from the thumb (-X)
        static readonly Vector3 ForearmDirection = new(-0.8f, -1f, -0.9f);

        // Viewmodel hands read bigger than life, like in CS
        const float HandScale = 1.25f;

        // Where a held handle sits: across the palm, inside the curled fingers
        static readonly Vector3 GripPoint = new(0f, 0.068f, -0.03f);

        Material gloveRubber, glovePanel, gloveTrim;

        GloveHand BuildGloveHand(Transform parent, string name, bool left)
        {
            gloveRubber ??= Make(new Color(0.025f, 0.025f, 0.028f), 0.75f, 0f);
            glovePanel ??= Make(new Color(0.13f, 0.13f, 0.14f), 0.4f, 0f);
            gloveTrim ??= Make(new Color(0.32f, 0.32f, 0.34f), 0.5f, 0.2f);

            var h = new GloveHand();
            h.root = new GameObject(name).transform;
            h.root.SetParent(parent, false);
            h.root.localScale = Vector3.one * HandScale;
            var t = new GameObject("Body").transform;
            t.SetParent(h.root, false);
            if (left) t.localScale = new Vector3(-1f, 1f, 1f);

            // Palm, padded back panel, knuckle guard
            Part(t, PrimitiveType.Sphere, glove, new Vector3(0f, 0.047f, -0.002f), new Vector3(0.082f, 0.098f, 0.032f));
            Part(t, PrimitiveType.Sphere, glovePanel, new Vector3(0f, 0.05f, 0.008f), new Vector3(0.07f, 0.075f, 0.02f));
            Part(t, PrimitiveType.Capsule, gloveRubber, new Vector3(0f, 0.079f, 0.011f), new Vector3(0.017f, 0.038f, 0.012f), Quaternion.Euler(0f, 0f, 90f));

            // Fingers: three joints each, a rubber pad on the first segment
            for (int f = 0; f < 4; f++)
            {
                Transform joint = t;
                float w = FingerWidth[f];
                for (int j = 0; j < 3; j++)
                {
                    var next = new GameObject($"Finger {f}.{j}").transform;
                    next.SetParent(joint, false);
                    next.localPosition = j == 0 ? new Vector3(FingerX[f], FingerBase[f], 0f) : new Vector3(0f, FingerLength[f, j - 1], 0f);
                    float len = FingerLength[f, j];
                    Part(next, PrimitiveType.Capsule, glove, new Vector3(0f, len * 0.5f, 0f), new Vector3(w, (len + w) * 0.5f, w * 0.92f));
                    if (j == 0) Part(next, PrimitiveType.Cube, gloveRubber, new Vector3(0f, len * 0.5f, w * 0.42f), new Vector3(w * 0.8f, len * 0.6f, 0.004f));
                    h.joints[f, j] = next;
                    joint = next;
                    w *= 0.92f;
                }
            }

            // Thumb: two joints from the heel of the palm
            Transform tj = t;
            for (int j = 0; j < 2; j++)
            {
                var next = new GameObject($"Thumb {j}").transform;
                next.SetParent(tj, false);
                next.localPosition = j == 0 ? new Vector3(0.031f, 0.022f, -0.01f) : new Vector3(0f, ThumbLength[0], 0f);
                Part(next, PrimitiveType.Capsule, glove, new Vector3(0f, ThumbLength[j] * 0.5f, 0f), new Vector3(0.021f, (ThumbLength[j] + 0.021f) * 0.5f, 0.019f));
                h.thumb[j] = next;
                tj = next;
            }

            // Cuff with its strap, then the bare forearm
            Part(t, PrimitiveType.Cylinder, cuff, new Vector3(0f, -0.006f, 0f), new Vector3(0.07f, 0.02f, 0.052f));
            Part(t, PrimitiveType.Cube, gloveTrim, new Vector3(0f, -0.004f, 0.027f), new Vector3(0.05f, 0.016f, 0.004f));
            // The wrist bends: the forearm heads back, down and outward to the screen corner
            Vector3 arm = ForearmDirection.normalized;
            Part(t, PrimitiveType.Cylinder, sleeve, new Vector3(0f, -0.012f, -0.004f) + arm * 0.16f, new Vector3(0.048f, 0.16f, 0.042f),
                Quaternion.FromToRotation(Vector3.up, arm));

            h.grip = new GameObject("Grip").transform;
            h.grip.SetParent(t, false);
            return h;
        }

        // curl: 0 open to 1 fist; thumbWrap: 0 relaxed to 1 wrapped over the fingers;
        // spread fans the fingers apart
        static void PoseHand(GloveHand h, float curl, float thumbWrap, float spread)
        {
            for (int f = 0; f < 4; f++)
            {
                float splay = (1.5f - f) * 4f * spread;
                h.joints[f, 0].localRotation = Quaternion.Euler(-curl * 88f, 0f, splay);
                h.joints[f, 1].localRotation = Quaternion.Euler(-curl * 100f, 0f, 0f);
                h.joints[f, 2].localRotation = Quaternion.Euler(-curl * 70f, 0f, 0f);
            }
            h.thumb[0].localRotation = Quaternion.Slerp(Quaternion.Euler(-25f, 0f, -45f), Quaternion.Euler(-55f, 35f, 20f), thumbWrap);
            h.thumb[1].localRotation = Quaternion.Euler(-10f - thumbWrap * 45f, 0f, thumbWrap * 25f);
        }

        // A rotation from where the fingers point (hand +Y) and where the back of the hand
        // faces (hand +Z), in camera space
        static Quaternion FingersBack(Vector3 fingers, Vector3 back)
        {
            fingers.Normalize();
            back = (back - Vector3.Dot(back, fingers) * fingers).normalized;
            return Quaternion.LookRotation(back, fingers);
        }

        // Put a knife in the right hand: its handle (knife space y -0.11 to 0) across the palm.
        // Forward grip: blade out of the thumb side. Reverse grip (karambit): blade out of the
        // pinky side, curling toward the back of the hand. `roll` turns the knife about its
        // handle, the way fingers roll it during an inspect.
        static void SetGrip(GloveHand h, bool reverse, float roll = 0f)
        {
            Quaternion r = Quaternion.Euler((reverse ? 90f : 0f) + roll, 0f, 0f) * Quaternion.Euler(0f, 0f, -90f);
            h.grip.SetLocalPositionAndRotation(GripPoint - r * new Vector3(0f, -0.055f, 0f), r);
        }
    }
}
