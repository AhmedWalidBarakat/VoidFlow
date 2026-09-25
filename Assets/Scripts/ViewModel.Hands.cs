using UnityEngine;

namespace VoidFlow
{
    // Chunky block arms, Roblox-shooter style: a long fabric sleeve ending in a bigger glove
    // block with a cuff band, a trim stripe and a knuckle plate. No fingers: a held weapon's
    // handle rests against the front edge of the glove block.
    //
    // Arm space: the glove block is centered on the origin, the arm points along +Y (the
    // sleeve runs back along -Y), the back of the hand faces +Z.
    public partial class ViewModel
    {
        class BlockArm
        {
            public Transform root, grip;
        }

        const float ArmLength = 0.4f;
        static readonly Vector3 GloveSize = new(0.076f, 0.082f, 0.071f);
        // Where a held handle sits: just in front of the glove's front face
        static readonly Vector3 GripFront = new(0f, GloveSize.y * 0.5f + 0.011f, 0f);

        Material gloveRubber, gloveTrim;

        BlockArm BuildBlockArm(Transform parent, string name)
        {
            gloveRubber ??= Make(new Color(0.03f, 0.03f, 0.035f), 0.7f, 0f);
            gloveTrim ??= Make(new Color(0.3f, 0.3f, 0.33f), 0.5f, 0.2f);

            var arm = new BlockArm { root = new GameObject(name).transform };
            var t = arm.root;
            t.SetParent(parent, false);
            Part(t, PrimitiveType.Cube, glove, Vector3.zero, GloveSize, Quaternion.identity).name = "GloveBlock";
            Part(t, PrimitiveType.Cube, gloveRubber, new Vector3(0f, 0.016f, GloveSize.z * 0.5f + 0.002f), new Vector3(0.06f, 0.036f, 0.006f), Quaternion.identity).name = "GlovePlate";
            arms.Add(arm);
            Part(t, PrimitiveType.Cube, gloveTrim, new Vector3(0f, -0.019f, 0f), new Vector3(GloveSize.x + 0.003f, 0.007f, GloveSize.z + 0.003f));
            Part(t, PrimitiveType.Cube, cuff, new Vector3(0f, -0.05f, 0f), new Vector3(0.083f, 0.021f, 0.078f));
            Part(t, PrimitiveType.Cube, sleeve, new Vector3(0f, -0.06f - ArmLength * 0.5f, 0f), new Vector3(0.069f, ArmLength, 0.064f));
            arm.grip = new GameObject("Grip").transform;
            arm.grip.SetParent(t, false);
            return arm;
        }

        // A rotation from where the arm points (arm +Y) and where the back of the hand faces
        // (arm +Z), in the parent's space
        static Quaternion FingersBack(Vector3 fingers, Vector3 back)
        {
            fingers.Normalize();
            back = (back - Vector3.Dot(back, fingers) * fingers).normalized;
            return Quaternion.LookRotation(back, fingers);
        }

        // Put a knife in the glove: its handle (knife space y -0.11 to 0) runs across the front
        // edge of the block.
        // Forward grip: blade out of the thumb side (+X). Reverse grip (talon knife): blade out of
        // the other side, curling toward the back of the hand. `roll` turns the knife about its
        // handle, the way fingers roll it during an inspect.
        static void SetGrip(BlockArm arm, bool reverse, float roll = 0f)
        {
            Quaternion r = Quaternion.Euler((reverse ? 90f : 0f) + roll, 0f, 0f) * Quaternion.Euler(0f, 0f, -90f);
            arm.grip.SetLocalPositionAndRotation(GripFront - r * new Vector3(0f, -0.055f, 0f), r);
        }
    }
}
