using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// BattleSceneManager lives only in the BattleScene.
// It reads the player's team from GameManager, generates the enemy team,
// then runs the battle as a coroutine so the player can watch it play out.
//
// Three playback modes:
//   Step     — click the Step button to advance one action at a time
//   Auto     — actions happen automatically at normal speed (1 second apart)
//   SpeedUp  — actions happen automatically at fast speed (0.2 seconds apart)

public class BattleSceneManager : MonoBehaviour
{
    [Header("Player Team (left side)")]
    public PokemonSlotUI[] playerSlots; // 3 slots

    [Header("Enemy Team (right side)")]
    public PokemonSlotUI[] enemySlots;  // 3 slots

    [Header("Battle Log")]
    public TextMeshProUGUI battleLogText; // Shows what just happened

    [Header("Result Banner")]
    public TextMeshProUGUI resultText;    // Shows WIN / LOSS / DRAW at the end

    [Header("Playback Buttons")]
    public Button stepButton;
    public Button autoButton;
    public Button speedUpButton;

    [Header("Continue Button")]
    public Button continueButton; // Hidden until battle is over

    // --- Playback ---
    public enum PlaybackMode { Step, Auto, SpeedUp }
    private PlaybackMode currentMode = PlaybackMode.Auto;

    private const float autoDelay    = 1.0f;  // seconds between actions in Auto mode
    private const float speedUpDelay = 0.15f; // seconds between actions in SpeedUp mode

    private bool stepRequested = false; // set to true when Step button is clicked

    // Working copies of teams for this battle (fresh HP, won't affect ShopManager)
    private List<PokemonInstance> playerTeam = new List<PokemonInstance>();
    private List<PokemonInstance> enemyTeam  = new List<PokemonInstance>();

    // -------------------------------------------------------

    private void Start()
    {
        // Wire up buttons
        stepButton.onClick.AddListener(OnStepClicked);
        autoButton.onClick.AddListener(OnAutoClicked);
        speedUpButton.onClick.AddListener(OnSpeedUpClicked);
        continueButton.onClick.AddListener(OnContinueClicked);

        // Hide Continue and result until the battle ends
        continueButton.gameObject.SetActive(false);
        resultText.gameObject.SetActive(false);

        // Set default mode
        SetMode(PlaybackMode.Auto);

        // Build player team — create fresh instances so HP is full at battle start
        playerTeam = GameManager.Instance.PlayerBattleTeam
            .Where(p => p != null)
            .Select(p => new PokemonInstance(p.baseData))
            .ToList();

        // Generate enemy team via BattleManager
        enemyTeam = BattleManager.Instance.GenerateEnemyTeam();

        // Show both teams on screen
        DisplayTeams();

        // Start the battle coroutine
        StartCoroutine(RunBattleCoroutine());
    }

    // -------------------------------------------------------
    // TEAM DISPLAY
    // -------------------------------------------------------

    private void DisplayTeams()
    {
        DisplayTeamInSlots(playerTeam, playerSlots);
        DisplayTeamInSlots(enemyTeam,  enemySlots);
    }

    private void DisplayTeamInSlots(List<PokemonInstance> team, PokemonSlotUI[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < team.Count && team[i] != null)
                slots[i].DisplayPokemon(team[i]);
            else
                slots[i].DisplayEmpty();
        }
    }

    // Refreshes just the HP values shown in slots (called after each attack)
    private void RefreshHP()
    {
        DisplayTeams();
    }

    // -------------------------------------------------------
    // BATTLE COROUTINE
    // -------------------------------------------------------

    private IEnumerator RunBattleCoroutine()
    {
        Log("Battle start!");
        yield return WaitForPlayback();

        BattleManager.BattleResult result = BattleManager.BattleResult.Draw;

        for (int turn = 1; turn <= 20; turn++)
        {
            // Get the front alive Pokemon from each side
            PokemonInstance playerFront = GetFront(playerTeam);
            PokemonInstance enemyFront  = GetFront(enemyTeam);

            if (playerFront == null || enemyFront == null) break;

            Log($"— Turn {turn} —");
            yield return WaitForPlayback();

            // Determine attack order based on Speed (tie = random)
            bool playerGoesFirst;
            if (playerFront.baseData.speed != enemyFront.baseData.speed)
                playerGoesFirst = playerFront.baseData.speed > enemyFront.baseData.speed;
            else
                playerGoesFirst = Random.value > 0.5f;

            PokemonInstance first  = playerGoesFirst ? playerFront : enemyFront;
            PokemonInstance second = playerGoesFirst ? enemyFront  : playerFront;

            Log($"{first.baseData.pokemonName} (Spd {first.baseData.speed}) goes before {second.baseData.pokemonName}");
            yield return WaitForPlayback();

            // First Pokemon attacks
            string attackLog = GetAttackLog(first, second);
            BattleManager.Instance.Attack(first, second, playerGoesFirst ? playerTeam : enemyTeam, playerGoesFirst ? enemyTeam : playerTeam);
            Log(attackLog);
            RefreshHP();
            yield return WaitForPlayback();

            if (second.currentHP <= 0)
            {
                Log($"{second.baseData.pokemonName} fainted!");
                yield return WaitForPlayback();
            }
            else
            {
                // Second Pokemon attacks back
                string counterLog = GetAttackLog(second, first);
                BattleManager.Instance.Attack(second, first, playerGoesFirst ? enemyTeam : playerTeam, playerGoesFirst ? playerTeam : enemyTeam);
                Log(counterLog);
                RefreshHP();
                yield return WaitForPlayback();

                if (first.currentHP <= 0)
                {
                    Log($"{first.baseData.pokemonName} fainted!");
                    yield return WaitForPlayback();
                }
            }

            // Remove fainted Pokemon
            playerTeam.RemoveAll(p => p.currentHP <= 0);
            enemyTeam.RemoveAll(p => p.currentHP <= 0);
            DisplayTeams();

            // Check end conditions
            bool playerWiped = playerTeam.Count == 0;
            bool enemyWiped  = enemyTeam.Count  == 0;

            if (playerWiped && enemyWiped) { result = BattleManager.BattleResult.Draw;       break; }
            if (enemyWiped)                { result = BattleManager.BattleResult.PlayerWin;  break; }
            if (playerWiped)               { result = BattleManager.BattleResult.PlayerLoss; break; }
        }

        // Battle over — show result
        yield return ShowResult(result);
    }

    // -------------------------------------------------------
    // RESULT
    // -------------------------------------------------------

    private IEnumerator ShowResult(BattleManager.BattleResult result)
    {
        string text  = result switch
        {
            BattleManager.BattleResult.PlayerWin  => "VICTORY!",
            BattleManager.BattleResult.PlayerLoss => "DEFEAT",
            _                                     => "DRAW"
        };

        Color color = result switch
        {
            BattleManager.BattleResult.PlayerWin  => new Color(0.2f, 1f, 0.2f),
            BattleManager.BattleResult.PlayerLoss => new Color(1f, 0.2f, 0.2f),
            _                                     => new Color(1f, 0.8f, 0.2f)
        };

        Log($"Battle over: {text}");
        resultText.text  = text;
        resultText.color = color;
        resultText.gameObject.SetActive(true);

        // Tell GameManager the result
        GameManager.Instance.OnBattleComplete(result);

        // Show Continue button
        yield return new WaitForSeconds(0.5f);
        continueButton.gameObject.SetActive(true);
    }

    // -------------------------------------------------------
    // ATTACK LOG HELPER
    // Builds the log message BEFORE the attack happens (so we can show pre-calculated info)
    // -------------------------------------------------------

    private string GetAttackLog(PokemonInstance attacker, PokemonInstance defender)
    {
        float multiplier = TypeChart.GetMultiplier(
            attacker.baseData.type1,
            defender.baseData.type1
        );

        int damage = Mathf.CeilToInt(attacker.attack * multiplier);

        string effectiveness = multiplier switch
        {
            0f         => " — No effect!",
            >= 4f      => " — Super effective! (x4)",
            >= 2f      => " — Super effective!",
            <= 0.25f   => " — Not very effective (x0.25)",
            <= 0.5f    => " — Not very effective",
            _          => ""
        };

        return $"{attacker.baseData.pokemonName} → {defender.baseData.pokemonName}: {damage} dmg{effectiveness}";
    }

    // -------------------------------------------------------
    // PLAYBACK CONTROL
    // -------------------------------------------------------

    // Returns a coroutine that waits based on the current mode
    private IEnumerator WaitForPlayback()
    {
        if (currentMode == PlaybackMode.Step)
        {
            // Wait until the player clicks Step (keep button enabled so it can be clicked)
            if (!stepRequested)
            {
                while (!stepRequested)
                    yield return null;
            }
            stepRequested = false; // consume the request
        }
        else if (currentMode == PlaybackMode.Auto)
        {
            yield return new WaitForSeconds(autoDelay);
        }
        else // SpeedUp
        {
            yield return new WaitForSeconds(speedUpDelay);
        }
    }

    private void SetMode(PlaybackMode mode)
    {
        currentMode = mode;

        // Highlight the active button
        SetButtonHighlight(stepButton,    mode == PlaybackMode.Step);
        SetButtonHighlight(autoButton,    mode == PlaybackMode.Auto);
        SetButtonHighlight(speedUpButton, mode == PlaybackMode.SpeedUp);
    }

    private void SetButtonHighlight(Button btn, bool active)
    {
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = active ? new Color(0.4f, 0.7f, 0.4f) : new Color(0.25f, 0.25f, 0.25f);
    }

    // -------------------------------------------------------
    // BUTTON CALLBACKS
    // -------------------------------------------------------

    private void OnStepClicked()
    {
        SetMode(PlaybackMode.Step);
        stepRequested = true; // Advance one step
    }

    private void OnAutoClicked()    => SetMode(PlaybackMode.Auto);
    private void OnSpeedUpClicked() => SetMode(PlaybackMode.SpeedUp);

    private void OnContinueClicked() => GameManager.Instance.ReturnToShop();

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    private PokemonInstance GetFront(List<PokemonInstance> team)
        => team.FirstOrDefault(p => p.currentHP > 0);

    private void Log(string message)
    {
        battleLogText.text = message;
        Debug.Log("[Battle] " + message);
    }
}
