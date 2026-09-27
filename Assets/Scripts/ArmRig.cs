using UnityEngine;

namespace VoidFlow
{
    // One arm for the view model: forearm, glove and strap as three skinned meshes that share
    // the same bones (so they meet with no seam), and the skeleton to pose them. Baked by
    // GrayboxBuilder.BakeArms from a CC0 rigged character, in arm space: the fingers point
    // along +Y, the back of the hand faces +Z, and a held handle runs along X through the
    // closed fist at ViewModel.GripFront.
    public class ArmRig : ScriptableObject
    {
        public Mesh skin, glove, strap;
        // Baked sport-glove and jacket textures (GrayboxBuilder.Gloves): colour with smoothness
        // in alpha, and normal maps; the glove's are laid out on its own side-on chart
        public Texture2D gloveAlbedo, gloveNormal, jacketAlbedo, jacketNormal, cuffNormal;
        public string[] boneNames;
        public int[] parents;                 // -1: the rig root
        public Vector3[] bindPositions;       // local to the parent
        public Quaternion[] bindRotations;
        // Finger joints (index, middle, ring, pinky: three each; thumb: three), as bone indices,
        // with the axis each one hinges about, in its own local space
        public int[] fingerJoints;
        public Vector3[] fingerAxes;
        public int[] thumbJoints;
        public Vector3[] thumbAxes;
        public Vector3 thumbSwingAxis;         // thumb_01's swing across the palm, in its local space

        // How far each finger is closed (degrees for joints 1, 2, 3; the thumb's .w swings it
        // across the palm)
        public struct Grip
        {
            public Vector3 index, fingers, thumb;
            public float across;

            public static Grip Lerp(Grip a, Grip b, float t) => new()
            {
                index = Vector3.Lerp(a.index, b.index, t),
                fingers = Vector3.Lerp(a.fingers, b.fingers, t),
                thumb = Vector3.Lerp(a.thumb, b.thumb, t),
                across = Mathf.Lerp(a.across, b.across, t),
            };

            public Grip Open(float amount, bool keepIndex = false) => new()
            {
                index = keepIndex ? index : index * (1f - amount),
                fingers = fingers * (1f - amount),
                thumb = thumb * (1f - amount),
                across = across * (1f - amount * 0.6f),
            };
        }

        public static readonly Grip Fist = new() { index = new(75f, 90f, 45f), fingers = new(80f, 95f, 50f), thumb = new(15f, 40f, 30f), across = 25f };
        public static readonly Grip Trigger = new() { index = new(20f, 55f, 30f), fingers = new(80f, 95f, 50f), thumb = new(15f, 40f, 30f), across = 25f };
        public static readonly Grip Support = new() { index = new(40f, 50f, 30f), fingers = new(45f, 55f, 35f), thumb = new(10f, 15f, 10f), across = 10f };
        public static readonly Grip Relaxed = new() { index = new(30f, 35f, 20f), fingers = new(40f, 45f, 25f), thumb = new(8f, 15f, 10f), across = 15f };

        // Puts the baked sport-glove look on a material: its colour and smoothness too, or (for a
        // skin's finish) just the stitching, padding and grain as relief
        public void DressGlove(Material m, bool colour)
        {
            if (!m) return;
            if (colour && gloveAlbedo)
            {
                m.SetTexture("_BaseMap", gloveAlbedo);
                m.SetTextureScale("_BaseMap", Vector2.one);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_SmoothnessTextureChannel", 1f);
                m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                m.SetFloat("_Smoothness", 1f);
            }
            if (gloveNormal)
            {
                m.SetTexture("_BumpMap", gloveNormal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
        }

        // The jacket's woven fabric (or the cuff's knit ribs) on a material
        public void DressJacket(Material m, bool cuff)
        {
            if (!m || !jacketAlbedo) return;
            m.SetTexture("_BaseMap", jacketAlbedo);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_SmoothnessTextureChannel", 1f);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            m.SetFloat("_Smoothness", 1f);
            var n = cuff ? cuffNormal : jacketNormal;
            if (n)
            {
                m.SetTexture("_BumpMap", n);
                m.SetTextureScale("_BaseMap", cuff ? new Vector2(1f, 1f) : Vector2.one);
                m.EnableKeyword("_NORMALMAP");
            }
        }

        public static Grip Named(string name) => name switch
        {
            "Trigger" => Trigger,
            "Support" => Support,
            "Relaxed" => Relaxed,
            _ => Fist,
        };

        // Poses the bones: every finger joint from its rest, turned about its hinge
        public void Pose(Transform[] bones, Grip g)
        {
            for (int f = 0; f < 4; f++)
            {
                Vector3 bend = f == 0 ? g.index : g.fingers;
                for (int j = 0; j < 3; j++)
                {
                    int k = f * 3 + j, b = fingerJoints[k];
                    if (b < 0) continue;
                    bones[b].localRotation = bindRotations[b] * Quaternion.AngleAxis(bend[j], fingerAxes[k]);
                }
            }
            for (int j = 0; j < 3; j++)
            {
                int b = thumbJoints[j];
                if (b < 0) continue;
                Quaternion r = bindRotations[b];
                if (j == 0) r *= Quaternion.AngleAxis(g.across, thumbSwingAxis);
                bones[b].localRotation = r * Quaternion.AngleAxis(g.thumb[j], thumbAxes[j]);
            }
        }
    }
}
