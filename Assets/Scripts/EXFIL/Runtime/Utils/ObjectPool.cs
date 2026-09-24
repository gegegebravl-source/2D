using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Utils
{
    /// <summary>Minimal GameObject pool for projectiles, impacts and decals.</summary>
    public sealed class ObjectPool : MonoBehaviour
    {
        [System.Serializable]
        public class PoolEntry
        {
            public string Key;
            public GameObject Prefab;
            public int Prewarm = 16;
        }

        [SerializeField] private List<PoolEntry> _entries = new List<PoolEntry>();

        private readonly Dictionary<string, Queue<GameObject>> _free = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, PoolEntry> _prefabs = new Dictionary<string, PoolEntry>();
        private readonly Dictionary<GameObject, string> _busy = new Dictionary<GameObject, string>();

        private void Awake()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                PoolEntry entry = _entries[i];
                if (string.IsNullOrEmpty(entry.Key) || entry.Prefab == null) continue;
                _prefabs[entry.Key] = entry;
                Queue<GameObject> queue = new Queue<GameObject>();
                for (int p = 0; p < entry.Prewarm; p++)
                    queue.Enqueue(CreateInstance(entry.Key));
                _free[entry.Key] = queue;
            }
        }

        private GameObject CreateInstance(string key)
        {
            PoolEntry entry = _prefabs[key];
            GameObject go = Instantiate(entry.Prefab, transform);
            go.SetActive(false);
            return go;
        }

        public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
        {
            Queue<GameObject> queue;
            if (!_free.TryGetValue(key, out queue))
            {
                if (!_prefabs.ContainsKey(key)) return null;
                queue = new Queue<GameObject>();
                _free[key] = queue;
            }

            GameObject go = queue.Count > 0 ? queue.Dequeue() : CreateInstance(key);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            _busy[go] = key;
            return go;
        }

        public void Despawn(GameObject go)
        {
            if (go == null) return;
            string key;
            if (!_busy.TryGetValue(go, out key)) return;
            _busy.Remove(go);
            go.SetActive(false);
            _free[key].Enqueue(go);
        }
    }
}
