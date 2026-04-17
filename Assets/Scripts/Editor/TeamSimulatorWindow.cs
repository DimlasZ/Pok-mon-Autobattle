using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Unity menu: Pokemon → Team Simulator
//
// Generates random teams for a chosen tier. Each team fights a configurable
// number of randomly selected opponents, using the exact same battle logic as
// BattleSceneManager. Exports a CSV sorted by win rate.
//
// CSV columns: Tier, Slot0–Slot5, Wins, Losses, Draws, WinRate%

public class TeamSimulatorWindow : EditorWindow
{
    private int _tier              = 1;
    private int _teamCount         = 1000;
    private int _battlesPerTeam    = 50;
    private int _battlesPerMatchup = 1;
    private string _lastExportPath = "";

    [MenuItem("Pokemon/Team Simulator")]
    public static void ShowWindow() => GetWindow<TeamSimulatorWindow>("Team Simulator");

    private void OnGUI()
    {
        GUILayout.Label("Team Simulator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _tier              = EditorGUILayout.IntSlider("Tier", _tier, 1, 6);
        _teamCount         = EditorGUILayout.IntField("Teams to generate", _teamCount);
        _battlesPerTeam    = EditorGUILayout.IntField("Opponents per team", _battlesPerTeam);
        _battlesPerMatchup = EditorGUILayout.IntField("Battles per matchup", _battlesPerMatchup);

        int teamSize     = TeamSizeForTier(_tier);
        int maxOpponents = Mathf.Max(0, _teamCount - 1);
        int opponents    = Mathf.Min(_battlesPerTeam, maxOpponents);
        int totalBattles = _teamCount * opponents * _battlesPerMatchup;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Team size:     {teamSize} Pokémon");
        if (_battlesPerTeam > maxOpponents)
            EditorGUILayout.HelpBox($"Capped at {maxOpponents} opponents (not enough teams).", MessageType.Warning);
        EditorGUILayout.LabelField($"Total battles: {totalBattles:N0}");
        EditorGUILayout.Space();

        if (GUILayout.Button("Run Simulation & Export CSV"))
            RunSimulation();

        if (!string.IsNullOrEmpty(_lastExportPath))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last export:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_lastExportPath, GUILayout.Height(18));
        }
    }

    private void RunSimulation()
    {
        // Load PokemonData directly from assets (works without the game running)
        // Min tier scales with the selected tier so low-tier Pokémon are excluded at high tiers:
        //   Tier 1–3 → min 1 | Tier 4 → min 2 | Tier 5 → min 3 | Tier 6 → min 4
        int minTier = Mathf.Max(1, _tier - 2);
        string[] guids = AssetDatabase.FindAssets("t:PokemonData", new[] { "Assets/Resources/Data/Pokemon" });
        var pool = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<PokemonData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null && p.tier >= minTier && p.tier <= _tier)
            .ToList();

        int teamSize = TeamSizeForTier(_tier);

        if (pool.Count < teamSize)
        {
            EditorUtility.DisplayDialog("Not enough Pokémon",
                $"Only {pool.Count} Pokémon available up to Tier {_tier}, need at least {teamSize}.",
                "OK");
            return;
        }

        string savePath = EditorUtility.SaveFilePanel(
            "Export simulation results", "", $"sim_tier{_tier}_teams.csv", "csv");
        if (string.IsNullOrEmpty(savePath)) return;

        // Generate random teams — no duplicate Pokémon per team
        var templates = new List<List<PokemonData>>();
        for (int i = 0; i < _teamCount; i++)
        {
            var shuffled = pool.OrderBy(_ => Random.value).ToList();
            templates.Add(shuffled.Take(teamSize).ToList());
        }

        // Win/Loss/Draw counters per team
        int[] wins   = new int[_teamCount];
        int[] losses = new int[_teamCount];
        int[] draws  = new int[_teamCount];

        int opponentsPerTeam  = Mathf.Min(_battlesPerTeam, _teamCount - 1);
        int totalBattles      = _teamCount * opponentsPerTeam * _battlesPerMatchup;
        int battlesDone       = 0;
        bool cancelled        = false;

        // Suppress the massive Debug.Log output from DamageCalculator during simulation
        bool prevLogEnabled = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;

        try
        {
            for (int i = 0; i < _teamCount && !cancelled; i++)
            {
                // Pick opponentsPerTeam distinct random opponents for this team
                var opponents = Enumerable.Range(0, _teamCount)
                    .Where(j => j != i)
                    .OrderBy(_ => Random.value)
                    .Take(opponentsPerTeam)
                    .ToList();

                foreach (int j in opponents)
                {
                    if (cancelled) break;

                    for (int k = 0; k < _battlesPerMatchup; k++)
                    {
                        battlesDone++;

                        if (battlesDone % 500 == 0)
                            cancelled = EditorUtility.DisplayCancelableProgressBar(
                                "Simulating battles",
                                $"Team {i + 1}/{_teamCount} — battle {battlesDone:N0}/{totalBattles:N0}",
                                (float)battlesDone / totalBattles);

                        var a      = BattleSimulator.MakeFreshTeam(templates[i]);
                        var b      = BattleSimulator.MakeFreshTeam(templates[j]);
                        var result = BattleSimulator.Simulate(a, b);

                        switch (result)
                        {
                            case BattleResult.PlayerWin:  wins[i]++;  losses[j]++; break;
                            case BattleResult.PlayerLoss: wins[j]++;  losses[i]++; break;
                            case BattleResult.Draw:       draws[i]++; draws[j]++;  break;
                        }
                    }
                }
            }
        }
        finally
        {
            Debug.unityLogger.logEnabled = prevLogEnabled;
            EditorUtility.ClearProgressBar();
        }

        if (cancelled)
        {
            EditorUtility.DisplayDialog("Cancelled", "Simulation cancelled — no file was written.", "OK");
            return;
        }

        // Build CSV sorted by win rate descending
        var sb = new StringBuilder();
        sb.AppendLine("Tier,Slot0,Slot1,Slot2,Slot3,Slot4,Slot5,Wins,Losses,Draws,WinRate%");

        var sorted = Enumerable.Range(0, _teamCount)
            .OrderByDescending(i =>
            {
                int total = wins[i] + losses[i] + draws[i];
                return total == 0 ? 0f : (float)wins[i] / total;
            });

        foreach (int i in sorted)
        {
            int   total = wins[i] + losses[i] + draws[i];
            float rate  = total == 0 ? 0f : (float)wins[i] / total * 100f;
            var   team  = templates[i];

            sb.Append(_tier).Append(',');
            for (int s = 0; s < 6; s++)
            {
                if (s < team.Count) sb.Append(team[s].pokemonName);
                sb.Append(',');
            }
            sb.Append(wins[i]).Append(',');
            sb.Append(losses[i]).Append(',');
            sb.Append(draws[i]).Append(',');
            sb.Append(rate.ToString("F1"));
            sb.AppendLine();
        }

        File.WriteAllText(savePath, sb.ToString());
        _lastExportPath = savePath;

        Debug.Log($"[TeamSimulator] {_teamCount} teams, {opponentsPerTeam} opponents/team, {_battlesPerMatchup} battles/matchup → {battlesDone:N0} battles → {savePath}");
        EditorUtility.DisplayDialog("Done",
            $"Simulation complete!\n\n" +
            $"{_teamCount} teams\n" +
            $"{opponentsPerTeam} opponents per team\n" +
            $"{_battlesPerMatchup} battles per matchup\n" +
            $"{battlesDone:N0} total battles\n\n" +
            $"Exported to:\n{savePath}", "OK");
    }

    private static int TeamSizeForTier(int tier)
    {
        if (tier <= 1) return 3;
        if (tier == 2) return 4;
        if (tier == 3) return 5;
        return 6;
    }
}
