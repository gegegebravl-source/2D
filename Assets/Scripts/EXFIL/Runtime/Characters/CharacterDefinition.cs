using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Characters
{
    public enum Faction { EastContract, WestContract }

    [Serializable]
    public class StartingItem
    {
        public string ItemId;
        public int Stack = 1;
        public Core.EquipmentSlot Slot = Core.EquipmentSlot.None;
        public string ContainerId = "";   // "pockets", "backpack", "secure", "stash"
    }

    [Serializable]
    public class CharacterPerk
    {
        public Core.SkillType Skill;
        public float StartingLevel;
        public string Description;
    }

    /// <summary>One of the six playable operators. Body is a prefab, the base body is the
    /// underwear/thermal layer so every piece of gear visibly stacks on top of it.</summary>
    [CreateAssetMenu(menuName = "EXFIL/Character/Operator", fileName = "Operator_")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public string Callsign;
        public Faction Faction = Faction.EastContract;
        [TextArea(3, 8)] public string Biography;

        [Header("Visuals")]
        public Sprite Portrait;
        public GameObject BodyPrefab;      // full 3D body, base thermal layer
        public Color FactionColor = new Color(0.55f, 0.62f, 0.45f);

        [Header("Voice")]
        public AudioClip[] PainSounds;
        public AudioClip[] DeathSounds;

        [Header("Progression")]
        public List<CharacterPerk> Perks = new List<CharacterPerk>();
        public List<StartingItem> StartingLoadout = new List<StartingItem>();

        [Header("Stats")]
        public float HealthBonus = 0f;
        public float StaminaBonus = 0f;
        public float CarryBonus = 0f;
    }
}
