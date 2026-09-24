using UnityEngine;
using UnityEngine.SceneManagement;
using EXFIL.Core;
using EXFIL.Items;
using EXFIL.Player;
using EXFIL.Raid;
using EXFIL.Characters;

namespace EXFIL.Meta
{
    /// <summary>
    /// Boots the game in any scene: wires the item database, spawns the session,
    /// and starts either the hideout flow or a raid.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Databases")]
        public ItemDatabase ItemDatabaseAsset;

        [Header("Raid")]
        public RaidSettings RaidSettingsOverride;
        public bool AutoStartRaid = false;

        [Header("Player")]
        public GameObject PlayerPrefab;

        private void Awake()
        {
            if (ItemDatabaseAsset != null) ItemDatabase.SetInstance(ItemDatabaseAsset);
            else ItemDatabase.SetInstance(Resources.Load<ItemDatabase>("Data/ItemDatabase"));

            if (GameSession.Instance == null)
            {
                GameObject sessionObject = new GameObject("GameSession");
                sessionObject.AddComponent<GameSession>();
            }
        }

        private void Start()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool isRaidScene = RaidManager.Instance != null;

            if (isRaidScene)
            {
                StartRaidFlow();
                return;
            }

            if (AutoStartRaid)
            {
                RaidSettings settings = RaidSettingsOverride;
                if (settings != null) GameSession.Instance.StartRaid(settings, false);
            }
            else
            {
                SpawnHideoutPlayer();
            }
        }

        private void StartRaidFlow()
        {
            RaidManager raid = RaidManager.Instance;
            RaidSettings settings = RaidSettingsOverride != null
                ? RaidSettingsOverride
                : Resources.Load<RaidSettings>("Data/DefaultRaid");

            raid.StartRaid(settings, withBots: GameSession.Instance == null || GameSession.Instance.Mode != SessionMode.OnlinePvP);

            if (GameSession.Instance != null)
            {
                CharacterDefinition definition = GameSession.Instance.CurrentCharacter;
                Inventory inventory = BuildRaidInventory(GameSession.Instance.Profile);
                raid.SpawnLocalPlayer(definition, inventory, 1);
                raid.Insure(inventory, 1);
            }
        }

        /// <summary>
        /// Copies the stash loadout into a fresh raid inventory: everything equipped plus
        /// what the player chose to take. (The pre-raid selection screen can trim this list.)
        /// </summary>
        private Inventory BuildRaidInventory(Progression.PlayerProfile profile)
        {
            Inventory raidInventory = new Inventory();
            raidInventory.AddContainer(Inventory.PocketsContainerId, 4, 1);
            raidInventory.AddContainer("secure", 2, 2);

            if (profile == null) return raidInventory;

            foreach (System.Collections.Generic.KeyValuePair<EquipmentSlot, ItemInstance> pair in profile.Stash.AllEquipment())
            {
                if (pair.Key == EquipmentSlot.None) continue;
                raidInventory.Equip(pair.Key, pair.Value, false);
            }
            raidInventory.SyncContainersFromEquipment();

            ContainerInstance backpack = raidInventory.GetContainer("backpack");
            if (backpack != null)
            {
                foreach (ItemInstance item in profile.Stash.AllItems())
                {
                    // simple rule: consumables and ammo come along, heavy barter stays home
                    if (item.Def is AmmoItemDefinition || item.Def is MedicalItemDefinition || item.Def is ConsumableItemDefinition)
                    {
                        string reason;
                        backpack.TryAdd(item, out reason);
                    }
                }
            }
            return raidInventory;
        }

        private void SpawnHideoutPlayer()
        {
            if (PlayerPrefab == null) return;
            GameObject go = Instantiate(PlayerPrefab, Vector3.up * 1.2f, Quaternion.identity);
            PlayerActor actor = go.GetComponent<PlayerActor>();
            if (actor != null && GameSession.Instance != null)
                actor.Initialize(GameSession.Instance.CurrentCharacter, GameSession.Instance.Profile.Stash, 1);
        }
    }
}
