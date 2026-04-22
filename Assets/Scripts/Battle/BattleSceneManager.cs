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

    [Header("Progress Icons (TopBar)")]
    public Image[] badgeImages;   // 8 gym badges — grey until won
    public Image[] starImages;    // 4 Elite 4 stars — grey until won
    public Image   champImage;    // Champion — grey until won
    public Image[] heartImages;   // Player HP hearts — white = alive, grey = lost

    [Header("Battle Log")]
    public TextMeshProUGUI battleLogText; // Shows what just happened

    [Header("Playback Buttons")]
    public Button stepButton;
    public Button autoButton;
    public Button speedUpButton;

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

    // Set by MultiplayerBattleSync before scene load in multiplayer; 0 = singleplayer
    public static int _pendingMultiplayerSeed = 0;

    // -------------------------------------------------------

    private void Start()
    {
        // Wire up buttons
        stepButton.onClick.AddListener(OnStepClicked);
        autoButton.onClick.AddListener(OnAutoClicked);
        speedUpButton.onClick.AddListener(OnSpeedUpClicked);

        // Set default mode
        SetMode(PlaybackMode.Auto);

        // Reuse existing instances (preserving rolled stats) — just reset HP/multipliers
        playerTeam = GameManager.Instance.PlayerBattleTeam
            .Where(p => p != null)
            .ToList();
        playerTeam.ForEach(p => p.ResetForBattle());

        // In multiplayer use the opponent's real team; in singleplayer generate an enemy team.
        var mpPending = MultiplayerNetworkManager.Instance?.PendingOpponentTeam;
        if (mpPending != null && mpPending.Length > 0)
        {
            enemyTeam = mpPending.Where(p => p != null).ToList();
            MultiplayerNetworkManager.Instance.PendingOpponentTeam = null;
        }
        else
            enemyTeam = EnemyGenerator.GenerateEnemyTeam();

        // Tag enemy instances so they show as "Enemy X" in logs
        foreach (var e in enemyTeam)
            e.displayName = "Enemy " + e.baseData.pokemonName;

        // Capture active slot count (max of both sides, they should be equal)
        activeSlots = Mathf.Max(playerTeam.Count, enemyTeam.Count);
        activeSlots = Mathf.Min(activeSlots, ShopManager.MaxBattleSize);

        // Set RNG seed — in multiplayer this is the shared seed from MultiplayerBattleSync.
        // In singleplayer a fresh random seed is used (non-determinism is fine there).
        int battleSeed = MultiplayerNetworkManager.Instance != null && MultiplayerNetworkManager.Instance.IsConnected
            ? _pendingMultiplayerSeed
            : UnityEngine.Random.Range(1, int.MaxValue);
        AbilitySystem.SetRng(battleSeed);

        // Register teams with AbilitySystem so it can resolve them without passing everywhere
        AbilitySystem.InitBattle(playerTeam, enemyTeam);

        // Show both teams on screen
        DisplayTeams();

        RefreshProgressIcons();

        // Music is started by SceneTransitionManager at the beginning of the battle intro transition

        // Subscribe to the ability VFX event and weather changes
        AbilitySystem.OnAbilityFired   += OnAbilityFiredHandler;
        AbilitySystem.OnWeatherChanged += OnWeatherChangedHandler;

        // Start the battle coroutine
        StartCoroutine(RunBattleCoroutine());
    }

    private void OnDestroy()
    {
        AbilitySystem.OnAbilityFired   -= OnAbilityFiredHandler;
        AbilitySystem.OnWeatherChanged -= OnWeatherChangedHandler;
    }

    // Called by AbilitySystem whenever an ability resolves its targets.
    // Spawns a VFX that travels from the ability user to each resolved target.
    private void OnAbilityFiredHandler(PokemonInstance source, AbilityData ab, System.Collections.Generic.List<PokemonInstance> targets)
    {
        foreach (var target in targets)
        {
            if (target == null || target.currentHP <= 0) continue;
            SpawnVFX(source, target, ab.vfxSheet, ab.vfxRow);
        }
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

        // Fire on_battle_start abilities — same speed fires simultaneously.
        // Only log and pause for abilities that actually did something.
        foreach (var group in AbilitySystem.GetSpeedOrder("on_battle_start", playerTeam, enemyTeam)
                     .GroupBy(e => e.Item1.speed).OrderByDescending(g => g.Key))
        {
            var fired = new List<PokemonInstance>();
            foreach (var (p, own, opp) in group)
                if (AbilitySystem.FireSingle("on_battle_start", p, own, opp))
                    fired.Add(p);
            if (fired.Count > 0)
            {
                Log(string.Join(" + ", fired.Select(p => $"{p.DisplayName}'s {p.baseData.ability.abilityName}")) + "!");
                RefreshHP();
                yield return WaitForPlayback();
            }
        }

        BattleResult result = BattleResult.Draw;

        for (int turn = 1; turn <= 20; turn++)
        {
            // Get the front alive Pokemon from each side
            PokemonInstance playerFront = GetFront(playerTeam);
            PokemonInstance enemyFront  = GetFront(enemyTeam);

            if (playerFront == null || enemyFront == null) break;

            // Fire on_round_start abilities — same speed fires simultaneously.
            // Only log and pause for abilities that actually did something.
            foreach (var group in AbilitySystem.GetSpeedOrder("on_round_start", playerTeam, enemyTeam)
                         .GroupBy(e => e.Item1.speed).OrderByDescending(g => g.Key))
            {
                var fired = new List<PokemonInstance>();
                foreach (var (p, own, opp) in group)
                    if (AbilitySystem.FireSingle("on_round_start", p, own, opp))
                        fired.Add(p);
                if (fired.Count > 0)
                {
                    Log(string.Join(" + ", fired.Select(p => $"{p.DisplayName}'s {p.baseData.ability.abilityName}")) + "!");
                    RefreshHP();
                    yield return WaitForPlayback();
                }
            }

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
                DamageCalculator.Attack(second, first, playerGoesFirst ? enemyTeam : playerTeam, playerGoesFirst ? playerTeam : enemyTeam);
                RefreshHP();
                yield return WaitForPlayback();

                if (first.currentHP <= 0)
                {
                }
            }

            // End-of-round weather tick (sandstorm chip damage)
            var weatherTicks = AbilitySystem.GetWeatherTick(playerTeam, enemyTeam);
            if (weatherTicks.Count > 0)
            {
                AudioManager.Instance?.PlaySound("Audio/Sounds/sandstormdamage");
                foreach (var (p, dmg) in weatherTicks)
                {
                    SpawnVFX(p, p, "claw", 6);
                    p.currentHP = Mathf.Max(0, p.currentHP - dmg);
                    Debug.Log($"[Sandstorm] {p.DisplayName} takes {dmg} chip damage ({p.currentHP}/{p.maxHP})");
                    var team    = playerTeam.Contains(p) ? playerTeam : enemyTeam;
                    var foeTeam = playerTeam.Contains(p) ? enemyTeam  : playerTeam;
                    AbilitySystem.FireAfterHit(p, team);
                    if (p.currentHP == 0)
                    {
                        Debug.Log($"[Sandstorm] {p.DisplayName} fainted!");
                        AbilitySystem.FireOnFaint(p, team, foeTeam);
                    }
                }
                Log("The sandstorm rages!");
                RefreshHP();
                yield return WaitForPlayback();
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
        // Stop weather effects when combat ends
        OnWeatherChangedHandler("");

        AudioManager.Instance?.StopMusic();
        if (result == BattleResult.PlayerWin)
            AudioManager.Instance?.PlayMusic("Victory");
        else if (result == BattleResult.PlayerLoss)
            AudioManager.Instance?.PlayMusicOnce("Loss");

        // Tell GameManager the result — this triggers the progress overlay
        GameManager.Instance.OnBattleComplete(result);

        yield return null;
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

        float abilityMultiplier  = AbilitySystem.FireBeforeAttack(attacker, null, null);
        float passiveMultiplier  = AbilitySystem.GetPassiveAttackMultiplier(attacker);
        float weatherMultiplier  = AbilitySystem.GetWeatherDamageMultiplier(attacker);

        int damage = Mathf.CeilToInt(attacker.attack * multiplier * abilityMultiplier * passiveMultiplier * weatherMultiplier);

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

    private static readonly Color IconLocked   = new Color(0.2f, 0.2f, 0.2f, 0.7f);
    private static readonly Color IconUnlocked = Color.white;

    private void RefreshProgressIcons()
    {
        int wins = GameManager.Instance.PlayerWins;
        int hp   = GameManager.Instance.PlayerHP;

        for (int i = 0; i < badgeImages.Length; i++)
            if (badgeImages[i] != null)
                badgeImages[i].color = wins > i ? IconUnlocked : IconLocked;

        for (int i = 0; i < starImages.Length; i++)
            if (starImages[i] != null)
                starImages[i].color = wins > 8 + i ? IconUnlocked : IconLocked;

        if (champImage != null)
            champImage.color = wins >= GameManager.Instance.winsToVictory ? IconUnlocked : IconLocked;

        for (int i = 0; i < heartImages.Length; i++)
            if (heartImages[i] != null)
                heartImages[i].color = i < hp ? IconUnlocked : IconLocked;
    }

    private void Log(string message)
    {
        battleLogText.text = message;
        Debug.Log("[Battle] " + message);
    }

    // -------------------------------------------------------
    // VFX
    // -------------------------------------------------------

    // Spawn a VFX animation that travels from the source slot to the target slot.
    // sheetName must match the PNG filename in Resources/VFX/Sprites/ (without extension).
    // row: which color row to play (0 = first row).
    // cols is calculated automatically from the texture width ÷ frame width (64 px).
    public void SpawnVFX(PokemonInstance source, PokemonInstance target, string sheetName, int row = 0)
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

        // Derive column count from the actual texture dimensions (frame width is always 64 px)
        int frameWidth = Mathf.Max(1, (int)all[0].rect.width);
        int cols       = all[0].texture.width / frameWidth;

        // Slice out the requested row
        int start = row * cols;
        int end   = Mathf.Min(start + cols, all.Length);
        if (start >= all.Length)
        {
            Debug.LogWarning($"[VFX] Row {row} out of range for sheet '{sheetName}' ({all.Length} frames, {cols} cols)");
            return;
        }
        Sprite[] frames = all[start..end];

        // Instantiate at the source slot, as a child of the Canvas so it renders in UI space
        var canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        var go     = Instantiate(vfxPrefab, canvas.transform);
        go.transform.position = sourceSlot.transform.position;

        // Travel to the target slot — animation plays during the flight
        float travelTime = Vector3.Distance(sourceSlot.transform.position, targetSlot.transform.position) / 800f;
        travelTime = Mathf.Clamp(travelTime, 0.15f, 0.5f);
        go.transform.DOMove(targetSlot.transform.position, travelTime).SetEase(Ease.InQuad);

        go.GetComponent<VFXPlayer>().Play(frames, vfxFps);
    }

    // -------------------------------------------------------
    // WEATHER BACKGROUND
    // -------------------------------------------------------

    private Coroutine _weatherCoroutine;
    private GameObject _weatherOverlay;

    private void OnWeatherChangedHandler(string weather)
    {
        // Stop any running weather loop
        if (_weatherCoroutine != null)
        {
            StopCoroutine(_weatherCoroutine);
            _weatherCoroutine = null;
        }
        if (_weatherOverlay != null)
        {
            Destroy(_weatherOverlay);
            _weatherOverlay = null;
        }

        AudioManager.Instance?.PlayWeatherSound(weather); // plays looping ambient; stops if weather == ""

        if (string.IsNullOrEmpty(weather)) return;

        // Load the sprite sheet for this weather (e.g. "rain" → Resources/VFX/Sprites/rain.png)
        Sprite[] frames = Resources.LoadAll<Sprite>("VFX/Sprites/" + weather);

        var canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();

        if (frames == null || frames.Length == 0)
        {
            // No sprite sheet — use procedural particle effect instead
            _weatherOverlay = WeatherParticleController.Create(canvas, weather).gameObject;
            return;
        }

        // Create a full-canvas overlay rendered on top of the battle field
        _weatherOverlay = new GameObject("WeatherOverlay_" + weather);
        _weatherOverlay.transform.SetParent(canvas.transform, false);
        _weatherOverlay.transform.SetAsLastSibling(); // render on top

        var rt = _weatherOverlay.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = _weatherOverlay.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.35f); // semi-transparent overlay
        img.raycastTarget = false;                  // don't block button clicks
        img.type = Image.Type.Tiled;
        img.sprite = frames[0];

        _weatherCoroutine = StartCoroutine(LoopWeatherAnimation(img, frames));
    }

    private IEnumerator LoopWeatherAnimation(Image img, Sprite[] frames)
    {
        float interval = 1f / vfxFps;
        int i = 0;
        while (true)
        {
            img.sprite = frames[i];
            i = (i + 1) % frames.Length;
            yield return new WaitForSeconds(interval);
        }
    }
}
