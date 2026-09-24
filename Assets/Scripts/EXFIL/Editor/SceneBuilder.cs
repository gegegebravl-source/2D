using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EXFIL.AI;
using EXFIL.Core;
using EXFIL.Hideout;
using EXFIL.Items;
using EXFIL.Player;
using EXFIL.Raid;

namespace EXFILEditor
{
    /// <summary>Procedurally builds the two playable scenes: the bunker hideout and a raid map.</summary>
    public static class SceneBuilder
    {
        // ------------------------------------------------------------- primitives
        private static Material Mat(string name, Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.name = name;
            return material;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            if (!collider)
            {
                Collider existing = go.GetComponent<Collider>();
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            }
            return go;
        }

        private static GameObject Plane(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go;
        }

        private static GameObject Lamp(string name, Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            return go;
        }

        private static void BakeNavMesh()
        {
            try
            {
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EXFIL] NavMesh bake skipped: " + e.Message);
            }
        }

        // ---------------------------------------------------------------- hideout
        public static void BuildHideoutScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material concrete = Mat("Concrete", new Color(0.42f, 0.43f, 0.44f));
            Material darkConcrete = Mat("DarkConcrete", new Color(0.26f, 0.27f, 0.28f));
            Material metal = Mat("Metal", new Color(0.32f, 0.34f, 0.36f));
            Material wood = Mat("Wood", new Color(0.40f, 0.28f, 0.16f));
            Material soilMat = Mat("Soil", new Color(0.24f, 0.17f, 0.11f));
            Material plantMat = Mat("Plant", new Color(0.24f, 0.52f, 0.22f));

            GameObject root = new GameObject("Bunker");

            // ---- corridor shell: 40m long, 6m wide, 3.2m high
            float length = 40f, width = 6f, height = 3.2f;
            Plane("Floor", root.transform, new Vector3(0f, 0f, 0f), new Vector3(length / 10f, 1f, width / 10f), concrete);
            Box("Ceiling", root.transform, new Vector3(0f, height, 0f), new Vector3(length, 0.2f, width), darkConcrete);
            Box("Wall_North", root.transform, new Vector3(0f, height / 2f, width / 2f), new Vector3(length, height, 0.3f), concrete);
            Box("Wall_South", root.transform, new Vector3(0f, height / 2f, -width / 2f), new Vector3(length, height, 0.3f), concrete);
            Box("Wall_West", root.transform, new Vector3(-length / 2f, height / 2f, 0f), new Vector3(0.3f, height, width), concrete);
            Box("Wall_East", root.transform, new Vector3(length / 2f, height / 2f, 0f), new Vector3(0.3f, height, width), concrete);

            // ---- rooms off the corridor (alcoves along the north wall)
            float[] roomZ = { -14f, -7f, 0f, 7f, 14f };
            string[] roomNames = { "Entrance", "StashRoom", "Workshop", "Medbay", "RestRoom" };

            for (int i = 0; i < roomZ.Length; i++)
            {
                GameObject room = new GameObject("Room_" + roomNames[i]);
                room.transform.SetParent(root.transform, false);
                room.transform.localPosition = new Vector3(roomZ[i], 0f, 6f);

                // alcove: 8 x 6 metres behind the corridor wall
                Plane("Floor", room.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 1f, 0.6f), darkConcrete);
                Box("Wall_Left", room.transform, new Vector3(-4f, height / 2f, 0f), new Vector3(0.3f, height, 6f), concrete);
                Box("Wall_Right", room.transform, new Vector3(4f, height / 2f, 0f), new Vector3(0.3f, height, 6f), concrete);
                Box("Wall_Back", room.transform, new Vector3(0f, height / 2f, 3f), new Vector3(8f, height, 0.3f), concrete);
                Box("Ceiling", room.transform, new Vector3(0f, height, 0f), new Vector3(8f, 0.2f, 6f), darkConcrete);
                Lamp("Lamp", room.transform, new Vector3(0f, height - 0.5f, 0f), new Color(1f, 0.94f, 0.82f), 1.4f, 12f);
            }

            // ---- greenhouse at the east end: glass room with 6 beds
            GameObject greenhouse = new GameObject("Greenhouse");
            greenhouse.transform.SetParent(root.transform, false);
            greenhouse.transform.localPosition = new Vector3(17.5f, 0f, 0f);

            Plane("Floor", greenhouse.transform, new Vector3(0f, 0.02f, 0f), new Vector3(1.2f, 1f, 0.7f), soilMat);
            for (int w = 0; w < 4; w++)
            {
                float angle = w * 90f;
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 5.5f, height / 2f + 0.8f, Mathf.Sin(angle * Mathf.Deg2Rad) * 3f);
                Box("Glass", greenhouse.transform, offset, new Vector3(11f, 3.6f, 0.12f),
                    Mat("Glass", new Color(0.55f, 0.72f, 0.65f)), true).GetComponent<Renderer>().sharedMaterial.color = new Color(0.6f, 0.8f, 0.7f, 0.35f);
            }

            GreenhouseController controller = greenhouse.AddComponent<GreenhouseController>();
            List<Light> growLights = new List<Light>();

            for (int bed = 0; bed < 6; bed++)
            {
                float x = -3.5f + (bed % 3) * 3.5f;
                float z = -1.4f + (bed / 3) * 2.8f;

                GameObject bedObject = new GameObject("Bed_" + (bed + 1));
                bedObject.transform.SetParent(greenhouse.transform, false);
                bedObject.transform.localPosition = new Vector3(x, 0.35f, z);

                GameObject soil = Box("Soil", bedObject.transform, Vector3.zero, new Vector3(2.8f, 0.4f, 1.6f), wood);
                MeshRenderer soilRenderer = soil.GetComponent<MeshRenderer>();
                if (soilRenderer != null) soilRenderer.sharedMaterial = soilMat;

                GameObject bedRoot = new GameObject("GrowthAnchor");
                bedRoot.transform.SetParent(bedObject.transform, false);
                bedRoot.transform.localPosition = new Vector3(0f, 0.2f, 0f);

                GardenPlot plot = bedObject.AddComponent<GardenPlot>();
                plot.PlotId = "plot_" + (bed + 1);
                plot.GrowthAnchor = bedRoot.transform;
                plot.SoilRenderer = soilRenderer;
                plot.Unlocked = bed < 2;                 // level 1 greenhouse = 2 plots
                controller.Plots.Add(plot);

                GameObject lampObject = Lamp("GrowLight", bedObject.transform, new Vector3(0f, 1.6f, 0f), new Color(0.85f, 0.55f, 1f), 1.2f, 4f);
                growLights.Add(lampObject.GetComponent<Light>());
            }
            controller.GrowLights = growLights.ToArray();
            controller.WaterTankLitres = 20f;

            // ---- props per room
            CreateStations(root.transform, metal, wood, roomZ);

            // ---- loot / ambience props: crates and barrels along the corridor
            for (int i = 0; i < 10; i++)
            {
                float x = -18f + i * 3.6f;
                Box("Crate_" + i, root.transform, new Vector3(x, 0.4f, -2.1f), new Vector3(0.9f, 0.8f, 0.9f), wood);
                if (i % 3 == 0)
                    Box("Barrel_" + i, root.transform, new Vector3(x + 1.4f, 0.55f, -2.0f), new Vector3(0.7f, 1.1f, 0.7f),
                        Mat("Barrel", new Color(0.32f, 0.38f, 0.30f)));
            }

            // ---- systems
            GameObject systems = new GameObject("Systems");
            HideoutManager hideout = systems.AddComponent<HideoutManager>();
            LoadModules(hideout);

            GameObject sessionObject = new GameObject("GameSession");
            Meta.GameSession session = sessionObject.AddComponent<Meta.GameSession>();
            LoadCharacters(session);
            LoadTraders(sessionObject);

            GameObject uiObject = new GameObject("GameUI");
            uiObject.AddComponent<EXFIL.UI.GameUI>();

            GameObject bootstrapObject = new GameObject("GameBootstrap");
            Meta.GameBootstrap bootstrap = bootstrapObject.AddComponent<Meta.GameBootstrap>();
            bootstrap.ItemDatabaseAsset = ItemDatabase.Instance;
            bootstrap.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");

            GameObject audioObject = new GameObject("AudioManager");
            audioObject.AddComponent<EXFIL.Audio.AudioManager>();

            // player spawn
            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(-18f, 1.1f, 0f);
            spawn.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // ambient light
            GameObject sun = new GameObject("Sun");
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 0.35f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.10f);
            RenderSettings.fogDensity = 0.035f;

            BakeNavMesh();

            string path = "Assets/Scenes/Hideout.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, path);
            AddSceneToBuildSettings(path, 0);
            Debug.Log("[EXFIL] Hideout scene built at " + path);
        }

        private static void CreateStations(Transform root, Material metal, Material wood, float[] roomZ)
        {
            // stash
            GameObject stash = Box("Stash_Locker", root.transform, new Vector3(roomZ[1], 0.9f, 7.4f), new Vector3(2.4f, 1.8f, 0.7f), metal);
            HideoutStation stashStation = stash.AddComponent<HideoutStation>();
            stashStation.Kind = StationKind.Stash;
            stashStation.StationName = "Stash";
            stashStation.LinkedModuleId = "stash";

            // workbench
            GameObject bench = Box("Workbench", root.transform, new Vector3(roomZ[2], 0.45f, 7.4f), new Vector3(3.2f, 0.9f, 0.9f), wood);
            HideoutStation benchStation = bench.AddComponent<HideoutStation>();
            benchStation.Kind = StationKind.Workbench;
            benchStation.StationName = "Workbench";
            benchStation.LinkedModuleId = "workbench";

            // medstation
            GameObject med = Box("Medstation", root.transform, new Vector3(roomZ[3], 0.5f, 7.4f), new Vector3(1.6f, 1.0f, 0.8f), metal);
            HideoutStation medStation = med.AddComponent<HideoutStation>();
            medStation.Kind = StationKind.Medstation;
            medStation.StationName = "Medstation";
            medStation.LinkedModuleId = "medstation";

            // nutrition unit
            GameObject nutrition = Box("NutritionUnit", root.transform, new Vector3(roomZ[3] + 2f, 0.5f, 7.4f), new Vector3(1.4f, 1.0f, 0.8f), metal);
            HideoutStation foodStation = nutrition.AddComponent<HideoutStation>();
            foodStation.Kind = StationKind.NutritionUnit;
            foodStation.StationName = "Nutrition unit";
            foodStation.LinkedModuleId = "nutrition_unit";

            // lavatory
            GameObject lavatory = Box("Lavatory", root.transform, new Vector3(roomZ[4] - 2.4f, 0.4f, 7.4f), new Vector3(1.0f, 0.8f, 0.8f), metal);
            HideoutStation lavatoryStation = lavatory.AddComponent<HideoutStation>();
            lavatoryStation.Kind = StationKind.Lavatory;
            lavatoryStation.StationName = "Lavatory";
            lavatoryStation.LinkedModuleId = "lavatory";

            // generator (west end)
            GameObject generator = Box("Generator", root.transform, new Vector3(-17.5f, 0.7f, 3.2f), new Vector3(2.2f, 1.4f, 1.6f),
                Mat("Generator", new Color(0.30f, 0.34f, 0.30f)));
            HideoutStation generatorStation = generator.AddComponent<HideoutStation>();
            generatorStation.Kind = StationKind.Generator;
            generatorStation.StationName = "Generator";
            generatorStation.LinkedModuleId = "generator";

            // water collector
            GameObject water = Box("WaterCollector", root.transform, new Vector3(-14f, 0.6f, 3.2f), new Vector3(1.2f, 1.2f, 1.2f), metal);
            HideoutStation waterStation = water.AddComponent<HideoutStation>();
            waterStation.Kind = StationKind.WaterCollector;
            waterStation.StationName = "Water collector";
            waterStation.LinkedModuleId = "water_collector";

            // greenhouse terminal
            GameObject terminal = Box("GreenhouseTerminal", root.transform, new Vector3(13.5f, 0.5f, 2.2f), new Vector3(0.8f, 1.0f, 0.6f), metal);
            HideoutStation greenhouseStation = terminal.AddComponent<HideoutStation>();
            greenhouseStation.Kind = StationKind.Greenhouse;
            greenhouseStation.StationName = "Greenhouse terminal";
            greenhouseStation.LinkedModuleId = "greenhouse";

            // bunk beds in the rest room
            for (int b = 0; b < 2; b++)
            {
                float y = 0.25f + b * 0.9f;
                Box("Bunk_" + b, root.transform, new Vector3(roomZ[4] + 2.2f, y, 8.2f), new Vector3(2.0f, 0.2f, 0.9f), wood);
            }

            // shooting range target wall at the far east
            Box("TargetWall", root.transform, new Vector3(19.4f, 1.2f, -2f), new Vector3(0.2f, 2.4f, 3f), metal);
        }

        // ------------------------------------------------------------------- raid
        public static void BuildRaidScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Material ground = Mat("Ground", new Color(0.22f, 0.26f, 0.20f));
            Material concrete = Mat("Concrete", new Color(0.44f, 0.44f, 0.42f));
            Material metal = Mat("Metal", new Color(0.30f, 0.32f, 0.34f));
            Material rust = Mat("Rust", new Color(0.42f, 0.24f, 0.16f));

            GameObject root = new GameObject("Map_Factory");

            Plane("Ground", root.transform, Vector3.zero, new Vector3(24f, 1f, 24f), ground);

            // perimeter walls
            float size = 120f;
            Box("Wall_N", root.transform, new Vector3(0f, 3f, size / 2f), new Vector3(size, 6f, 1f), concrete);
            Box("Wall_S", root.transform, new Vector3(0f, 3f, -size / 2f), new Vector3(size, 6f, 1f), concrete);
            Box("Wall_E", root.transform, new Vector3(size / 2f, 3f, 0f), new Vector3(1f, 6f, size), concrete);
            Box("Wall_W", root.transform, new Vector3(-size / 2f, 3f, 0f), new Vector3(1f, 6f, size), concrete);

            // main industrial building
            GameObject factory = new GameObject("Factory");
            factory.transform.SetParent(root.transform, false);
            factory.transform.localPosition = new Vector3(0f, 0f, 0f);
            Plane("Floor", factory.transform, new Vector3(0f, 0.02f, 0f), new Vector3(6f, 1f, 4f), concrete);
            for (int i = 0; i < 4; i++)
            {
                Box("Pillar_" + i, factory.transform, new Vector3(-18f + i * 12f, 3f, 12f), new Vector3(1.2f, 6f, 1.2f), concrete);
                Box("Pillar2_" + i, factory.transform, new Vector3(-18f + i * 12f, 3f, -12f), new Vector3(1.2f, 6f, 1.2f), concrete);
            }
            Box("Wall_Factory_N", factory.transform, new Vector3(0f, 4f, 20f), new Vector3(44f, 8f, 0.6f), rust);
            Box("Wall_Factory_S", factory.transform, new Vector3(0f, 4f, -20f), new Vector3(44f, 8f, 0.6f), rust);
            Box("Wall_Factory_E", factory.transform, new Vector3(22f, 4f, 0f), new Vector3(0.6f, 8f, 40f), rust);
            Box("Wall_Factory_W", factory.transform, new Vector3(-22f, 4f, 0f), new Vector3(0.6f, 8f, 40f), rust);
            Box("Roof", factory.transform, new Vector3(0f, 8.2f, 0f), new Vector3(44f, 0.4f, 40f), metal);

            // interior cover: machines, containers, sandbags
            for (int i = 0; i < 12; i++)
            {
                float x = -20f + (i % 6) * 8f;
                float z = (i / 6 == 0) ? 8f : -8f;
                Box("Machine_" + i, factory.transform, new Vector3(x, 1.1f, z), new Vector3(2.4f, 2.2f, 2.4f), metal);
                Box("Container_" + i, root.transform, new Vector3(x + 3f, 1.3f, z - 18f), new Vector3(6f, 2.6f, 2.6f), rust);
            }

            // systems
            GameObject systems = new GameObject("Systems");
            RaidManager raid = systems.AddComponent<RaidManager>();
            BotDirector director = systems.AddComponent<BotDirector>();
            raid.Director = director;
            LoadBotProfiles(director);

            // spawn points
            List<Transform> botSpawns = new List<Transform>();
            string[] botSpawnNames = { "BotSpawn_N", "BotSpawn_S", "BotSpawn_E", "BotSpawn_W", "BotSpawn_NE", "BotSpawn_SW" };
            Vector3[] botPositions =
            {
                new Vector3(0f, 0f, 45f), new Vector3(0f, 0f, -45f),
                new Vector3(45f, 0f, 0f), new Vector3(-45f, 0f, 0f),
                new Vector3(35f, 0f, 35f), new Vector3(-35f, 0f, -35f)
            };
            for (int i = 0; i < botSpawnNames.Length; i++)
            {
                GameObject spawn = new GameObject(botSpawnNames[i]);
                spawn.transform.position = botPositions[i];
                spawn.transform.SetParent(systems.transform, true);
                botSpawns.Add(spawn.transform);
            }
            director.SpawnPoints = botSpawns;

            // player spawns
            List<Transform> playerSpawns = new List<Transform>();
            Vector3[] playerPositions =
            {
                new Vector3(-50f, 1.1f, 50f), new Vector3(50f, 1.1f, -50f),
                new Vector3(-50f, 1.1f, -50f), new Vector3(50f, 1.1f, 50f)
            };
            for (int i = 0; i < playerPositions.Length; i++)
            {
                GameObject spawn = new GameObject("PlayerSpawn_" + (i + 1));
                spawn.transform.position = playerPositions[i];
                spawn.transform.SetParent(systems.transform, true);
                playerSpawns.Add(spawn.transform);
            }
            raid.PlayerSpawnPoints = playerSpawns.ToArray();
            raid.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");

            // exfils
            List<ExfilZone> exits = new List<ExfilZone>();
            (string, Vector3)[] exitData =
            {
                ("Railroad crossing", new Vector3(-52f, 0f, 0f)),
                ("Ruined gate", new Vector3(52f, 0f, 12f)),
                ("Sewer manhole", new Vector3(0f, 0f, -52f)),
                ("Old checkpoint", new Vector3(20f, 0f, 52f))
            };
            foreach ((string name, Vector3 position) in exitData)
            {
                GameObject zone = new GameObject("Exfil_" + name.Replace(" ", "_"));
                zone.transform.position = position;
                zone.transform.SetParent(systems.transform, true);
                SphereCollider collider = zone.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 3.2f;
                ExfilZone exfil = zone.AddComponent<ExfilZone>();
                exfil.ExitName = name;
                exfil.Radius = 3.2f;
                exfil.ExtractTime = 6f;
                exits.Add(exfil);
            }
            raid.Exits = exits.ToArray();

            // loot spawners
            List<LootSpawner> spawners = new List<LootSpawner>();
            for (int i = 0; i < 40; i++)
            {
                float angle = i * 37f * Mathf.Deg2Rad;
                float radius = 8f + (i % 5) * 7f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                GameObject spawnerObject = new GameObject("LootSpawner_" + i);
                spawnerObject.transform.position = position;
                spawnerObject.transform.SetParent(systems.transform, true);
                LootSpawner spawner = spawnerObject.AddComponent<LootSpawner>();
                spawner.ContainerName = i % 4 == 0 ? "Weapon crate" : (i % 4 == 1 ? "Medcase" : (i % 4 == 2 ? "Supply crate" : "Toolbox"));
                spawner.LootTableIds = new[] { "general", "medical", "weapons" };
                spawners.Add(spawner);
            }
            raid.LootSpawners = spawners.ToArray();

            // loot tables
            LootSpawner.RegisterTable("general", new List<string>
            {
                "barter_wires", "barter_bolts", "barter_screws", "barter_hose", "food_crackers", "food_water", "money_rouble"
            });
            LootSpawner.RegisterTable("medical", new List<string>
            {
                "med_bandage", "med_ifak", "med_painkiller", "med_salewa", "med_splint", "med_vaseline"
            });
            LootSpawner.RegisterTable("weapons", new List<string>
            {
                "wpn_aks74u", "wpn_makarov", "wpn_saiga12", "wpn_bizon", "mag_ak74_30", "ammo_545_ps", "ammo_762x39_ps"
            });

            // default raid settings
            RaidSettings settings = ScriptableObject.CreateInstance<RaidSettings>();
            settings.Id = "raid_factory";
            settings.DisplayName = "Factory district";
            settings.SceneName = "Raid";
            settings.DurationMinutes = 35f;
            settings.Waves = new List<BotWave>
            {
                new BotWave { ProfileId = "bot_scav", Count = 8, RespawnsOverTime = true, RespawnInterval = 150f },
                new BotWave { ProfileId = "bot_contractor", Count = 4, DelayAfterStart = 60f, RespawnsOverTime = true, RespawnInterval = 240f },
                new BotWave { ProfileId = "bot_sniper", Count = 2, DelayAfterStart = 180f },
                new BotWave { ProfileId = "bot_raider", Count = 3, DelayAfterStart = 420f }
            };
            settings.MaxBotsAlive = 14;
            settings.LootContainersToFill = 40;
            System.IO.Directory.CreateDirectory("Assets/Data");
            AssetDatabase.CreateAsset(settings, "Assets/Data/DefaultRaid.asset");

            // UI + bootstrap
            GameObject uiObject = new GameObject("GameUI");
            uiObject.AddComponent<EXFIL.UI.GameUI>();

            GameObject bootstrapObject = new GameObject("GameBootstrap");
            Meta.GameBootstrap bootstrap = bootstrapObject.AddComponent<Meta.GameBootstrap>();
            bootstrap.ItemDatabaseAsset = ItemDatabase.Instance;
            bootstrap.RaidSettingsOverride = settings;
            bootstrap.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");

            GameObject audioObject = new GameObject("AudioManager");
            audioObject.AddComponent<EXFIL.Audio.AudioManager>();

            // lighting: overcast dusk
            GameObject sun = new GameObject("Sun");
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.1f;
            sunLight.color = new Color(1f, 0.94f, 0.84f);
            sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.26f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.34f, 0.36f, 0.36f);
            RenderSettings.fogDensity = 0.012f;

            BakeNavMesh();

            string path = "Assets/Scenes/Raid.unity";
            EditorSceneManager.SaveScene(scene, path);
            AddSceneToBuildSettings(path, 1);
            Debug.Log("[EXFIL] Raid scene built at " + path);
        }

        // ------------------------------------------------------------------ utils
        private static void AddSceneToBuildSettings(string path, int index)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(Mathf.Min(index, scenes.Count), new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void LoadModules(HideoutManager hideout)
        {
            string[] guids = AssetDatabase.FindAssets("t:HideoutModuleDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                HideoutModuleDefinition def = AssetDatabase.LoadAssetAtPath<HideoutModuleDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (def != null && !hideout.Modules.Contains(def)) hideout.Modules.Add(def);
            }
        }

        private static void LoadCharacters(Meta.GameSession session)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                CharacterDefinition def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (def != null && !session.Characters.Contains(def)) session.Characters.Add(def);
            }
        }

        private static void LoadTraders(GameObject parent)
        {
            Economy.EconomyService service = parent.GetComponent<Economy.EconomyService>();
            if (service == null) service = parent.AddComponent<Economy.EconomyService>();

            string[] guids = AssetDatabase.FindAssets("t:TraderDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                Economy.TraderDefinition def = AssetDatabase.LoadAssetAtPath<Economy.TraderDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (def != null && !service.Traders.Contains(def)) service.Traders.Add(def);
            }
        }

        private static void LoadBotProfiles(BotDirector director)
        {
            string[] guids = AssetDatabase.FindAssets("t:BotProfile");
            for (int i = 0; i < guids.Length; i++)
            {
                BotProfile profile = AssetDatabase.LoadAssetAtPath<BotProfile>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (profile != null && !director.Profiles.Contains(profile)) director.Profiles.Add(profile);
            }
        }
    }
}
