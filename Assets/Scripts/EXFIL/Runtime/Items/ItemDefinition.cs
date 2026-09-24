using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    /// <summary>
    /// Base ScriptableObject for every item in the game. Specialised fields live in
    /// subclasses (weapon / armor / ammo / medical ...). One asset per item id.
    /// </summary>
    public abstract class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public string ShortName;
        [TextArea(2, 6)] public string Description;

        [Header("Classification")]
        public Core.ItemType Type = Core.ItemType.Barter;
        public Core.ItemRarity Rarity = Core.ItemRarity.Common;

        [Header("Grid")]
        public Vector2Int Size = new Vector2Int(1, 1);

        [Header("Economy")]
        public int BasePrice = 100;
        public float Weight = 0.1f;

        [Header("Rules")]
        public int MaxStack = 1;
        public bool CanBeSold = true;
        public bool IsQuestItem;
        public float ExamineTime = 0.5f;

        [Header("Visuals / Audio")]
        public Sprite Icon;
        public GameObject WorldPrefab;
        public AudioClip UseSound;
        public AudioClip DropSound;

        public virtual string GetTypeName()
        {
            return Type.ToString();
        }

        /// <summary>Extra lines shown in the item tooltip.</summary>
        public virtual void CollectTooltipLines(List<string> lines)
        {
            lines.Add("Weight: " + Weight.ToString("0.00") + " kg");
            if (MaxStack > 1) lines.Add("Stack: " + Stack + " / " + MaxStack);
        }

        public virtual int Stack
        {
            get { return MaxStack; }
        }

        public virtual bool CanEquip
        {
            get { return false; }
        }

        public virtual Core.EquipmentSlot EquipmentSlot
        {
            get { return Core.EquipmentSlot.None; }
        }
    }
}
