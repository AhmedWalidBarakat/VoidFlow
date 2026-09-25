using UnityEngine;

namespace VoidFlow
{
    // Void swords live in a sheath on your left hip: its mouth and the sword's hilt peek into
    // the bottom-left of the screen. Switching to the knife slot draws the sword: the right
    // hand reaches down to the hilt, pulls the blade out along the sheath and swings it up
    // into the ready pose. While you hold the sniper the sword sits in the sheath.
    public partial class ViewModel
    {
        const float SwordDrawTime = 0.9f, SwordGrab = 0.3f, SwordPulled = 0.55f;

        // The sheath mouth, and the sheath running back from it (its +Y) down past the hip
        static readonly Vector3 SheathMouth = new(-0.2f, -0.215f, 0.42f);
        static readonly Quaternion SheathRotation = FingersBack(new Vector3(-0.25f, -0.35f, -1f), new Vector3(1f, 0f, -0.25f));

        Transform sheath;
        WeaponParts sheathed;
        bool unsheatheSound, previewingDraw;

        // Shows a moment of the sword draw in edit mode, for photos
        public void PreviewSwordDraw(float time)
        {
            if (weapons == null) return;
            previewingDraw = true;
            drawTime = time;
            UpdateSheath();
            PoseKnife(Vector3.zero, Vector3.zero, -1f);
            previewingDraw = false;
            drawTime = 99f;
            UpdateSheath();
        }

        // (Re)builds the sheath with a copy of the sword in it; knives have no sheath
        void BuildSheath(Skins.Skin skin, WeaponBuilder builder)
        {
            if (sheath) Kill(sheath.gameObject);
            sheath = null;
            sheathed = null;
            if (!knife.IsSword) return;

            sheath = new GameObject("Sheath").transform;
            sheath.SetParent(transform, false);
            sheath.SetLocalPositionAndRotation(SheathMouth, SheathRotation);

            // Lacquered scabbard with metal fittings at the mouth and tip and a cord wrap; the
            // big cleaver gets a wide one
            bool wide = knife.model == KnifeModel.Colossus;
            Color lacquer = knife.model == KnifeModel.Tidebreaker ? new Color(0.04f, 0.06f, 0.14f) : new Color(0.03f, 0.03f, 0.035f);
            Color cordColor = knife.model switch
            {
                KnifeModel.HollowMoon => new Color(0.75f, 0.05f, 0.06f),
                KnifeModel.Tidebreaker => new Color(0.2f, 0.55f, 1f),
                _ => new Color(0.55f, 0.25f, 1f),
            };
            Material body = builder.Mat(lacquer, 0.85f, 0.2f);
            Material fitting = builder.Mat(new Color(0.85f, 0.65f, 0.3f), 0.85f, 0.9f);
            Material cord = builder.Mat(cordColor, 0.35f, 0f);
            float width = wide ? 0.07f : 0.034f, depth = wide ? 0.018f : 0.026f, length = wide ? 0.25f : 0.3f;
            builder.Part(sheath, PrimitiveType.Cube, body, new Vector3(0f, length * 0.5f, 0f), new Vector3(width, length, depth));
            builder.Part(sheath, PrimitiveType.Cube, fitting, new Vector3(0f, 0.006f, 0f), new Vector3(width + 0.005f, 0.012f, depth + 0.005f));
            builder.Part(sheath, PrimitiveType.Cube, fitting, new Vector3(0f, length - 0.006f, 0f), new Vector3(width + 0.004f, 0.012f, depth + 0.004f));
            builder.Part(sheath, PrimitiveType.Cube, cord, new Vector3(0f, 0.06f, 0f), new Vector3(width + 0.006f, 0.014f, depth + 0.006f));

            // The sword itself, blade down inside the scabbard, hilt sticking out of the mouth
            var holder = new GameObject("Sheathed Sword").transform;
            holder.SetParent(sheath, false);
            sheathed = builder.Knife(skin, holder);
            if (sheathed.aura) Kill(sheathed.aura.gameObject);
        }

        // Which copy of the sword shows: the one in the hand once it's been grabbed, otherwise
        // the one in the sheath
        void UpdateSheath()
        {
            if (sheathed == null) return;
            bool inHand = current == KnifeSlot && drawTime >= SwordGrab;
            sheathed.root.gameObject.SetActive(!inHand);
            knife.root.gameObject.SetActive(current != KnifeSlot || inHand);
            if (current == KnifeSlot && drawTime < SwordGrab) unsheatheSound = true;
            if (unsheatheSound && inHand)
            {
                unsheatheSound = false;
                Play(WeaponSounds.Unsheathe, 0.8f);
            }
        }

        // The draw: reach to the hilt, pull the blade out along the sheath, swing up to ready.
        // Returns false once the sword is out.
        bool PoseSwordDraw()
        {
            if (sheathed == null || current != KnifeSlot || drawTime >= SwordDrawTime || (!Application.isPlaying && !previewingDraw)) return false;

            // Where the hand has to be for its sword to sit exactly where the sheathed one is
            Transform rig = hand.parent;
            Quaternion swordRotation = Quaternion.Inverse(rig.rotation) * sheathed.root.rotation;
            Vector3 swordPosition = rig.InverseTransformPoint(sheathed.root.position);
            Quaternion atHilt = swordRotation * Quaternion.Inverse(rightHand.grip.localRotation);
            Vector3 hiltPosition = swordPosition - atHilt * rightHand.grip.localPosition;
            // Pulled clear: out along the blade, away from the sheath
            Vector3 pulled = hiltPosition + swordRotation * Vector3.down * 0.3f;

            float t = drawTime;
            if (t < SwordGrab)
            {
                float a = Ease(t, 0f, SwordGrab);
                hand.localPosition = Vector3.Lerp(RightIdle + new Vector3(-0.05f, -0.2f, -0.05f), hiltPosition, a);
                hand.localRotation = Quaternion.Slerp(ForwardIdle, atHilt, a);
            }
            else if (t < SwordPulled)
            {
                hand.localPosition = Vector3.Lerp(hiltPosition, pulled, Ease(t, SwordGrab, SwordPulled));
                hand.localRotation = atHilt;
            }
            else
            {
                float a = Ease(t, SwordPulled, SwordDrawTime);
                hand.localPosition = Vector3.Lerp(pulled, RightIdle, a) + Vector3.up * (Mathf.Sin(a * Mathf.PI) * 0.06f);
                hand.localRotation = Quaternion.Slerp(atHilt, ForwardIdle, a);
            }
            // The left arm dips while the right hand crosses the body to the hip
            leftHand.root.localPosition = LeftIdle + LeftHandAway * Plateau(t, 0f, 0.15f, SwordPulled, SwordDrawTime);
            leftHand.root.gameObject.SetActive(true);
            return true;
        }
    }
}
