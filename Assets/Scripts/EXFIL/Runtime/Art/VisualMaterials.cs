using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EXFIL.Art
{
    /// <summary>
    /// Material factory that works under both the Built-in pipeline and URP.
    /// Anything that builds geometry at runtime (placeholder bodies, gear stubs,
    /// loot props, primitive fallbacks) must go through here so nothing renders
    /// magenta when URP is installed.
    /// </summary>
    public static class VisualMaterials
    {
        private static Shader _litShader;
        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        public static Shader LitShader
        {
            get
            {
                if (_litShader != null) return _litShader;
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    _litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (_litShader == null) _litShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                }
                if (_litShader == null) _litShader = Shader.Find("Standard");
                if (_litShader == null) _litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (_litShader == null) _litShader = Shader.Find("Legacy Shaders/Diffuse");
                if (_litShader == null) _litShader = Shader.Find("Sprites/Default");
                return _litShader;
            }
        }

        /// <summary>Cached opaque material for a colour (one material per colour+finish).</summary>
        public static Material Solid(string key, Color color, float smoothness = 0.12f, float metallic = 0f)
        {
            string cacheKey = key + "|" + color.ToString("F3") + "|" + smoothness.ToString("F2");
            Material cached;
            if (_cache.TryGetValue(cacheKey, out cached) && cached != null) return cached;

            Material material = new Material(LitShader);
            material.name = "EXFIL_" + key;
            Apply(material, color, smoothness, metallic);
            _cache[cacheKey] = material;
            return material;
        }

        public static Material Textured(Texture2D texture, string key = "tex")
        {
            string cacheKey = key + "|" + (texture != null ? texture.GetInstanceID().ToString() : "null");
            Material cached;
            if (_cache.TryGetValue(cacheKey, out cached) && cached != null) return cached;
            Material material = Solid(key + "_base", Color.white, 0.08f);
            if (texture != null)
            {
                material = new Material(LitShader);
                material.name = "EXFIL_" + key;
                Apply(material, Color.white, 0.08f, 0f);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            }
            _cache[cacheKey] = material;
            return material;
        }

        public static void Apply(Material material, Color color, float smoothness, float metallic)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        }

        /// <summary>Gives a renderer sensible shadows regardless of pipeline.</summary>
        public static void Style(Renderer renderer, bool castShadows = true, bool receiveShadows = true)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
        }
    }
}
