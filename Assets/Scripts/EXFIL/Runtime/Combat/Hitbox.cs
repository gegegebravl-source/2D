using UnityEngine;
using EXFIL.Core;
using EXFIL.Characters;

namespace EXFIL.Combat
{
    /// <summary>Body part collider. Bots and players both get one collider per body part.</summary>
    public class Hitbox : MonoBehaviour
    {
        public BodyPart Part = BodyPart.Thorax;
        [Range(0.5f, 2f)] public float DamageMultiplier = 1f;

        private HealthController _owner;
        private ArmorController _armor;
        private CharacterBody3D _body;

        public HealthController Owner
        {
            get
            {
                if (_owner == null) _owner = GetComponentInParent<HealthController>();
                return _owner;
            }
        }

        public ArmorController Armor
        {
            get
            {
                if (_armor == null) _armor = GetComponentInParent<ArmorController>();
                return _armor;
            }
        }

        public CharacterBody3D Body
        {
            get
            {
                if (_body == null) _body = GetComponentInParent<CharacterBody3D>();
                return _body;
            }
        }

        /// <summary>Attaches hit colliders to a freshly built body (placeholder or imported).</summary>
        public static void BuildHitboxes(CharacterBody3D body, HealthController health)
        {
            if (body == null || health == null) return;

            Transform[] anchors = { body.HeadAnchor, body.ChestAnchor, body.HipsAnchor };
            BodyPart[] parts = { BodyPart.Head, BodyPart.Thorax, BodyPart.Stomach };
            Vector3[] sizes =
            {
                new Vector3(0.26f, 0.30f, 0.26f),
                new Vector3(0.44f, 0.40f, 0.28f),
                new Vector3(0.40f, 0.26f, 0.28f)
            };

            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null) continue;
                GameObject go = new GameObject("Hitbox_" + parts[i]);
                go.transform.SetParent(anchors[i], false);
                go.layer = body.gameObject.layer;
                BoxCollider collider = go.AddComponent<BoxCollider>();
                collider.size = sizes[i];
                collider.isTrigger = false;
                Hitbox hitbox = go.AddComponent<Hitbox>();
                hitbox.Part = parts[i];
                if (parts[i] == BodyPart.Head) hitbox.DamageMultiplier = 1.6f;
            }

            // simplified arm / leg boxes around the torso root
            CreateLimbHitbox(body, health, "Hitbox_LeftArm", BodyPart.LeftArm, new Vector3(-0.30f, 1.28f, 0f));
            CreateLimbHitbox(body, health, "Hitbox_RightArm", BodyPart.RightArm, new Vector3(0.30f, 1.28f, 0f));
            CreateLimbHitbox(body, health, "Hitbox_LeftLeg", BodyPart.LeftLeg, new Vector3(-0.12f, 0.45f, 0f));
            CreateLimbHitbox(body, health, "Hitbox_RightLeg", BodyPart.RightLeg, new Vector3(0.12f, 0.45f, 0f));
        }

        private static void CreateLimbHitbox(CharacterBody3D body, HealthController health, string name, BodyPart part, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = localPosition;
            go.layer = body.gameObject.layer;
            CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
            collider.radius = 0.09f;
            collider.height = part == BodyPart.LeftLeg || part == BodyPart.RightLeg ? 0.85f : 0.42f;
            collider.direction = 1;
            Hitbox hitbox = go.AddComponent<Hitbox>();
            hitbox.Part = part;
            hitbox.DamageMultiplier = 0.7f;
        }
    }
}
