using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    /// <summary>
    /// Full character inventory: equipped slots (weapons, armor, containers) plus the
    /// grids those containers provide. Handles add/remove/move/merge/swap and weight.
    /// </summary>
    [Serializable]
    public class Inventory
    {
        public const string StashContainerId = "stash";
        public const string PocketsContainerId = "pockets";

        [SerializeField] private List<ContainerInstance> containers = new List<ContainerInstance>();
        [SerializeField] private List<EquippedEntry> equipped = new List<EquippedEntry>();

        [Serializable]
        public class EquippedEntry
        {
            public int Slot;              // Core.EquipmentSlot as int (serializable)
            public ItemInstance Item;
        }

        public List<ContainerInstance> Containers { get { return containers; } }

        // ------------------------------------------------------------ containers
        public ContainerInstance GetContainer(string id)
        {
            for (int i = 0; i < containers.Count; i++)
                if (containers[i].ContainerId == id) return containers[i];
            return null;
        }

        public ContainerInstance AddContainer(string id, int width, int height, string definitionId = "", float weightReduction = 0f)
        {
            ContainerInstance existing = GetContainer(id);
            if (existing != null) return existing;
            ContainerInstance container = new ContainerInstance(id, width, height, definitionId, weightReduction);
            containers.Add(container);
            return container;
        }

        /// <summary>Rebuilds grids from equipped containers (pockets + rig + backpack + secure).</summary>
        public void SyncContainersFromEquipment()
        {
            // keep stash and pockets, drop gear driven ones
            for (int i = containers.Count - 1; i >= 0; i--)
            {
                string id = containers[i].ContainerId;
                if (id == StashContainerId || id == PocketsContainerId) continue;
                containers.RemoveAt(i);
            }

            ItemInstance vest = GetEquipped(Core.EquipmentSlot.ArmorVest);
            AddRig(vest, "vest");

            ItemInstance rig = GetEquipped(Core.EquipmentSlot.ChestRig);
            AddRig(rig, "rig");

            ItemInstance backpack = GetEquipped(Core.EquipmentSlot.Backpack);
            if (backpack != null)
            {
                ContainerItemDefinition def = backpack.Def as ContainerItemDefinition;
                if (def != null)
                    AddContainer("backpack", def.GridSize.x, def.GridSize.y, def.Id, def.WeightReduction);
            }

            ItemInstance secure = GetEquipped(Core.EquipmentSlot.SecureContainer);
            if (secure != null)
            {
                ContainerItemDefinition def = secure.Def as ContainerItemDefinition;
                if (def != null)
                    AddContainer("secure", def.GridSize.x, def.GridSize.y, def.Id, def.WeightReduction);
            }
        }

        private void AddRig(ItemInstance item, string key)
        {
            if (item == null) return;
            ArmorItemDefinition armor = item.Def as ArmorItemDefinition;
            if (armor != null && armor.RigGridSize.x > 0 && armor.RigGridSize.y > 0)
            {
                AddContainer(key, armor.RigGridSize.x, armor.RigGridSize.y, armor.Id);
                return;
            }
            ContainerItemDefinition container = item.Def as ContainerItemDefinition;
            if (container != null)
                AddContainer(key, container.GridSize.x, container.GridSize.y, container.Id, container.WeightReduction);
        }

        // ------------------------------------------------------------- equipment
        public ItemInstance GetEquipped(Core.EquipmentSlot slot)
        {
            int key = (int)slot;
            for (int i = 0; i < equipped.Count; i++)
                if (equipped[i].Slot == key) return equipped[i].Item;
            return null;
        }

        /// <summary>Equips an item, returns the previously equipped one (may be null).</summary>
        public ItemInstance Equip(Core.EquipmentSlot slot, ItemInstance item, bool syncContainers = true)
        {
            int key = (int)slot;
            ItemInstance previous = null;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].Slot == key)
                {
                    previous = equipped[i].Item;
                    equipped[i].Item = item;
                    if (syncContainers) SyncContainersFromEquipment();
                    return previous;
                }
            }
            equipped.Add(new EquippedEntry { Slot = key, Item = item });
            if (syncContainers) SyncContainersFromEquipment();
            return previous;
        }

        public ItemInstance Unequip(Core.EquipmentSlot slot)
        {
            int key = (int)slot;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].Slot == key)
                {
                    ItemInstance item = equipped[i].Item;
                    equipped[i].Item = null;
                    SyncContainersFromEquipment();
                    return item;
                }
            }
            return null;
        }

        public List<KeyValuePair<Core.EquipmentSlot, ItemInstance>> AllEquipment()
        {
            List<KeyValuePair<Core.EquipmentSlot, ItemInstance>> result =
                new List<KeyValuePair<Core.EquipmentSlot, ItemInstance>>();
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].Item == null) continue;
                result.Add(new KeyValuePair<Core.EquipmentSlot, ItemInstance>(
                    (Core.EquipmentSlot)equipped[i].Slot, equipped[i].Item));
            }
            return result;
        }

        // ------------------------------------------------------------ item flow
        public bool TryAdd(ItemInstance item, out ItemInstance leftover, string preferredContainer = null)
        {
            leftover = item;
            if (item == null) return false;

            if (preferredContainer != null)
            {
                ContainerInstance preferred = GetContainer(preferredContainer);
                if (preferred != null && TryAddStacked(preferred, item)) { leftover = null; return true; }
            }

            string[] order = { "secure", "pockets", "rig", "vest", "backpack", StashContainerId };
            for (int i = 0; i < order.Length; i++)
            {
                ContainerInstance container = GetContainer(order[i]);
                if (container == null) continue;
                if (TryAddStacked(container, item)) { leftover = null; return true; }
            }

            if (item.Stack > item.MaxStack)
            {
                ItemInstance part = item.Split(item.MaxStack);
                foreach (string key in order)
                {
                    ContainerInstance container = GetContainer(key);
                    if (container == null) continue;
                    if (TryAddStacked(container, item)) { leftover = part; return true; }
                }
            }
            return false;
        }

        private bool TryAddStacked(ContainerInstance container, ItemInstance item)
        {
            if (container == null || item == null) return false;

            if (item.MaxStack > 1)
            {
                container.Grid.ForEachItem(existing =>
                {
                    if (item.Stack <= 0 || !existing.CanStackWith(item)) return;
                    int space = existing.MaxStack - existing.Stack;
                    if (space <= 0) return;
                    int moved = Mathf.Min(space, item.Stack);
                    existing.Stack += moved;
                    item.Stack -= moved;
                });
                if (item.Stack <= 0) return true;
            }

            int x, y;
            bool rotated;
            bool placed = container.Grid.TryAutoPlace(item, out x, out y, out rotated);
            if (placed && item.Stack <= 0) return true;
            return placed;
        }

        public bool Remove(string uid, out ItemInstance removed)
        {
            removed = null;
            for (int i = 0; i < containers.Count; i++)
            {
                ContainerInstance container = containers[i];
                ItemInstance item = container.Grid.Find(uid);
                if (item == null) continue;
                container.Grid.Remove(uid);
                removed = item;
                return true;
            }
            return false;
        }

        public ItemInstance FindItem(string uid)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                ItemInstance item = containers[i].Grid.Find(uid);
                if (item != null) return item;
            }
            List<KeyValuePair<Core.EquipmentSlot, ItemInstance>> gear = AllEquipment();
            for (int i = 0; i < gear.Count; i++)
                if (gear[i].Value.Uid == uid) return gear[i].Value;
            return null;
        }

        public int CountOf(string definitionId)
        {
            int total = 0;
            for (int i = 0; i < containers.Count; i++)
                containers[i].Grid.ForEachItem(item =>
                {
                    if (item.DefinitionId == definitionId) total += item.Stack;
                });
            return total;
        }

        public bool Consume(string definitionId, int amount)
        {
            if (CountOf(definitionId) < amount) return false;
            int left = amount;
            for (int i = 0; i < containers.Count; i++)
            {
                InventoryGrid grid = containers[i].Grid;
                for (int j = grid.Items.Count - 1; j >= 0 && left > 0; j--)
                {
                    ItemInstance item = grid.Items[j];
                    if (item == null || item.DefinitionId != definitionId) continue;
                    if (item.Stack <= left)
                    {
                        left -= item.Stack;
                        grid.Remove(item.Uid);
                    }
                    else
                    {
                        item.Stack -= left;
                        left = 0;
                    }
                }
            }
            return left <= 0;
        }

        // ---------------------------------------------------------------- weight
        public float TotalWeight()
        {
            float weight = 0f;
            for (int i = 0; i < containers.Count; i++)
                weight += containers[i].ContentsWeight();

            List<KeyValuePair<Core.EquipmentSlot, ItemInstance>> gear = AllEquipment();
            for (int i = 0; i < gear.Count; i++)
            {
                if (gear[i].Value == null) continue;
                ContainerItemDefinition containerDef = gear[i].Value.Def as ContainerItemDefinition;
                if (containerDef != null && containerDef.Kind == ContainerItemDefinition.ContainerKind.Backpack)
                    continue; // contents counted above
                if (containerDef != null && containerDef.Kind == ContainerItemDefinition.ContainerKind.ChestRig)
                    continue;
                weight += gear[i].Value.TotalWeight;
            }
            return weight;
        }

        public List<ItemInstance> AllItems()
        {
            List<ItemInstance> all = new List<ItemInstance>();
            for (int i = 0; i < containers.Count; i++)
                for (int j = 0; j < containers[i].Grid.Items.Count; j++)
                    if (containers[i].Grid.Items[j] != null) all.Add(containers[i].Grid.Items[j]);
            return all;
        }

        public void Clear()
        {
            containers.Clear();
            equipped.Clear();
        }
    }
}
