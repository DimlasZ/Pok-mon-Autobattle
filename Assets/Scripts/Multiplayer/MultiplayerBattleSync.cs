using System;
using UnityEngine;
using Unity.Netcode;

// Sits on the NetworkManager GameObject.
// Handles ready-state sync, team exchange, shared RNG seed, and post-battle state sync.
//
// Flow:
//   1. Player presses Start Battle → NotifyReadyServerRpc → host broadcasts ready states.
//   2. UI shows "Opponent is ready!" when the other player locks in.
//   3. When both are ready → host exchanges teams + seed via ClientRpc.
//   4. Both sides run BattleSimulator with the shared seed (identical result guaranteed).
//   5. Host calls SyncPostBattle() → pushes authoritative game state to client.
//   6. Client applies state, both return to shop.

public class MultiplayerBattleSync : NetworkBehaviour
{
    public static MultiplayerBattleSync Instance { get; private set; }

    // Fired on both clients when the opponent locks in (true = opponent ready).
    public event Action<bool> OnOpponentReadyChanged;
    public event Action<PokemonInstance[], int> OnBattleReady; // (opponentTeam, seed)

    private PokemonInstance[] _myTeam;
    private PokemonInstance[] _clientTeam;
    private bool _hostReady;
    private bool _clientReady;

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    // ── Called when player presses Start Battle ────────────────────────────

    public void NotifyReady(PokemonInstance[] myTeam)
    {
        _myTeam = myTeam;
        NotifyReadyServerRpc(SerializeTeam(myTeam));
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyReadyServerRpc(NetworkTeam team, ServerRpcParams rpc = default)
    {
        bool senderIsHost = rpc.Receive.SenderClientId == NetworkManager.ServerClientId;

        if (senderIsHost)
        {
            _clientTeam  = DeserializeTeam(team); // confusingly named — this is host's team stored for dispatch
            _hostReady   = true;
            // Tell client that host is ready
            UpdateReadyStateClientRpc(hostReady: true, clientReady: _clientReady);
        }
        else
        {
            _clientTeam = DeserializeTeam(team);
            _clientReady = true;
            // Tell everyone the new state
            UpdateReadyStateClientRpc(hostReady: _hostReady, clientReady: true);
        }

        if (_hostReady && _clientReady)
            DispatchBattle();
    }

    [ClientRpc]
    private void UpdateReadyStateClientRpc(bool hostReady, bool clientReady)
    {
        // Each client fires the event with whether their *opponent* is ready.
        bool opponentReady = IsHost ? clientReady : hostReady;
        OnOpponentReadyChanged?.Invoke(opponentReady);
    }

    // ── Dispatch ───────────────────────────────────────────────────────────

    private void DispatchBattle()
    {
        // Use System.Random for the seed pick — keeps it off UnityEngine.Random
        int seed = new System.Random().Next(1, int.MaxValue);
        // _myTeam = host team, _clientTeam = client team
        BattleDataClientRpc(SerializeTeam(_myTeam), seed);
        StartBattleLocally(_clientTeam, seed); // host fires locally
    }

    [ClientRpc]
    private void BattleDataClientRpc(NetworkTeam hostTeam, int seed)
    {
        if (IsHost) return;
        StartBattleLocally(DeserializeTeam(hostTeam), seed);
    }

    private void StartBattleLocally(PokemonInstance[] opponentTeam, int seed)
    {
        // Store opponent team so GameManager can use it instead of EnemyGenerator
        MultiplayerNetworkManager.Instance.PendingOpponentTeam = opponentTeam;
        BattleSceneManager._pendingMultiplayerSeed = seed;
        OnBattleReady?.Invoke(opponentTeam, seed);
        GameManager.Instance?.StartBattle();
    }

    // ── Post-battle state sync (host → client) ─────────────────────────────
    // Called by BattleSceneManager on the host after OnBattleComplete().
    // Pushes the authoritative HP/wins/round to the client so both sides stay in sync.

    public void SyncPostBattle(BattleResult hostSeesResult, int playerHP, int playerWins, int currentRound, bool heartRestored)
    {
        if (!IsHost) return;
        // Client sees the mirror result: if host won, client lost, and vice versa.
        BattleResult clientResult = hostSeesResult == BattleResult.PlayerWin  ? BattleResult.PlayerLoss
                                  : hostSeesResult == BattleResult.PlayerLoss ? BattleResult.PlayerWin
                                  : BattleResult.Draw;
        PostBattleClientRpc((int)hostSeesResult, (int)clientResult, playerHP, playerWins, currentRound, heartRestored);

        // Reset ready flags for next round
        _hostReady   = false;
        _clientReady = false;
        _myTeam      = null;
        _clientTeam  = null;
    }

    [ClientRpc]
    private void PostBattleClientRpc(int hostResult, int clientResult, int playerHP, int playerWins, int currentRound, bool heartRestored)
    {
        // Reset ready flags on client too
        _hostReady   = false;
        _clientReady = false;
        _myTeam      = null;
        _clientTeam  = null;

        if (IsHost) return; // host already handled via SyncPostBattle caller

        var gm = GameManager.Instance;
        if (gm == null) return;

        // Apply authoritative state from host
        gm.MultiplayerApplyPostBattle((BattleResult)clientResult, playerHP, playerWins, currentRound, heartRestored);
    }

    // ── Serialization ──────────────────────────────────────────────────────
    // PokemonInstance is not a NetworkBehaviour, so we flatten it to a struct.

    private static NetworkTeam SerializeTeam(PokemonInstance[] team)
    {
        var nt = new NetworkTeam { slots = new NetworkPokemon[team.Length] };
        for (int i = 0; i < team.Length; i++)
        {
            var p = team[i];
            nt.slots[i] = new NetworkPokemon
            {
                pokemonId   = p.baseData.id,
                currentHP   = p.currentHP,
                baseMaxHP   = p.baseMaxHP,
                baseAttack  = p.baseAttack,
                baseSpeed   = p.baseSpeed,
            };
        }
        return nt;
    }

    private static PokemonInstance[] DeserializeTeam(NetworkTeam nt)
    {
        var db = Resources.Load<PokemonDatabase>("PokemonDatabase");
        var team = new PokemonInstance[nt.slots.Length];
        for (int i = 0; i < nt.slots.Length; i++)
        {
            var slot = nt.slots[i];
            var data = db?.GetById(slot.pokemonId);
            if (data == null) { Debug.LogError($"[MP] Unknown pokemonId {slot.pokemonId}"); continue; }

            var inst         = new PokemonInstance(data);
            inst.currentHP   = slot.currentHP;
            inst.baseMaxHP   = slot.baseMaxHP;
            inst.baseAttack  = slot.baseAttack;
            inst.baseSpeed   = slot.baseSpeed;
            team[i]          = inst;
        }
        return team;
    }
}

// ── Network-safe structs ───────────────────────────────────────────────────

public struct NetworkTeam : INetworkSerializable
{
    public NetworkPokemon[] slots;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int len = slots?.Length ?? 0;
        serializer.SerializeValue(ref len);
        if (serializer.IsReader) slots = new NetworkPokemon[len];
        for (int i = 0; i < len; i++)
            serializer.SerializeValue(ref slots[i]);
    }
}

public struct NetworkPokemon : INetworkSerializable
{
    public int   pokemonId;
    public int   currentHP;
    public int   baseMaxHP;
    public int   baseAttack;
    public int   baseSpeed;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref pokemonId);
        serializer.SerializeValue(ref currentHP);
        serializer.SerializeValue(ref baseMaxHP);
        serializer.SerializeValue(ref baseAttack);
        serializer.SerializeValue(ref baseSpeed);
    }
}
