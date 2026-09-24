using UnityEditor;
using UnityEngine;
using EXFIL.Characters;
using EXFIL.Combat;
using EXFIL.Player;

namespace EXFILEditor
{
    /// <summary>Builds the player prefab and the item database asset.</summary>
    public static class PrefabFactory
    {
        public static GameObject BuildPlayerPrefab()
        {
            const string path = "Assets/Prefabs/Player.prefab";
            System.IO.Directory.CreateDirectory("Assets/Prefabs");

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject root = new GameObject("Player");
            root.tag = "Player";
            root.layer = LayerMask.NameToLayer("Default");

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            // body transform (yaw) -> head (pitch) -> camera
            GameObject head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(head.transform, false);
            cameraObject.transform.localPosition = Vector3.zero;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.05f;
            camera.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();

            // view model root in front of the camera
            GameObject viewModelRoot = new GameObject("ViewModelRoot");
            viewModelRoot.transform.SetParent(cameraObject.transform, false);
            viewModelRoot.transform.localPosition = Vector3.zero;

            GameObject handHandler = new GameObject("Hands");
            handHandler.transform.SetParent(cameraObject.transform, false);

            // components
            FpsController motor = root.AddComponent<FpsController>();
            motor.Head = head.transform;

            MouseLook look = root.AddComponent<MouseLook>();
            look.Body = root.transform;
            look.Head = head.transform;

            InteractionSystem interaction = root.AddComponent<InteractionSystem>();
            interaction.AimOrigin = cameraObject.transform;

            HealthController health = root.AddComponent<HealthController>();
            SkillSet skills = root.AddComponent<SkillSet>();
            ArmorController armor = root.AddComponent<ArmorController>();

            WeaponHandler primary = handHandler.AddComponent<WeaponHandler>();
            WeaponHandler secondary = handHandler.AddComponent<WeaponHandler>();
            WeaponHandler holster = handHandler.AddComponent<WeaponHandler>();

            EquipmentController equipment = root.AddComponent<EquipmentController>();
            equipment.Body = null;                       // filled when a character body is spawned
            equipment.ArmorCoverage = armor;
            equipment.Motor = motor;
            equipment.PrimaryHandler = primary;
            equipment.SecondaryHandler = secondary;
            equipment.HolsterHandler = holster;

            PlayerLoadout loadout = root.AddComponent<PlayerLoadout>();
            loadout.PrimaryHandler = primary;
            loadout.SecondaryHandler = secondary;
            loadout.HolsterHandler = holster;
            loadout.Equipment = equipment;
            loadout.Health = health;
            loadout.Skills = skills;

            FirstPersonWeapon viewModel = cameraObject.AddComponent<FirstPersonWeapon>();
            viewModel.ViewModelRoot = viewModelRoot.transform;
            viewModel.ViewCamera = camera;

            PlayerActor actor = root.AddComponent<PlayerActor>();
            actor.Motor = motor;
            actor.Look = look;
            actor.Interaction = interaction;
            actor.Loadout = loadout;
            actor.ViewModel = viewModel;
            actor.Health = health;
            actor.Skills = skills;
            actor.ViewCamera = camera;

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.spatialBlend = 0f;
            audio.playOnAwake = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[EXFIL] Player prefab created at " + path);
            return prefab;
        }

        public static ItemDatabase BuildItemDatabase()
        {
            const string path = "Assets/Resources/Data/ItemDatabase.asset";
            System.IO.Directory.CreateDirectory("Assets/Resources/Data");

            ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(database, path);
            }

            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition");
            int count = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                ItemDefinition def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (def == null) continue;
                database.Add(def);
                count++;
            }
            database.Rebuild();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log("[EXFIL] Item database rebuilt with " + count + " items.");
            return database;
        }
    }
}
