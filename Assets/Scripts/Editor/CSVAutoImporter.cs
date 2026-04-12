using UnityEditor;

// Watches the two CSV files and re-runs the matching importer whenever they are saved.
// Works automatically — just save the CSV and Unity will update the assets.

public class CSVAutoImporter : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (path == "Assets/Data/Ability_data.csv")
            {
                UnityEngine.Debug.Log("[AutoImport] Ability CSV changed — reimporting abilities...");
                AbilityCSVImporter.ImportFromCSV();
            }
            else if (path == "Assets/Data/Pokémon_data.csv")
            {
                UnityEngine.Debug.Log("[AutoImport] Pokémon CSV changed — reimporting Pokémon...");
                PokemonCSVImporter.ImportFromCSV();
            }
        }
    }
}
