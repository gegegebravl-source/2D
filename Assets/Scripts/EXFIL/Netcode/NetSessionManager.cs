using UnityEngine;
using EXFIL.Core;
using EXFIL.Meta;
using EXFIL.Raid;

#if EXFIL_NETCODE
using Unity.Netcode;
#endif

namespace EXFIL.Netcode
{
    /// <summary>
    /// Unity Netcode for GameObjects session layer. The raid itself stays authority-driven:
    /// the host runs RaidManager and all bots, clients only send input and receive state.
    /// </summary>
    public class NetSessionManager : MonoBehaviour, INetBackend
    {
        public string Name { get { return "Netcode for GameObjects"; } }
        public uint LocalPlayerId { get; private set; } = 1;

        public NetRole Role
        {
            get
            {
#if EXFIL_NETCODE
                NetworkManager manager = NetworkManager.Singleton;
                if (manager == null) return NetRole.Offline;
                if (manager.IsHost) return NetRole.Host;
                if (manager.IsServer) return NetRole.Server;
                if (manager.IsClient) return NetRole.Client;
#endif
                return NetRole.Offline;
            }
        }

        public bool IsAuthority
        {
            get
            {
#if EXFIL_NETCODE
                NetworkManager manager = NetworkManager.Singleton;
                return manager == null || manager.IsServer || manager.IsHost;
#else
                return true;
#endif
            }
        }

        public bool IsLocalPlayer(uint id)
        {
            return id == LocalPlayerId;
        }

        private void Awake()
        {
            NetBackendRegistry.Register(() => this);
        }

        public void StartSession(SessionMode mode, System.Action onReady)
        {
#if EXFIL_NETCODE
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                GameObject go = new GameObject("NetworkManager");
                manager = go.AddComponent<NetworkManager>();
                go.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            }

            switch (mode)
            {
                case SessionMode.SoloWithBots:
                case SessionMode.OnlinePvE:
                    manager.StartHost();
                    break;
                default:
                    manager.StartHost();     // lobby / listen server; dedicated server below
                    break;
            }

            manager.OnClientConnectedCallback += id =>
            {
                Debug.Log("[EXFIL] Client connected: " + id);
                LocalPlayerId = id;
            };

            Debug.Log("[EXFIL] Network session started: " + mode);
#else
            Debug.LogWarning("[EXFIL] Netcode package missing - running offline with bots.");
#endif
            onReady?.Invoke();
        }

        public void StartDedicatedServer()
        {
#if EXFIL_NETCODE
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null) manager.StartServer();
#endif
        }

        public void Shutdown()
        {
#if EXFIL_NETCODE
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening) manager.Shutdown();
#endif
        }

        /// <summary>Called by the raid flow: host spawns bots, clients just watch.</summary>
        public bool ShouldSpawnBots()
        {
            return IsAuthority;
        }
    }

#if EXFIL_NETCODE
    /// <summary>Client -> server input + server -> client state for one player.</summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        public NetworkVariable<uint> PlayerId = new NetworkVariable<uint>(0);
        public NetworkVariable<float> Health = new NetworkVariable<float>(100f);
        public NetworkVariable<byte> State = new NetworkVariable<byte>(0);

        public override void OnNetworkSpawn()
        {
            PlayerId.Value = OwnerClientId;
            if (IsOwner)
            {
                RaidManager raid = RaidManager.Instance;
                if (raid != null)
                {
                    Player.PlayerActor actor = GetComponent<Player.PlayerActor>();
                    raid.RegisterPlayer(actor);
                }
            }
        }

        [ServerRpc]
        public void ReportShotServerRpc(Vector3 origin, Vector3 direction, ServerRpcParams parameters = default)
        {
            if (!IsServer) return;
            GameEvents.Raise(new ShotFiredEvent
            {
                Origin = origin,
                Direction = direction,
                Loudness = 1f,
                ShooterId = parameters.Receive.SenderClientId
            });
        }

        [ClientRpc]
        public void ApplyHitClientRpc(BodyPart part, float damage, ClientRpcParams parameters = default)
        {
            Characters.HealthController health = GetComponent<Characters.HealthController>();
            if (health == null || !IsOwner) return;
            health.ApplyDamage(part, new Characters.DamageInfo { Type = DamageType.Bullet, Amount = damage });
        }
    }
#endif
}
