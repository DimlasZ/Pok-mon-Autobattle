using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Generates the enemy team by randomly selecting a pre-simulated team from enemy_teams.csv.
// Teams are filtered to the current max tier so difficulty scales with round progression.

public static class EnemyGenerator
{
    // Parsed CSV rows, keyed by tier. Loaded once and cached.
    private static Dictionary<int, List<string[]>> _teamsByTier;

    // Column indices in enemy_teams.csv
    private const int ColTier  = 0;
    private const int ColSlot0 = 1;
    private const int ColSlot5 = 6; // last slot column (inclusive)

    public static List<PokemonInstance> GenerateEnemyTeam()
    {
        if (_teamsByTier == null)
            LoadTeams();

        int round = GameManager.Instance.CurrentRound;

        string[] row = PickTeamRow(round);

        if (row == null)
        {
            int maxTier = ShopManager.Instance.GetTierForRound(round);
            Debug.LogWarning($"EnemyGenerator: No pre-simulated teams found for round {round}. Falling back to random.");
            return GenerateRandom(maxTier);
        }

        // Build a lookup from name -> PokemonData
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

    // Selects a team row based on round-based difficulty scaling.
    //
    // Round pattern (2 rounds per tier):
    //   Odd  rounds (1, 3, 5, …) → bottom 500 of the current tier (indices 500–999)
    //   Even rounds (2, 4, 6, …) → full 1000 of the current tier  (indices   0–999)
    //
    //   Round 1 → Tier 1 bottom 500
    //   Round 2 → Tier 1 full 1000
    //   Round 3 → Tier 2 bottom 500
    //   Round 4 → Tier 2 full 1000
    //   Round 5 → Tier 3 bottom 500  … etc.
    private static string[] PickTeamRow(int round)
    {
        if (round % 2 == 1)
        {
            // Odd round — bottom 500 of the current tier
            int tier = (round + 1) / 2;
            return PickFromRange(tier, 500, 999);
        }
        else
        {
            // Even round — full 1000 of the current tier
            int tier = round / 2;
            return PickFromRange(tier, 0, 999);
        }
    }

    private static string[] PickFromRange(int tier, int indexMin, int indexMax)
    {
        if (!_teamsByTier.TryGetValue(tier, out List<string[]> pool) || pool.Count == 0)
            return null;

        int lo = Mathf.Min(indexMin, pool.Count - 1);
        int hi = Mathf.Min(indexMax, pool.Count - 1);
        if (lo > hi) return null;

        return pool[Random.Range(lo, hi + 1)];
    }

    private static void LoadTeams()
    {
        _teamsByTier = new Dictionary<int, List<string[]>>();

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
            if (cols.Length < 2) continue;

            if (!int.TryParse(cols[ColTier].Trim(), out int tier)) continue;

            if (!_teamsByTier.ContainsKey(tier))
                _teamsByTier[tier] = new List<string[]>();

            _teamsByTier[tier].Add(cols);
        }

        int total = _teamsByTier.Values.Sum(v => v.Count);
        Debug.Log($"EnemyGenerator: Loaded {total} pre-simulated teams across {_teamsByTier.Count} tiers.");
    }

    // Fallback: original random generation (used if CSV is missing or empty)
    private static List<PokemonInstance> GenerateRandom(int maxTier)
    {
        int teamSize = ShopManager.Instance.BattleSize;

        List<PokemonData> available = ShopManager.Instance.AllPokemon
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
