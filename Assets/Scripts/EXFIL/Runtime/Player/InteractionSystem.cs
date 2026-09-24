using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Player
{
    /// <summary>Raycast based interaction with a hold-to-use progress bar.</summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Ray")]
        public Transform AimOrigin;
        public float Range = 2.6f;
        public LayerMask Mask = ~0;
        public float SphereRadius = 0.08f;

        [Header("Runtime")]
        public float Progress;
        public IInteractable Current { get; private set; }
        public string CurrentPrompt { get; private set; }

        private readonly List<IInteractable> _hits = new List<IInteractable>();
        private float _holdTime;

        private void Update()
        {
            if (AimOrigin == null) AimOrigin = transform;
            Current = FindTarget();
            CurrentPrompt = Current != null ? Current.GetPrompt(gameObject) : string.Empty;
        }

        public IInteractable FindTarget()
        {
            _hits.Clear();
            RaycastHit[] hits = Physics.SphereCastAll(AimOrigin.position, SphereRadius, AimOrigin.forward, Range, Mask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            IInteractable bestTarget = null;

            for (int i = 0; i < hits.Length; i++)
            {
                IInteractable interactable = hits[i].collider.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract(gameObject)) continue;
                if (hits[i].distance >= best) continue;
                best = hits[i].distance;
                bestTarget = interactable;
            }
            return bestTarget;
        }

        /// <summary>Called every frame with the raw interact button state.</summary>
        public void Tick(bool interactHeld, bool interactPressed)
        {
            if (Current == null)
            {
                Progress = 0f;
                _holdTime = 0f;
                return;
            }

            float duration = Mathf.Max(0f, Current.GetInteractTime(gameObject));
            if (duration <= 0.01f)
            {
                if (interactPressed) Current.Interact(gameObject);
                Progress = 0f;
                return;
            }

            if (interactHeld)
            {
                _holdTime += Time.deltaTime;
                Progress = Mathf.Clamp01(_holdTime / duration);
                if (_holdTime >= duration)
                {
                    Current.Interact(gameObject);
                    _holdTime = 0f;
                    Progress = 0f;
                }
            }
            else
            {
                _holdTime = 0f;
                Progress = 0f;
            }
        }
    }
}
