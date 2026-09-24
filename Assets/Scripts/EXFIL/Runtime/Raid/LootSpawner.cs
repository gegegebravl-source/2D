using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.Raid
{
    /// <summary>A marked spot that becomes a filled container when the raid starts.</summary>
    public class LootSpawner : MonoBehaviour
    {
        public string ContainerName = "Supply crate";
        public string[] LootTableIds = new string[0];
        public int MinItems = 2;
        public int MaxItems = 6;
        public float SearchTime = 2.4f;
        public Mesh RendererMesh;

        private static Dictionary<string, List<string>> _tables = new Dictionary<string, List<string>>();

        public static void RegisterTable(string id, List<string> itemIds)
        {
            _tables[id] = itemIds;
        }

        public void Fill(Rng rng)
        {
            LootPile pile = LootPile.Create(transform.position, ContainerName);
            pile.SearchTime = SearchTime;
            pile.transform.rotation = transform.rotation;

            int count = rng.Range(MinItems, MaxItems + 1);
            for (int i = 0; i < count; i++)
            {
                string itemId = Pick(rng);
                if (string.IsNullOrEmpty(itemId)) continue;
                ItemDefinition def = ItemDatabase.Find(itemId);
                if (def == null) continue;
                pile.Items.Add(ItemInstance.Create(def, 1));
            }
        }

        private string Pick(Rng rng)
        {
            List<string> pool = new List<string>();
            for (int i = 0; i < LootTableIds.Length; i++)
            {
                List<string> table;
                if (LootTableIds[i] != null && _tables.TryGetValue(LootTableIds[i], out table))
                    pool.AddRange(table);
            }
            if (pool.Count == 0) return null;
            return rng.Pick(pool);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.3f, new Vector3(0.7f, 0.5f, 0.6f));
        }
    }
}
