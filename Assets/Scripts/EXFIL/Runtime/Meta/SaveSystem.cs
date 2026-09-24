using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EXFIL.Characters;
using EXFIL.Items;
using EXFIL.Progression;

namespace EXFIL.Meta
{
    [Serializable]
    public class SaveGame
    {
        public int Version = 1;
        public string SavedAt;
        public PlayerProfile Profile = new PlayerProfile();
        public Hideout.HideoutSaveData Hideout = new Hideout.HideoutSaveData();
        public List<Hideout.PlantSaveData> Plants = new List<Hideout.PlantSaveData>();
        public Core.GameSettingsData Settings = new Core.GameSettingsData();
    }

    /// <summary>JSON save files under Application.persistentDataPath.</summary>
    public static class SaveSystem
    {
        private const string FileName = "exfil_save_v1.json";

        public static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static bool Exists()
        {
            return File.Exists(SavePath);
        }

        public static void Write(SaveGame save)
        {
            try
            {
                save.SavedAt = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(save, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError("[EXFIL] Save failed: " + e.Message);
            }
        }

        public static SaveGame Read()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                string json = File.ReadAllText(SavePath);
                SaveGame save = JsonUtility.FromJson<SaveGame>(json);
                return save;
            }
            catch (Exception e)
            {
                Debug.LogError("[EXFIL] Load failed: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
