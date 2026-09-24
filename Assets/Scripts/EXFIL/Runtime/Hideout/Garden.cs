using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;
using EXFIL.Items;
using EXFIL.Player;

namespace EXFIL.Hideout
{
    public enum PlantKind { Vegetable = 0, Herb, Mushroom, Technical }

    /// <summary>A growable plant: seeds, growth stages, needs and harvest yield.</summary>
    [CreateAssetMenu(menuName = "EXFIL/Hideout/Plant", fileName = "Plant_")]
    public class PlantDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public PlantKind Kind = PlantKind.Vegetable;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;

        [Header("Growth")]
        public float GrowTimeSeconds = 900f;
        public float WaterUsePerMinute = 0.8f;
        public float LightRequirement = 0.6f;   // 0..1
        public float OptimalTemperature = 22f;
        public bool NeedsFertilizer = false;

        [Header("Harvest")]
        public string YieldItemId;
        public int MinYield = 2;
        public int MaxYield = 5;
        public string SeedItemId;
        public bool ReturnsSeed = true;

        [Header("Visuals (one prefab per stage: seed, sprout, growing, mature, withered)")]
        public GameObject[] StagePrefabs = new GameObject[5];

        public int StageCount { get { return StagePrefabs != null ? StagePrefabs.Length : 0; } }
    }

    [Serializable]
    public class PlantInstance
    {
        public string PlantId;
        public float Progress;          // 0..1
        public float Water;             // 0..1
        public float Health = 1f;
        public bool Fertilized;
        public double PlantedAt;

        public PlantStage Stage
        {
            get
            {
                if (Health <= 0f) return PlantStage.Withered;
                if (Progress >= 1f) return PlantStage.Mature;
                if (Progress > 0.6f) return PlantStage.Growing;
                if (Progress > 0.25f) return PlantStage.Sprout;
                return PlantStage.Seedling;
            }
        }
    }

    /// <summary>
    /// One greenhouse bed. Interact to plant / water / fertilize / harvest.
    /// Growth is driven by the GreenhouseController (light, temperature, water tank).
    /// </summary>
    public class GardenPlot : MonoBehaviour, IInteractable
    {
        [Header("Plot")]
        public string PlotId;
        public Transform GrowthAnchor;
        public MeshRenderer SoilRenderer;

        [Header("Runtime")]
        public PlantInstance Plant;
        public bool Unlocked = true;

        private GameObject _visual;
        private PlantDefinition _def;

        public Transform Transform { get { return transform; } }
        public PlantDefinition Definition
        {
            get
            {
                if (Plant == null) return null;
                if (_def == null || _def.Id != Plant.PlantId) _def = PlantDatabase.Find(Plant.PlantId);
                return _def;
            }
        }

        public bool IsEmpty { get { return Plant == null; } }
        public bool IsMature { get { return Plant != null && Plant.Stage == PlantStage.Mature; } }

        public string GetPrompt(GameObject actor)
        {
            if (!Unlocked) return "Plot locked - upgrade the greenhouse";
            if (IsEmpty) return "Plant a seed";
            if (IsMature) return "Harvest " + Definition.DisplayName;
            return Definition.DisplayName + " - " + (Plant.Progress * 100f).ToString("0") + "% (water " +
                   (Plant.Water * 100f).ToString("0") + "%) - hold to water";
        }

        public bool CanInteract(GameObject actor)
        {
            return Unlocked;
        }

        public float GetInteractTime(GameObject actor)
        {
            if (IsEmpty) return 0f;
            if (IsMature) return 1.5f;
            return 1.2f; // watering
        }

        public void Interact(GameObject actor)
        {
            if (IsEmpty) { UI.GameUI.Instance?.OpenPlanting(this); return; }
            if (IsMature) { Harvest(); return; }
            Water();
        }

        public void PlantSeed(PlantDefinition def)
        {
            Plant = new PlantInstance
            {
                PlantId = def.Id,
                Progress = 0f,
                Water = 0.5f,
                Health = 1f,
                Fertilized = false,
                PlantedAt = Epoch.Now()
            };
            RefreshVisual();
        }

        public void Water()
        {
            if (Plant == null) return;
            Plant.Water = Mathf.Clamp01(Plant.Water + 0.45f);
        }

        public void Fertilize()
        {
            if (Plant == null) return;
            Plant.Fertilized = true;
        }

        public void Harvest()
        {
            if (!IsMature) return;
            PlantDefinition def = Definition;
            if (def == null) return;

            Inventory stash = Meta.GameSession.Instance != null ? Meta.GameSession.Instance.Profile.Stash : null;
            int yield = UnityEngine.Random.Range(def.MinYield, def.MaxYield + 1);
            if (Plant.Fertilized) yield += 1;

            if (stash != null)
            {
                ItemDefinition yieldDef = ItemDatabase.Find(def.YieldItemId);
                if (yieldDef != null)
                {
                    ItemInstance harvested = ItemInstance.Create(yieldDef, yield);
                    string reason;
                    stash.TryAdd(harvested, out reason);
                }
                if (def.ReturnsSeed && !string.IsNullOrEmpty(def.SeedItemId))
                {
                    ItemDefinition seedDef = ItemDatabase.Find(def.SeedItemId);
                    if (seedDef != null)
                    {
                        ItemInstance seed = ItemInstance.Create(seedDef, 1);
                        string reason;
                        stash.TryAdd(seed, out reason);
                    }
                }
            }

            GameEvents.Raise(new PlantHarvestedEvent { PlantId = def.Id, Yield = yield });
            Plant = null;
            RefreshVisual();
        }

        public void Tick(float deltaTime, float lightLevel, float temperature, float growthBonus, float waterTankLitres)
        {
            if (Plant == null) return;
            PlantDefinition def = Definition;
            if (def == null) return;

            float waterUse = def.WaterUsePerMinute * (deltaTime / 60f);
            if (waterTankLitres > 0f)
            {
                Plant.Water = Mathf.Clamp01(Plant.Water + waterUse * 1.6f);
            }
            Plant.Water = Mathf.Clamp01(Plant.Water - waterUse);

            float lightFactor = Mathf.Clamp01(lightLevel / Mathf.Max(0.01f, def.LightRequirement));
            float temperatureFactor = 1f - Mathf.Clamp01(Mathf.Abs(temperature - def.OptimalTemperature) / 25f);
            float waterFactor = Plant.Water > 0.15f ? 1f : 0.25f;
            float fertilizerFactor = def.NeedsFertilizer ? (Plant.Fertilized ? 1.25f : 0.6f) : (Plant.Fertilized ? 1.1f : 1f);

            float rate = lightFactor * temperatureFactor * waterFactor * fertilizerFactor * (1f + growthBonus);
            Plant.Progress += (deltaTime / Mathf.Max(1f, def.GrowTimeSeconds)) * rate;

            if (Plant.Water <= 0f || lightFactor < 0.15f)
                Plant.Health = Mathf.Max(0f, Plant.Health - deltaTime * 0.006f);

            if (Plant.Progress >= 1f) Plant.Progress = 1f;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (_visual != null)
            {
                Destroy(_visual);
                _visual = null;
            }

            PlantDefinition def = Definition;
            if (def == null || Plant == null) return;
            if (def.StagePrefabs == null || def.StagePrefabs.Length == 0) return;

            int stage = (int)Plant.Stage;
            if (stage < 0 || stage >= def.StagePrefabs.Length) return;
            GameObject prefab = def.StagePrefabs[stage];
            if (prefab == null) return;

            Transform anchor = GrowthAnchor != null ? GrowthAnchor : transform;
            _visual = Instantiate(prefab, anchor);
            _visual.transform.localPosition = Vector3.zero;
            _visual.transform.localRotation = Quaternion.identity;

            float scale = 0.35f + 0.65f * Mathf.Clamp01(Plant.Progress);
            _visual.transform.localScale = Vector3.one * scale;

            if (SoilRenderer != null)
            {
                Color soil = Color.Lerp(new Color(0.42f, 0.32f, 0.22f), new Color(0.18f, 0.14f, 0.10f), Plant.Water);
                SoilRenderer.material.color = soil;
            }
        }

        public PlantSaveData ToSaveData()
        {
            return new PlantSaveData { PlotId = PlotId, Plant = Plant };
        }

        public void Load(PlantSaveData data)
        {
            if (data == null) return;
            Plant = data.Plant;
            RefreshVisual();
        }
    }

    [Serializable]
    public class PlantSaveData
    {
        public string PlotId;
        public PlantInstance Plant;
    }

    /// <summary>Drives light, temperature and water for all plots in the greenhouse.</summary>
    public class GreenhouseController : MonoBehaviour
    {
        [Header("Environment")]
        public float LightLevel = 0.8f;         // 0..1, lowered when the generator is off
        public float Temperature = 22f;
        public float WaterTankLitres = 20f;
        public float WaterTankCapacity = 60f;
        public Light[] GrowLights;

        [Header("Plots")]
        public List<GardenPlot> Plots = new List<GardenPlot>();

        private float _tick;

        public static GreenhouseController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            HideoutManager hideout = HideoutManager.Instance;
            bool powered = hideout == null || hideout.HasPower();
            LightLevel = Mathf.MoveTowards(LightLevel, powered ? 0.85f : 0.1f, Time.deltaTime * 0.5f);
            Temperature = Mathf.MoveTowards(Temperature, powered ? 23f : 12f, Time.deltaTime * 0.4f);

            if (GrowLights != null)
                for (int i = 0; i < GrowLights.Length; i++)
                    if (GrowLights[i] != null) GrowLights[i].intensity = LightLevel * 2.2f;

            // water tank slowly refills from the water collector module
            if (hideout != null)
                WaterTankLitres = Mathf.Min(WaterTankCapacity,
                    WaterTankLitres + hideout.LevelOf("water_collector") * 0.12f * Time.deltaTime);

            _tick += Time.deltaTime;
            if (_tick < 1f) return;
            float delta = _tick;
            _tick = 0f;

            float growthBonus = hideout != null ? hideout.PlantGrowthBonus() : 0f;
            for (int i = 0; i < Plots.Count; i++)
            {
                GardenPlot plot = Plots[i];
                if (plot == null) continue;
                float before = WaterTankLitres;
                plot.Tick(delta, LightLevel, Temperature, growthBonus, WaterTankLitres);
                WaterTankLitres = Mathf.Max(0f, before - delta * 0.02f);
            }
        }

        /// <summary>Offline growth: called once on load with the elapsed real seconds.</summary>
        public void CatchUp(float seconds)
        {
            HideoutManager hideout = HideoutManager.Instance;
            float growthBonus = hideout != null ? hideout.PlantGrowthBonus() : 0f;
            for (int i = 0; i < Plots.Count; i++)
                if (Plots[i] != null) Plots[i].Tick(seconds, LightLevel, Temperature, growthBonus, WaterTankLitres);
        }
    }

    public static class PlantDatabase
    {
        private static readonly List<PlantDefinition> _plants = new List<PlantDefinition>();

        public static IList<PlantDefinition> All { get { return _plants; } }

        public static void Clear() { _plants.Clear(); }

        public static void Register(PlantDefinition plant)
        {
            if (plant != null && !_plants.Contains(plant)) _plants.Add(plant);
        }

        public static PlantDefinition Find(string id)
        {
            for (int i = 0; i < _plants.Count; i++)
                if (_plants[i] != null && _plants[i].Id == id) return _plants[i];
            return null;
        }
    }
}
