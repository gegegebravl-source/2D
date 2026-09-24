using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.Player
{
    /// <summary>
    /// Owns the player's Inventory, the weapon slots, quick-use slots and all item actions
    /// (equip, heal, reload, eat, unload...). UI talks to this, not to the raw inventory.
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        [Header("Handlers")]
        public Combat.WeaponHandler PrimaryHandler;
        public Combat.WeaponHandler SecondaryHandler;
        public Combat.WeaponHandler HolsterHandler;

        [Header("Refs")]
        public EquipmentController Equipment;
        public HealthController Health;
        public SkillSet Skills;
        public uint NetId;

        public Inventory Inventory { get; private set; }
        public int ActiveSlot { get; private set; } = 1;
        public string[] QuickSlots = new string[5];   // index 0 unused, 1..4 quick items

        public Combat.WeaponHandler ActiveWeapon
        {
            get
            {
                switch (ActiveSlot)
                {
                    case 2: return SecondaryHandler;
                    case 3: return HolsterHandler;
                    default: return PrimaryHandler;
                }
            }
        }

        public bool IsBusy { get; private set; }

        private void Awake()
        {
            if (Inventory == null) Inventory = new Inventory();
        }

        public void Initialize(CharacterDefinition definition, Inventory inventory, uint netId, string ownerName)
        {
            Inventory = inventory ?? new Inventory();
            NetId = netId;

            if (Equipment != null) Equipment.Bind(Inventory);
            if (Health != null) Health.NetId = netId;
            if (Skills == null) Skills = GetComponent<SkillSet>();

            if (PrimaryHandler != null) PrimaryHandler.Bind(netId, ownerName, Skills);
            if (SecondaryHandler != null) SecondaryHandler.Bind(netId, ownerName, Skills);
            if (HolsterHandler != null) HolsterHandler.Bind(netId, ownerName, Skills);

            Refresh();
        }

        public void Refresh()
        {
            Inventory.SyncContainersFromEquipment();
            if (Equipment != null) { Equipment.Bind(Inventory); Equipment.Rebuild(); }
        }

        public void SwitchTo(int slot)
        {
            if (IsBusy) return;
            ActiveSlot = Mathf.Clamp(slot, 1, 3);
        }

        public void NextWeapon()
        {
            SwitchTo(ActiveSlot >= 3 ? 1 : ActiveSlot + 1);
        }

        public bool TryFire(bool pressed, bool held, float deltaTime)
        {
            if (IsBusy) return false;
            Combat.WeaponHandler handler = ActiveWeapon;
            return handler != null && handler.TryFire(pressed, held, deltaTime);
        }

        public void Reload()
        {
            Combat.WeaponHandler handler = ActiveWeapon;
            if (handler != null) handler.StartReload();
        }

        public void CycleFireMode()
        {
            Combat.WeaponHandler handler = ActiveWeapon;
            if (handler != null) handler.CycleFireMode();
        }

        // ------------------------------------------------------------------ items
        /// <summary>Equips an item from any container into its slot (returns the replaced item).</summary>
        public ItemInstance EquipItem(string uid)
        {
            ItemInstance item = Inventory.FindItem(uid);
            if (item == null || item.Def == null) return null;

            EquipmentSlot slot = item.Def.EquipmentSlot;
            if (slot == EquipmentSlot.None) return null;

            Inventory.Remove(uid, out ItemInstance taken);
            ItemInstance previous = Inventory.Equip(slot, taken);

            if (previous != null)
            {
                string reason;
                Inventory.TryAdd(previous, out reason);
            }

            Refresh();
            return previous;
        }

        public bool UnequipSlot(EquipmentSlot slot)
        {
            ItemInstance item = Inventory.Unequip(slot);
            if (item == null) return false;
            string reason;
            bool ok = Inventory.TryAdd(item, out reason);
            if (!ok) Inventory.Equip(slot, item);
            Refresh();
            return ok;
        }

        public void SetQuickSlot(int index, string uid)
        {
            if (index < 1 || index >= QuickSlots.Length) return;
            QuickSlots[index] = uid;
        }

        /// <summary>Uses a medical item / food / drink from a quick slot or by uid.</summary>
        public bool UseItem(string uid)
        {
            if (IsBusy) return false;
            ItemInstance item = Inventory.FindItem(uid);
            if (item == null || item.Def == null) return false;

            MedicalItemDefinition medical = item.Def as MedicalItemDefinition;
            if (medical != null) { StartCoroutine(UseMedical(item, medical)); return true; }

            ConsumableItemDefinition consumable = item.Def as ConsumableItemDefinition;
            if (consumable != null) { StartCoroutine(UseConsumable(item, consumable)); return true; }

            SeedItemDefinition seed = item.Def as SeedItemDefinition;
            if (seed != null) return false; // planting is done from the garden UI

            return false;
        }

        private IEnumerator UseMedical(ItemInstance item, MedicalItemDefinition medical)
        {
            IsBusy = true;
            float elapsed = 0f;
            float resource = item.Durability < 0f ? medical.MaxResource : item.Durability;

            while (elapsed < medical.UseTime)
            {
                elapsed += Time.deltaTime;
                float heal = medical.HealPerSecond * Time.deltaTime;
                resource -= heal;
                if (Health != null) Health.HealAll(heal);
                yield return null;
            }

            for (int i = 0; i < medical.Removes.Count; i++)
                if (Health != null) Health.RemoveEffect(medical.Removes[i]);
            for (int i = 0; i < medical.Adds.Count; i++)
                if (Health != null) Health.AddEffect(medical.Adds[i], 30f);
            if (medical.PainkillerDuration > 0f && Health != null)
                Health.AddEffect(HealthEffectType.Painkiller, medical.PainkillerDuration);

            item.Durability = resource;
            if (resource <= 0f)
            {
                Inventory.Remove(item.Uid, out ItemInstance removed);
                removed = null;
            }
            IsBusy = false;
            Refresh();
        }

        private IEnumerator UseConsumable(ItemInstance item, ConsumableItemDefinition consumable)
        {
            IsBusy = true;
            yield return new WaitForSeconds(consumable.UseTime);

            if (Health != null)
            {
                Health.Consume(consumable.Energy, consumable.Hydration);
                for (int i = 0; i < consumable.Adds.Count; i++) Health.AddEffect(consumable.Adds[i], 20f);
                for (int i = 0; i < consumable.Removes.Count; i++) Health.RemoveEffect(consumable.Removes[i]);
            }

            item.Stack -= 1;
            if (item.Stack <= 0) Inventory.Remove(item.Uid, out ItemInstance removed);
            IsBusy = false;
            Refresh();
        }

        /// <summary>Everything that must follow the player into the secure container on death.</summary>
        public List<ItemInstance> GetKeptOnDeath()
        {
            List<ItemInstance> kept = new List<ItemInstance>();
            ContainerInstance secure = Inventory.GetContainer("secure");
            if (secure != null) kept.AddRange(secure.Grid.Items);
            return kept;
        }

        /// <summary>Everything that is dropped as loot on death.</summary>
        public List<ItemInstance> GetDroppedOnDeath()
        {
            List<ItemInstance> dropped = new List<ItemInstance>();
            foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in Inventory.AllEquipment())
            {
                if (pair.Key == EquipmentSlot.SecureContainer) continue;
                if (pair.Value != null) dropped.Add(pair.Value);
            }
            foreach (ContainerInstance container in Inventory.Containers)
            {
                if (container.ContainerId == "secure") continue;
                dropped.AddRange(container.Grid.Items);
            }
            return dropped;
        }
    }
}
