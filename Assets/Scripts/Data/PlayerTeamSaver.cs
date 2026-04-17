using System.IO;
using System.Text;
using UnityEngine;

// Appends the player's battle team to a CSV file after each round.
// Saved to: Application.persistentDataPath/SavedTeams/player_teams.csv
// Format:   Round,Tier,Slot0,Slot1,Slot2,Slot3,Slot4,Slot5

public static class PlayerTeamSaver
{
    private const string FolderName = "SavedTeams";
    private const string FileName   = "player_teams.csv";
    private const int    MaxSlots   = 6;

    public static string FilePath =>
        Path.Combine(Application.persistentDataPath, FolderName, FileName);

    public static void SaveTeam(int round, int tier, PokemonInstance[] team)
    {
        string path = FilePath;
        string dir  = Path.GetDirectoryName(path);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        bool writeHeader = !File.Exists(path);

        var sb = new StringBuilder();

        if (writeHeader)
            sb.AppendLine("Round,Tier,Slot0,Slot1,Slot2,Slot3,Slot4,Slot5");

        sb.Append(round).Append(',').Append(tier);

        for (int i = 0; i < MaxSlots; i++)
        {
            sb.Append(',');
            if (team != null && i < team.Length && team[i] != null)
                sb.Append(team[i].baseData.pokemonName);
        }

        sb.AppendLine();

        File.AppendAllText(path, sb.ToString());
        Debug.Log($"[PlayerTeamSaver] Round {round} team saved → {path}");
    }
}
