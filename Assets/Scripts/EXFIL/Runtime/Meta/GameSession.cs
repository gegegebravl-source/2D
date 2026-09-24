using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Hideout;
using EXFIL.Items;
using EXFIL.Progression;
using EXFIL.Raid;

namespace EXFIL.Meta
{
    /// <summary>
    /// The one object that survives scene changes: current profile, save/load,
    /// insurance returns and starting raids (solo with bots or networked).
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Header("Content")]
        public List<CharacterDefinition> Characters = new List<CharacterDefinition>();
        public TextAsset ItemsJson;               // optional export of the item database

        [Header("Session")]
        public PlayerProfile Profile;
        public CharacterDefinition CurrentCharacter;
        public SessionMode Mode = SessionMode.SoloWithBots;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Services.Register(this);
        }

        private void Start()
        {
            Load();
        }

        // ---------------------------------------------------------------- profile
        public void NewGame(CharacterDefinition character)
        {
            CurrentCharacter = character != null ? character : (Characters.Count > 0 ? Characters[0] : null);
            Profile = PlayerProfile.CreateNew(CurrentCharacter);
            Hideout.HideoutManager.Instance?.Load(new HideoutSaveData());
            Save();
        }

        public void Load()
        {
            SaveGame save = SaveSystem.Read();
            if (save == null || save.Profile == null)
            {
                if (Characters.Count > 0) NewGame(Characters[0]);
                return;
            }

            Profile = save.Profile;
            if (Profile.Stash == null) Profile.Stash = new Inventory();
            if (Profile.Stash.Containers.Count == 0)
            {
                Profile.Stash.AddContainer(Inventory.StashContainerId, 10, 28);
                Profile.Stash.AddContainer(Inventory.PocketsContainerId, 4, 1);
            }

            CurrentCharacter = FindCharacter(Profile.CharacterId);
            ApplyStashSizeFromHideout();
            HideoutManager.Instance?.Load(save.Hideout);

            if (save.Plants != null && GreenhouseController.Instance != null)
            {
                foreach (PlantSaveData plant in save.Plants)
                {
                    GardenPlot plot = GreenhouseController.Instance.Plots.Find(p => p != null && p.PlotId == plant.PlotId);
                    if (plot != null) plot.Load(plant);
                }
            }

            if (QuestLog.Instance != null) QuestLog.Instance.Bind(Profile);
            Debug.Log("[EXFIL] Save loaded. Level " + Profile.Level);
        }

        public void Save()
        {
            if (Profile == null) return;

            SaveGame save = new SaveGame { Profile = Profile };
            if (HideoutManager.Instance != null) save.Hideout = HideoutManager.Instance.ToSaveData();
            if (GreenhouseController.Instance != null)
                foreach (GardenPlot plot in GreenhouseController.Instance.Plots)
                    if (plot != null) save.Plants.Add(plot.ToSaveData());

            SaveSystem.Write(save);
        }

        public void ApplyStashSizeFromHideout()
        {
            if (Profile == null || HideoutManager.Instance == null) return;
            Vector2Int bonus = HideoutManager.Instance.StashBonus();
            ContainerInstance stash = Profile.Stash.GetContainer(Inventory.StashContainerId);
            if (stash == null)
                stash = Profile.Stash.AddContainer(Inventory.StashContainerId, 10 + bonus.x, 28 + bonus.y);
            else
            {
                stash.Grid.Width = 10 + bonus.x;
                stash.Grid.Height = 28 + bonus.y;
            }
        }

        public CharacterDefinition FindCharacter(string id)
        {
            for (int i = 0; i < Characters.Count; i++)
                if (Characters[i] != null && Characters[i].Id == id) return Characters[i];
            return Characters.Count > 0 ? Characters[0] : null;
        }

        // ------------------------------------------------------------------ raids
        public void StartRaid(RaidSettings settings, bool online)
        {
            if (Profile == null || settings == null) return;
            Mode = online ? SessionMode.OnlinePvP : SessionMode.SoloWithBots;
            Save();
            SceneManager.LoadScene(settings.SceneName);
        }

        /// <summary>Called when the player dies: secure container kept, insured gear may return.</summary>
        public void ReturnInsurance(Inventory raidInventory, List<ItemInstance> kept)
        {
            if (Profile == null) return;

            for (int i = 0; i < kept.Count; i++)
            {
                string reason;
                Profile.Stash.TryAdd(kept[i], out reason);
            }

            for (int i = Profile.Insurance.Count - 1; i >= 0; i--)
            {
                InsuranceContract contract = Profile.Insurance[i];
                if (contract.Returned) continue;

                // insured gear comes back only if nobody looted it: 55% base chance
                float chance = 0.55f;
                if (Random.value > chance) { Profile.Insurance.RemoveAt(i); continue; }

                foreach (string itemId in contract.ItemIds)
                {
                    ItemDefinition def = ItemDatabase.Find(itemId);
                    if (def == null) continue;
                    ItemInstance returned = ItemInstance.Create(def, 1);
                    string reason;
                    Profile.Stash.TryAdd(returned, out reason);
                }
                contract.Returned = true;
                Profile.Insurance.RemoveAt(i);
            }
        }

        public void AddXp(float amount)
        {
            if (Profile == null) return;
            if (ProgressionService.AddXp(Profile, amount))
                Debug.Log("[EXFIL] Level up! Now " + Profile.Level);
        }

        private void OnApplicationQuit()
        {
            Save();
        }
    }
}
