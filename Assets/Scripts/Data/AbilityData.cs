using UnityEngine;

[CreateAssetMenu(fileName = "New Ability", menuName = "Pokemon/AbilityData")]
public class AbilityData : ScriptableObject
{
    public int    abilityID;
    public string abilityName;
    public string trigger;      // on_battle_start | on_round_start | before_attack | on_attack | after_attack | before_hit | on_hit | on_kill | on_faint | on_ally_faint | on_debuff | on_heal | passive
    public string target;       // self | enemy_front | enemy_second | enemy_next | enemy_last | enemy_all | enemy_random | all_others | all | attacker | ally_all | ally_random | ally_last | ally_behind
    public int    count;        // how many targets from front/back (0 = unset, treated as 1)
    public string effect;       // heal | heal_flat | leech | deal_damage | deal_damage_flat | deal_damage_percent | boost_attack | boost_attack_once | boost_speed | boost_all_stats | boost_speed_and_attack | boost_hp | boost_damage_taken | boost_healing | boost_ability_damage | reduce_attack | reduce_speed | reduce_healing | summon_weather | summon_screen | survive_ko | negate_damage | recoil_damage | damage_reduction | damage_reduction_flat | immune_to_ability_damage | immune_to_debuff | immune | ignore_damage_reduction | ignore_abilities | negate_weather | swap_enemies | move_ally_to_front | cure_status | solar_power | moody
    public string value;        // numeric ("1.5", "20") or keyword ("rain", "sun", "sandstorm", "light_screen")
    public string condition;    // hp_below_N | full_hp | weather_sun | weather_rain | weather_sandstorm | super_effective | first_hit | first_last
    public float  chance;       // 0 = always trigger, 0.3 = 30% chance
    public string custom;       // flags special handling: eruption | serene_grace
    public string description;
    public string vfxSheet;    // PNG name in Resources/VFX/Sprites/ (without extension), e.g. "fireblast"
    public int    vfxRow;      // which color row to play (0 = first row)

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

    public bool ShouldTrigger()
    {
        if (chance <= 0f) return true;
        return Random.value <= chance;
    }
}
