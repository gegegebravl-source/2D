using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.Progression
{
    [Serializable]
    public class QuestObjective
    {
        public QuestObjectiveType Type;
        public string TargetId;          // item id, bot profile id, weapon id, module id
        public int Amount = 1;
        public string LocationId;        // optional map filter
        public BodyPart BodyPart = BodyPart.Thorax;
        [TextArea] public string Description;
    }

    [Serializable]
    public class QuestReward
    {
        public int Money;
        public string CurrencyId = "money_rouble";
        public float Xp;
        public float TraderRep;
        public List<string> ItemIds = new List<string>();
        public List<int> ItemCounts = new List<int>();
        public List<string> UnlockRecipeIds = new List<string>();
    }

    /// <summary>A task chain from a trader: objectives, conditions, rewards.</summary>
    [CreateAssetMenu(menuName = "EXFIL/Progression/Quest", fileName = "Quest_")]
    public class QuestDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea(3, 8)] public string Description;
        public string TraderId;
        public int RequiredLevel = 1;
        public List<string> PrerequisiteQuestIds = new List<string>();

        public List<QuestObjective> Objectives = new List<QuestObjective>();
        public QuestReward Rewards = new QuestReward();
    }

    /// <summary>Tracks quest progress from gameplay events and hands out rewards.</summary>
    public class QuestLog : MonoBehaviour
    {
        public List<QuestDefinition> Quests = new List<QuestDefinition>();

        private PlayerProfile _profile;

        public static QuestLog Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            GameEvents.Subscribe<CharacterDiedEvent>(OnKill);
            GameEvents.Subscribe<ItemLootedEvent>(OnLoot);
            GameEvents.Subscribe<ExtractedEvent>(OnExtract);
            GameEvents.Subscribe<PlantHarvestedEvent>(OnHarvest);
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<CharacterDiedEvent>(OnKill);
            GameEvents.Unsubscribe<ItemLootedEvent>(OnLoot);
            GameEvents.Unsubscribe<ExtractedEvent>(OnExtract);
            GameEvents.Unsubscribe<PlantHarvestedEvent>(OnHarvest);
        }

        public void Bind(PlayerProfile profile)
        {
            _profile = profile;
        }

        public bool IsAvailable(QuestDefinition quest)
        {
            if (quest == null || _profile == null) return false;
            if (_profile.Level < quest.RequiredLevel) return false;
            for (int i = 0; i < quest.PrerequisiteQuestIds.Count; i++)
            {
                QuestProgress prerequisite = _profile.Quest(quest.PrerequisiteQuestIds[i]);
                if (prerequisite == null || prerequisite.Status != QuestStatus.Completed) return false;
            }
            QuestProgress own = _profile.Quest(quest.Id);
            return own == null || own.Status == QuestStatus.Available;
        }

        public void Start(QuestDefinition quest)
        {
            if (quest == null || _profile == null || !IsAvailable(quest)) return;
            QuestProgress progress = _profile.Quest(quest.Id);
            if (progress == null)
            {
                progress = new QuestProgress { QuestId = quest.Id, Status = QuestStatus.Active };
                _profile.Quests.Add(progress);
            }
            progress.Status = QuestStatus.Active;
            while (progress.ObjectiveProgress.Count < quest.Objectives.Count) progress.ObjectiveProgress.Add(0);
        }

        private void Advance(QuestObjectiveType type, string targetId)
        {
            if (_profile == null) return;

            for (int i = 0; i < _profile.Quests.Count; i++)
            {
                QuestProgress progress = _profile.Quests[i];
                if (progress.Status != QuestStatus.Active) continue;
                QuestDefinition quest = Find(progress.QuestId);
                if (quest == null) continue;

                for (int o = 0; o < quest.Objectives.Count; o++)
                {
                    QuestObjective objective = quest.Objectives[o];
                    if (objective.Type != type) continue;
                    if (!string.IsNullOrEmpty(objective.TargetId) && objective.TargetId != targetId) continue;

                    while (progress.ObjectiveProgress.Count <= o) progress.ObjectiveProgress.Add(0);
                    progress.ObjectiveProgress[o]++;
                }

                if (IsComplete(quest, progress))
                {
                    progress.Status = QuestStatus.Completed;
                    Debug.Log("[EXFIL] Quest completed: " + quest.DisplayName);
                }
            }
        }

        public bool IsComplete(QuestDefinition quest, QuestProgress progress)
        {
            for (int o = 0; o < quest.Objectives.Count; o++)
            {
                QuestObjective objective = quest.Objectives[o];
                int value = o < progress.ObjectiveProgress.Count ? progress.ObjectiveProgress[o] : 0;
                if (value < objective.Amount) return false;
            }
            return true;
        }

        public void ClaimRewards(QuestDefinition quest)
        {
            if (quest == null || _profile == null) return;
            QuestProgress progress = _profile.Quest(quest.Id);
            if (progress == null || progress.Status != QuestStatus.Completed || progress.RewardsClaimed) return;

            progress.RewardsClaimed = true;
            ProgressionService.AddXp(_profile, quest.Rewards.Xp);

            if (quest.Rewards.Money > 0)
            {
                ItemDefinition currency = ItemDatabase.Find(quest.Rewards.CurrencyId);
                if (currency != null)
                {
                    ItemInstance money = ItemInstance.Create(currency, quest.Rewards.Money);
                    string reason;
                    _profile.Stash.TryAdd(money, out reason);
                }
            }

            for (int i = 0; i < quest.Rewards.ItemIds.Count; i++)
            {
                ItemDefinition def = ItemDatabase.Find(quest.Rewards.ItemIds[i]);
                if (def == null) continue;
                int count = i < quest.Rewards.ItemCounts.Count ? quest.Rewards.ItemCounts[i] : 1;
                ItemInstance reward = ItemInstance.Create(def, count);
                string reason;
                _profile.Stash.TryAdd(reward, out reason);
            }

            if (!string.IsNullOrEmpty(quest.TraderId))
            {
                TraderStanding standing = _profile.Standing(quest.TraderId);
                standing.Reputation += quest.Rewards.TraderRep;
            }
        }

        private void OnKill(CharacterDiedEvent evt) { Advance(QuestObjectiveType.Kill, evt.VictimName); }
        private void OnLoot(ItemLootedEvent evt) { Advance(QuestObjectiveType.FindItem, evt.ItemId); }
        private void OnExtract(ExtractedEvent evt) { Advance(QuestObjectiveType.SurviveRaid, evt.ExitName); }
        private void OnHarvest(PlantHarvestedEvent evt) { Advance(QuestObjectiveType.HarvestPlant, evt.PlantId); }

        public QuestDefinition Find(string id)
        {
            for (int i = 0; i < Quests.Count; i++)
                if (Quests[i] != null && Quests[i].Id == id) return Quests[i];
            return null;
        }
    }
}
