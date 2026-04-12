using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

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
    [Header("VFX")]
    public GameObject vfxPrefab;        // Assign the VFXPlayer prefab here
    public float      vfxFps = 15f;     // Playback speed for all VFX

    [Header("Player Team (left side)")]
    public PokemonSlotUI[] playerSlots; // 6 slots (2x3 grid, slots 0-2 = front row, 3-5 = back row)

    [Header("Enemy Team (right side)")]
    public PokemonSlotUI[] enemySlots;  // 6 slots (same layout)

    [Header("Player HP")]
    public TextMeshProUGUI playerHPLabel; // TopBar HP display

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

    private const float autoDelay    = 2f;  // seconds between actions in Auto mode
    private const float speedUpDelay = 1f; // seconds between actions in SpeedUp mode

    private bool stepRequested = false; // set to true when Step button is clicked

    // Working copies of teams for this battle (fresh HP, won't affect ShopManager)
    private List<PokemonInstance> playerTeam = new List<PokemonInstance>();
    private List<PokemonInstance> enemyTeam  = new List<PokemonInstance>();

    // How many slots are active this battle (captured at Start, stays fixed during the fight)
    private int activeSlots;

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

        // Generate enemy team
        enemyTeam = EnemyGenerator.GenerateEnemyTeam();

        // Tag enemy instances so they show as "Enemy X" in logs
        foreach (var e in enemyTeam)
            e.displayName = "Enemy " + e.baseData.pokemonName;

        // Capture active slot count (max of both sides, they should be equal)
        activeSlots = Mathf.Max(playerTeam.Count, enemyTeam.Count);
        activeSlots = Mathf.Min(activeSlots, ShopManager.MaxBattleSize);

        // Register teams with AbilitySystem so it can resolve them without passing everywhere
        AbilitySystem.InitBattle(playerTeam, enemyTeam);

        // Show both teams on screen
        DisplayTeams();

        if (playerHPLabel != null)
            playerHPLabel.text = $"HP: {GameManager.Instance.PlayerHP}/{GameManager.Instance.playerMaxHP}";

        AudioManager.Instance?.PlayMusic("Trainerbattle");

        // Start the battle coroutine
        StartCoroutine(RunBattleCoroutine());
    }

    // -------------------------------------------------------
    // TEAM DISPLAY
    // -------------------------------------------------------

    private void DisplayTeams()
    {
        DisplayTeamInSlots(playerTeam, playerSlots, activeSlots);
        DisplayTeamInSlots(enemyTeam,  enemySlots,  activeSlots);
    }

    private void DisplayTeamInSlots(List<PokemonInstance> team, PokemonSlotUI[] slots, int activeSize)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            bool inRange = i < activeSize;
            slots[i].gameObject.SetActive(inRange);
            if (!inRange) continue;

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
        AbilitySystem.ResetBattleState();

        Log("Battle start!");
        yield return WaitForPlayback();

        AbilitySystem.FireBattleStart(playerTeam, enemyTeam);
        AbilitySystem.FireBattleStart(enemyTeam, playerTeam);

        BattleResult result = BattleResult.Draw;

        for (int turn = 1; turn <= 20; turn++)
        {
            // Get the front alive Pokemon from each side
            PokemonInstance playerFront = GetFront(playerTeam);
            PokemonInstance enemyFront  = GetFront(enemyTeam);

            if (playerFront == null || enemyFront == null) break;

            AbilitySystem.FireRoundStart(playerTeam, enemyTeam);
            AbilitySystem.FireRoundStart(enemyTeam, playerTeam);

            // Determine attack order based on Speed (tie = random)
            bool playerGoesFirst;
            if (playerFront.speed != enemyFront.speed)
                playerGoesFirst = playerFront.speed > enemyFront.speed;
            else
                playerGoesFirst = Random.value > 0.5f;

            PokemonInstance first  = playerGoesFirst ? playerFront : enemyFront;
            PokemonInstance second = playerGoesFirst ? enemyFront  : playerFront;

            // First Pokemon attacks
            string attackLog = GetAttackLog(first, second);
            Log(attackLog);
            yield return PlayAttackAnim(first);
            SpawnVFX(first, second, GetVFXSheet(first), GetVFXRow(first));
            DamageCalculator.Attack(first, second, playerGoesFirst ? playerTeam : enemyTeam, playerGoesFirst ? enemyTeam : playerTeam);
            RefreshHP();
            yield return WaitForPlayback();

            if (second.currentHP <= 0)
            {
            }
            else
            {
                // Second Pokemon attacks back
                string counterLog = GetAttackLog(second, first);
                Log(counterLog);
                yield return PlayAttackAnim(second);
                SpawnVFX(second, first, GetVFXSheet(second), GetVFXRow(second));
                DamageCalculator.Attack(second, first, playerGoesFirst ? enemyTeam : playerTeam, playerGoesFirst ? playerTeam : enemyTeam);
                RefreshHP();
                yield return WaitForPlayback();

                if (first.currentHP <= 0)
                {
                }
            }

            // Remove fainted Pokemon
            playerTeam.RemoveAll(p => p.currentHP <= 0);
            enemyTeam.RemoveAll(p => p.currentHP <= 0);
            DisplayTeams();

            // Check end conditions
            bool playerWiped = playerTeam.Count == 0;
            bool enemyWiped  = enemyTeam.Count  == 0;

            if (playerWiped && enemyWiped) { result = BattleResult.Draw;       break; }
            if (enemyWiped)                { result = BattleResult.PlayerWin;  break; }
            if (playerWiped)               { result = BattleResult.PlayerLoss; break; }
        }

        // Battle over — show result
        yield return ShowResult(result);
    }

    // -------------------------------------------------------
    // RESULT
    // -------------------------------------------------------

    private IEnumerator ShowResult(BattleResult result)
    {
        string text  = result switch
        {
            BattleResult.PlayerWin  => "VICTORY!",
            BattleResult.PlayerLoss => "DEFEAT",
            _                                     => "DRAW"
        };

        Color color = result switch
        {
            BattleResult.PlayerWin  => new Color(0.2f, 1f, 0.2f),
            BattleResult.PlayerLoss => new Color(1f, 0.2f, 0.2f),
            _                                     => new Color(1f, 0.8f, 0.2f)
        };

        Log($"Battle over: {text}");
        resultText.text  = text;
        resultText.color = color;
        resultText.gameObject.SetActive(true);

        if (result == BattleResult.PlayerWin)
            AudioManager.Instance?.PlayMusic("Victory");

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
            0f       => " — No effect!",
            >= 1.5f  => " — Super effective!",
            <= 0.75f => " — Not very effective",
            _        => ""
        };

        return $"{attacker.DisplayName} -> {defender.DisplayName}: {damage} dmg{effectiveness}";
    }

    // -------------------------------------------------------
    // PLAYBACK CONTROL
    // -------------------------------------------------------

    // Returns a coroutine that waits based on the current mode.
    // Polls every frame so any mode switch (Step ↔ Auto ↔ SpeedUp) takes effect immediately.
    // Timer resets whenever the mode changes, giving the new mode a clean start.
    private IEnumerator WaitForPlayback()
    {
        float timer = 0f;
        PlaybackMode lastMode = currentMode;

        while (true)
        {
            // Mode changed — reset the timer so the new mode gets a clean delay
            if (currentMode != lastMode)
            {
                timer    = 0f;
                lastMode = currentMode;
            }

            switch (currentMode)
            {
                case PlaybackMode.Step:
                    if (stepRequested)
                    {
                        stepRequested = false;
                        yield break;
                    }
                    break;

                case PlaybackMode.Auto:
                    timer += Time.deltaTime;
                    if (timer >= autoDelay) yield break;
                    break;

                case PlaybackMode.SpeedUp:
                    timer += Time.deltaTime;
                    if (timer >= speedUpDelay) yield break;
                    break;
            }

            yield return null;
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

    private void OnContinueClicked()
    {
        AudioManager.Instance?.PlayMusic("Pokémon Center");
        GameManager.Instance.ReturnToShop();
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    // -------------------------------------------------------
    // ATTACK ANIMATION
    // -------------------------------------------------------

    private IEnumerator PlayAttackAnim(PokemonInstance attacker)
    {
        var slot = GetSlotFor(attacker);
        if (slot == null) yield break;

        float dir = playerTeam.Contains(attacker) ? 1f : -1f;
        Vector3 origin  = slot.transform.localPosition;
        Vector3 windUp  = origin + new Vector3(-dir * 10f,  0, 0); // pull back
        Vector3 lunge   = origin + new Vector3( dir * 50f, 0, 0); // lunge forward

        var seq = DOTween.Sequence();
        seq.Append(slot.transform.DOLocalMove(windUp,  0.08f).SetEase(Ease.OutQuad));
        seq.Append(slot.transform.DOLocalMove(lunge,   0.12f).SetEase(Ease.OutQuad));
        seq.AppendCallback(() => AudioManager.Instance?.PlaySound("Audio/Combat/hit_sound"));
        seq.Append(slot.transform.DOLocalMove(origin,  0.10f).SetEase(Ease.InQuad));
        yield return seq.WaitForCompletion();
    }

    private PokemonSlotUI GetSlotFor(PokemonInstance p)
    {
        int idx = playerTeam.IndexOf(p);
        if (idx >= 0 && idx < playerSlots.Length) return playerSlots[idx];
        idx = enemyTeam.IndexOf(p);
        if (idx >= 0 && idx < enemySlots.Length) return enemySlots[idx];
        return null;
    }

    private PokemonInstance GetFront(List<PokemonInstance> team)
        => team.FirstOrDefault(p => p.currentHP > 0);

    private void Log(string message)
    {
        battleLogText.text = message;
        Debug.Log("[Battle] " + message);
    }

    // -------------------------------------------------------
    // VFX
    // -------------------------------------------------------

    // Returns the VFX sheet name for an attacker: ability sheet if set, otherwise type name.
    private string GetVFXSheet(PokemonInstance attacker)
    {
        if (attacker.baseData.ability != null && !string.IsNullOrEmpty(attacker.baseData.ability.vfxSheet))
            return attacker.baseData.ability.vfxSheet;
        return attacker.baseData.type1.ToString().ToLower();
    }

    private int GetVFXRow(PokemonInstance attacker)
        => attacker.baseData.ability?.vfxRow ?? 0;

    // Spawn a VFX animation that travels from the source slot to the target slot.
    // sheetName must match the PNG filename in Resources/VFX/Sprites/ (without extension).
    // row: which color row to play (0 = first row). cols: frames per row (default 13).
    public void SpawnVFX(PokemonInstance source, PokemonInstance target, string sheetName, int row = 0, int cols = 13)
    {
        if (vfxPrefab == null) return;

        var sourceSlot = GetSlotFor(source);
        var targetSlot = GetSlotFor(target);
        if (sourceSlot == null || targetSlot == null) return;

        // Load all sliced sprites from the sheet (must be in a Resources/ folder)
        Sprite[] all = Resources.LoadAll<Sprite>("VFX/Sprites/" + sheetName);
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning($"[VFX] No sprites found for sheet '{sheetName}'");
            return;
        }

        // Slice out the requested row
        int start = row * cols;
        int end   = Mathf.Min(start + cols, all.Length);
        if (start >= all.Length)
        {
            Debug.LogWarning($"[VFX] Row {row} out of range for sheet '{sheetName}' ({all.Length} frames)");
            return;
        }
        Sprite[] frames = all[start..end];

        // Instantiate at the source slot, as a child of the Canvas so it renders in UI space
        var canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        var go     = Instantiate(vfxPrefab, canvas.transform);
        go.transform.position = sourceSlot.transform.position;

        // Travel to the target slot — animation plays during the flight
        float travelTime = Vector3.Distance(sourceSlot.transform.position, targetSlot.transform.position) / 800f;
        travelTime = Mathf.Clamp(travelTime, 0.15f, 0.5f);
        go.transform.DOMove(targetSlot.transform.position, travelTime).SetEase(Ease.InQuad);

        go.GetComponent<VFXPlayer>().Play(frames, vfxFps);
    }
}
