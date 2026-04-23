using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

// Handles ready-state sync, team exchange, and post-battle state sync.
// Uses CustomMessagingManager so no NetworkObject / NetworkBehaviour is required.
//
// Flow:
//   1. Player presses Start Battle → NotifyReady → host collects both teams.
//   2. UI shows "Opponent is ready!" when the other player locks in.
//   3. When both ready → host sends BattleData (team + seed) to client, starts locally.
//   4. Client receives BattleData → starts locally with host's team.
//   5. Host calls SyncPostBattle() → pushes authoritative state to client.

public class MultiplayerBattleSync : MonoBehaviour
{
    public static MultiplayerBattleSync Instance { get; private set; }

    public event Action<bool> OnOpponentReadyChanged;

    private PokemonInstance[] _myTeam;
    private PokemonInstance[] _clientTeam;
    private bool _hostReady;
    private bool _clientReady;
    private int  _clientHP;    // client's HP at time of ready — stored on host to compute post-battle state
    private int  _clientWins;  // client's wins at time of ready — stored on host

    // Client-side: buffer host result until local battle animation finishes.
    private bool _localBattleDone;
    private (BattleResult result, int hp, int wins, int round, bool heartRestored)? _pendingPostBattle;

    private const string MSG_NOTIFY_READY    = "MP_NotifyReady";
    private const string MSG_READY_STATE    = "MP_ReadyState";
    private const string MSG_BATTLE_DATA    = "MP_BattleData";
    private const string MSG_POST_BATTLE    = "MP_PostBattle";
    private const string MSG_OPPONENT_DEAD  = "MP_OpponentDead";

    // Set to true when the opponent's HP reaches 0 — checked by GameManager.ReturnToShop.
    public bool OpponentGameOver { get; private set; }

    public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Called by MultiplayerNetworkManager after StartHost / StartClient succeeds.
    public void RegisterHandlers()
    {
        var msg = NetworkManager.Singleton?.CustomMessagingManager;
        if (msg == null) { Debug.LogWarning("[MP Sync] CustomMessagingManager not available yet."); return; }
        msg.RegisterNamedMessageHandler(MSG_NOTIFY_READY,   OnReceiveNotifyReady);
        msg.RegisterNamedMessageHandler(MSG_READY_STATE,   OnReceiveReadyState);
        msg.RegisterNamedMessageHandler(MSG_BATTLE_DATA,   OnReceiveBattleData);
        msg.RegisterNamedMessageHandler(MSG_POST_BATTLE,   OnReceivePostBattle);
        msg.RegisterNamedMessageHandler(MSG_OPPONENT_DEAD, OnReceiveOpponentDead);
        Debug.Log("[MP Sync] Handlers registered.");
    }

    // ── Called when player presses Start Battle ────────────────────────────

    public void NotifyReady(PokemonInstance[] myTeam)
    {
        _myTeam = myTeam;

        if (IsHost)
        {
            _hostReady = true;
            BroadcastReadyState();
            if (_hostReady && _clientReady) DispatchBattle();
        }
        else
        {
            var cmm = NetworkManager.Singleton?.CustomMessagingManager;
            if (cmm == null) { Debug.LogError("[MP Sync] CustomMessagingManager unavailable — not connected?"); return; }
            var writer = new FastBufferWriter(512, Allocator.Temp, 65536);
            WriteTeam(ref writer, myTeam);
            writer.WriteValueSafe(GameManager.Instance?.PlayerHP    ?? 0);
            writer.WriteValueSafe(GameManager.Instance?.PlayerWins  ?? 0);
            cmm.SendNamedMessage(MSG_NOTIFY_READY, NetworkManager.ServerClientId, writer);
            writer.Dispose();
        }
    }

    private void OnReceiveNotifyReady(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsHost) return;
        _clientTeam  = ReadTeam(ref reader);
        reader.ReadValueSafe(out _clientHP);
        reader.ReadValueSafe(out _clientWins);
        _clientReady = true;
        BroadcastReadyState();
        if (_hostReady && _clientReady) DispatchBattle();
    }

    private void BroadcastReadyState()
    {
        using var writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe(_hostReady);
        writer.WriteValueSafe(_clientReady);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(MSG_READY_STATE, writer);
    }

    private void OnReceiveReadyState(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out bool hostReady);
        reader.ReadValueSafe(out bool clientReady);
        bool opponentReady = IsHost ? clientReady : hostReady;
        OnOpponentReadyChanged?.Invoke(opponentReady);
    }

    // ── Dispatch ───────────────────────────────────────────────────────────

    private void DispatchBattle()
    {
        int seed = new System.Random().Next(1, int.MaxValue);

        // Send host team + seed to all non-host clients.
        var writer = new FastBufferWriter(512, Allocator.Temp, 65536);
        WriteTeam(ref writer, _myTeam);
        writer.WriteValueSafe(seed);
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId) continue;
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MSG_BATTLE_DATA, clientId, writer);
        }
        writer.Dispose();

        StartBattleLocally(_clientTeam, seed);
    }

    private void OnReceiveBattleData(ulong senderClientId, FastBufferReader reader)
    {
        var hostTeam = ReadTeam(ref reader);
        reader.ReadValueSafe(out int seed);
        StartBattleLocally(hostTeam, seed);
    }

    private void StartBattleLocally(PokemonInstance[] opponentTeam, int seed)
    {
        _localBattleDone    = false;
        _pendingPostBattle  = null;
        MultiplayerNetworkManager.Instance.PendingOpponentTeam = opponentTeam;
        BattleSceneManager._pendingMultiplayerSeed = seed;
        GameManager.Instance?.StartBattle();
    }

    // Called by GameManager.OnBattleComplete on the client when the local animation finishes.
    public void OnLocalBattleFinished(BattleResult localResult)
    {
        _localBattleDone = true;
        if (_pendingPostBattle.HasValue)
        {
            var p = _pendingPostBattle.Value;
            _pendingPostBattle = null;
            GameManager.Instance?.MultiplayerApplyPostBattle(p.result, p.hp, p.wins, p.round, p.heartRestored);
        }
        // If host result hasn't arrived yet, we wait — OnReceivePostBattle will apply it once it does.
    }

    // ── Post-battle state sync (host → client) ─────────────────────────────

    public void SyncPostBattle(BattleResult hostSeesResult, int playerHP, int playerWins, int currentRound, bool heartRestored)
    {
        if (!IsHost) return;

        BattleResult clientResult = hostSeesResult == BattleResult.PlayerWin  ? BattleResult.PlayerLoss
                                  : hostSeesResult == BattleResult.PlayerLoss ? BattleResult.PlayerWin
                                  : BattleResult.Draw;

        // Compute the client's post-battle state from their pre-battle values (sent with NotifyReady).
        int maxHP = GameManager.Instance?.playerMaxHP ?? 7;
        int newClientWins = clientResult == BattleResult.PlayerWin  ? _clientWins + 1 : _clientWins;
        int newClientHP   = clientResult == BattleResult.PlayerLoss ? Mathf.Max(_clientHP - 1, 0) : _clientHP;

        // Apply the same tier-upgrade heart restoration check for the client.
        bool clientHeartRestored = false;
        int tierBefore = ShopManager.Instance?.GetTierForRound(currentRound)   ?? 0;
        int tierAfter  = ShopManager.Instance?.GetTierForRound(currentRound + 1) ?? 0;
        if (tierAfter > tierBefore && (tierAfter == 2 || tierAfter == 4) && newClientHP < maxHP)
        {
            newClientHP++;
            clientHeartRestored = true;
        }

        using var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((int)clientResult);
        writer.WriteValueSafe(newClientHP);
        writer.WriteValueSafe(newClientWins);
        writer.WriteValueSafe(currentRound);
        writer.WriteValueSafe(clientHeartRestored);

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId) continue;
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MSG_POST_BATTLE, clientId, writer);
        }

        if (newClientHP <= 0)
            OpponentGameOver = true;

        _hostReady = _clientReady = false;
        _myTeam = _clientTeam = null;
        _clientHP = _clientWins = 0;
    }

    private void OnReceivePostBattle(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int clientResult);
        reader.ReadValueSafe(out int playerHP);
        reader.ReadValueSafe(out int playerWins);
        reader.ReadValueSafe(out int currentRound);
        reader.ReadValueSafe(out bool heartRestored);

        _hostReady = _clientReady = false;
        _myTeam = _clientTeam = null;

        var result = (BattleResult)clientResult;
        if (_localBattleDone)
        {
            _localBattleDone = false;
            GameManager.Instance?.MultiplayerApplyPostBattle(result, playerHP, playerWins, currentRound, heartRestored);
        }
        else
        {
            // Local battle animation still running — buffer until it finishes.
            _pendingPostBattle = (result, playerHP, playerWins, currentRound, heartRestored);
        }
    }

    // ── Opponent game-over notification ───────────────────────────────────────

    // Called from GameManager.EnterGameOver so the opponent learns they won.
    public void SendOpponentDead()
    {
        var cmm = NetworkManager.Singleton?.CustomMessagingManager;
        if (cmm == null) return;
        using var writer = new FastBufferWriter(1, Allocator.Temp);
        if (IsHost)
        {
            // Host → all clients
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.Singleton.LocalClientId) continue;
                cmm.SendNamedMessage(MSG_OPPONENT_DEAD, clientId, writer);
            }
        }
        else
        {
            // Client → server only
            cmm.SendNamedMessage(MSG_OPPONENT_DEAD, NetworkManager.ServerClientId, writer);
        }
    }

    private void OnReceiveOpponentDead(ulong senderClientId, FastBufferReader reader)
    {
        OpponentGameOver = true;
        // The player is in the shop — show the victory overlay immediately.
        GameManager.Instance?.EnterOpponentDiedVictory();
    }

    // ── Serialization ──────────────────────────────────────────────────────

    private static void WriteTeam(ref FastBufferWriter writer, PokemonInstance[] team)
    {
        // Count non-null slots only (unfilled battle slots are null).
        int len = 0;
        if (team != null) foreach (var p in team) if (p != null) len++;
        writer.WriteValueSafe(len);
        if (team == null) return;
        foreach (var p in team)
        {
            if (p == null) continue;
            writer.WriteValueSafe(p.baseData.id);
            writer.WriteValueSafe(p.currentHP);
            writer.WriteValueSafe(p.baseMaxHP);
            writer.WriteValueSafe(p.baseAttack);
            writer.WriteValueSafe(p.baseSpeed);
        }
    }

    private static PokemonInstance[] ReadTeam(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out int len);
        var db   = Resources.Load<PokemonDatabase>("PokemonDatabase");
        var team = new PokemonInstance[len];
        for (int i = 0; i < len; i++)
        {
            reader.ReadValueSafe(out int pokemonId);
            reader.ReadValueSafe(out int currentHP);
            reader.ReadValueSafe(out int baseMaxHP);
            reader.ReadValueSafe(out int baseAttack);
            reader.ReadValueSafe(out int baseSpeed);

            var data = db?.GetById(pokemonId);
            if (data == null) { Debug.LogError($"[MP] Unknown pokemonId {pokemonId}"); continue; }
            var inst       = new PokemonInstance(data);
            inst.currentHP  = currentHP;
            inst.baseMaxHP  = baseMaxHP;
            inst.baseAttack = baseAttack;
            inst.baseSpeed  = baseSpeed;
            // Spread() randomized live stats on construction — overwrite with actual transferred values.
            inst.maxHP      = baseMaxHP;
            inst.attack     = baseAttack;
            inst.speed      = baseSpeed;
            team[i]         = inst;
        }
        return team;
    }
}
