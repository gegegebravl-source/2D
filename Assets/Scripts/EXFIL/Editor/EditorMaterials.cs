using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EXFILEditor
{
    /// <summary>
    /// Materials used by procedurally built scenes. They are written as real assets under
    /// Assets/Generated/Materials so saved scenes never lose their references (a plain
    /// "new Material(...)" would end up as a missing reference in the .unity file).
    /// </summary>
    public static class EditorMaterials
    {
        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();
        private const string Root = "Assets/Generated/Materials";

        public static Material Get(string name, Color color, float smoothness = 0.08f, float metallic = 0f)
        {
            string key = name + "|" + color.ToString("F3");
            Material cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            if (!Directory.Exists(Root)) Directory.CreateDirectory(Root);
            string path = Root + "/" + Sanitise(name) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetPipeline.PickShader());
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = Sanitise(name);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            _cache[key] = material;
            return material;
        }

        private static string Sanitise(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++) name = name.Replace(invalid[i], '_');
            return name.Replace(' ', '_');
        }
    }
}
