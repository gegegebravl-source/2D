using UnityEditor;
using UnityEngine;
using EXFIL.Art;

namespace EXFILEditor
{
    /// <summary>One-click project bootstrap from the Unity menu.</summary>
    public static class EXFILSetup
    {
        [MenuItem("EXFIL/Setup/Run full setup (one click)", false, 1)]
        public static void RunFullSetup()
        {
            if (!EditorUtility.DisplayDialog("EXFIL setup",
                    "This will:\n" +
                    "1. import the CC0 3D models into Unity assets (meshes, materials, AnimationClips, prefabs)\n" +
                    "2. seed all content (items, operators, modules, recipes, quests)\n" +
                    "3. build the item database and the player prefab\n" +
                    "4. build the Hideout and Raid scenes dressed with real models and a baked NavMesh\n\n" +
                    "This can take a couple of minutes on the first run. Continue?", "Do it", "Cancel"))
                return;

            // 1. art first: everything else wires itself to the imported models
            ModelLibrary library = AssetPipeline.Build(true);

            // 2. content
            ContentSeeder.SeedAll();
            PrefabFactory.BuildItemDatabase();
            PrefabFactory.BuildPlayerPrefab();
            AssetDatabase.SaveAssets();

            // 3. wire the art into items, plants and operators
            if (library != null) ArtWiring.WireAll();

            // 4. scenes
            SceneBuilder.BuildHideoutScene();
            SceneBuilder.BuildRaidScene();

            Debug.Log("[EXFIL] Setup complete. Open Assets/Scenes/Hideout.unity and press Play.");
        }

        [MenuItem("EXFIL/Setup/0 - Import 3D models (CC0 art pipeline)", false, 19)]
        public static void ImportArt()
        {
            ModelLibrary library = AssetPipeline.Build(true);
            if (library != null) ArtWiring.WireAll();
            Debug.Log("[EXFIL] Art import finished: " + (library != null ? library.Entries.Count : 0) + " models.");
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
