using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Unity menu: Pokemon → Team Simulator
//
// Generates random teams for a chosen round (or all rounds).
// Each team fights a configurable number of randomly selected opponents,
// using the exact same battle logic as BattleSceneManager.
// Exports one CSV per round, sorted by win rate.
//
// CSV columns: Round, Tier, Slot0–Slot5, Wins, Losses, Draws, WinRate%

public class TeamSimulatorWindow : EditorWindow
{
    private int  _round             = 1;
    private int  _teamCount         = 1000;
    private int  _battlesPerTeam    = 50;
    private int  _battlesPerMatchup = 1;
    private bool _allRounds         = false;
    private string _lastExportPath  = "";

    [MenuItem("Pokemon/Team Simulator")]
    public static void ShowWindow() => GetWindow<TeamSimulatorWindow>("Team Simulator");

    private void OnGUI()
    {
        GUILayout.Label("Team Simulator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _allRounds         = EditorGUILayout.Toggle("All Rounds", _allRounds);
        if (!_allRounds)
            _round         = EditorGUILayout.IntSlider("Round", _round, 1, 16);
        _teamCount         = EditorGUILayout.IntField("Teams to generate", _teamCount);
        _battlesPerTeam    = EditorGUILayout.IntField("Opponents per team", _battlesPerTeam);
        _battlesPerMatchup = EditorGUILayout.IntField("Battles per matchup", _battlesPerMatchup);

        int maxOpponents = Mathf.Max(0, _teamCount - 1);
        int opponents    = Mathf.Min(_battlesPerTeam, maxOpponents);

        EditorGUILayout.Space();
        if (_allRounds)
        {
            EditorGUILayout.LabelField("Simulating rounds 1–16");
            EditorGUILayout.LabelField($"Battles per round: {_teamCount * opponents * _battlesPerMatchup:N0}");
        }
        else
        {
            int tier     = TierForRound(_round);
            int teamSize = BattleSizeForRound(_round);
            EditorGUILayout.LabelField($"Tier:          {tier}");
            EditorGUILayout.LabelField($"Battle slots:  {teamSize} Pokémon");
            EditorGUILayout.LabelField($"Total battles: {_teamCount * opponents * _battlesPerMatchup:N0}");
        }
        if (_battlesPerTeam > maxOpponents)
            EditorGUILayout.HelpBox($"Capped at {maxOpponents} opponents (not enough teams).", MessageType.Warning);

        EditorGUILayout.Space();

        string buttonLabel = _allRounds ? "Run All Rounds & Export CSVs" : "Run Simulation & Export CSV";
        if (GUILayout.Button(buttonLabel))
        {
            if (_allRounds) RunAllRounds();
            else            RunSimulation(_round);
        }

        if (!string.IsNullOrEmpty(_lastExportPath))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last export:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_lastExportPath, GUILayout.Height(18));
        }
    }

    private void RunAllRounds()
    {
        string folder = EditorUtility.SaveFolderPanel("Choose export folder for all rounds", "", "");
        if (string.IsNullOrEmpty(folder)) return;

        string[] guids = AssetDatabase.FindAssets("t:PokemonData", new[] { "Assets/Resources/Data/Pokemon" });
        var allPokemon = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<PokemonData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null)
            .ToList();

        int exportedCount = 0;
        for (int round = 1; round <= 16; round++)
        {
            bool cancelled = SimulateRound(round, allPokemon, folder,
                progressPrefix: $"Round {round}/16 — ");
            if (cancelled)
            {
                EditorUtility.DisplayDialog("Cancelled",
                    $"Cancelled after {exportedCount} round(s). Completed files were saved.", "OK");
                return;
            }
            exportedCount++;
        }

        EditorUtility.DisplayDialog("Done",
            $"All 16 rounds simulated.\nFiles saved to:\n{folder}", "OK");
    }

    private void RunSimulation(int round)
    {
        string[] guids = AssetDatabase.FindAssets("t:PokemonData", new[] { "Assets/Resources/Data/Pokemon" });
        var allPokemon = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<PokemonData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null)
            .ToList();

        int tier = TierForRound(round);
        string savePath = EditorUtility.SaveFilePanel(
            "Export simulation results", "", $"sim_round{round}_tier{tier}_teams.csv", "csv");
        if (string.IsNullOrEmpty(savePath)) return;

        bool cancelled = SimulateRound(round, allPokemon, Path.GetDirectoryName(savePath),
            overridePath: savePath, progressPrefix: "");

        if (cancelled)
            EditorUtility.DisplayDialog("Cancelled", "Simulation cancelled — no file was written.", "OK");
    }

    // Returns true if the user cancelled. Saves CSV to folder/sim_round{round}_tier{tier}_teams.csv,
    // or to overridePath if provided.
    private bool SimulateRound(int round, List<PokemonData> allPokemon, string folder,
        string overridePath = null, string progressPrefix = "")
    {
        int tier     = TierForRound(round);
        int minTier  = Mathf.Max(1, tier - 2);
        int teamSize = BattleSizeForRound(round);

        var pool = allPokemon
            .Where(p => p.tier >= minTier && p.tier <= tier)
            .ToList();

        if (pool.Count < teamSize)
        {
            Debug.LogWarning($"[TeamSimulator] Round {round}: only {pool.Count} Pokémon available (need {teamSize}), skipping.");
            return false;
        }

        // Generate random teams
        var templates = new List<List<PokemonData>>();
        for (int i = 0; i < _teamCount; i++)
        {
            var shuffled = pool.OrderBy(_ => Random.value).ToList();
            templates.Add(shuffled.Take(teamSize).ToList());
        }

        int[] wins   = new int[_teamCount];
        int[] losses = new int[_teamCount];
        int[] draws  = new int[_teamCount];

        int opponentsPerTeam = Mathf.Min(_battlesPerTeam, _teamCount - 1);
        int totalBattles     = _teamCount * opponentsPerTeam * _battlesPerMatchup;
        int battlesDone      = 0;
        bool cancelled       = false;

        bool prevLogEnabled = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;

        try
        {
            for (int i = 0; i < _teamCount && !cancelled; i++)
            {
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
                                $"{progressPrefix}Simulating round {round}",
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

        if (cancelled) return true;

        // Build CSV
        var sb = new StringBuilder();
        sb.AppendLine("Round,Tier,Slot0,Slot1,Slot2,Slot3,Slot4,Slot5,Wins,Losses,Draws,WinRate%");

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

            sb.Append(round).Append(',').Append(tier).Append(',');
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

        string path = overridePath ?? Path.Combine(folder, $"sim_round{round}_tier{tier}_teams.csv");
        File.WriteAllText(path, sb.ToString());
        _lastExportPath = path;

        Debug.Log($"[TeamSimulator] Round {round} (Tier {tier}, {teamSize} slots) — {_teamCount} teams, {opponentsPerTeam} opponents/team → {battlesDone:N0} battles → {path}");
        return false;
    }

    // Must stay in sync with ShopManager.GetTierForRound
    private static int TierForRound(int round)
    {
        if (round <= 2)  return 1;
        if (round <= 5)  return 2;
        if (round <= 8)  return 3;
        if (round <= 11) return 4;
        if (round <= 14) return 5;
        return 6;
    }

    // Must stay in sync with ShopManager.GetBattleSizeForRound
    private static int BattleSizeForRound(int round)
    {
        if (round < 3) return 3;
        if (round < 5) return 4;
        if (round < 7) return 5;
        return 6;
    }
}
