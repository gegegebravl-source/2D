using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Items;

namespace EXFIL.Hideout
{
    /// <summary>A recipe: inputs, output, station requirement and real-time duration.</summary>
    [CreateAssetMenu(menuName = "EXFIL/Hideout/Craft recipe", fileName = "Recipe_")]
    public class CraftRecipe : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;

        [Header("Output")]
        public string OutputItemId;
        public int OutputCount = 1;

        [Header("Input")]
        public List<ItemRequirement> Inputs = new List<ItemRequirement>();

        [Header("Requirements")]
        public string StationModuleId;
        public int StationLevel = 1;
        public float DurationSeconds = 120f;
        public int RequiredPlayerLevel = 1;
        public bool RequiresPower = true;
    }

    /// <summary>Static crafting service bound to the hideout stash.</summary>
    public static class Crafting
    {
        public static bool CanCraft(CraftRecipe recipe, Inventory stash, int playerLevel, out string reason)
        {
            reason = null;
            if (recipe == null) { reason = "No recipe"; return false; }
            if (stash == null) { reason = "No stash"; return false; }
            if (playerLevel < recipe.RequiredPlayerLevel) { reason = "Level too low"; return false; }

            HideoutManager hideout = HideoutManager.Instance;
            if (hideout != null)
            {
                if (!string.IsNullOrEmpty(recipe.StationModuleId) &&
                    hideout.LevelOf(recipe.StationModuleId) < recipe.StationLevel)
                {
                    reason = "Station not built";
                    return false;
                }
                if (recipe.RequiresPower && !hideout.HasPower()) { reason = "No power"; return false; }
                if (hideout.Crafts.Count >= hideout.TotalCraftingSlots()) { reason = "No free slots"; return false; }
            }

            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                ItemRequirement input = recipe.Inputs[i];
                if (stash.CountOf(input.ItemId) < input.Count) { reason = "Missing items"; return false; }
            }
            return true;
        }

        public static bool Start(CraftRecipe recipe, Inventory stash, int playerLevel, out string reason)
        {
            if (!CanCraft(recipe, stash, playerLevel, out reason)) return false;

            for (int i = 0; i < recipe.Inputs.Count; i++)
                stash.Consume(recipe.Inputs[i].ItemId, recipe.Inputs[i].Count);

            HideoutManager hideout = HideoutManager.Instance;
            double now = Epoch.Now();
            float speed = 1f + (hideout != null ? hideout.CraftSpeedBonus() : 0f);
            if (speed <= 0.05f) speed = 0.05f;

            CraftingTask task = new CraftingTask
            {
                RecipeId = recipe.Id,
                StationId = recipe.StationModuleId,
                StartedAt = now,
                FinishesAt = now + recipe.DurationSeconds / speed,
                OutputCount = recipe.OutputCount
            };
            hideout?.Crafts.Add(task);
            return true;
        }

        public static void Deliver(CraftingTask task)
        {
            Inventory stash = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile.Stash : null;
            if (stash == null) return;

            CraftRecipe recipe = RecipeDatabase.Find(task.RecipeId);
            if (recipe == null) return;

            ItemDefinition def = ItemDatabase.Find(recipe.OutputItemId);
            if (def == null) return;

            ItemInstance output = ItemInstance.Create(def, recipe.OutputCount);
            string reason;
            if (!stash.TryAdd(output, out reason))
                Debug.LogWarning("[EXFIL] Stash is full, crafted item was lost: " + recipe.OutputItemId);
        }
    }

    /// <summary>Registry of every craft recipe (built by the editor setup tool).</summary>
    public static class RecipeDatabase
    {
        private static readonly List<CraftRecipe> _recipes = new List<CraftRecipe>();

        public static IList<CraftRecipe> All { get { return _recipes; } }

        public static void Clear()
        {
            _recipes.Clear();
        }

        public static void Register(CraftRecipe recipe)
        {
            if (recipe != null && !_recipes.Contains(recipe)) _recipes.Add(recipe);
        }

        public static CraftRecipe Find(string id)
        {
            for (int i = 0; i < _recipes.Count; i++)
                if (_recipes[i] != null && _recipes[i].Id == id) return _recipes[i];
            return null;
        }
    }
}
