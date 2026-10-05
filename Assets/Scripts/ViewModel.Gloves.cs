using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Glove skins: the equipped gloves go on every arm (the knife arms and the sniper arms).
    // Mythic gloves wear a finish on the glove; Void gloves are painted to match a karambit
    // (the glove and its wrist strap, see KarambitPaints.Gloves).
    public partial class ViewModel
    {
        int gloveSkin;
        readonly List<BlockArm> arms = new();
        readonly List<WeaponParts> gloveKits = new();
        readonly List<Material> gloveMaterials = new();
        // The fingers tightening on the grip (0..1), on the weapon in hand
        void SqueezeGloves(Weapon w, float amount)
        {
            foreach (var arm in arms)
                if (arm.root && arm.root.IsChildOf(w.root)) arm.squeeze = amount;
        }

        public void EquipGloveSkin(int index)
        {
            if (weapons == null) return;
            gloveSkin = Mathf.Clamp(index, 0, Skins.Gloves.Length - 1);
            Skins.EquippedGlove = gloveSkin;
            ApplyGloves();
            Play(WeaponSounds.Draw, 0.6f);
        }

        public void EquipSkin(ItemSlot slot, int index)
        {
            if (slot == ItemSlot.Primary) EquipSniperSkin(index);
            else if (slot == ItemSlot.Hands) EquipGloveSkin(index);
            else EquipKnifeSkin(index);
        }

        void ApplyGloves()
        {
            foreach (var kit in gloveKits)
            {
                if (kit.root) Kill(kit.root.gameObject);
                foreach (var bit in kit.attached) Kill(bit);
            }
            gloveKits.Clear();
            foreach (var m in gloveMaterials) Kill(m);
            gloveMaterials.Clear();

            var skin = Skins.Gloves[gloveSkin];
            var builder = new WeaponBuilder(template, Layer, false, gloveMaterials);
            bool painted = skin.paint != null;
            Material body = painted ? builder.PaintedGlove(skin.paint) : skin.finish == KnifeFinish.Polished ? glove : builder.SkinMaterial(skin.finish);
            // Skins keep the glove's stitching, padding and grain as relief under their finish
            if (body != glove && !painted) Resources.Load<ArmRig>("Arms/RightArm")?.DressGlove(body, false);
            Material strap = painted ? builder.PaintedStrap(skin.paint) : cuff;
            Color hue = KnifeFinishes.Get(skin.finish).glow;
            if (hue.maxColorComponent > 0f) hue /= hue.maxColorComponent;
            Material plate = skin.rarity == SkinRarity.Void ? builder.Glow(hue, 2.4f, null) : gloveRubber;
            foreach (var arm in arms)
            {
                if (!arm.root) continue;
                foreach (var r in arm.root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "GloveBlock") SetArmMaterial(r, body);
                    else if (r.name == "GloveStrap" && strap) SetArmMaterial(r, strap);
                    else if (r.name == "GlovePlate") r.gameObject.SetActive(false); // (the fitted add-ons glow instead)
                }
                if (skin.rarity == SkinRarity.Void && !painted)
                {
                    // Fitted to the glove with the fingers straight, then back to the hand's pose
                    var fit = HandFit.Measure(arm.rig, arm.bones, FindSkin(arm, "GloveBlock"), FindSkin(arm, "GloveStrap"));
                    gloveKits.Add(builder.GloveKit(skin, fit));
                    arm.rig.Pose(arm.bones, arm.current);
                }
            }
        }

        static SkinnedMeshRenderer FindSkin(BlockArm arm, string name)
        {
            foreach (var r in arm.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == name) return r;
            return null;
        }

        // Swaps a glove part's material, keeping the see-through copy the arm fades to in step
        void SetArmMaterial(Renderer r, Material solid)
        {
            foreach (var (list, alpha) in new[] { (armParts, armAlpha), (leftArmParts, leftAlpha) })
                foreach (var part in list)
                {
                    if (part.renderer != r) continue;
                    var clear = fadeTemplate ? new Material(fadeTemplate) : MakeTransparent(new Material(template));
                    clear.SetTexture("_BaseMap", solid.GetTexture("_BaseMap"));
                    var c = solid.GetColor("_BaseColor");
                    c.a = alpha;
                    clear.SetColor("_BaseColor", c);
                    clear.SetFloat("_Smoothness", solid.GetFloat("_Smoothness"));
                    clear.SetFloat("_Metallic", solid.GetFloat("_Metallic"));
                    gloveMaterials.Add(clear);
                    part.solid = solid;
                    part.clear = clear;
                    r.sharedMaterial = alpha > 0.995f ? solid : clear;
                    return;
                }
            r.sharedMaterial = solid;
        }

        void UpdateGloves()
        {
            float inspect = current == KnifeSlot ? inspectTime : -1f;
            foreach (var kit in gloveKits) kit.Animate(Application.isPlaying ? Time.time : 0f, inspect);
            UpdateFingers(Application.isPlaying ? Time.deltaTime : 1f);
        }
    }
}
