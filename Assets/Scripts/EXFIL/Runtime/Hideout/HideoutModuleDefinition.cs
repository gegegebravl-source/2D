using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Hideout
{
    [Serializable]
    public class ItemRequirement
    {
        public string ItemId;
        public int Count = 1;
        public bool Consumed = true;
    }

    [Serializable]
    public class ModulePrerequisite
    {
        public string ModuleId;
        public int Level = 1;
    }

    [Serializable]
    public class HideoutModuleLevel
    {
        [Header("Cost")]
        public int MoneyCost = 0;
        public string CurrencyId = "money_rouble";
        public List<ItemRequirement> Requirements = new List<ItemRequirement>();
        public List<ModulePrerequisite> Prerequisites = new List<ModulePrerequisite>();
        public float BuildTimeSeconds = 60f;

        [Header("Effects (cumulative with previous levels)")]
        public int CraftingSlots = 0;
        public int PlantPlots = 0;
        public int StashWidth = 0;
        public int StashHeight = 0;
        public float FuelConsumption = 0f;        // per real minute
        public float CraftSpeedBonus = 0f;        // 0.1 = +10%
        public float PlantGrowthBonus = 0f;
        public float ExperienceBonus = 0f;
        public float RepairCostReduction = 0f;
        public float ScavCooldownReduction = 0f;
        [TextArea] public string Description;
    }

    public enum HideoutModuleKind
    {
        Stash, Generator, WaterCollector, Medstation, NutritionUnit, Workbench,
        Lavatory, Vents, Security, ShootingRange, Greenhouse, SolarPower,
        RestSpace, Heating, WaterTank
    }

    /// <summary>One upgradeable room of the bunker (each level costs items, money and real time).</summary>
    [CreateAssetMenu(menuName = "EXFIL/Hideout/Module", fileName = "Module_")]
    public class HideoutModuleDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea(2, 5)] public string Description;
        public HideoutModuleKind Kind = HideoutModuleKind.Workbench;
        public Sprite Icon;
        public List<HideoutModuleLevel> Levels = new List<HideoutModuleLevel>();

        public int MaxLevel { get { return Levels.Count; } }

        public HideoutModuleLevel Level(int level)
        {
            if (level < 1 || level > Levels.Count) return null;
            return Levels[level - 1];
        }
    }
}
