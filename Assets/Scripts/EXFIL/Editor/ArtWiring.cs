using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using EXFIL.Art;
using EXFIL.Characters;
using EXFIL.Hideout;
using EXFIL.Items;
using EXFIL;

namespace EXFILEditor
{
    /// <summary>
    /// Connects the imported CC0 models to the gameplay assets: weapon view/world models,
    /// plant growth stages for the greenhouse beds and operator bodies.
    /// </summary>
    public static class ArtWiring
    {
        /// <summary>weapon id prefix -> (first person model, muzzle kind, two handed)</summary>
        private static readonly Dictionary<string, string> WeaponKeys = new Dictionary<string, string>
        {
            { "wpn_pistol_pm", "wpn_pistol" }, { "wpn_pistol", "wpn_pistol" },
            { "wpn_smg", "wpn_smg" }, { "wpn_mp", "wpn_smg" },
            { "wpn_ak", "wpn_ak" }, { "wpn_ar", "wpn_rifle" }, { "wpn_m4", "wpn_rifle" },
            { "wpn_rifle", "wpn_rifle" }, { "wpn_carbine", "wpn_rifle" },
            { "wpn_dmr", "wpn_dmr" }, { "wpn_svd", "wpn_dmr" },
            { "wpn_sniper", "wpn_sniper" }, { "wpn_sv", "wpn_sniper" },
            { "wpn_shotgun", "wpn_shotgun" }, { "wpn_pump", "wpn_shotgun" },
            { "wpn_melee", "wpn_melee_knife" }, { "wpn_knife", "wpn_melee_knife" },
            { "wpn_axe", "wpn_melee_axe" }, { "wpn_sword", "wpn_melee_sword" },
            { "wpn_launcher", "wpn_launcher" }, { "wpn_grenade", "wpn_launcher" },
        };

        private static readonly Dictionary<string, Vector3> ViewOffsets = new Dictionary<string, Vector3>
        {
            { "pistol", new Vector3(0.16f, -0.16f, 0.30f) },
            { "smg", new Vector3(0.17f, -0.15f, 0.28f) },
            { "rifle", new Vector3(0.19f, -0.17f, 0.24f) },
            { "dmr", new Vector3(0.19f, -0.17f, 0.22f) },
            { "sniper", new Vector3(0.19f, -0.17f, 0.20f) },
            { "shotgun", new Vector3(0.19f, -0.17f, 0.26f) },
            { "melee", new Vector3(0.22f, -0.22f, 0.28f) },
            { "launcher", new Vector3(0.19f, -0.18f, 0.22f) },
        };

        [MenuItem("EXFIL/Art/3 - Wire models into items, plants and operators", false, 62)]
        public static void WireAll()
        {
            int weapons = WireWeapons();
            int plants = WirePlants();
            int sounds = WireAudio();
            int operators = 0;
            ModelLibrary library = ModelLibrary.Instance;
            if (library != null) operators = AssetPipeline.WireOperators(library);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("[EXFIL/Art] wired {0} weapons, {1} plant stage sets, {2} operators, {3} sound sets.",
                weapons, plants, operators, sounds));
        }

        public static int WireWeapons()
        {
            int wired = 0;
            string[] guids = AssetDatabase.FindAssets("t:WeaponItemDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                WeaponItemDefinition def = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (def == null) continue;
                string key = ResolveWeaponKey(def);
                if (key == null) continue;
                ModelEntry entry = ModelLibrary.Find(key);
                if (entry == null || entry.Prefab == null) continue;

                def.ViewModelPrefab = entry.Prefab;
                def.WorldModelPrefab = entry.Prefab;

                string kind = KindOf(entry);
                Vector3 offset;
                if (!ViewOffsets.TryGetValue(kind, out offset)) offset = ViewOffsets["rifle"];
                def.ViewModelOffset = offset;
                def.ViewModelAimOffset = new Vector3(0f, offset.y + 0.05f, offset.z - 0.06f);
                def.ViewModelEuler = Vector3.zero;
                def.WorldModelOffset = new Vector3(0f, -0.02f, 0.06f);
                def.WorldModelEuler = Vector3.zero;
                def.WorldModelScale = 1f;
                def.TwoHanded = def.WeaponClass != Core.WeaponClass.Pistol && def.WeaponClass != Core.WeaponClass.Melee;
                def.MuzzleOffset = new Vector2(0f, 0.02f);
                EditorUtility.SetDirty(def);
                wired++;
            }
            return wired;
        }

        private static string KindOf(ModelEntry entry)
        {
            if (entry.Tags != null)
                for (int i = 0; i < entry.Tags.Length; i++)
                    if (ViewOffsets.ContainsKey(entry.Tags[i])) return entry.Tags[i];
            return "rifle";
        }

        private static string ResolveWeaponKey(WeaponItemDefinition def)
        {
            string id = (def.Id ?? "").ToLowerInvariant();
            // longest prefix match wins so "wpn_ak74" resolves before "wpn_ak"
            string best = null;
            foreach (KeyValuePair<string, string> pair in WeaponKeys)
            {
                if (!id.StartsWith(pair.Key)) continue;
                if (best == null || pair.Key.Length > best.Length) best = pair.Key;
            }
            if (best != null) return WeaponKeys[best];
            switch (def.WeaponClass)
            {
                case Core.WeaponClass.AssaultRifle: return "wpn_rifle";
                case Core.WeaponClass.SMG: return "wpn_smg";
                case Core.WeaponClass.Pistol: return "wpn_pistol";
                case Core.WeaponClass.SniperRifle: return "wpn_sniper";
                case Core.WeaponClass.MarksmanRifle: return "wpn_dmr";
                case Core.WeaponClass.AssaultCarbine: return "wpn_rifle";
                case Core.WeaponClass.LMG: return "wpn_rifle";
                case Core.WeaponClass.Throwable: return "wpn_launcher";
                case Core.WeaponClass.Shotgun: return "wpn_shotgun";
                case Core.WeaponClass.Melee: return "wpn_melee_knife";
                default: return "wpn_rifle";
            }
        }

        /// <summary>CC0 gun / impact sounds from the Kenney starter kits.</summary>
        public static int WireAudio()
        {
            AudioClip shot = LoadClip("Assets/Audio/SFX/blaster.ogg");
            AudioClip rifle = LoadClip("Assets/Audio/SFX/blaster_repeater.ogg");
            AudioClip change = LoadClip("Assets/Audio/SFX/weapon_change.ogg");
            if (shot == null && rifle == null) return 0;

            int wired = 0;
            string[] guids = AssetDatabase.FindAssets("t:WeaponItemDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                WeaponItemDefinition def = AssetDatabase.LoadAssetAtPath<WeaponItemDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (def == null) continue;
                bool big = def.WeaponClass == Core.WeaponClass.AssaultRifle ||
                           def.WeaponClass == Core.WeaponClass.AssaultCarbine ||
                           def.WeaponClass == Core.WeaponClass.MarksmanRifle ||
                           def.WeaponClass == Core.WeaponClass.SniperRifle ||
                           def.WeaponClass == Core.WeaponClass.LMG ||
                           def.WeaponClass == Core.WeaponClass.Shotgun;
                def.FireSound = big ? (rifle != null ? rifle : shot) : (shot != null ? shot : rifle);
                def.ReloadSound = change;
                def.DryFireSound = change;
                EditorUtility.SetDirty(def);
                wired++;
            }

            AudioClip hurt = LoadClip("Assets/Audio/SFX/enemy_hurt.ogg");
            AudioClip death = LoadClip("Assets/Audio/SFX/enemy_destroy.ogg");
            string[] characterGuids = AssetDatabase.FindAssets("t:CharacterDefinition");
            for (int i = 0; i < characterGuids.Length; i++)
            {
                CharacterDefinition character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                    AssetDatabase.GUIDToAssetPath(characterGuids[i]));
                if (character == null) continue;
                if (hurt != null) character.PainSounds = new[] { hurt };
                if (death != null) character.DeathSounds = new[] { death };
                EditorUtility.SetDirty(character);
            }
            return wired;
        }

        private static AudioClip LoadClip(string path)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>Growth stages for the greenhouse beds: sprout -> mature plant.</summary>
        public static int WirePlants()
        {
            string[] stageKeys = { "kf_grass_small", "kf_grass", "kc_grass", "kc_grass_trees", "kc_grass_trees_tall" };
            GameObject[] stages = new GameObject[stageKeys.Length];
            for (int i = 0; i < stageKeys.Length; i++)
            {
                ModelEntry entry = ModelLibrary.Find(stageKeys[i]);
                stages[i] = entry != null ? entry.Prefab : null;
            }
            if (stages[1] == null && stages[3] == null) return 0;

            int wired = 0;
            string[] guids = AssetDatabase.FindAssets("t:PlantDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                PlantDefinition plant = AssetDatabase.LoadAssetAtPath<PlantDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (plant == null) continue;
                if (plant.StagePrefabs == null || plant.StagePrefabs.Length != stageKeys.Length)
                    plant.StagePrefabs = new GameObject[stageKeys.Length];
                for (int s = 0; s < stageKeys.Length; s++)
                    if (stages[s] != null) plant.StagePrefabs[s] = stages[s];
                EditorUtility.SetDirty(plant);
                wired++;
            }
            return wired;
        }
    }
}
