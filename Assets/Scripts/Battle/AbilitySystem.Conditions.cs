public static partial class AbilitySystem
{
    // Returns true if any alive ally (not p itself) has an ability with the given name.
    private static bool TeamHasAbilityNamed(PokemonInstance p, string abilityName)
    {
        var team = GetTeamOf(p);
        if (team == null) return false;
        foreach (var ally in team)
        {
            if (ally == p || ally.currentHP <= 0) continue;
            var ab = ally.baseData.ability;
            if (ab != null && ab.abilityName == abilityName) return true;
        }
        return false;
    }

    private static bool CheckCondition(PokemonInstance p, string condition)
    {
        if (string.IsNullOrEmpty(condition)) return true;

        // Support compound conditions joined by '&' (all must be true)
        if (condition.Contains('&'))
        {
            foreach (var part in condition.Split('&'))
                if (!CheckCondition(p, part.Trim())) return false;
            return true;
        }

        if (condition.StartsWith("hp_below_"))
        {
            if (int.TryParse(condition.Substring("hp_below_".Length), out int threshold))
                return (float)p.currentHP / p.maxHP < threshold / 100f;
        }

        switch (condition)
        {
            case "full_hp":
            case "at_full_hp":        return p.currentHP == p.maxHP;
            case "not_full_hp":       return p.currentHP < p.maxHP;
            case "weather_sun":       return IsWeatherActive("sun");
            case "weather_rain":      return IsWeatherActive("rain");
            case "weather_sandstorm": return IsWeatherActive("sandstorm");
            case "weather_hail":      return IsWeatherActive("hail");
            case "ally_is_minus":     return TeamHasAbilityNamed(p, "Minus");
            case "ally_is_plus":      return TeamHasAbilityNamed(p, "Plus");
            // These are context-checked or always-pass in this simplified system
            case "super_effective":
            case "first_hit":
            case "first_last":
            case "last_two":
            default:                  return true;
        }
    }
}
