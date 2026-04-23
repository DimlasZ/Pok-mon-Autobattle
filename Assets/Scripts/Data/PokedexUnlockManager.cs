using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Tracks which Pokemon have earned Pokedex achievements across all runs.
// Elite4 unlock: Pokemon was on the team when the player reached win 8 (Elite 4).
// Champ unlock:  Pokemon was on the team when the player beat the Champion (win 13).

[Serializable]
public class PokedexUnlockData
{
    public List<int> elite4Ids = new List<int>();
    public List<int> champIds  = new List<int>();
}

public static class PokedexUnlockManager
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "pokedex_unlocks.json");

    static PokedexUnlockData _cache;

    public static PokedexUnlockData Load()
    {
        if (_cache != null) return _cache;

        if (!File.Exists(FilePath))
        {
            _cache = new PokedexUnlockData();
            return _cache;
        }
        try
        {
            _cache = JsonUtility.FromJson<PokedexUnlockData>(File.ReadAllText(FilePath)) ?? new PokedexUnlockData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PokedexUnlockManager: Failed to load — {e.Message}");
            _cache = new PokedexUnlockData();
        }
        return _cache;
    }

    static void Save()
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(_cache, prettyPrint: true)); }
        catch (Exception e) { Debug.LogWarning($"PokedexUnlockManager: Failed to save — {e.Message}"); }
    }

    public static void UnlockElite4(PokemonInstance[] team)
    {
        var data = Load();
        bool changed = false;
        if (team != null)
            foreach (var p in team)
                if (p != null)
                    foreach (int id in GetEvolutionChainIds(p.baseData))
                        if (!data.elite4Ids.Contains(id))
                        { data.elite4Ids.Add(id); changed = true; }
        if (changed) Save();
    }

    public static void UnlockChamp(PokemonInstance[] team)
    {
        var data = Load();
        bool changed = false;
        if (team != null)
            foreach (var p in team)
                if (p != null)
                    foreach (int id in GetEvolutionChainIds(p.baseData))
                        if (!data.champIds.Contains(id))
                        { data.champIds.Add(id); changed = true; }
        if (changed) Save();
    }

    // Returns all IDs in the evolution family (ancestors + descendants) of the given pokemon.
    static List<int> GetEvolutionChainIds(PokemonData pokemon)
    {
        var db = Resources.Load<PokemonDatabase>("PokemonDatabase");
        if (db == null || db.allPokemon == null)
            return new List<int> { pokemon.id };

        var all = db.allPokemon;

        // Walk back to root
        var root = pokemon;
        int guard = 0;
        while (root.preEvolutionId != 0 && guard++ < 10)
        {
            var pre = System.Array.Find(all, p => p != null && p.id == root.preEvolutionId);
            if (pre == null) break;
            root = pre;
        }

        // BFS forward to collect full family
        var ids     = new List<int>();
        var visited = new HashSet<int>();
        var queue   = new Queue<PokemonData>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node == null || !visited.Add(node.id)) continue;
            ids.Add(node.id);
            foreach (var p in all)
                if (p != null && p.preEvolutionId == node.id)
                    queue.Enqueue(p);
        }
        return ids;
    }

    public static bool HasElite4(int pokemonId) => Load().elite4Ids.Contains(pokemonId);
    public static bool HasChamp(int pokemonId)  => Load().champIds.Contains(pokemonId);

    // Call on application start or scene load to clear the in-memory cache so fresh data is read.
    public static void ClearCache() => _cache = null;
}
