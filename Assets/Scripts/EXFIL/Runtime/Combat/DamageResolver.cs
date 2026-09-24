using UnityEngine;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.Combat
{
    /// <summary>Turns a projectile hit into final damage: armor roll, absorption, fragmentation, effects.</summary>
    public static class DamageResolver
    {
        public static void Resolve(Hitbox hitbox, Projectile.SpawnData data, float rawDamage, Vector3 point, Vector3 normal)
        {
            if (hitbox == null || hitbox.Owner == null) return;
            HealthController health = hitbox.Owner;
            if (!health.IsAlive) return;

            AmmoItemDefinition ammo = data.Ammo;
            float damage = rawDamage * hitbox.DamageMultiplier;

            DamageInfo info = new DamageInfo
            {
                Amount = damage,
                Type = DamageType.Bullet,
                Penetration = ammo != null ? ammo.Penetration : 10f,
                ArmorDamagePercent = ammo != null ? ammo.ArmorDamagePercent : 0.3f,
                FragmentationChance = ammo != null ? ammo.FragmentationChance : 0f,
                AttackerId = data.OwnerId,
                AttackerName = data.OwnerName,
                AmmoId = ammo != null ? ammo.Id : string.Empty,
                Direction = data.Direction,
                CanCauseFracture = true
            };

            ArmorCover cover = hitbox.Armor != null ? hitbox.Armor.GetCover(hitbox.Part) : null;
            if (cover != null && cover.Definition != null && cover.Instance != null && cover.Instance.CurrentDurability > 0f)
            {
                float durability01 = cover.Instance.Durability01;
                float chance = Ballistics.PenetrationChance(info.Penetration, cover.Definition.ArmorClass, durability01);
                bool penetrated = Random.value < chance;

                float loss = Ballistics.ArmorDurabilityLoss(
                    damage, info.ArmorDamagePercent, cover.Definition.Material, cover.Definition.ArmorClass);
                if (hitbox.Armor != null) hitbox.Armor.DamageArmor(cover, loss);

                if (!penetrated)
                {
                    if (Random.value < cover.Definition.RicochetChance)
                        return; // ricochet, no damage

                    info.Amount *= Ballistics.ArmorAbsorption(cover.Definition.ArmorClass, durability01);
                    info.Type = DamageType.Bullet;
                }
            }

            // fragmentation: extra damage when the round breaks apart
            if (info.FragmentationChance > 0f && Random.value < info.FragmentationChance)
            {
                info.Amount *= 1.5f;
                if (Random.value < 0.5f)
                    health.AddEffect(HealthEffectType.HeavyBleed, 0f, 1f, hitbox.Part);
            }
            else if (ammo != null)
            {
                if (Random.value < ammo.LightBleedChance) health.AddEffect(HealthEffectType.LightBleed, 0f, 1f, hitbox.Part);
                else if (Random.value < ammo.HeavyBleedChance) health.AddEffect(HealthEffectType.HeavyBleed, 0f, 1f, hitbox.Part);
            }

            health.ApplyDamage(hitbox.Part, info);

            // skill xp for the shooter is granted by the raid / bot controller hooks
            if (hitbox.Body != null)
            {
                AudioSource source = hitbox.Body.GetComponent<AudioSource>();
                if (source != null)
                {
                    CharacterDefinition def = hitbox.Body.Definition;
                    if (def != null && def.PainSounds != null && def.PainSounds.Length > 0)
                        source.PlayOneShot(def.PainSounds[Random.Range(0, def.PainSounds.Length)]);
                }
            }
        }
    }
}
