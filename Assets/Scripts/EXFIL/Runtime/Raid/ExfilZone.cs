using System.Collections;
using UnityEngine;
using EXFIL.Core;
using EXFIL.Player;

namespace EXFIL.Raid
{
    /// <summary>Extraction point: stand inside for ExtractTime seconds to leave the raid.</summary>
    public class ExfilZone : MonoBehaviour
    {
        public string ExitName = "Exit";
        public float Radius = 3f;
        public float ExtractTime = 6f;
        public bool IsOpen = true;
        public bool RequiresItem;
        public string RequiredItemId;

        private readonly List<GameObject> _inside = new List<GameObject>();
        private readonly Dictionary<GameObject, Coroutine> _timers = new Dictionary<GameObject, Coroutine>();

        private void OnTriggerEnter(Collider other)
        {
            PlayerActor player = other.GetComponentInParent<PlayerActor>();
            if (player == null || _inside.Contains(player.gameObject)) return;
            if (!IsOpen || !PlayerHasRequirement(player)) return;

            _inside.Add(player.gameObject);
            _timers[player.gameObject] = StartCoroutine(Countdown(player));
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerActor player = other.GetComponentInParent<PlayerActor>();
            if (player == null || !_inside.Contains(player.gameObject)) return;

            _inside.Remove(player.gameObject);
            Coroutine routine;
            if (_timers.TryGetValue(player.gameObject, out routine) && routine != null)
                StopCoroutine(routine);
            _timers.Remove(player.gameObject);
        }

        private bool PlayerHasRequirement(PlayerActor player)
        {
            if (!RequiresItem || string.IsNullOrEmpty(RequiredItemId)) return true;
            PlayerLoadout loadout = player.GetComponent<PlayerLoadout>();
            return loadout != null && loadout.Inventory != null && loadout.Inventory.CountOf(RequiredItemId) > 0;
        }

        private IEnumerator Countdown(PlayerActor player)
        {
            float elapsed = 0f;
            while (elapsed < ExtractTime)
            {
                if (!IsOpen) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= ExtractTime)
                RaidManager.Instance?.ExtractPlayer(player, ExitName);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsOpen ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}
