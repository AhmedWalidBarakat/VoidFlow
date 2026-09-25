using UnityEngine;

namespace VoidFlow
{
    public enum KnifeModel { Talon, Butterfly, HollowMoon, Tidebreaker, Colossus, Rifle, Reaper, Saber, Shardfang, Railgun, Hellfire, Kukri, Claws, Axe, Sai, Spear, Kris, Prism, Bone, Lance, Seraph }

    // Finishes. Most are painted in code (our own takes on the classic flashy knife finishes,
    // and the glowing Void ones); the stone, carbon and metal ones use CC0 photo textures from
    // ambientCG (Resources/SkinTextures).
    public enum KnifeFinish
    {
        Polished, Tempered, Nebula, SunsetFade, CandySwirl, AmberStripe, RedWeb, EmeraldNebula, HollowMoon, Tidebreaker, Colossus,
        TidewaterOnyx, SmokeOnyx, PearlOnyx, GreyMarble, BlackMarble, WhiteMarble, Carbon, Gunmetal, DiamondPlate, Saddle,
        DesertOnyx, CaramelSwirl, GlacierOnyx, AmberOnyx, CrimsonOnyx, StormOnyx, VioletOnyx, Obsidian, Confetti, MagmaVein, Molten, LavaFlow, Plasma, Toxic, VoidFlare, Frostbite, ShatteredIce, Sapphire, Amethyst, Ruby, Chrome, Gold, Copper, AntiqueGold, Holographic,
        SoulReaper, FrostReaper, NovaSaber, CrimsonSaber, Shardfang, Singularity, FrostRail, Hellfire,
        SerpentFang, DragonClaw, DoomAxe, StormSai, Starlance, WraithKris, InfernoReaper, VoidSaber, SolarSaber, EmeraldShard, EventHorizon, TempestRail, Inferno, AbyssalFire, PrismRifle, AmethystPrism, Deathwhisper, PlasmaLance, CrimsonLance, Seraph,
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

        public static bool IsRifle(KnifeModel m) => m is KnifeModel.Rifle or KnifeModel.Railgun or KnifeModel.Hellfire
            or KnifeModel.Prism or KnifeModel.Bone or KnifeModel.Lance or KnifeModel.Seraph;

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
            new("Soul Reaper", KnifeModel.Reaper, KnifeFinish.SoulReaper, SkinRarity.Void),
            new("Frost Reaper", KnifeModel.Reaper, KnifeFinish.FrostReaper, SkinRarity.Void),
            new("Nova Saber", KnifeModel.Saber, KnifeFinish.NovaSaber, SkinRarity.Void),
            new("Crimson Saber", KnifeModel.Saber, KnifeFinish.CrimsonSaber, SkinRarity.Void),
            new("Shardfang", KnifeModel.Shardfang, KnifeFinish.Shardfang, SkinRarity.Void),
            new("Serpent Fang", KnifeModel.Kukri, KnifeFinish.SerpentFang, SkinRarity.Void),
            new("Dragon Claw", KnifeModel.Claws, KnifeFinish.DragonClaw, SkinRarity.Void),
            new("Doom Axe", KnifeModel.Axe, KnifeFinish.DoomAxe, SkinRarity.Void),
            new("Storm Sai", KnifeModel.Sai, KnifeFinish.StormSai, SkinRarity.Void),
            new("Starlance", KnifeModel.Spear, KnifeFinish.Starlance, SkinRarity.Void),
            new("Wraith Kris", KnifeModel.Kris, KnifeFinish.WraithKris, SkinRarity.Void),
            new("Inferno Reaper", KnifeModel.Reaper, KnifeFinish.InfernoReaper, SkinRarity.Void),
            new("Void Saber", KnifeModel.Saber, KnifeFinish.VoidSaber, SkinRarity.Void),
            new("Solar Saber", KnifeModel.Saber, KnifeFinish.SolarSaber, SkinRarity.Void),
            new("Emerald Shard", KnifeModel.Shardfang, KnifeFinish.EmeraldShard, SkinRarity.Void),
            new("Talon Knife | Tidewater Onyx", KnifeModel.Talon, KnifeFinish.TidewaterOnyx, SkinRarity.Mythic),
            new("Talon Knife | Carbon", KnifeModel.Talon, KnifeFinish.Carbon, SkinRarity.Mythic),
            new("Butterfly | Black Marble", KnifeModel.Butterfly, KnifeFinish.BlackMarble, SkinRarity.Mythic),
            new("Butterfly | Smoke Onyx", KnifeModel.Butterfly, KnifeFinish.SmokeOnyx, SkinRarity.Mythic),
            new("Talon Knife | Pearl Onyx", KnifeModel.Talon, KnifeFinish.PearlOnyx, SkinRarity.Mythic),
            new("Talon Knife | Desert Onyx", KnifeModel.Talon, KnifeFinish.DesertOnyx, SkinRarity.Mythic),
            new("Butterfly | Caramel Swirl", KnifeModel.Butterfly, KnifeFinish.CaramelSwirl, SkinRarity.Mythic),
            new("Talon Knife | Glacier Onyx", KnifeModel.Talon, KnifeFinish.GlacierOnyx, SkinRarity.Mythic),
            new("Butterfly | Amber Onyx", KnifeModel.Butterfly, KnifeFinish.AmberOnyx, SkinRarity.Mythic),
            new("Talon Knife | Crimson Onyx", KnifeModel.Talon, KnifeFinish.CrimsonOnyx, SkinRarity.Mythic),
            new("Butterfly | Crimson Onyx", KnifeModel.Butterfly, KnifeFinish.CrimsonOnyx, SkinRarity.Mythic),
            new("Butterfly | Storm Onyx", KnifeModel.Butterfly, KnifeFinish.StormOnyx, SkinRarity.Mythic),
            new("Talon Knife | Violet Onyx", KnifeModel.Talon, KnifeFinish.VioletOnyx, SkinRarity.Mythic),
            new("Butterfly | Violet Onyx", KnifeModel.Butterfly, KnifeFinish.VioletOnyx, SkinRarity.Mythic),
            new("Butterfly | Obsidian", KnifeModel.Butterfly, KnifeFinish.Obsidian, SkinRarity.Mythic),
            new("Talon Knife | Confetti", KnifeModel.Talon, KnifeFinish.Confetti, SkinRarity.Mythic),
            new("Butterfly | Magma Vein", KnifeModel.Butterfly, KnifeFinish.MagmaVein, SkinRarity.Mythic),
            new("Talon Knife | Magma Vein", KnifeModel.Talon, KnifeFinish.MagmaVein, SkinRarity.Mythic),
            new("Talon Knife | Molten", KnifeModel.Talon, KnifeFinish.Molten, SkinRarity.Mythic),
            new("Butterfly | Molten", KnifeModel.Butterfly, KnifeFinish.Molten, SkinRarity.Mythic),
            new("Butterfly | Lava Flow", KnifeModel.Butterfly, KnifeFinish.LavaFlow, SkinRarity.Mythic),
            new("Talon Knife | Lava Flow", KnifeModel.Talon, KnifeFinish.LavaFlow, SkinRarity.Mythic),
            new("Talon Knife | Plasma", KnifeModel.Talon, KnifeFinish.Plasma, SkinRarity.Mythic),
            new("Butterfly | Plasma", KnifeModel.Butterfly, KnifeFinish.Plasma, SkinRarity.Mythic),
            new("Butterfly | Toxic", KnifeModel.Butterfly, KnifeFinish.Toxic, SkinRarity.Mythic),
            new("Talon Knife | Toxic", KnifeModel.Talon, KnifeFinish.Toxic, SkinRarity.Mythic),
            new("Talon Knife | Void Flare", KnifeModel.Talon, KnifeFinish.VoidFlare, SkinRarity.Mythic),
            new("Butterfly | Void Flare", KnifeModel.Butterfly, KnifeFinish.VoidFlare, SkinRarity.Mythic),
            new("Butterfly | Frostbite", KnifeModel.Butterfly, KnifeFinish.Frostbite, SkinRarity.Mythic),
            new("Talon Knife | Frostbite", KnifeModel.Talon, KnifeFinish.Frostbite, SkinRarity.Mythic),
            new("Talon Knife | Shattered Ice", KnifeModel.Talon, KnifeFinish.ShatteredIce, SkinRarity.Mythic),
            new("Butterfly | Shattered Ice", KnifeModel.Butterfly, KnifeFinish.ShatteredIce, SkinRarity.Mythic),
            new("Butterfly | Sapphire", KnifeModel.Butterfly, KnifeFinish.Sapphire, SkinRarity.Mythic),
            new("Talon Knife | Sapphire", KnifeModel.Talon, KnifeFinish.Sapphire, SkinRarity.Mythic),
            new("Talon Knife | Amethyst", KnifeModel.Talon, KnifeFinish.Amethyst, SkinRarity.Mythic),
            new("Butterfly | Amethyst", KnifeModel.Butterfly, KnifeFinish.Amethyst, SkinRarity.Mythic),
            new("Butterfly | Ruby", KnifeModel.Butterfly, KnifeFinish.Ruby, SkinRarity.Mythic),
            new("Talon Knife | Ruby", KnifeModel.Talon, KnifeFinish.Ruby, SkinRarity.Mythic),
            new("Talon Knife | Chrome", KnifeModel.Talon, KnifeFinish.Chrome, SkinRarity.Mythic),
            new("Butterfly | Chrome", KnifeModel.Butterfly, KnifeFinish.Chrome, SkinRarity.Mythic),
            new("Butterfly | 24K Gold", KnifeModel.Butterfly, KnifeFinish.Gold, SkinRarity.Mythic),
            new("Talon Knife | 24K Gold", KnifeModel.Talon, KnifeFinish.Gold, SkinRarity.Mythic),
            new("Talon Knife | Copper", KnifeModel.Talon, KnifeFinish.Copper, SkinRarity.Mythic),
            new("Butterfly | Antique Gold", KnifeModel.Butterfly, KnifeFinish.AntiqueGold, SkinRarity.Mythic),
            new("Talon Knife | Holographic", KnifeModel.Talon, KnifeFinish.Holographic, SkinRarity.Mythic),
            new("Butterfly | Holographic", KnifeModel.Butterfly, KnifeFinish.Holographic, SkinRarity.Mythic),
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
            new("Singularity", KnifeModel.Railgun, KnifeFinish.Singularity, SkinRarity.Void),
            new("Frostbite Railgun", KnifeModel.Railgun, KnifeFinish.FrostRail, SkinRarity.Void),
            new("Hellfire", KnifeModel.Hellfire, KnifeFinish.Hellfire, SkinRarity.Void),
            new("Event Horizon", KnifeModel.Railgun, KnifeFinish.EventHorizon, SkinRarity.Void),
            new("Tempest", KnifeModel.Railgun, KnifeFinish.TempestRail, SkinRarity.Void),
            new("Inferno", KnifeModel.Hellfire, KnifeFinish.Inferno, SkinRarity.Void),
            new("Abyssal Fire", KnifeModel.Hellfire, KnifeFinish.AbyssalFire, SkinRarity.Void),
            new("Prism", KnifeModel.Prism, KnifeFinish.PrismRifle, SkinRarity.Void),
            new("Amethyst Prism", KnifeModel.Prism, KnifeFinish.AmethystPrism, SkinRarity.Void),
            new("Deathwhisper", KnifeModel.Bone, KnifeFinish.Deathwhisper, SkinRarity.Void),
            new("Plasma Lance", KnifeModel.Lance, KnifeFinish.PlasmaLance, SkinRarity.Void),
            new("Crimson Lance", KnifeModel.Lance, KnifeFinish.CrimsonLance, SkinRarity.Void),
            new("Seraph", KnifeModel.Seraph, KnifeFinish.Seraph, SkinRarity.Void),
            new("Longreach | Tidewater Onyx", KnifeModel.Rifle, KnifeFinish.TidewaterOnyx, SkinRarity.Mythic),
            new("Longreach | Carbon", KnifeModel.Rifle, KnifeFinish.Carbon, SkinRarity.Mythic),
            new("Longreach | Diamond Plate", KnifeModel.Rifle, KnifeFinish.DiamondPlate, SkinRarity.Mythic),
            new("Longreach | Grey Marble", KnifeModel.Rifle, KnifeFinish.GreyMarble, SkinRarity.Mythic),
            new("Longreach | White Marble", KnifeModel.Rifle, KnifeFinish.WhiteMarble, SkinRarity.Mythic),
            new("Longreach | Gunmetal", KnifeModel.Rifle, KnifeFinish.Gunmetal, SkinRarity.Mythic),
            new("Longreach | Saddle", KnifeModel.Rifle, KnifeFinish.Saddle, SkinRarity.Mythic),
            new("Longreach | Desert Onyx", KnifeModel.Rifle, KnifeFinish.DesertOnyx, SkinRarity.Mythic),
            new("Longreach | Caramel Swirl", KnifeModel.Rifle, KnifeFinish.CaramelSwirl, SkinRarity.Mythic),
            new("Longreach | Glacier Onyx", KnifeModel.Rifle, KnifeFinish.GlacierOnyx, SkinRarity.Mythic),
            new("Longreach | Amber Onyx", KnifeModel.Rifle, KnifeFinish.AmberOnyx, SkinRarity.Mythic),
            new("Longreach | Crimson Onyx", KnifeModel.Rifle, KnifeFinish.CrimsonOnyx, SkinRarity.Mythic),
            new("Longreach | Storm Onyx", KnifeModel.Rifle, KnifeFinish.StormOnyx, SkinRarity.Mythic),
            new("Longreach | Violet Onyx", KnifeModel.Rifle, KnifeFinish.VioletOnyx, SkinRarity.Mythic),
            new("Longreach | Obsidian", KnifeModel.Rifle, KnifeFinish.Obsidian, SkinRarity.Mythic),
            new("Longreach | Confetti", KnifeModel.Rifle, KnifeFinish.Confetti, SkinRarity.Mythic),
            new("Longreach | Magma Vein", KnifeModel.Rifle, KnifeFinish.MagmaVein, SkinRarity.Mythic),
            new("Longreach | Molten", KnifeModel.Rifle, KnifeFinish.Molten, SkinRarity.Mythic),
            new("Longreach | Lava Flow", KnifeModel.Rifle, KnifeFinish.LavaFlow, SkinRarity.Mythic),
            new("Longreach | Plasma", KnifeModel.Rifle, KnifeFinish.Plasma, SkinRarity.Mythic),
            new("Longreach | Toxic", KnifeModel.Rifle, KnifeFinish.Toxic, SkinRarity.Mythic),
            new("Longreach | Void Flare", KnifeModel.Rifle, KnifeFinish.VoidFlare, SkinRarity.Mythic),
            new("Longreach | Frostbite", KnifeModel.Rifle, KnifeFinish.Frostbite, SkinRarity.Mythic),
            new("Longreach | Shattered Ice", KnifeModel.Rifle, KnifeFinish.ShatteredIce, SkinRarity.Mythic),
            new("Longreach | Sapphire", KnifeModel.Rifle, KnifeFinish.Sapphire, SkinRarity.Mythic),
            new("Longreach | Amethyst", KnifeModel.Rifle, KnifeFinish.Amethyst, SkinRarity.Mythic),
            new("Longreach | Ruby", KnifeModel.Rifle, KnifeFinish.Ruby, SkinRarity.Mythic),
            new("Longreach | Chrome", KnifeModel.Rifle, KnifeFinish.Chrome, SkinRarity.Mythic),
            new("Longreach | 24K Gold", KnifeModel.Rifle, KnifeFinish.Gold, SkinRarity.Mythic),
            new("Longreach | Copper", KnifeModel.Rifle, KnifeFinish.Copper, SkinRarity.Mythic),
            new("Longreach | Antique Gold", KnifeModel.Rifle, KnifeFinish.AntiqueGold, SkinRarity.Mythic),
            new("Longreach | Holographic", KnifeModel.Rifle, KnifeFinish.Holographic, SkinRarity.Mythic),
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
