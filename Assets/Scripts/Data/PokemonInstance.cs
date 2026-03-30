// PokemonInstance represents a Pokemon that actually exists in the game at runtime.
// PokemonData is the static template (from CSV). PokemonInstance is the live copy with current stats.
// Example: Two Bulbasaurs in your team are the same PokemonData, but separate PokemonInstances.

public class PokemonInstance
{
    // The base data this Pokemon was created from (name, sprite, base stats, etc.)
    public PokemonData baseData;

    // Current stats — these can change during a run (buffs, level ups, etc.)
    public int currentHP;
    public int maxHP;
    public int attack;
    public int speed;

    // Star level — 1 = base, 2 = combined (3x), 3 = fully evolved (3x level 2)
    public int starLevel = 1;

    // Constructor — creates a new instance from a PokemonData template
    public PokemonInstance(PokemonData data)
    {
        baseData   = data;
        maxHP      = data.hp;
        currentHP  = data.hp;
        attack     = data.attack;
        speed      = data.speed;
        starLevel  = 1;
    }
}
