using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Items
{
    [CreateAssetMenu(menuName = "EXFIL/Items/Weapon", fileName = "Weapon_")]
    public class WeaponItemDefinition : ItemDefinition
    {
        [Header("Weapon")]
        public Core.WeaponClass WeaponClass = Core.WeaponClass.AssaultRifle;
        public Core.AmmoCaliber Caliber = Core.AmmoCaliber._545x39;
        [EnumFlags] public Core.FireMode FireModes = Core.FireMode.Single | Core.FireMode.FullAuto;
        public Core.FireMode DefaultFireMode = Core.FireMode.FullAuto;
        public float RoundsPerMinute = 650f;
        public float MuzzleVelocity = 900f;
        public float BurstCount = 3f;

        [Header("Handling")]
        public int Ergonomics = 45;
        public float VerticalRecoil = 120f;
        public float HorizontalRecoil = 60f;
        public float BaseSpreadDegrees = 1.4f;
        public float SpreadPerShot = 0.55f;
        public float SpreadRecoverSpeed = 2.2f;
        public float SightingRange = 100f;

        [Header("Reliability")]
        public float MaxDurability = 100f;
        public float WearPerShot = 0.02f;
        public float FailureChanceAtZero = 0.55f;
        public float OverheatPerShot = 0.6f;
        public float OverheatCooldown = 4f;

        [Header("Timing")]
        public float DrawTime = 0.65f;
        public float ReloadTactical = 2.4f;
        public float ReloadEmpty = 3.1f;
        public float ChamberTime = 0.12f;
        public float AimTime = 0.32f;

        [Header("Slots")]
        public List<Core.AttachmentSlot> AllowedModules = new List<Core.AttachmentSlot>();

        [Header("Defaults")]
        public MagazineItemDefinition DefaultMagazine;
        public AmmoItemDefinition DefaultAmmo;
        public List<string> DefaultModules = new List<string>();

        [Header("Visuals")]
        public Sprite IconSprite;                 // 2D icon used by the inventory UI (already in ItemDefinition.Icon)
        [Tooltip("First person view model. Empty = primitive stand-in is generated at runtime.")]
        public GameObject ViewModelPrefab;
        public Vector3 ViewModelOffset = new Vector3(0.18f, -0.16f, 0.32f);
        public Vector3 ViewModelAimOffset = new Vector3(0f, -0.085f, 0.24f);
        public Vector3 ViewModelEuler = new Vector3(0f, 0f, 0f);
        [Tooltip("World model held by bots / dropped on death / shown on corpses.")]
        public GameObject WorldModelPrefab;
        [Tooltip("Local offset of the world model inside the right hand anchor.")]
        public Vector3 WorldModelOffset = new Vector3(0f, 0f, 0.06f);
        public Vector3 WorldModelEuler = new Vector3(0f, 90f, 0f);
        public float WorldModelScale = 1f;
        [Tooltip("True for rifles/carbines: the rig switches to the two handed aim pose.")]
        public bool TwoHanded = true;
        public Sprite WeaponSprite;
        public Vector2 MuzzleOffset = new Vector2(0.55f, 0.02f);
        public Vector2 GripOffset = new Vector2(0.15f, -0.05f);
        public Vector2 ShellEjectOffset = new Vector2(0.2f, 0.05f);
        public Vector2 WeaponScale = Vector2.one;
        public AudioClip FireSound;
        public AudioClip DryFireSound;
        public AudioClip ReloadSound;

        private void OnEnable()
        {
            Type = Core.ItemType.Weapon;
            if (AllowedModules.Count == 0)
                AllowedModules.Add(Core.AttachmentSlot.Optic);
        }

        public float SecondsPerShot
        {
            get { return 60f / Mathf.Max(1f, RoundsPerMinute); }
        }

        public bool HasFireMode(Core.FireMode mode)
        {
            return (FireModes & mode) == mode;
        }

        public override void CollectTooltipLines(List<string> lines)
        {
            base.CollectTooltipLines(lines);
            lines.Add("Caliber: " + CaliberName.Pretty(Caliber));
            lines.Add("RPM: " + RoundsPerMinute.ToString("0"));
            lines.Add("Ergonomics: " + Ergonomics);
            lines.Add("Velocity: " + MuzzleVelocity.ToString("0") + " m/s");
        }

        public override bool CanEquip { get { return true; } }
        public override Core.EquipmentSlot EquipmentSlot { get { return Core.EquipmentSlot.PrimaryWeapon; } }
    }

    /// <summary>EnumFlags drawer marker (custom drawer lives in Editor).</summary>
    public class EnumFlagsAttribute : PropertyAttribute { }

    public static class CaliberName
    {
        public static string Pretty(Core.AmmoCaliber caliber)
        {
            switch (caliber)
            {
                case Core.AmmoCaliber._9x18PM: return "9x18 PM";
                case Core.AmmoCaliber._9x19Para: return "9x19 Parabellum";
                case Core.AmmoCaliber._762x25TT: return "7.62x25 TT";
                case Core.AmmoCaliber._45ACP: return ".45 ACP";
                case Core.AmmoCaliber._545x39: return "5.45x39";
                case Core.AmmoCaliber._556x45NATO: return "5.56x45 NATO";
                case Core.AmmoCaliber._762x39: return "7.62x39";
                case Core.AmmoCaliber._762x51NATO: return "7.62x51 NATO";
                case Core.AmmoCaliber._762x54R: return "7.62x54R";
                case Core.AmmoCaliber._9x39: return "9x39";
                case Core.AmmoCaliber._366TKM: return ".366 TKM";
                case Core.AmmoCaliber._12gauge: return "12/70";
                case Core.AmmoCaliber._20gauge: return "20/70";
                default: return caliber.ToString();
            }
        }
    }
}
