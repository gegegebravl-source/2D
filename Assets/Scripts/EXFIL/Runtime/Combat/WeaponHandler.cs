using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Characters;
using EXFIL.Items;
using EXFIL.Player;

namespace EXFIL.Combat
{
    /// <summary>
    /// The logical weapon: fire modes, cycling, magazines, jams, durability, recoil events.
    /// Shared by the local player, remote players and bots (bots just feed synthetic input).
    /// </summary>
    public class WeaponHandler : MonoBehaviour
    {
        [Header("Muzzle")]
        public Transform Muzzle;
        public ParticleSystem MuzzleFlash;
        public Light MuzzleLight;
        public GameObject ShellPrefab;
        public Transform ShellEjectPoint;

        [Header("Audio")]
        public AudioSource AudioSource;

        public ItemInstance Weapon { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsJammed { get; private set; }
        public bool IsAiming { get; set; }
        public float ReloadProgress { get; private set; }
        public Core.FireMode FireMode { get; private set; }
        public float Spread { get; private set; }
        public float Heat { get; private set; }

        private float _nextShotTime;
        private float _reloadEnd;
        private int _burstLeft;
        private float _chamberTime;

        private WeaponItemDefinition _def;
        private WeaponStats _stats;
        private SkillSet _skills;
        private uint _ownerId;
        private string _ownerName = "Operator";
        private List<string> _modules = new List<string>();

        public event Action<ItemInstance, WeaponStats> OnShot;
        public event Action<float> OnReloadStarted;     // duration
        public event Action OnReloadFinished;
        public event Action OnJammed;
        public event Action OnDryFire;

        public WeaponStats Stats { get { return _stats; } }
        public WeaponItemDefinition Definition { get { return _def; } }

        public void Bind(uint ownerId, string ownerName, SkillSet skills)
        {
            _ownerId = ownerId;
            _ownerName = ownerName;
            _skills = skills;
        }

        /// <summary>Equips an item instance; rebuilds cached stats and module list.</summary>
        public void Equip(ItemInstance weapon)
        {
            Weapon = weapon;
            IsReloading = false;
            IsJammed = false;
            _burstLeft = 0;
            RefreshStats();
        }

        public void RefreshStats()
        {
            if (Weapon == null)
            {
                _def = null;
                _stats = null;
                return;
            }
            _def = Weapon.Def as WeaponItemDefinition;
            _stats = Weapon.BuildStats();
            _modules = Weapon.Modules;
            if (_def != null && !_def.HasFireMode(FireMode))
                FireMode = _def.HasFireMode(Core.FireMode.FullAuto) ? Core.FireMode.FullAuto : _def.DefaultFireMode;
            if (_def != null && FireMode == Core.FireMode.None) FireMode = _def.DefaultFireMode;
        }

        public bool HasWeapon { get { return Weapon != null && _def != null; } }

        public void CycleFireMode()
        {
            if (_def == null) return;
            Core.FireMode[] order = { Core.FireMode.Single, Core.FireMode.Burst, Core.FireMode.FullAuto };
            int index = 0;
            for (int i = 0; i < order.Length; i++)
                if (order[i] == FireMode) index = i;

            for (int step = 1; step <= order.Length; step++)
            {
                Core.FireMode candidate = order[(index + step) % order.Length];
                if (_def.HasFireMode(candidate))
                {
                    FireMode = candidate;
                    return;
                }
            }
        }

        /// <summary>Returns true when a shot was fired this call.</summary>
        public bool TryFire(bool firePressed, bool fireHeld, float deltaTime)
        {
            TickTimers(deltaTime);
            if (!HasWeapon || IsReloading) return false;
            if (_chamberTime > 0f) return false;

            bool wantsShot;
            switch (FireMode)
            {
                case Core.FireMode.Single: wantsShot = firePressed; break;
                case Core.FireMode.Burst:
                    if (firePressed) _burstLeft = Mathf.RoundToInt(_def.BurstCount);
                    wantsShot = _burstLeft > 0;
                    break;
                default: wantsShot = fireHeld; break;
            }

            if (!wantsShot) return false;
            if (Time.time < _nextShotTime) return false;

            if (IsJammed)
            {
                OnDryFire?.Invoke();
                if (firePressed) TryClearJam();
                return false;
            }

            string roundId = Weapon.PeekRound();
            if (string.IsNullOrEmpty(roundId))
            {
                OnDryFire?.Invoke();
                if (firePressed && Core.GameSettings.Load().AutoReloadEmpty) StartReload();
                return false;
            }

            // malfunction roll scales with wear and heat
            float wear = 1f - Weapon.Durability01;
            float failureChance = _def.FailureChanceAtZero * wear * (0.6f + Heat * 0.4f);
            if (UnityEngine.Random.value < failureChance)
            {
                IsJammed = true;
                OnJammed?.Invoke();
                return false;
            }

            Weapon.ConsumeRound();
            Weapon.Durability = Mathf.Max(0f, Weapon.CurrentDurability - _def.WearPerShot);
            Heat += _def.OverheatPerShot;
            RefreshStats();

            _nextShotTime = Time.time + _def.SecondsPerShot;
            if (FireMode == Core.FireMode.Burst) _burstLeft--;
            _chamberTime = _def.ChamberTime;

            FireProjectile(roundId);
            PlayShotEffects();
            OnShot?.Invoke(Weapon, _stats);

            if (_skills != null && _def != null)
                _skills.AddXp(Core.SkillType.WeaponClassToSkill(_def.WeaponClass) == Core.SkillType.AssaultRifle
                    ? SkillSet.WeaponClassToSkill(_def.WeaponClass)
                    : SkillSet.WeaponClassToSkill(_def.WeaponClass), 0.25f);

            return true;
        }

        private void TickTimers(float deltaTime)
        {
            if (_chamberTime > 0f) _chamberTime -= deltaTime;
            Heat = Mathf.Max(0f, Heat - _def != null ? 0f : 0f);
            if (_def != null) Heat = Mathf.Max(0f, Heat - (_def.OverheatPerShot / Mathf.Max(0.1f, _def.OverheatCooldown)) * deltaTime * 3f);
            Spread = Mathf.Max(0f, Spread - (_def != null ? _def.SpreadRecoverSpeed : 2f) * deltaTime);

            if (IsReloading && Time.time >= _reloadEnd)
                FinishReload();
        }

        private void FireProjectile(string roundId)
        {
            AmmoItemDefinition ammo = ItemDatabase.Find(roundId) as AmmoItemDefinition;
            if (ammo == null) return;

            float spreadDegrees = (_stats != null ? _stats.SpreadDegrees : 1f) + Spread;
            float moveSpread = 0f;
            FpsController motor = GetComponent<FpsController>();
            if (motor != null) moveSpread = motor.CurrentSpeed * 0.22f;
            spreadDegrees += moveSpread;
            if (IsAiming) spreadDegrees *= 0.35f;

            Transform origin = Muzzle != null ? Muzzle : transform;
            Vector3 direction = ApplySpread(origin.forward, spreadDegrees);

            float velocity = _stats != null ? _stats.MuzzleVelocity : ammo.Velocity;
            Ballistics.SpawnProjectile(new Projectile.SpawnData
            {
                Origin = origin.position,
                Direction = direction,
                Velocity = velocity,
                Ammo = ammo,
                OwnerId = _ownerId,
                OwnerName = _ownerName,
                WeaponId = _def != null ? _def.Id : string.Empty
            });

            Spread += _def != null ? _def.SpreadPerShot : 0.4f;

            bool silenced = false;
            for (int i = 0; i < _modules.Count; i++)
            {
                WeaponModuleItemDefinition mod = ItemDatabase.Find(_modules[i]) as WeaponModuleItemDefinition;
                if (mod != null && mod.SilencesShots) silenced = true;
            }

            Core.GameEvents.Raise(new Core.ShotFiredEvent
            {
                Origin = origin.position,
                Direction = direction,
                Caliber = ammo.Caliber,
                Loudness = silenced ? 0.18f : 1f,
                ShooterId = _ownerId,
                IsSilenced = silenced
            });
        }

        public static Vector3 ApplySpread(Vector3 direction, float degrees)
        {
            if (degrees <= 0f) return direction;
            float radius = Mathf.Tan(degrees * 0.5f * Mathf.Deg2Rad);
            Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, direction).normalized;
            return (direction + right * offset.x + up * offset.y).normalized;
        }

        private void PlayShotEffects()
        {
            if (AudioSource != null && _def != null && _def.FireSound != null)
                AudioSource.PlayOneShot(_def.FireSound, 0.9f);
            if (MuzzleFlash != null) MuzzleFlash.Play();
            if (MuzzleLight != null) StartCoroutine(FlashLight());
            if (ShellPrefab != null && ShellEjectPoint != null)
            {
                GameObject shell = Instantiate(ShellPrefab, ShellEjectPoint.position, ShellEjectPoint.rotation);
                Rigidbody rb = shell.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(ShellEjectPoint.right * 2.4f + Vector3.up * 1.6f, ForceMode.Impulse);
                Destroy(shell, 6f);
            }
        }

        private System.Collections.IEnumerator FlashLight()
        {
            MuzzleLight.enabled = true;
            yield return new WaitForSeconds(0.045f);
            if (MuzzleLight != null) MuzzleLight.enabled = false;
        }

        // ---------------------------------------------------------------- reload
        public void StartReload()
        {
            if (!HasWeapon || IsReloading || Weapon == null) return;
            ItemInstance mag = Weapon.GetMagazine();
            bool empty = Weapon.AmmoCount == 0;
            float duration = empty ? _def.ReloadEmpty : _def.ReloadTactical;
            if (_skills != null) duration /= _skills.GetReloadSpeedMultiplier(_def.WeaponClass);

            IsReloading = true;
            ReloadProgress = 0f;
            _reloadEnd = Time.time + duration;
            OnReloadStarted?.Invoke(duration);
        }

        private void FinishReload()
        {
            IsReloading = false;
            IsJammed = false;
            ReloadProgress = 1f;

            // pull the most loaded compatible magazine from the rig / pockets
            ItemInstance best = null;
            Inventory inventory = FindOwnerInventory();
            if (inventory != null)
            {
                List<string> ammoPool = new List<string>();
                CollectAmmo(inventory, _def.Caliber, ammoPool);

                List<ItemInstance> candidates = new List<ItemInstance>();
                inventory.Containers.ForEach(container =>
                    container.Grid.ForEachItem(item =>
                    {
                        MagazineItemDefinition magDef = item.Def as MagazineItemDefinition;
                        if (magDef != null && magDef.Caliber == _def.Caliber)
                            candidates.Add(item);
                    }));

                for (int i = 0; i < candidates.Count; i++)
                    if (best == null || candidates[i].Rounds.Count > best.Rounds.Count)
                        best = candidates[i];

                ItemInstance oldMag = Weapon.GetMagazine();
                if (oldMag != null)
                {
                    Weapon.Contents.Remove(oldMag);
                    inventory.TryAdd(oldMag, out ItemInstance leftover);
                    if (leftover != null) Debug.Log("[EXFIL] No space for the old magazine.");
                }

                if (best != null)
                {
                    inventory.Remove(best.Uid, out ItemInstance moved);
                    Weapon.Contents.Add(moved);
                }
            }

            OnReloadFinished?.Invoke();
            RefreshStats();
        }

        private void CollectAmmo(Inventory inventory, Core.AmmoCaliber caliber, List<string> pool)
        {
            inventory.Containers.ForEach(container =>
                container.Grid.ForEachItem(item =>
                {
                    AmmoItemDefinition ammo = item.Def as AmmoItemDefinition;
                    if (ammo != null && ammo.Caliber == caliber)
                        for (int i = 0; i < item.Stack; i++) pool.Add(item.DefinitionId);
                }));
        }

        private Inventory FindOwnerInventory()
        {
            Player.PlayerLoadout loadout = GetComponentInParent<Player.PlayerLoadout>();
            if (loadout != null) return loadout.Inventory;
            AI.BotController bot = GetComponentInParent<AI.BotController>();
            return bot != null ? bot.Inventory : null;
        }

        public void TryClearJam()
        {
            if (!IsJammed) return;
            IsJammed = false;
            StartReload();
        }

        /// <summary>Recoil impulse in degrees, ready to feed MouseLook.AddRecoil.</summary>
        public Vector2 GetRecoilKick()
        {
            if (_stats == null) return Vector2.zero;
            float horizontal = UnityEngine.Random.Range(-1f, 1f) * _stats.HorizontalRecoil * 0.02f;
            float vertical = _stats.VerticalRecoil * 0.016f;
            if (IsAiming) { horizontal *= 0.7f; vertical *= 0.7f; }
            if (_skills != null)
            {
                float multiplier = _skills.GetRecoilMultiplier(_def.WeaponClass);
                horizontal *= multiplier;
                vertical *= multiplier;
            }
            return new Vector2(horizontal, vertical);
        }
    }
}
