using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// BattleManager orchestrates the battle simulation.
// Damage logic lives in DamageCalculator.
// Enemy generation lives in EnemyGenerator.

public class BattleManager : MonoBehaviour
{
    // --- Singleton ---
    public static BattleManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Maximum turns before the battle ends in a draw")]
    public int maxTurns = 20;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -------------------------------------------------------
    // BATTLE ENTRY POINT
    // -------------------------------------------------------

    public BattleResult RunBattle()
    {
        List<PokemonInstance> playerTeam = ShopManager.Instance.BattleRow
            .Where(p => p != null)
            .ToList();

        List<PokemonInstance> enemyTeam = EnemyGenerator.GenerateEnemyTeam();

        if (playerTeam.Count == 0)
        {
            Debug.Log("Battle: Player has no Pokemon in battle row — auto loss.");
            return BattleResult.PlayerLoss;
        }

        Debug.Log($"Battle start! Player: {playerTeam.Count} Pokemon | Enemy: {enemyTeam.Count} Pokemon");

        AbilitySystem.ResetBattleState();
        AbilitySystem.InitBattle(playerTeam, enemyTeam);
        AbilitySystem.FireBattleStart(playerTeam, enemyTeam);
        AbilitySystem.FireBattleStart(enemyTeam, playerTeam);

        for (int turn = 1; turn <= maxTurns; turn++)
        {
            Debug.Log($"--- Turn {turn} ---");

            AbilitySystem.FireRoundStart(playerTeam, enemyTeam);
            AbilitySystem.FireRoundStart(enemyTeam, playerTeam);

            PokemonInstance playerFront = GetFront(playerTeam);
            PokemonInstance enemyFront  = GetFront(enemyTeam);

            if (playerFront == null || enemyFront == null) break;

            RunTurn(playerFront, enemyFront, playerTeam, enemyTeam);

            RemoveFainted(playerTeam);
            RemoveFainted(enemyTeam);

            if (playerTeam.Count == 0 && enemyTeam.Count == 0)
            {
                Debug.Log("Battle result: Draw (both teams wiped on the same turn)");
                return BattleResult.Draw;
            }
            if (playerTeam.Count == 0)
            {
                Debug.Log("Battle result: Player loses!");
                return BattleResult.PlayerLoss;
            }
            if (enemyTeam.Count == 0)
            {
                Debug.Log("Battle result: Player wins!");
                return BattleResult.PlayerWin;
            }
        }

        Debug.Log($"Battle result: Draw (reached {maxTurns} turn limit)");
        return BattleResult.Draw;
    }

    // -------------------------------------------------------
    // TURN LOGIC
    // -------------------------------------------------------

    private void RunTurn(PokemonInstance playerFront, PokemonInstance enemyFront,
        List<PokemonInstance> playerTeam, List<PokemonInstance> enemyTeam)
    {
        bool playerGoesFirst;

        bool playerPriority = AbilitySystem.HasPriorityMove(playerFront);
        bool enemyPriority  = AbilitySystem.HasPriorityMove(enemyFront);

        if (playerPriority != enemyPriority)
            playerGoesFirst = playerPriority;
        else if (playerFront.speed != enemyFront.speed)
            playerGoesFirst = playerFront.speed > enemyFront.speed;
        else
            playerGoesFirst = AbilitySystem.RngNextBool();

        PokemonInstance first  = playerGoesFirst ? playerFront : enemyFront;
        PokemonInstance second = playerGoesFirst ? enemyFront  : playerFront;
        List<PokemonInstance> firstTeam  = playerGoesFirst ? playerTeam : enemyTeam;
        List<PokemonInstance> secondTeam = playerGoesFirst ? enemyTeam  : playerTeam;

        Debug.Log($"{first.baseData.pokemonName} (Speed {first.speed}) goes before {second.baseData.pokemonName} (Speed {second.speed})");

        DamageCalculator.Attack(first, second, firstTeam, secondTeam);

        if (second.currentHP > 0)
            DamageCalculator.Attack(second, first, secondTeam, firstTeam);
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    private PokemonInstance GetFront(List<PokemonInstance> team)
        => team.FirstOrDefault(p => p.currentHP > 0);

    private void RemoveFainted(List<PokemonInstance> team)
        => team.RemoveAll(p => p.currentHP <= 0);
}
