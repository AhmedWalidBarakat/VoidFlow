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
    //  - Talon knife: hold F and it spins around the corner of the fist through its ring, at a
    //    tilted angle; let go and it finishes the turn and settles.
    //  - Butterfly knife: standing up on the fist, it flips one way, flips back the other way,
    //    rolls right around the hand, and flips home, the wrist rolling along with it.
    //  - Void swords: hold F for two quick cuts in opposite directions, then the sword is
    //    sheathed and held there, right hand on the hilt and left hand on the scabbard, like a
    //    samurai waiting to draw; let go and it's drawn back out.
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
        static readonly Vector3 TalonSpot = new(0.06f, -0.075f, 0.36f);
        static readonly Quaternion TalonRotation = FromXY(new Vector3(-0.4f, 0.3f, -0.87f), new Vector3(0.3f, 1f, 0.1f));
        // The top corner of the glove nearest you, where the talon hangs off the index finger
        static readonly Vector3 FingerPoint = new(GloveSize.x * 0.5f, GloveSize.y * 0.5f, GloveSize.z * 0.5f);

        // Butterfly flips, Counter Blox style: the arm reaches in from the right toward the
        // left, back of the hand up, and the knife stands on top of the fist facing you (its
        // held handle down through the fist). The wrist rolls between flips so each one is
        // seen from a new angle.
        static readonly Vector3 ButterflySpot = new(0.07f, -0.1f, 0.36f);
        static readonly Quaternion ButterflyRotation = FingersBack(new Vector3(-0.75f, 0.3f, 0.6f), Vector3.up);
        static readonly Vector3 HingePoint = new(0f, 0f, GloveSize.z * 0.5f + 0.035f);

        // Sword kata: the sheath rides up into view and the left hand takes hold of it just
        // below the mouth, the right hand resting on the hilt
        static readonly Vector3 PresentMouth = new(-0.03f, -0.125f, 0.34f);
        static readonly Quaternion PresentSheath = FingersBack(new Vector3(-0.85f, -0.25f, -0.45f), new Vector3(0.45f, 0.1f, -0.9f));
        static readonly Quaternion LeftOnSheath = FingersBack(new Vector3(0.35f, 0.55f, 0.75f), new Vector3(-0.3f, 0.7f, -0.6f));

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

        // Sword kata: cut, cut back, sheathe; hold on the sheath while F is held; let go and
        // it's drawn again. The first cut starts on the right, the second comes back.
        const float KataCut = 0.42f, KataSheathe = 1.26f, KataSeated = 1.18f, KataDraw = 0.75f;
        float swordRelease = -1f;

        void UpdateSwordPoint(bool held, float dt)
        {
            float before = inspectTime - dt;
            if (before < 0.02f && inspectTime >= 0.02f) Play(WeaponSounds.Slash, 0.75f);
            if (before < KataCut && inspectTime >= KataCut) Play(WeaponSounds.Slash, 0.75f);
            if (before < KataSeated && inspectTime >= KataSeated) Play(WeaponSounds.Sheathe, 0.8f);
            if (swordRelease >= 0f)
            {
                float r = swordRelease;
                swordRelease += dt;
                if (r < 0.22f && swordRelease >= 0.22f) Play(WeaponSounds.Unsheathe, 0.8f);
                if (swordRelease > KataDraw) StopInspect();
            }
            else if (!held && inspectTime >= KataSheathe) swordRelease = 0f;
        }

        // ------------------------------------------------------------------ posing

        // Places the hands for the current knife: the resting hold plus the slash offset
        // (camera space pos, rot), blended into the inspect pose while inspecting (inspect >= 0)
        void PoseKnife(Vector3 pos, Vector3 rot, float inspect)
        {
            bool reverse = knife.model == KnifeModel.Talon;
            SetGrip(rightHand, reverse);
            float time = Application.isPlaying ? Time.time : 0f;
            if (sheath && !(inspect >= 0f && knife.IsSword)) sheath.SetLocalPositionAndRotation(SheathMouth, SheathRotation);
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
                var (fingerPos, fingerRot) = FingerGrip(inspect);
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
        (Vector3, Quaternion) FingerGrip(float inspect)
        {
            if (knife.model == KnifeModel.Talon)
            {
                Quaternion r = Quaternion.Euler(0f, 90f, 0f); // knife Z runs along the finger (arm +X)
                return (FingerPoint - r * knife.ringCenter, r);
            }
            // In the rollover the knife travels right round the fist, about the forearm
            var orbit = Quaternion.Euler(0f, inspect >= 0f ? WeaponParts.ButterflyTrick(inspect).orbit : 0f, 0f);
            return (orbit * HingePoint, orbit * Quaternion.LookRotation(Vector3.right, Vector3.forward));
        }

        // Small wrist motion on top of the trick pose
        Quaternion TrickMove(float t)
        {
            if (t < 0f) return Quaternion.identity;
            if (knife.model == KnifeModel.Talon)
                return Quaternion.Euler(0f, Mathf.Sin(talonSpin * Mathf.Deg2Rad) * 3f, 0f);
            // Butterfly: the wrist rolls one way with the first flip, back with the second, and
            // rocks through the rollover and the flips home
            float flips = Plateau(t, 0.3f, 0.45f, 2.3f, 2.5f);
            float roll = 30f * Mathf.Sin(Mathf.InverseLerp(0.35f, 1.25f, t) * Mathf.PI * 2f) + 12f * Mathf.Sin(Mathf.InverseLerp(1.25f, 2.45f, t) * Mathf.PI * 3f);
            return Quaternion.Euler(0f, roll * flips, 0f);
        }

        // The butterfly arm sways side to side and dips with each flip while it shows off
        Vector3 TrickBob(float t)
        {
            if (t < 0f || knife.model != KnifeModel.Butterfly) return Vector3.zero;
            float active = Plateau(t, 0.3f, 0.45f, 2.3f, 2.5f);
            float beat = (t - 0.35f) * Mathf.PI / WeaponParts.FlipPeriod;
            return new Vector3(Mathf.Sin(beat * 0.5f) * 0.02f, -Mathf.Abs(Mathf.Sin(beat)) * 0.01f, 0f) * active;
        }

        // Sword kata: two cuts, then to the hip and into the sheath, held there with both hands
        // (the sheath rides up into view), then drawn back out when F is released
        void PoseSwordInspect(float t)
        {
            float present = Ease(t, KataCut * 2f, KataSheathe);
            if (swordRelease >= 0f) present *= 1f - Ease(swordRelease, 0.35f, KataDraw);
            sheath.SetLocalPositionAndRotation(Vector3.Lerp(SheathMouth, PresentMouth, present), Quaternion.Slerp(SheathRotation, PresentSheath, present));
            var (atHilt, hiltPosition, pulled) = HiltPose();
            Vector3 p;
            Quaternion r;
            bool inSheath = false;
            float fade = 1f;

            if (t < KataCut * 2f)
            {
                // Two cuts: first from the right, then back from the left
                float c = t < KataCut ? t : t - KataCut;
                var (sp, sr) = Sample(SlashKeys, c * SlashKeys[^1].t / KataCut);
                float side = t < KataCut ? 1f : -1f;
                sp = new Vector3(sp.x * side, sp.y, sp.z) * 1.3f;
                sr = new Vector3(sr.x, sr.y * side, sr.z * side) * 1.3f;
                p = RightIdle + sp;
                r = Quaternion.Euler(sr) * ForwardIdle;
                leftHand.root.gameObject.SetActive(false);
            }
            else if (t < KataSheathe)
            {
                // To the hip; the blade slides home along the sheath at the end
                float a = Ease(t, KataCut * 2f, KataSeated);
                p = Vector3.Lerp(RightIdle, hiltPosition, a);
                r = Quaternion.Slerp(ForwardIdle, atHilt, a);
                inSheath = t >= KataSeated;
                fade = 0.35f;
            }
            else
            {
                // Held on the hilt, breathing
                p = hiltPosition + new Vector3(0f, Mathf.Sin(t * 2f) * 0.002f, 0f);
                r = atHilt;
                inSheath = true;
            }

            if (swordRelease >= 0f)
            {
                // Drawn back out: grip, pull clear along the sheath, swing up to ready
                float s = swordRelease;
                inSheath = s < 0.22f;
                fade = 0.35f;
                if (s < 0.22f) { p = hiltPosition; r = atHilt; }
                else if (s < 0.45f) { p = Vector3.Lerp(hiltPosition, pulled, Ease(s, 0.22f, 0.45f)); r = atHilt; }
                else
                {
                    float a = Ease(s, 0.45f, KataDraw);
                    p = Vector3.Lerp(pulled, RightIdle, a) + Vector3.up * (Mathf.Sin(a * Mathf.PI) * 0.05f);
                    r = Quaternion.Slerp(atHilt, ForwardIdle, a);
                    fade = Mathf.Lerp(0.35f, 1f, a);
                }
            }

            hand.localPosition = p;
            hand.localRotation = r;
            sheathed.root.gameObject.SetActive(inSheath);
            knife.root.gameObject.SetActive(!inSheath);
            SetArmAlpha(fade);

            // The left hand takes the scabbard just below the mouth while the sword is sheathed
            bool leftOn = t >= KataCut * 2f;
            leftHand.root.gameObject.SetActive(leftOn);
            if (leftOn)
            {
                Transform rig = hand.parent;
                Vector3 onSheath = rig.InverseTransformPoint(sheath.TransformPoint(new Vector3(0f, 0.07f, 0f)));
                leftHand.root.SetLocalPositionAndRotation(onSheath - LeftOnSheath * GripFront, LeftOnSheath);
            }
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
