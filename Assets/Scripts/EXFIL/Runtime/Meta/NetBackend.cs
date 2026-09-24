using System;
using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;

namespace EXFIL.Meta
{
    /// <summary>
    /// Abstraction over the transport: the offline backend runs everything locally
    /// (solo raid with bots), the netcode package registers its own implementation.
    /// </summary>
    public interface INetBackend
    {
        NetRole Role { get; }
        bool IsAuthority { get; }
        string Name { get; }
        void StartSession(SessionMode mode, Action onReady);
        void Shutdown();
        uint LocalPlayerId { get; }
        bool IsLocalPlayer(uint id);
    }

    public static class NetBackendRegistry
    {
        private static readonly List<Func<INetBackend>> _factories = new List<Func<INetBackend>>();
        private static INetBackend _current;

        public static void Register(Func<INetBackend> factory)
        {
            if (factory != null) _factories.Add(factory);
        }

        public static INetBackend Current
        {
            get
            {
                if (_current == null)
                {
                    if (_factories.Count > 0) _current = _factories[_factories.Count - 1]();
                    else _current = new LocalNetBackend();
                }
                return _current;
            }
            set { _current = value; }
        }
    }

    /// <summary>Default offline backend: full raid with bots, no network at all.</summary>
    public sealed class LocalNetBackend : INetBackend
    {
        public NetRole Role { get { return NetRole.Offline; } }
        public bool IsAuthority { get { return true; } }
        public string Name { get { return "Offline (bots)"; } }
        public uint LocalPlayerId { get { return 1; } }

        public void StartSession(SessionMode mode, Action onReady)
        {
            Debug.Log("[EXFIL] Starting offline session: " + mode);
            onReady?.Invoke();
        }

        public void Shutdown() { }

        public bool IsLocalPlayer(uint id)
        {
            return id == LocalPlayerId;
        }
    }
}
