using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    [Serializable]
    public class GridPlacement
    {
        public string Uid;
        public int X;
        public int Y;
        public bool Rotated;

        public GridPlacement() { }

        public GridPlacement(string uid, int x, int y, bool rotated)
        {
            Uid = uid;
            X = x;
            Y = y;
            Rotated = rotated;
        }
    }

    /// <summary>
    /// Tarkov style 2D tetris container: items occupy Width x Height cells, can be rotated,
    /// cannot overlap. Pure data - the UI (see UI/InventoryGridUI) renders it.
    /// </summary>
    [Serializable]
    public class InventoryGrid
    {
        public int Width = 4;
        public int Height = 4;
        public List<GridPlacement> Placements = new List<GridPlacement>();
        public List<ItemInstance> Items = new List<ItemInstance>();

        public InventoryGrid() { }

        public InventoryGrid(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public bool IsEmpty { get { return Items.Count == 0; } }

        public ItemInstance Find(string uid)
        {
            for (int i = 0; i < Items.Count; i++)
                if (Items[i] != null && Items[i].Uid == uid) return Items[i];
            return null;
        }

        public GridPlacement PlacementOf(string uid)
        {
            for (int i = 0; i < Placements.Count; i++)
                if (Placements[i].Uid == uid) return Placements[i];
            return null;
        }

        public Vector2Int Footprint(ItemInstance item)
        {
            if (item == null || item.Def == null) return new Vector2Int(1, 1);
            return new Vector2Int(Mathf.Max(1, item.Def.Size.x), Mathf.Max(1, item.Def.Size.y));
        }

        public bool InBounds(int x, int y, int w, int h)
        {
            return x >= 0 && y >= 0 && x + w <= Width && y + h <= Height;
        }

        public bool CanPlace(ItemInstance item, int x, int y, bool rotated, string ignoreUid = null)
        {
            if (item == null) return false;
            Vector2Int size = Footprint(item);
            int w = rotated ? size.y : size.x;
            int h = rotated ? size.x : size.y;
            if (!InBounds(x, y, w, h)) return false;

            for (int i = 0; i < Placements.Count; i++)
            {
                GridPlacement other = Placements[i];
                if (other.Uid == ignoreUid || other.Uid == item.Uid) continue;
                ItemInstance otherItem = Find(other.Uid);
                if (otherItem == null) continue;

                Vector2Int otherSize = Footprint(otherItem);
                int ow = other.Rotated ? otherSize.y : otherSize.x;
                int oh = other.Rotated ? otherSize.x : otherSize.y;

                if (x < other.X + ow && x + w > other.X && y < other.Y + oh && y + h > other.Y)
                    return false;
            }
            return true;
        }

        public bool Add(ItemInstance item, int x, int y, bool rotated)
        {
            if (!CanPlace(item, x, y, rotated)) return false;
            Items.Add(item);
            Placements.Add(new GridPlacement(item.Uid, x, y, rotated));
            return true;
        }

        /// <summary>First free spot scanning row-major; used by auto-loot and quick move.</summary>
        public bool TryAutoPlace(ItemInstance item, out int x, out int y, out bool rotated)
        {
            Vector2Int size = Footprint(item);
            for (int r = 0; r < 2; r++)
            {
                bool rot = r == 1;
                int w = rot ? size.y : size.x;
                int h = rot ? size.x : size.y;
                for (int yy = 0; yy <= Height - h; yy++)
                    for (int xx = 0; xx <= Width - w; xx++)
                        if (CanPlace(item, xx, yy, rot))
                        {
                            x = xx;
                            y = yy;
                            rotated = rot;
                            return Add(item, xx, yy, rot);
                        }
            }
            x = y = 0;
            rotated = false;
            return false;
        }

        public bool MoveTo(string uid, int x, int y, bool rotated)
        {
            ItemInstance item = Find(uid);
            if (item == null) return false;
            if (!CanPlace(item, x, y, rotated, uid)) return false;
            GridPlacement placement = PlacementOf(uid);
            if (placement == null) return false;
            placement.X = x;
            placement.Y = y;
            placement.Rotated = rotated;
            return true;
        }

        public bool Remove(string uid)
        {
            ItemInstance item = Find(uid);
            if (item == null) return false;
            Items.Remove(item);
            for (int i = Placements.Count - 1; i >= 0; i--)
                if (Placements[i].Uid == uid) Placements.RemoveAt(i);
            return true;
        }

        public int FreeCells()
        {
            int used = 0;
            for (int i = 0; i < Placements.Count; i++)
            {
                ItemInstance item = Find(Placements[i].Uid);
                if (item == null) continue;
                Vector2Int size = Footprint(item);
                used += size.x * size.y;
            }
            return Width * Height - used;
        }

        public float TotalWeight()
        {
            float total = 0f;
            for (int i = 0; i < Items.Count; i++)
                if (Items[i] != null) total += Items[i].TotalWeight;
            return total;
        }

        public void ForEachItem(Action<ItemInstance> action)
        {
            for (int i = 0; i < Items.Count; i++)
                if (Items[i] != null) action(Items[i]);
        }
    }

    /// <summary>A container instance (backpack, pockets, rig, secure case, stash...).</summary>
    [Serializable]
    public class ContainerInstance
    {
        public string ContainerId;      // item instance uid of the container, "stash" for hideout
        public string DefinitionId;     // may be empty (pockets / stash)
        public InventoryGrid Grid = new InventoryGrid(4, 4);
        public float WeightReduction;

        public ContainerInstance() { }

        public ContainerInstance(string containerId, int width, int height, string definitionId = "", float weightReduction = 0f)
        {
            ContainerId = containerId;
            DefinitionId = definitionId;
            Grid = new InventoryGrid(width, height);
            WeightReduction = weightReduction;
        }

        public bool TryAdd(ItemInstance item, out string reason)
        {
            int x, y;
            bool rotated;
            if (Grid.TryAutoPlace(item, out x, out y, out rotated))
            {
                reason = null;
                return true;
            }
            reason = "No space";
            return false;
        }

        public float ContentsWeight()
        {
            return Grid.TotalWeight() * (1f - Mathf.Clamp01(WeightReduction));
        }
    }
}
