using UnityEngine;

public static partial class AbilitySystem
{
    private static void RecalculateWeatherNegation()
    {
        _weatherNegated = false;
        foreach (var p in _teamA)
            if (p.currentHP > 0 && p.baseData.ability?.effect == "negate_weather") { _weatherNegated = true; return; }
        foreach (var p in _teamB)
            if (p.currentHP > 0 && p.baseData.ability?.effect == "negate_weather") { _weatherNegated = true; return; }
    }

    private static bool IsWeatherActive(string weather)
    {
        if (_weatherNegated) return false;
        return ActiveWeather == weather;
    }

    // Returns the damage multiplier the active weather grants to a given attacker.
    // Applied to both basic attacks and deal_damage_flat abilities.
    public static float GetWeatherDamageMultiplier(PokemonInstance pokemon)
    {
        if (_weatherNegated) return 1f;
        string t = pokemon.baseData.type1.ToLower();
        switch (ActiveWeather)
        {
            case "rain":
                if (t == "water") return 1.1f;
                if (t == "fire")  return 0.9f;
                break;
            case "sun":
                if (t == "fire")  return 1.1f;
                if (t == "water") return 0.9f;
                break;
        }
        return 1f;
    }

    // Returns 0.9 for sandstorm-immune Pokemon during sandstorm (10% damage reduction), 1 otherwise.
    public static float GetWeatherDamageReduction(PokemonInstance defender)
    {
        if (_weatherNegated) return 1f;
        if (ActiveWeather != "sandstorm") return 1f;
        if (IsSandstormImmune(defender)) return 0.9f;
        return 1f;
    }

    private static bool IsSandstormImmune(PokemonInstance p)
    {
        string t    = p.baseData.type1.ToLower();
        string name = p.baseData.pokemonName;
        return t == "ground" || name == "Cacnea" || name == "Cacturne";
    }

    // Returns all alive Pokemon that take sandstorm chip damage this round (non-Ground, 6% max HP).
    public static System.Collections.Generic.List<(PokemonInstance pokemon, int damage)>
        GetWeatherTick(System.Collections.Generic.List<PokemonInstance> teamA,
                       System.Collections.Generic.List<PokemonInstance> teamB)
    {
        var result = new System.Collections.Generic.List<(PokemonInstance, int)>();
        if (_weatherNegated || ActiveWeather != "sandstorm") return result;

        foreach (var p in teamA)
            if (p.currentHP > 0 && !IsSandstormImmune(p))
                result.Add((p, Mathf.Max(1, Mathf.RoundToInt(p.maxHP * 0.06f))));
        foreach (var p in teamB)
            if (p.currentHP > 0 && !IsSandstormImmune(p))
                result.Add((p, Mathf.Max(1, Mathf.RoundToInt(p.maxHP * 0.06f))));

        return result;
    }

    private static void HandleSummonWeather(EffectContext ctx)
    {
        RevertPassiveWeatherAbilities(); // undo previous weather boosts before switching weather
        ActiveWeather = ctx.ab.value;
        RecalculateWeatherNegation();
        OnWeatherChanged?.Invoke(_weatherNegated ? "" : ActiveWeather);
        Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: Weather → {ActiveWeather}!");
        FirePassiveWeatherAbilities();
    }

    // Reverts any passive weather boosts applied by FirePassiveWeatherAbilities,
    // restoring each affected Pokemon's attack/speed to what it was before the boost.
    // Also removes them from _boostOnceApplied so they can be re-boosted by a new weather.
    private static void RevertPassiveWeatherAbilities()
    {
        foreach (var kvp in _weatherBoostedStats)
        {
            var p = kvp.Key;
            p.attack = kvp.Value.attack;
            p.speed  = kvp.Value.speed;
            _boostOnceApplied.Remove(p);
            Debug.Log($"[Weather Revert] {p.DisplayName}: attack={p.attack}, speed={p.speed}");
        }
        _weatherBoostedStats.Clear();
    }

    // Applies passive weather-conditional speed/attack boosts (e.g. Chlorophyll, Swift Swim)
    // to all alive Pokemon whose condition now matches the active weather.
    // Saves pre-boost stats so they can be reverted if the weather changes again.
    private static void FirePassiveWeatherAbilities()
    {
        var teams = new[] { (_teamA, _teamB), (_teamB, _teamA) };
        foreach (var (team, opp) in teams)
        {
            foreach (var p in new System.Collections.Generic.List<PokemonInstance>(team))
            {
                if (p.currentHP <= 0) continue;
                var ab = p.baseData.ability;
                if (ab == null || ab.trigger != "passive") continue;
                if (string.IsNullOrEmpty(ab.condition) || !ab.condition.StartsWith("weather_")) continue;
                if (ab.effect != "boost_speed" && ab.effect != "boost_attack") continue;
                if (!CheckCondition(p, ab.condition)) continue;
                if (_boostOnceApplied.Contains(p)) continue;
                _weatherBoostedStats[p] = (p.attack, p.speed); // save before boosting
                _boostOnceApplied.Add(p);
                ApplyEffect(p, team, opp, ab, 0, 0, null);
            }
        }
    }

    private static void HandleSummonScreen(EffectContext ctx)
    {
        ActiveScreen = ctx.ab.value;
        Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {ActiveScreen} activated!");
    }
}
