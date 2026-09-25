using UnityEngine;

namespace VoidFlow
{
    public enum KnifeModel { Talon, Butterfly, HollowMoon, Tidebreaker, Colossus, Rifle }

    // Finishes. Most are painted in code (our own takes on the classic flashy knife finishes,
    // and the glowing Void ones); the stone, carbon and metal ones use CC0 photo textures from
    // ambientCG (Resources/SkinTextures).
    public enum KnifeFinish
    {
        Polished, Tempered, Nebula, SunsetFade, CandySwirl, AmberStripe, RedWeb, EmeraldNebula, HollowMoon, Tidebreaker, Colossus,
        TidewaterOnyx, SmokeOnyx, PearlOnyx, GreyMarble, BlackMarble, WhiteMarble, Carbon, Gunmetal, DiamondPlate, Saddle,
    }

    public enum SkinRarity { Default, Mythic, Void }

    // Every weapon skin in the game. Besides the defaults there are two rarities: Mythic and
    // Void. Mythic knives are a talon knife or butterfly knife with a flashy finish; Void knives
    // are knife-sized takes on legendary swords with glowing edges, an aura and their own
    // inspect. Cases drop Void 6% of the time.
    public static class Skins
    {
        public const float VoidChance = 0.06f;

        public readonly struct Skin
        {
            public readonly string name;
            public readonly KnifeModel model;
            public readonly KnifeFinish finish;
            public readonly SkinRarity rarity;

            public Skin(string name, KnifeModel model, KnifeFinish finish, SkinRarity rarity)
            {
                this.name = name;
                this.model = model;
                this.finish = finish;
                this.rarity = rarity;
            }
        }

        public static readonly Skin[] Knives =
        {
            new("Talon Knife", KnifeModel.Talon, KnifeFinish.Tempered, SkinRarity.Default),
            new("Talon Knife | Nebula", KnifeModel.Talon, KnifeFinish.Nebula, SkinRarity.Mythic),
            new("Talon Knife | Amber Stripe", KnifeModel.Talon, KnifeFinish.AmberStripe, SkinRarity.Mythic),
            new("Talon Knife | Red Web", KnifeModel.Talon, KnifeFinish.RedWeb, SkinRarity.Mythic),
            new("Butterfly | Sunset Fade", KnifeModel.Butterfly, KnifeFinish.SunsetFade, SkinRarity.Mythic),
            new("Butterfly | Candy Swirl", KnifeModel.Butterfly, KnifeFinish.CandySwirl, SkinRarity.Mythic),
            new("Butterfly | Emerald Nebula", KnifeModel.Butterfly, KnifeFinish.EmeraldNebula, SkinRarity.Mythic),
            new("Hollow Moon", KnifeModel.HollowMoon, KnifeFinish.HollowMoon, SkinRarity.Void),
            new("Tidebreaker", KnifeModel.Tidebreaker, KnifeFinish.Tidebreaker, SkinRarity.Void),
            new("Colossus", KnifeModel.Colossus, KnifeFinish.Colossus, SkinRarity.Void),
            new("Talon Knife | Tidewater Onyx", KnifeModel.Talon, KnifeFinish.TidewaterOnyx, SkinRarity.Mythic),
            new("Talon Knife | Carbon", KnifeModel.Talon, KnifeFinish.Carbon, SkinRarity.Mythic),
            new("Butterfly | Black Marble", KnifeModel.Butterfly, KnifeFinish.BlackMarble, SkinRarity.Mythic),
            new("Butterfly | Smoke Onyx", KnifeModel.Butterfly, KnifeFinish.SmokeOnyx, SkinRarity.Mythic),
            new("Talon Knife | Pearl Onyx", KnifeModel.Talon, KnifeFinish.PearlOnyx, SkinRarity.Mythic),
        };

        public static readonly Skin[] Snipers =
        {
            new("Longreach", KnifeModel.Rifle, KnifeFinish.Polished, SkinRarity.Default),
            new("Longreach | Nebula", KnifeModel.Rifle, KnifeFinish.Nebula, SkinRarity.Mythic),
            new("Longreach | Amber Stripe", KnifeModel.Rifle, KnifeFinish.AmberStripe, SkinRarity.Mythic),
            new("Longreach | Candy Swirl", KnifeModel.Rifle, KnifeFinish.CandySwirl, SkinRarity.Mythic),
            new("Longreach | Red Web", KnifeModel.Rifle, KnifeFinish.RedWeb, SkinRarity.Mythic),
            new("Longreach | Hollow Moon", KnifeModel.Rifle, KnifeFinish.HollowMoon, SkinRarity.Void),
            new("Longreach | Tidebreaker", KnifeModel.Rifle, KnifeFinish.Tidebreaker, SkinRarity.Void),
            new("Longreach | Tidewater Onyx", KnifeModel.Rifle, KnifeFinish.TidewaterOnyx, SkinRarity.Mythic),
            new("Longreach | Carbon", KnifeModel.Rifle, KnifeFinish.Carbon, SkinRarity.Mythic),
            new("Longreach | Diamond Plate", KnifeModel.Rifle, KnifeFinish.DiamondPlate, SkinRarity.Mythic),
            new("Longreach | Grey Marble", KnifeModel.Rifle, KnifeFinish.GreyMarble, SkinRarity.Mythic),
            new("Longreach | White Marble", KnifeModel.Rifle, KnifeFinish.WhiteMarble, SkinRarity.Mythic),
            new("Longreach | Gunmetal", KnifeModel.Rifle, KnifeFinish.Gunmetal, SkinRarity.Mythic),
            new("Longreach | Saddle", KnifeModel.Rifle, KnifeFinish.Saddle, SkinRarity.Mythic),
        };

        // Picks a case drop: Void 6% of the time, otherwise a Mythic, evenly within each
        public static int Roll(Skin[] pool)
        {
            var wanted = Random.value < VoidChance ? SkinRarity.Void : SkinRarity.Mythic;
            int count = 0;
            foreach (var s in pool) if (s.rarity == wanted) count++;
            int pick = Random.Range(0, count);
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].rarity == wanted && pick-- == 0) return i;
            return 0;
        }

        public static string RarityName(SkinRarity r) => r switch
        {
            SkinRarity.Mythic => "Mythic",
            SkinRarity.Void => "Void",
            _ => "Default",
        };

        // Void shimmers between violet and a pale glow
        public static Color RarityColor(SkinRarity r) => r switch
        {
            SkinRarity.Mythic => new Color(1f, 0.3f, 0.65f),
            SkinRarity.Void => Color.Lerp(new Color(0.6f, 0.25f, 1f), new Color(0.85f, 0.7f, 1f), Mathf.Sin(Time.time * 2.5f) * 0.5f + 0.5f),
            _ => new Color(0.7f, 0.72f, 0.78f),
        };

        // Equipped skins, remembered between sessions
        public static int EquippedKnife
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("VoidFlow.Knife", 0), 0, Knives.Length - 1);
            set { PlayerPrefs.SetInt("VoidFlow.Knife", value); PlayerPrefs.Save(); }
        }

        public static int EquippedSniper
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("VoidFlow.Sniper", 0), 0, Snipers.Length - 1);
            set { PlayerPrefs.SetInt("VoidFlow.Sniper", value); PlayerPrefs.Save(); }
        }
    }
}
