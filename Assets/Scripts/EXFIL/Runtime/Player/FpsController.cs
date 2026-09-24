using UnityEngine;
using EXFIL.Characters;
using EXFIL.Items;

namespace EXFIL.Player
{
    /// <summary>
    /// First person movement: walk / sprint / crouch / prone, stamina, weight and
    /// injury driven speed modifiers, head bob, footsteps, stance height transitions.
    /// Works for the local player and (with an injected input frame) for replays/bots.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FpsController : MonoBehaviour
    {
        [Header("Movement")]
        public float WalkSpeed = 3.1f;
        public float SprintSpeed = 5.6f;
        public float CrouchSpeed = 1.7f;
        public float ProneSpeed = 0.85f;
        public float Acceleration = 18f;
        public float Deceleration = 22f;
        public float AirControl = 0.25f;

        [Header("Stamina")]
        public float MaxStamina = 100f;
        public float SprintDrain = 12f;
        public float JumpDrain = 14f;
        public float StaminaRegen = 7f;
        public float StaminaRegenDelay = 1.4f;

        [Header("Body")]
        public float StandingHeight = 1.8f;
        public float CrouchHeight = 1.25f;
        public float ProneHeight = 0.65f;
        public float EyeHeightStanding = 1.65f;
        public float EyeHeightCrouch = 1.1f;
        public float EyeHeightProne = 0.45f;
        public float Radius = 0.32f;

        [Header("Feel")]
        public float BobFrequency = 9f;
        public float LandingHardThreshold = 6f;

        [Header("References")]
        public Transform Head;
        public AudioSource FootstepSource;
        public AudioClip[] Footsteps;

        public Core.MovementStance Stance { get; private set; }
        public float Stamina { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsGrounded { get; private set; }
        public float CurrentSpeed { get; private set; }
        public Vector3 Velocity { get { return _velocity; } }

        private CharacterController _controller;
        private Vector3 _velocity;
        private Vector3 _horizontal;
        private float _bobPhase;
        private float _regenDelay;
        private float _targetEyeHeight;
        private float _eyeHeight;
        private float _fallSpeed;
        private float _nextFootstep;
        private bool _wasGrounded = true;

        private HealthController _health;
        private SkillSet _skills;
        private Inventory _inventory;

        public float SpeedMultiplierFromLoadout { get; set; } = 1f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.height = StandingHeight;
            _controller.radius = Radius;
            _controller.center = new Vector3(0f, StandingHeight * 0.5f, 0f);
            Stamina = MaxStamina;
            _eyeHeight = EyeHeightStanding;
            _targetEyeHeight = EyeHeightStanding;
        }

        private void Start()
        {
            _health = GetComponent<HealthController>();
            _skills = GetComponent<SkillSet>();
        }

        public void BindInventory(Inventory inventory)
        {
            _inventory = inventory;
        }

        public void SetStance(Core.MovementStance stance)
        {
            Stance = stance;
            switch (stance)
            {
                case Core.MovementStance.Crouch:
                    _controller.height = CrouchHeight;
                    _targetEyeHeight = EyeHeightCrouch;
                    break;
                case Core.MovementStance.Prone:
                    _controller.height = ProneHeight;
                    _targetEyeHeight = EyeHeightProne;
                    break;
                default:
                    _controller.height = StandingHeight;
                    _targetEyeHeight = EyeHeightStanding;
                    break;
            }
            _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);
        }

        /// <summary>Drives one simulation step. Bots feed a synthetic frame.</summary>
        public void Move(Core.EXFILInput.Frame input, float deltaTime, bool allowSprint = true)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();

            IsGrounded = _controller.isGrounded;

            // ---- stance -------------------------------------------------
            Core.MovementStance wanted = Core.MovementStance.Stand;
            if (input.Prone) wanted = Core.MovementStance.Prone;
            else if (input.Crouch) wanted = Core.MovementStance.Crouch;
            if (wanted != Stance) SetStance(wanted);

            // ---- stamina ------------------------------------------------
            bool wantsSprint = allowSprint && input.Sprint && Stance == Core.MovementStance.Stand &&
                               input.Move.y > 0.1f && Stamina > 1f && CanSprint();
            IsSprinting = wantsSprint;

            if (IsSprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - SprintDrain * deltaTime);
                _regenDelay = StaminaRegenDelay;
            }
            else
            {
                _regenDelay = Mathf.Max(0f, _regenDelay - deltaTime);
                if (_regenDelay <= 0f)
                {
                    float regen = StaminaRegen * (_skills != null ? _skills.GetEnduranceRegenMultiplier() : 1f);
                    Stamina = Mathf.Min(MaxStamina, Stamina + regen * deltaTime);
                }
            }

            // ---- speed --------------------------------------------------
            float baseSpeed;
            switch (Stance)
            {
                case Core.MovementStance.Crouch: baseSpeed = CrouchSpeed; break;
                case Core.MovementStance.Prone: baseSpeed = ProneSpeed; break;
                default: baseSpeed = IsSprinting ? SprintSpeed : WalkSpeed; break;
            }

            float weightPenalty = 1f;
            if (_inventory != null)
            {
                float weight = _inventory.TotalWeight();
                float carry = 25f + (_skills != null ? _skills.GetCarryBonus() : 0f);
                if (weight > carry) weightPenalty = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01((weight - carry) / 40f));
            }

            float injuryPenalty = _health != null ? _health.GetMovementMultiplier() : 1f;
            float endurance = _skills != null ? _skills.GetSpeedMultiplier() : 1f;
            float target = baseSpeed * weightPenalty * injuryPenalty * endurance * SpeedMultiplierFromLoadout;

            // ---- horizontal movement ------------------------------------
            Vector3 wish = transform.right * input.Move.x + transform.forward * input.Move.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            wish *= target;

            float control = IsGrounded ? 1f : AirControl;
            float accel = wish.sqrMagnitude > 0.01f ? Acceleration : Deceleration;
            _horizontal = Vector3.MoveTowards(_horizontal, wish, accel * control * deltaTime);

            // ---- gravity / jump -----------------------------------------
            if (IsGrounded)
            {
                if (_velocity.y < 0f)
                {
                    if (Mathf.Abs(_fallSpeed) > LandingHardThreshold && _health != null)
                        _health.ApplyFallDamage(Mathf.Abs(_fallSpeed));
                    _velocity.y = -2f;
                }
                _fallSpeed = 0f;
            }
            else
            {
                _velocity.y += Physics.gravity.y * deltaTime;
                _fallSpeed = _velocity.y;
            }

            _velocity.x = _horizontal.x;
            _velocity.z = _horizontal.z;
            _controller.Move(_velocity * deltaTime);

            CurrentSpeed = new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;

            // ---- head bob + footsteps -----------------------------------
            float bobAmount = HeadBobAmount();
            _bobPhase += CurrentSpeed * BobFrequency * deltaTime;
            float bob = Mathf.Sin(_bobPhase) * bobAmount * (IsSprinting ? 1.4f : 1f);
            float side = Mathf.Cos(_bobPhase * 0.5f) * bobAmount * 0.6f;

            _eyeHeight = Mathf.MoveTowards(_eyeHeight, _targetEyeHeight, 4f * deltaTime);
            if (Head != null)
                Head.localPosition = new Vector3(side, _eyeHeight + bob, 0f);

            if (IsGrounded && CurrentSpeed > 1.2f && !_wasGrounded == false)
            {
                float interval = Mathf.Clamp(2.4f / Mathf.Max(0.5f, CurrentSpeed), 0.28f, 0.8f);
                if (Time.time >= _nextFootstep)
                {
                    _nextFootstep = Time.time + interval;
                    PlayFootstep();
                }
            }

            _wasGrounded = IsGrounded;
        }

        private float HeadBobAmount()
        {
            GameSettings settings = GameSettings.Load();
            float amount = settings != null ? settings.HeadBobAmount : 0.035f;
            return CurrentSpeed * amount;
        }

        private bool CanSprint()
        {
            if (_health == null) return true;
            return !_health.HasBrokenLegs() && _health.StaminaCapacity01() > 0.05f;
        }

        private void PlayFootstep()
        {
            if (FootstepSource == null || Footsteps == null || Footsteps.Length == 0) return;
            AudioClip clip = Footsteps[Random.Range(0, Footsteps.Length)];
            FootstepSource.pitch = Random.Range(0.92f, 1.08f);
            FootstepSource.PlayOneShot(clip, IsSprinting ? 1f : 0.65f);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            _horizontal = Vector3.zero;
            _velocity = Vector3.zero;
        }
    }
}
