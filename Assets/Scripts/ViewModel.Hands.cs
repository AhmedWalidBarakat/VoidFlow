using UnityEngine;

namespace VoidFlow
{
    // Realistic arms in the style of the classic shooters: a bare forearm and a padded glove
    // with a strap round the wrist, all one skinned surface (baked from a CC0 rigged character,
    // see GrayboxBuilder.Arms) so the glove meets the arm in one smooth line. The fingers are
    // live bones: each arm blends toward the grip it's given and can loosen, re-grip or keep
    // just the index finger closed (a knife spinning on its ring) while the rest open.
    // Glove skins go on the glove ("GloveBlock", as the old block glove was named).
    //
    // Arm space: the fingers point along +Y (the forearm runs back along -Y), the back of the
    // hand faces +Z, and a held handle runs along X through GripFront.
    public partial class ViewModel
    {
        class BlockArm
        {
            public Transform root, grip;
            public ArmRig rig;
            public Transform[] bones;
            public ArmRig.Grip target, current;
            public float open, squeeze;  // 0..1: fingers loosened / tightened on the grip
            public bool keepIndex;       // while opening, the index finger stays closed
            public bool snapped;
        }

        public const string Fist = "Fist", Trigger = "Trigger", Support = "Support", Relaxed = "Relaxed";

        const float ArmLength = 0.4f;
        // The arm is drawn a bit under life size: at our wide field of view a full-size hand
        // fills the bottom of the screen. It shrinks toward the grip, so held things don't move.
        const float ArmScale = 0.85f;
        static readonly Vector3 GloveSize = new(0.076f, 0.082f, 0.071f); // the hand's rough extent
        // Where a held handle sits: in the closed fist
        static readonly Vector3 GripFront = new(0f, 0.052f, 0f);

        Material gloveRubber, gloveTrim, armSkin;

        BlockArm BuildBlockArm(Transform parent, string name, string grip = Fist)
        {
            gloveRubber ??= Make(new Color(0.03f, 0.03f, 0.035f), 0.7f, 0f);
            gloveTrim ??= Make(new Color(0.3f, 0.3f, 0.33f), 0.5f, 0.2f);
            armSkin ??= Make(new Color(0.66f, 0.47f, 0.35f), 0.32f, 0f); // tanned forearm

            var arm = new BlockArm { root = new GameObject(name).transform };
            arm.root.SetParent(parent, false);
            bool left = name.StartsWith("Left");
            var t = new GameObject("Visual").transform;
            t.SetParent(arm.root, false);
            t.localPosition = GripFront * (1f - ArmScale);
            t.localScale = Vector3.one * ArmScale;

            // The skeleton
            arm.rig = Resources.Load<ArmRig>($"Arms/{(left ? "Left" : "Right")}Arm");
            var rig = arm.rig;
            arm.bones = new Transform[rig.boneNames.Length];
            for (int i = 0; i < arm.bones.Length; i++)
            {
                var b = new GameObject(rig.boneNames[i]).transform;
                b.SetParent(rig.parents[i] < 0 ? t : arm.bones[rig.parents[i]], false);
                b.SetLocalPositionAndRotation(rig.bindPositions[i], rig.bindRotations[i]);
                arm.bones[i] = b;
            }
            // Forearm, glove and strap on the same bones
            Skinned(t, "ArmSkin", rig.skin, armSkin, arm);
            Skinned(t, "GloveBlock", rig.glove, glove, arm);
            Skinned(t, "GloveStrap", rig.strap, cuff, arm);

            // A plate over the knuckles, only there (glowing) on Void gloves
            var plate = Part(t, PrimitiveType.Cube, gloveRubber, new Vector3(left ? 0.004f : -0.004f, 0.018f, 0.032f), new Vector3(0.056f, 0.028f, 0.006f),
                Quaternion.Euler(-12f, 0f, 0f));
            plate.name = "GlovePlate";
            plate.gameObject.SetActive(false);
            arms.Add(arm);

            arm.target = arm.current = ArmRig.Named(grip);
            rig.Pose(arm.bones, arm.current);

            arm.grip = new GameObject("Grip").transform;
            arm.grip.SetParent(arm.root, false);
            return arm;
        }

        void Skinned(Transform parent, string name, Mesh mesh, Material mat, BlockArm arm)
        {
            var go = new GameObject(name);
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = mesh;
            r.bones = arm.bones;
            r.rootBone = arm.bones[0];
            r.sharedMaterial = mat;
            r.updateWhenOffscreen = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // Moves an arm's hand to another grip (the same hand goes from rifle grip to bolt)
        static void SetHandGrip(BlockArm arm, string grip) => arm.target = ArmRig.Named(grip);

        // Every frame: fingers ease toward their grip, loosened or squeezed as asked
        void UpdateFingers(float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 14f);
            foreach (var arm in arms)
            {
                if (arm.rig == null || arm.bones == null || !arm.root) continue;
                arm.current = ArmRig.Grip.Lerp(arm.current, arm.target, k);
                var g = arm.current.Open(arm.open, arm.keepIndex);
                if (arm.squeeze > 0f)
                {
                    g.fingers += Vector3.one * (6f * arm.squeeze);
                    g.index += Vector3.one * (4f * arm.squeeze);
                }
                arm.rig.Pose(arm.bones, g);
            }
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
        static readonly Vector3 TalonHandle = new(0f, -0.04f, 0f);

        static void SetTalonGrip(BlockArm arm) =>
            arm.grip.SetLocalPositionAndRotation(GripFront - TalonGrip * TalonHandle, TalonGrip);

        static void SetGrip(BlockArm arm, bool reverse, float roll = 0f)
        {
            Quaternion r = Quaternion.Euler((reverse ? 90f : 0f) + roll, 0f, 0f) * Quaternion.Euler(0f, 0f, -90f);
            arm.grip.SetLocalPositionAndRotation(GripFront - r * new Vector3(0f, -0.055f, 0f), r);
        }
    }
}
