using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EXFIL.Art;
using EXFIL.Core;
using EXFIL.Hideout;
using EXFIL.Raid;

namespace EXFILEditor
{
    /// <summary>
    /// Dresses the procedurally built scenes with the imported CC0 models.
    ///
    /// Strategy: the primitive shells (floors, walls, colliders) stay so the level is always
    /// walkable and never breaks - their renderers are switched off and real modules are
    /// placed on top of them. If the art pipeline has not run, decoration is skipped and the
    /// primitive scene is used as-is.
    /// </summary>
    public static class WorldDecorator
    {
        public enum ScaleMode
        {
            None,        // keep model scale
            MaxDim,      // largest dimension = target
            Height,      // vertical size = target (walls)
            Footprint,   // largest horizontal dimension = target (floors, bases)
        }

        private static System.Random _rng;

        public static bool ArtAvailable
        {
            get { return ModelLibrary.Instance != null && ModelLibrary.Instance.Entries.Count > 0; }
        }

        public static void Begin(int seed)
        {
            _rng = new System.Random(seed);
        }

        // ------------------------------------------------------------------ placing
        public static GameObject Place(string key, Transform parent, Vector3 position, float yaw,
                                       float target = 0f, ScaleMode mode = ScaleMode.None,
                                       float extraPitch = 0f, float extraRoll = 0f)
        {
            ModelEntry entry = ModelLibrary.Find(key);
            if (entry == null || entry.Prefab == null) return null;
            GameObject go = UnityEngine.Object.Instantiate(entry.Prefab, position,
                Quaternion.Euler(extraPitch, yaw, extraRoll), parent);
            go.name = key;
            if (target > 0f && mode != ScaleMode.None)
                go.transform.localScale = ScaleFor(entry.Size, target, mode);
            return go;
        }

        public static GameObject PlaceTag(string tag, Transform parent, Vector3 position, float yaw,
                                          float target = 0f, ScaleMode mode = ScaleMode.None)
        {
            return PlaceTag(tag, parent, position, yaw, target, mode, null);
        }

        public static GameObject PlaceTag(string tag, Transform parent, Vector3 position, float yaw,
                                          float target, ScaleMode mode, params string[] alternatives)
        {
            ModelEntry entry = ModelLibrary.Pick(tag, _rng);
            if (entry != null)
            {
                GameObject placed = Place(entry.Key, parent, position, yaw, target, mode);
                if (placed != null) return placed;
            }
            if (alternatives != null)
                for (int i = 0; i < alternatives.Length; i++)
                {
                    GameObject placed = Place(alternatives[i], parent, position, yaw, target, mode);
                    if (placed != null) return placed;
                }
            return null;
        }

        public static GameObject Fallback(string name, Transform parent, Vector3 position, Vector3 size,
                                          Color color, float yaw = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = EditorMaterials.Get("Primitive_" + name, color);
            return go;
        }

        public static GameObject PlaceOrFallback(string key, string tag, Transform parent, Vector3 position, float yaw,
                                                 float target, ScaleMode mode, Vector3 fallbackSize, Color fallbackColor)
        {
            GameObject placed = key != null ? Place(key, parent, position, yaw, target, mode) : null;
            if (placed == null && tag != null) placed = PlaceTag(tag, parent, position, yaw, target, mode);
            if (placed != null) return placed;
            return Fallback("Prim_" + (key ?? tag), parent, position, fallbackSize, fallbackColor, yaw);
        }

        private static Vector3 ScaleFor(Vector3 size, float target, ScaleMode mode)
        {
            if (target <= 0f || mode == ScaleMode.None) return Vector3.one;
            switch (mode)
            {
                case ScaleMode.Height:
                    return Vector3.one * (size.y > 0.0001f ? target / size.y : 1f);
                case ScaleMode.Footprint:
                {
                    float foot = Mathf.Max(size.x, size.z);
                    return Vector3.one * (foot > 0.0001f ? target / foot : 1f);
                }
                default:
                {
                    float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                    return Vector3.one * (max > 0.0001f ? target / max : 1f);
                }
            }
        }

        /// <summary>Disables only the renderer: colliders keep the level sealed.</summary>
        public static int HideRenderers(Transform root, params string[] namePrefixes)
        {
            int hidden = 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                string name = renderers[i].gameObject.name;
                for (int p = 0; p < namePrefixes.Length; p++)
                {
                    if (name.StartsWith(namePrefixes[p], StringComparison.OrdinalIgnoreCase))
                    {
                        renderers[i].enabled = false;
                        hidden++;
                        break;
                    }
                }
            }
            return hidden;
        }

        public static GameObject Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i].gameObject;
            return null;
        }

        /// <summary>
        /// Flags decorated props as NavigationStatic so the NavMesh bake treats crates,
        /// containers, modules and walls as obstacles instead of walkable ground.
        /// </summary>
        public static int MarkStaticObstacles(bool enabled)
        {
            if (!enabled) return 0;
            int marked = 0;
            GameObject[] roots = { GameObject.Find("Bunker"), GameObject.Find("Map_Factory") };
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == null) continue;
                GameObject decor = Find(roots[r].transform, "Decor");
                if (decor == null) continue;
                Renderer[] renderers = decor.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Bounds bounds = renderers[i].bounds;
                    float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                    if (footprint < 0.7f) continue;      // tiny clutter does not block navigation
                    GameObjectUtility.SetStaticEditorFlags(renderers[i].gameObject,
                        StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccludeeStatic);
                    marked++;
                }
            }
            return marked;
        }

        // =================================================================== HIDEOUT
        public static int DecorateHideout()
        {
            if (!ArtAvailable) return 0;
            Begin(20260924);
            GameObject rootObject = GameObject.Find("Bunker");
            if (rootObject == null)
            {
                Debug.LogWarning("[EXFIL/Decor] Bunker root not found - skipping hideout decoration.");
                return 0;
            }
            Transform root = rootObject.transform;

            // primitive shells become invisible collision volumes
            HideRenderers(root, "Wall_", "Floor", "Ceiling", "Glass", "Stash_Locker", "Workbench",
                          "Medstation", "NutritionUnit", "Lavatory", "Generator", "WaterCollector",
                          "GreenhouseTerminal", "Bunk_", "TargetWall");

            int count = 0;
            GameObject decor = new GameObject("Decor");
            decor.transform.SetParent(root, false);
            Transform d = decor.transform;

            // ---- corridor: modular floor, walls with doorways, ceiling -------------
            for (float x = -18f; x <= 18f; x += 4f)
            {
                if (Place("pb_floor", d, new Vector3(x, 0.02f, 0f), 0f, 4f, ScaleMode.Footprint) != null) count++;
                Place("pb_floor", d, new Vector3(x, 3.18f, 0f), 180f, 4f, ScaleMode.Footprint);
            }
            float[] roomCentres = { -14f, -7f, 0f, 7f, 14f };
            for (float x = -19.2f; x <= 19.2f; x += 3.2f)
            {
                bool doorway = false;
                for (int r = 0; r < roomCentres.Length; r++)
                    if (Mathf.Abs(x - roomCentres[r]) < 1.7f) doorway = true;
                string key = doorway ? "pb_wall_doorway" : ((Mathf.RoundToInt(x / 3.2f) % 3 == 0) ? "pb_wall_window_open" : "pb_wall");
                if (Place(key, d, new Vector3(x, 0f, 3.05f), 0f, 3.2f, ScaleMode.Height) != null) count++;
                Place("pb_wall", d, new Vector3(x, 0f, -3.05f), 180f, 3.2f, ScaleMode.Height);
            }
            // corridor clutter along the south wall
            for (float x = -16f; x <= 16f; x += 4.5f)
            {
                PlaceTag("crate", d, new Vector3(x + 0.8f, 0f, -2.4f), (float)(_rng.NextDouble() * 360.0));
                if (x % 9f < 4.5f) PlaceTag("barrel", d, new Vector3(x - 1.2f, 0f, -2.5f), 0f);
            }

            // ---- rooms -------------------------------------------------------------
            string[] roomNames = { "Entrance", "StashRoom", "Workshop", "Medbay", "RestRoom" };
            for (int i = 0; i < roomCentres.Length; i++)
            {
                float cx = roomCentres[i];
                GameObject room = Find(root, "Room_" + roomNames[i]);
                Transform rp = room != null ? room.transform : d;
                float z0 = room != null ? room.transform.position.z : 6f;

                // floor + ceiling tiles
                for (int gx = -1; gx <= 1; gx += 2)
                {
                    Place("dg_floor_tile", rp, new Vector3(cx + gx * 2f, 0.03f, z0), 0f, 4f, ScaleMode.Footprint);
                    Place("dg_floor_tile", rp, new Vector3(cx + gx * 2f, 0.03f, z0 + 4f), 0f, 4f, ScaleMode.Footprint);
                    Place("dg_floor_tile", rp, new Vector3(cx + gx * 2f, 3.18f, z0 + 2f), 180f, 4f, ScaleMode.Footprint);
                }
                // side walls
                for (int side = -1; side <= 1; side += 2)
                    for (int k = -1; k <= 1; k++)
                        Place("dg_wall", rp, new Vector3(cx + side * 4f, 0f, z0 + k * 3.2f), 90f * side, 3.2f, ScaleMode.Height);
                for (int k = -1; k <= 1; k++)
                    Place("dg_wall", rp, new Vector3(cx + k * 3.2f, 0f, z0 + 6.05f), 180f, 3.2f, ScaleMode.Height);

                // furniture per room
                switch (i)
                {
                    case 0:   // entrance: crates, a light
                        PlaceTag("crate", rp, new Vector3(cx - 3f, 0f, z0 + 1.2f), 15f);
                        PlaceTag("crate_large", rp, new Vector3(cx + 3f, 0f, z0 + 1.2f), -20f, 1.6f, ScaleMode.Height);
                        Place("dg_torch_lit", rp, new Vector3(cx, 2.2f, z0 + 5.6f), 180f);
                        break;
                    case 1:   // stash room: shelving, chests, weapon rack
                        Place("dg_shelf_large", rp, new Vector3(cx - 2.6f, 0f, z0 + 5.4f), 180f, 2f, ScaleMode.Height);
                        Place("dg_shelf_large", rp, new Vector3(cx + 2.6f, 0f, z0 + 5.4f), 180f, 2f, ScaleMode.Height);
                        Place("dg_chest", rp, new Vector3(cx, 0f, z0 + 3.4f), -8f, 1.1f, ScaleMode.Height);
                        Place("dg_trunk_medium", rp, new Vector3(cx - 1.4f, 0f, z0 + 0.9f), 25f);
                        Place("ka_weapon_rack", rp, new Vector3(cx + 3.2f, 0f, z0 + 1f), -90f, 2f, ScaleMode.Height);
                        Place("ka_weapon_spear", rp, new Vector3(cx + 3.2f, 0.9f, z0 + 1f), 90f);
                        break;
                    case 2:   // workshop: bench, crates, tools
                        Place("pb_table_long", rp, new Vector3(cx, 0f, z0 + 3.2f), 0f, 0.95f, ScaleMode.Height);
                        Place("pb_box_a", rp, new Vector3(cx - 2.8f, 0f, z0 + 1.4f), 0f);
                        Place("pb_box_b", rp, new Vector3(cx - 2.4f, 0.45f, z0 + 1.6f), 30f);
                        Place("pb_box_c", rp, new Vector3(cx + 2.8f, 0f, z0 + 5f), -15f);
                        Place("dg_trunk_medium", rp, new Vector3(cx + 2.2f, 0f, z0 + 1.2f), 40f);
                        Place("pb_barrel_a", rp, new Vector3(cx - 3.2f, 0f, z0 + 4.6f), 0f, 0.95f, ScaleMode.Height);
                        Place("dg_wall_shelves", rp, new Vector3(cx, 1.1f, z0 + 5.9f), 180f, 1.6f, ScaleMode.Height);
                        break;
                    case 3:   // medbay: beds, bottles, cabinet
                        Place("dg_bed_decorated", rp, new Vector3(cx - 2f, 0f, z0 + 4.6f), 90f, 2f, ScaleMode.Height);
                        Place("dg_bed_frame", rp, new Vector3(cx + 2.2f, 0f, z0 + 4.6f), 90f, 2f, ScaleMode.Height);
                        Place("dg_table_small", rp, new Vector3(cx, 0f, z0 + 1.2f), 0f, 0.8f, ScaleMode.Height);
                        Place("dg_bottle_green", rp, new Vector3(cx - 0.2f, 0.8f, z0 + 1.2f), 0f);
                        Place("dg_bottle_brown", rp, new Vector3(cx + 0.2f, 0.8f, z0 + 1.3f), 0f);
                        Place("dg_shelf_small", rp, new Vector3(cx + 3.4f, 0f, z0 + 1.2f), -90f, 1.6f, ScaleMode.Height);
                        break;
                    case 4:   // rest room: beds, table, keg
                        Place("dg_bed_decorated", rp, new Vector3(cx + 2.2f, 0f, z0 + 4.8f), 90f, 2f, ScaleMode.Height);
                        Place("dg_bed_decorated", rp, new Vector3(cx + 2.2f, 0.95f, z0 + 4.8f), 90f, 2f, ScaleMode.Height);
                        Place("dg_table_small", rp, new Vector3(cx - 2.4f, 0f, z0 + 3.2f), 20f, 0.8f, ScaleMode.Height);
                        Place("dg_chair", rp, new Vector3(cx - 3.4f, 0f, z0 + 2.4f), 120f);
                        Place("dg_keg", rp, new Vector3(cx - 2.8f, 0f, z0 + 5.2f), 0f, 0.9f, ScaleMode.Height);
                        Place("dg_plate", rp, new Vector3(cx - 2.4f, 0.85f, z0 + 3.2f), 0f);
                        break;
                }
                Place("dg_torch_mounted", rp, new Vector3(cx - 3.7f, 2.1f, z0 + 2.4f), 90f, 0.5f, ScaleMode.Height);
            }

            // ---- greenhouse: raised beds, plants, water ---------------------------
            GameObject greenhouse = Find(root, "Greenhouse");
            if (greenhouse != null)
            {
                Transform g = greenhouse.transform;
                Vector3 gOrigin = g.position;
                string[] stages = { "kf_grass_small", "kf_grass", "kc_grass", "kc_grass_trees", "kc_grass_trees_tall" };
                for (int b = 0; b < 6; b++)
                {
                    float bx = -4f + (b % 3) * 4f;
                    float bz = (b < 3 ? -1.8f : 1.8f);
                    Vector3 bedPos = gOrigin + new Vector3(bx, 0f, bz);
                    GameObject bed = Place("dg_box_large", g, bedPos, 0f, 0.55f, ScaleMode.Height, 0f, 0f);
                    if (bed != null)
                    {
                        bed.name = "GardenBed_" + b;
                        count++;
                    }
                    else
                    {
                        Fallback("GardenBed_" + b, g, gOrigin + new Vector3(bx, 0.25f, bz),
                                 new Vector3(2.6f, 0.5f, 1.2f), new Color(0.24f, 0.17f, 0.11f));
                    }
                    string stage = stages[_rng.Next(stages.Length)];
                    Place(stage, g, gOrigin + new Vector3(bx, 0.45f, bz), (float)(_rng.NextDouble() * 360.0), 0.7f, ScaleMode.Height);
                }
                Place("dg_table_small", g, gOrigin + new Vector3(4.4f, 0f, 2.2f), 0f, 0.8f, ScaleMode.Height);
                Place("dg_bottle_green", g, gOrigin + new Vector3(4.4f, 0.8f, 2.2f), 0f);
                Place("dg_bottle_brown", g, gOrigin + new Vector3(4.6f, 0.8f, 2.3f), 0f);
                Place("pb_barrel_b", g, gOrigin + new Vector3(-5.2f, 0f, 0f), 0f, 0.95f, ScaleMode.Height);
                Place("dg_shelf_small", g, gOrigin + new Vector3(5.2f, 0f, -2.2f), -90f, 1.6f, ScaleMode.Height);
                Place("dg_torch_lit", g, gOrigin + new Vector3(0f, 2.4f, 3.2f), 180f, 0.6f, ScaleMode.Height);
            }

            // ---- stations: dress the interaction volumes --------------------------
            DressStation(root, "Stash_Locker", new[] { "dg_shelf_large" }, 1.9f, ScaleMode.Height);
            DressStation(root, "Workbench", new[] { "pb_table_long" }, 1.0f, ScaleMode.Height);
            DressStation(root, "Medstation", new[] { "dg_table_small" }, 0.9f, ScaleMode.Height);
            DressStation(root, "NutritionUnit", new[] { "dg_barrel_large" }, 1.0f, ScaleMode.Height);
            DressStation(root, "Lavatory", new[] { "pb_can_a", "pb_box_a" }, 0.9f, ScaleMode.Height);
            DressStation(root, "Generator", new[] { "sb_solarpanel" }, 2.4f, ScaleMode.Height);
            DressStation(root, "WaterCollector", new[] { "dg_barrel_large", "pb_barrel_c" }, 1.2f, ScaleMode.Height);
            DressStation(root, "GreenhouseTerminal", new[] { "pb_box_b" }, 1.0f, ScaleMode.Height);
            DressStation(root, "TargetWall", new[] { "pb_wall_target" }, 2.4f, ScaleMode.Height);

            // shooting range targets
            for (int t = 0; t < 3; t++)
            {
                GameObject target = Place("pb_target_stand_a", d, new Vector3(17.5f + t * 1.4f, 0f, -1.4f + t * 1.2f), 90f, 1.6f, ScaleMode.Height);
                if (target != null) count++;
                Place("pb_target_small", d, new Vector3(17.5f + t * 1.4f, 1.1f, -1.4f + t * 1.2f), 90f, 0.5f, ScaleMode.Height);
            }

            // ---- generator corner: machinery -------------------------------------
            Place("sb_solarpanel", root, new Vector3(-17f, 1.6f, -2.2f), 20f, 2.6f, ScaleMode.MaxDim);
            Place("sb_lights", root, new Vector3(-19f, 2.8f, 0f), 0f, 1.2f, ScaleMode.MaxDim);
            Place("pb_barrel_c", root, new Vector3(-15.5f, 0f, -2.4f), 0f, 0.95f, ScaleMode.Height);

            return count;
        }

        private static void DressStation(Transform root, string stationName, string[] modelKeys, float target, ScaleMode mode)
        {
            GameObject station = Find(root, stationName);
            if (station == null) return;
            GameObject decor = new GameObject("Model_" + stationName);
            decor.transform.SetParent(station.transform, false);
            Vector3 origin = station.transform.position;
            for (int i = 0; i < modelKeys.Length; i++)
                Place(modelKeys[i], decor.transform, origin + new Vector3(0f, 0f, i * 0.01f), 0f, target, mode);
        }

        // ====================================================================== RAID
        public static int DecorateRaid()
        {
            if (!ArtAvailable) return 0;
            Begin(77001);
            GameObject rootObject = GameObject.Find("Map_Factory");
            if (rootObject == null)
            {
                Debug.LogWarning("[EXFIL/Decor] Map_Factory root not found - skipping raid decoration.");
                return 0;
            }
            Transform root = rootObject.transform;

            // hide the abstract shells that real models replace (colliders stay)
            HideRenderers(root, "Machine_", "Container_", "Wall_N", "Wall_S", "Wall_E", "Wall_W");

            GameObject decor = new GameObject("Decor");
            decor.transform.SetParent(root, false);
            Transform d = decor.transform;
            int count = 0;

            // ---- perimeter: fence of real modules, with gates ---------------------
            float half = 60f;
            for (float t = -half + 3f; t <= half - 3f; t += 6f)
            {
                bool gate = Mathf.Abs(Mathf.Abs(t) - 24f) < 3f;
                string key = gate ? "sb_structure_low" : "kf_wall_high";
                if (Place(key, d, new Vector3(t, 0f, half - 0.6f), 0f, 3f, ScaleMode.Height) != null) count++;
                Place(key, d, new Vector3(t, 0f, -half + 0.6f), 180f, 3f, ScaleMode.Height);
                Place(key, d, new Vector3(half - 0.6f, 0f, t), -90f, 3f, ScaleMode.Height);
                Place(key, d, new Vector3(-half + 0.6f, 0f, t), 90f, 3f, ScaleMode.Height);
            }

            // ---- factory hall: interior structures, cover, loot spots -------------
            GameObject factory = Find(root, "Factory");
            Transform f = factory != null ? factory.transform : d;
            Vector3[] moduleSpots =
            {
                new Vector3(-18f, 0f, 15f), new Vector3(-8f, 0f, 15f), new Vector3(8f, 0f, 15f),
                new Vector3(18f, 0f, -15f), new Vector3(8f, 0f, -15f), new Vector3(-8f, 0f, -15f)
            };
            string[] moduleKeys = { "sb_basemodule_a", "sb_basemodule_b", "sb_basemodule_c", "sb_basemodule_d", "sb_basemodule_garage", "sb_basemodule_e" };
            for (int i = 0; i < moduleSpots.Length; i++)
            {
                GameObject module = Place(moduleKeys[i % moduleKeys.Length], f, moduleSpots[i],
                    i % 2 == 0 ? 0f : 180f, 6.5f, ScaleMode.Footprint);
                if (module != null) count++;
            }
            // containers and cargo as cover
            for (int i = 0; i < 22; i++)
            {
                float x = -19f + (i % 11) * 3.8f;
                float z = (i < 11) ? 6f + (i % 3) * 3f : -6f - (i % 3) * 3f;
                string key = (i % 4 == 0) ? "sb_cargodepot_a" : (i % 3 == 0 ? "sb_cargo_a_stacked" : "sb_containers_a");
                if (Place(key, f, new Vector3(x, 0f, z), (i % 2) * 180f, 2.4f, ScaleMode.Footprint) != null) count++;
            }
            // machines / landmark structures
            Place("sb_drill_structure", f, new Vector3(-14f, 0f, 0f), 0f, 8f, ScaleMode.Height);
            Place("sb_structure_tall", f, new Vector3(14f, 0f, 0f), 180f, 8f, ScaleMode.Height);
            Place("sb_solarpanel", f, new Vector3(20f, 1.4f, 6f), -90f, 4f, ScaleMode.MaxDim);
            Place("sb_lights", f, new Vector3(0f, 7.2f, 0f), 0f, 3f, ScaleMode.MaxDim);
            Place("sb_tunnel_straight_a", f, new Vector3(-22f, 0f, 22f), 0f, 8f, ScaleMode.Footprint);
            Place("sb_landingpad_small", d, new Vector3(38f, 0.1f, 38f), 0f, 12f, ScaleMode.Footprint);
            Place("sb_spacetruck", d, new Vector3(-34f, 0f, 18f), 25f, 6f, ScaleMode.Footprint);
            Place("sb_spacetruck_large", d, new Vector3(30f, 0f, -26f), -140f, 8f, ScaleMode.Footprint);

            // interior crates / barrels / chests (loot ambience)
            for (int i = 0; i < 40; i++)
            {
                float x = -20f + (float)_rng.NextDouble() * 40f;
                float z = -18f + (float)_rng.NextDouble() * 36f;
                float yaw = (float)(_rng.NextDouble() * 360.0);
                GameObject placed = PlaceTag(i % 5 == 0 ? "chest" : "crate", f, new Vector3(x, 0f, z), yaw);
                if (placed != null) count++;
                if (i % 3 == 0) PlaceTag("barrel", f, new Vector3(x + 1.6f, 0f, z + 1.1f), 0f);
            }

            // ---- the town outside ------------------------------------------------
            Vector3[] houseSpots =
            {
                new Vector3(-40f, 0f, -30f), new Vector3(-28f, 0f, -40f), new Vector3(-45f, 0f, -12f),
                new Vector3(42f, 0f, 20f), new Vector3(46f, 0f, 6f), new Vector3(30f, 0f, 40f),
                new Vector3(-36f, 0f, 34f), new Vector3(12f, 0f, -42f), new Vector3(-6f, 0f, 44f)
            };
            for (int i = 0; i < houseSpots.Length; i++)
            {
                string key = "kc_building_" + new[] { "a", "b", "c", "d" }[i % 4];
                if (i % 5 == 4) key = "kc_building_garage";
                GameObject house = Place(key, d, houseSpots[i], i * 37f, 7f, ScaleMode.Footprint);
                if (house != null) count++;
            }
            // roads through the site
            for (int i = -3; i <= 3; i++)
            {
                Place("kc_road_straight", d, new Vector3(i * 8f, 0.05f, 44f), 0f, 8f, ScaleMode.Footprint);
                Place("kc_road_straight", d, new Vector3(i * 8f, 0.05f, -44f), 0f, 8f, ScaleMode.Footprint);
                Place("kc_road_straight", d, new Vector3(44f, 0.05f, i * 8f), 90f, 8f, ScaleMode.Footprint);
                Place("kc_road_straight", d, new Vector3(-44f, 0.05f, i * 8f), 90f, 8f, ScaleMode.Footprint);
            }
            Place("kc_road_intersection", d, new Vector3(44f, 0.05f, 44f), 0f, 8f, ScaleMode.Footprint);
            Place("kc_road_intersection", d, new Vector3(-44f, 0.05f, -44f), 0f, 8f, ScaleMode.Footprint);
            Place("kc_road_lights", d, new Vector3(0f, 0.05f, 44f), 0f, 16f, ScaleMode.Footprint);

            // ---- nature / debris -------------------------------------------------
            for (int i = 0; i < 46; i++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                float radius = 26f + (float)_rng.NextDouble() * 30f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Mathf.Abs(position.x) < 24f && Mathf.Abs(position.z) < 22f) continue;   // keep the hall clear
                string tag = i % 5 == 0 ? "rock" : (i % 3 == 0 ? "grass" : "tree");
                if (PlaceTag(tag, d, position, (float)(_rng.NextDouble() * 360.0), tag == "tree" ? 6f : 0f,
                             tag == "tree" ? ScaleMode.Height : ScaleMode.None) != null) count++;
            }
            for (int i = 0; i < 18; i++)
            {
                float x = -50f + (float)_rng.NextDouble() * 100f;
                float z = -50f + (float)_rng.NextDouble() * 100f;
                GameObject rubble = PlaceTag("rubble", d, new Vector3(x, 0f, z), (float)(_rng.NextDouble() * 360.0));
                if (rubble != null) count++;
            }

            // ---- exfil markers ---------------------------------------------------
            ExfilZone[] exits = UnityEngine.Object.FindObjectsOfType<ExfilZone>();
            for (int i = 0; i < exits.Length; i++)
            {
                Vector3 position = exits[i].transform.position;
                GameObject marker = new GameObject("ExfilDecor_" + exits[i].ExitName);
                marker.transform.SetParent(exits[i].transform, false);
                Vector3 origin = exits[i].transform.position;
                Place("sb_landingpad_small", marker.transform, origin + new Vector3(0f, 0.05f, 0f), i * 45f, 8f, ScaleMode.Footprint);
                Place("kf_wall_low", marker.transform, origin + new Vector3(-2.5f, 0f, 0f), 0f, 1.2f, ScaleMode.Height);
                Place("kf_wall_low", marker.transform, origin + new Vector3(2.5f, 0f, 0f), 0f, 1.2f, ScaleMode.Height);
                Place("sb_lights", marker.transform, origin + new Vector3(0f, 2.6f, 0f), 0f, 1.6f, ScaleMode.MaxDim);
            }

            return count;
        }
    }
}
