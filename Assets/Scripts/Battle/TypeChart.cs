using System.Collections.Generic;

// TypeChart handles Pokemon type effectiveness.
// Returns a damage multiplier given an attacking type and a defending type.
// Dual-type defenders multiply both type matchups together.
// Multipliers: 2 = super effective, 0.5 = not very effective, 0 = immune, 1 = neutral

public static class TypeChart
{
    // Lookup table: attackingType -> defendingType -> multiplier
    // Only non-neutral (non-1x) matchups are listed. Everything else defaults to 1x.
    private static readonly Dictionary<string, Dictionary<string, float>> chart =
        new Dictionary<string, Dictionary<string, float>>
    {
        ["Normal"] = new Dictionary<string, float>
        {
            ["Rock"]  = 0.5f,
            ["Ghost"] = 0f
        },
        ["Fire"] = new Dictionary<string, float>
        {
            ["Fire"]   = 0.5f,
            ["Water"]  = 0.5f,
            ["Grass"]  = 2f,
            ["Ice"]    = 2f,
            ["Bug"]    = 2f,
            ["Rock"]   = 0.5f,
            ["Dragon"] = 0.5f
        },
        ["Water"] = new Dictionary<string, float>
        {
            ["Fire"]   = 2f,
            ["Water"]  = 0.5f,
            ["Grass"]  = 0.5f,
            ["Ground"] = 2f,
            ["Rock"]   = 2f,
            ["Dragon"] = 0.5f
        },
        ["Grass"] = new Dictionary<string, float>
        {
            ["Fire"]   = 0.5f,
            ["Water"]  = 2f,
            ["Grass"]  = 0.5f,
            ["Poison"] = 0.5f,
            ["Ground"] = 2f,
            ["Flying"] = 0.5f,
            ["Bug"]    = 0.5f,
            ["Rock"]   = 2f,
            ["Dragon"] = 0.5f
        },
        ["Electric"] = new Dictionary<string, float>
        {
            ["Water"]    = 2f,
            ["Electric"] = 0.5f,
            ["Grass"]    = 0.5f,
            ["Ground"]   = 0f,
            ["Flying"]   = 2f,
            ["Dragon"]   = 0.5f
        },
        ["Ice"] = new Dictionary<string, float>
        {
            ["Water"]  = 0.5f,
            ["Grass"]  = 2f,
            ["Ice"]    = 0.5f,
            ["Ground"] = 2f,
            ["Flying"] = 2f,
            ["Dragon"] = 2f
        },
        ["Fighting"] = new Dictionary<string, float>
        {
            ["Normal"]  = 2f,
            ["Ice"]     = 2f,
            ["Poison"]  = 0.5f,
            ["Flying"]  = 0.5f,
            ["Psychic"] = 0.5f,
            ["Bug"]     = 0.5f,
            ["Rock"]    = 2f,
            ["Ghost"]   = 0f
        },
        ["Poison"] = new Dictionary<string, float>
        {
            ["Grass"]  = 2f,
            ["Poison"] = 0.5f,
            ["Ground"] = 0.5f,
            ["Bug"]    = 2f,
            ["Rock"]   = 0.5f,
            ["Ghost"]  = 0.5f
        },
        ["Ground"] = new Dictionary<string, float>
        {
            ["Fire"]     = 2f,
            ["Electric"] = 2f,
            ["Grass"]    = 0.5f,
            ["Poison"]   = 2f,
            ["Flying"]   = 0f,
            ["Bug"]      = 0.5f,
            ["Rock"]     = 2f
        },
        ["Flying"] = new Dictionary<string, float>
        {
            ["Electric"] = 0.5f,
            ["Grass"]    = 2f,
            ["Fighting"] = 2f,
            ["Bug"]      = 2f,
            ["Rock"]     = 0.5f
        },
        ["Psychic"] = new Dictionary<string, float>
        {
            ["Fighting"] = 2f,
            ["Poison"]   = 2f,
            ["Psychic"]  = 0.5f,
            ["Ghost"]    = 0f    // Gen 1: Ghost is immune to Psychic
        },
        ["Bug"] = new Dictionary<string, float>
        {
            ["Fire"]     = 0.5f,
            ["Grass"]    = 2f,
            ["Fighting"] = 0.5f,
            ["Flying"]   = 0.5f,
            ["Psychic"]  = 2f,
            ["Ghost"]    = 0.5f
        },
        ["Rock"] = new Dictionary<string, float>
        {
            ["Fire"]     = 2f,
            ["Ice"]      = 2f,
            ["Fighting"] = 0.5f,
            ["Ground"]   = 0.5f,
            ["Flying"]   = 2f,
            ["Bug"]      = 2f
        },
        ["Ghost"] = new Dictionary<string, float>
        {
            ["Normal"]  = 0f,
            ["Psychic"] = 0f,   // Gen 1 bug: Ghost should be 2x vs Psychic but was coded as immune
            ["Ghost"]   = 2f
        },
        ["Dragon"] = new Dictionary<string, float>
        {
            ["Dragon"] = 2f
        }
    };

    // Returns the damage multiplier for an attack of attackType hitting a Pokemon
    // with type1 and type2. Pass "" for type2 if the defender is single-type.
    public static float GetMultiplier(string attackType, string defenderType1, string defenderType2)
    {
        float multiplier = GetSingleMultiplier(attackType, defenderType1);

        // If defender has a second type, multiply again
        if (!string.IsNullOrEmpty(defenderType2))
            multiplier *= GetSingleMultiplier(attackType, defenderType2);

        return multiplier;
    }

    // Returns the multiplier for a single type matchup
    private static float GetSingleMultiplier(string attackType, string defendType)
    {
        if (string.IsNullOrEmpty(attackType) || string.IsNullOrEmpty(defendType))
            return 1f;

        if (chart.TryGetValue(attackType, out var defMap))
            if (defMap.TryGetValue(defendType, out float value))
                return value;

        return 1f; // Default: neutral damage
    }
}
