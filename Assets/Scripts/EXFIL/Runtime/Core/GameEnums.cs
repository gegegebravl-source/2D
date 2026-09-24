using System;

namespace EXFIL.Core
{
    // ---------------------------------------------------------------- Items
    public enum ItemType
    {
        Any = 0,
        Weapon,
        WeaponModule,   // attachment
        Magazine,
        Ammo,
        Armor,          // vest / armor rig
        Helmet,
        Backpack,
        ChestRig,
        Clothing,       // body suit / jacket / pants / boots / gloves
        Medical,
        Consumable,     // food and drink
        Container,      // secure container, case
        Key,
        Barter,         // trade goods
        Currency,
        Valuable,
        Electronics,
        Tool,
        BuildingMaterial,
        PlantSeed,
        Harvest,
        Grenade,
        Quest,
        Special
    }

    public enum ItemRarity { Common = 0, Rare, Epic, Legendary, Quest }

    public enum EquipmentSlot
    {
        None = 0,
        PrimaryWeapon,
        SecondaryWeapon,
        Holster,
        ArmorVest,
        ChestRig,
        Helmet,
        Headwear,
        FaceCover,
        Eyewear,
        EarPiece,
        Backpack,
        SecureContainer,
        Pockets,
        BodySuit,
        Gloves,
        Boots
    }

    public enum AttachmentSlot
    {
        None = 0,
        Optic,
        RearSight,
        Muzzle,
        Foregrip,
        Handguard,
        Stock,
        PistolGrip,
        Laser,
        Flashlight,
        Magazine,
        Mount,
        Auxiliary
    }

    public enum AmmoCaliber
    {
        None = 0,
        _9x18PM, _9x19Para, _762x25TT, _45ACP,
        _545x39, _556x45NATO, _762x39, _762x51NATO, _762x54R, _9x39, _366TKM,
        _12gauge, _20gauge
    }

    public enum WeaponClass
    {
        None = 0,
        AssaultRifle, AssaultCarbine, SMG, Shotgun, SniperRifle,
        MarksmanRifle, Pistol, LMG, Melee, Throwable
    }

    [Flags]
    public enum FireMode { None = 0, Single = 1, Burst = 2, FullAuto = 4 }

    public enum ArmorClass { None = 0, One = 1, Two, Three, Four, Five, Six }

    public enum ArmorMaterial { None = 0, Aramid, UHMWPE, Steel, Titanium, Ceramic, Glass, Aluminium, Combined }

    // ------------------------------------------------------------ Character
    public enum BodyPart { Head = 0, Thorax, Stomach, LeftArm, RightArm, LeftLeg, RightLeg }

    [Flags]
    public enum BodyZone
    {
        None = 0,
        Head = 1 << 0,
        Thorax = 1 << 1,
        Stomach = 1 << 2,
        Arms = 1 << 3,
        Legs = 1 << 4
    }

    public enum HealthEffectType
    {
        None = 0, LightBleed, HeavyBleed, Fracture, Pain, Painkiller,
        Contusion, Concussion, Intoxication, Dehydration, Exhaustion,
        Radiation, Tremor, Regeneration, Infection
    }

    public enum MovementStance { Stand = 0, Crouch, Prone }

    public enum Facing8 { S = 0, SE, E, NE, N, NW, W, SW }

    public enum DamageType
    {
        Bullet = 0, Melee, Explosion, Fall, Bleeding, Dehydration,
        Exhaustion, Fragmentation, Environment
    }

    // --------------------------------------------------------------- Combat
    public enum SkillType
    {
        Endurance = 0, Strength, Vitality, Health, Perception, Attention,
        Metabolism, Immunity, Surgery, Search, Sniper, AssaultRifle,
        SMG, Shotgun, Pistol, DMR, Crafting, HideoutManagement, Charisma,
        CovertMovement, RecoilControl, Melee
    }

    public enum TraderId { None = 0, Supplier, Medic, Dealer, Gunsmith, Outfitter, Fence }

    public enum BotRole { Scavenger = 0, Contractor, Raider, Sniper, Boss, Guard }

    public enum BotState
    {
        Idle = 0, Patrol, Move, Investigate, Combat, Flank, Suppress,
        TakeCover, Loot, Heal, Reload, Flee, Extract, Dead
    }

    public enum RaidStatus { NotStarted = 0, Loading, InProgress, Extracting, Finished }

    public enum RaidResult { None = 0, Survived, RunThrough, Killed, Missing, Disconnected }

    // ------------------------------------------------------------- Hideout
    public enum PlantStage { Empty = 0, Seedling, Sprout, Growing, Mature, Withered }

    public enum PlantNeed { Water = 0, Light, Fertilizer, Temperature }

    // ---------------------------------------------------------- Progression
    public enum QuestObjectiveType
    {
        Kill = 0, KillWithWeapon, FindItem, HandOverItem, PlaceMarker,
        SurviveRaid, ReachLevel, BuildHideoutModule, CraftItem, HarvestPlant,
        VisitLocation, ExtractWithItem
    }

    public enum QuestStatus { Locked = 0, Available, Active, Completed, Failed }

    // ----------------------------------------------------------------- Meta
    public enum NetRole { Offline = 0, Host, Server, Client }

    public enum SessionMode { SoloWithBots = 0, OnlinePvE, OnlinePvP }
}
