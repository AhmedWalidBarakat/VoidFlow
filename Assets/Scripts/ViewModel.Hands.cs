using System.Collections.Generic;
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

        Material gloveRubber, gloveTrim, armSkin, jacketCuff;

        BlockArm BuildBlockArm(Transform parent, string name, string grip = Fist)
        {
            gloveRubber ??= Make(new Color(0.03f, 0.03f, 0.035f), 0.7f, 0f);
            gloveTrim ??= Make(new Color(0.3f, 0.3f, 0.33f), 0.5f, 0.2f);
            if (!armSkin)
            {
                // A black jacket sleeve over the forearm (goes with every glove)
                armSkin = Make(Color.white, 0.32f, 0f);
                armSkin.SetTexture("_BaseMap", JacketTexture());
                armSkin.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
                Resources.Load<ArmRig>("Arms/RightArm")?.DressJacket(armSkin, false); // the baked woven fabric
                jacketCuff = Make(Color.white, 0.3f, 0f);
                Resources.Load<ArmRig>("Arms/RightArm")?.DressJacket(jacketCuff, true);
                jacketCuff.SetTextureScale("_BumpMap", Vector2.one);
            }

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
            Skinned(t, "ArmSkin", Jacket(rig), armSkin, arm);
            // The sleeve's hem: a snug band of the same fabric over the end of the sleeve and the
            // top of the glove's wrist, closing the opening between them
            var (cuffAt, crx, crz) = SleeveEnd(rig.skin);
            var ring = new GameObject("JacketCuff");
            ring.layer = Layer;
            ring.transform.SetParent(t, false);
            ring.transform.localPosition = cuffAt + Vector3.down * 0.006f;
            ring.AddComponent<MeshFilter>().sharedMesh = HandFit.Loop(crx + 0.0068f, crz + 0.0068f, 0.0042f, 3.6f, 48, 12);
            var cr = ring.AddComponent<MeshRenderer>();
            cr.sharedMaterial = armSkin;
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

        // The forearm made into a jacket sleeve: puffed out a little all round, and more at the
        // cuff where it meets the glove, so the glove disappears into it. Its far end (the
        // baked arm stops short of the elbow, rounded off) is drawn out into a long sleeve
        // widening toward the elbow, so it runs on off the bottom of the screen like a real
        // arm's and is never seen to end
        static readonly Dictionary<ArmRig, Mesh> jackets = new();
        static Mesh Jacket(ArmRig rig)
        {
            if (jackets.TryGetValue(rig, out var mesh) && mesh) return mesh;
            mesh = Object.Instantiate(rig.skin);
            mesh.name = "Jacket";
            var v = mesh.vertices;
            var n = mesh.normals;
            float top = float.MinValue, bottom = float.MaxValue;
            foreach (var p in v) { top = Mathf.Max(top, p.y); bottom = Mathf.Min(bottom, p.y); }
            const float Tail = 0.12f, Reach = 0.3f;
            for (int i = 0; i < v.Length; i++)
            {
                float cuff = Mathf.Clamp01(1f - (top - v[i].y) / 0.03f);
                float back = Mathf.Clamp01(1f - (v[i].y - bottom) / Tail); // 1 at the far end
                v[i] += n[i] * (0.006f + 0.005f * cuff + 0.004f * back);
                v[i].y -= Reach * back * back * back;
            }
            mesh.vertices = v;
            mesh.RecalculateBounds();
            return jackets[rig] = mesh;
        }

        // Where the bare forearm ends at the glove: the middle of that opening and its half-widths
        static (Vector3 at, float rx, float rz) SleeveEnd(Mesh skin)
        {
            var v = skin.vertices;
            float top = float.MinValue;
            foreach (var p in v) top = Mathf.Max(top, p.y);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var p in v)
            {
                if (p.y < top - 0.004f) continue;
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            return (new Vector3((x0 + x1) * 0.5f, top - 0.002f, (z0 + z1) * 0.5f), (x1 - x0) * 0.5f, (z1 - z0) * 0.5f);
        }

        // Black ripstop jacket fabric: a fine grid of heavier threads, soft creases, a faint sheen
        static Texture2D jacketTexture;
        static Texture2D JacketTexture()
        {
            if (jacketTexture) return jacketTexture;
            const int size = 256;
            jacketTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Jacket", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, w = (float)y / size;
                float crease = Mathf.PerlinNoise(u * 4f + 3.1f, w * 12f) * 0.5f + Mathf.PerlinNoise(u * 16f, w * 16f) * 0.2f;
                bool rip = x % 16 == 0 || y % 16 == 0;
                float weave = (x + y) % 2 == 0 ? 0.01f : 0f;
                float g = 0.055f + crease * 0.045f + weave + (rip ? 0.03f : 0f);
                jacketTexture.SetPixel(x, y, new Color(g, g, g * 1.08f));
            }
            jacketTexture.Apply();
            return jacketTexture;
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
        static readonly Quaternion TalonGrip = Quaternion.LookRotation(Vector3.forward, Vector3.right);
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
