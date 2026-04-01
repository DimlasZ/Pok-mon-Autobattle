using UnityEngine;

[CreateAssetMenu(fileName = "New Ability", menuName = "Pokemon/AbilityData")]
public class AbilityData : ScriptableObject
{
    public int    abilityID;
    public string abilityName;
    public string trigger;     // on_battle_start | on_round_start | before_attack | on_attack | after_attack | before_hit | on_hit | on_kill | on_faint | passive
    public string effect;      // heal_self | damage_all_enemies | damage_next_enemy | boost_attack | etc.
    public string value;       // stored as string — numeric ("1.5") or text ("rain", "light_screen")
    public string condition;   // hp_below_33 | weather_rain | first_enemy | at_full_hp | etc.
    public string description;

    // Returns value parsed as float. Returns 0 if value is non-numeric (e.g. "rain").
    public float FloatValue
    {
        get
        {
            if (float.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out float f))
                return f;
            return 0f;
        }
    }
}
