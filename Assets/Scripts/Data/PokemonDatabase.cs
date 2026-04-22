using UnityEngine;

// Holds a reference to every PokemonData asset in the project.
// Populated automatically by MainMenuSceneGenerator.
// Placed in Assets/Resources/PokemonDatabase.asset so it can be loaded at runtime.

[CreateAssetMenu(fileName = "PokemonDatabase", menuName = "Pokemon/PokemonDatabase")]
public class PokemonDatabase : ScriptableObject
{
    public PokemonData[] allPokemon;

    public PokemonData GetById(int id)
    {
        foreach (var p in allPokemon)
            if (p != null && p.id == id) return p;
        return null;
    }
}
