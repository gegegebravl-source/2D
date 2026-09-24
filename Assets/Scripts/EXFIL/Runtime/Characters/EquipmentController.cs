using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;
using EXFIL.Items;
using EXFIL.Player;
using EXFIL.Combat;

namespace EXFIL.Characters
{
    /// <summary>
    /// Turns the inventory equipment slots into real 3D gear on the body, armor coverage
    /// and movement penalties. Also rebuilds weapon handlers.
    /// </summary>
    public class EquipmentController : MonoBehaviour
    {
        [Header("Body")]
        public CharacterBody3D Body;
        public ArmorController ArmorCoverage;   // EXFIL.Combat
        public FpsController Motor;

        [Header("Weapons")]
        public Combat.WeaponHandler PrimaryHandler;
        public Combat.WeaponHandler SecondaryHandler;
        public Combat.WeaponHandler HolsterHandler;

        private Inventory _inventory;
        private readonly List<GameObject> _spawnedGear = new List<GameObject>();

        public Inventory Inventory { get { return _inventory; } }
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float TurnSpeedMultiplier { get; private set; } = 1f;

        public void Bind(Inventory inventory)
        {
            _inventory = inventory;
            Rebuild();
        }

        public void Rebuild()
        {
            if (_inventory == null) return;

            ClearGear();
            if (ArmorCoverage != null) { ArmorCoverage.Bind(_inventory); ArmorCoverage.Rebuild(); }

            List<KeyValuePair<EquipmentSlot, ItemInstance>> gear = _inventory.AllEquipment();
            for (int i = 0; i < gear.Count; i++)
                Dress(gear[i].Key, gear[i].Value);

            // weapons
            EquipWeapon(EquipmentSlot.PrimaryWeapon, PrimaryHandler);
            EquipWeapon(EquipmentSlot.SecondaryWeapon, SecondaryHandler);
            EquipWeapon(EquipmentSlot.Holster, HolsterHandler);

            float movePenalty = ArmorCoverage != null ? ArmorCoverage.MoveSpeedPenalty : 0f;
            float turnPenalty = ArmorCoverage != null ? ArmorCoverage.TurnSpeedPenalty : 0f;

            // heavy rigs also slow you down when stuffed full
            float loadFactor = 0f;
            ContainerInstance backpack = _inventory.GetContainer("backpack");
            if (backpack != null)
                loadFactor = Mathf.Clamp01(backpack.Grid.TotalWeight() / 30f) * 0.12f;

            MoveSpeedMultiplier = Mathf.Clamp(1f - movePenalty - loadFactor, 0.45f, 1f);
            TurnSpeedMultiplier = Mathf.Clamp(1f - turnPenalty, 0.5f, 1f);

            if (Motor != null) Motor.SpeedMultiplierFromLoadout = MoveSpeedMultiplier;
        }

        private void Dress(EquipmentSlot slot, ItemInstance instance)
        {
            if (instance == null || Body == null) return;

            ArmorItemDefinition armor = instance.Def as ArmorItemDefinition;
            ClothingItemDefinition clothing = instance.Def as ClothingItemDefinition;

            EquipmentVisual visual = null;
            if (armor != null) visual = armor.Visual;
            else if (clothing != null) visual = clothing.Visual;
            if (visual == null) return;

            AttachPoint point = SlotToAttachPoint(slot, visual);
            GameObject attached;
            if (visual.Prefab3D != null)
            {
                // real imported model wins
                attached = Body.AttachGear(point, visual.Prefab3D, visual.Offset3D, visual.Rotation3D, visual.Scale3D);
            }
            else
            {
                // gear built to measure around the body it stacks on
                Transform anchor = Body.AnchorFor(point);
                attached = GearFactory.Build(slot, instance.Def, visual, anchor);
            }
            if (attached != null) _spawnedGear.Add(attached);
        }

        private static AttachPoint SlotToAttachPoint(EquipmentSlot slot, EquipmentVisual visual)
        {
            if (visual.AttachPoint3D != AttachPoint.Chest) return visual.AttachPoint3D;
            switch (slot)
            {
                case EquipmentSlot.Helmet:
                case EquipmentSlot.Headwear: return AttachPoint.Head;
                case EquipmentSlot.FaceCover: return AttachPoint.Face;
                case EquipmentSlot.Eyewear: return AttachPoint.Face;
                case EquipmentSlot.Backpack: return AttachPoint.Back;
                case EquipmentSlot.Boots: return AttachPoint.Hips;
                case EquipmentSlot.Gloves: return AttachPoint.RightHand;
                default: return AttachPoint.Chest;
            }
        }

        private void EquipWeapon(EquipmentSlot slot, Combat.WeaponHandler handler)
        {
            if (handler == null) return;
            ItemInstance weapon = _inventory.GetEquipped(slot);
            handler.Equip(weapon);
        }

        private void ClearGear()
        {
            for (int i = 0; i < _spawnedGear.Count; i++)
                if (_spawnedGear[i] != null) Destroy(_spawnedGear[i]);
            _spawnedGear.Clear();
        }
    }

}
