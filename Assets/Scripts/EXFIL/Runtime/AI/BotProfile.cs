using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;

namespace EXFIL.AI
{
    [Serializable]
    public class LootEntry
    {
        public string ItemId;
        [Range(0f, 1f)] public float Chance = 0.5f;
        public int MinStack = 1;
        public int MaxStack = 1;
        public string ContainerId = "pockets";
    }

    /// <summary>Blueprint for a bot type: how it fights, what it carries, how it spawns.</summary>
    [CreateAssetMenu(menuName = "EXFIL/AI/Bot profile", fileName = "Bot_")]
    public class BotProfile : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public BotRole Role = BotRole.Scavenger;
        public Characters.Faction Faction = Characters.Faction.EastContract;
        public int Tier = 1;                       // 1..5 difficulty

        [Header("Combat")]
        [Range(0f, 1f)] public float Accuracy = 0.45f;
        public float ReactionTime = 0.55f;
        public float AimSpeed = 6f;                // degrees per second of aiming convergence
        [Range(0f, 1f)] public float Aggression = 0.5f;
        [Range(0f, 1f)] public float FlankChance = 0.3f;
        [Range(0f, 1f)] public float GrenadeChance = 0.1f;
        public float BurstDuration = 0.6f;
        public float BurstPause = 0.9f;
        [Range(0f, 1f)] public float HealChance = 0.6f;
        [Range(0f, 1f)] public float LootDesire = 0.4f;

        [Header("Senses")]
        public float ViewDistance = 70f;
        [Range(20f, 210f)] public float FieldOfView = 120f;
        public float HearingRadius = 45f;
        public float MemoryTime = 8f;

        [Header("Body")]
        public float HealthMultiplier = 1f;
        public float MoveSpeedMultiplier = 1f;

        [Header("Gear")]
        public List<string> WeaponPool = new List<string>();
        public List<string> ArmorPool = new List<string>();
        public List<string> BackpackPool = new List<string>();
        public List<string> HelmetPool = new List<string>();
        public List<LootEntry> Loot = new List<LootEntry>();

        [Header("Spawning")]
        public int MaxAlive = 12;
        public float SpawnWeight = 1f;
        public bool SpawnsInGroups = true;
        public int GroupSize = 2;

        public string PickWeapon(Rng rng)
        {
            return WeaponPool.Count > 0 ? rng.Pick(WeaponPool) : null;
        }

        public string PickArmor(Rng rng)
        {
            return ArmorPool.Count > 0 && rng.Chance(0.7f) ? rng.Pick(ArmorPool) : null;
        }
    }
}
