using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.Progression
{
    [Serializable]
    public class TraderStanding
    {
        public string TraderId;
        public float Reputation;
        public float MoneySpent;
        public int LoyaltyLevel = 1;
    }

    [Serializable]
    public class QuestProgress
    {
        public string QuestId;
        public QuestStatus Status;
        public List<int> ObjectiveProgress = new List<int>();
        public bool RewardsClaimed;
    }

    [Serializable]
    public class InsuranceContract
    {
        public string TraderId;
        public List<string> ItemIds = new List<string>();
        public int RaidSeed;
        public bool Returned;
    }

    /// <summary>The persistent account: one operator, his stash, hideout, quests and standings.</summary>
    [Serializable]
    public class PlayerProfile
    {
        public string Id;
        public string Name = "Operator";
        public string CharacterId = "alexei";
        public int Level = 1;
        public float Xp;

        public Inventory Stash = new Inventory();
        public List<SkillProgress> Skills = new List<SkillProgress>();
        public List<QuestProgress> Quests = new List<QuestProgress>();
        public List<TraderStanding> Traders = new List<TraderStanding>();
        public List<InsuranceContract> Insurance = new List<InsuranceContract>();

        public int RaidsPlayed;
        public int RaidsSurvived;
        public int Kills;
        public int Deaths;
        public float KdRatio { get { return Deaths > 0 ? (float)Kills / Deaths : Kills; } }

        public static PlayerProfile CreateNew(CharacterDefinition character)
        {
            PlayerProfile profile = new PlayerProfile
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 10),
                Name = character != null ? character.DisplayName : "Operator",
                CharacterId = character != null ? character.Id : "alexei",
                Level = 1,
                Xp = 0f
            };

            profile.Stash.AddContainer(Inventory.StashContainerId, 10, 28);
            profile.Stash.AddContainer(Inventory.PocketsContainerId, 4, 1);

            if (character != null)
            {
                for (int i = 0; i < character.Perks.Count; i++)
                {
                    CharacterPerk perk = character.Perks[i];
                    profile.Skills.Add(new SkillProgress { Skill = (int)perk.Skill, Level = perk.StartingLevel, Xp = 0f });
                }

                for (int i = 0; i < character.StartingLoadout.Count; i++)
                {
                    StartingItem entry = character.StartingLoadout[i];
                    ItemDefinition def = ItemDatabase.Find(entry.ItemId);
                    if (def == null) continue;

                    ItemInstance instance = ItemInstance.Create(def, entry.Stack);
                    if (entry.Slot != EquipmentSlot.None)
                        profile.Stash.Equip(entry.Slot, instance);
                    else
                    {
                        ContainerInstance container = string.IsNullOrEmpty(entry.ContainerId)
                            ? null : profile.Stash.GetContainer(entry.ContainerId);
                        string reason;
                        if (container == null || !container.TryAdd(instance, out reason))
                            profile.Stash.TryAdd(instance, out reason);
                    }
                }
            }

            profile.Stash.SyncContainersFromEquipment();
            return profile;
        }

        public TraderStanding Standing(string traderId)
        {
            for (int i = 0; i < Traders.Count; i++)
                if (Traders[i].TraderId == traderId) return Traders[i];
            TraderStanding standing = new TraderStanding { TraderId = traderId };
            Traders.Add(standing);
            return standing;
        }

        public QuestProgress Quest(string questId)
        {
            for (int i = 0; i < Quests.Count; i++)
                if (Quests[i].QuestId == questId) return Quests[i];
            return null;
        }
    }

    /// <summary>Static XP curve + profile services.</summary>
    public static class ProgressionService
    {
        public static float XpForLevel(int level)
        {
            return 1000f + Mathf.Pow(level, 2.1f) * 450f;
        }

        public static bool AddXp(PlayerProfile profile, float amount)
        {
            if (profile == null) return false;
            profile.Xp += amount;
            bool levelled = false;
            while (profile.Xp >= XpForLevel(profile.Level))
            {
                profile.Xp -= XpForLevel(profile.Level);
                profile.Level++;
                levelled = true;
            }
            return levelled;
        }
    }
}
