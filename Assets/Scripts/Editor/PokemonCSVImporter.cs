using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class PokemonCSVImporter
{
    [MenuItem("Pokemon/Import Pokemon from CSV")]
    public static void ImportFromCSV()
    {
        string csvPath     = "Assets/Data/Pokémon_data.csv";
        string outputFolder = "Assets/Data/Pokemon";

        if (!File.Exists(csvPath))
        {
            Debug.LogError("CSV not found at: " + csvPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(outputFolder))
            AssetDatabase.CreateFolder("Assets/Data", "Pokemon");

        // Build ability lookup dictionary (ID → AbilityData asset)
        var abilityLookup = new Dictionary<int, AbilityData>();
        string[] abilityGuids = AssetDatabase.FindAssets("t:AbilityData", new[] { "Assets/Data/Abilities" });
        foreach (string guid in abilityGuids)
        {
            AbilityData ab = AssetDatabase.LoadAssetAtPath<AbilityData>(AssetDatabase.GUIDToAssetPath(guid));
            if (ab != null)
                abilityLookup[ab.abilityID] = ab;
        }

        string[] lines = File.ReadAllLines(csvPath);

        int created = 0;
        int skipped = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] col = line.Split(',');

            // Skip rows with no attack or hp filled in
            if (string.IsNullOrEmpty(col[2]) || string.IsNullOrEmpty(col[3]))
            {
                skipped++;
                continue;
            }

            int    id        = int.Parse(col[0]);
            string pName     = col[1].Trim();
            int    attack    = int.Parse(col[2]);
            int    hp        = int.Parse(col[3]);
            int    speed     = col.Length > 4  && !string.IsNullOrEmpty(col[4])  ? int.Parse(col[4])  : 0;
            string type1     = col.Length > 5  ? col[5].Trim() : "";
            int    tier          = col.Length > 14 && !string.IsNullOrEmpty(col[14]) ? int.Parse(col[14]) : 0;
            string abilityID     = col.Length > 15 ? col[15].Trim() : "";
            string spriteName    = col.Length > 16 ? col[16].Trim() : "";
            int    preEvolutionId = col.Length > 20 && !string.IsNullOrEmpty(col[20]) ? int.Parse(col[20]) : 0;

            string safeName  = string.Concat(pName.Split(System.IO.Path.GetInvalidFileNameChars()));
            string assetPath = $"{outputFolder}/{id:0000} {safeName}.asset";

            PokemonData data = AssetDatabase.LoadAssetAtPath<PokemonData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<PokemonData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.id             = id;
            data.pokemonName    = pName;
            data.attack         = attack;
            data.hp             = hp;
            data.type1          = type1;
            data.speed          = speed;
            data.tier           = tier;
            data.preEvolutionId = preEvolutionId;

            // Link ability asset by ID
            if (int.TryParse(abilityID, out int abilityIDInt) && abilityLookup.TryGetValue(abilityIDInt, out AbilityData ab))
                data.ability = ab;
            else
                data.ability = null;

            // Try to find and link the sprite
            if (!string.IsNullOrEmpty(spriteName))
            {
                string safeSpriteName = string.Concat(spriteName.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
                string spritePath = $"Assets/Sprites/Pokémon/{safeSpriteName}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                    data.sprite = sprite;
                else
                    Debug.LogWarning($"Sprite not found: {spritePath}");
            }

            EditorUtility.SetDirty(data);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Import complete: {created} Pokemon imported, {skipped} skipped (no stats).");
    }
}
