using UnityEngine;
using EXFIL.Core;

namespace EXFIL.AI
{
    /// <summary>Vision cone + hearing. Hears every ShotFiredEvent in the world.</summary>
    public class BotPerception : MonoBehaviour
    {
        public BotProfile Profile;
        public LayerMask Blockers = ~0;

        public Transform Target { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public bool HasTarget { get; private set; }
        public float Visibility { get; private set; }   // 0..1 confidence

        private float _memoryTimer;
        private float _attention;                        // ramps up while the target is visible
        private Transform _eyes;

        public void Initialize(BotProfile profile, Transform eyes)
        {
            Profile = profile;
            _eyes = eyes != null ? eyes : transform;
            GameEvents.Subscribe<ShotFiredEvent>(OnShotHeard);
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<ShotFiredEvent>(OnShotHeard);
        }

        private void OnShotHeard(ShotFiredEvent evt)
        {
            if (Profile == null) return;
            float radius = Profile.HearingRadius * evt.Loudness;
            if (Vector3.Distance(transform.position, evt.Origin) > radius) return;
            if (Vector3.Distance(transform.position, evt.Origin) < 1.5f) return;

            LastKnownPosition = evt.Origin;
            if (!HasTarget)
            {
                _memoryTimer = Profile.MemoryTime;
                HasTarget = true;
                _attention = 0.35f;
            }
        }

        private void Update()
        {
            if (Profile == null) return;

            if (HasTarget && Target == null)
            {
                _memoryTimer -= Time.deltaTime;
                if (_memoryTimer <= 0f) { HasTarget = false; _attention = 0f; }
            }

            Visibility = Mathf.Clamp01(_attention);
        }

        /// <summary>Scans all potential targets (players and other bots).</summary>
        public bool TryDetect(Transform candidate, HealthController health)
        {
            if (Profile == null || candidate == null) return false;
            if (health != null && !health.IsAlive) { Forget(candidate); return false; }

            Vector3 toTarget = candidate.position - _eyes.position;
            float distance = toTarget.magnitude;
            if (distance > Profile.ViewDistance) return false;

            float angle = Vector3.Angle(_eyes.forward, toTarget.normalized);
            float halfFov = Profile.FieldOfView * 0.5f;
            float distanceFactor = 1f - Mathf.Clamp01(distance / Profile.ViewDistance);

            // peripheral awareness: closer targets are noticed even outside the cone
            float tolerance = halfFov + distanceFactor * 35f;
            if (angle > tolerance) return false;

            RaycastHit hit;
            if (Physics.Raycast(_eyes.position, toTarget.normalized, out hit, distance, Blockers, QueryTriggerInteraction.Ignore))
            {
                HealthController hitHealth = hit.collider.GetComponentInParent<HealthController>();
                if (hitHealth == null || hitHealth != health) return false;
            }

            float gain = (1f - distanceFactor * 0.6f) * (1f - angle / Mathf.Max(1f, tolerance)) * 2.4f;
            _attention = Mathf.Clamp01(_attention + gain * Time.deltaTime);

            if (_attention > 0.25f)
            {
                Target = candidate;
                LastKnownPosition = candidate.position;
                HasTarget = true;
                _memoryTimer = Profile.MemoryTime;
                return true;
            }
            return false;
        }

        public void Forget(Transform candidate)
        {
            if (Target == candidate)
            {
                Target = null;
                _attention = 0f;
                _memoryTimer = 0f;
                HasTarget = false;
            }
        }
    }
}
