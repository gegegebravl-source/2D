using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;

namespace EXFIL.Characters
{
    [Serializable]
    public class BodyPartState
    {
        public BodyPart Part;
        public float MaxHp;
        public float Hp;
        public bool Destroyed;

        public float Ratio { get { return MaxHp > 0f ? Mathf.Clamp01(Hp / MaxHp) : 0f; } }
    }

    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public float Penetration;
        public float ArmorDamagePercent;
        public float FragmentationChance;
        public uint AttackerId;
        public string AttackerName;
        public string AmmoId;
        public Vector3 Direction;
        public bool CanCauseFracture;
    }

    /// <summary>
    /// Tarkov-like health model: 7 separate body parts, blacked limbs spread damage,
    /// bleeds / fractures / pain / exhaustion, energy and hydration drains.
    /// </summary>
    public class HealthController : MonoBehaviour
    {
        [Header("Base pools")]
        public float MaxEnergy = 110f;
        public float MaxHydration = 95f;

        [Header("Drain (per raid minute)")]
        public float EnergyDrainPerMinute = 1.15f;
        public float HydrationDrainPerMinute = 1.6f;

        [Header("Bleeding")]
        public float LightBleedDamagePerSecond = 0.5f;
        public float HeavyBleedDamagePerSecond = 1.4f;

        [Header("Runtime (read only)")]
        [SerializeField] private List<BodyPartState> parts = new List<BodyPartState>();
        [SerializeField] private List<ActiveEffect> effects = new List<ActiveEffect>();

        public float Energy = 110f;
        public float Hydration = 95f;
        public bool IsAlive { get; private set; } = true;
        public uint NetId;
        public string CharacterName = "Operator";

        [Serializable]
        public class ActiveEffect
        {
            public HealthEffectType Type;
            public float Remaining;
            public float Strength;
            public BodyPart Part = BodyPart.Thorax;
        }

        public event Action<BodyPart, float, DamageInfo> OnDamaged;
        public event Action OnDied;

        private static readonly BodyPart[] AllParts =
        {
            BodyPart.Head, BodyPart.Thorax, BodyPart.Stomach,
            BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg
        };

        private static readonly float[] BaseHp = { 35f, 85f, 70f, 60f, 60f, 65f, 65f };

        private void Awake()
        {
            Reset();
        }

        public void Reset()
        {
            parts.Clear();
            for (int i = 0; i < AllParts.Length; i++)
            {
                parts.Add(new BodyPartState
                {
                    Part = AllParts[i],
                    MaxHp = BaseHp[i],
                    Hp = BaseHp[i],
                    Destroyed = false
                });
            }
            Energy = MaxEnergy;
            Hydration = MaxHydration;
            effects.Clear();
            IsAlive = true;
        }

        public BodyPartState GetPart(BodyPart part)
        {
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].Part == part) return parts[i];
            return null;
        }

        public float TotalHp
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < parts.Count; i++) total += parts[i].Hp;
                return total;
            }
        }

        public float MaxTotalHp
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < parts.Count; i++) total += parts[i].MaxHp;
                return total;
            }
        }

        // ------------------------------------------------------------------ damage
        /// <summary>Applies already-armor-resolved damage. Returns the damage actually taken.</summary>
        public float ApplyDamage(BodyPart part, DamageInfo info)
        {
            if (!IsAlive) return 0f;

            BodyPartState state = GetPart(part);
            if (state == null) return 0f;

            float damage = info.Amount;

            // Damage taken on an already blacked limb is spread across the rest of the body.
            if (state.Destroyed)
            {
                damage *= 0.7f;
                SpreadDamage(damage, info, part);
                return damage;
            }

            state.Hp -= damage;
            if (state.Hp <= 0f)
            {
                state.Hp = 0f;
                state.Destroyed = true;
                OnLimbDestroyed(part);
            }

            // secondary effects
            if (info.Type == DamageType.Bullet || info.Type == DamageType.Fragmentation)
            {
                if (UnityEngine.Random.value < 0.08f && info.CanCauseFracture)
                    AddEffect(HealthEffectType.Fracture, 0f, 1f, part);
                if (UnityEngine.Random.value < 0.35f)
                    AddEffect(HealthEffectType.Pain, 12f, 1f, part);
                if (part == BodyPart.Head && UnityEngine.Random.value < 0.25f)
                    AddEffect(HealthEffectType.Concussion, 20f, 1f, part);
            }

            OnDamaged?.Invoke(part, damage, info);
            GameEvents.Raise(new DamageAppliedEvent
            {
                VictimId = NetId,
                AttackerId = info.AttackerId,
                Part = part,
                Amount = damage,
                Type = info.Type,
                Lethal = !IsAlive
            });

            CheckDeath(info);
            return damage;
        }

        private void SpreadDamage(float damage, DamageInfo info, BodyPart skip)
        {
            float aliveCount = 0f;
            for (int i = 0; i < parts.Count; i++)
                if (!parts[i].Destroyed && parts[i].Part != skip) aliveCount += 1f;
            if (aliveCount <= 0f)
            {
                Kill(info);
                return;
            }

            float perPart = damage / aliveCount;
            for (int i = 0; i < parts.Count; i++)
            {
                BodyPartState state = parts[i];
                if (state.Destroyed || state.Part == skip) continue;
                state.Hp -= perPart;
                if (state.Hp <= 0f)
                {
                    state.Hp = 0f;
                    state.Destroyed = true;
                    OnLimbDestroyed(state.Part);
                }
            }
            CheckDeath(info);
        }

        private void OnLimbDestroyed(BodyPart part)
        {
            if (part == BodyPart.Head || part == BodyPart.Thorax)
            {
                Kill(new DamageInfo { Type = DamageType.Bullet, Amount = 999f });
                return;
            }
            if (part == BodyPart.Stomach)
                AddEffect(HealthEffectType.Intoxication, 60f, 1f, part);
            if (part == BodyPart.LeftLeg || part == BodyPart.RightLeg)
                AddEffect(HealthEffectType.Fracture, 0f, 1f, part);
        }

        public void Kill(DamageInfo info)
        {
            if (!IsAlive) return;
            IsAlive = false;
            for (int i = 0; i < parts.Count; i++) { parts[i].Hp = 0f; parts[i].Destroyed = true; }

            GameEvents.Raise(new CharacterDiedEvent
            {
                VictimId = NetId,
                KillerId = info.AttackerId,
                VictimName = CharacterName,
                KillerName = info.AttackerName
            });
            OnDied?.Invoke();
        }

        private void CheckDeath(DamageInfo info)
        {
            if (!IsAlive) return;
            BodyPartState head = GetPart(BodyPart.Head);
            BodyPartState thorax = GetPart(BodyPart.Thorax);
            if ((head != null && head.Destroyed) || (thorax != null && thorax.Destroyed))
                Kill(info);
        }

        // ------------------------------------------------------------------ effects
        public void AddEffect(HealthEffectType type, float duration, float strength = 1f, BodyPart part = BodyPart.Thorax)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Type == type && effects[i].Part == part)
                {
                    effects[i].Remaining = Mathf.Max(effects[i].Remaining, duration);
                    effects[i].Strength = Mathf.Max(effects[i].Strength, strength);
                    return;
                }
            }
            effects.Add(new ActiveEffect { Type = type, Remaining = duration, Strength = strength, Part = part });
        }

        public bool HasEffect(HealthEffectType type)
        {
            for (int i = 0; i < effects.Count; i++)
                if (effects[i].Type == type) return true;
            return false;
        }

        public void RemoveEffect(HealthEffectType type)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
                if (effects[i].Type == type) effects.RemoveAt(i);
        }

        public List<ActiveEffect> Effects { get { return effects; } }

        // ------------------------------------------------------------------ healing
        public float Heal(BodyPart part, float amount)
        {
            BodyPartState state = GetPart(part);
            if (state == null) return 0f;
            float before = state.Hp;
            state.Hp = Mathf.Min(state.MaxHp, state.Hp + amount);
            if (state.Hp > 0f) state.Destroyed = false;
            return state.Hp - before;
        }

        public float HealAll(float amountPerPart)
        {
            float healed = 0f;
            for (int i = 0; i < parts.Count; i++) healed += Heal(parts[i].Part, amountPerPart);
            return healed;
        }

        /// <summary>Field surgery: restores a blacked limb at reduced max hp.</summary>
        public bool RestorePart(BodyPart part)
        {
            BodyPartState state = GetPart(part);
            if (state == null || !state.Destroyed) return false;
            state.Destroyed = false;
            state.Hp = Mathf.Max(1f, state.MaxHp * 0.25f);
            return true;
        }

        public void Consume(float energy, float hydration)
        {
            Energy = Mathf.Min(MaxEnergy, Energy + energy);
            Hydration = Mathf.Min(MaxHydration, Hydration + hydration);
        }

        // ------------------------------------------------------------------ tick
        private float _tickAccumulator;

        private void Update()
        {
            if (!IsAlive) return;
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!IsAlive) return;

            float minutes = deltaTime / 60f;
            Energy = Mathf.Max(0f, Energy - EnergyDrainPerMinute * minutes);
            Hydration = Mathf.Max(0f, Hydration - HydrationDrainPerMinute * minutes);

            if (Energy <= 0f)
                ApplyDamage(BodyPart.Thorax, new DamageInfo { Type = DamageType.Exhaustion, Amount = 1.1f * deltaTime });
            if (Hydration <= 0f)
                ApplyDamage(BodyPart.Thorax, new DamageInfo { Type = DamageType.Dehydration, Amount = 0.9f * deltaTime });

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                ActiveEffect effect = effects[i];
                switch (effect.Type)
                {
                    case HealthEffectType.LightBleed:
                        ApplyDamage(BodyPart.Thorax, new DamageInfo { Type = DamageType.Bleeding, Amount = LightBleedDamagePerSecond * deltaTime });
                        break;
                    case HealthEffectType.HeavyBleed:
                        ApplyDamage(BodyPart.Thorax, new DamageInfo { Type = DamageType.Bleeding, Amount = HeavyBleedDamagePerSecond * deltaTime });
                        break;
                    case HealthEffectType.Tremor:
                    case HealthEffectType.Regeneration:
                        HealAll(0.6f * deltaTime);
                        break;
                }

                if (effect.Remaining > 0f)
                {
                    effect.Remaining -= deltaTime;
                    if (effect.Remaining <= 0f) effects.RemoveAt(i);
                }
            }

            _tickAccumulator += deltaTime;
            if (_tickAccumulator >= 5f)
            {
                _tickAccumulator = 0f;
                if (Energy > 25f && Hydration > 25f && !HasEffect(HealthEffectType.HeavyBleed))
                    HealAll(0.35f);
            }
        }

        public void ApplyFallDamage(float impactSpeed)
        {
            float damage = (impactSpeed - 6f) * 7f;
            if (damage <= 0f) return;
            BodyPart part = UnityEngine.Random.value < 0.85f ? BodyPart.LeftLeg : BodyPart.RightLeg;
            ApplyDamage(part, new DamageInfo
            {
                Type = DamageType.Fall,
                Amount = damage,
                CanCauseFracture = true
            });
            if (damage > 18f) AddEffect(HealthEffectType.Fracture, 0f, 1f, part);
        }

        // ------------------------------------------------------------- modifiers
        public float GetMovementMultiplier()
        {
            float multiplier = 1f;
            BodyPartState leftLeg = GetPart(BodyPart.LeftLeg);
            BodyPartState rightLeg = GetPart(BodyPart.RightLeg);
            if (leftLeg != null && leftLeg.Destroyed) multiplier *= 0.55f;
            if (rightLeg != null && rightLeg.Destroyed) multiplier *= 0.55f;
            if (HasEffect(HealthEffectType.Fracture)) multiplier *= 0.7f;
            if (HasEffect(HealthEffectType.Pain)) multiplier *= 0.88f;
            if (Energy <= 0f) multiplier *= 0.6f;
            return Mathf.Clamp(multiplier, 0.25f, 1f);
        }

        public bool HasBrokenLegs()
        {
            BodyPartState leftLeg = GetPart(BodyPart.LeftLeg);
            BodyPartState rightLeg = GetPart(BodyPart.RightLeg);
            return (leftLeg != null && leftLeg.Destroyed) || (rightLeg != null && rightLeg.Destroyed);
        }

        public float StaminaCapacity01()
        {
            float capacity = 1f;
            if (Energy <= 0f) capacity *= 0.25f;
            if (HasEffect(HealthEffectType.Fracture)) capacity *= 0.6f;
            return Mathf.Clamp01(capacity);
        }

        public bool HasBlackenedPart()
        {
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].Destroyed) return true;
            return false;
        }
    }
}
