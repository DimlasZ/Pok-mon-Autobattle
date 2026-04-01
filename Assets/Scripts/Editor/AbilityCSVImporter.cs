using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AbilityCSVImporter
{
    [MenuItem("Pokemon/Import Abilities from CSV")]
    public static void ImportFromCSV()
    {
        string csvPath    = "Assets/Data/Ability_data.csv";
        string outputFolder = "Assets/Data/Abilities";

        if (!File.Exists(csvPath))
        {
            Debug.LogError("Ability CSV not found at: " + csvPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(outputFolder))
            AssetDatabase.CreateFolder("Assets/Data", "Abilities");

        string[] lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);
        int created = 0, skipped = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] col = ParseCSVLine(line);

            // Skip rows with no name
            if (col.Length < 2 || string.IsNullOrEmpty(col[1].Trim()))
            {
                skipped++;
                continue;
            }

            int    id          = int.Parse(col[0].Trim());
            string abilityName = col[1].Trim();
            string trigger     = col.Length > 2 ? col[2].Trim() : "";
            string effect      = col.Length > 3 ? col[3].Trim() : "";
            string value       = col.Length > 4 ? col[4].Trim() : "";
            string condition   = col.Length > 5 ? col[5].Trim() : "";
            string description = col.Length > 6 ? col[6].Trim() : "";

            string assetPath = $"{outputFolder}/{id:000} {abilityName}.asset";

            AbilityData data = AssetDatabase.LoadAssetAtPath<AbilityData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AbilityData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.abilityID   = id;
            data.abilityName = abilityName;
            data.trigger     = trigger;
            data.effect      = effect;
            data.value       = value;
            data.condition   = condition;
            data.description = description;

            EditorUtility.SetDirty(data);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Ability import complete: {created} imported, {skipped} skipped.");
    }

    // Handles quoted fields with commas inside (e.g. "After attack, heal 5 HP.")
    private static string[] ParseCSVLine(string line)
    {
        var result   = new List<string>();
        var current  = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
                current.Append(c);
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}
