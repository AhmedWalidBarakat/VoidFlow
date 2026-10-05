using UnityEngine;

namespace VoidFlow
{
    // The Void weapons swing the way their weapon would: each has a combo of cuts that the
    // clicks run through in turn (the next cut mirrored from the last, so they flow side to
    // side), timed to its weight: a sword sweeps wide, chops down and rises, a scythe reaps in
    // great low arcs, a dagger slashes and stabs, a karambit hooks with a roll of the wrist, a
    // kunai and a lance thrust, and the twin blades cut one hand after the other. The other
    // knives keep the one diagonal cut.
    //
    // A cut is keys of (time, hand offset in camera space, hand turn in degrees) from the hold.
    public partial class ViewModel
    {
        // Wound up on the right, swept flat across to the left, followed through
        static readonly (float t, Vector3 pos, Vector3 rot)[] SweepCut =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.09f, new Vector3(0.06f, 0.02f, -0.03f), new Vector3(-5f, 35f, -10f)),
            (0.2f, new Vector3(-0.17f, 0f, 0.08f), new Vector3(5f, -55f, 20f)),
            (0.3f, new Vector3(-0.16f, -0.01f, 0.06f), new Vector3(5f, -50f, 18f)),
            (0.52f, Vector3.zero, Vector3.zero),
        };
        // Raised high and brought down hard
        static readonly (float t, Vector3 pos, Vector3 rot)[] OverheadCut =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.1f, new Vector3(0.02f, 0.06f, 0.0f), new Vector3(-40f, 10f, -10f)),
            (0.2f, new Vector3(-0.06f, -0.05f, 0.05f), new Vector3(40f, -15f, 20f)),
            (0.3f, new Vector3(-0.055f, -0.055f, 0.045f), new Vector3(36f, -12f, 18f)),
            (0.54f, Vector3.zero, Vector3.zero),
        };
        // From low on the left, rising up and across
        static readonly (float t, Vector3 pos, Vector3 rot)[] RisingCut =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.08f, new Vector3(-0.04f, -0.06f, -0.02f), new Vector3(30f, -20f, 30f)),
            (0.18f, new Vector3(0.08f, 0.07f, 0.07f), new Vector3(-40f, 30f, -35f)),
            (0.27f, new Vector3(0.07f, 0.07f, 0.05f), new Vector3(-36f, 27f, -30f)),
            (0.48f, Vector3.zero, Vector3.zero),
        };
        // A short, fast thrust straight out
        static readonly (float t, Vector3 pos, Vector3 rot)[] StabCut =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.06f, new Vector3(0.01f, -0.01f, -0.05f), new Vector3(5f, 0f, 0f)),
            (0.13f, new Vector3(-0.03f, 0.02f, 0.16f), new Vector3(-10f, -8f, 0f)),
            (0.2f, new Vector3(-0.03f, 0.02f, 0.15f), new Vector3(-10f, -8f, 0f)),
            (0.36f, Vector3.zero, Vector3.zero),
        };
        // A scythe's reap: drawn far back to the right, a great low arc across, the blade
        // turning into the cut
        static readonly (float t, Vector3 pos, Vector3 rot)[] ReapCut =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.12f, new Vector3(0.09f, 0.03f, -0.03f), new Vector3(-10f, 45f, -20f)),
            (0.26f, new Vector3(-0.2f, -0.04f, 0.1f), new Vector3(10f, -70f, 35f)),
            (0.36f, new Vector3(-0.19f, -0.05f, 0.08f), new Vector3(8f, -65f, 32f)),
            (0.62f, Vector3.zero, Vector3.zero),
        };
        // A karambit's hook: the wrist rolling over as the curve rakes down and in
        static readonly (float t, Vector3 pos, Vector3 rot)[] HookCut =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.06f, new Vector3(0.05f, 0.02f, -0.01f), new Vector3(-10f, 20f, -25f)),
            (0.15f, new Vector3(-0.045f, -0.03f, 0.04f), new Vector3(15f, -20f, 30f)),
            (0.22f, new Vector3(-0.06f, -0.05f, 0.03f), new Vector3(24f, -18f, 40f)),
            (0.4f, Vector3.zero, Vector3.zero),
        };

        static (float t, Vector3 pos, Vector3 rot)[][] SwordCombo => new[] { SweepCut, OverheadCut, RisingCut };
        static (float t, Vector3 pos, Vector3 rot)[][] ScytheCombo => new[] { ReapCut, OverheadCut };
        static (float t, Vector3 pos, Vector3 rot)[][] DaggerCombo => new[] { SlashKeys, StabCut, SlashKeys };
        static (float t, Vector3 pos, Vector3 rot)[][] KarambitCombo => new[] { HookCut, HookCut, StabCut };
        static (float t, Vector3 pos, Vector3 rot)[][] KunaiCombo => new[] { StabCut, SlashKeys, StabCut };
        static (float t, Vector3 pos, Vector3 rot)[][] PlainCombo => new[] { SlashKeys };

        int comboStep;
        float lastCutAt = -99f;
        (float t, Vector3 pos, Vector3 rot)[] currentCut;

        (float t, Vector3 pos, Vector3 rot)[][] ComboFor()
        {
            var skin = Skins.Knives[knifeSkin];
            if (skin.asset == null) return PlainCombo;
            string id = skin.asset.Substring(0, 2);
            return skin.model switch
            {
                KnifeModel.ModelScythe => ScytheCombo,
                KnifeModel.ModelDual => PlainCombo, // (one hand after the other, see PoseKnife)
                _ => id switch
                {
                    "30" or "35" => KarambitCombo,
                    "32" => KunaiCombo,
                    "17" => DaggerCombo,
                    _ => Skins.IsVoidKnife(skin.asset) ? DaggerCombo : SwordCombo,
                },
            };
        }

        // The next cut of the combo (a fresh combo after a pause)
        void NextCut(float sinceLast)
        {
            var combo = ComboFor();
            comboStep = sinceLast > 0.9f ? 0 : (comboStep + 1) % combo.Length;
            currentCut = combo[comboStep];
        }

        // When the next click can start a cut: most of the way through this one
        float CutReady => (currentCut ?? SlashKeys)[^1].t * 0.62f;

        // The twin blades cut one hand after the other
        bool TwinCutsLeft => knife != null && knife.model == KnifeModel.ModelDual && slashTime >= 0f && slashSide < 0f;
    }
}
