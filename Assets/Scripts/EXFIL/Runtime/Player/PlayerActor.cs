using UnityEngine;
using EXFIL.Characters;
using EXFIL.Combat;
using EXFIL.Core;

namespace EXFIL.Player
{
    /// <summary>
    /// The local player: reads input, drives movement / look / weapon / interaction,
    /// awards skill xp and reports state upwards (UI, netcode, raid manager).
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class PlayerActor : MonoBehaviour
    {
        [Header("Refs")]
        public FpsController Motor;
        public MouseLook Look;
        public InteractionSystem Interaction;
        public PlayerLoadout Loadout;
        public FirstPersonWeapon ViewModel;
        public HealthController Health;
        public SkillSet Skills;
        public Camera ViewCamera;
        public CharacterBody3D Body;

        [Header("Settings")]
        public bool LockCursorOnStart = true;

        public bool InputEnabled { get; set; } = true;
        public uint NetId;

        private EXFILInput.Frame _input;
        private float _distanceTravelled;
        private bool _aimHeld;

        private void Awake()
        {
            if (Motor == null) Motor = GetComponent<FpsController>();
            if (Look == null) Look = GetComponentInChildren<MouseLook>();
            if (Health == null) Health = GetComponent<HealthController>();
            if (Skills == null) Skills = GetComponent<SkillSet>();
            if (Loadout == null) Loadout = GetComponent<PlayerLoadout>();
            if (ViewCamera == null) ViewCamera = GetComponentInChildren<Camera>();

            GameSettings settings = GameSettings.Load();
            if (Look != null) Look.SetSensitivity(settings.MouseSensitivity);
            if (ViewCamera != null) ViewCamera.fieldOfView = settings.FieldOfView;
            Application.targetFrameRate = settings.TargetFrameRate;
        }

        private void Start()
        {
            if (LockCursorOnStart) EXFILInput.SetCursorLock(true);
            if (Loadout != null && ViewModel != null)
            {
                ViewModel.Setup(Loadout.PrimaryHandler);
                Loadout.SwitchTo(1);
            }
            if (Health != null)
            {
                Health.CharacterName = Loadout != null ? gameObject.name : "Operator";
                Health.OnDied += OnDeath;
            }
        }

        public void Initialize(CharacterDefinition definition, Inventory inventory, uint netId)
        {
            NetId = netId;
            if (Loadout != null) Loadout.Initialize(definition, inventory, netId, definition != null ? definition.DisplayName : "Operator");
            if (Health != null) Health.NetId = netId;
        }

        private void Update()
        {
            _input = InputEnabled ? EXFILInput.Sample() : default(EXFILInput.Frame);
            if (!InputEnabled) return;

            HandleLook();
            HandleStance();
            HandleWeapons();
            HandleInteraction();
            HandleMovementXp();
        }

        private void FixedUpdate()
        {
            if (!InputEnabled) return;
            float before = transform.position.magnitude;
            Motor.Move(_input, Time.fixedDeltaTime);
            _distanceTravelled += Mathf.Abs(transform.position.magnitude - before);
        }

        private void HandleLook()
        {
            if (Look == null) return;
            GameSettings settings = GameSettings.Load();
            Look.Look(_input.Look * settings.MouseSensitivity, settings.InvertY);
        }

        private void HandleStance()
        {
            // stance is applied inside Motor.Move from the input frame
        }

        private void HandleWeapons()
        {
            if (Loadout == null) return;

            if (_input.WeaponSlot > 0) Loadout.SwitchTo(_input.WeaponSlot);
            if (_input.NextWeapon) Loadout.NextWeapon();
            if (_input.Reload) Loadout.Reload();
            if (_input.ToggleFireMode) Loadout.CycleFireMode();

            if (ViewModel != null)
            {
                bool aiming = Core.GameSettings.Load().ToggleAim
                    ? (_input.Aim && !_aimHeld ? !ViewModel.IsAiming : ViewModel.IsAiming)
                    : _input.Aim;
                _aimHeld = _input.Aim;

                if (aiming != ViewModel.IsAiming)
                {
                    ViewModel.SetAiming(aiming);
                    if (ViewModel.Handler != Loadout.ActiveWeapon || ViewModel.Handler == null)
                        ViewModel.Setup(Loadout.ActiveWeapon);
                }

                if (Loadout.ActiveWeapon != ViewModel.Handler && !Loadout.IsBusy)
                    ViewModel.Setup(Loadout.ActiveWeapon);
            }

            bool fired = Loadout.TryFire(_input.FirePressed, _input.Fire, Time.deltaTime);
            if (fired && ViewModel != null && Look != null)
            {
                Vector2 kick = Loadout.ActiveWeapon != null ? Loadout.ActiveWeapon.GetRecoilKick() : Vector2.zero;
                Look.AddRecoil(kick);
                ViewModel.AddRecoil(kick);
            }

            for (int slot = 1; slot < Loadout.QuickSlots.Length; slot++)
            {
                if (_input.WeaponSlot != slot) continue;
                // quick item usage is bound to number keys 1..4 for weapons; quick-use lies on H
            }
            if (_input.Heal && Loadout.QuickSlots[4] != null)
                Loadout.UseItem(Loadout.QuickSlots[4]);
        }

        private void HandleInteraction()
        {
            if (Interaction != null) Interaction.Tick(_input.Interact, _input.InteractPressed);
        }

        private void HandleMovementXp()
        {
            if (Skills == null) return;
            if (Motor != null && Motor.CurrentSpeed > 2.2f)
                Skills.AddXp(SkillType.Endurance, Time.deltaTime * 0.35f);
            if (Loadout != null && Loadout.Inventory != null && Loadout.Inventory.TotalWeight() > 25f)
                Skills.AddXp(SkillType.Strength, Time.deltaTime * 0.25f);
        }

        private void OnDeath()
        {
            InputEnabled = false;
            if (Motor != null) Motor.enabled = false;
            if (ViewCamera != null)
            {
                // simple death cam tilt
                ViewCamera.transform.localEulerAngles = new Vector3(0f, 0f, 55f);
            }
            Debug.Log("[EXFIL] Player died.");
        }

        public EXFILInput.Frame CurrentInput { get { return _input; } }
    }
}
