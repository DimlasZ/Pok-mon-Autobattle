using System.Collections.Generic;

// TypeChart handles Pokemon type effectiveness.
// Returns a damage multiplier given an attacking type and a defending type.
// Dual-type defenders multiply both type matchups together.
// Multipliers: 1.5 = super effective, 0.75 = not very effective, 0 = immune, 1 = neutral

public static class TypeChart
{
    // Lookup table: attackingType -> defendingType -> multiplier
    // Only non-neutral (non-1x) matchups are listed. Everything else defaults to 1x.
    private static readonly Dictionary<string, Dictionary<string, float>> chart =
        new Dictionary<string, Dictionary<string, float>>
    {
        ["Normal"] = new Dictionary<string, float>
        {
            ["Rock"]  = 0.75f,
            ["Ghost"] = 0f
        },
        ["Fire"] = new Dictionary<string, float>
        {
            ["Fire"]   = 0.75f,
            ["Water"]  = 0.75f,
            ["Grass"]  = 1.5f,
            ["Ice"]    = 1.5f,
            ["Bug"]    = 1.5f,
            ["Rock"]   = 0.75f,
            ["Dragon"] = 0.75f
        },
        ["Water"] = new Dictionary<string, float>
        {
            ["Fire"]   = 1.5f,
            ["Water"]  = 0.75f,
            ["Grass"]  = 0.75f,
            ["Ground"] = 1.5f,
            ["Rock"]   = 1.5f,
            ["Dragon"] = 0.75f
        },
        ["Grass"] = new Dictionary<string, float>
        {
            ["Fire"]   = 0.75f,
            ["Water"]  = 1.5f,
            ["Grass"]  = 0.75f,
            ["Poison"] = 0.75f,
            ["Ground"] = 1.5f,
            ["Flying"] = 0.75f,
            ["Bug"]    = 0.75f,
            ["Rock"]   = 1.5f,
            ["Dragon"] = 0.75f
        },
        ["Electric"] = new Dictionary<string, float>
        {
            ["Water"]    = 1.5f,
            ["Electric"] = 0.75f,
            ["Grass"]    = 0.75f,
            ["Ground"]   = 0f,
            ["Flying"]   = 1.5f,
            ["Dragon"]   = 0.75f
        },
        ["Ice"] = new Dictionary<string, float>
        {
            ["Water"]  = 0.75f,
            ["Grass"]  = 1.5f,
            ["Ice"]    = 0.75f,
            ["Ground"] = 1.5f,
            ["Flying"] = 1.5f,
            ["Dragon"] = 1.5f
        },
        ["Fighting"] = new Dictionary<string, float>
        {
            ["Normal"]  = 1.5f,
            ["Ice"]     = 1.5f,
            ["Poison"]  = 0.75f,
            ["Flying"]  = 0.75f,
            ["Psychic"] = 0.75f,
            ["Bug"]     = 0.75f,
            ["Rock"]    = 1.5f,
            ["Ghost"]   = 0f
        },
        ["Poison"] = new Dictionary<string, float>
        {
            ["Grass"]  = 1.5f,
            ["Poison"] = 0.75f,
            ["Ground"] = 0.75f,
            ["Bug"]    = 1.5f,
            ["Rock"]   = 0.75f,
            ["Ghost"]  = 0.75f
        },
        ["Ground"] = new Dictionary<string, float>
        {
            ["Fire"]     = 1.5f,
            ["Electric"] = 1.5f,
            ["Grass"]    = 0.75f,
            ["Poison"]   = 1.5f,
            ["Flying"]   = 0f,
            ["Bug"]      = 0.75f,
            ["Rock"]     = 1.5f
        },
        ["Flying"] = new Dictionary<string, float>
        {
            ["Electric"] = 0.75f,
            ["Grass"]    = 1.5f,
            ["Fighting"] = 1.5f,
            ["Bug"]      = 1.5f,
            ["Rock"]     = 0.75f
        },
        ["Psychic"] = new Dictionary<string, float>
        {
            ["Fighting"] = 1.5f,
            ["Poison"]   = 1.5f,
            ["Psychic"]  = 0.75f,
            ["Ghost"]    = 0f    // Gen 1: Ghost is immune to Psychic
        },
        ["Bug"] = new Dictionary<string, float>
        {
            ["Fire"]     = 0.75f,
            ["Grass"]    = 1.5f,
            ["Fighting"] = 0.75f,
            ["Flying"]   = 0.75f,
            ["Psychic"]  = 1.5f,
            ["Ghost"]    = 0.75f
        },
        ["Rock"] = new Dictionary<string, float>
        {
            ["Fire"]     = 1.5f,
            ["Ice"]      = 1.5f,
            ["Fighting"] = 0.75f,
            ["Ground"]   = 0.75f,
            ["Flying"]   = 1.5f,
            ["Bug"]      = 1.5f
        },
        ["Ghost"] = new Dictionary<string, float>
        {
            ["Normal"]  = 0f,
            ["Psychic"] = 0f,   // Gen 1 bug: Ghost should be 2x vs Psychic but was coded as immune
            ["Ghost"]   = 1.5f
        },
        ["Dragon"] = new Dictionary<string, float>
        {
            ["Dragon"] = 1.5f
        }
    };

    // Returns the damage multiplier for an attack of attackType hitting a Pokemon with defenderType.
    public static float GetMultiplier(string attackType, string defenderType)
    {
        return GetSingleMultiplier(attackType, defenderType);
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
