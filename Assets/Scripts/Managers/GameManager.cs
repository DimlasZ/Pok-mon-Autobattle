using UnityEngine;
using UnityEngine.SceneManagement;
using System;

// GameManager controls the overall game flow and persists across all scenes.
// It is the single source of truth for: current phase, round, player HP, and battle teams.

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GamePhase { Buy, Battle, Results, GameOver, Victory }
    public GamePhase CurrentPhase { get; private set; }

    [Header("Win Settings")]
    public int winsToVictory = 13;
    public int PlayerWins { get; private set; } = 0;
    public int CurrentRound { get; private set; } = 1;

    [Header("Player Health")]
    public int playerMaxHP = 6;
    public int  PlayerHP            { get; private set; }
    public bool PendingHeartRestored { get; private set; }

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenuScene";
    [Tooltip("Name of the shop/buy phase scene")]
    public string shopSceneName = "ShopScene";
    [Tooltip("Name of the battle scene — swap this later for tier-based arenas")]
    public string battleSceneName = "BattleScene";

    // The player's battle team, captured before switching to the battle scene
    public PokemonInstance[] PlayerBattleTeam { get; private set; }

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.quitting += OnApplicationQuitting;
    }

    private void OnApplicationQuitting()
    {
        // Save mid-run state so the player can Continue next session.
        // Only save during Buy phase — battle state is ephemeral.
        // Skip if on the main menu: ReturnToMainMenu() already saved before clearing rows,
        // and saving here would overwrite that valid save with empty ShopManager data.
        if (CurrentPhase == GamePhase.Buy && PlayerHP > 0
            && SceneManager.GetActiveScene().name != mainMenuSceneName)
            AutoSaveManager.Save();
    }

    private void Start()
    {
        PlayerHP = playerMaxHP;
        // Skip buy phase when starting from the main menu — StartGame() handles that transition.
        if (SceneManager.GetActiveScene().name != mainMenuSceneName)
            EnterBuyPhase();
    }

    // -------------------------------------------------------
    // PHASE TRANSITIONS
    // -------------------------------------------------------

    // Called at the start of each round — opens the shop
    public void EnterBuyPhase()
    {
        CurrentPhase = GamePhase.Buy;
        Debug.Log($"Round {CurrentRound} — Buy Phase started");

        if (ShopManager.Instance != null)
            ShopManager.Instance.StartRound();

        // Save after the shop is populated so Continue restores a valid buy-phase state.
        // Skip on round 1 of a fresh game — nothing worth saving yet.
        if (CurrentRound > 1)
            AutoSaveManager.Save();
    }

    // Returns to the main menu. Saves current state so the player can Continue the run.
    // Only saves during Buy phase — if called from battle/results the run isn't in a clean state.
    public void ReturnToMainMenu()
    {
        if (CurrentPhase == GamePhase.Buy && PlayerHP > 0)
            AutoSaveManager.Save();
        else
            AutoSaveManager.Delete();

        ResetShop();
        GlobalOverlayManager.Instance?.CloseAll();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Called by MainMenuController's Play Now button — starts the game from scratch.
    public void StartGame()
    {
        AutoSaveManager.Delete();
        PlayerHP     = playerMaxHP;
        PlayerWins   = 0;
        CurrentRound = 1;
        ResetShop();
        EnterBuyPhase();
        SceneManager.LoadScene(shopSceneName);
    }

    // Pending save to be applied by UIManager once the shop scene finishes loading.
    public GameSaveData PendingSaveLoad { get; private set; }

    // Called by MainMenuController's Continue button — resumes from the autosave.
    public bool ContinueGame()
    {
        var save = AutoSaveManager.Load();
        if (save == null) return false;

        PlayerHP        = save.playerHP;
        PlayerWins      = save.playerWins;
        CurrentRound    = save.currentRound;
        CurrentPhase    = GamePhase.Buy;
        PendingSaveLoad = save;   // consumed by UIManager.Start() after scene loads

        SceneManager.LoadScene(shopSceneName);
        return true;
    }

    public void ClearPendingSaveLoad() => PendingSaveLoad = null;

    // Wipes all three team rows so the previous run's team doesn't bleed into a new game.
    private void ResetShop()
    {
        if (ShopManager.Instance == null) return;
        ShopManager.Instance.ClearAllRows();
    }

    // Called when player clicks START BATTLE
    // Captures the battle team then loads the battle scene.
    // Reverses the array so the rightmost shop slot (index 2) becomes the front fighter (index 0).
    public void StartBattle()
    {
        var row  = ShopManager.Instance.BattleRow;
        int size = ShopManager.Instance.BattleSize;
        PlayerBattleTeam = new PokemonInstance[size];
        for (int i = 0; i < size; i++)
            PlayerBattleTeam[i] = row[size - 1 - i];

        int tier = ShopManager.Instance.GetTierForRound(CurrentRound);
        PlayerTeamSaver.SaveTeam(CurrentRound, tier, PlayerBattleTeam);

        CurrentPhase = GamePhase.Battle;
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.BattleIntroToScene(battleSceneName);
        else
            SceneManager.LoadScene(battleSceneName);
    }

    // Called by BattleSceneManager when the battle finishes
    public void OnBattleComplete(BattleResult result)
    {
        CurrentPhase = GamePhase.Results;

        if (result == BattleResult.PlayerWin)
        {
            PlayerWins++;
            if (PlayerWins == 8)
                PokedexUnlockManager.UnlockElite4(PlayerBattleTeam);
        }
        else if (result == BattleResult.PlayerLoss)
            TakeDamage(1);

        string resultLabel = result == BattleResult.PlayerWin ? $"Victory! ({PlayerWins}/{winsToVictory} wins)" : result == BattleResult.Draw ? "Draw" : "Defeat";
        Debug.Log($"Round {CurrentRound} — {resultLabel}");

        // In multiplayer the host pushes authoritative state to the client immediately.
        // Heart restoration is pre-calculated here so it can be included in the sync payload.
        // ReturnToShop() on the host will skip the heart logic since PendingHeartRestored is already set.
        var sync = MultiplayerBattleSync.Instance;
        if (sync != null && sync.IsHost)
        {
            int tierBefore = ShopManager.Instance.GetTierForRound(CurrentRound);
            int tierAfter  = ShopManager.Instance.GetTierForRound(CurrentRound + 1);
            PendingHeartRestored = false;
            if (tierAfter > tierBefore && (tierAfter == 2 || tierAfter == 4) && PlayerHP < playerMaxHP)
            {
                PlayerHP++;
                PendingHeartRestored = true;
            }
            sync.SyncPostBattle(result, PlayerHP, PlayerWins, CurrentRound, PendingHeartRestored);
        }

        GlobalOverlayManager.Instance?.progressOverlay?.Show(result);
    }

    // Called on the client by MultiplayerBattleSync after receiving host's authoritative state.
    public void MultiplayerApplyPostBattle(BattleResult result, int hp, int wins, int round, bool heartRestored)
    {
        CurrentPhase         = GamePhase.Results;
        PlayerHP             = hp;
        PlayerWins           = wins;
        CurrentRound         = round;
        PendingHeartRestored = heartRestored;

        Debug.Log($"[MP Client] Round {round} result applied — HP:{hp} Wins:{wins}");
        GlobalOverlayManager.Instance?.progressOverlay?.Show(result);
    }

    // Called when player clicks Continue after the battle
    public void ReturnToShop()
    {
        GlobalOverlayManager.Instance?.progressOverlay?.Hide();

        if (PlayerHP <= 0)               { EnterGameOver(); return; }
        if (PlayerWins >= winsToVictory) { EnterVictory();  return; }

        int tierBefore = ShopManager.Instance.GetTierForRound(CurrentRound);
        CurrentRound++;
        int tierAfter = ShopManager.Instance.GetTierForRound(CurrentRound);

        // In multiplayer the host pre-calculates heart restoration in OnBattleComplete
        // so it can be synced to the client — skip it here to avoid applying it twice.
        bool isMultiplayer = MultiplayerBattleSync.Instance != null;
        if (!isMultiplayer)
        {
            PendingHeartRestored = false;
            if (tierAfter > tierBefore && (tierAfter == 2 || tierAfter == 4) && PlayerHP < playerMaxHP)
            {
                PlayerHP             = Mathf.Min(PlayerHP + 1, playerMaxHP);
                PendingHeartRestored = true;
                Debug.Log($"Tier {tierAfter} reached — restored 1 HP. HP: {PlayerHP}/{playerMaxHP}");
            }
        }

        // Heal all player Pokémon back to full so bench shows correct HP in the shop.
        if (PlayerBattleTeam != null)
            foreach (var p in PlayerBattleTeam)
                if (p != null) p.ResetForBattle();

        // Set up the next round now — ShopManager persists so StartRound() runs immediately.
        // UIManager doesn't exist yet (it lives in the shop scene), so RefreshAll() is skipped.
        // UIManager.Start() calls RefreshAll() once the shop scene finishes loading.
        EnterBuyPhase();
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.FadeToScene(shopSceneName);
        else
            SceneManager.LoadScene(shopSceneName);
    }

    // -------------------------------------------------------
    // PLAYER HP
    // -------------------------------------------------------

    public void TakeDamage(int amount)
    {
        PlayerHP -= amount;
        PlayerHP  = Mathf.Max(PlayerHP, 0);
        Debug.Log($"Player took {amount} damage — HP: {PlayerHP}/{playerMaxHP}");
    }

    // -------------------------------------------------------
    // GAME OVER / VICTORY
    // -------------------------------------------------------

    private void EnterGameOver()
    {
        CurrentPhase = GamePhase.GameOver;
        AutoSaveManager.Delete();
        Debug.Log("Game Over!");
        GlobalOverlayManager.Instance?.gameOverOverlay?.Show();
    }

    private void EnterVictory()
    {
        CurrentPhase = GamePhase.Victory;
        Debug.Log("You are the Pokémon Champion!");

        AutoSaveManager.Delete();
        HallOfFameManager.SaveEntry(PlayerBattleTeam);
        PokedexUnlockManager.UnlockChamp(PlayerBattleTeam);
        GlobalOverlayManager.Instance?.victoryOverlay?.Show(PlayerBattleTeam);
    }
}
