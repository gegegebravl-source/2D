using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EXFIL.Core;
using EXFIL.Economy;
using EXFIL.Hideout;
using EXFIL.Items;
using EXFIL.Player;
using EXFIL.Progression;
using EXFIL.Raid;

namespace EXFIL.UI
{
    /// <summary>
    /// Every menu in the game. Built at runtime with uGUI so the project has no UI prefab
    /// dependencies: inventory, loot, crafting, garden, hideout, traders, quests, results.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        [Header("Refs")]
        public HUD Hud;
        public EconomyService Economy;
        public QuestLog Quests;

        private Canvas _canvas;
        private RectTransform _windowsRoot;
        private GameObject _currentWindow;
        private PlayerLoadout _localLoadout;

        public bool IsWindowOpen { get { return _currentWindow != null; } }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Services.Register(this);
        }

        private void Start()
        {
            UIFactory.EnsureEventSystem();
            _canvas = UIFactory.Canvas("GameUI");
            _canvas.transform.SetParent(transform, false);
            _windowsRoot = _canvas.GetComponent<RectTransform>();

            if (Hud == null)
            {
                GameObject hudObject = new GameObject("HUD");
                hudObject.transform.SetParent(transform, false);
                Hud = hudObject.AddComponent<HUD>();
            }
            Hud.Build(transform);

            if (Economy == null) Economy = EconomyService.Instance;
            if (Quests == null) Quests = QuestLog.Instance;
        }

        private void Update()
        {
            EXFILInput.Frame input = EXFILInput.Sample();
            if (input.Inventory) ToggleInventory();

            if (Input.GetKeyDown(KeyCode.Escape) && _currentWindow != null)
                CloseWindow();
        }

        public bool CursorUnlocked
        {
            set
            {
                EXFILInput.SetCursorLock(!value);
                PlayerActor player = FindLocalPlayer();
                if (player != null) player.InputEnabled = !value;
            }
        }

        private PlayerActor FindLocalPlayer()
        {
            return FindObjectOfType<PlayerActor>();
        }

        private Inventory LocalInventory()
        {
            PlayerActor player = FindLocalPlayer();
            _localLoadout = player != null ? player.Loadout : null;
            if (player != null && RaidManager.Instance != null && RaidManager.Instance.Status == RaidStatus.InProgress)
                return _localLoadout != null ? _localLoadout.Inventory : null;
            return Meta.GameSession.Instance != null && Meta.GameSession.Instance.Profile != null
                ? Meta.GameSession.Instance.Profile.Stash
                : null;
        }

        // ------------------------------------------------------------ window plumbing
        private RectTransform OpenWindow(string title, float width = 1000f, float height = 700f)
        {
            CloseWindow();
            RectTransform window = UIFactory.Window(_windowsRoot, title, width, height);
            _currentWindow = window.gameObject;
            CursorUnlocked = true;

            Button close = UIFactory.Button(window, "X", CloseWindow, new Color(0.45f, 0.16f, 0.16f));
            close.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
            close.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            close.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 36f);
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(-28f, -26f);
            return window;
        }

        public void CloseWindow()
        {
            if (_currentWindow != null) Destroy(_currentWindow);
            _currentWindow = null;
            CursorUnlocked = false;
        }

        public void ToggleInventory()
        {
            if (_currentWindow != null) { CloseWindow(); return; }
            OpenStash();
        }

        // --------------------------------------------------------------- inventory
        public void OpenStash()
        {
            Inventory inventory = LocalInventory();
            if (inventory == null) return;

            RectTransform window = OpenWindow("STASH / INVENTORY", 1180f, 720f);
            RectTransform body = UIFactory.Panel(window, "Body", new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0f), new Vector2(1f, 1f));
            body.offsetMin = new Vector2(14f, 14f);
            body.offsetMax = new Vector2(-14f, -60f);

            RectTransform scroll;
            UIFactory.ScrollArea(body, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(0.62f, 1f);
            scroll.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            scroll.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            RectTransform detail = UIFactory.Panel(body, "Detail", new Color(0.10f, 0.11f, 0.12f, 0.9f),
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            detail.anchorMin = new Vector2(0.63f, 0f);
            detail.anchorMax = new Vector2(1f, 1f);
            detail.offsetMin = Vector2.zero;
            detail.offsetMax = Vector2.zero;

            Text detailText = UIFactory.Text(detail, "Select an item", 15, TextAnchor.UpperLeft);
            detailText.rectTransform.anchorMin = new Vector2(0.03f, 0.03f);
            detailText.rectTransform.anchorMax = new Vector2(0.97f, 0.97f);
            detailText.rectTransform.offsetMin = Vector2.zero;
            detailText.rectTransform.offsetMax = Vector2.zero;

            foreach (KeyValuePair<EquipmentSlot, ItemInstance> pair in inventory.AllEquipment())
            {
                EquipmentSlot slot = pair.Key;
                ItemInstance item = pair.Value;
                string label = slot + ": " + (item != null ? item.DisplayName + " x" + item.Stack : "-");
                RectTransform row = UIFactory.Row(scroll, 36f, new Color(0.22f, 0.26f, 0.30f, 0.95f));
                AddLabel(row, label, 300f);

                if (item != null)
                {
                    if (item.Def.CanEquip)
                        AddButton(row, "Unequip", () => { inventory.UnequipSlot(slot); OpenStash(); }, 110f);
                    if (item.Def is MedicalItemDefinition || item.Def is ConsumableItemDefinition)
                        AddButton(row, "Use", () => UseItemAnywhere(item), 80f);
                }
            }

            foreach (ContainerInstance container in inventory.Containers)
            {
                AddLabel(scroll, "-- " + container.ContainerId.ToUpper() + " (" + container.Grid.FreeCells() + " free) --", 600f, 34f);
                foreach (ItemInstance item in container.Grid.Items)
                {
                    if (item == null) continue;
                    RectTransform row = UIFactory.Row(scroll, 34f);
                    AddLabel(row, item.DisplayName + (item.Stack > 1 ? " x" + item.Stack : string.Empty), 340f);

                    if (item.Def.CanEquip)
                        AddButton(row, "Equip", () => EquipItemAnywhere(item, slotOf(item)), 90f);
                    if (item.Def is MedicalItemDefinition || item.Def is ConsumableItemDefinition)
                        AddButton(row, "Use", () => UseItemAnywhere(item), 70f);
                    AddButton(row, "Drop", () => { container.Grid.Remove(item.Uid); OpenStash(); }, 80f);
                    AddButton(row, "Info", () => ShowItemInfo(item, detailText), 70f);
                }
            }
        }

        private static EquipmentSlot slotOf(ItemInstance item)
        {
            return item.Def != null ? item.Def.EquipmentSlot : EquipmentSlot.None;
        }

        private void UseItemAnywhere(ItemInstance item)
        {
            PlayerActor player = FindLocalPlayer();
            if (player != null && player.Loadout != null && player.Loadout.Inventory != null && player.Loadout.Inventory.FindItem(item.Uid) != null)
            {
                player.Loadout.UseItem(item.Uid);
                OpenStash();
                return;
            }

            // hideout: consume food / meds directly
            Inventory stash = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile.Stash : null;
            if (stash == null) return;
            MedicalItemDefinition medical = item.Def as MedicalItemDefinition;
            if (medical != null)
            {
                item.Stack -= 1;
                if (item.Stack <= 0) stash.Remove(item.Uid, out ItemInstance removed);
            }
            ConsumableItemDefinition consumable = item.Def as ConsumableItemDefinition;
            if (consumable != null)
            {
                item.Stack -= 1;
                if (item.Stack <= 0) stash.Remove(item.Uid, out ItemInstance removed);
            }
            OpenStash();
        }

        private void EquipItemAnywhere(ItemInstance item, EquipmentSlot slot)
        {
            Inventory inventory = LocalInventory();
            if (inventory == null || slot == EquipmentSlot.None) return;
            inventory.Remove(item.Uid, out ItemInstance taken);
            ItemInstance previous = inventory.Equip(slot, taken);
            if (previous != null) inventory.TryAdd(previous, out ItemInstance leftover);
            inventory.SyncContainersFromEquipment();
            PlayerActor player = FindLocalPlayer();
            if (player != null && player.Loadout != null) player.Loadout.Refresh();
            OpenStash();
        }

        private void ShowItemInfo(ItemInstance item, Text target)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("<b>" + item.DisplayName + "</b>");
            builder.AppendLine(item.Def != null ? item.Def.Description : string.Empty);
            builder.AppendLine();
            if (item.Def != null)
            {
                List<string> lines = new List<string>();
                item.Def.CollectTooltipLines(lines);
                for (int i = 0; i < lines.Count; i++) builder.AppendLine(lines[i]);
            }
            if (item.MaxDurability > 0f)
                builder.AppendLine("Durability: " + item.CurrentDurability.ToString("0.0") + " / " + item.MaxDurability.ToString("0.0"));
            target.text = builder.ToString();
        }

        // -------------------------------------------------------------------- loot
        public void OpenLoot(LootPile pile, GameObject actor)
        {
            if (pile == null) return;
            Inventory player = actor != null && actor.GetComponentInParent<PlayerLoadout>() != null
                ? actor.GetComponentInParent<PlayerLoadout>().Inventory
                : LocalInventory();
            if (player == null) return;

            RectTransform window = OpenWindow("LOOT: " + pile.ContainerName, 900f, 640f);
            RectTransform scroll;
            UIFactory.ScrollArea(window, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 60f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -14f);

            for (int i = 0; i < pile.Items.Count; i++)
            {
                ItemInstance item = pile.Items[i];
                if (item == null) continue;
                RectTransform row = UIFactory.Row(scroll, 34f);
                AddLabel(row, item.DisplayName + (item.Stack > 1 ? " x" + item.Stack : string.Empty), 420f);
                AddButton(row, "Take", () =>
                {
                    pile.Items.Remove(item);
                    ItemInstance leftover;
                    if (player.TryAdd(item, out leftover))
                        GameEvents.Raise(new ItemLootedEvent { ItemId = item.DefinitionId, Amount = item.Stack });
                    else
                        pile.Items.Add(item);
                    if (pile.Items.Count == 0) CloseWindow();
                    else OpenLoot(pile, actor);
                }, 110f);
            }

            Button takeAll = UIFactory.Button(window, "Take all", () =>
            {
                for (int i = pile.Items.Count - 1; i >= 0; i--)
                {
                    ItemInstance item = pile.Items[i];
                    ItemInstance leftover;
                    if (player.TryAdd(item, out leftover)) pile.Items.RemoveAt(i);
                }
                CloseWindow();
            }, new Color(0.18f, 0.4f, 0.22f));
            takeAll.GetComponent<RectTransform>().anchorMin = new Vector2(0.4f, 0f);
            takeAll.GetComponent<RectTransform>().anchorMax = new Vector2(0.6f, 0f);
            takeAll.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 40f);
            takeAll.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 26f);
        }

        // ---------------------------------------------------------------- crafting
        public void OpenCrafting(string moduleId)
        {
            Inventory stash = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile.Stash : null;
            if (stash == null) return;

            RectTransform window = OpenWindow("CRAFTING", 1000f, 680f);
            RectTransform scroll;
            UIFactory.ScrollArea(window, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.72f);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 14f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -70f);

            int playerLevel = Meta.GameSession.Instance.Profile.Level;

            foreach (CraftRecipe recipe in RecipeDatabase.All)
            {
                if (recipe == null) continue;
                if (!string.IsNullOrEmpty(moduleId) && recipe.StationModuleId != moduleId) continue;

                string reason;
                bool can = Crafting.CanCraft(recipe, stash, playerLevel, out reason);
                RectTransform row = UIFactory.Row(scroll, 40f, can ? new Color(0.16f, 0.24f, 0.18f, 0.95f) : new Color(0.24f, 0.16f, 0.16f, 0.95f));
                AddLabel(row, recipe.DisplayName + "  (" + recipe.DurationSeconds.ToString("0") + "s)", 420f);
                AddLabel(row, can ? "Ready" : reason, 220f);
                AddButton(row, "Craft", () =>
                {
                    if (Crafting.Start(recipe, stash, playerLevel, out string error)) OpenCrafting(moduleId);
                    else Debug.Log("[EXFIL] " + error);
                }, 110f);
            }

            // queue
            HideoutManager hideout = HideoutManager.Instance;
            if (hideout != null)
            {
                AddLabel(window, "QUEUE (" + hideout.Crafts.Count + " / " + hideout.TotalCraftingSlots() + ")", 400f, 30f);
                for (int i = 0; i < hideout.Crafts.Count; i++)
                {
                    CraftingTask task = hideout.Crafts[i];
                    CraftRecipe recipe = RecipeDatabase.Find(task.RecipeId);
                    RectTransform row = UIFactory.Row(window, 30f);
                    AddLabel(row, (recipe != null ? recipe.DisplayName : task.RecipeId) + "  " +
                                  (task.Progress * 100f).ToString("0") + "%", 520f);
                }
            }
        }

        // ------------------------------------------------------------------ garden
        public void OpenGarden()
        {
            GreenhouseController greenhouse = GreenhouseController.Instance;
            if (greenhouse == null) return;

            RectTransform window = OpenWindow("GREENHOUSE  (light " + (greenhouse.LightLevel * 100f).ToString("0") +
                                             "%, " + greenhouse.Temperature.ToString("0") + "C, water " +
                                             greenhouse.WaterTankLitres.ToString("0.0") + "L)", 900f, 640f);
            RectTransform scroll;
            UIFactory.ScrollArea(window, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 60f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -14f);

            for (int i = 0; i < greenhouse.Plots.Count; i++)
            {
                GardenPlot plot = greenhouse.Plots[i];
                if (plot == null) continue;

                RectTransform row = UIFactory.Row(scroll, 40f);
                string status = plot.IsEmpty ? "empty" :
                    plot.Definition.DisplayName + "  " + (plot.Plant.Progress * 100f).ToString("0") + "%  water " +
                    (plot.Plant.Water * 100f).ToString("0") + "%";
                AddLabel(row, "Plot " + (i + 1) + ": " + status, 520f);

                if (plot.IsEmpty)
                    AddButton(row, "Plant", () => OpenPlanting(plot), 100f);
                else
                {
                    AddButton(row, "Water", () => { plot.Water(); OpenGarden(); }, 90f);
                    if (plot.IsMature)
                        AddButton(row, "Harvest", () => { plot.Harvest(); OpenGarden(); }, 110f);
                    else
                        AddButton(row, "Clear", () => { plot.Plant = null; OpenGarden(); }, 90f);
                }
            }
        }

        public void OpenPlanting(GardenPlot plot)
        {
            Inventory stash = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile.Stash : null;
            if (stash == null || plot == null) return;

            RectTransform window = OpenWindow("PLANT A SEED", 800f, 560f);
            RectTransform scroll;
            UIFactory.ScrollArea(window, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 60f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -14f);

            foreach (ItemInstance item in stash.AllItems())
            {
                SeedItemDefinition seed = item.Def as SeedItemDefinition;
                if (seed == null) continue;
                PlantDefinition plant = PlantDatabase.Find(seed.PlantId);
                if (plant == null) continue;

                RectTransform row = UIFactory.Row(scroll, 36f);
                AddLabel(row, plant.DisplayName + "  (" + (plant.GrowTimeSeconds / 60f).ToString("0") + " min)", 420f);
                AddButton(row, "Plant", () =>
                {
                    plot.PlantSeed(plant);
                    stash.Consume(item.DefinitionId, 1);
                    OpenGarden();
                }, 110f);
            }
        }

        // ----------------------------------------------------------------- hideout
        public void OpenHideout()
        {
            HideoutManager hideout = HideoutManager.Instance;
            Inventory stash = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile.Stash : null;
            if (hideout == null || stash == null) return;

            RectTransform window = OpenWindow("HIDEOUT  (fuel " + hideout.FuelLitres.ToString("0.0") + " L, burn " +
                                             (hideout.TotalFuelConsumption() * 60f).ToString("0.0") + " L/h)", 1000f, 700f);
            RectTransform scroll;
            UIFactory.ScrollArea(window, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 60f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -14f);

            foreach (HideoutModuleDefinition module in hideout.Modules)
            {
                if (module == null) continue;
                ModuleState state = hideout.Get(module.Id);
                string status = state.IsBuilding ? "BUILDING" : "lvl " + state.Level + " / " + module.MaxLevel;

                RectTransform row = UIFactory.Row(scroll, 40f);
                AddLabel(row, module.DisplayName + "  [" + status + "]", 460f);

                string reason;
                bool can = hideout.CanStartUpgrade(module.Id, stash, out reason);
                AddLabel(row, can ? "Upgrade available" : reason, 260f);
                if (can)
                    AddButton(row, "Upgrade", () => { hideout.StartUpgrade(module.Id, stash, out string error); OpenHideout(); }, 120f);
            }
        }

        public void OpenFuel()
        {
            HideoutManager hideout = HideoutManager.Instance;
            Inventory stash = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile.Stash : null;
            if (hideout == null || stash == null) return;

            RectTransform window = OpenWindow("GENERATOR  (" + hideout.FuelLitres.ToString("0.0") + " L)", 700f, 460f);
            RectTransform scroll;
            UIFactory.ScrollArea(window, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 60f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -14f);

            foreach (ItemInstance item in stash.AllItems())
            {
                if (item == null || item.Def == null) continue;
                if (!item.DefinitionId.Contains("fuel")) continue;

                RectTransform row = UIFactory.Row(scroll, 36f);
                AddLabel(row, item.DisplayName + " x" + item.Stack, 380f);
                AddButton(row, "Pour in", () =>
                {
                    hideout.FuelLitres += 5f;
                    stash.Consume(item.DefinitionId, 1);
                    OpenFuel();
                }, 130f);
            }
        }

        // ----------------------------------------------------------------- traders
        public void OpenTraders()
        {
            if (Economy == null || Meta.GameSession.Instance == null) return;
            PlayerProfile profile = Meta.GameSession.Instance.Profile;

            RectTransform window = OpenWindow("TRADERS", 1100f, 720f);
            RectTransform scroll;
            UIFactory.ScrollArea(window, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 60f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -14f);

            foreach (TraderDefinition trader in Economy.Traders)
            {
                if (trader == null) continue;
                TraderStanding standing = profile.Standing(trader.Id);
                AddLabel(scroll, "-- " + trader.DisplayName + "  (loyalty " + standing.LoyaltyLevel + ", rep " +
                                 standing.Reputation.ToString("0.00") + ") --", 900f, 34f);

                foreach (TraderOffer offer in trader.Assort)
                {
                    ItemDefinition def = ItemDatabase.Find(offer.ItemId);
                    if (def == null) continue;
                    int stock = Economy.StockOf(trader.Id, offer.ItemId);

                    RectTransform row = UIFactory.Row(scroll, 34f);
                    AddLabel(row, def.DisplayName + "  " + offer.Price + "  (stock " + (offer.Stock < 0 ? "∞" : stock.ToString()) +
                             ", LL" + offer.LoyaltyLevel + ")", 560f);

                    AddButton(row, "Buy", () =>
                    {
                        if (Economy.Buy(trader, offer, profile, 1, out string reason)) OpenTraders();
                        else Debug.Log("[EXFIL] " + reason);
                    }, 90f);
                }

                AddLabel(scroll, "  sell: select items in the stash screen, or use the sell button here", 900f, 26f);
                AddButton(scroll, "Sell all junk", () =>
                {
                    List<ItemInstance> junk = new List<ItemInstance>();
                    foreach (ItemInstance item in profile.Stash.AllItems())
                        if (item != null && item.Def != null && item.Def.CanBeSold && item.Def.Rarity == ItemRarity.Common
                            && !(item.Def is MedicalItemDefinition) && !(item.Def is ConsumableItemDefinition))
                            junk.Add(item);

                    for (int i = 0; i < junk.Count; i++)
                    {
                        Economy.Sell(trader, junk[i], profile, out int price, out string reason);
                    }
                    OpenTraders();
                }, 220f);
            }
        }

        // ------------------------------------------------------------------ quests
        public void OpenQuests()
        {
            if (Quests == null || Meta.GameSession.Instance == null) return;
            PlayerProfile profile = Meta.GameSession.Instance.Profile;

            RectTransform window = OpenWindow("QUESTS", 1000f, 680f);
            RectTransform scroll;
            UIFactory.ScrollArea(window, out scroll);
            scroll.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 60f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, -14f);

            foreach (QuestDefinition quest in Quests.Quests)
            {
                if (quest == null) continue;
                QuestProgress progress = profile.Quest(quest.Id);
                string status = progress == null ? "available" : progress.Status.ToString();

                RectTransform row = UIFactory.Row(scroll, 40f);
                AddLabel(row, quest.DisplayName + "  [" + status + "]", 520f);

                if (progress == null)
                    AddButton(row, "Accept", () => { Quests.Start(quest); OpenQuests(); }, 110f);
                else if (progress.Status == QuestStatus.Completed && !progress.RewardsClaimed)
                    AddButton(row, "Claim", () => { Quests.ClaimRewards(quest); OpenQuests(); }, 110f);
                else if (progress.Status == QuestStatus.Active)
                {
                    string detail = string.Empty;
                    for (int o = 0; o < quest.Objectives.Count; o++)
                    {
                        int value = o < progress.ObjectiveProgress.Count ? progress.ObjectiveProgress[o] : 0;
                        detail += quest.Objectives[o].Type + " " + value + "/" + quest.Objectives[o].Amount + "   ";
                    }
                    AddLabel(row, detail, 420f);
                }
            }
        }

        // ------------------------------------------------------------- raid result
        public void ShowRaidResult(RaidResult result, float timeLeft)
        {
            RectTransform window = OpenWindow("RAID " + result.ToString().ToUpper(), 700f, 420f);

            Text summary = UIFactory.Text(window, string.Empty, 20, TextAnchor.UpperLeft);
            summary.rectTransform.anchorMin = new Vector2(0.05f, 0.25f);
            summary.rectTransform.anchorMax = new Vector2(0.95f, 0.9f);
            summary.rectTransform.offsetMin = Vector2.zero;
            summary.rectTransform.offsetMax = Vector2.zero;

            PlayerProfile profile = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile : null;
            string text = "Result: " + result + "\n";
            if (profile != null)
                text += "Level " + profile.Level + "   Raids " + profile.RaidsPlayed + "   Survived " +
                        profile.RaidsSurvived + "   K/D " + profile.KdRatio.ToString("0.00") + "\n";
            text += "Time left: " + timeLeft.ToString("0") + "s";
            summary.text = text;

            Button back = UIFactory.Button(window, "Back to hideout", () =>
            {
                CloseWindow();
                RaidManager.Instance?.LeaveToHideout();
            }, new Color(0.18f, 0.4f, 0.22f));
            back.GetComponent<RectTransform>().anchorMin = new Vector2(0.35f, 0.05f);
            back.GetComponent<RectTransform>().anchorMax = new Vector2(0.65f, 0.05f);
            back.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 46f);
            back.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 30f);
        }

        // ----------------------------------------------------------------- helpers
        private static void AddLabel(Transform parent, string text, float width, float height = 30f)
        {
            Text label = UIFactory.Text(parent, text, 15);
            LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.minHeight = height;
        }

        private static void AddButton(Transform parent, string label, Action action, float width)
        {
            Button button = UIFactory.Button(parent, label, action);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.minHeight = 28f;
        }
    }
}
