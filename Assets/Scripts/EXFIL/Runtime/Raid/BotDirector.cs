using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EXFIL.AI;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Items;

namespace EXFIL.Raid
{
    /// <summary>
    /// Spawns and maintains the bot population: opening waves, trickle respawns,
    /// distance rules (never right on top of a player) and difficulty scaling.
    /// </summary>
    public class BotDirector : MonoBehaviour
    {
        public RaidSettings Settings;
        public List<BotProfile> Profiles = new List<BotProfile>();
        public List<Transform> SpawnPoints = new List<Transform>();

        private readonly List<BotController> _alive = new List<BotController>();
        private Rng _rng;
        private uint _nextId = 1000;

        public IList<BotController> Alive { get { return _alive; } }

        public void StartDirector(RaidSettings settings, int seed)
        {
            Settings = settings;
            _rng = new Rng(seed);
            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            if (Settings == null) yield break;

            foreach (BotWave wave in Settings.Waves)
            {
                if (wave.DelayAfterStart > 0f) yield return new WaitForSeconds(wave.DelayAfterStart);

                for (int i = 0; i < wave.Count; i++)
                {
                    if (_alive.Count >= Settings.MaxBotsAlive) yield return new WaitForSeconds(2f);
                    SpawnBot(wave.ProfileId);
                    yield return new WaitForSeconds(0.35f);
                }

                if (wave.RespawnsOverTime)
                    StartCoroutine(Trickle(wave));
            }
        }

        private IEnumerator Trickle(BotWave wave)
        {
            while (true)
            {
                yield return new WaitForSeconds(wave.RespawnInterval);
                if (_alive.Count < Settings.MaxBotsAlive) SpawnBot(wave.ProfileId);
            }
        }

        public BotController SpawnBot(string profileId)
        {
            BotProfile profile = FindProfile(profileId);
            if (profile == null)
            {
                Debug.LogWarning("[EXFIL] Bot profile not found: " + profileId);
                return null;
            }

            Transform spawn = PickSpawnPoint();
            if (spawn == null) return null;

            Inventory inventory = BuildBotInventory(profile);

            GameObject go = new GameObject("Bot_" + profile.DisplayName);
            go.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            CharacterBody3D body = CharacterBody3D.Spawn(null, go.transform, spawn.position, spawn.rotation);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            Material factionMaterial = null;
            if (profile.Faction == Characters.Faction.WestContract)
                factionMaterial = null; // placeholder body already tinted per faction
            if (factionMaterial != null) { }

            HealthController health = go.AddComponent<HealthController>();
            health.Reset();

            Combat.Hitbox.BuildHitboxes(body, health);

            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.height = 1.8f;
            agent.radius = 0.35f;

            BotController bot = go.AddComponent<BotController>();
            bot.Initialize(profile, inventory, _nextId++, _rng);
            bot.Body = body;

            EquipVisualGear(bot);

            _alive.Add(bot);
            health.OnDied += () => _alive.Remove(bot);
            return bot;
        }

        private void EquipVisualGear(BotController bot)
        {
            EquipmentController equipment = bot.GetComponent<EquipmentController>();
            if (equipment == null) equipment = bot.gameObject.AddComponent<EquipmentController>();
            equipment.Body = bot.Body;
            equipment.ArmorCoverage = bot.Armor;
            equipment.Rebuild();
        }

        private Inventory BuildBotInventory(BotProfile profile)
        {
            Inventory inventory = new Inventory();
            inventory.AddContainer(Inventory.PocketsContainerId, 4, 1);
            inventory.AddContainer("secure", 2, 2);

            string weaponId = profile.PickWeapon(_rng);
            if (!string.IsNullOrEmpty(weaponId) && ItemDatabase.Find(weaponId) != null)
            {
                ItemInstance weapon = ItemInstance.Create(ItemDatabase.Find(weaponId));
                inventory.Equip(EquipmentSlot.PrimaryWeapon, weapon, false);
            }

            string armorId = profile.PickArmor(_rng);
            if (!string.IsNullOrEmpty(armorId) && ItemDatabase.Find(armorId) != null)
                inventory.Equip(EquipmentSlot.ArmorVest, ItemInstance.Create(ItemDatabase.Find(armorId)), false);

            string helmetId = profile.HelmetPool.Count > 0 && _rng.Chance(0.45f) ? _rng.Pick(profile.HelmetPool) : null;
            if (!string.IsNullOrEmpty(helmetId) && ItemDatabase.Find(helmetId) != null)
                inventory.Equip(EquipmentSlot.Helmet, ItemInstance.Create(ItemDatabase.Find(helmetId)), false);

            string backpackId = profile.BackpackPool.Count > 0 && _rng.Chance(0.6f) ? _rng.Pick(profile.BackpackPool) : null;
            if (!string.IsNullOrEmpty(backpackId) && ItemDatabase.Find(backpackId) != null)
                inventory.Equip(EquipmentSlot.Backpack, ItemInstance.Create(ItemDatabase.Find(backpackId)), false);

            inventory.SyncContainersFromEquipment();

            for (int i = 0; i < profile.Loot.Count; i++)
            {
                LootEntry entry = profile.Loot[i];
                if (!_rng.Chance(entry.Chance)) continue;
                ItemDefinition def = ItemDatabase.Find(entry.ItemId);
                if (def == null) continue;
                ItemInstance item = ItemInstance.Create(def, _rng.Range(entry.MinStack, entry.MaxStack + 1));
                ContainerInstance container = inventory.GetContainer(entry.ContainerId) ?? inventory.GetContainer(Inventory.PocketsContainerId);
                string reason;
                if (container != null && !container.TryAdd(item, out reason))
                    inventory.TryAdd(item, out reason);
            }
            return inventory;
        }

        private BotProfile FindProfile(string id)
        {
            for (int i = 0; i < Profiles.Count; i++)
                if (Profiles[i] != null && Profiles[i].Id == id) return Profiles[i];
            return Profiles.Count > 0 ? Profiles[0] : null;
        }

        private Transform PickSpawnPoint()
        {
            if (SpawnPoints.Count == 0) return null;
            Vector3? playerPosition = RaidManager.Instance != null ? RaidManager.Instance.LocalPlayerPosition() : null;

            List<Transform> valid = new List<Transform>();
            for (int i = 0; i < SpawnPoints.Count; i++)
            {
                Transform point = SpawnPoints[i];
                if (point == null) continue;
                if (playerPosition.HasValue && Settings != null &&
                    Vector3.Distance(point.position, playerPosition.Value) < Settings.BotSpawnDistanceFromPlayers)
                    continue;
                valid.Add(point);
            }
            if (valid.Count == 0) valid.AddRange(SpawnPoints.FindAll(t => t != null));
            return valid.Count > 0 ? _rng.Pick(valid) : null;
        }

        public void ClearAll()
        {
            for (int i = 0; i < _alive.Count; i++)
                if (_alive[i] != null) Destroy(_alive[i].gameObject);
            _alive.Clear();
            StopAllCoroutines();
        }
    }
}
