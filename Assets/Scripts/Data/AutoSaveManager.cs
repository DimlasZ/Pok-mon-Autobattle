using System;
using System.IO;
using UnityEngine;

// Autosave — persists game state between app sessions so the player can resume a run.
//
// Save is written: at the start of every Buy phase (when ReturnToShop completes)
// Save is deleted: on new game, game over, victory, or voluntary return to main menu
// Save format:    JSON at Application.persistentDataPath/save.json
// Version field:  if the version doesn't match SaveVersion the save is discarded

[Serializable]
public class SavedPokemonInstance
{
    public int   pokemonId;
    public int   currentHP;
    public int   maxHP;
    public int   baseMaxHP;
    public int   attack;
    public int   speed;
    public int   baseAttack;
    public int   baseSpeed;
    public int   starLevel;
}

[Serializable]
public class GameSaveData
{
    public int    version = AutoSaveManager.SaveVersion;
    public int    playerWins;
    public int    currentRound;
    public int    playerHP;
    public int    currentPokedollars;

    public SavedPokemonInstance[] shopRow    = new SavedPokemonInstance[ShopManager.MaxShopSize];
    public SavedPokemonInstance[] battleRow  = new SavedPokemonInstance[ShopManager.MaxBattleSize];
    public SavedPokemonInstance[] benchRow   = new SavedPokemonInstance[ShopManager.MaxBenchSize];
    public bool[]                 baitedSlots = new bool[ShopManager.MaxShopSize];
}

public static class AutoSaveManager
{
    public const int SaveVersion = 1;

    static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    // -------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------

    public static bool SaveExists()
    {
        if (!File.Exists(FilePath)) return false;
        try
        {
            var data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(FilePath));
            return data != null && data.version == SaveVersion;
        }
        catch { return false; }
    }

    public static void Save()
    {
        var gm = GameManager.Instance;
        var sm = ShopManager.Instance;
        if (gm == null || sm == null) return;

        var data = new GameSaveData
        {
            playerWins         = gm.PlayerWins,
            currentRound       = gm.CurrentRound,
            playerHP           = gm.PlayerHP,
            currentPokedollars = sm.CurrentPokedollars,
        };

        for (int i = 0; i < ShopManager.MaxShopSize;   i++) data.shopRow[i]    = Serialize(sm.ShopRow[i]);
        for (int i = 0; i < ShopManager.MaxBattleSize; i++) data.battleRow[i]  = Serialize(sm.BattleRow[i]);
        for (int i = 0; i < ShopManager.MaxBenchSize;  i++) data.benchRow[i]   = Serialize(sm.BenchRow[i]);
        for (int i = 0; i < ShopManager.MaxShopSize;   i++) data.baitedSlots[i] = sm.BaitedSlots[i];

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: false));
            Debug.Log($"[AutoSave] Saved round {data.currentRound}, wins {data.playerWins}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AutoSave] Save failed: {e.Message}");
        }
    }

    public static GameSaveData Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(FilePath));
            if (data == null || data.version != SaveVersion)
            {
                Debug.Log("[AutoSave] Save version mismatch — discarding.");
                Delete();
                return null;
            }
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AutoSave] Load failed: {e.Message}");
            Delete();
            return null;
        }
    }

    public static void Delete()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
            Debug.Log("[AutoSave] Save deleted.");
        }
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    static SavedPokemonInstance Serialize(PokemonInstance p)
    {
        if (p == null) return null;
        return new SavedPokemonInstance
        {
            pokemonId  = p.baseData.id,
            currentHP  = p.currentHP,
            maxHP      = p.maxHP,
            baseMaxHP  = p.baseMaxHP,
            attack     = p.attack,
            speed      = p.speed,
            baseAttack = p.baseAttack,
            baseSpeed  = p.baseSpeed,
            starLevel  = p.starLevel,
        };
    }

    public static PokemonInstance Deserialize(SavedPokemonInstance s, PokemonData[] allPokemon)
    {
        if (s == null) return null;
        PokemonData data = null;
        foreach (var p in allPokemon)
            if (p != null && p.id == s.pokemonId) { data = p; break; }
        if (data == null)
        {
            Debug.LogWarning($"[AutoSave] Could not find PokemonData for id {s.pokemonId} — slot skipped.");
            return null;
        }

        var inst = new PokemonInstance(data)
        {
            currentHP  = s.currentHP,
            maxHP      = s.maxHP,
            baseMaxHP  = s.baseMaxHP > 0 ? s.baseMaxHP : s.maxHP,
            attack     = s.attack,
            speed      = s.speed,
            baseAttack = s.baseAttack,
            baseSpeed  = s.baseSpeed,
            starLevel  = s.starLevel,
        };
        return inst;
    }
}
