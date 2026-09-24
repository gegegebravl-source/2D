using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.Hideout
{
    [Serializable]
    public class ModuleState
    {
        public string ModuleId;
        public int Level;                 // 0 = not built
        public double BuildStartedAt;     // unix time
        public double BuildFinishesAt;
        public bool IsBuilding;
        public float StoredFuel;          // litres for the generator
    }

    [Serializable]
    public class CraftingTask
    {
        public string RecipeId;
        public string StationId;
        public double StartedAt;
        public double FinishesAt;
        public int OutputCount = 1;

        public bool IsComplete { get { return Epoch.Now() >= FinishesAt; } }
        public float Progress
        {
            get
            {
                double total = Math.Max(1.0, FinishesAt - StartedAt);
                return Mathf.Clamp01((float)((Epoch.Now() - StartedAt) / total));
            }
        }
    }

    public static class Epoch
    {
        public static double Now()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }

    /// <summary>
    /// Owns the bunker: module construction (real time, progresses while the game is closed),
    /// fuel, crafting queue, plot allocation and all derived bonuses.
    /// </summary>
    public class HideoutManager : MonoBehaviour
    {
        public static HideoutManager Instance { get; private set; }

        [Header("Content")]
        public List<HideoutModuleDefinition> Modules = new List<HideoutModuleDefinition>();

        [Header("State")]
        public List<ModuleState> State = new List<ModuleState>();
        public List<CraftingTask> Crafts = new List<CraftingTask>();
        public float FuelLitres = 0f;
        public double LastTickEpoch;

        public event Action<ModuleState> OnModuleFinished;
        public event Action<CraftingTask> OnCraftFinished;

        private void Awake()
        {
            Instance = this;
            Services.Register(this);
        }

        // ------------------------------------------------------------------ modules
        public ModuleState Get(string moduleId)
        {
            for (int i = 0; i < State.Count; i++)
                if (State[i].ModuleId == moduleId) return State[i];
            ModuleState created = new ModuleState { ModuleId = moduleId, Level = 0 };
            State.Add(created);
            return created;
        }

        public int LevelOf(string moduleId)
        {
            return Get(moduleId).Level;
        }

        public HideoutModuleDefinition Definition(string moduleId)
        {
            for (int i = 0; i < Modules.Count; i++)
                if (Modules[i] != null && Modules[i].Id == moduleId) return Modules[i];
            return null;
        }

        public bool CanStartUpgrade(string moduleId, Inventory stash, out string reason)
        {
            reason = null;
            HideoutModuleDefinition def = Definition(moduleId);
            if (def == null) { reason = "Unknown module"; return false; }

            ModuleState state = Get(moduleId);
            int nextLevel = state.Level + 1;
            HideoutModuleLevel level = def.Level(nextLevel);
            if (level == null) { reason = "Max level"; return false; }
            if (state.IsBuilding) { reason = "Already building"; return false; }

            for (int i = 0; i < level.Prerequisites.Count; i++)
            {
                ModulePrerequisite prerequisite = level.Prerequisites[i];
                if (LevelOf(prerequisite.ModuleId) < prerequisite.Level)
                {
                    reason = "Requires " + prerequisite.ModuleId + " lvl " + prerequisite.Level;
                    return false;
                }
            }

            for (int i = 0; i < level.Requirements.Count; i++)
            {
                ItemRequirement requirement = level.Requirements[i];
                if (stash != null && stash.CountOf(requirement.ItemId) < requirement.Count)
                {
                    reason = "Not enough items";
                    return false;
                }
            }

            if (level.MoneyCost > 0 && stash != null && stash.CountOf(level.CurrencyId) < level.MoneyCost)
            {
                reason = "Not enough money";
                return false;
            }
            return true;
        }

        public bool StartUpgrade(string moduleId, Inventory stash, out string reason)
        {
            if (!CanStartUpgrade(moduleId, stash, out reason)) return false;

            HideoutModuleDefinition def = Definition(moduleId);
            ModuleState state = Get(moduleId);
            HideoutModuleLevel level = def.Level(state.Level + 1);

            for (int i = 0; i < level.Requirements.Count; i++)
            {
                ItemRequirement requirement = level.Requirements[i];
                if (requirement.Consumed && stash != null) stash.Consume(requirement.ItemId, requirement.Count);
            }
            if (level.MoneyCost > 0 && stash != null) stash.Consume(level.CurrencyId, level.MoneyCost);

            state.IsBuilding = true;
            state.BuildStartedAt = Epoch.Now();
            state.BuildFinishesAt = state.BuildStartedAt + level.BuildTimeSeconds;
            return true;
        }

        // ------------------------------------------------------------------ ticking
        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            double now = Epoch.Now();

            // offline progression: everything is timestamp based
            for (int i = 0; i < State.Count; i++)
            {
                ModuleState state = State[i];
                if (!state.IsBuilding || now < state.BuildFinishesAt) continue;
                state.IsBuilding = false;
                state.Level += 1;
                OnModuleFinished?.Invoke(state);
            }

            for (int i = Crafts.Count - 1; i >= 0; i--)
            {
                CraftingTask task = Crafts[i];
                if (!task.IsComplete) continue;
                Crafts.RemoveAt(i);
                OnCraftFinished?.Invoke(task);
                Crafting.Deliver(task);
            }

            // fuel burn
            float consumption = TotalFuelConsumption();
            if (consumption > 0f)
            {
                float burned = consumption * (deltaTime / 60f);
                FuelLitres = Mathf.Max(0f, FuelLitres - burned);
            }

            GameEvents.Raise(new HideoutTickEvent { DeltaTime = deltaTime });
            LastTickEpoch = now;
        }

        public void CatchUpOfflineProgress()
        {
            Tick(0f);
        }

        // ------------------------------------------------------------------ effects
        public float TotalFuelConsumption()
        {
            float total = 0f;
            for (int i = 0; i < State.Count; i++)
            {
                HideoutModuleDefinition def = Definition(State[i].ModuleId);
                if (def == null) continue;
                for (int l = 1; l <= State[i].Level; l++)
                {
                    HideoutModuleLevel level = def.Level(l);
                    if (level != null) total += level.FuelConsumption;
                }
            }
            return total * (HasPower() ? 1f : 0f);
        }

        public bool HasPower()
        {
            return FuelLitres > 0f || LevelOf("generator") == 0;
        }

        public int TotalCraftingSlots()
        {
            int slots = 0;
            foreach (ModuleState state in State)
            {
                HideoutModuleDefinition def = Definition(state.ModuleId);
                if (def == null) continue;
                for (int l = 1; l <= state.Level; l++)
                {
                    HideoutModuleLevel level = def.Level(l);
                    if (level != null) slots += level.CraftingSlots;
                }
            }
            return slots;
        }

        public int TotalPlots()
        {
            int plots = 0;
            foreach (ModuleState state in State)
            {
                HideoutModuleDefinition def = Definition(state.ModuleId);
                if (def == null) continue;
                for (int l = 1; l <= state.Level; l++)
                {
                    HideoutModuleLevel level = def.Level(l);
                    if (level != null) plots += level.PlantPlots;
                }
            }
            return plots;
        }

        public Vector2Int StashBonus()
        {
            int width = 0, height = 0;
            foreach (ModuleState state in State)
            {
                HideoutModuleDefinition def = Definition(state.ModuleId);
                if (def == null) continue;
                for (int l = 1; l <= state.Level; l++)
                {
                    HideoutModuleLevel level = def.Level(l);
                    if (level == null) continue;
                    width += level.StashWidth;
                    height += level.StashHeight;
                }
            }
            return new Vector2Int(width, height);
        }

        public float CraftSpeedBonus()
        {
            float bonus = 0f;
            foreach (ModuleState state in State)
            {
                HideoutModuleDefinition def = Definition(state.ModuleId);
                if (def == null) continue;
                for (int l = 1; l <= state.Level; l++)
                {
                    HideoutModuleLevel level = def.Level(l);
                    if (level != null) bonus += level.CraftSpeedBonus;
                }
            }
            return bonus + (HasPower() ? 0f : -0.3f);
        }

        public float PlantGrowthBonus()
        {
            float bonus = 0f;
            foreach (ModuleState state in State)
            {
                HideoutModuleDefinition def = Definition(state.ModuleId);
                if (def == null) continue;
                for (int l = 1; l <= state.Level; l++)
                {
                    HideoutModuleLevel level = def.Level(l);
                    if (level != null) bonus += level.PlantGrowthBonus;
                }
            }
            return bonus;
        }

        // ------------------------------------------------------------------ serializing
        public HideoutSaveData ToSaveData()
        {
            return new HideoutSaveData
            {
                Modules = State,
                Crafts = Crafts,
                FuelLitres = FuelLitres,
                LastTickEpoch = Epoch.Now()
            };
        }

        public void Load(HideoutSaveData data)
        {
            if (data == null) return;
            State = data.Modules ?? new List<ModuleState>();
            Crafts = data.Crafts ?? new List<CraftingTask>();
            FuelLitres = data.FuelLitres;
            LastTickEpoch = data.LastTickEpoch;
            CatchUpOfflineProgress();
        }
    }

    [Serializable]
    public class HideoutSaveData
    {
        public List<ModuleState> Modules = new List<ModuleState>();
        public List<CraftingTask> Crafts = new List<CraftingTask>();
        public float FuelLitres;
        public double LastTickEpoch;
    }
}
