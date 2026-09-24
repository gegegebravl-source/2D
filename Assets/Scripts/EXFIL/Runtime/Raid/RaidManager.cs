using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Items;
using EXFIL.Player;

namespace EXFIL.Raid
{
    /// <summary>
    /// Server authoritative raid flow: countdown, spawns, bot population, exfils,
    /// end-of-raid results, insurance and loot transfer back to the stash.
    /// Works for solo (bots only) and multiplayer - the netcode layer only decides
    /// who is allowed to call the mutating methods.
    /// </summary>
    public class RaidManager : MonoBehaviour
    {
        public static RaidManager Instance { get; private set; }

        [Header("Scene")]
        public RaidSettings Settings;
        public BotDirector Director;
        public Transform[] PlayerSpawnPoints = new Transform[0];
        public LootSpawner[] LootSpawners = new LootSpawner[0];
        public ExfilZone[] Exits = new ExfilZone[0];
        public GameObject PlayerPrefab;

        [Header("Runtime")]
        public RaidStatus Status = RaidStatus.NotStarted;
        public float TimeLeft;
        public int Seed;

        private readonly List<HealthController> _targets = new List<HealthController>();
        private readonly List<PlayerActor> _players = new List<PlayerActor>();
        private readonly Dictionary<uint, List<ItemInstance>> _insured = new Dictionary<uint, List<ItemInstance>>();

        public List<HealthController> PotentialTargets { get { return _targets; } }
        public IList<PlayerActor> Players { get { return _players; } }

        private void Awake()
        {
            Instance = this;
            Services.Register(this);
        }

        public void StartRaid(RaidSettings settings, bool withBots = true, int seed = 0)
        {
            Settings = settings;
            Seed = seed == 0 ? (int)System.DateTime.UtcNow.Ticks : seed;
            Rng rng = new Rng(Seed);

            Status = RaidStatus.Loading;
            TimeLeft = settings != null ? settings.DurationMinutes * 60f : 2100f;

            if (Exits == null || Exits.Length == 0) Exits = FindObjectsOfType<ExfilZone>(true);
            if (LootSpawners == null || LootSpawners.Length == 0) LootSpawners = FindObjectsOfType<LootSpawner>(true);

            Shuffle(LootSpawners, rng);
            int toFill = settings != null ? Mathf.Min(settings.LootContainersToFill, LootSpawners.Length) : LootSpawners.Length;
            for (int i = 0; i < toFill; i++)
                if (LootSpawners[i] != null) LootSpawners[i].Fill(rng);

            if (Director != null && withBots && settings != null)
                Director.StartDirector(settings, Seed);

            Status = RaidStatus.InProgress;
            GameEvents.Raise(new RaidStateChangedEvent { Previous = RaidStatus.Loading, Current = Status, TimeLeft = TimeLeft });
        }

        private static void Shuffle<T>(IList<T> list, Rng rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private void Update()
        {
            if (Status != RaidStatus.InProgress) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f) EndRaid(RaidResult.Missing);
        }

        // ------------------------------------------------------------------ players
        public PlayerActor SpawnLocalPlayer(CharacterDefinition definition, Inventory inventory, uint netId = 1)
        {
            Transform spawn = PlayerSpawnPoints != null && PlayerSpawnPoints.Length > 0
                ? PlayerSpawnPoints[Random.Range(0, PlayerSpawnPoints.Length)]
                : null;

            GameObject go = PlayerPrefab != null ? Instantiate(PlayerPrefab) : new GameObject("LocalPlayer");
            if (spawn != null) go.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            PlayerActor actor = go.GetComponent<PlayerActor>();
            if (actor == null) actor = go.AddComponent<PlayerActor>();
            actor.Initialize(definition, inventory, netId);

            RegisterPlayer(actor);
            return actor;
        }

        public void RegisterPlayer(PlayerActor actor)
        {
            if (actor == null || _players.Contains(actor)) return;
            _players.Add(actor);
            HealthController health = actor.GetComponent<HealthController>();
            if (health != null && !_targets.Contains(health)) _targets.Add(health);
        }

        public void RegisterHealth(HealthController health)
        {
            if (health != null && !_targets.Contains(health)) _targets.Add(health);
        }

        public Vector3? LocalPlayerPosition()
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i] != null) return _players[i].transform.position;
            return null;
        }

        public void ExtractPlayer(PlayerActor player, string exitName)
        {
            if (player == null || Status != RaidStatus.InProgress) return;

            bool runThrough = TimeLeft > (Settings != null ? Settings.DurationMinutes * 60f * 0.85f : 1800f);
            RaidResult result = runThrough ? RaidResult.RunThrough : RaidResult.Survived;

            GameEvents.Raise(new ExtractedEvent { CharacterId = player.NetId, ExitName = exitName, IsPlayer = true });
            FinishForPlayer(player, result);
        }

        public void FinishForPlayer(PlayerActor player, RaidResult result)
        {
            if (player == null) return;

            PlayerLoadout loadout = player.GetComponent<PlayerLoadout>();
            if (loadout != null)
            {
                if (result == RaidResult.Survived || result == RaidResult.RunThrough)
                {
                    Progression.PlayerProfile profile = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile : null;
                    if (profile != null)
                    {
                        foreach (ItemInstance item in loadout.Inventory.AllItems())
                            profile.Stash.TryAdd(item, out string reason);
                        foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in loadout.Inventory.AllEquipment())
                            if (pair.Value != null) profile.Stash.TryAdd(pair.Value, out string reason);
                    }
                }
                else
                {
                    // keep only the secure container contents, insure the rest
                    List<ItemInstance> kept = loadout.GetKeptOnDeath();
                    Meta.GameSession.Instance?.ReturnInsurance(loadout.Inventory, kept);
                }
            }

            if (result == RaidResult.Killed || result == RaidResult.Missing)
            {
                _players.Remove(player);
                if (_players.Count == 0) EndRaid(result);
            }
        }

        public void Insure(Inventory inventory, uint id)
        {
            List<ItemInstance> items = new List<ItemInstance>(inventory.AllItems());
            _insured[id] = items;
        }

        public void EndRaid(RaidResult result)
        {
            if (Status == RaidStatus.Finished) return;
            Status = RaidStatus.Finished;
            GameEvents.Raise(new RaidStateChangedEvent { Previous = RaidStatus.InProgress, Current = Status, TimeLeft = TimeLeft });

            if (Director != null) Director.ClearAll();
            UI.GameUI.Instance?.ShowRaidResult(result, TimeLeft);
        }

        public void LeaveToHideout()
        {
            Meta.GameSession.Instance?.Save();
            SceneManager.LoadScene("Hideout");
        }
    }
}
