using System.Collections.Generic;
using UnityEngine;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.Combat
{
    public class ArmorCover
    {
        public ArmorItemDefinition Definition;
        public ItemInstance Instance;
    }

    /// <summary>Reads equipped armor and answers "what covers this body part?".</summary>
    public class ArmorController : MonoBehaviour
    {
        private readonly Dictionary<BodyPart, ArmorCover> _cover = new Dictionary<BodyPart, ArmorCover>();
        private Inventory _inventory;

        public float MoveSpeedPenalty { get; private set; }
        public float TurnSpeedPenalty { get; private set; }
        public float ErgonomicsPenalty { get; private set; }

        public void Bind(Inventory inventory)
        {
            _inventory = inventory;
            Rebuild();
        }

        public void Rebuild()
        {
            _cover.Clear();
            MoveSpeedPenalty = TurnSpeedPenalty = ErgonomicsPenalty = 0f;
            if (_inventory == null) return;

            List<KeyValuePair<EquipmentSlot, ItemInstance>> gear = _inventory.AllEquipment();
            List<ArmorItemDefinition> used = new List<ArmorItemDefinition>();

            for (int i = 0; i < gear.Count; i++)
            {
                ItemInstance instance = gear[i].Value;
                ArmorItemDefinition armor = instance != null ? instance.Def as ArmorItemDefinition : null;
                if (armor == null) continue;

                ApplyZone(armor, instance, BodyZone.Head, BodyPart.Head);
                ApplyZone(armor, instance, BodyZone.Thorax, BodyPart.Thorax);
                ApplyZone(armor, instance, BodyZone.Stomach, BodyPart.Stomach);

                if (!used.Contains(armor))
                {
                    used.Add(armor);
                    MoveSpeedPenalty += armor.MoveSpeedPenalty;
                    TurnSpeedPenalty += armor.TurnSpeedPenalty;
                    ErgonomicsPenalty += armor.ErgonomicsPenalty;
                }
            }
        }

        private void ApplyZone(ArmorItemDefinition armor, ItemInstance instance, BodyZone zone, BodyPart part)
        {
            if ((armor.Zones & zone) == 0) return;
            if (_cover.ContainsKey(part)) return;
            _cover[part] = new ArmorCover { Definition = armor, Instance = instance };
        }

        public ArmorCover GetCover(BodyPart part)
        {
            ArmorCover cover;
            return _cover.TryGetValue(part, out cover) ? cover : null;
        }

        public void DamageArmor(ArmorCover cover, float amount)
        {
            if (cover == null || cover.Instance == null) return;
            cover.Instance.Durability = Mathf.Max(0f, cover.Instance.CurrentDurability - amount);
        }
    }
}
