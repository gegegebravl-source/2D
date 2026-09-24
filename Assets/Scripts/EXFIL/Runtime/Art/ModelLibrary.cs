using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Art
{
    /// <summary>Marker placed on every generated prop prefab so the runtime knows what it is.</summary>
    public class PropInfo : MonoBehaviour
    {
        public string Key;
        public string Category;
        public string[] Tags = new string[0];
        public Vector3 Size = Vector3.one;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            for (int i = 0; i < Tags.Length; i++)
                if (string.Equals(Tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    [Serializable]
    public class ModelEntry
    {
        public string Key;
        public string Category;         // prop / weapon / character
        public string[] Tags = new string[0];
        public GameObject Prefab;
        public Vector3 Size = Vector3.one;
        public Vector3 BoundsMin = Vector3.zero;
        public string Source;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            for (int i = 0; i < Tags.Length; i++)
                if (string.Equals(Tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>
    /// Catalogue of every CC0 model imported by the art pipeline. Generated into
    /// Assets/Resources/Data/ModelLibrary.asset by EXFIL/Setup/Run full setup.
    /// Everything degrades gracefully to primitive stand-ins when empty.
    /// </summary>
    public class ModelLibrary : ScriptableObject
    {
        public List<ModelEntry> Entries = new List<ModelEntry>();

        private static ModelLibrary _instance;
        private static bool _searched;
        private Dictionary<string, ModelEntry> _index;

        public static ModelLibrary Instance
        {
            get
            {
                if (_instance == null && !_searched)
                {
                    _searched = true;
                    _instance = Resources.Load<ModelLibrary>("Data/ModelLibrary");
                }
                return _instance;
            }
        }

        public static void ResetCache()
        {
            _instance = null;
            _searched = false;
        }

        public static bool HasModels { get { return Instance != null && Instance.Entries.Count > 0; } }

        private void BuildIndex()
        {
            if (_index != null) return;
            _index = new Dictionary<string, ModelEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Entries.Count; i++)
                if (!string.IsNullOrEmpty(Entries[i].Key)) _index[Entries[i].Key] = Entries[i];
        }

        public static ModelEntry Find(string key)
        {
            ModelLibrary lib = Instance;
            if (lib == null || string.IsNullOrEmpty(key)) return null;
            lib.BuildIndex();
            ModelEntry entry;
            if (lib._index.TryGetValue(key, out entry)) return entry;
            // tolerant lookup: suffix match ("kf_blaster" <- "blaster")
            for (int i = 0; i < lib.Entries.Count; i++)
            {
                ModelEntry e = lib.Entries[i];
                if (e.Key != null && e.Key.EndsWith(key, StringComparison.OrdinalIgnoreCase)) return e;
            }
            return null;
        }

        public static GameObject Get(string key)
        {
            ModelEntry entry = Find(key);
            return entry != null ? entry.Prefab : null;
        }

        public static Vector3 SizeOf(string key)
        {
            ModelEntry entry = Find(key);
            return entry != null ? entry.Size : Vector3.one;
        }

        public static List<ModelEntry> ByTag(string tag)
        {
            List<ModelEntry> result = new List<ModelEntry>();
            ModelLibrary lib = Instance;
            if (lib == null) return result;
            for (int i = 0; i < lib.Entries.Count; i++)
                if (lib.Entries[i].HasTag(tag)) result.Add(lib.Entries[i]);
            return result;
        }

        public static ModelEntry Pick(string tag, System.Random rng)
        {
            List<ModelEntry> list = ByTag(tag);
            if (list.Count == 0) return null;
            int index = rng != null ? rng.Next(list.Count) : UnityEngine.Random.Range(0, list.Count);
            return list[index];
        }

        /// <summary>Spawns a prop, optionally rescaled so its largest dimension equals targetMaxDim.</summary>
        public static GameObject Spawn(string key, Transform parent, Vector3 position, Quaternion rotation,
                                       float targetMaxDim = 0f, string name = null)
        {
            ModelEntry entry = Find(key);
            if (entry == null || entry.Prefab == null) return null;
            GameObject go = Instantiate(entry.Prefab, position, rotation, parent);
            Vector3 size = entry.Size;
            float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (targetMaxDim > 0f && max > 0.0001f)
                go.transform.localScale = Vector3.one * (targetMaxDim / max);
            if (!string.IsNullOrEmpty(name)) go.name = name;
            return go;
        }

        public static GameObject SpawnTagged(string tag, Transform parent, Vector3 position, Quaternion rotation,
                                             float targetMaxDim = 0f, System.Random rng = null)
        {
            ModelEntry entry = Pick(tag, rng);
            if (entry == null) return null;
            return Spawn(entry.Key, parent, position, rotation, targetMaxDim, entry.Key);
        }
    }
}
