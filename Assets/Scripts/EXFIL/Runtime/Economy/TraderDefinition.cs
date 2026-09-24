using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;

namespace EXFIL.Economy
{
    [Serializable]
    public class TraderOffer
    {
        public string ItemId;
        public int Price;
        public string CurrencyId = "money_rouble";
        public int Stock = -1;              // -1 = unlimited
        public int LoyaltyLevel = 1;
        public bool IsBarter;
        public List<string> BarterItemIds = new List<string>();
        public List<int> BarterCounts = new List<int>();
    }

    [Serializable]
    public class LoyaltyRequirement
    {
        public int Level;                   // loyalty level
        public int PlayerLevel;
        public float Reputation;
        public float MoneySpent;
    }

    /// <summary>A trader with assort, loyalty levels, repair and insurance services.</summary>
    [CreateAssetMenu(menuName = "EXFIL/Economy/Trader", fileName = "Trader_")]
    public class TraderDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea(2, 5)] public string Description;
        public Sprite Portrait;
        public List<TraderOffer> Assort = new List<TraderOffer>();
        public List<LoyaltyRequirement> Loyalty = new List<LoyaltyRequirement>();

        [Header("Services")]
        public bool Repairs = false;
        public bool Insures = false;
        public float RepairQuality = 1f;      // 1 = perfect, lower = worse
        public float RepairPricePerPoint = 12f;
        public float InsuranceRate = 0.15f;   // % of item value

        public LoyaltyRequirement LoyaltyForLevel(int level)
        {
            for (int i = 0; i < Loyalty.Count; i++)
                if (Loyalty[i].Level == level) return Loyalty[i];
            return null;
        }
    }
}
