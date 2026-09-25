using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Glove skins: the equipped gloves go on every arm (the knife arms and the sniper arms).
    // Mythic gloves wear a finish on the glove block; Void gloves also get their own add-ons
    // (armour, claws, runes, scales...) and flames, and a glowing knuckle plate.
    public partial class ViewModel
    {
        int gloveSkin;
        readonly List<BlockArm> arms = new();
        readonly List<WeaponParts> gloveKits = new();
        readonly List<Material> gloveMaterials = new();

        public void EquipGloveSkin(int index)
        {
            if (weapons == null) return;
            gloveSkin = Mathf.Clamp(index, 0, Skins.Gloves.Length - 1);
            Skins.EquippedGlove = gloveSkin;
            ApplyGloves();
            Play(WeaponSounds.Draw, 0.6f);
        }

        void ApplyGloves()
        {
            foreach (var kit in gloveKits) if (kit.root) Kill(kit.root.gameObject);
            gloveKits.Clear();
            foreach (var m in gloveMaterials) Kill(m);
            gloveMaterials.Clear();

            var skin = Skins.Gloves[gloveSkin];
            var builder = new WeaponBuilder(template, Layer, false, gloveMaterials);
            Material body = skin.finish == KnifeFinish.Polished ? glove : builder.SkinMaterial(skin.finish);
            Color hue = KnifeFinishes.Get(skin.finish).glow;
            if (hue.maxColorComponent > 0f) hue /= hue.maxColorComponent;
            Material plate = skin.rarity == SkinRarity.Void ? builder.Glow(hue, 2.4f, null) : gloveRubber;
            foreach (var arm in arms)
            {
                if (!arm.root) continue;
                foreach (var r in arm.root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name == "GloveBlock") SetArmMaterial(r, body);
                    else if (r.name == "GlovePlate") SetArmMaterial(r, plate);
                }
                if (skin.rarity == SkinRarity.Void) gloveKits.Add(builder.GloveKit(skin, arm.root));
            }
        }

        // Swaps a glove part's material, keeping the see-through copy the arm fades to in step
        void SetArmMaterial(MeshRenderer r, Material solid)
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
        }
    }
}
