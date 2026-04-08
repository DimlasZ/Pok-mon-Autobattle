using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Generates the enemy team for a battle based on the current round.
// Team size matches the player's active battle row size.
// Tier pool matches the shop: tier 1 up to the current max tier for the round.

public static class EnemyGenerator
{
    public static List<PokemonInstance> GenerateEnemyTeam()
    {
        int round    = GameManager.Instance.CurrentRound;
        int maxTier  = ShopManager.Instance.GetTierForRound(round);
        int teamSize = ShopManager.Instance.BattleSize;

        List<PokemonData> available = ShopManager.Instance.allPokemon
            .Where(p => p.tier > 0 && p.tier <= maxTier)
            .ToList();

        List<PokemonInstance> enemyTeam = new List<PokemonInstance>();

        for (int i = 0; i < teamSize; i++)
        {
            if (available.Count == 0)
            {
                Debug.LogWarning($"EnemyGenerator: No Pokemon available up to tier {maxTier} (round {round})");
                break;
            }
            PokemonData picked = available[Random.Range(0, available.Count)];
            enemyTeam.Add(new PokemonInstance(picked));
            Debug.Log($"Enemy slot {i}: {picked.pokemonName} (Tier {picked.tier})");
        }

        return enemyTeam;
    }
}
