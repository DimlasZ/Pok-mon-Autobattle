using UnityEngine;

// PokemonInstance represents a Pokemon that actually exists in the game at runtime.
// PokemonData is the static template (from CSV). PokemonInstance is the live copy with current stats.
// Example: Two Bulbasaurs in your team are the same PokemonData, but separate PokemonInstances.

public class PokemonInstance
{
    public PokemonData baseData;

    public int currentHP;
    public int maxHP;
    public int attack;
    public int speed;

    // Rolled values at spawn — used to detect buffs (green) and debuffs (red) in the UI, and to reset after battle
    public int baseMaxHP;
    public int baseAttack;
    public int baseSpeed;

    public int starLevel = 1;

    // Permanent incoming-damage multiplier — set by Cursed Aura and similar lasting effects
    public float damageTakenMultiplier = 1f;

    // One-shot incoming-damage multiplier — set by Screech/Leer, consumed on the next hit
    public float nextHitDamageMultiplier = 1f;

    // Optional override for log display (e.g. "Enemy Bulbasaur"). Falls back to baseData name.
    public string displayName;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? baseData.pokemonName : displayName;

    // Each stat rolls between 80%-100% of the base CSV value on spawn
    private const float StatSpreadMin = 0.8f;
    private const float StatSpreadMax = 1.0f;

    public PokemonInstance(PokemonData data)
    {
        baseData  = data;
        maxHP      = Spread(data.hp);
        currentHP  = maxHP;
        attack     = Spread(data.attack);
        speed      = Spread(data.speed);
        baseMaxHP  = maxHP;
        baseAttack = attack;
        baseSpeed  = speed;
        starLevel  = 1;
    }

    // Resets all battle-modified stats back to their rolled values
    public void ResetForBattle()
    {
        maxHP                   = baseMaxHP;
        currentHP               = baseMaxHP;
        attack                  = baseAttack;
        speed                   = baseSpeed;
        damageTakenMultiplier   = 1f;
        nextHitDamageMultiplier = 1f;
    }

    private static int Spread(int baseStat)
        => Mathf.Max(1, Mathf.RoundToInt(baseStat * Random.Range(StatSpreadMin, StatSpreadMax)));
}
