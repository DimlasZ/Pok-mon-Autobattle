using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Headless, synchronous mirror of BattleSceneManager.RunBattleCoroutine.
// No UI, no coroutines, no audio — pure battle logic for offline simulation.
// Both lists are mutated during the battle (HP changes, faints removed).
// Always pass fresh instances via MakeFreshTeam — never reuse the same objects.

public static class BattleSimulator
{
    // Simulate a single battle between two fresh teams.
    // PlayerWin  = teamA wins, PlayerLoss = teamB wins, Draw = simultaneous wipe or 20-turn timeout.
    public static BattleResult Simulate(List<PokemonInstance> teamA, List<PokemonInstance> teamB)
    {
        AbilitySystem.InitBattle(teamA, teamB);
        AbilitySystem.ResetBattleState();

        // on_battle_start — same speed fires simultaneously (mirrors BattleSceneManager)
        foreach (var group in AbilitySystem.GetSpeedOrder("on_battle_start", teamA, teamB)
                     .GroupBy(e => e.Item1.speed).OrderByDescending(g => g.Key))
            foreach (var (p, own, opp) in group)
                AbilitySystem.FireSingle("on_battle_start", p, own, opp);

        BattleResult result = BattleResult.Draw;

        for (int turn = 1; turn <= 20; turn++)
        {
            PokemonInstance aFront = teamA.FirstOrDefault(p => p.currentHP > 0);
            PokemonInstance bFront = teamB.FirstOrDefault(p => p.currentHP > 0);
            if (aFront == null || bFront == null) break;

            // on_round_start — same speed fires simultaneously
            foreach (var group in AbilitySystem.GetSpeedOrder("on_round_start", teamA, teamB)
                         .GroupBy(e => e.Item1.speed).OrderByDescending(g => g.Key))
                foreach (var (p, own, opp) in group)
                    AbilitySystem.FireSingle("on_round_start", p, own, opp);

            // Determine attack order (mirrors BattleSceneManager exactly)
            bool aGoesFirst = aFront.speed != bFront.speed
                ? aFront.speed > bFront.speed
                : Random.value > 0.5f;

            var first      = aGoesFirst ? aFront : bFront;
            var second     = aGoesFirst ? bFront : aFront;
            var firstTeam  = aGoesFirst ? teamA  : teamB;
            var secondTeam = aGoesFirst ? teamB  : teamA;

            DamageCalculator.Attack(first, second, firstTeam, secondTeam);

            if (second.currentHP > 0)
                DamageCalculator.Attack(second, first, secondTeam, firstTeam);

            // Weather tick (sandstorm chip damage)
            foreach (var (p, dmg) in AbilitySystem.GetWeatherTick(teamA, teamB))
            {
                p.currentHP = Mathf.Max(0, p.currentHP - dmg);
                AbilitySystem.FireAfterHit(p, teamA.Contains(p) ? teamA : teamB);
            }

            teamA.RemoveAll(p => p.currentHP <= 0);
            teamB.RemoveAll(p => p.currentHP <= 0);

            bool aOut = teamA.Count == 0;
            bool bOut = teamB.Count == 0;
            if (aOut && bOut) { result = BattleResult.Draw;       break; }
            if (bOut)         { result = BattleResult.PlayerWin;  break; }
            if (aOut)         { result = BattleResult.PlayerLoss; break; }
        }

        return result;
    }

    // Create a fresh team from a list of PokemonData blueprints.
    // Each call produces new PokemonInstances with freshly rolled stats.
    public static List<PokemonInstance> MakeFreshTeam(List<PokemonData> template)
        => template.Select(p => new PokemonInstance(p)).ToList();
}
