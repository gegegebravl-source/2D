using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;
using EXFIL.Items;
using EXFIL.Progression;

namespace EXFIL.Economy
{
    /// <summary>Buying, selling, repairing and insuring - all against the player's stash.</summary>
    public class EconomyService : MonoBehaviour
    {
        public List<TraderDefinition> Traders = new List<TraderDefinition>();
        private readonly Dictionary<string, int> _stock = new Dictionary<string, int>();
        private double _lastRestock;

        public static EconomyService Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Services.Register(this);
            RestockAll();
        }

        public TraderDefinition Find(string id)
        {
            for (int i = 0; i < Traders.Count; i++)
                if (Traders[i] != null && Traders[i].Id == id) return Traders[i];
            return null;
        }

        public void RestockAll()
        {
            _stock.Clear();
            for (int i = 0; i < Traders.Count; i++)
            {
                TraderDefinition trader = Traders[i];
                if (trader == null) continue;
                for (int o = 0; o < trader.Assort.Count; o++)
                {
                    TraderOffer offer = trader.Assort[o];
                    _stock[trader.Id + ":" + offer.ItemId] = offer.Stock;
                }
            }
            _lastRestock = Hideout.Epoch.Now();
        }

        public int StockOf(string traderId, string itemId)
        {
            int value;
            if (!_stock.TryGetValue(traderId + ":" + itemId, out value)) return 0;
            return value;
        }

        // ------------------------------------------------------------------- trade
        public bool Buy(TraderDefinition trader, TraderOffer offer, PlayerProfile profile, int count, out string reason)
        {
            reason = null;
            if (trader == null || offer == null || profile == null) { reason = "Bad request"; return false; }

            TraderStanding standing = profile.Standing(trader.Id);
            if (standing.LoyaltyLevel < offer.LoyaltyLevel) { reason = "Loyalty level too low"; return false; }
            if (offer.Stock > 0 && StockOf(trader.Id, offer.ItemId) < count) { reason = "Out of stock"; return false; }

            ItemDefinition item = ItemDatabase.Find(offer.ItemId);
            if (item == null) { reason = "Unknown item"; return false; }

            if (offer.IsBarter)
            {
                for (int i = 0; i < offer.BarterItemIds.Count; i++)
                {
                    int needed = offer.BarterCounts[i] * count;
                    if (profile.Stash.CountOf(offer.BarterItemIds[i]) < needed) { reason = "Missing barter items"; return false; }
                }
                for (int i = 0; i < offer.BarterItemIds.Count; i++)
                    profile.Stash.Consume(offer.BarterItemIds[i], offer.BarterCounts[i] * count);
            }
            else
            {
                int price = offer.Price * count;
                if (profile.Stash.CountOf(offer.CurrencyId) < price) { reason = "Not enough money"; return false; }
                profile.Stash.Consume(offer.CurrencyId, price);
                standing.MoneySpent += price;
            }

            ItemInstance bought = ItemInstance.Create(item, count);
            if (!profile.Stash.TryAdd(bought, out reason)) return false;

            if (offer.Stock > 0)
                _stock[trader.Id + ":" + offer.ItemId] = StockOf(trader.Id, offer.ItemId) - count;
            RecalculateLoyalty(trader, standing, profile);
            return true;
        }

        public bool Sell(TraderDefinition trader, ItemInstance item, PlayerProfile profile, out int price, out string reason)
        {
            price = 0;
            reason = null;
            if (trader == null || item == null || profile == null) { reason = "Bad request"; return false; }
            if (item.Def == null || !item.Def.CanBeSold) { reason = "Item cannot be sold"; return false; }

            price = Mathf.RoundToInt(item.Def.BasePrice * item.Stack * SellMultiplier(trader));
            ItemDefinition currency = ItemDatabase.Find("money_rouble");
            if (currency != null)
            {
                ItemInstance money = ItemInstance.Create(currency, price);
                string addReason;
                profile.Stash.TryAdd(money, out addReason);
            }

            profile.Stash.Remove(item.Uid, out ItemInstance removed);
            removed = null;
            return true;
        }

        private float SellMultiplier(TraderDefinition trader)
        {
            return trader != null && trader.Id == "fence" ? 0.45f : 0.62f;
        }

        private void RecalculateLoyalty(TraderDefinition trader, TraderStanding standing, PlayerProfile profile)
        {
            if (trader == null) return;
            int level = 1;
            for (int i = 0; i < trader.Loyalty.Count; i++)
            {
                LoyaltyRequirement requirement = trader.Loyalty[i];
                if (profile.Level >= requirement.PlayerLevel &&
                    standing.Reputation >= requirement.Reputation &&
                    standing.MoneySpent >= requirement.MoneySpent)
                    level = Mathf.Max(level, requirement.Level);
            }
            standing.LoyaltyLevel = level;
        }

        // ------------------------------------------------------------------ repair
        public int RepairCost(TraderDefinition trader, ItemInstance item)
        {
            if (trader == null || item == null || item.MaxDurability <= 0f) return 0;
            float missing = item.MaxDurability - item.CurrentDurability;
            return Mathf.RoundToInt(missing * trader.RepairPricePerPoint);
        }

        public bool Repair(TraderDefinition trader, ItemInstance item, PlayerProfile profile, out string reason)
        {
            reason = null;
            if (trader == null || !trader.Repairs) { reason = "Trader does not repair"; return false; }
            int cost = RepairCost(trader, item);
            if (cost <= 0) { reason = "Nothing to repair"; return false; }
            if (profile.Stash.CountOf("money_rouble") < cost) { reason = "Not enough money"; return false; }

            profile.Stash.Consume("money_rouble", cost);
            float restored = (item.MaxDurability - item.CurrentDurability) * Mathf.Clamp01(trader.RepairQuality);
            item.Durability = Mathf.Min(item.MaxDurability, item.CurrentDurability + restored);
            item.MaxDurability = Mathf.Max(1f, item.MaxDurability - restored * 0.12f); // wear reduces max
            return true;
        }

        // --------------------------------------------------------------- insurance
        public int InsuranceCost(TraderDefinition trader, List<ItemInstance> items)
        {
            if (trader == null || !trader.Insures) return 0;
            float value = 0f;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].Def != null) value += items[i].Def.BasePrice * items[i].Stack;
            return Mathf.RoundToInt(value * trader.InsuranceRate);
        }

        public bool Insure(TraderDefinition trader, List<ItemInstance> items, PlayerProfile profile, out string reason)
        {
            reason = null;
            if (trader == null || !trader.Insures) { reason = "Trader does not insure"; return false; }
            int cost = InsuranceCost(trader, items);
            if (profile.Stash.CountOf("money_rouble") < cost) { reason = "Not enough money"; return false; }

            profile.Stash.Consume("money_rouble", cost);
            InsuranceContract contract = new InsuranceContract { TraderId = trader.Id };
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null) contract.ItemIds.Add(items[i].DefinitionId);
            profile.Insurance.Add(contract);
            return true;
        }
    }
}
