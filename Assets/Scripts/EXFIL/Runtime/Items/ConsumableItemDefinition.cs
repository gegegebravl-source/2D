using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    [CreateAssetMenu(menuName = "EXFIL/Items/Medical", fileName = "Med_")]
    public class MedicalItemDefinition : ItemDefinition
    {
        [Header("Healing")]
        public float HealPerSecond = 4f;
        public float MaxResource = 300f;
        public float UseTime = 3f;
        public int Uses = 1;
        public bool WorksOnBlackenedPart = true;

        [Header("Effects")]
        public List<Core.HealthEffectType> Removes = new List<Core.HealthEffectType>();
        public List<Core.HealthEffectType> Adds = new List<Core.HealthEffectType>();
        public float PainkillerDuration = 0f;

        [Header("Restrictions")]
        public float MovementSpeedMultiplier = 0.6f;

        private void OnEnable()
        {
            Type = Core.ItemType.Medical;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Resource: " + MaxResource.ToString("0"));
            lines.Add("Use time: " + UseTime.ToString("0.0") + "s");
            if (Removes.Count > 0) lines.Add("Removes: " + string.Join(", ", Removes.ConvertAll(e => e.ToString()).ToArray()));
        }
    }

    [CreateAssetMenu(menuName = "EXFIL/Items/Consumable", fileName = "Food_")]
    public class ConsumableItemDefinition : ItemDefinition
    {
        [Header("Nutrition")]
        public float Energy = 25f;
        public float Hydration = 15f;
        public float UseTime = 3f;
        public int Uses = 1;
        public bool IsDrink;

        [Header("Effects")]
        public List<Core.HealthEffectType> Adds = new List<Core.HealthEffectType>();
        public List<Core.HealthEffectType> Removes = new List<Core.HealthEffectType>();

        private void OnEnable()
        {
            Type = Core.ItemType.Consumable;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Energy: +" + Energy.ToString("0"));
            lines.Add("Hydration: +" + Hydration.ToString("0"));
            lines.Add("Use time: " + UseTime.ToString("0.0") + "s");
        }
    }

    [CreateAssetMenu(menuName = "EXFIL/Items/Container", fileName = "Container_")]
    public class ContainerItemDefinition : ItemDefinition
    {
        public enum ContainerKind { Pockets, Backpack, ChestRig, Secure, Case }

        [Header("Container")]
        public ContainerKind Kind = ContainerKind.Backpack;
        public Vector2Int GridSize = new Vector2Int(4, 4);
        public float WeightReduction = 0f; // 0.15 = 15% lighter contents

        private void OnEnable()
        {
            Type = Core.ItemType.Container;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Grid: " + GridSize.x + "x" + GridSize.y);
            if (WeightReduction > 0f) lines.Add("Weight reduction: " + (WeightReduction * 100f).ToString("0") + "%");
        }

        public override bool CanEquip { get { return Kind != ContainerKind.Case; } }
        public override Core.EquipmentSlot EquipmentSlot
        {
            get
            {
                switch (Kind)
                {
                    case ContainerKind.Backpack: return Core.EquipmentSlot.Backpack;
                    case ContainerKind.ChestRig: return Core.EquipmentSlot.ChestRig;
                    case ContainerKind.Secure: return Core.EquipmentSlot.SecureContainer;
                    case ContainerKind.Pockets: return Core.EquipmentSlot.Pockets;
                    default: return Core.EquipmentSlot.None;
                }
            }
        }
    }

    [CreateAssetMenu(menuName = "EXFIL/Items/Clothing", fileName = "Clothing_")]
    public class ClothingItemDefinition : ItemDefinition
    {
        [Header("Clothing")]
        public Core.EquipmentSlot Slot = Core.EquipmentSlot.BodySuit;
        public bool ReplacesWholeBody = true;
        public float ColdResistance = 0f;
        public float HeatResistance = 0f;
        [Range(0f, 1f)] public float Camouflage = 0f; // lowers bot spotting distance

        [Header("Visuals")]
        public Characters.EquipmentVisual Visual = new Characters.EquipmentVisual();

        private void OnEnable()
        {
            Type = Core.ItemType.Clothing;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Slot: " + Slot);
            if (Camouflage > 0f) lines.Add("Camouflage: " + (Camouflage * 100f).ToString("0") + "%");
        }

        public override bool CanEquip { get { return true; } }
        public override Core.EquipmentSlot EquipmentSlot { get { return Slot; } }
    }

    [CreateAssetMenu(menuName = "EXFIL/Items/Grenade", fileName = "Grenade_")]
    public class GrenadeItemDefinition : ItemDefinition
    {
        [Header("Throwable")]
        public float FuseTime = 3.5f;
        public float ThrowForce = 12f;
        public float Damage = 120f;
        public float Radius = 6f;
        public int Fragments = 40;

        private void OnEnable()
        {
            Type = Core.ItemType.Grenade;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Fuse: " + FuseTime.ToString("0.0") + "s");
            lines.Add("Radius: " + Radius.ToString("0") + "m");
            lines.Add("Fragments: " + Fragments);
        }
    }

    [CreateAssetMenu(menuName = "EXFIL/Items/Seed", fileName = "Seed_")]
    public class SeedItemDefinition : ItemDefinition
    {
        [Header("Gardening")]
        public string PlantId;                 // PlantDefinition.Id
        public float BaseGrowTime = 900f;      // seconds of in-game growth
        public int MinYield = 2;
        public int MaxYield = 5;
        public string HarvestItemId;           // produced item

        private void OnEnable()
        {
            Type = Core.ItemType.PlantSeed;
            if (MaxStack < 5) MaxStack = 5;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Grow time: " + (BaseGrowTime / 60f).ToString("0") + " min");
            lines.Add("Yield: " + MinYield + " - " + MaxYield);
        }
    }
}
