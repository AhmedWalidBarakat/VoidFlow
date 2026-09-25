using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace VoidFlow
{
    // Knife handling: the resting hold, slashes, and each knife's own inspect, all in full 3D.
    // Whenever a move could pass through the right arm, that arm fades to see-through so the
    // knife always reads cleanly.
    //
    //  - Talon knife: hold F and it spins face-on around the index finger through its ring;
    //    let go and it finishes the turn and settles.
    //  - Butterfly knife: Counter Blox style flips standing up on the fist, the wrist rolling
    //    between flips so each is seen from a new angle.
    //  - Void swords: hold F to slide the sword back into the hip sheath, draw it out again and
    //    point it onward: arm and blade in one line toward the crosshair, held until you let go.
    public partial class ViewModel
    {
        [Tooltip("A transparent URP Lit material; see-through copies of the arm are made from it")]
        public Material fadeTemplate;

        // Resting poses in camera space: two block arms coming in from the bottom corners, the
        // left glove empty, the right one holding the knife (a talon knife comes out the far
        // side and curls up to the right)
        static readonly Vector3 RightIdle = new(0.11f, -0.1f, 0.3f);
        static readonly Vector3 LeftIdle = new(-0.125f, -0.105f, 0.31f);
        static readonly Quaternion ReverseIdle = FingersBack(new Vector3(-0.3f, 0.5f, 1f), new Vector3(0.1f, 0.6f, -0.7f));
        static readonly Quaternion ForwardIdle = FingersBack(new Vector3(-0.3f, 0.5f, 1f), new Vector3(0.6f, 0.5f, -0.6f));
        static readonly Quaternion LeftIdleRotation = FingersBack(new Vector3(0.3f, 0.5f, 1f), new Vector3(-0.1f, 0.6f, -0.7f));
        static readonly Vector3 LeftHandAway = new(-0.04f, -0.14f, -0.05f);

        // Talon spin: fist raised upright with the index finger (arm +X) pointing straight back
        // at you, so the knife spins face-on around it. The ring sits on the finger at the top
        // of the glove.
        static readonly Vector3 TalonSpot = new(0.06f, -0.07f, 0.37f);
        static readonly Quaternion TalonRotation = FromXY(new Vector3(-0.08f, 0.05f, -1f), new Vector3(0.05f, 1f, 0f));
        static readonly Vector3 FingerPoint = new(0f, 0.038f, 0f);

        // Butterfly flips, Counter Blox style: the arm reaches in from the right toward the
        // left, back of the hand up, and the knife stands on top of the fist facing you (its
        // held handle down through the fist). The wrist rolls between flips so each one is
        // seen from a new angle.
        static readonly Vector3 ButterflySpot = new(0.07f, -0.1f, 0.36f);
        static readonly Quaternion ButterflyRotation = FingersBack(new Vector3(-0.75f, 0.3f, 0.6f), Vector3.up);
        static readonly Vector3 HingePoint = new(0f, 0f, GloveSize.z * 0.5f + 0.035f);

        // Sword point: the arm reaches out toward the crosshair and the sword carries straight
        // on out of the front of the glove, pointing onward
        static readonly Vector3 PointSpot = new(0.14f, -0.13f, 0.25f);
        static readonly Quaternion PointRotation = FingersBack(new Vector3(-0.32f, 0.26f, 0.75f), Vector3.up);
        static readonly Vector3 PointGripSpot = new(0f, GloveSize.y * 0.5f + 0.11f, 0f);

        // A rotation from where arm +X and arm +Y point
        static Quaternion FromXY(Vector3 x, Vector3 y)
        {
            x.Normalize();
            y = (y - Vector3.Dot(y, x) * x).normalized;
            return Quaternion.LookRotation(Vector3.Cross(x, y), y);
        }

        static float Ease(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        // Rises from a to b, holds until c, falls back by d
        static float Plateau(float t, float a, float b, float c, float d) =>
            t < c ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t)) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c, d, t));

        // ------------------------------------------------------------------ update

        void UpdateKnife(Keyboard kb, Mouse mouse, bool locked, bool ready, float dt)
        {
            bool held = kb != null && kb.fKey.isPressed;
            if (kb != null && kb.fKey.wasPressedThisFrame && inspectTime < 0f && ready) StartInspect();
            if (ready && locked && mouse != null && mouse.leftButton.wasPressedThisFrame && (slashTime < 0f || slashTime > 0.3f))
            {
                slashTime = 0f;
                slashSide = -slashSide;
                StopInspect();
                Play(WeaponSounds.Slash, 0.7f);
            }

            var (pos, rot) = (Vector3.zero, Vector3.zero);
            if (slashTime >= 0f)
            {
                slashTime += dt;
                (pos, rot) = Sample(SlashKeys, slashTime);
                // Every other swing mirrors, so cuts alternate right-to-left and left-to-right;
                // swords swing wider
                float reach = knife.IsSword ? 1.3f : 1f;
                pos = new Vector3(pos.x * slashSide, pos.y, pos.z) * reach;
                rot = new Vector3(rot.x, rot.y * slashSide, rot.z * slashSide) * reach;
                if (slashTime > SlashKeys[^1].t) slashTime = -1f;
            }
            else if (inspectTime >= 0f)
            {
                inspectTime += dt;
                if (knife.model == KnifeModel.Talon) UpdateTalonSpin(held, dt);
                else if (knife.IsSword) UpdateSwordPoint(held, dt);
                else if (inspectTime > knife.InspectLength) StopInspect();
            }
            PoseKnife(pos, rot, inspectTime);
        }

        void StartInspect()
        {
            inspectTime = 0f;
            talonSpin = 0f;
            talonLower = swordRelease = -1f;
        }

        void StopInspect()
        {
            inspectTime = -1f;
            talonSpin = 0f;
            talonLower = swordRelease = -1f;
        }

        // Talon: spin up quickly, keep spinning while F is held; on release finish the turn
        // it's on (at least one full turn), then lower
        const float TalonSpinSpeed = 1080f, TalonSpinStart = 0.35f, TalonLowerTime = 0.35f;
        float talonSpin, talonLower = -1f;

        void UpdateTalonSpin(bool held, float dt)
        {
            if (talonLower >= 0f)
            {
                talonLower += dt;
                if (talonLower > TalonLowerTime) StopInspect();
                return;
            }
            if (inspectTime < TalonSpinStart) return;
            float speed = TalonSpinSpeed * Mathf.Clamp01(0.3f + talonSpin / 180f);
            float stopAt = held ? float.MaxValue : Mathf.Max(360f, Mathf.Ceil(talonSpin / 360f) * 360f);
            talonSpin = Mathf.Min(talonSpin + speed * dt, stopAt);
            if (talonSpin >= stopAt) talonLower = 0f;
        }

        // Sword: sheathe, draw, point; hold the point while F is held, then return to ready
        const float SwordSheathe = 0.3f, SwordRegrab = 0.42f, SwordOut = 0.68f, SwordPointed = 0.95f, SwordReturn = 0.4f;
        float swordRelease = -1f;
        bool swordSounded;

        void UpdateSwordPoint(bool held, float dt)
        {
            if (swordRelease >= 0f)
            {
                swordRelease += dt;
                if (swordRelease > SwordReturn) StopInspect();
            }
            else if (!held && inspectTime >= SwordPointed) swordRelease = 0f;
            if (inspectTime < SwordRegrab) swordSounded = false;
            else if (!swordSounded)
            {
                swordSounded = true;
                Play(WeaponSounds.Unsheathe, 0.8f);
            }
        }

        // ------------------------------------------------------------------ posing

        // Places the hands for the current knife: the resting hold plus the slash offset
        // (camera space pos, rot), blended into the inspect pose while inspecting (inspect >= 0)
        void PoseKnife(Vector3 pos, Vector3 rot, float inspect)
        {
            bool reverse = knife.model == KnifeModel.Talon;
            SetGrip(rightHand, reverse);
            float time = Application.isPlaying ? Time.time : 0f;
            if (PoseSwordDraw())
            {
                SetArmAlpha(0.35f);
                knife.Animate(time, -1f);
                return;
            }
            if (inspect >= 0f && knife.IsSword)
            {
                PoseSwordInspect(inspect);
                knife.Animate(time, inspect);
                return;
            }
            leftHand.root.gameObject.SetActive(true);

            // Talon and butterfly: raise the fist and turn it; the knife moves onto the finger
            float w = inspect < 0f ? 0f
                : reverse ? Ease(inspect, 0f, 0.3f) * (talonLower >= 0f ? 1f - Ease(talonLower, 0f, TalonLowerTime) : 1f)
                : Plateau(inspect, 0f, 0.3f, knife.InspectLength - 0.35f, knife.InspectLength);
            Quaternion show = (reverse ? TalonRotation : ButterflyRotation) * TrickMove(inspect);
            hand.localPosition = Vector3.Lerp(RightIdle, reverse ? TalonSpot : ButterflySpot, w) + pos + TrickBob(inspect);
            hand.localRotation = Quaternion.Euler(rot) * Quaternion.Slerp(reverse ? ReverseIdle : ForwardIdle, show, w);
            if (w > 0f)
            {
                var (fingerPos, fingerRot) = FingerGrip();
                rightHand.grip.SetLocalPositionAndRotation(
                    Vector3.Lerp(rightHand.grip.localPosition, fingerPos, w), Quaternion.Slerp(rightHand.grip.localRotation, fingerRot, w));
            }
            SetArmAlpha(Mathf.Lerp(1f, 0.3f, w));

            // The left arm dips out of the way of inspects and of cuts that cross the body
            float slashAway = slashTime >= 0f && slashSide < 0f ? Plateau(slashTime, 0f, 0.1f, 0.3f, 0.45f) : 0f;
            leftHand.root.localPosition = LeftIdle + LeftHandAway * Mathf.Max(w, slashAway);
            knife.ringSpin = talonSpin;
            knife.Animate(time, inspect, current == KnifeSlot ? drawTime : 99f);
        }

        // Where the knife sits for a trick. Talon: on the index finger at the top of the glove,
        // its blade plane square to the finger, pivoting on the ring. Butterfly: standing up
        // out of the back of the fist (knife up = arm +Z, facing along the finger), pivoting
        // on the hinge pin.
        (Vector3, Quaternion) FingerGrip()
        {
            if (knife.model == KnifeModel.Talon)
            {
                Quaternion r = Quaternion.Euler(0f, 90f, 0f); // knife Z runs along the finger (arm +X)
                return (FingerPoint - r * knife.ringCenter, r);
            }
            return (HingePoint, Quaternion.LookRotation(Vector3.right, Vector3.forward));
        }

        // Small wrist motion on top of the trick pose
        Quaternion TrickMove(float t)
        {
            if (t < 0f) return Quaternion.identity;
            if (knife.model == KnifeModel.Talon)
                return Quaternion.Euler(0f, Mathf.Sin(talonSpin * Mathf.Deg2Rad) * 3f, 0f);
            // Butterfly: the wrist rolls around the forearm between flips (one way, then back)
            float flips = Plateau(t, 0.3f, 0.45f, 2f, 2.2f);
            float roll = Mathf.Sin((t - 0.35f) * Mathf.PI / (2f * WeaponParts.FlipPeriod)) * 35f;
            return Quaternion.Euler(0f, roll * flips, 0f);
        }

        // The butterfly arm sways side to side and dips with each flip while it shows off
        Vector3 TrickBob(float t)
        {
            if (t < 0f || knife.model != KnifeModel.Butterfly) return Vector3.zero;
            float active = Plateau(t, 0.3f, 0.45f, 2f, 2.2f);
            float beat = (t - 0.35f) * Mathf.PI / WeaponParts.FlipPeriod;
            return new Vector3(Mathf.Sin(beat * 0.5f) * 0.02f, -Mathf.Abs(Mathf.Sin(beat)) * 0.01f, 0f) * active;
        }

        // Sword inspect: to the hip, into the sheath, out again, and point
        void PoseSwordInspect(float t)
        {
            leftHand.root.gameObject.SetActive(false);
            var (atHilt, hiltPosition, pulled) = HiltPose();
            Vector3 breathe = new(0f, Mathf.Sin(t * 2.2f) * 0.003f, 0f);
            Vector3 p;
            Quaternion r;
            bool inSheath = false;
            if (t < SwordSheathe)
            {
                float a = Ease(t, 0f, SwordSheathe);
                p = Vector3.Lerp(RightIdle, hiltPosition, a);
                r = Quaternion.Slerp(ForwardIdle, atHilt, a);
                // The blade slides home along the sheath over the last part
                inSheath = t > SwordSheathe * 0.85f;
            }
            else if (t < SwordRegrab)
            {
                p = hiltPosition;
                r = atHilt;
                inSheath = true;
            }
            else if (t < SwordOut)
            {
                p = Vector3.Lerp(hiltPosition, pulled, Ease(t, SwordRegrab, SwordOut));
                r = atHilt;
            }
            else
            {
                float a = Ease(t, SwordOut, SwordPointed);
                p = Vector3.Lerp(pulled, PointSpot, a) + Vector3.up * (Mathf.Sin(a * Mathf.PI) * 0.05f) + breathe * a;
                r = Quaternion.Slerp(atHilt, PointRotation, a);
            }
            // While pointing, the sword turns in the hand to carry straight on out of the front
            // of the glove, in line with the arm
            float onward = t < SwordOut ? 0f : Ease(t, SwordOut, SwordPointed);
            if (swordRelease >= 0f)
            {
                float a = Ease(swordRelease, 0f, SwordReturn);
                p = Vector3.Lerp(p, RightIdle, a);
                r = Quaternion.Slerp(r, ForwardIdle, a);
                onward *= 1f - a;
            }
            hand.localPosition = p;
            hand.localRotation = r;
            if (onward > 0f)
                rightHand.grip.SetLocalPositionAndRotation(
                    Vector3.Lerp(rightHand.grip.localPosition, PointGripSpot, onward), Quaternion.Slerp(rightHand.grip.localRotation, Quaternion.identity, onward));
            sheathed.root.gameObject.SetActive(inSheath);
            knife.root.gameObject.SetActive(!inSheath);
            // The arm crosses the body to the hip: see-through until the point is set
            SetArmAlpha(t < SwordOut ? 0.35f : Mathf.Lerp(0.35f, 1f, Ease(t, SwordOut, SwordPointed)));
        }

        // Shows a moment of the knife inspect in edit mode, for photos (negative: at rest)
        public void PreviewKnifeInspect(float time)
        {
            if (weapons == null) return;
            talonSpin = Mathf.Max(0f, time - TalonSpinStart) * TalonSpinSpeed;
            talonLower = swordRelease = -1f;
            PoseKnife(Vector3.zero, Vector3.zero, time);
            talonSpin = 0f;
            if (time < 0f) UpdateSheath();
        }

        // ------------------------------------------------------------------ see-through arm

        class ArmPart
        {
            public MeshRenderer renderer;
            public Material solid, clear;
        }

        readonly List<ArmPart> armParts = new();
        float armAlpha = 1f;

        // Gives the right arm its own solid and see-through materials
        void SetupArmFade()
        {
            foreach (var r in rightHand.root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var solid = new Material(r.sharedMaterial);
                materials.Add(solid);
                var clear = fadeTemplate ? new Material(fadeTemplate) : MakeTransparent(new Material(template));
                clear.SetTexture("_BaseMap", solid.GetTexture("_BaseMap"));
                clear.SetColor("_BaseColor", solid.GetColor("_BaseColor"));
                clear.SetFloat("_Smoothness", solid.GetFloat("_Smoothness"));
                clear.SetFloat("_Metallic", solid.GetFloat("_Metallic"));
                materials.Add(clear);
                r.sharedMaterial = solid;
                armParts.Add(new ArmPart { renderer = r, solid = solid, clear = clear });
            }
        }

        void SetArmAlpha(float alpha)
        {
            if (Mathf.Abs(alpha - armAlpha) < 0.001f) return;
            armAlpha = alpha;
            foreach (var part in armParts)
            {
                if (alpha > 0.995f) { part.renderer.sharedMaterial = part.solid; continue; }
                Color c = part.solid.GetColor("_BaseColor");
                c.a = alpha;
                part.clear.SetColor("_BaseColor", c);
                part.renderer.sharedMaterial = part.clear;
            }
        }

        // URP Lit, switched to alpha-blended transparent
        public static Material MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }
    }
}
