using UnityEngine;

namespace VoidFlow
{
    public enum KnifeModel { Talon, Butterfly, HollowMoon, Tidebreaker, Colossus, Rifle, Reaper, Saber, Shardfang, Railgun, Hellfire, Kukri, Claws, Axe, Sai, Spear, Kris, Prism, Bone, Lance, Seraph,
        Crescent, Leviathan, Storm, Clockwork, Orbit, Serpent, ScytheRifle, BlackHole, Glitch,
        Bayonet, Skeleton, KukriKnife,
        Glove, GloveArmor, GloveClaws, GloveRunes, GloveScales, GloveKnuckles, GloveBone, GloveCrystal, GloveWings, GloveStorm, GloveWraps,
        // Void weapons built from real models (Sketchfab, CC-BY)
        ModelBlade, ModelScythe, ModelRifle,
        ModelDual } // a real-model blade held in each hand

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
        AbyssTalon, InfernoButterfly, FrostBayonet, PhantomSkeleton, BloodmoonKukri,
    }

    public enum SkinRarity { Default, Mythic, Void }

    // Every weapon skin in the game. Besides the defaults there are two rarities: Mythic and
    // Void. Mythic knives are a talon knife or butterfly knife with a flashy finish; Void knives
    // are knife-sized takes on legendary swords with glowing edges, an aura and their own
    // inspect. Cases drop Void 20% of the time.
    // Loadout slots: the sniper is the primary, the knife the secondary, gloves go on the hands
    public enum ItemSlot { Primary, Secondary, Hands }

    public static class Skins
    {
        public const float VoidChance = 0.2f;

        public readonly struct Skin
        {
            public readonly string name;
            public readonly KnifeModel model;
            public readonly KnifeFinish finish;
            public readonly SkinRarity rarity;
            public readonly string asset, credit; // real-model Void weapons only
            public readonly string paint;         // a colourway of the model (KarambitPaints), or null

            public Skin(string name, KnifeModel model, KnifeFinish finish, SkinRarity rarity)
            {
                this.name = name;
                this.model = model;
                this.finish = finish;
                this.rarity = rarity;
                asset = credit = paint = null;
            }

            // A Void weapon made from a real model: Resources/VoidModels/<asset>/fitted, by <credit>
            public Skin(string name, KnifeModel model, string asset, string credit)
            {
                this.name = name;
                this.model = model;
                finish = KnifeFinish.Polished;
                rarity = SkinRarity.Void;
                this.asset = asset;
                this.credit = credit;
                paint = null;
            }

            // A colourway of a real-model Void weapon: the same model, repainted
            public Skin(string name, KnifeModel model, string asset, string credit, string paint) : this(name, model, asset, credit) => this.paint = paint;

            // Void gloves painted to match a karambit (KarambitPaints.Gloves)
            public Skin(string name, string paint) : this(name, KnifeModel.Glove, KnifeFinish.Polished, SkinRarity.Void) => this.paint = paint;
        }

        public static bool IsRifle(KnifeModel m) => m is KnifeModel.ModelRifle or KnifeModel.Rifle or KnifeModel.Railgun or KnifeModel.Hellfire
            or KnifeModel.Prism or KnifeModel.Bone or KnifeModel.Lance or KnifeModel.Seraph
            || (m >= KnifeModel.Crescent && m <= KnifeModel.Glitch);

        // Each Void weapon's own colour: its swing trail, its draw's embers, the tint of the light
        // that shines on it
        public static Color VoidHue(string asset) => (asset != null && asset.Length >= 2 ? asset.Substring(0, 2) : "") switch
        {
            "01" => new Color(1f, 0.78f, 0.3f), "02" => new Color(1f, 0.15f, 0.2f), "03" => new Color(1f, 0.2f, 0.35f),
            "05" => new Color(0.95f, 0.12f, 0.18f), "06" => new Color(0.7f, 0.3f, 1f), "07" => new Color(1f, 0.6f, 0.2f),
            "08" => new Color(1f, 0.72f, 0.38f), "09" => new Color(0.3f, 1f, 0.6f), "10" => new Color(1f, 0.38f, 0.65f),
            "11" => new Color(1f, 0.45f, 0.15f), "12" => new Color(0.3f, 1f, 0.7f),
            "14" => new Color(0.45f, 0.5f, 1f), "15" => new Color(0.3f, 0.9f, 1f), "16" => new Color(1f, 0.85f, 0.5f),
            "17" => new Color(0.3f, 0.6f, 1f), "18" => new Color(1f, 0.55f, 0.15f), "19" => new Color(0.6f, 0.8f, 1f),
            "21" => new Color(1f, 0.5f, 0.2f), "24" => new Color(0.3f, 0.8f, 1f), "26" => new Color(1f, 0.25f, 0.2f),
            "28" => new Color(0.55f, 0.75f, 1f), "29" => new Color(0.2f, 1f, 0.85f), "30" => new Color(1f, 0.25f, 0.75f),
            "31" => new Color(0.2f, 1f, 1f), "32" => new Color(0.6f, 0.95f, 1f), "33" => new Color(0.45f, 1f, 0.2f),
            "34" => new Color(0.3f, 1f, 0.7f), "35" => new Color(1f, 0.15f, 0.15f),
            _ => new Color(0.75f, 0.4f, 1f),
        };
        // A skin's own colour: its colourway's, or its model's
        public static Color HueOf(Skin skin) => IsGlove(skin.model) && KarambitPaints.HasGlove(skin.paint) ? KarambitPaints.GloveHue(skin.paint)
            : KarambitPaints.Has(skin.paint) ? KarambitPaints.Hue(skin.paint) : VoidHue(skin.asset);

        // The Void karambits are held and spun like the talon knife (reverse grip, the finger ring
        // above the index finger)
        public static bool IsKarambit(string asset) => asset != null && (asset.StartsWith("30") || asset.StartsWith("35"));
        public static bool TalonHeld(Skin skin) => skin.model == KnifeModel.Talon || IsKarambit(skin.asset);
        public static bool IsVoidKnife(string asset) => asset != null && string.CompareOrdinal(asset, "28") >= 0 && string.CompareOrdinal(asset, "36") < 0;

        public static bool IsGlove(KnifeModel m) => m >= KnifeModel.Glove && m <= KnifeModel.GloveWraps;

        public static Skin[] Pool(ItemSlot slot) => slot switch { ItemSlot.Primary => Snipers, ItemSlot.Hands => Gloves, _ => Knives };
        public static string SlotName(ItemSlot slot) => slot switch { ItemSlot.Primary => "PRIMARY", ItemSlot.Hands => "HANDS", _ => "SECONDARY" };
        public static string Noun(ItemSlot slot) => slot switch { ItemSlot.Primary => "SNIPER", ItemSlot.Hands => "GLOVES", _ => "KNIFE" };
        // Items are saved by name, so the lists can change without mixing up what people own;
        // saves from before (by place in the list) are read through the old lists' names
        public static int IndexOf(ItemSlot slot, string name)
        {
            var pool = Pool(slot);
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].name == name) return i;
            return -1;
        }

        public static string LegacyName(ItemSlot slot, int index)
        {
            var names = LegacySkinNames.Of(slot);
            return index >= 0 && index < names.Length ? names[index] : null;
        }

        static int Saved(string key, ItemSlot slot)
        {
            string name = PlayerPrefs.GetString(key + "Name", null);
            if (string.IsNullOrEmpty(name)) name = LegacyName(slot, PlayerPrefs.GetInt(key, 0));
            return Mathf.Max(0, name != null ? IndexOf(slot, name) : 0);
        }

        public static int Equipped(ItemSlot slot) => slot switch { ItemSlot.Primary => EquippedSniper, ItemSlot.Hands => EquippedGlove, _ => EquippedKnife };

        // What kind of item a skin is, for cards
        public static string KindName(Skin skin) => skin.model switch
        {
            var m when IsGlove(m) => skin.rarity == SkinRarity.Void ? "VOID GLOVES" : "GLOVES",
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
            KnifeModel.ModelScythe => "VOID SCYTHE",
            KnifeModel.ModelDual => "VOID TWIN BLADES",
            KnifeModel.ModelRifle => "VOID SNIPER",
            _ => IsRifle(skin.model) ? "VOID RIFLE" : IsVoidKnife(skin.asset) ? "VOID KNIFE" : "VOID BLADE",
        };

        // Gloves: the default black pair, Mythic finishes, and Void gloves to match the karambits
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
            // Void: a pair for each karambit, painted in its design
            new("Void Gloves | Rubi", "rubi"),
            new("Void Gloves | Crimson", "crimson"),
            new("Void Gloves | Blackout", "blackout"),
            new("Void Gloves | Whiteout", "whiteout"),
            new("Void Gloves | Sunset Fade", "sunset"),
            new("Void Gloves | Abyss Sapphire", "sapphire"),
            new("Void Gloves | Emerald Venom", "emerald"),
            new("Void Gloves | Fire & Ice", "fireice"),
            new("Void Gloves | Velocity", "velocity"), // (the bhop challenge's prize)
        };

        public static int EquippedGlove
        {
            get => Saved("VoidFlow.Glove", ItemSlot.Hands);
            set { PlayerPrefs.SetString("VoidFlow.GloveName", Pool(ItemSlot.Hands)[Mathf.Clamp(value, 0, Gloves.Length - 1)].name); PlayerPrefs.Save(); }
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
            // Classic fixed blades: our own takes on the real kukri, M9 bayonet and skeleton knife
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
            // Void versions of the classics: damascus steel burning at the edge, an aura and flames
            // Void: real models from Sketchfab (CC-BY 4.0, credited in CREDITS.md)
            new("Gold Skull Glory Sword", KnifeModel.ModelBlade, "01_gold_skull_glory_sword", "nodgerty"),
            new("Desolate Devil Scythe", KnifeModel.ModelScythe, "02_desolate_devil_scythe", "nodgerty"),
            new("Bloody Rose Sword", KnifeModel.ModelBlade, "03_bloody_rose_sword", "nodgerty"),
            new("Abyssal Heart", KnifeModel.ModelBlade, "05_abyssal_heart", "nodgerty"),
            new("Demonic Twinblades", KnifeModel.ModelDual, "06_demonic_twinblades", "nodgerty"),
            new("Sword of Golden Blood", KnifeModel.ModelBlade, "07_golden_blood", "nodgerty"),
            new("Da Vinci's Sword", KnifeModel.ModelBlade, "08_steampunk_sword", "nodgerty"),
            new("Jade Sword", KnifeModel.ModelBlade, "09_jade_sword", "Ole Gunnar Isager"),
            new("Shattered Crystal Sword", KnifeModel.ModelBlade, "10_shattered_crystal", "WizOfFab"),
            new("Demon Sword", KnifeModel.ModelBlade, "11_demon_sword", "kyrylyushkov"),
            new("Soulsucker", KnifeModel.ModelBlade, "12_soulsucker", "tuomaspaul"),
            new("Gradient Fantasy Sword", KnifeModel.ModelBlade, "14_gradient_sword", "Mikolaj Michalak"),
            new("Cyber Blade", KnifeModel.ModelBlade, "15_cyber_blade", "jordanger88"),
            new("Divine Reaper", KnifeModel.ModelScythe, "16_divine_reaper", "amunozs"),
            new("Squid Dagger", KnifeModel.ModelBlade, "17_squid_dagger", "DigitalBirb"),
            new("Autumn Sword", KnifeModel.ModelBlade, "18_autumn_sword", "SimberGI"),
            // Void knives
            new("Ice Cyclone Blade", KnifeModel.ModelBlade, "28_ice_cyclone", "cyanidecoffee"),
            new("Arcane Crystal Dagger", KnifeModel.ModelBlade, "29_crystal_fantasy", "Shaz"),
            new("Karambit Rubi", KnifeModel.ModelBlade, "30_karambit_rubi", "Diego Clemente"),
            new("Cyberpunk Knife", KnifeModel.ModelBlade, "31_cyberpunk_knife", "re1monsen"),
            new("Miraigata Kunai", KnifeModel.ModelBlade, "32_miraigata_kunai", "Tino Hunda"),
            new("Fel Whisper", KnifeModel.ModelBlade, "33_fel_whisper", "KodaWowo"),
            new("Tidal Crystal Dagger", KnifeModel.ModelBlade, "34_crystal_dagger", "Dekkaebi"),
            new("Crimson Karambit", KnifeModel.ModelBlade, "35_karambit_red", "AvnisT"),
            // Karambit colourways (the Karambit Rubi repainted, see KarambitPaints)
            new("Karambit | Blackout", KnifeModel.ModelBlade, "30_karambit_rubi", "Diego Clemente", "blackout"),
            new("Karambit | Whiteout", KnifeModel.ModelBlade, "30_karambit_rubi", "Diego Clemente", "whiteout"),
            new("Karambit | Sunset Fade", KnifeModel.ModelBlade, "30_karambit_rubi", "Diego Clemente", "sunset"),
            new("Karambit | Abyss Sapphire", KnifeModel.ModelBlade, "30_karambit_rubi", "Diego Clemente", "sapphire"),
            new("Karambit | Emerald Venom", KnifeModel.ModelBlade, "30_karambit_rubi", "Diego Clemente", "emerald"),
            new("Karambit | Fire & Ice", KnifeModel.ModelBlade, "30_karambit_rubi", "Diego Clemente", "fireice"),
            // The bhop challenge's prize (see Exclusive)
            new("Karambit | Velocity", KnifeModel.ModelBlade, "30_karambit_rubi", "Diego Clemente", "velocity"),
        };

        public static readonly Skin[] Snipers =
        {
            new("Longreach", KnifeModel.Rifle, KnifeFinish.Polished, SkinRarity.Default),
            new("Longreach | Nebula", KnifeModel.Rifle, KnifeFinish.Nebula, SkinRarity.Mythic),
            new("Longreach | Amber Stripe", KnifeModel.Rifle, KnifeFinish.AmberStripe, SkinRarity.Mythic),
            new("Longreach | Candy Swirl", KnifeModel.Rifle, KnifeFinish.CandySwirl, SkinRarity.Mythic),
            new("Longreach | Red Web", KnifeModel.Rifle, KnifeFinish.RedWeb, SkinRarity.Mythic),
            // Void snipers: every one its own design (Spectrum cycles through every color)
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
            // Void: real models from Sketchfab (CC-BY 4.0, credited in CREDITS.md)
            new("Sci-Fi Sniper", KnifeModel.ModelRifle, "19_scifi_sniper", "Matija Svaco"),
            new("Futuristic Sniper", KnifeModel.ModelRifle, "21_futuristic_sniper", "trolosqlfod"),
            new("Renegade Railgun", KnifeModel.ModelRifle, "24_renegade_railgun", "Bl4ckGh0st"),
            new("Nexus Railgun", KnifeModel.ModelRifle, "26_nexus_railgun", "Bl4ckGh0st"),
        };

        // Only won, never dropped or given: the bhop challenge's Karambit | Velocity and its gloves
        public static bool Exclusive(Skin skin) => skin.paint == "velocity";

        // Picks a case drop: Void 20% of the time, otherwise a Mythic, evenly within each
        public static int Roll(Skin[] pool)
        {
            var wanted = Random.value < VoidChance ? SkinRarity.Void : SkinRarity.Mythic;
            int count = 0;
            foreach (var s in pool) if (s.rarity == wanted && !Exclusive(s)) count++;
            if (count == 0) // (no Void items of this kind: a Mythic one)
            {
                wanted = SkinRarity.Mythic;
                foreach (var s in pool) if (s.rarity == wanted && !Exclusive(s)) count++;
            }
            int pick = Random.Range(0, count);
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].rarity == wanted && !Exclusive(pool[i]) && pick-- == 0) return i;
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
            get => Saved("VoidFlow.Knife", ItemSlot.Secondary);
            set { PlayerPrefs.SetString("VoidFlow.KnifeName", Pool(ItemSlot.Secondary)[Mathf.Clamp(value, 0, Knives.Length - 1)].name); PlayerPrefs.Save(); }
        }

        public static int EquippedSniper
        {
            get => Saved("VoidFlow.Sniper", ItemSlot.Primary);
            set { PlayerPrefs.SetString("VoidFlow.SniperName", Pool(ItemSlot.Primary)[Mathf.Clamp(value, 0, Snipers.Length - 1)].name); PlayerPrefs.Save(); }
        }
    }
}
