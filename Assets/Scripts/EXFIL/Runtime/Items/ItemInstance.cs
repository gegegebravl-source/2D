using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    /// <summary>
    /// Runtime (mutable) copy of an item: stack size, durability, loaded rounds,
    /// installed modules, nested contents. JSON friendly: only ids and primitives.
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        public string Uid;
        public string DefinitionId;
        public int Stack = 1;
        public float Durability = -1f;          // <0 = use definition default
        public float MaxDurability = -1f;
        public List<string> Rounds = new List<string>();      // loaded ammo ids (magazines / chambers)
        public List<string> Modules = new List<string>();     // installed attachment ids
        public List<ItemInstance> Contents = new List<ItemInstance>(); // nested (cases, backpacks)
        public bool IsExamined = true;

        [NonSerialized] private ItemDefinition _cached;

        public ItemInstance() { }

        public ItemInstance(string definitionId, int stack = 1)
        {
            Uid = Guid.NewGuid().ToString("N");
            DefinitionId = definitionId;
            Stack = Mathf.Max(1, stack);
        }

        public static ItemInstance Create(ItemDefinition def, int stack = 1)
        {
            ItemInstance instance = new ItemInstance(def != null ? def.Id : string.Empty, stack);
            if (def != null)
            {
                WeaponItemDefinition weapon = def as WeaponItemDefinition;
                if (weapon != null)
                {
                    instance.MaxDurability = weapon.MaxDurability;
                    instance.Durability = weapon.MaxDurability;
                    instance.Modules.AddRange(weapon.DefaultModules);
                    if (weapon.DefaultMagazine != null)
                    {
                        ItemInstance mag = new ItemInstance(weapon.DefaultMagazine.Id, 1);
                        MagazineItemDefinition magDef = weapon.DefaultMagazine;
                        if (weapon.DefaultAmmo != null)
                            for (int i = 0; i < magDef.Capacity; i++)
                                mag.Rounds.Add(weapon.DefaultAmmo.Id);
                        instance.Contents.Add(mag);
                    }
                }

                ArmorItemDefinition armor = def as ArmorItemDefinition;
                if (armor != null)
                {
                    instance.MaxDurability = armor.MaxDurability;
                    instance.Durability = armor.MaxDurability;
                }
            }
            return instance;
        }

        public ItemDefinition Def
        {
            get
            {
                if (_cached == null)
                    _cached = ItemDatabase.Find(DefinitionId);
                return _cached;
            }
        }

        public string DisplayName
        {
            get { return Def != null ? Def.DisplayName : DefinitionId; }
        }

        public float CurrentDurability
        {
            get { return Durability < 0f ? 1f : Durability; }
            set { Durability = Mathf.Max(0f, value); }
        }

        public float Durability01
        {
            get
            {
                float max = MaxDurability > 0f ? MaxDurability : 1f;
                return Mathf.Clamp01(CurrentDurability / max);
            }
        }

        public int MaxStack
        {
            get { return Def != null ? Mathf.Max(1, Def.MaxStack) : 1; }
        }

        public Vector2Int Size(Vector2Int fallback)
        {
            return Def != null ? Def.Size : fallback;
        }

        public float TotalWeight
        {
            get
            {
                float weight = Def != null ? Def.Weight : 0f;
                for (int i = 0; i < Contents.Count; i++)
                    if (Contents[i] != null) weight += Contents[i].TotalWeight;
                return weight;
            }
        }

        public bool CanStackWith(ItemInstance other)
        {
            if (other == null || other == this) return false;
            if (other.DefinitionId != DefinitionId) return false;
            if (MaxStack <= 1) return false;
            if (Rounds.Count > 0 || other.Rounds.Count > 0) return false;
            if (Modules.Count > 0 || other.Modules.Count > 0) return false;
            if (Contents.Count > 0 || other.Contents.Count > 0) return false;
            return true;
        }

        public ItemInstance Split(int amount)
        {
            if (amount >= Stack) return null;
            ItemInstance part = new ItemInstance(DefinitionId, amount);
            Stack -= amount;
            return part;
        }

        /// <summary>Ammo id of the next round that will be fired (mag first, else chamber).</summary>
        public string PeekRound()
        {
            ItemInstance mag = GetMagazine();
            if (mag != null && mag.Rounds.Count > 0)
                return mag.Rounds[mag.Rounds.Count - 1];
            return Rounds.Count > 0 ? Rounds[Rounds.Count - 1] : null;
        }

        public ItemInstance GetMagazine()
        {
            for (int i = 0; i < Contents.Count; i++)
            {
                ItemInstance c = Contents[i];
                if (c != null && c.Def is MagazineItemDefinition)
                    return c;
            }
            return null;
        }

        public int AmmoCount
        {
            get
            {
                ItemInstance mag = GetMagazine();
                return mag != null ? mag.Rounds.Count : Rounds.Count;
            }
        }

        public bool ConsumeRound()
        {
            ItemInstance mag = GetMagazine();
            if (mag != null && mag.Rounds.Count > 0)
            {
                mag.Rounds.RemoveAt(mag.Rounds.Count - 1);
                return true;
            }
            if (Rounds.Count > 0)
            {
                Rounds.RemoveAt(Rounds.Count - 1);
                return true;
            }
            return false;
        }

        public bool LoadRound(string ammoId)
        {
            ItemInstance mag = GetMagazine();
            MagazineItemDefinition magDef = mag != null ? mag.Def as MagazineItemDefinition : null;
            if (magDef != null)
            {
                if (mag.Rounds.Count >= magDef.Capacity) return false;
                mag.Rounds.Add(ammoId);
                return true;
            }
            return false;
        }

        /// <summary>Recomputed stats of an assembled weapon (modules + durability).</summary>
        public WeaponStats BuildStats()
        {
            WeaponItemDefinition weapon = Def as WeaponItemDefinition;
            if (weapon == null) return null;

            float recoil = 1f;
            float accuracy = 1f;
            float velocity = 1f;
            int ergo = weapon.Ergonomics;

            for (int i = 0; i < Modules.Count; i++)
            {
                WeaponModuleItemDefinition mod = ItemDatabase.Find(Modules[i]) as WeaponModuleItemDefinition;
                if (mod == null) continue;
                recoil *= mod.RecoilModifier;
                accuracy *= mod.AccuracyModifier;
                velocity *= mod.VelocityModifier;
                ergo += mod.ErgonomicsModifier;
            }

            float wear = 1f - Durability01;
            return new WeaponStats
            {
                VerticalRecoil = weapon.VerticalRecoil * recoil * (1f + wear * 1.5f),
                HorizontalRecoil = weapon.HorizontalRecoil * recoil * (1f + wear * 1.5f),
                SpreadDegrees = weapon.BaseSpreadDegrees * accuracy * (1f + wear),
                MuzzleVelocity = weapon.MuzzleVelocity * velocity * (1f - wear * 0.15f),
                Ergonomics = Mathf.Max(1, ergo),
                Durability01 = Durability01
            };
        }
    }

    [Serializable]
    public class WeaponStats
    {
        public float VerticalRecoil;
        public float HorizontalRecoil;
        public float SpreadDegrees;
        public float MuzzleVelocity;
        public int Ergonomics;
        public float Durability01 = 1f;
    }
}
