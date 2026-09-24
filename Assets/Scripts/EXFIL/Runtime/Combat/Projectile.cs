using UnityEngine;
using EXFIL.Characters;
using EXFIL.Items;

namespace EXFIL.Combat
{
    /// <summary>Travel-time projectile: raycasts each step so fast rounds still hit reliably.</summary>
    public class Projectile : MonoBehaviour
    {
        public struct SpawnData
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public float Velocity;
            public AmmoItemDefinition Ammo;
            public uint OwnerId;
            public string OwnerName;
            public string WeaponId;
        }

        public SpawnData Data { get; private set; }
        public float DistanceTravelled { get; private set; }
        public float MaxDistance = 400f;
        public LayerMask HitMask = ~0;

        private Vector3 _position;
        private Vector3 _velocity;
        private float _damage;
        private bool _dead;

        public void Launch(SpawnData data)
        {
            Data = data;
            _position = data.Origin;
            _velocity = data.Direction.normalized * data.Velocity;
            _damage = data.Ammo != null ? data.Ammo.Damage : 20f;
            transform.position = _position;
        }

        private void Update()
        {
            if (_dead) return;
            Step(Time.deltaTime);
        }

        public void Step(float deltaTime)
        {
            if (_dead) return;

            Vector3 next = _position + _velocity * deltaTime;
            _velocity += Vector3.down * Ballistics.Gravity * deltaTime * 0.35f;

            Vector3 segment = next - _position;
            float distance = segment.magnitude;
            if (distance <= 0.0001f) return;

            RaycastHit hit;
            if (Physics.Raycast(_position, segment.normalized, out hit, distance, HitMask, QueryTriggerInteraction.Ignore))
            {
                ProcessHit(hit);
                return;
            }

            _position = next;
            transform.position = _position;
            transform.rotation = Quaternion.LookRotation(segment.normalized);
            DistanceTravelled += distance;

            if (DistanceTravelled > MaxDistance) Destroy(gameObject);
        }

        private void ProcessHit(RaycastHit hit)
        {
            _dead = true;

            Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox != null && hitbox.Owner != null)
            {
                DamageResolver.Resolve(hitbox, Data, _damage, hit.point, hit.normal);
            }
            else
            {
                // surface impact decal / particles are spawned by the impact service
                Core.GameEvents.Raise(new Core.ShotFiredEvent { Origin = hit.point, Direction = hit.normal, Loudness = 0.05f });
            }

            Destroy(gameObject, 0.02f);
        }
    }
}
