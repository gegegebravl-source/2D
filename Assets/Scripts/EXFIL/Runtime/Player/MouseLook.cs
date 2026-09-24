using UnityEngine;

namespace EXFIL.Player
{
    /// <summary>Free look with pitch clamp, recoil kick support and smooth aim transitions.</summary>
    public class MouseLook : MonoBehaviour
    {
        [Header("Limits")]
        public float MinPitch = -88f;
        public float MaxPitch = 88f;

        [Header("Smoothing")]
        public float Smoothing = 0.02f;

        public Transform Body;      // yaw applied here
        public Transform Head;      // pitch applied here

        private float _yaw;
        private float _pitch;
        private Vector2 _recoil;
        private Vector2 _recoilVelocity;
        private float _sensitivity = 1f;

        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _pitch; } }

        private void Awake()
        {
            if (Body == null) Body = transform;
            if (Head == null) Head = transform;
            _yaw = Body.eulerAngles.y;
            _pitch = 0f;
        }

        public void SetSensitivity(float value)
        {
            _sensitivity = value;
        }

        public void Look(Vector2 delta, bool invertY)
        {
            _yaw += delta.x * _sensitivity;
            _pitch += (invertY ? -delta.y : delta.y) * _sensitivity;
            _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
        }

        /// <summary>Adds a recoil impulse (degrees) that recovers over time.</summary>
        public void AddRecoil(Vector2 kick)
        {
            _recoilVelocity += kick;
        }

        private void LateUpdate()
        {
            _recoilVelocity = Vector2.Lerp(_recoilVelocity, Vector2.zero, 1f - Mathf.Exp(-12f * Time.deltaTime));
            _recoil += _recoilVelocity * Time.deltaTime * 60f;
            _recoil = Vector2.Lerp(_recoil, Vector2.zero, 1f - Mathf.Exp(-6f * Time.deltaTime));

            float yaw = _yaw + _recoil.x;
            float pitch = Mathf.Clamp(_pitch + _recoil.y, MinPitch, MaxPitch);

            if (Body != null)
                Body.rotation = Quaternion.Slerp(Body.rotation, Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-Smoothing * 1000f * Time.deltaTime));
            if (Head != null)
                Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(pitch, 0f, 0f), 1f - Mathf.Exp(-Smoothing * 1000f * Time.deltaTime));
        }

        public void SetAngles(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        }
    }
}
