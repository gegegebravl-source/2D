using UnityEngine;
using EXFIL.Characters;
using EXFIL.Items;

namespace EXFIL.Player
{
    /// <summary>
    /// First person view model: builds the weapon in front of the camera (real prefab when
    /// assigned, primitive stand-in otherwise), handles ADS, sway, recoil kick and reload pose.
    /// </summary>
    public class FirstPersonWeapon : MonoBehaviour
    {
        [Header("Anchors")]
        public Transform ViewModelRoot;
        public Camera ViewCamera;

        [Header("Sway")]
        public float SwayAmount = 0.012f;
        public float SwaySmooth = 8f;
        public float BobAmount = 0.006f;

        [Header("ADS")]
        public float AimSpeed = 9f;

        public Combat.WeaponHandler Handler { get; private set; }
        public bool IsAiming { get; private set; }

        private GameObject _viewModel;
        private Transform _muzzle;
        private Vector3 _swayOffset;
        private Vector3 _recoilOffset;
        private Vector3 _recoilVelocity;
        private float _aimBlend;
        private float _reloadBlend;
        private Vector3 _basePosition;
        private Vector3 _aimPosition;
        private float _bobPhase;

        public void Setup(Combat.WeaponHandler handler)
        {
            Handler = handler;
            Rebuild();
        }

        public void Rebuild()
        {
            if (_viewModel != null) Destroy(_viewModel);
            if (Handler == null || !Handler.HasWeapon) return;

            WeaponItemDefinition def = Handler.Definition;
            _viewModel = def.ViewModelPrefab != null
                ? Instantiate(def.ViewModelPrefab, ViewModelRoot)
                : BuildPlaceholderViewModel(def);

            _viewModel.transform.localPosition = def.ViewModelOffset;
            _viewModel.transform.localEulerAngles = def.ViewModelEuler;
            _basePosition = def.ViewModelOffset;
            _aimPosition = def.ViewModelAimOffset;

            _muzzle = FindChild(_viewModel.transform, "Muzzle");
            if (_muzzle == null)
            {
                GameObject muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(_viewModel.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 0.02f, 0.42f);
                _muzzle = muzzle.transform;
            }

            Handler.Muzzle = _muzzle;
            Handler.MuzzleFlash = FindParticle(_viewModel.transform);
            Handler.MuzzleLight = FindLight(_viewModel.transform);
            Handler.AudioSource = GetComponent<AudioSource>();
            Handler.ShellEjectPoint = FindChild(_viewModel.transform, "ShellEject");

            SetVisible(true);
        }

        private static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
                Transform found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static ParticleSystem FindParticle(Transform root)
        {
            ParticleSystem system = root.GetComponentInChildren<ParticleSystem>();
            return system;
        }

        private static Light FindLight(Transform root)
        {
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            return lights.Length > 0 ? lights[0] : null;
        }

        /// <summary>Primitive rifle stand-in so the game is playable before models are imported.</summary>
        private GameObject BuildPlaceholderViewModel(WeaponItemDefinition def)
        {
            GameObject root = new GameObject("VM_" + def.name) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(ViewModelRoot, false);

            Material dark = new Material(Shader.Find("Standard")) { color = new Color(0.13f, 0.13f, 0.14f) };
            Material metal = new Material(Shader.Find("Standard")) { color = new Color(0.26f, 0.26f, 0.28f) };
            Material wood = new Material(Shader.Find("Standard")) { color = new Color(0.34f, 0.22f, 0.12f) };

            bool isPistol = def.WeaponClass == Core.WeaponClass.Pistol;
            float length = isPistol ? 0.20f : 0.52f;

            GameObject receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            receiver.name = "Receiver";
            receiver.transform.SetParent(root.transform, false);
            receiver.transform.localScale = new Vector3(0.055f, 0.075f, length * 0.55f);
            receiver.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            receiver.GetComponent<Renderer>().material = metal;

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localScale = new Vector3(0.022f, length * 0.7f, 0.022f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localPosition = new Vector3(0f, 0.012f, length * 0.72f);
            barrel.GetComponent<Renderer>().material = dark;

            GameObject magazine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            magazine.name = "Magazine";
            magazine.transform.SetParent(root.transform, false);
            magazine.transform.localScale = new Vector3(0.045f, isPistol ? 0.10f : 0.16f, 0.055f);
            magazine.transform.localPosition = new Vector3(0f, -0.09f, isPistol ? 0f : 0.10f);
            magazine.GetComponent<Renderer>().material = dark;

            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            grip.transform.SetParent(root.transform, false);
            grip.transform.localScale = new Vector3(0.045f, 0.12f, 0.055f);
            grip.transform.localPosition = new Vector3(0f, -0.10f, -0.06f);
            grip.transform.localEulerAngles = new Vector3(-18f, 0f, 0f);
            grip.GetComponent<Renderer>().material = wood;

            if (!isPistol)
            {
                GameObject stock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stock.name = "Stock";
                stock.transform.SetParent(root.transform, false);
                stock.transform.localScale = new Vector3(0.05f, 0.07f, 0.20f);
                stock.transform.localPosition = new Vector3(0f, -0.01f, -0.20f);
                stock.GetComponent<Renderer>().material = wood;
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.012f, length * 1.05f);

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Destroy(colliders[i]);

            return root;
        }

        public void SetVisible(bool visible)
        {
            if (_viewModel != null) _viewModel.SetActive(visible);
        }

        public void SetAiming(bool aiming)
        {
            IsAiming = aiming;
            if (Handler != null) Handler.IsAiming = aiming;
        }

        public void AddRecoil(Vector2 kick)
        {
            _recoilVelocity += new Vector3(0f, 0f, -kick.y * 0.35f) + new Vector3(kick.x * 0.2f, 0f, 0f);
        }

        private void Update()
        {
            if (_viewModel == null || ViewModelRoot == null) return;

            _aimBlend = Mathf.MoveTowards(_aimBlend, IsAiming ? 1f : 0f, AimSpeed * Time.deltaTime);
            _reloadBlend = Mathf.MoveTowards(_reloadBlend, Handler != null && Handler.IsReloading ? 1f : 0f, 6f * Time.deltaTime);

            Vector2 look = Vector2.zero;
            float bob = 0f;
            FpsController motor = GetComponentInParent<FpsController>();
            if (motor != null) bob = Mathf.Sin(Time.time * 11f) * BobAmount * motor.CurrentSpeed;

            Vector3 targetSway = new Vector3(-look.x, -look.y + bob, 0f) * SwayAmount;
            _swayOffset = Vector3.Lerp(_swayOffset, targetSway, SwaySmooth * Time.deltaTime);

            _recoilVelocity = Vector3.Lerp(_recoilVelocity, Vector3.zero, 1f - Mathf.Exp(-14f * Time.deltaTime));
            _recoilOffset += _recoilVelocity * Time.deltaTime * 60f;
            _recoilOffset = Vector3.Lerp(_recoilOffset, Vector3.zero, 1f - Mathf.Exp(-7f * Time.deltaTime));

            Vector3 position = Vector3.Lerp(_basePosition, _aimPosition, _aimBlend);
            position += _swayOffset + _recoilOffset;
            position += Vector3.down * _reloadBlend * 0.22f;

            _viewModel.transform.localPosition = position;
            _viewModel.transform.localEulerAngles = (Handler != null && Handler.Definition != null ? Handler.Definition.ViewModelEuler : Vector3.zero)
                                                    + new Vector3(_reloadBlend * -28f, _reloadBlend * 12f, 0f);

            if (ViewCamera != null)
            {
                Core.GameSettings settings = Core.GameSettings.Load();
                float targetFov = IsAiming ? settings.AimFieldOfView : settings.FieldOfView;
                ViewCamera.fieldOfView = Mathf.Lerp(ViewCamera.fieldOfView, targetFov, 10f * Time.deltaTime);
            }
        }
    }
}
