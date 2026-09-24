using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    /// <summary>
    /// Single registry of every ItemDefinition. Auto-collected from Resources/Data/Items
    /// and from the asset itself (see EXFIL/Setup in the editor).
    /// </summary>
    [CreateAssetMenu(menuName = "EXFIL/Database/Item database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

        private static ItemDatabase _instance;
        private Dictionary<string, ItemDefinition> _byId;

        public static ItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ItemDatabase>("Data/ItemDatabase");
                    if (_instance == null)
                        _instance = Resources.Load<ItemDatabase>("ItemDatabase");
                }
                return _instance;
            }
        }

        public List<ItemDefinition> Items { get { return items; } }

        public static void SetInstance(ItemDatabase db)
        {
            _instance = db;
            if (db != null) db.Rebuild();
        }

        public void Rebuild()
        {
            _byId = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition def = items[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (_byId.ContainsKey(def.Id))
                {
                    Debug.LogWarning("[EXFIL] Duplicate item id: " + def.Id);
                    continue;
                }
                _byId.Add(def.Id, def);
            }
        }

        public bool Contains(string id)
        {
            Ensure();
            return !string.IsNullOrEmpty(id) && _byId.ContainsKey(id);
        }

        public ItemDefinition Get(string id)
        {
            Ensure();
            ItemDefinition def;
            return _byId.TryGetValue(id, out def) ? def : null;
        }

        public T Get<T>(string id) where T : ItemDefinition
        {
            return Get(id) as T;
        }

        public void Add(ItemDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            if (!items.Contains(def)) items.Add(def);
        }

        public List<T> All<T>() where T : ItemDefinition
        {
            Ensure();
            List<T> result = new List<T>();
            for (int i = 0; i < items.Count; i++)
            {
                T typed = items[i] as T;
                if (typed != null) result.Add(typed);
            }
            return result;
        }

        private void Ensure()
        {
            if (_byId == null) Rebuild();
        }

        // ------------------------------------------------------- static shortcuts
        public static ItemDefinition Find(string id)
        {
            ItemDatabase db = Instance;
            return db != null ? db.Get(id) : null;
        }

        public static T Find<T>(string id) where T : ItemDefinition
        {
            return Find(id) as T;
        }
    }

    /// <summary>Plain trade / barter / quest good (no special behaviour).</summary>
    [CreateAssetMenu(menuName = "EXFIL/Items/Barter item", fileName = "Item_")]
    public class BarterItemDefinition : ItemDefinition
    {
        private void OnEnable()
        {
            if (Type == Core.ItemType.Any) Type = Core.ItemType.Barter;
        }
    }

    /// <summary>Money stacks (roubles, dollars, euros).</summary>
    [CreateAssetMenu(menuName = "EXFIL/Items/Currency", fileName = "Currency_")]
    public class CurrencyItemDefinition : ItemDefinition
    {
        private void OnEnable()
        {
            Type = Core.ItemType.Currency;
        }
    }

    /// <summary>Keycard / door key with a matching lock id.</summary>
    [CreateAssetMenu(menuName = "EXFIL/Items/Key", fileName = "Key_")]
    public class KeyItemDefinition : ItemDefinition
    {
        public string LockId;
        public int Usages = 10;

        private void OnEnable()
        {
            Type = Core.ItemType.Key;
        }

        public override void CollectTooltipLines(System.Collections.Generic.List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Lock: " + LockId);
            lines.Add("Usages: " + Usages);
        }
    }
}
