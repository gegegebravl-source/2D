using System.Collections.Generic;
using UnityEngine;
using EXFIL.Items;
using EXFIL.Player;

namespace EXFIL.Raid
{
    /// <summary>
    /// A searchable container in the world (corpse, crate, cabinet). Holds ItemInstances,
    /// opens a shared loot UI window when searched.
    /// </summary>
    public class LootPile : MonoBehaviour, IInteractable
    {
        public string ContainerName = "Crate";
        public List<ItemInstance> Items = new List<ItemInstance>();
        public float SearchTime = 2f;
        public bool IsSearched;

        public Transform Transform { get { return transform; } }

        public static LootPile Create(Vector3 position, string name = "Corpse")
        {
            GameObject go = new GameObject("Loot_" + name);
            go.transform.position = position;
            LootPile pile = go.AddComponent<LootPile>();
            pile.ContainerName = name;

            // real CC0 prop when the art pipeline ran, primitive stand-in otherwise
            GameObject prop = null;
            if (Art.ModelLibrary.HasModels)
            {
                string tag = name != null && name.ToLowerInvariant().Contains("corpse") ? "trunk" : "crate";
                prop = Art.ModelLibrary.SpawnTagged(tag, go.transform, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), 0.75f);
            }
            if (prop != null)
            {
                prop.transform.localPosition = Vector3.zero;
            }
            else
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = new Vector3(0.7f, 0.4f, 0.5f);
                visual.transform.localPosition = Vector3.up * 0.2f;
                Renderer fallbackRenderer = visual.GetComponent<Renderer>();
                if (fallbackRenderer != null)
                    fallbackRenderer.sharedMaterial = Art.VisualMaterials.Solid("loot", new Color(0.32f, 0.26f, 0.18f));
            }
            return pile;
        }

        public void AddItems(IEnumerable<ItemInstance> items)
        {
            Items.AddRange(items);
        }

        public string GetPrompt(GameObject actor)
        {
            return IsSearched ? string.Empty : "Search " + ContainerName;
        }

        public bool CanInteract(GameObject actor)
        {
            return true;
        }

        public float GetInteractTime(GameObject actor)
        {
            PlayerLoadout loadout = actor != null ? actor.GetComponentInParent<PlayerLoadout>() : null;
            float speed = loadout != null && loadout.Skills != null ? loadout.Skills.GetSearchSpeedMultiplier() : 1f;
            return SearchTime / speed;
        }

        public void Interact(GameObject actor)
        {
            IsSearched = true;
            UI.GameUI.Instance?.OpenLoot(this, actor);
        }
    }
}
