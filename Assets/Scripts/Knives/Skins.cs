using UnityEngine;

namespace VoidFlow
{
    public enum KnifeModel { Talon, Butterfly, HollowMoon, Tidebreaker, Colossus, Rifle, Reaper, Saber, Shardfang, Railgun, Hellfire, Kukri, Claws, Axe, Sai, Spear, Kris, Prism, Bone, Lance, Seraph,
        Crescent, Leviathan, Storm, Clockwork, Orbit, Serpent, ScytheRifle, BlackHole, Glitch,
        Bayonet, Skeleton, KukriKnife,
        Glove, GloveArmor, GloveClaws, GloveRunes, GloveScales, GloveKnuckles, GloveBone, GloveCrystal, GloveWings, GloveStorm, GloveWraps }

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
        InfernoGauntlet, FrostTalons, VoidRunes, Dragonscale, PlasmaKnuckles, Bonehand, CrystalGauntlet, SeraphWraps, StormGauntlet, ReaperWraps,
        Vanilla,
    }

    public enum SkinRarity { Default, Mythic, Void }

    // Every weapon skin in the game. Besides the defaults there are two rarities: Mythic and
    // Void. Mythic knives are a talon knife or butterfly knife with a flashy finish; Void knives
    // are knife-sized takes on legendary swords with glowing edges, an aura and their own
    // inspect. Cases drop Void 6% of the time.
    // Loadout slots: the sniper is the primary, the knife the secondary, gloves go on the hands
    public enum ItemSlot { Primary, Secondary, Hands }

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
            or KnifeModel.Prism or KnifeModel.Bone or KnifeModel.Lance or KnifeModel.Seraph
            || (m >= KnifeModel.Crescent && m <= KnifeModel.Glitch);

        public static bool IsGlove(KnifeModel m) => m >= KnifeModel.Glove;

        public static Skin[] Pool(ItemSlot slot) => slot switch { ItemSlot.Primary => Snipers, ItemSlot.Hands => Gloves, _ => Knives };
        public static string SlotName(ItemSlot slot) => slot switch { ItemSlot.Primary => "PRIMARY", ItemSlot.Hands => "HANDS", _ => "SECONDARY" };
        public static string Noun(ItemSlot slot) => slot switch { ItemSlot.Primary => "SNIPER", ItemSlot.Hands => "GLOVES", _ => "KNIFE" };
        public static int Equipped(ItemSlot slot) => slot switch { ItemSlot.Primary => EquippedSniper, ItemSlot.Hands => EquippedGlove, _ => EquippedKnife };

        // What kind of item a skin is, for cards
        public static string KindName(Skin skin) => skin.model switch
        {
            var m when IsGlove(m) => m == KnifeModel.Glove ? "GLOVES" : "VOID GLOVES",
            KnifeModel.Talon => "TALON KNIFE",
            KnifeModel.Butterfly => "BUTTERFLY",
            KnifeModel.Bayonet => "M9 BAYONET",
            KnifeModel.Skeleton => "SKELETON KNIFE",
            KnifeModel.KukriKnife => "KUKRI KNIFE",
            KnifeModel.HollowMoon or KnifeModel.Tidebreaker or KnifeModel.Colossus => "SWORD",
            KnifeModel.Rifle => "LONGREACH",
            KnifeModel.Reaper => "VOID SCYTHE",
            KnifeModel.Saber => "PLASMA SABER",
            KnifeModel.Shardfang => "CRYSTAL DAGGER",
            KnifeModel.Railgun => "VOID RAILGUN",
            KnifeModel.Crescent => "MOON RIFLE",
            KnifeModel.Leviathan => "SEA SERPENT",
            KnifeModel.Storm => "TESLA RIFLE",
            KnifeModel.Clockwork => "CLOCKWORK",
            KnifeModel.Orbit => "ORBITAL",
            KnifeModel.Serpent => "SERPENT RIFLE",
            KnifeModel.ScytheRifle => "SCYTHE RIFLE",
            KnifeModel.BlackHole => "SINGULARITY",
            KnifeModel.Glitch => "CORRUPTED",
            KnifeModel.Kukri => "VOID KUKRI",
            KnifeModel.Claws => "VOID CLAWS",
            KnifeModel.Axe => "VOID AXE",
            KnifeModel.Sai => "VOID SAI",
            KnifeModel.Spear => "VOID SPEAR",
            KnifeModel.Kris => "VOID KRIS",
            _ => IsRifle(skin.model) ? "VOID RIFLE" : "VOID BLADE",
        };

        // Gloves: the default black pair, Mythic finishes, and Void gloves with their own add-ons
        public static readonly Skin[] Gloves =
        {
            new("Gloves", KnifeModel.Glove, KnifeFinish.Polished, SkinRarity.Default),
            new("Gloves | Nebula", KnifeModel.Glove, KnifeFinish.Nebula, SkinRarity.Mythic),
            new("Gloves | Sunset Fade", KnifeModel.Glove, KnifeFinish.SunsetFade, SkinRarity.Mythic),
            new("Gloves | Candy Swirl", KnifeModel.Glove, KnifeFinish.CandySwirl, SkinRarity.Mythic),
            new("Gloves | Amber Stripe", KnifeModel.Glove, KnifeFinish.AmberStripe, SkinRarity.Mythic),
            new("Gloves | Red Web", KnifeModel.Glove, KnifeFinish.RedWeb, SkinRarity.Mythic),
            new("Gloves | Emerald Nebula", KnifeModel.Glove, KnifeFinish.EmeraldNebula, SkinRarity.Mythic),
            new("Gloves | Carbon", KnifeModel.Glove, KnifeFinish.Carbon, SkinRarity.Mythic),
            new("Gloves | Gunmetal", KnifeModel.Glove, KnifeFinish.Gunmetal, SkinRarity.Mythic),
            new("Gloves | Diamond Plate", KnifeModel.Glove, KnifeFinish.DiamondPlate, SkinRarity.Mythic),
            new("Gloves | Saddle", KnifeModel.Glove, KnifeFinish.Saddle, SkinRarity.Mythic),
            new("Gloves | Tidewater Onyx", KnifeModel.Glove, KnifeFinish.TidewaterOnyx, SkinRarity.Mythic),
            new("Gloves | Black Marble", KnifeModel.Glove, KnifeFinish.BlackMarble, SkinRarity.Mythic),
            new("Gloves | 24K Gold", KnifeModel.Glove, KnifeFinish.Gold, SkinRarity.Mythic),
            new("Gloves | Chrome", KnifeModel.Glove, KnifeFinish.Chrome, SkinRarity.Mythic),
            new("Gloves | Holographic", KnifeModel.Glove, KnifeFinish.Holographic, SkinRarity.Mythic),
            new("Gloves | Molten", KnifeModel.Glove, KnifeFinish.Molten, SkinRarity.Mythic),
            new("Gloves | Toxic", KnifeModel.Glove, KnifeFinish.Toxic, SkinRarity.Mythic),
            new("Gloves | Sapphire", KnifeModel.Glove, KnifeFinish.Sapphire, SkinRarity.Mythic),
            new("Gloves | Ruby", KnifeModel.Glove, KnifeFinish.Ruby, SkinRarity.Mythic),
            new("Gloves | Amethyst", KnifeModel.Glove, KnifeFinish.Amethyst, SkinRarity.Mythic),
            new("Gloves | Obsidian", KnifeModel.Glove, KnifeFinish.Obsidian, SkinRarity.Mythic),
            new("Gloves | Confetti", KnifeModel.Glove, KnifeFinish.Confetti, SkinRarity.Mythic),
            new("Inferno Gauntlet", KnifeModel.GloveArmor, KnifeFinish.InfernoGauntlet, SkinRarity.Void),
            new("Frost Talons", KnifeModel.GloveClaws, KnifeFinish.FrostTalons, SkinRarity.Void),
            new("Void Runes", KnifeModel.GloveRunes, KnifeFinish.VoidRunes, SkinRarity.Void),
            new("Dragonscale", KnifeModel.GloveScales, KnifeFinish.Dragonscale, SkinRarity.Void),
            new("Plasma Knuckles", KnifeModel.GloveKnuckles, KnifeFinish.PlasmaKnuckles, SkinRarity.Void),
            new("Bonehand", KnifeModel.GloveBone, KnifeFinish.Bonehand, SkinRarity.Void),
            new("Crystal Gauntlet", KnifeModel.GloveCrystal, KnifeFinish.CrystalGauntlet, SkinRarity.Void),
            new("Seraph Wraps", KnifeModel.GloveWings, KnifeFinish.SeraphWraps, SkinRarity.Void),
            new("Storm Gauntlet", KnifeModel.GloveStorm, KnifeFinish.StormGauntlet, SkinRarity.Void),
            new("Reaper Wraps", KnifeModel.GloveWraps, KnifeFinish.ReaperWraps, SkinRarity.Void),
        };

        public static int EquippedGlove
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("VoidFlow.Glove", 0), 0, Gloves.Length - 1);
            set { PlayerPrefs.SetInt("VoidFlow.Glove", value); PlayerPrefs.Save(); }
        }

        public static readonly Skin[] Knives =
        {
            new("Talon Knife", KnifeModel.Talon, KnifeFinish.Vanilla, SkinRarity.Default),
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
            // Plain satin steel, like the classic shooters' vanilla knives
            new("Talon Knife | Tempered", KnifeModel.Talon, KnifeFinish.Tempered, SkinRarity.Mythic),
            new("Butterfly Knife", KnifeModel.Butterfly, KnifeFinish.Vanilla, SkinRarity.Mythic),
            new("M9 Bayonet", KnifeModel.Bayonet, KnifeFinish.Vanilla, SkinRarity.Mythic),
            new("Skeleton Knife", KnifeModel.Skeleton, KnifeFinish.Vanilla, SkinRarity.Mythic),
            new("Kukri Knife", KnifeModel.KukriKnife, KnifeFinish.Vanilla, SkinRarity.Mythic),
            // Classic fixed blades: our own takes on the real kukri, M9 bayonet and skeleton knife
            new("Kukri Knife | Sunset Fade", KnifeModel.KukriKnife, KnifeFinish.SunsetFade, SkinRarity.Mythic),
            new("Kukri Knife | Red Web", KnifeModel.KukriKnife, KnifeFinish.RedWeb, SkinRarity.Mythic),
            new("Kukri Knife | Nebula", KnifeModel.KukriKnife, KnifeFinish.Nebula, SkinRarity.Mythic),
            new("Kukri Knife | Emerald Nebula", KnifeModel.KukriKnife, KnifeFinish.EmeraldNebula, SkinRarity.Mythic),
            new("Kukri Knife | Carbon", KnifeModel.KukriKnife, KnifeFinish.Carbon, SkinRarity.Mythic),
            new("M9 Bayonet | Sunset Fade", KnifeModel.Bayonet, KnifeFinish.SunsetFade, SkinRarity.Mythic),
            new("M9 Bayonet | Nebula", KnifeModel.Bayonet, KnifeFinish.Nebula, SkinRarity.Mythic),
            new("M9 Bayonet | Sapphire", KnifeModel.Bayonet, KnifeFinish.Sapphire, SkinRarity.Mythic),
            new("M9 Bayonet | Ruby", KnifeModel.Bayonet, KnifeFinish.Ruby, SkinRarity.Mythic),
            new("M9 Bayonet | Red Web", KnifeModel.Bayonet, KnifeFinish.RedWeb, SkinRarity.Mythic),
            new("M9 Bayonet | Amber Stripe", KnifeModel.Bayonet, KnifeFinish.AmberStripe, SkinRarity.Mythic),
            new("Skeleton Knife | Sunset Fade", KnifeModel.Skeleton, KnifeFinish.SunsetFade, SkinRarity.Mythic),
            new("Skeleton Knife | Red Web", KnifeModel.Skeleton, KnifeFinish.RedWeb, SkinRarity.Mythic),
            new("Skeleton Knife | Nebula", KnifeModel.Skeleton, KnifeFinish.Nebula, SkinRarity.Mythic),
            new("Skeleton Knife | Emerald Nebula", KnifeModel.Skeleton, KnifeFinish.EmeraldNebula, SkinRarity.Mythic),
            new("Skeleton Knife | Black Marble", KnifeModel.Skeleton, KnifeFinish.BlackMarble, SkinRarity.Mythic),
            new("Skeleton Knife | 24K Gold", KnifeModel.Skeleton, KnifeFinish.Gold, SkinRarity.Mythic),
        };

        public static readonly Skin[] Snipers =
        {
            new("Longreach", KnifeModel.Rifle, KnifeFinish.Polished, SkinRarity.Default),
            new("Longreach | Nebula", KnifeModel.Rifle, KnifeFinish.Nebula, SkinRarity.Mythic),
            new("Longreach | Amber Stripe", KnifeModel.Rifle, KnifeFinish.AmberStripe, SkinRarity.Mythic),
            new("Longreach | Candy Swirl", KnifeModel.Rifle, KnifeFinish.CandySwirl, SkinRarity.Mythic),
            new("Longreach | Red Web", KnifeModel.Rifle, KnifeFinish.RedWeb, SkinRarity.Mythic),
            // Void snipers: every one its own design (Spectrum cycles through every color)
            new("Spectrum", KnifeModel.Railgun, KnifeFinish.Singularity, SkinRarity.Void),
            new("Crescent", KnifeModel.Crescent, KnifeFinish.HollowMoon, SkinRarity.Void),
            new("Leviathan", KnifeModel.Leviathan, KnifeFinish.Tidebreaker, SkinRarity.Void),
            new("Hellfire", KnifeModel.Hellfire, KnifeFinish.Hellfire, SkinRarity.Void),
            new("Stormcaller", KnifeModel.Storm, KnifeFinish.TempestRail, SkinRarity.Void),
            new("Clockwork", KnifeModel.Clockwork, KnifeFinish.Inferno, SkinRarity.Void),
            new("Nebula Core", KnifeModel.Orbit, KnifeFinish.FrostRail, SkinRarity.Void),
            new("Viper", KnifeModel.Serpent, KnifeFinish.SerpentFang, SkinRarity.Void),
            new("Grim Harvest", KnifeModel.ScytheRifle, KnifeFinish.AbyssalFire, SkinRarity.Void),
            new("Event Horizon", KnifeModel.BlackHole, KnifeFinish.EventHorizon, SkinRarity.Void),
            new("Glitch", KnifeModel.Glitch, KnifeFinish.CrimsonLance, SkinRarity.Void),
            new("Prism", KnifeModel.Prism, KnifeFinish.PrismRifle, SkinRarity.Void),
            new("Deathwhisper", KnifeModel.Bone, KnifeFinish.Deathwhisper, SkinRarity.Void),
            new("Plasma Lance", KnifeModel.Lance, KnifeFinish.PlasmaLance, SkinRarity.Void),
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
