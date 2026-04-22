using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobby;
using Unity.Services.Lobby.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
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
    public event Action<string>             OnRoomCodeGenerated;   // host: 4-letter code ready
    public event Action                     OnOpponentConnected;   // both players in lobby
    public event Action<PokemonInstance[]>  OnOpponentTeamReceived;// opponent team arrived
    public event Action<int>               OnBattleSeedReceived;  // seed ready → start sim
    public event Action<string>             OnError;               // human-readable error

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsHost       { get; private set; }
    public bool IsConnected  { get; private set; }

    // Opponent's team set by MultiplayerBattleSync before battle scene loads.
    // BattleSceneManager reads this instead of calling EnemyGenerator.
    public PokemonInstance[] PendingOpponentTeam { get; set; }

    private Lobby  _lobby;
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
        NetworkManager.Singleton?.Shutdown();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public async Task SignInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    // Host: allocates relay, creates lobby, returns 4-letter room code.
    public async Task<string> HostGame()
    {
        try
        {
            await SignInAsync();

            var allocation   = await RelayService.Instance.CreateAllocationAsync(RelayMaxConns);
            _relayJoinCode   = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = true,
                Data = new Dictionary<string, DataObject>
                {
                    ["relayCode"] = new DataObject(DataObject.VisibilityOptions.Member, _relayJoinCode)
                }
            };

            _lobby  = await LobbyService.Instance.CreateLobbyAsync("PokemonBattle", MaxPlayers, lobbyOptions);
            IsHost  = true;

            // Room code = first 4 chars of lobby ID (uppercase, easy to type)
            string roomCode = _lobby.Id[..4].ToUpper();

            ConfigureRelayHost(allocation);
            NetworkManager.Singleton.StartHost();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

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
    public async Task JoinGame(string roomCode)
    {
        try
        {
            await SignInAsync();

            // Query lobbies for matching ID prefix
            var queryOptions = new QueryLobbiesOptions
            {
                Count   = 10,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };

            var results = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            Lobby matched = null;
            foreach (var l in results.Results)
            {
                if (l.Id[..4].ToUpper() == roomCode.ToUpper())
                { matched = l; break; }
            }

            if (matched == null)
            { OnError?.Invoke($"Room '{roomCode}' not found."); return; }

            _lobby = await LobbyService.Instance.JoinLobbyByIdAsync(matched.Id);
            IsHost = false;

            string relayCode = _lobby.Data["relayCode"].Value;
            var joinAlloc    = await RelayService.Instance.JoinAllocationAsync(relayCode);

            ConfigureRelayClient(joinAlloc);
            NetworkManager.Singleton.StartClient();

            Debug.Log($"[MP] Joined lobby {roomCode}");
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

    // Host calls this once both players press Start Battle.
    // Picks a seed, sends it and the opponent's team to the client.
    public void HostSendBattleData(PokemonInstance[] myTeam)
    {
        if (!IsHost) return;
        // Netcode RPC exchange handled by MultiplayerBattleSync component.
        // This just triggers it.
        MultiplayerBattleSync.Instance?.HostInitiateBattle(myTeam);
    }

    public void ClientSendTeam(PokemonInstance[] myTeam)
    {
        if (IsHost) return;
        MultiplayerBattleSync.Instance?.ClientSendTeam(myTeam);
    }

    // ── Relay helpers ──────────────────────────────────────────────────────

    private void ConfigureRelayHost(Allocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetHostRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData);
    }

    private void ConfigureRelayClient(JoinAllocation allocation)
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
        if (!IsHost) return;
        if (NetworkManager.Singleton.ConnectedClients.Count >= MaxPlayers)
        {
            IsConnected = true;
            OnOpponentConnected?.Invoke();
            Debug.Log("[MP] Opponent connected — lobby full.");
        }
    }
}
