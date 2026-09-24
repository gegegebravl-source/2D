using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    [CreateAssetMenu(menuName = "EXFIL/Items/Ammo", fileName = "Ammo_")]
    public class AmmoItemDefinition : ItemDefinition
    {
        [Header("Ballistics")]
        public Core.AmmoCaliber Caliber = Core.AmmoCaliber._545x39;
        public float Damage = 45f;
        public float Penetration = 25f;      // 0..70, checked against armor class
        public float ArmorDamagePercent = 0.42f;
        public float FragmentationChance = 0.15f;
        public float Velocity = 880f;
        public float RecoilModifier = 1f;
        public float AccuracyModifier = 1f;
        public bool Tracer;
        public bool Subsonic;
        public float LightBleedChance = 0.18f;
        public float HeavyBleedChance = 0.06f;

        private void OnEnable()
        {
            Type = Core.ItemType.Ammo;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Damage: " + Damage.ToString("0"));
            lines.Add("Penetration: " + Penetration.ToString("0"));
            lines.Add("Armor damage: " + (ArmorDamagePercent * 100f).ToString("0") + "%");
            lines.Add("Frag chance: " + (FragmentationChance * 100f).ToString("0") + "%");
        }
    }

    [CreateAssetMenu(menuName = "EXFIL/Items/Magazine", fileName = "Magazine_")]
    public class MagazineItemDefinition : ItemDefinition
    {
        public Core.AmmoCaliber Caliber = Core.AmmoCaliber._545x39;
        public int Capacity = 30;
        public float LoadSpeedModifier = 1f;
        public float CheckTime = 1.6f;
        public List<string> CompatibleWeapons = new List<string>(); // empty = universal for caliber

        private void OnEnable()
        {
            Type = Core.ItemType.Magazine;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Capacity: " + Capacity);
            lines.Add("Caliber: " + CaliberName.Pretty(Caliber));
        }
    }

    [CreateAssetMenu(menuName = "EXFIL/Items/Weapon module", fileName = "Module_")]
    public class WeaponModuleItemDefinition : ItemDefinition
    {
        public Core.AttachmentSlot Slot = Core.AttachmentSlot.Optic;
        public int ErgonomicsModifier = 0;
        public float RecoilModifier = 1f;
        public float AccuracyModifier = 1f;
        public float VelocityModifier = 1f;
        public float Zoom = 1f;
        public bool SilencesShots;
        public List<string> CompatibleWeapons = new List<string>();

        private void OnEnable()
        {
            Type = Core.ItemType.WeaponModule;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Slot: " + Slot);
            if (ErgonomicsModifier != 0) lines.Add("Ergonomics: " + (ErgonomicsModifier > 0 ? "+" : "") + ErgonomicsModifier);
            if (!Mathf.Approximately(RecoilModifier, 1f)) lines.Add("Recoil: x" + RecoilModifier.ToString("0.00"));
            if (Zoom > 1f) lines.Add("Zoom: x" + Zoom.ToString("0.0"));
        }
    }
}
