using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    [CreateAssetMenu(menuName = "EXFIL/Items/Armor", fileName = "Armor_")]
    public class ArmorItemDefinition : ItemDefinition
    {
        [Header("Protection")]
        [EnumFlags] public Core.BodyZone Zones = Core.BodyZone.Thorax | Core.BodyZone.Stomach;
        public Core.ArmorClass ArmorClass = Core.ArmorClass.Three;
        public Core.ArmorMaterial Material = Core.ArmorMaterial.Steel;
        public float MaxDurability = 60f;
        public float RicochetChance = 0.12f;
        public int PlateSlots = 0;

        [Header("Penalties")]
        public float MoveSpeedPenalty = 0.02f;
        public float TurnSpeedPenalty = 0.03f;
        public float ErgonomicsPenalty = 5f;

        [Header("Capacity (rigs only)")]
        public Vector2Int RigGridSize = new Vector2Int(0, 0);

        [Header("Visuals")]
        public Characters.EquipmentVisual Visual = new Characters.EquipmentVisual();

        private void OnEnable()
        {
            if (Type != Core.ItemType.Helmet && Type != Core.ItemType.ChestRig)
                Type = Core.ItemType.Armor;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Class: " + (int)ArmorClass);
            lines.Add("Zones: " + Zones);
            lines.Add("Durability: " + MaxDurability.ToString("0") + " / " + MaxDurability.ToString("0"));
            if (MoveSpeedPenalty > 0f) lines.Add("Movement: -" + (MoveSpeedPenalty * 100f).ToString("0") + "%");
        }

        public override bool CanEquip { get { return true; } }
        public override Core.EquipmentSlot EquipmentSlot
        {
            get
            {
                if (Type == Core.ItemType.Helmet) return Core.EquipmentSlot.Helmet;
                if (Type == Core.ItemType.ChestRig) return Core.EquipmentSlot.ChestRig;
                return Core.EquipmentSlot.ArmorVest;
            }
        }
    }
}
