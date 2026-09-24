using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using EXFIL.AI;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Economy;
using EXFIL.Hideout;
using EXFIL.Items;
using EXFIL.Progression;
using EXFIL;

namespace EXFILEditor
{
    /// <summary>
    /// Creates every ScriptableObject asset the game needs (items, operators, modules,
    /// recipes, plants, traders, bots, quests) under Assets/Data. Idempotent.
    /// </summary>
    public static class ContentSeeder
    {
        private const string Root = "Assets/Data";
        private static int _created;
        private static int _skipped;
        private static readonly Dictionary<string, ItemDefinition> _itemsById = new Dictionary<string, ItemDefinition>();

        /// <summary>Items are resolved locally first: the database asset is not filled yet while seeding.</summary>
        private static ItemDefinition FindItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            ItemDefinition local;
            if (_itemsById.TryGetValue(id, out local)) return local;
            return ItemDatabase.Find(id);
        }

        public static void SeedAll()
        {
            _created = 0;
            _skipped = 0;

            Directory.CreateDirectory(Root + "/Items");
            Directory.CreateDirectory(Root + "/Characters");
            Directory.CreateDirectory(Root + "/Hideout");
            Directory.CreateDirectory(Root + "/Traders");
            Directory.CreateDirectory(Root + "/Bots");
            Directory.CreateDirectory(Root + "/Quests");

            SeedCurrency();
            SeedAmmo();
            SeedMagazines();
            SeedWeapons();
            SeedArmor();
            SeedBackpacks();
            SeedMedical();
            SeedConsumables();
            SeedBarter();
            SeedPlants();
            SeedModules();
            SeedRecipes();
            SeedCharacters();
            SeedBots();
            SeedTraders();
            SeedQuests();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EXFIL] Content seeded. Created: " + _created + ", skipped (already exist): " + _skipped);
        }

        // ------------------------------------------------------------------ helpers
        private static T Asset<T>(T asset, string relativePath) where T : ScriptableObject
        {
            string path = Root + "/" + relativePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                _skipped++;
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            ItemDefinition item = asset as ItemDefinition;
            if (item != null && !string.IsNullOrEmpty(item.Id)) _itemsById[item.Id] = item;
            _created++;
            return asset;
        }

        private static BarterItemDefinition Barter(string id, string name, int price, float weight, Vector2Int size, ItemRarity rarity = ItemRarity.Common, int stack = 1)
        {
            BarterItemDefinition def = ScriptableObject.CreateInstance<BarterItemDefinition>();
            def.Id = id; def.DisplayName = name; def.ShortName = name;
            def.BasePrice = price; def.Weight = weight; def.Size = size; def.Rarity = rarity; def.MaxStack = stack;
            return Asset(def, "Items/" + id + ".asset");
        }

        private static CurrencyItemDefinition Currency(string id, string name, int valuePerUnit)
        {
            CurrencyItemDefinition def = ScriptableObject.CreateInstance<CurrencyItemDefinition>();
            def.Id = id; def.DisplayName = name; def.ShortName = name;
            def.BasePrice = valuePerUnit; def.Weight = 0.001f; def.Size = new Vector2Int(1, 1);
            def.MaxStack = 1_000_000;
            return Asset(def, "Items/" + id + ".asset");
        }

        private static AmmoItemDefinition Ammo(string id, string name, AmmoCaliber caliber, float damage, float pen,
            float armorDamage, float velocity, int price, ItemRarity rarity = ItemRarity.Common)
        {
            AmmoItemDefinition def = ScriptableObject.CreateInstance<AmmoItemDefinition>();
            def.Id = id; def.DisplayName = name; def.ShortName = name;
            def.Caliber = caliber; def.Damage = damage; def.Penetration = pen;
            def.ArmorDamagePercent = armorDamage; def.Velocity = velocity;
            def.BasePrice = price; def.Weight = 0.012f; def.Size = new Vector2Int(1, 1);
            def.MaxStack = 60; def.Rarity = rarity;
            return Asset(def, "Items/Ammo/" + id + ".asset");
        }

        private static MagazineItemDefinition Magazine(string id, string name, AmmoCaliber caliber, int capacity, int price)
        {
            MagazineItemDefinition def = ScriptableObject.CreateInstance<MagazineItemDefinition>();
            def.Id = id; def.DisplayName = name; def.ShortName = name;
            def.Caliber = caliber; def.Capacity = capacity; def.BasePrice = price;
            def.Weight = 0.12f; def.Size = new Vector2Int(1, 2);
            return Asset(def, "Items/Magazines/" + id + ".asset");
        }

        private static WeaponItemDefinition Weapon(string id, string name, WeaponClass cls, AmmoCaliber caliber,
            int rpm, float damage, int ergo, float recoil, int price, string magId, string ammoId, Vector2Int size)
        {
            WeaponItemDefinition def = ScriptableObject.CreateInstance<WeaponItemDefinition>();
            def.Id = id; def.DisplayName = name; def.ShortName = name;
            def.WeaponClass = cls; def.Caliber = caliber; def.RoundsPerMinute = rpm;
            def.Ergonomics = ergo; def.VerticalRecoil = recoil; def.HorizontalRecoil = recoil * 0.5f;
            def.BasePrice = price; def.Weight = 3.4f; def.Size = size;
            def.DefaultMagazine = FindItem(magId) as MagazineItemDefinition;
            def.DefaultAmmo = FindItem(ammoId) as AmmoItemDefinition;
            def.AllowedModules = new List<AttachmentSlot> { AttachmentSlot.Optic, AttachmentSlot.Muzzle, AttachmentSlot.Foregrip, AttachmentSlot.Stock };
            return Asset(def, "Items/Weapons/" + id + ".asset");
        }

        private static ArmorItemDefinition Armor(string id, string name, ArmorClass armorClass, ArmorMaterial material,
            float durability, BodyZone zones, int price, ItemType type = ItemType.Armor)
        {
            ArmorItemDefinition def = ScriptableObject.CreateInstance<ArmorItemDefinition>();
            def.Id = id; def.DisplayName = name; def.ShortName = name;
            def.Type = type; def.ArmorClass = armorClass; def.Material = material;
            def.MaxDurability = durability; def.Zones = zones;
            def.MoveSpeedPenalty = (int)armorClass * 0.008f;
            def.TurnSpeedPenalty = (int)armorClass * 0.010f;
            def.ErgonomicsPenalty = (int)armorClass * 1.5f;
            def.BasePrice = price; def.Weight = (int)armorClass * 2.1f;
            def.Size = new Vector2Int(2, 2);
            return Asset(def, "Items/Armor/" + id + ".asset");
        }

        // ------------------------------------------------------------------- content
        private static void SeedCurrency()
        {
            Currency("money_rouble", "Roubles", 1);
            Currency("money_dollar", "Dollars", 120);
            Currency("money_euro", "Euros", 135);
        }

        private static void SeedAmmo()
        {
            Ammo("ammo_545_ps", "5.45x39 PS", AmmoCaliber._545x39, 42f, 24f, 0.40f, 880f, 60);
            Ammo("ammo_545_bt", "5.45x39 BT", AmmoCaliber._545x39, 44f, 37f, 0.44f, 890f, 110, ItemRarity.Rare);
            Ammo("ammo_545_bs", "5.45x39 BS", AmmoCaliber._545x39, 46f, 51f, 0.48f, 900f, 220, ItemRarity.Epic);
            Ammo("ammo_556_m855", "5.56x45 M855", AmmoCaliber._556x45NATO, 46f, 30f, 0.42f, 940f, 95);
            Ammo("ammo_556_m855a1", "5.56x45 M855A1", AmmoCaliber._556x45NATO, 48f, 44f, 0.46f, 950f, 210, ItemRarity.Epic);
            Ammo("ammo_762x39_ps", "7.62x39 PS", AmmoCaliber._762x39, 52f, 30f, 0.48f, 730f, 90);
            Ammo("ammo_762x39_bp", "7.62x39 BP", AmmoCaliber._762x39, 56f, 47f, 0.52f, 750f, 230, ItemRarity.Epic);
            Ammo("ammo_9x19_pst", "9x19 PST Gzh", AmmoCaliber._9x19Para, 54f, 20f, 0.36f, 400f, 70);
            Ammo("ammo_9x19_ap", "9x19 AP 6.3", AmmoCaliber._9x19Para, 52f, 39f, 0.42f, 420f, 190, ItemRarity.Rare);
            Ammo("ammo_12_buckshot", "12/70 buckshot", AmmoCaliber._12gauge, 8.5f, 2f, 0.20f, 340f, 40);
            Ammo("ammo_12_slug", "12/70 slug", AmmoCaliber._12gauge, 165f, 34f, 0.55f, 430f, 120, ItemRarity.Rare);
            Ammo("ammo_762x54_lps", "7.62x54R LPS", AmmoCaliber._762x54R, 81f, 55f, 0.60f, 830f, 240, ItemRarity.Epic);
            Ammo("ammo_9x39_sp6", "9x39 SP-6", AmmoCaliber._9x39, 58f, 46f, 0.50f, 305f, 200, ItemRarity.Epic);
            Ammo("ammo_9x18_ps", "9x18 PM PS", AmmoCaliber._9x18PM, 50f, 18f, 0.32f, 330f, 55);
        }

        private static void SeedMagazines()
        {
            Magazine("mag_ak74_30", "AK-74 30-round magazine", AmmoCaliber._545x39, 30, 3200);
            Magazine("mag_ak74_45", "RPK-74 45-round magazine", AmmoCaliber._545x39, 45, 7400);
            Magazine("mag_akm_30", "AKM 30-round magazine", AmmoCaliber._762x39, 30, 3600);
            Magazine("mag_stanag_30", "STANAG 30-round magazine", AmmoCaliber._556x45NATO, 30, 4200);
            Magazine("mag_mp5_30", "MP5 30-round magazine", AmmoCaliber._9x19Para, 30, 3100);
            Magazine("mag_glock_17", "Glock 17-round magazine", AmmoCaliber._9x19Para, 17, 1900);
            Magazine("mag_svd_10", "SVD 10-round magazine", AmmoCaliber._762x54R, 10, 6100);
            Magazine("mag_saiga_8", "Saiga-12 8-shell magazine", AmmoCaliber._12gauge, 8, 5400);
            Magazine("mag_pm_8", "PM 8-round magazine", AmmoCaliber._9x18PM, 8, 1100);
            Magazine("mag_vss_10", "VSS 10-round magazine", AmmoCaliber._9x39, 10, 6800);
        }

        private static void SeedWeapons()
        {
            Weapon("wpn_ak74n", "AK-74N", WeaponClass.AssaultRifle, AmmoCaliber._545x39, 650, 42f, 44, 118f, 34000, "mag_ak74_30", "ammo_545_ps", new Vector2Int(5, 2));
            Weapon("wpn_akm", "AKM", WeaponClass.AssaultRifle, AmmoCaliber._762x39, 600, 52f, 40, 142f, 31000, "mag_akm_30", "ammo_762x39_ps", new Vector2Int(5, 2));
            Weapon("wpn_aks74u", "AKS-74U", WeaponClass.AssaultCarbine, AmmoCaliber._545x39, 700, 40f, 52, 128f, 27000, "mag_ak74_30", "ammo_545_ps", new Vector2Int(4, 2));
            Weapon("wpn_vss", "VSS Vintorez", WeaponClass.MarksmanRifle, AmmoCaliber._9x39, 700, 58f, 48, 96f, 62000, "mag_vss_10", "ammo_9x39_sp6", new Vector2Int(5, 2));
            Weapon("wpn_saiga12", "Saiga-12K", WeaponClass.Shotgun, AmmoCaliber._12gauge, 300, 8.5f, 38, 190f, 29000, "mag_saiga_8", "ammo_12_buckshot", new Vector2Int(5, 2));
            Weapon("wpn_svd", "SVD-S", WeaponClass.SniperRifle, AmmoCaliber._762x54R, 300, 81f, 30, 210f, 78000, "mag_svd_10", "ammo_762x54_lps", new Vector2Int(6, 2));
            Weapon("wpn_bizon", "PP-19 Bizon", WeaponClass.SMG, AmmoCaliber._9x19Para, 700, 54f, 55, 88f, 24000, "mag_mp5_30", "ammo_9x19_pst", new Vector2Int(4, 2));
            Weapon("wpn_makarov", "Makarov PM", WeaponClass.Pistol, AmmoCaliber._9x18PM, 500, 50f, 68, 74f, 6400, "mag_pm_8", "ammo_9x18_ps", new Vector2Int(2, 1));
            Weapon("wpn_m4a1", "M4A1", WeaponClass.AssaultRifle, AmmoCaliber._556x45NATO, 700, 46f, 52, 92f, 58000, "mag_stanag_30", "ammo_556_m855", new Vector2Int(5, 2));
            Weapon("wpn_hk416", "HK 416A5", WeaponClass.AssaultRifle, AmmoCaliber._556x45NATO, 850, 46f, 49, 86f, 74000, "mag_stanag_30", "ammo_556_m855a1", new Vector2Int(5, 2));
            Weapon("wpn_mp5", "MP5", WeaponClass.SMG, AmmoCaliber._9x19Para, 800, 54f, 58, 74f, 33000, "mag_mp5_30", "ammo_9x19_pst", new Vector2Int(4, 2));
            Weapon("wpn_mp9", "MP9", WeaponClass.SMG, AmmoCaliber._9x19Para, 900, 54f, 62, 70f, 29000, "mag_mp5_30", "ammo_9x19_ap", new Vector2Int(3, 2));
            Weapon("wpn_m870", "Remington 870", WeaponClass.Shotgun, AmmoCaliber._12gauge, 120, 8.5f, 35, 205f, 19000, "mag_saiga_8", "ammo_12_slug", new Vector2Int(5, 2));
            Weapon("wpn_m700", "M700", WeaponClass.SniperRifle, AmmoCaliber._762x51NATO, 40, 78f, 26, 232f, 66000, "mag_svd_10", "ammo_762x54_lps", new Vector2Int(6, 2));
            Weapon("wpn_glock17", "Glock 17", WeaponClass.Pistol, AmmoCaliber._9x19Para, 600, 54f, 71, 66f, 12000, "mag_glock_17", "ammo_9x19_pst", new Vector2Int(2, 1));
            Weapon("wpn_fiveseven", "FN Five-seveN", WeaponClass.Pistol, AmmoCaliber._9x19Para, 650, 52f, 74, 62f, 17000, "mag_glock_17", "ammo_9x19_ap", new Vector2Int(2, 1));
        }

        private static void SeedArmor()
        {
            Armor("armor_paca", "PACA soft armor", ArmorClass.Two, ArmorMaterial.Aramid, 45f, BodyZone.Thorax | BodyZone.Stomach, 24000);
            Armor("armor_6b23", "6B23-1 armor", ArmorClass.Three, ArmorMaterial.Steel, 62f, BodyZone.Thorax | BodyZone.Stomach, 48000);
            Armor("armor_zhuk", "Zhuk-3 press armor", ArmorClass.Three, ArmorMaterial.UHMWPE, 58f, BodyZone.Thorax | BodyZone.Stomach, 56000);
            Armor("armor_6b13", "6B13 assault armor", ArmorClass.Four, ArmorMaterial.Ceramic, 70f, BodyZone.Thorax | BodyZone.Stomach, 92000);
            Armor("armor_trooper", "Trooper armor", ArmorClass.Four, ArmorMaterial.Combined, 78f, BodyZone.Thorax | BodyZone.Stomach, 118000);
            Armor("armor_fort", "Fort Redut-M", ArmorClass.Five, ArmorMaterial.Steel, 85f, BodyZone.Thorax | BodyZone.Stomach, 186000);
            Armor("armor_slick", "Slick plate carrier", ArmorClass.Six, ArmorMaterial.Titanium, 80f, BodyZone.Thorax | BodyZone.Stomach, 340000, ItemType.Armor);

            Armor("helmet_6b47", "6B47 helmet", ArmorClass.Three, ArmorMaterial.Aramid, 35f, BodyZone.Head, 39000, ItemType.Helmet);
            Armor("helmet_fast", "FAST MT helmet", ArmorClass.Four, ArmorMaterial.UHMWPE, 42f, BodyZone.Head, 78000, ItemType.Helmet);
            Armor("helmet_altyn", "Altyn helmet", ArmorClass.Five, ArmorMaterial.Titanium, 55f, BodyZone.Head, 164000, ItemType.Helmet);
            Armor("helmet_ssh68", "SSh-68 helmet", ArmorClass.Three, ArmorMaterial.Steel, 40f, BodyZone.Head, 21000, ItemType.Helmet);

            Armor("rig_blackrock", "BlackRock chest rig", ArmorClass.None, ArmorMaterial.None, 0f, BodyZone.None, 18000, ItemType.ChestRig).RigGridSize = new Vector2Int(3, 3);
            Armor("rig_mk3", "SSO MK3 rig", ArmorClass.None, ArmorMaterial.None, 0f, BodyZone.None, 26000, ItemType.ChestRig).RigGridSize = new Vector2Int(4, 3);
            Armor("rig_tv110", "TV-110 rig", ArmorClass.None, ArmorMaterial.None, 0f, BodyZone.None, 44000, ItemType.ChestRig).RigGridSize = new Vector2Int(5, 3);
        }

        private static void SeedBackpacks()
        {
            ContainerItemDefinition bp = ScriptableObject.CreateInstance<ContainerItemDefinition>();
            bp.Id = "bp_scav"; bp.DisplayName = "Scav backpack"; bp.GridSize = new Vector2Int(4, 4);
            bp.BasePrice = 9000; bp.Weight = 1.2f; bp.Size = new Vector2Int(2, 3);
            Asset(bp, "Items/Containers/bp_scav.asset");

            bp = ScriptableObject.CreateInstance<ContainerItemDefinition>();
            bp.Id = "bp_berkut"; bp.DisplayName = "Berkut backpack"; bp.GridSize = new Vector2Int(5, 5);
            bp.BasePrice = 34000; bp.Weight = 1.6f; bp.Size = new Vector2Int(3, 3); bp.WeightReduction = 0.05f;
            Asset(bp, "Items/Containers/bp_berkut.asset");

            bp = ScriptableObject.CreateInstance<ContainerItemDefinition>();
            bp.Id = "bp_trizip"; bp.DisplayName = "Tri-Zip backpack"; bp.GridSize = new Vector2Int(5, 6);
            bp.BasePrice = 62000; bp.Weight = 1.9f; bp.Size = new Vector2Int(3, 4); bp.WeightReduction = 0.09f;
            Asset(bp, "Items/Containers/bp_trizip.asset");

            bp = ScriptableObject.CreateInstance<ContainerItemDefinition>();
            bp.Id = "bp_pilgrim"; bp.DisplayName = "Pilgrim backpack"; bp.GridSize = new Vector2Int(6, 6);
            bp.BasePrice = 118000; bp.Weight = 2.4f; bp.Size = new Vector2Int(3, 4); bp.WeightReduction = 0.12f;
            Asset(bp, "Items/Containers/bp_pilgrim.asset");

            ContainerItemDefinition secure = ScriptableObject.CreateInstance<ContainerItemDefinition>();
            secure.Id = "secure_alpha"; secure.DisplayName = "Alpha container";
            secure.Kind = ContainerItemDefinition.ContainerKind.Secure;
            secure.GridSize = new Vector2Int(2, 2); secure.BasePrice = 0; secure.Weight = 0.4f;
            secure.Size = new Vector2Int(1, 1);
            Asset(secure, "Items/Containers/secure_alpha.asset");
        }

        private static void SeedMedical()
        {
            Med("med_bandage", "Bandage", 60f, 1.5f, 2, 3200, HealthEffectType.LightBleed);
            Med("med_ifak", "IFAK personal tactical first aid kit", 220f, 3.5f, 1, 34000, HealthEffectType.LightBleed, HealthEffectType.HeavyBleed);
            Med("med_salewa", "Salewa first aid kit", 385f, 5.5f, 1, 48000, HealthEffectType.LightBleed, HealthEffectType.HeavyBleed);
            Med("med_grizzly", "Grizzly medical kit", 900f, 6.5f, 1, 124000, HealthEffectType.LightBleed, HealthEffectType.HeavyBleed, HealthEffectType.Fracture, HealthEffectType.Pain);
            Med("med_painkiller", "Analgin painkillers", 0f, 2f, 4, 6200, HealthEffectType.Pain).PainkillerDuration = 180f;
            Med("med_morphine", "Morphine injector", 0f, 2.5f, 3, 24000, HealthEffectType.Pain, HealthEffectType.Contusion).PainkillerDuration = 420f;
            Med("med_splint", "Aluminium splint", 0f, 2.2f, 2, 18000, HealthEffectType.Fracture);
            Med("med_cms", "CMS surgical kit", 0f, 4f, 5, 62000, HealthEffectType.None);
            Med("med_vaseline", "Vaseline balm", 0f, 2.5f, 4, 14000, HealthEffectType.Contusion, HealthEffectType.Intoxication);
        }

        private static MedicalItemDefinition Med(string id, string name, float resource, float useTime, int uses, int price, params HealthEffectType[] removes)
        {
            MedicalItemDefinition def = ScriptableObject.CreateInstance<MedicalItemDefinition>();
            def.Id = id; def.DisplayName = name; def.ShortName = name;
            def.MaxResource = resource; def.HealPerSecond = resource / Mathf.Max(0.5f, useTime);
            def.UseTime = useTime; def.Uses = uses; def.BasePrice = price;
            def.Weight = 0.25f; def.Size = new Vector2Int(1, 1); def.MaxStack = uses > 1 ? 5 : 1;
            def.Removes = new List<HealthEffectType>();
            for (int i = 0; i < removes.Length; i++)
                if (removes[i] != HealthEffectType.None) def.Removes.Add(removes[i]);
            return Asset(def, "Items/Medical/" + id + ".asset");
        }

        private static void SeedConsumables()
        {
            Food("food_water", "Bottle of water", 0f, 45f, 3f, 1, 8000, true);
            Food("food_crackers", "Crackers", 26f, -3f, 3f, 1, 6400, false);
            Food("food_condensed", "Condensed milk", 48f, 6f, 4f, 1, 12000, false);
            Food("food_stew", "Military stew", 62f, 14f, 5f, 1, 24000, false);
            Food("food_energy", "Energy drink", 12f, 10f, 2.5f, 1, 18000, true);
            Food("food_chocolate", "Chocolate bar", 22f, -4f, 2f, 1, 9000, false);
        }

        private static ConsumableItemDefinition Food(string id, string name, float energy, float hydration, float useTime, int uses, int price, bool drink)
        {
            ConsumableItemDefinition def = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            def.Id = id; def.DisplayName = name; def.ShortName = name;
            def.Energy = energy; def.Hydration = hydration; def.UseTime = useTime;
            def.Uses = uses; def.BasePrice = price; def.IsDrink = drink;
            def.Weight = 0.3f; def.Size = new Vector2Int(1, 1); def.MaxStack = 5;
            return Asset(def, "Items/Consumables/" + id + ".asset");
        }

        private static void SeedBarter()
        {
            Barter("barter_wires", "Wires", 12000, 0.1f, new Vector2Int(1, 1), ItemRarity.Common, 20);
            Barter("barter_bolts", "Bolts", 9000, 0.08f, new Vector2Int(1, 1), ItemRarity.Common, 20);
            Barter("barter_screws", "Screws", 8000, 0.06f, new Vector2Int(1, 1), ItemRarity.Common, 20);
            Barter("barter_filter", "Water filter", 46000, 0.5f, new Vector2Int(1, 1), ItemRarity.Rare);
            Barter("barter_gasanalyser", "Gas analyser", 72000, 0.9f, new Vector2Int(1, 2), ItemRarity.Rare);
            Barter("barter_goldchain", "Gold chain", 62000, 0.2f, new Vector2Int(1, 1), ItemRarity.Epic);
            Barter("barter_ledx", "LEDX skin transilluminator", 640000, 0.3f, new Vector2Int(1, 1), ItemRarity.Legendary);
            Barter("barter_fuel", "Metal fuel tank", 88000, 1.2f, new Vector2Int(2, 2));
            Barter("barter_fuelcan", "Fuel canister", 24000, 0.8f, new Vector2Int(1, 2));
            Barter("barter_hose", "Corrugated hose", 14000, 0.4f, new Vector2Int(1, 1), ItemRarity.Common, 10);
            Barter("barter_weed", "Dried herbs bundle", 18000, 0.2f, new Vector2Int(1, 1), ItemRarity.Common, 10);
            Barter("barter_biomass", "Algae biomass", 41000, 0.6f, new Vector2Int(1, 1), ItemRarity.Rare);

            // greenhouse harvest (produced by the garden, consumed by crafting)
            Barter("harvest_potato", "Harvested potatoes", 4200, 0.3f, new Vector2Int(1, 1), ItemRarity.Common, 20);
            Barter("harvest_carrot", "Harvested carrots", 3600, 0.2f, new Vector2Int(1, 1), ItemRarity.Common, 20);
            Barter("harvest_herbs", "Harvested herbs", 6800, 0.1f, new Vector2Int(1, 1), ItemRarity.Common, 20);
            Barter("harvest_mushroom", "Harvested mushrooms", 9400, 0.2f, new Vector2Int(1, 1), ItemRarity.Common, 20);
            Barter("harvest_algae", "Harvested algae", 12000, 0.3f, new Vector2Int(1, 1), ItemRarity.Rare, 20);
            Barter("harvest_mint", "Harvested mint", 5200, 0.1f, new Vector2Int(1, 1), ItemRarity.Common, 20);
        }

        private static void SeedPlants()
        {
            Plant("plant_potato", "Potato", PlantKind.Vegetable, "harvest_potato", 900f, "seed_potato", 3, 6, 0.7f);
            Plant("plant_carrot", "Carrot", PlantKind.Vegetable, "harvest_carrot", 720f, "seed_carrot", 2, 5, 0.6f);
            Plant("plant_herbs", "Medicinal herbs", PlantKind.Herb, "harvest_herbs", 600f, "seed_herbs", 2, 4, 0.5f);
            Plant("plant_mushroom", "Cultivated mushrooms", PlantKind.Mushroom, "harvest_mushroom", 1080f, "seed_mushroom", 2, 5, 0.25f);
            Plant("plant_algae", "Technical algae", PlantKind.Technical, "harvest_algae", 1500f, "seed_algae", 1, 3, 0.4f);
            Plant("plant_tea", "Field mint", PlantKind.Herb, "harvest_mint", 840f, "seed_mint", 2, 4, 0.5f);
        }

        private static PlantDefinition Plant(string id, string name, PlantKind kind, string yieldItemId, float growTime,
            string seedId, int minYield, int maxYield, float light)
        {
            PlantDefinition def = ScriptableObject.CreateInstance<PlantDefinition>();
            def.Id = id; def.DisplayName = name; def.Kind = kind;
            def.YieldItemId = yieldItemId; def.GrowTimeSeconds = growTime;
            def.SeedItemId = seedId; def.MinYield = minYield; def.MaxYield = maxYield;
            def.LightRequirement = light; def.OptimalTemperature = 21f;
            def.WaterUsePerMinute = 0.7f; def.BasePrice = 0;
            Asset(def, "Hideout/Plants/" + id + ".asset");

            SeedItemDefinition seed = ScriptableObject.CreateInstance<SeedItemDefinition>();
            seed.Id = seedId; seed.DisplayName = name + " seeds"; seed.ShortName = "seeds";
            seed.PlantId = id; seed.BaseGrowTime = growTime;
            seed.MinYield = minYield; seed.MaxYield = maxYield; seed.HarvestItemId = yieldItemId;
            seed.BasePrice = 9000; seed.Weight = 0.02f; seed.Size = new Vector2Int(1, 1);
            Asset(seed, "Items/Seeds/" + seedId + ".asset");
            return def;
        }

        private static void SeedModules()
        {
            Module("generator", "Generator", HideoutModuleKind.Generator, 3, 0);
            Module("water_collector", "Water collector", HideoutModuleKind.WaterCollector, 3, 0);
            Module("workbench", "Workbench", HideoutModuleKind.Workbench, 3, 1);
            Module("medstation", "Medstation", HideoutModuleKind.Medstation, 3, 1);
            Module("nutrition_unit", "Nutrition unit", HideoutModuleKind.NutritionUnit, 3, 1);
            Module("lavatory", "Lavatory", HideoutModuleKind.Lavatory, 3, 1);
            Module("greenhouse", "Greenhouse", HideoutModuleKind.Greenhouse, 3, 2);
            Module("stash", "Stash", HideoutModuleKind.Stash, 4, 0);
            Module("vents", "Ventilation", HideoutModuleKind.Vents, 3, 0);
            Module("security", "Security", HideoutModuleKind.Security, 3, 0);
            Module("shooting_range", "Shooting range", HideoutModuleKind.ShootingRange, 2, 0);
            Module("solar_power", "Solar power", HideoutModuleKind.SolarPower, 2, 0);
            Module("rest_space", "Rest space", HideoutModuleKind.RestSpace, 3, 0);
            Module("heating", "Heating", HideoutModuleKind.Heating, 2, 0);
        }

        private static HideoutModuleDefinition Module(string id, string name, HideoutModuleKind kind, int levels, int craftSlots)
        {
            HideoutModuleDefinition def = ScriptableObject.CreateInstance<HideoutModuleDefinition>();
            def.Id = id; def.DisplayName = name; def.Kind = kind;
            def.Description = name + " - upgrade it to unlock bonuses.";
            def.Levels = new List<HideoutModuleLevel>();

            for (int l = 1; l <= levels; l++)
            {
                HideoutModuleLevel level = new HideoutModuleLevel
                {
                    MoneyCost = 25000 * l * l,
                    BuildTimeSeconds = 120f * l * l,
                    CraftingSlots = kind == HideoutModuleKind.Workbench || kind == HideoutModuleKind.Medstation ||
                                    kind == HideoutModuleKind.NutritionUnit || kind == HideoutModuleKind.Lavatory
                        ? (l >= 2 ? craftSlots : 1)
                        : 0,
                    PlantPlots = kind == HideoutModuleKind.Greenhouse ? (l == 1 ? 2 : l == 2 ? 4 : 6) : 0,
                    StashWidth = kind == HideoutModuleKind.Stash ? (l == 1 ? 2 : l == 2 ? 2 : l == 3 ? 2 : 2) : 0,
                    StashHeight = kind == HideoutModuleKind.Stash ? (l == 1 ? 4 : l == 2 ? 6 : l == 3 ? 8 : 10) : 0,
                    FuelConsumption = kind == HideoutModuleKind.Generator ? 0f : l * 0.02f,
                    CraftSpeedBonus = kind == HideoutModuleKind.Workbench ? l * 0.05f : 0f,
                    PlantGrowthBonus = kind == HideoutModuleKind.Greenhouse ? l * 0.12f : 0f,
                    ExperienceBonus = kind == HideoutModuleKind.RestSpace ? l * 0.03f : 0f,
                    Description = name + " level " + l
                };

                if (l > 1)
                    level.Requirements.Add(new ItemRequirement { ItemId = "barter_screws", Count = 4 * l });
                if (kind == HideoutModuleKind.Greenhouse)
                    level.Requirements.Add(new ItemRequirement { ItemId = "barter_hose", Count = 2 * l });
                if (kind == HideoutModuleKind.Generator)
                    level.Requirements.Add(new ItemRequirement { ItemId = "barter_fuelcan", Count = 2 });
                if (kind == HideoutModuleKind.WaterCollector)
                    level.Requirements.Add(new ItemRequirement { ItemId = "barter_filter", Count = l });

                def.Levels.Add(level);
            }
            return Asset(def, "Hideout/Modules/module_" + id + ".asset");
        }

        private static void SeedRecipes()
        {
            Recipe("recipe_bandage", "Craft a bandage", "med_bandage", 2, 45f, "lavatory", 1,
                new[] { "barter_wires" }, new[] { 1 });
            Recipe("recipe_water", "Purify water", "food_water", 3, 120f, "nutrition_unit", 1,
                new[] { "barter_filter" }, new[] { 1 });
            Recipe("recipe_ifak", "Assemble an IFAK", "med_ifak", 1, 300f, "medstation", 1,
                new[] { "barter_wires", "med_bandage", "med_painkiller" }, new[] { 2, 2, 1 });
            Recipe("recipe_salewa", "Assemble a Salewa kit", "med_salewa", 1, 420f, "medstation", 2,
                new[] { "med_ifak", "harvest_herbs", "med_vaseline" }, new[] { 1, 3, 1 });
            Recipe("recipe_stew", "Cook military stew", "food_stew", 2, 240f, "nutrition_unit", 2,
                new[] { "harvest_potato", "barter_fuelcan" }, new[] { 4, 1 });
            Recipe("recipe_ammo_bs", "Handload 5.45 BS", "ammo_545_bs", 30, 600f, "workbench", 2,
                new[] { "ammo_545_bt", "barter_bolts" }, new[] { 30, 2 });
            Recipe("recipe_ammo_bp", "Handload 7.62 BP", "ammo_762x39_bp", 30, 600f, "workbench", 3,
                new[] { "ammo_762x39_ps", "barter_screws" }, new[] { 30, 3 });
            Recipe("recipe_biomass", "Grow technical biomass", "barter_biomass", 2, 900f, "workbench", 2,
                new[] { "harvest_algae", "barter_filter" }, new[] { 3, 1 });
            Recipe("recipe_energy", "Brew an energy drink", "food_energy", 3, 180f, "nutrition_unit", 2,
                new[] { "harvest_mint", "food_water" }, new[] { 3, 1 });
            Recipe("recipe_herbs", "Dry medicinal herbs", "barter_weed", 4, 150f, "lavatory", 2,
                new[] { "harvest_herbs" }, new[] { 4 });
            Recipe("recipe_fuelcan", "Refill a fuel canister", "barter_fuelcan", 2, 480f, "workbench", 3,
                new[] { "barter_fuel", "barter_hose" }, new[] { 1, 1 });
            Recipe("recipe_grizzly", "Assemble a Grizzly kit", "med_grizzly", 1, 900f, "medstation", 3,
                new[] { "med_salewa", "med_morphine", "med_splint" }, new[] { 2, 1, 1 });
        }

        private static CraftRecipe Recipe(string id, string name, string outputId, int outputCount, float duration,
            string moduleId, int moduleLevel, string[] inputs, int[] counts)
        {
            CraftRecipe recipe = ScriptableObject.CreateInstance<CraftRecipe>();
            recipe.Id = id; recipe.DisplayName = name;
            recipe.OutputItemId = outputId; recipe.OutputCount = outputCount;
            recipe.DurationSeconds = duration; recipe.StationModuleId = moduleId; recipe.StationLevel = moduleLevel;
            recipe.Inputs = new List<ItemRequirement>();
            for (int i = 0; i < inputs.Length; i++)
                recipe.Inputs.Add(new ItemRequirement { ItemId = inputs[i], Count = counts[i] });
            return Asset(recipe, "Hideout/Recipes/" + id + ".asset");
        }

        private static void SeedCharacters()
        {
            Character("alexei", "Alexei Volkov", "GHOST", Characters.Faction.EastContract,
                "Former recon sergeant. Silent, methodical, always takes the high ground.",
                new[] { SkillType.Endurance, SkillType.CovertMovement }, new[] { "wpn_ak74n", "armor_6b23", "bp_berkut", "med_ifak" });
            Character("nikita", "Nikita Sokolov", "STORM", Characters.Faction.EastContract,
                "Assault breacher. Close quarters, heavy armour, no patience.",
                new[] { SkillType.Strength, SkillType.AssaultRifle }, new[] { "wpn_akm", "armor_zhuk", "bp_scav", "med_salewa" });
            Character("ivan", "Ivan Morozov", "BEAR", Characters.Faction.EastContract,
                "Machine gunner and field engineer. Carries everything, complains about nothing.",
                new[] { SkillType.Vitality, SkillType.Strength, SkillType.Crafting }, new[] { "wpn_saiga12", "armor_6b13", "bp_trizip", "med_grizzly" });
            Character("anna", "Anna Kuznetsova", "WOLF", Characters.Faction.WestContract,
                "Designated marksman. Reads the wind, waits, takes the one shot that matters.",
                new[] { SkillType.DMR, SkillType.Perception }, new[] { "wpn_svd", "armor_trooper", "bp_berkut", "med_ifak" });
            Character("olga", "Olga Semyonova", "SHADOW", Characters.Faction.WestContract,
                "Recon and sabotage. Travels light, moves at night, leaves nothing behind.",
                new[] { SkillType.CovertMovement, SkillType.Search, SkillType.Perception }, new[] { "wpn_mp5", "armor_paca", "bp_scav", "med_salewa" });
            Character("maria", "Maria Orlova", "FURY", Characters.Faction.WestContract,
                "Squad medic turned raider. Fast hands, faster temper, keeps everyone alive.",
                new[] { SkillType.Metabolism, SkillType.Surgery, SkillType.Vitality }, new[] { "wpn_m4a1", "armor_6b23", "bp_berkut", "med_grizzly" });
        }

        private static void Character(string id, string name, string callsign, Characters.Faction faction, string bio,
            SkillType[] perks, string[] loadout)
        {
            CharacterDefinition def = ScriptableObject.CreateInstance<CharacterDefinition>();
            def.Id = id; def.DisplayName = name; def.Callsign = callsign; def.Faction = faction;
            def.Biography = bio;
            def.FactionColor = faction == Characters.Faction.EastContract
                ? new Color(0.42f, 0.48f, 0.36f) : new Color(0.36f, 0.40f, 0.52f);

            def.Perks = new List<CharacterPerk>();
            for (int i = 0; i < perks.Length; i++)
                def.Perks.Add(new CharacterPerk { Skill = perks[i], StartingLevel = 6f, Description = perks[i] + " +6" });

            def.StartingLoadout = new List<StartingItem>();
            for (int i = 0; i < loadout.Length; i++)
            {
                ItemDefinition item = FindItem(loadout[i]);
                StartingItem entry = new StartingItem
                {
                    ItemId = loadout[i],
                    Stack = 1,
                    Slot = item.CanEquip ? item.EquipmentSlot : EquipmentSlot.None,
                    ContainerId = item.CanEquip ? string.Empty : "stash"
                };
                def.StartingLoadout.Add(entry);
            }
            def.StartingLoadout.Add(new StartingItem { ItemId = "money_rouble", Stack = 250000, Slot = EquipmentSlot.None, ContainerId = "stash" });
            def.StartingLoadout.Add(new StartingItem { ItemId = "ammo_545_ps", Stack = 120, Slot = EquipmentSlot.None, ContainerId = "stash" });
            def.StartingLoadout.Add(new StartingItem { ItemId = "ammo_762x39_ps", Stack = 90, Slot = EquipmentSlot.None, ContainerId = "stash" });
            def.StartingLoadout.Add(new StartingItem { ItemId = "secure_alpha", Stack = 1, Slot = EquipmentSlot.SecureContainer, ContainerId = string.Empty });
            def.StartingLoadout.Add(new StartingItem { ItemId = "food_water", Stack = 2, Slot = EquipmentSlot.None, ContainerId = "stash" });
            def.StartingLoadout.Add(new StartingItem { ItemId = "food_crackers", Stack = 3, Slot = EquipmentSlot.None, ContainerId = "stash" });
            def.StartingLoadout.Add(new StartingItem { ItemId = "seed_potato", Stack = 3, Slot = EquipmentSlot.None, ContainerId = "stash" });
            def.StartingLoadout.Add(new StartingItem { ItemId = "seed_herbs", Stack = 3, Slot = EquipmentSlot.None, ContainerId = "stash" });

            Asset(def, "Characters/" + id + ".asset");
        }

        private static void SeedBots()
        {
            Bot("bot_scav", "Scavenger", BotRole.Scavenger, 1, 0.32f, 0.85f, 0.35f, 55f,
                new[] { "wpn_aks74u", "wpn_makarov", "wpn_saiga12", "wpn_bizon" },
                new[] { "armor_paca" }, new[] { "bp_scav" }, new[] { "helmet_ssh68" });
            Bot("bot_contractor", "Contractor", BotRole.Contractor, 3, 0.62f, 0.45f, 0.65f, 85f,
                new[] { "wpn_ak74n", "wpn_m4a1", "wpn_mp5", "wpn_akm" },
                new[] { "armor_6b23", "armor_zhuk", "armor_6b13" }, new[] { "bp_berkut", "bp_trizip" }, new[] { "helmet_6b47", "helmet_fast" });
            Bot("bot_raider", "Raider", BotRole.Raider, 4, 0.78f, 0.28f, 0.8f, 110f,
                new[] { "wpn_hk416", "wpn_m4a1", "wpn_vss" },
                new[] { "armor_trooper", "armor_fort" }, new[] { "bp_trizip" }, new[] { "helmet_fast", "helmet_altyn" });
            Bot("bot_sniper", "Sniper", BotRole.Sniper, 3, 0.7f, 0.7f, 0.4f, 160f,
                new[] { "wpn_svd", "wpn_m700" }, new[] { "armor_zhuk" }, new[] { "bp_scav" }, new string[0]);
            Bot("bot_boss", "Boss", BotRole.Boss, 5, 0.86f, 0.22f, 0.9f, 140f,
                new[] { "wpn_akm", "wpn_vss", "wpn_saiga12" },
                new[] { "armor_fort", "armor_slick" }, new[] { "bp_pilgrim" }, new[] { "helmet_altyn" });
        }

        private static void Bot(string id, string name, BotRole role, int tier, float accuracy, float reaction,
            float aggression, float view, string[] weapons, string[] armor, string[] backpacks, string[] helmets)
        {
            BotProfile profile = ScriptableObject.CreateInstance<BotProfile>();
            profile.Id = id; profile.DisplayName = name; profile.Role = role; profile.Tier = tier;
            profile.Accuracy = accuracy; profile.ReactionTime = reaction;
            profile.Aggression = aggression; profile.ViewDistance = view;
            profile.FieldOfView = 120f; profile.HearingRadius = view * 0.7f;
            profile.MoveSpeedMultiplier = 0.9f + tier * 0.05f;
            profile.WeaponPool = new List<string>(weapons);
            profile.ArmorPool = new List<string>(armor);
            profile.BackpackPool = new List<string>(backpacks);
            profile.HelmetPool = new List<string>(helmets);
            profile.Loot = new List<LootEntry>
            {
                new LootEntry { ItemId = "money_rouble", Chance = 0.8f, MinStack = 2000, MaxStack = 14000, ContainerId = "pockets" },
                new LootEntry { ItemId = "med_bandage", Chance = 0.45f, MinStack = 1, MaxStack = 2, ContainerId = "pockets" },
                new LootEntry { ItemId = "barter_wires", Chance = 0.3f, ContainerId = "pockets" },
                new LootEntry { ItemId = "food_crackers", Chance = 0.35f, ContainerId = "pockets" },
                new LootEntry { ItemId = "ammo_545_ps", Chance = 0.5f, MinStack = 20, MaxStack = 60, ContainerId = "pockets" }
            };
            profile.MaxAlive = role == BotRole.Boss ? 1 : 12;
            profile.GroupSize = role == BotRole.Boss ? 3 : 2;
            Asset(profile, "Bots/" + id + ".asset");
        }

        private static void SeedTraders()
        {
            Trader("supplier", "Vadim the Supplier", new[] { "wpn_ak74n", "wpn_akm", "wpn_aks74u", "wpn_makarov", "mag_ak74_30", "ammo_545_ps", "ammo_545_bt", "ammo_762x39_ps" },
                new[] { 34000, 31000, 27000, 6400, 3200, 60, 110, 90 }, true, false);
            Trader("medic", "Lydia the Medic", new[] { "med_bandage", "med_ifak", "med_salewa", "med_painkiller", "med_morphine", "med_splint", "food_water" },
                new[] { 3200, 34000, 48000, 6200, 24000, 18000, 8000 }, false, false);
            Trader("dealer", "Grisha the Dealer", new[] { "food_crackers", "food_condensed", "food_stew", "barter_wires", "barter_bolts", "barter_screws", "barter_fuelcan", "seed_potato", "seed_herbs" },
                new[] { 6400, 12000, 24000, 12000, 9000, 8000, 24000, 9000, 9000 }, false, false);
            Trader("gunsmith", "Pavel the Gunsmith", new[] { "wpn_m4a1", "wpn_hk416", "wpn_mp5", "wpn_mp9", "wpn_m870", "wpn_m700", "mag_stanag_30", "ammo_556_m855" },
                new[] { 58000, 74000, 33000, 29000, 19000, 66000, 4200, 95 }, true, false);
            Trader("outfitter", "Raisa the Outfitter", new[] { "armor_paca", "armor_6b23", "armor_zhuk", "helmet_6b47", "bp_scav", "bp_berkut", "rig_blackrock" },
                new[] { 24000, 48000, 56000, 39000, 9000, 34000, 18000 }, false, true);
        }

        private static void Trader(string id, string name, string[] items, int[] prices, bool repairs, bool insures)
        {
            TraderDefinition trader = ScriptableObject.CreateInstance<TraderDefinition>();
            trader.Id = id; trader.DisplayName = name;
            trader.Description = name + " trades goods, gear and services.";
            trader.Repairs = repairs; trader.Insures = insures;
            trader.RepairQuality = repairs ? 0.85f : 0f;
            trader.Assort = new List<TraderOffer>();
            for (int i = 0; i < items.Length && i < prices.Length; i++)
                trader.Assort.Add(new TraderOffer { ItemId = items[i], Price = prices[i], Stock = -1, LoyaltyLevel = 1 });

            trader.Loyalty = new List<LoyaltyRequirement>
            {
                new LoyaltyRequirement { Level = 1, PlayerLevel = 1, Reputation = 0f, MoneySpent = 0f },
                new LoyaltyRequirement { Level = 2, PlayerLevel = 8, Reputation = 0.25f, MoneySpent = 250000f },
                new LoyaltyRequirement { Level = 3, PlayerLevel = 18, Reputation = 0.75f, MoneySpent = 1200000f },
                new LoyaltyRequirement { Level = 4, PlayerLevel = 30, Reputation = 1.6f, MoneySpent = 4000000f }
            };
            Asset(trader, "Traders/trader_" + id + ".asset");
        }

        private static void SeedQuests()
        {
            Quest("quest_first_blood", "First Blood", "supplier", 1, new string[0],
                new[] { QuestObjectiveType.Kill }, new[] { "Scavenger" }, new[] { 5 },
                1200f, 50000, 0.05f, new[] { "med_ifak", "ammo_545_bt" }, new[] { 2, 60 });

            Quest("quest_supplies", "Supply Run", "dealer", 3, new[] { "quest_first_blood" },
                new[] { QuestObjectiveType.FindItem, QuestObjectiveType.SurviveRaid }, new[] { "barter_wires", string.Empty }, new[] { 3, 2 },
                2400f, 90000, 0.08f, new[] { "bp_berkut" }, new[] { 1 });

            Quest("quest_green_thumb", "Green Thumb", "medic", 5, new[] { "quest_supplies" },
                new[] { QuestObjectiveType.HarvestPlant, QuestObjectiveType.BuildHideoutModule }, new[] { "plant_herbs", "greenhouse" }, new[] { 2, 1 },
                3200f, 140000, 0.12f, new[] { "med_salewa", "seed_herbs" }, new[] { 2, 5 });

            Quest("quest_deep_raid", "Deep Raid", "gunsmith", 8, new[] { "quest_green_thumb" },
                new[] { QuestObjectiveType.Kill, QuestObjectiveType.SurviveRaid }, new[] { "Contractor", string.Empty }, new[] { 10, 3 },
                5400f, 260000, 0.2f, new[] { "wpn_m4a1", "mag_stanag_30" }, new[] { 1, 4 });
        }

        private static void Quest(string id, string name, string traderId, int requiredLevel, string[] prerequisites,
            QuestObjectiveType[] types, string[] targets, int[] amounts, float xp, int money, float rep,
            string[] rewardItems, int[] rewardCounts)
        {
            QuestDefinition quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.Id = id; quest.DisplayName = name; quest.TraderId = traderId;
            quest.RequiredLevel = requiredLevel;
            quest.PrerequisiteQuestIds = new List<string>(prerequisites);
            quest.Objectives = new List<QuestObjective>();
            for (int i = 0; i < types.Length; i++)
                quest.Objectives.Add(new QuestObjective
                {
                    Type = types[i],
                    TargetId = i < targets.Length ? targets[i] : string.Empty,
                    Amount = i < amounts.Length ? amounts[i] : 1,
                    Description = types[i] + " x" + (i < amounts.Length ? amounts[i] : 1)
                });

            quest.Rewards = new QuestReward
            {
                Xp = xp,
                Money = money,
                TraderRep = rep,
                ItemIds = new List<string>(rewardItems),
                ItemCounts = new List<int>(rewardCounts)
            };
            Asset(quest, "Quests/" + id + ".asset");
        }
    }
}
