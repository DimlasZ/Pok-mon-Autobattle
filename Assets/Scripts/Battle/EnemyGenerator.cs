using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Generates the enemy team by randomly selecting a pre-simulated team from enemy_teams.csv.
// Teams are keyed by Round so each game round gets its own pool.

public static class EnemyGenerator
{
    // Parsed CSV rows, keyed by round number. Loaded once and cached.
    private static Dictionary<int, List<string[]>> _teamsByRound;

    // Column indices in enemy_teams.csv (Round,Tier,Slot0,Slot1,Slot2,Slot3,Slot4,Slot5,...)
    private const int ColRound = 0;
    private const int ColSlot0 = 2;
    private const int ColSlot5 = 7;

    public static List<PokemonInstance> GenerateEnemyTeam()
    {
        if (_teamsByRound == null)
            LoadTeams();

        int round = GameManager.Instance.CurrentRound;

        string[] row = PickTeamRow(round);

        if (row == null)
        {
            int maxTier = ShopManager.Instance.GetTierForRound(round);
            Debug.LogWarning($"EnemyGenerator: No pre-simulated teams found for round {round}. Falling back to random.");
            return GenerateRandom(maxTier);
        }

        Dictionary<string, PokemonData> lookup = ShopManager.Instance.AllPokemon
            .Where(p => p != null)
            .ToDictionary(p => p.pokemonName, p => p);

        List<PokemonInstance> enemyTeam = new List<PokemonInstance>();

        for (int col = ColSlot0; col <= ColSlot5; col++)
        {
            if (col >= row.Length) break;
            string name = row[col].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            if (lookup.TryGetValue(name, out PokemonData data))
            {
                enemyTeam.Add(new PokemonInstance(data));
                Debug.Log($"Enemy slot {enemyTeam.Count - 1}: {name} (Tier {data.tier})");
            }
            else
            {
                Debug.LogWarning($"EnemyGenerator: Pokemon '{name}' not found in AllPokemon.");
            }
        }

        return enemyTeam;
    }

    private static string[] PickTeamRow(int round)
    {
        int maxRound = _teamsByRound.Keys.Max();
        int clampedRound = Mathf.Min(round, maxRound);

        if (!_teamsByRound.TryGetValue(clampedRound, out List<string[]> pool) || pool.Count == 0)
            return null;

        return pool[Random.Range(0, pool.Count)];
    }

    private static void LoadTeams()
    {
        _teamsByRound = new Dictionary<int, List<string[]>>();

        TextAsset csv = Resources.Load<TextAsset>("Data/Teams/enemy_teams");
        if (csv == null)
        {
            Debug.LogError("EnemyGenerator: Could not load Data/Teams/enemy_teams from Resources.");
            return;
        }

        string[] lines = csv.text.Split('\n');
        // Skip header (line 0)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = line.Split(',');
            if (cols.Length < ColSlot5 + 1) continue;

            if (!int.TryParse(cols[ColRound].Trim(), out int round)) continue;

            if (!_teamsByRound.ContainsKey(round))
                _teamsByRound[round] = new List<string[]>();

            _teamsByRound[round].Add(cols);
        }

        int total = _teamsByRound.Values.Sum(v => v.Count);
        Debug.Log($"EnemyGenerator: Loaded {total} pre-simulated teams across {_teamsByRound.Count} rounds.");
    }

    // Fallback: original random generation (used if CSV is missing or empty)
    private static List<PokemonInstance> GenerateRandom(int maxTier)
    {
        int teamSize = ShopManager.Instance.BattleSize;

        List<PokemonData> available = ShopManager.Instance.AllPokemon
            .Where(p => p != null && p.tier == maxTier)
            .ToList();

        // Widen pool if exact tier has no Pokémon
        if (available.Count == 0)
            available = ShopManager.Instance.AllPokemon
                .Where(p => p != null && p.tier > 0 && p.tier <= maxTier)
                .ToList();

        List<PokemonInstance> enemyTeam = new List<PokemonInstance>();

        for (int i = 0; i < teamSize; i++)
        {
            if (available.Count == 0) break;
            PokemonData picked = available[Random.Range(0, available.Count)];
            enemyTeam.Add(new PokemonInstance(picked));
        }

        return enemyTeam;
    }
}
