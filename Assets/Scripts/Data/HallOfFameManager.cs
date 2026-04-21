using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Persists champion runs to JSON at Application.persistentDataPath/HallOfFame/hall_of_fame.json

[Serializable]
public class HallOfFameEntry
{
    public int    runNumber;
    public string date;          // ISO 8601: "2026-04-19 21:34"
    public int[]  pokemonIds;    // PokemonData.id for sprite lookup
    public string[] pokemonNames;
    public int[]  starLevels;
}

[Serializable]
public class HallOfFameData
{
    public List<HallOfFameEntry> entries = new List<HallOfFameEntry>();
}

public static class HallOfFameManager
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "HallOfFame", "hall_of_fame.json");

    public static HallOfFameData Load()
    {
        if (!File.Exists(FilePath)) return new HallOfFameData();
        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<HallOfFameData>(json) ?? new HallOfFameData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"HallOfFameManager: Failed to load — {e.Message}");
            return new HallOfFameData();
        }
    }

    public static HallOfFameEntry SaveEntry(PokemonInstance[] team)
    {
        var data = Load();

        int count = 0;
        if (team != null)
            foreach (var p in team) if (p != null) count++;

        var ids   = new int[count];
        var names = new string[count];
        var stars = new int[count];
        int idx = 0;
        if (team != null)
        {
            foreach (var p in team)
            {
                if (p == null) continue;
                ids[idx]   = p.baseData.id;
                names[idx] = p.baseData.pokemonName;
                stars[idx] = p.starLevel;
                idx++;
            }
        }

        var entry = new HallOfFameEntry
        {
            runNumber    = data.entries.Count + 1,
            date         = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            pokemonIds   = ids,
            pokemonNames = names,
            starLevels   = stars
        };
        data.entries.Add(entry);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"HallOfFameManager: Failed to save — {e.Message}");
        }

        return entry;
    }
}
