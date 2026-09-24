using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Raid
{
    [Serializable]
    public class BotWave
    {
        public string ProfileId;
        public int Count = 4;
        public float DelayAfterStart = 0f;
        public bool RespawnsOverTime = false;
        public float RespawnInterval = 120f;
    }

    /// <summary>Scenario for one raid: map, duration, exits, bot waves and loot rules.</summary>
    [CreateAssetMenu(menuName = "EXFIL/Raid/Raid settings", fileName = "Raid_")]
    public class RaidSettings : ScriptableObject
    {
        [Header("Map")]
        public string Id;
        public string DisplayName;
        public string SceneName;
        public float DurationMinutes = 35f;
        public int MinPlayers = 1;

        [Header("Bots")]
        public List<BotWave> Waves = new List<BotWave>();
        public int MaxBotsAlive = 14;
        public float BotSpawnDistanceFromPlayers = 45f;

        [Header("Loot")]
        public int LootContainersToFill = 40;
        public float LootValueMultiplier = 1f;

        [Header("Environment")]
        public bool EnableAirdrop = true;
        public float AirdropTime = 12f;
        public bool EnableRadiationZones = false;

        [Header("Extraction")]
        public string[] AlwaysOpenExits = new string[0];
    }
}
