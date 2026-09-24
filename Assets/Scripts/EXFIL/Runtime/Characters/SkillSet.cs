using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;

namespace EXFIL.Characters
{
    [Serializable]
    public class SkillProgress
    {
        public int Skill;           // SkillType as int
        public float Xp;
        public float Level;         // 0..50 (elite at 50)
    }

    /// <summary>
    /// All character skills. XP is awarded by gameplay actions (see SkillSystem hooks) and
    /// every skill feeds concrete gameplay multipliers, exactly like the original game.
    /// </summary>
    public class SkillSet : MonoBehaviour
    {
        public const float MaxLevel = 50f;

        [SerializeField] private List<SkillProgress> skills = new List<SkillProgress>();

        public event Action<SkillType, float> OnXpGained;

        public IList<SkillProgress> All { get { return skills; } }

        public void Reset()
        {
            skills.Clear();
        }

        public float GetLevel(SkillType type)
        {
            return GetProgress(type).Level;
        }

        public float GetXp(SkillType type)
        {
            return GetProgress(type).Xp;
        }

        private SkillProgress GetProgress(SkillType type)
        {
            int key = (int)type;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].Skill == key) return skills[i];

            SkillProgress created = new SkillProgress { Skill = key, Xp = 0f, Level = 0f };
            skills.Add(created);
            return created;
        }

        public void AddXp(SkillType type, float amount)
        {
            if (amount <= 0f) return;
            SkillProgress progress = GetProgress(type);
            progress.Xp += amount;

            float needed = XpForNextLevel(progress.Level);
            while (progress.Xp >= needed && progress.Level < MaxLevel)
            {
                progress.Xp -= needed;
                progress.Level = Mathf.Min(MaxLevel, progress.Level + 1f);
                needed = XpForNextLevel(progress.Level);
            }
            OnXpGained?.Invoke(type, amount);
        }

        /// <summary>XP curve: easier early, steep later (elite level ~ 50).</summary>
        public static float XpForNextLevel(float level)
        {
            return 10f + Mathf.Pow(level, 1.75f) * 2.4f;
        }

        public bool IsElite(SkillType type)
        {
            return GetLevel(type) >= MaxLevel;
        }

        // ------------------------------------------------------------- multipliers
        public float GetSpeedMultiplier()
        {
            return 1f + GetLevel(SkillType.Endurance) * 0.0022f;
        }

        public float GetCarryBonus()
        {
            return GetLevel(SkillType.Strength) * 0.55f;
        }

        public float GetEnduranceRegenMultiplier()
        {
            return 1f + GetLevel(SkillType.Endurance) * 0.008f;
        }

        public float GetRecoilMultiplier(WeaponClass weaponClass)
        {
            SkillType skill = WeaponClassToSkill(weaponClass);
            float mastery = GetLevel(skill) * 0.006f + GetLevel(SkillType.RecoilControl) * 0.004f;
            return Mathf.Clamp(1f - mastery, 0.45f, 1f);
        }

        public float GetReloadSpeedMultiplier(WeaponClass weaponClass)
        {
            float mastery = GetLevel(WeaponClassToSkill(weaponClass)) * 0.005f;
            return Mathf.Clamp(1f + mastery, 1f, 1.65f);
        }

        public float GetSearchSpeedMultiplier()
        {
            return Mathf.Clamp(1f + GetLevel(SkillType.Search) * 0.012f, 1f, 2.2f);
        }

        public float GetCraftSpeedMultiplier()
        {
            return Mathf.Clamp(1f + GetLevel(SkillType.Crafting) * 0.01f, 1f, 1.8f);
        }

        public float GetMetabolismMultiplier()
        {
            return Mathf.Clamp(1f - GetLevel(SkillType.Metabolism) * 0.006f, 0.5f, 1f);
        }

        public float GetNoiseMultiplier()
        {
            return Mathf.Clamp(1f - GetLevel(SkillType.CovertMovement) * 0.008f, 0.5f, 1f);
        }

        public float GetHealthPoolBonus()
        {
            return GetLevel(SkillType.Vitality) * 0.7f;
        }

        public float GetSpottingBonus()
        {
            return GetLevel(SkillType.Perception) * 0.35f;
        }

        public static SkillType WeaponClassToSkill(WeaponClass weaponClass)
        {
            switch (weaponClass)
            {
                case WeaponClass.AssaultRifle:
                case WeaponClass.AssaultCarbine: return SkillType.AssaultRifle;
                case WeaponClass.SMG: return SkillType.SMG;
                case WeaponClass.Shotgun: return SkillType.Shotgun;
                case WeaponClass.Pistol: return SkillType.Pistol;
                case WeaponClass.SniperRifle:
                case WeaponClass.MarksmanRifle: return SkillType.DMR;
                default: return SkillType.AssaultRifle;
            }
        }
    }
}
