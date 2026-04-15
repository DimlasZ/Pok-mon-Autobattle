using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static partial class AbilitySystem
{
    // Returns pre-resolved targets from ctx when available (ensures VFX and effect hit the same Pokémon).
    private static List<PokemonInstance> GetTargets(EffectContext ctx) =>
        ctx.preResolvedTargets ?? ResolveTargets(ctx.ab.target, ctx.count, ctx.source, ctx.sourceTeam, ctx.enemyTeam, ctx.contextTarget);

    private static List<PokemonInstance> ResolveTargets(string target, int count, PokemonInstance source,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> enemyTeam, PokemonInstance contextTarget)
    {
        var result = new List<PokemonInstance>();
        int n = count > 0 ? count : 1;

        switch (target)
        {
            case "self":
                result.Add(source);
                break;

            case "enemy_front":
                // If called from an on_attack context, prefer the actual defender (contextTarget)
                // even if it just died — avoids spilling debuffs onto the next alive enemy.
                if (contextTarget != null && enemyTeam.Contains(contextTarget))
                    result.Add(contextTarget);
                else
                    result.AddRange(GetFirstNAlive(enemyTeam, n));
                break;

            case "enemy_second":
            {
                var t = GetSecondAlive(enemyTeam);
                if (t != null) result.Add(t);
                break;
            }

            case "enemy_next":
            {
                var t = GetNextAlive(enemyTeam, contextTarget);
                if (t != null) result.Add(t);
                break;
            }

            case "enemy_last":
                result.AddRange(GetLastNAlive(enemyTeam, n));
                break;

            case "enemy_all":
                foreach (var t in enemyTeam) if (t.currentHP > 0) result.Add(t);
                break;

            case "enemy_random":
            {
                var t = GetRandomAlive(enemyTeam, null);
                if (t != null) result.Add(t);
                break;
            }

            case "ally_all":
                foreach (var t in sourceTeam) if (t != source && t.currentHP > 0) result.Add(t);
                break;

            case "ally_random":
            {
                var t = GetRandomAlive(sourceTeam, source);
                if (t != null) result.Add(t);
                break;
            }

            case "ally_last":
                result.AddRange(GetLastNAlive(sourceTeam.Where(p => p != source).ToList(), n));
                break;

            case "ally_behind":
            {
                int idx = sourceTeam.IndexOf(source);
                for (int i = idx + 1; i < sourceTeam.Count; i++)
                    if (sourceTeam[i].currentHP > 0) { result.Add(sourceTeam[i]); break; }
                break;
            }

            case "attacker":
                if (contextTarget != null) result.Add(contextTarget);
                break;

            case "all_others":
                foreach (var t in enemyTeam)  if (t.currentHP > 0) result.Add(t);
                foreach (var t in sourceTeam) if (t != source && t.currentHP > 0) result.Add(t);
                break;

            case "all":
                foreach (var t in enemyTeam)  if (t.currentHP > 0) result.Add(t);
                foreach (var t in sourceTeam) if (t.currentHP > 0) result.Add(t);
                break;
        }

        return result;
    }

    private static List<PokemonInstance> GetTeamOf(PokemonInstance p)
    {
        if (_teamB != null && _teamB.Contains(p)) return _teamB;
        return _teamA;
    }

    private static List<PokemonInstance> GetOpponentOf(PokemonInstance p)
    {
        if (_teamB != null && _teamB.Contains(p)) return _teamA;
        return _teamB;
    }

    private static PokemonInstance GetFirstAlive(List<PokemonInstance> team)
    {
        foreach (var p in team) if (p.currentHP > 0) return p;
        return null;
    }

    private static List<PokemonInstance> GetFirstNAlive(List<PokemonInstance> team, int n)
    {
        var result = new List<PokemonInstance>();
        foreach (var p in team)
        {
            if (p.currentHP <= 0) continue;
            result.Add(p);
            if (result.Count >= n) break;
        }
        return result;
    }

    private static PokemonInstance GetSecondAlive(List<PokemonInstance> team)
    {
        int seen = 0;
        foreach (var p in team)
        {
            if (p.currentHP <= 0) continue;
            if (++seen == 2) return p;
        }
        return null;
    }

    private static List<PokemonInstance> GetLastNAlive(List<PokemonInstance> team, int n)
    {
        var alive = team.Where(p => p.currentHP > 0).ToList();
        return alive.Skip(Mathf.Max(0, alive.Count - n)).ToList();
    }

    private static PokemonInstance GetNextAlive(List<PokemonInstance> team, PokemonInstance current)
    {
        if (current == null) return GetFirstAlive(team);
        int idx = team.IndexOf(current);
        for (int i = idx + 1; i < team.Count; i++)
            if (team[i].currentHP > 0) return team[i];
        return null;
    }

    private static PokemonInstance GetRandomAlive(List<PokemonInstance> team, PokemonInstance exclude)
    {
        var alive = team.Where(p => p != exclude && p.currentHP > 0).ToList();
        if (alive.Count == 0) return null;
        return alive[Random.Range(0, alive.Count)];
    }
}
