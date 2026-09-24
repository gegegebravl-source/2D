using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EXFIL.Characters;
using EXFIL.Combat;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.AI
{
    /// <summary>
    /// Full bot brain: patrol -> investigate -> combat -> flank/cover/reload/heal -> loot -> extract.
    /// Driven by NavMesh for movement and by WeaponHandler for shooting (same code as players).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class BotController : MonoBehaviour
    {
        [Header("Refs (filled by BotDirector)")]
        public BotProfile Profile;
        public NavMeshAgent Agent;
        public CharacterBody3D Body;
        public HealthController Health;
        public ArmorController Armor;
        public WeaponHandler Weapon;
        public BotPerception Perception;

        [Header("Debug")]
        public BotState State = BotState.Patrol;
        public float StateTime;
        public string DebugTargetName = "-";

        public Inventory Inventory { get; private set; }
        public bool IsAlive { get { return Health == null || Health.IsAlive; } }
        public uint NetId;

        private Transform _target;
        private HealthController _targetHealth;
        private Vector3 _homePoint;
        private Vector3 _patrolTarget;
        private float _nextDecision;
        private float _burstEnd;
        private float _nextBurst;
        private float _reactionTimer;
        private float _aimError;
        private bool _firing;
        private Rng _rng;
        private List<ItemInstance> _meds = new List<ItemInstance>();

        public void Initialize(BotProfile profile, Inventory inventory, uint netId, Rng rng)
        {
            Profile = profile;
            Inventory = inventory ?? new Inventory();
            _rng = rng ?? new Rng((int)(Time.time * 1000f));
            NetId = netId;

            Agent = GetComponent<NavMeshAgent>();
            Health = GetComponent<HealthController>();
            if (Health == null) Health = gameObject.AddComponent<HealthController>();
            Health.NetId = netId;
            Health.CharacterName = profile != null ? profile.DisplayName : "Bot";

            Armor = gameObject.AddComponent<ArmorController>();
            Armor.Bind(Inventory);
            Armor.Rebuild();

            Weapon = gameObject.AddComponent<WeaponHandler>();
            Weapon.Bind(netId, Health.CharacterName, null);

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(transform, false);
            muzzle.transform.localPosition = new Vector3(0.22f, 1.32f, 0.55f);
            Weapon.Muzzle = muzzle.transform;
            ItemInstance weapon = Inventory.GetEquipped(EquipmentSlot.PrimaryWeapon);
            if (weapon == null)
                foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in Inventory.AllEquipment())
                    if (pair.Value != null && pair.Value.Def is WeaponItemDefinition) { weapon = pair.Value; break; }
            Weapon.Equip(weapon);

            CharacterBody3D existing = GetComponentInChildren<CharacterBody3D>();
            if (existing != null) Body = existing;

            Perception = gameObject.AddComponent<BotPerception>();
            Perception.Initialize(profile, transform);

            if (Agent != null)
            {
                Agent.speed = 3.2f * (profile != null ? profile.MoveSpeedMultiplier : 1f);
                Agent.acceleration = 12f;
                Agent.stoppingDistance = 1.2f;
            }

            _homePoint = transform.position;
            State = BotState.Patrol;
            PickPatrolPoint();

            Health.OnDied += OnDied;
        }

        private void Update()
        {
            if (!IsAlive) return;
            StateTime += Time.deltaTime;

            Scan();
            Think();
            Act();
        }

        // ------------------------------------------------------------------ senses
        private void Scan()
        {
            Transform best = null;
            HealthController bestHealth = null;
            float bestScore = 0f;

            List<HealthController> candidates = Raid.RaidManager.Instance != null
                ? Raid.RaidManager.Instance.PotentialTargets
                : new List<HealthController>();

            for (int i = 0; i < candidates.Count; i++)
            {
                HealthController candidate = candidates[i];
                if (candidate == null || candidate == Health || !candidate.IsAlive) continue;
                if (!Perception.TryDetect(candidate.transform, candidate)) continue;

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                float score = 1f / Mathf.Max(1f, distance);
                if (score > bestScore) { bestScore = score; best = candidate.transform; bestHealth = candidate; }
            }

            if (best != null)
            {
                if (_target != best) _reactionTimer = Profile != null ? Profile.ReactionTime : 0.5f;
                _target = best;
                _targetHealth = bestHealth;
                DebugTargetName = best.name;
            }
        }

        // ------------------------------------------------------------------- brain
        private void Think()
        {
            if (State == BotState.Dead) return;

            bool enemyVisible = _target != null && _targetHealth != null && _targetHealth.IsAlive &&
                                HasLineOfSight(_target);

            if (enemyVisible && State != BotState.Combat && State != BotState.Flank)
            {
                SetState(BotState.Combat);
                _reactionTimer = Profile != null ? Profile.ReactionTime : 0.5f;
            }

            if (Weapon != null && Weapon.IsJammed) Weapon.TryClearJam();
            if (Weapon != null && !Weapon.IsReloading && Weapon.HasWeapon && Weapon.AmmoCount <= 0)
                SetState(BotState.Reload);

            switch (State)
            {
                case BotState.Combat:
                    if (!enemyVisible && StateTime > 4f) SetState(BotState.Investigate);
                    if (Health != null && Health.TotalHp < Health.MaxTotalHp * 0.35f && _rng.Chance(0.4f))
                        SetState(BotState.Heal);
                    break;

                case BotState.Investigate:
                    if (StateTime > 12f) SetState(BotState.Patrol);
                    break;

                case BotState.Patrol:
                    if (Agent != null && !Agent.pathPending && Agent.remainingDistance < 1.5f)
                    {
                        if (Time.time > _nextDecision)
                        {
                            _nextDecision = Time.time + 2f + _rng.NextFloat() * 4f;
                            PickPatrolPoint();
                        }
                    }
                    break;

                case BotState.Reload:
                    if (Weapon != null && !Weapon.IsReloading) SetState(enemyVisible ? BotState.Combat : BotState.Investigate);
                    break;

                case BotState.Heal:
                    if (StateTime > 4f) SetState(enemyVisible ? BotState.Combat : BotState.Investigate);
                    break;
            }
        }

        private void SetState(BotState state)
        {
            if (State == state) return;
            State = state;
            StateTime = 0f;

            if (state == BotState.Reload && Weapon != null) Weapon.StartReload();
            if (state == BotState.Heal) TryHeal();
        }

        private void Act()
        {
            if (Agent == null) return;

            switch (State)
            {
                case BotState.Combat: CombatBehaviour(); break;
                case BotState.Investigate:
                    Agent.isStopped = false;
                    Agent.SetDestination(Perception != null ? Perception.LastKnownPosition : transform.position);
                    break;
                case BotState.Patrol:
                    Agent.isStopped = false;
                    Agent.SetDestination(_patrolTarget);
                    break;
                case BotState.Flee:
                    Agent.isStopped = false;
                    Agent.SetDestination(_homePoint);
                    break;
                default:
                    Agent.isStopped = true;
                    break;
            }
        }

        private void CombatBehaviour()
        {
            if (_target == null || Weapon == null) { SetState(BotState.Investigate); return; }

            float distance = Vector3.Distance(transform.position, _target.position);
            float preferred = 12f + (Profile != null ? (1f - Profile.Aggression) * 20f : 10f);

            // movement: close in, back off, or strafe
            if (StateTime > 1.6f && _rng.Chance(0.02f) && Profile != null && _rng.Chance(Profile.FlankChance))
            {
                Vector3 flank = _target.position + Quaternion.Euler(0f, _rng.Range(-140, 140), 0f) * Vector3.forward * preferred;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(flank, out hit, 4f, NavMesh.AllAreas))
                {
                    Agent.isStopped = false;
                    Agent.SetDestination(hit.position);
                }
            }
            else if (Mathf.Abs(distance - preferred) > 3f)
            {
                Vector3 direction = (transform.position - _target.position).normalized;
                Vector3 wanted = distance > preferred
                    ? _target.position + direction * preferred
                    : transform.position + direction * 4f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(wanted, out hit, 3f, NavMesh.AllAreas))
                {
                    Agent.isStopped = false;
                    Agent.SetDestination(hit.position);
                }
            }
            else
            {
                Agent.isStopped = true;
            }

            // face and shoot
            Vector3 aimPoint = _target.position + Vector3.up * 1.2f;
            Vector3 toTarget = aimPoint - transform.position;
            Quaternion wanted2 = Quaternion.LookRotation(new Vector3(toTarget.x, 0f, toTarget.z).normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted2,
                (Profile != null ? Profile.AimSpeed : 6f) * 12f * Time.deltaTime);

            if (_reactionTimer > 0f)
            {
                _reactionTimer -= Time.deltaTime;
                return;
            }

            bool aligned = Vector3.Angle(transform.forward, toTarget.normalized) < 12f;
            if (!aligned || !HasLineOfSight(_target)) return;

            if (Time.time > _nextBurst)
            {
                if (Time.time > _burstEnd)
                {
                    _nextBurst = Time.time + (Profile != null ? Profile.BurstPause : 0.8f);
                    _burstEnd = Time.time + (Profile != null ? Profile.BurstDuration : 0.5f);
                    _aimError = (1f - (Profile != null ? Profile.Accuracy : 0.4f)) * 6f;
                }

                Vector3 shotDirection = WeaponHandler.ApplySpread(toTarget.normalized, _aimError);
                if (Weapon.Muzzle != null) Weapon.Muzzle.rotation = Quaternion.LookRotation(shotDirection);
                _firing = true;
            }
            else
            {
                _firing = false;
            }

            if (_firing) Weapon.TryFire(false, true, Time.deltaTime);
        }

        private bool HasLineOfSight(Transform target)
        {
            Vector3 origin = transform.position + Vector3.up * 1.55f;
            Vector3 destination = target.position + Vector3.up * 1.2f;
            RaycastHit hit;
            if (Physics.Raycast(origin, (destination - origin).normalized, out hit,
                    Vector3.Distance(origin, destination), ~0, QueryTriggerInteraction.Ignore))
            {
                HealthController health = hit.collider.GetComponentInParent<HealthController>();
                return health != null && health == _targetHealth;
            }
            return true;
        }

        private void PickPatrolPoint()
        {
            Vector3 random = transform.position + new Vector3(_rng.Range(-25, 25), 0f, _rng.Range(-25, 25));
            NavMeshHit hit;
            if (NavMesh.SamplePosition(random, out hit, 12f, NavMesh.AllAreas))
                _patrolTarget = hit.position;
        }

        private void TryHeal()
        {
            _meds.Clear();
            Inventory.Containers.ForEach(container =>
                container.Grid.ForEachItem(item =>
                {
                    if (item.Def is MedicalItemDefinition) _meds.Add(item);
                }));

            if (_meds.Count == 0) return;
            ItemInstance med = _rng.Pick(_meds);
            MedicalItemDefinition def = med.Def as MedicalItemDefinition;
            if (def == null) return;

            Health.HealAll(Mathf.Min(35f, def.MaxResource));
            for (int i = 0; i < def.Removes.Count; i++) Health.RemoveEffect(def.Removes[i]);
            med.Stack -= 1;
            if (med.Stack <= 0) Inventory.Remove(med.Uid, out ItemInstance removed);
        }

        private void OnDied()
        {
            SetState(BotState.Dead);
            if (Agent != null) Agent.isStopped = true;

            // drop the corpse loot
            Raid.LootPile pile = Raid.LootPile.Create(transform.position + Vector3.up * 0.35f);
            List<ItemInstance> drops = new List<ItemInstance>();
            foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in Inventory.AllEquipment())
                if (pair.Value != null) drops.Add(pair.Value);
            foreach (ContainerInstance container in Inventory.Containers)
                drops.AddRange(container.Grid.Items);
            pile.AddItems(drops);

            if (Body != null) StartCoroutine(CorpseFade());
        }

        private IEnumerator CorpseFade()
        {
            yield return new WaitForSeconds(45f);
            Destroy(gameObject);
        }
    }
}
