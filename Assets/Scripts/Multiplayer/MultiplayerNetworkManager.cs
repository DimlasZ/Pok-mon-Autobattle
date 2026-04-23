using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using LobbyModel      = Unity.Services.Lobbies.Models.Lobby;
using LobbyOptions    = Unity.Services.Lobbies.CreateLobbyOptions;
using LobbyData       = Unity.Services.Lobbies.Models.DataObject;
using QueryOptions    = Unity.Services.Lobbies.QueryLobbiesOptions;
using QueryFilterOpt  = Unity.Services.Lobbies.Models.QueryFilter;
using Unity.Services.Relay;
using RelayAllocation     = Unity.Services.Relay.Models.Allocation;
using RelayJoinAllocation = Unity.Services.Relay.Models.JoinAllocation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

// Manages the full multiplayer session lifecycle:
//   Sign in → Host/Join lobby → Relay connection → Team exchange → Seed sync
//
// Usage:
//   await MultiplayerNetworkManager.Instance.HostGame();   // returns room code
//   await MultiplayerNetworkManager.Instance.JoinGame(code);
//
// Subscribe to events to drive UI and game flow.

public class MultiplayerNetworkManager : MonoBehaviour
{
    public static MultiplayerNetworkManager Instance { get; private set; }

    // ── Events ─────────────────────────────────────────────────────────────
    public event Action<string> OnRoomCodeGenerated; // host: 4-letter code ready
    public event Action         OnOpponentConnected; // both players in lobby
    public event Action<string> OnError;             // human-readable error

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsHost       { get; private set; }
    public bool IsConnected  { get; private set; }

    // Opponent's team set by MultiplayerBattleSync before battle scene loads.
    // BattleSceneManager reads this instead of calling EnemyGenerator.
    public PokemonInstance[] PendingOpponentTeam { get; set; }

    private LobbyModel _lobby;
    private string _relayJoinCode;

    private const int MaxPlayers    = 2;
    private const int RelayMaxConns = 1; // host + 1 client

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public async Task SignInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
#if UNITY_EDITOR
            // ParrelSync clones share the same project, so we need a different auth profile
            // per editor instance to avoid "already a member" lobby errors.
            if (ParrelSync.ClonesManager.IsClone())
                AuthenticationService.Instance.SwitchProfile("Clone");
#endif
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    // Host: allocates relay, creates lobby, returns 4-letter room code.
    public async Task<string> HostGame()
    {
        try
        {
            await SignInAsync();

            var allocation   = await RelayService.Instance.CreateAllocationAsync(RelayMaxConns);
            _relayJoinCode   = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var lobbyOptions = new LobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, LobbyData>
                {
                    ["relayCode"] = new LobbyData(LobbyData.VisibilityOptions.Member, _relayJoinCode)
                }
            };

            _lobby  = await LobbyService.Instance.CreateLobbyAsync("PokemonBattle", MaxPlayers, lobbyOptions);
            IsHost  = true;

            // Room code = first 4 chars of lobby ID (uppercase, easy to type)
            string roomCode = _lobby.Id[..4].ToUpper();

            ConfigureRelayHost(allocation);
            NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += cid => Debug.Log($"[MP Host] OnClientDisconnect cid:{cid}");
            NetworkManager.Singleton.OnTransportFailure         += ()  => Debug.Log("[MP Host] TransportFailure");
            bool started = NetworkManager.Singleton.StartHost();
            Debug.Log($"[MP Host] StartHost returned:{started}");
            MultiplayerBattleSync.Instance?.RegisterHandlers();

            OnRoomCodeGenerated?.Invoke(roomCode);
            Debug.Log($"[MP] Hosting — room code: {roomCode}");
            return roomCode;
        }
        catch (Exception e)
        {
            OnError?.Invoke($"Failed to host: {e.Message}");
            throw;
        }
    }

    // Client: looks up lobby by 4-letter code, retrieves relay join code, connects.
    // Returns true if StartClient was called successfully; false if room not found.
    public async Task<bool> JoinGame(string roomCode)
    {
        try
        {
            Debug.Log($"[MP] JoinGame called — code:'{roomCode}'");
            await SignInAsync();
            Debug.Log("[MP] SignIn OK, querying lobbies...");

            // Query lobbies for matching ID prefix
            var queryOptions = new QueryOptions
            {
                Count   = 10,
                Filters = new List<QueryFilterOpt>
                {
                    new QueryFilterOpt(QueryFilterOpt.FieldOptions.AvailableSlots, "0", QueryFilterOpt.OpOptions.GT)
                }
            };

            var results = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            Debug.Log($"[MP] Query returned {results.Results.Count} lobbies");
            LobbyModel matched = null;
            foreach (var l in results.Results)
            {
                Debug.Log($"[MP] Lobby id:{l.Id} prefix:{l.Id[..4].ToUpper()}");
                if (l.Id[..4].ToUpper() == roomCode.ToUpper())
                { matched = l; break; }
            }

            if (matched == null)
            { OnError?.Invoke($"Room '{roomCode}' not found."); return false; }

            _lobby = await LobbyService.Instance.JoinLobbyByIdAsync(matched.Id);
            IsHost = false;

            string relayCode = _lobby.Data["relayCode"].Value;
            var joinAlloc    = await RelayService.Instance.JoinAllocationAsync(relayCode);

            ConfigureRelayClient(joinAlloc);

            NetworkManager.Singleton.OnClientConnectedCallback    += cid => {
                Debug.Log($"[MP Client] OnClientConnected cid:{cid}");
                IsConnected = true;
                OnOpponentConnected?.Invoke();
            };
            NetworkManager.Singleton.OnClientDisconnectCallback   += cid => Debug.Log($"[MP Client] OnClientDisconnect cid:{cid}");
            NetworkManager.Singleton.OnTransportFailure           += ()  => Debug.Log("[MP Client] TransportFailure");

            bool started = NetworkManager.Singleton.StartClient();
            Debug.Log($"[MP] StartClient returned:{started} — relay:{relayCode}");
            if (started) MultiplayerBattleSync.Instance?.RegisterHandlers();
            return started;
        }
        catch (Exception e)
        {
            OnError?.Invoke($"Failed to join: {e.Message}");
            throw;
        }
    }

    public void Disconnect()
    {
        NetworkManager.Singleton?.Shutdown();
        IsConnected = false;

        if (_lobby != null && IsHost)
            LobbyService.Instance.DeleteLobbyAsync(_lobby.Id);

        _lobby = null;
    }

    // ── Team & Seed Exchange (called after both players are in the battle scene) ──

    // ── Relay helpers ──────────────────────────────────────────────────────

    private void ConfigureRelayHost(RelayAllocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetHostRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData);
    }

    private void ConfigureRelayClient(RelayJoinAllocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetClientRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData);
    }

    // ── Callbacks ──────────────────────────────────────────────────────────

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[MP] OnClientConnected fired — clientId:{clientId} IsHost:{IsHost} LocalId:{NetworkManager.Singleton.LocalClientId}");
        if (!IsHost) return;
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        IsConnected = true;
        OnOpponentConnected?.Invoke();
        Debug.Log($"[MP] Opponent connected (clientId {clientId}).");
    }
}
