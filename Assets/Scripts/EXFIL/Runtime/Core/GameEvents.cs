using System;
using System.Collections.Generic;

namespace EXFIL.Core
{
    /// <summary>Global typed event bus (damage, shots, extracts, hideout ticks...).</summary>
    public static class GameEvents
    {
        private class Handlers
        {
            public readonly List<Delegate> List = new List<Delegate>();
        }

        private static readonly Dictionary<Type, Handlers> _handlers = new Dictionary<Type, Handlers>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            Handlers bag;
            if (!_handlers.TryGetValue(typeof(T), out bag))
            {
                bag = new Handlers();
                _handlers[typeof(T)] = bag;
            }
            if (!bag.List.Contains(handler))
                bag.List.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Handlers bag;
            if (_handlers.TryGetValue(typeof(T), out bag))
                bag.List.Remove(handler);
        }

        public static void Raise<T>(T payload) where T : struct
        {
            Handlers bag;
            if (!_handlers.TryGetValue(typeof(T), out bag))
                return;

            for (int i = bag.List.Count - 1; i >= 0; i--)
            {
                Delegate d = bag.List[i];
                Action<T> typed = d as Action<T>;
                if (typed != null)
                {
                    try
                    {
                        typed.Invoke(payload);
                    }
#pragma warning disable CS0168
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }
#pragma warning restore CS0168
                }
            }
        }

        public static void Clear()
        {
            _handlers.Clear();
        }
    }

    // ------------------------------------------------------------------ Events
    public struct ShotFiredEvent
    {
        public UnityEngine.Vector3 Origin;
        public UnityEngine.Vector3 Direction;
        public AmmoCaliber Caliber;
        public float Loudness;      // 0..1 -> hearing radius for bots
        public uint ShooterId;      // net id, 0 for bots
        public bool IsSilenced;
    }

    public struct DamageAppliedEvent
    {
        public uint VictimId;
        public uint AttackerId;
        public BodyPart Part;
        public float Amount;
        public DamageType Type;
        public bool Lethal;
    }

    public struct CharacterDiedEvent
    {
        public uint VictimId;
        public uint KillerId;
        public string VictimName;
        public string KillerName;
        public bool IsPlayer;
    }

    public struct ExtractedEvent
    {
        public uint CharacterId;
        public string ExitName;
        public bool IsPlayer;
    }

    public struct RaidStateChangedEvent
    {
        public RaidStatus Previous;
        public RaidStatus Current;
        public float TimeLeft;
    }

    public struct ItemLootedEvent
    {
        public uint LooterId;
        public string ItemId;
        public int Amount;
    }

    public struct HideoutTickEvent
    {
        public float DeltaTime;
    }

    public struct PlantHarvestedEvent
    {
        public string PlantId;
        public int Yield;
    }
}
