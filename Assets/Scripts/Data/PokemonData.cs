using UnityEngine;

[CreateAssetMenu(fileName = "New Pokemon", menuName = "Pokemon/PokemonData")]
public class PokemonData : ScriptableObject
{
    public int id;
    public string pokemonName;
    public int attack;
    public int hp;
    public string type1;
    public int speed;
    public int tier;
    public AbilityData ability;
    public Sprite sprite;
}
