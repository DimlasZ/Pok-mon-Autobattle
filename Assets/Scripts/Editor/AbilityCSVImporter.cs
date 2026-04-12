using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AbilityCSVImporter
{
    // CSV column order:
    // 0=ID, 1=Name, 2=Trigger, 3=Target, 4=Count, 5=Effect, 6=Value,
    // 7=Condition, 8=Chance, 9=Custom, 10=VFXSheet, 11=VFXRow, 12=Description

    [MenuItem("Pokemon/Import Abilities from CSV")]
    public static void ImportFromCSV()
    {
        string csvPath      = "Assets/Data/Ability_data.csv";
        string outputFolder = "Assets/Data/Abilities";

        if (!File.Exists(csvPath))
        {
            Debug.LogError("Ability CSV not found at: " + csvPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(outputFolder))
            AssetDatabase.CreateFolder("Assets/Data", "Abilities");

        string[] lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);
        int created = 0, updated = 0, skipped = 0;

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

            if (!int.TryParse(col[0].Trim(), out int id)) continue;

            string abilityName = Get(col, 1);
            string trigger     = Get(col, 2);
            string target      = Get(col, 3);
            string countStr    = Get(col, 4);
            string effect      = Get(col, 5);
            string value       = Get(col, 6);
            string condition   = Get(col, 7);
            string chanceStr   = Get(col, 8);
            string custom      = Get(col, 9);
            string vfxSheet    = Get(col, 10);
            string vfxRowStr   = Get(col, 11);
            string description = Get(col, 12);

            int.TryParse(countStr, out int count);
            int.TryParse(vfxRowStr, out int vfxRow);

            float.TryParse(chanceStr,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out float chance);

            string assetPath = $"{outputFolder}/{id:000} {abilityName}.asset";

            AbilityData data = AssetDatabase.LoadAssetAtPath<AbilityData>(assetPath);
            bool isNew = data == null;
            if (isNew)
            {
                data = ScriptableObject.CreateInstance<AbilityData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.abilityID   = id;
            data.abilityName = abilityName;
            data.trigger     = trigger;
            data.target      = target;
            data.count       = count;
            data.effect      = effect;
            data.value       = value;
            data.condition   = condition;
            data.chance      = chance;
            data.custom      = custom;
            data.vfxSheet    = vfxSheet;
            data.vfxRow      = vfxRow;
            data.description = description;

            EditorUtility.SetDirty(data);

            if (isNew) created++;
            else       updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Ability import complete: {created} created, {updated} updated, {skipped} skipped.");
    }

    private static string Get(string[] col, int index)
        => col.Length > index ? col[index].Trim() : "";

    // Handles quoted fields with commas inside (e.g. "After attack, heal 5 HP.")
    private static string[] ParseCSVLine(string line)
    {
        var result  = new List<string>();
        var current = new System.Text.StringBuilder();
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
