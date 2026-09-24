using UnityEditor;
using UnityEngine;

namespace EXFILEditor
{
    /// <summary>One-click project bootstrap from the Unity menu.</summary>
    public static class EXFILSetup
    {
        [MenuItem("EXFIL/Setup/Run full setup (one click)", false, 1)]
        public static void RunFullSetup()
        {
            if (!EditorUtility.DisplayDialog("EXFIL setup",
                    "This will generate all content, the item database, the player prefab and both scenes.\n\n" +
                    "Existing assets are kept as they are. Continue?", "Do it", "Cancel"))
                return;

            ContentSeeder.SeedAll();
            PrefabFactory.BuildItemDatabase();
            PrefabFactory.BuildPlayerPrefab();
            AssetDatabase.SaveAssets();
            SceneBuilder.BuildHideoutScene();
            SceneBuilder.BuildRaidScene();

            Debug.Log("[EXFIL] Setup complete. Open Assets/Scenes/Hideout.unity and press Play.");
        }

        [MenuItem("EXFIL/Setup/1 - Seed content (items, operators, modules, recipes)", false, 20)]
        public static void SeedContent()
        {
            ContentSeeder.SeedAll();
        }

        [MenuItem("EXFIL/Setup/2 - Rebuild item database", false, 21)]
        public static void RebuildDatabase()
        {
            PrefabFactory.BuildItemDatabase();
        }

        [MenuItem("EXFIL/Setup/3 - Create player prefab", false, 22)]
        public static void CreatePlayer()
        {
            PrefabFactory.BuildPlayerPrefab();
        }

        [MenuItem("EXFIL/Setup/4 - Build hideout scene", false, 23)]
        public static void BuildHideout()
        {
            SceneBuilder.BuildHideoutScene();
        }

        [MenuItem("EXFIL/Setup/5 - Build raid scene", false, 24)]
        public static void BuildRaid()
        {
            SceneBuilder.BuildRaidScene();
        }

        [MenuItem("EXFIL/Setup/6 - Install URP (recommended)", false, 40)]
        public static void InstallUrp()
        {
            UnityEditor.PackageManager.Client.Add("com.unity.render-pipelines.universal");
            Debug.Log("[EXFIL] URP install started. After it finishes use EXFIL/Setup/Apply URP to graphics settings.");
        }

        [MenuItem("EXFIL/Setup/Apply URP to graphics settings", false, 41)]
        public static void ApplyUrp()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[EXFIL] No URP asset found. Create one via Assets > Create > Rendering > URP Asset.");
                return;
            }
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (asset != null)
            {
                UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = asset as UnityEngine.Rendering.RenderPipelineAsset;
                QualitySettings.renderPipeline = asset as UnityEngine.Rendering.RenderPipelineAsset;
                Debug.Log("[EXFIL] URP assigned: " + asset.name);
            }
        }

        [MenuItem("EXFIL/Setup/Install AI Navigation (NavMesh components)", false, 42)]
        public static void InstallNavigation()
        {
            UnityEditor.PackageManager.Client.Add("com.unity.ai.navigation");
        }
    }
}
