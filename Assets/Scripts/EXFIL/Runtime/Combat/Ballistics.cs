using System.Collections.Generic;
using UnityEngine;
using EXFIL.Items;

namespace EXFIL.Combat
{
    /// <summary>
    /// Penetration model: probability to pierce grows with (penetration - armor class),
    /// armor absorbs damage depending on material, durability drops by ammo armor damage.
    /// </summary>
    public static class Ballistics
    {
        public const float Gravity = 9.81f;

        private static readonly Dictionary<Core.ArmorMaterial, float> MaterialDurabilityFactor =
            new Dictionary<Core.ArmorMaterial, float>
            {
                { Core.ArmorMaterial.Aramid, 0.25f },
                { Core.ArmorMaterial.UHMWPE, 0.45f },
                { Core.ArmorMaterial.Steel, 0.55f },
                { Core.ArmorMaterial.Titanium, 0.65f },
                { Core.ArmorMaterial.Ceramic, 0.75f },
                { Core.ArmorMaterial.Glass, 0.8f },
                { Core.ArmorMaterial.Aluminium, 0.5f },
                { Core.ArmorMaterial.Combined, 0.6f },
                { Core.ArmorMaterial.None, 1f }
            };

        /// <summary>Chance 0..1 that the round penetrates the given armor.</summary>
        public static float PenetrationChance(float penetration, Core.ArmorClass armorClass, float armorDurability01)
        {
            float classValue = (int)armorClass * 10f;
            float effective = classValue * (0.35f + 0.65f * armorDurability01);
            float delta = penetration - effective;

            // logistic curve
            float chance = 1f / (1f + Mathf.Exp(-delta / 6f));
            return Mathf.Clamp(chance, 0.02f, 0.98f);
        }

        /// <summary>Damage multiplier applied when the round is stopped by the armor.</summary>
        public static float ArmorAbsorption(Core.ArmorClass armorClass, float armorDurability01)
        {
            float absorbed = (int)armorClass * 0.09f * (0.4f + 0.6f * armorDurability01);
            return Mathf.Clamp01(1f - absorbed);
        }

        /// <summary>How much durability the armor loses (in points).</summary>
        public static float ArmorDurabilityLoss(float ammoDamage, float armorDamagePercent, Core.ArmorMaterial material, Core.ArmorClass armorClass)
        {
            float factor;
            if (!MaterialDurabilityFactor.TryGetValue(material, out factor)) factor = 0.5f;
            float classValue = Mathf.Max(1f, (int)armorClass);
            return ammoDamage * armorDamagePercent * factor * (30f / classValue) * 0.06f;
        }

        public static Vector3 BallisticDirection(Vector3 direction, float distance, float velocity)
        {
            float time = distance / Mathf.Max(1f, velocity);
            return (direction + Vector3.down * (0.5f * Gravity * time * time) / Mathf.Max(0.01f, distance)).normalized;
        }

        // ---------------------------------------------------------------- spawning
        private static Projectile _prefabOverride;

        public static void RegisterProjectilePrefab(Projectile prefab)
        {
            _prefabOverride = prefab;
        }

        public static Projectile SpawnProjectile(Projectile.SpawnData data)
        {
            GameObject go = new GameObject("Projectile");
            Projectile projectile = go.AddComponent<Projectile>();
            projectile.Launch(data);

            if (_prefabOverride != null)
            {
                GameObject visual = Object.Instantiate(_prefabOverride.gameObject, data.Origin, Quaternion.LookRotation(data.Direction));
                visual.transform.SetParent(go.transform, true);
            }
            return projectile;
        }
    }
}
