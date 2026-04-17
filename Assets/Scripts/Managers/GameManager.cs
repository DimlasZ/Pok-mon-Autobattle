using UnityEngine;
using UnityEngine.SceneManagement;

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
    public int PlayerHP { get; private set; }

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenuScene";
    [Tooltip("Name of the shop/buy phase scene")]
    public string shopSceneName = "SampleScene";
    [Tooltip("Name of the battle scene — swap this later for tier-based arenas")]
    public string battleSceneName = "BattleScene";

    // The player's battle team, captured before switching to the battle scene
    public PokemonInstance[] PlayerBattleTeam { get; private set; }

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persists across all scenes
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

        // ShopManager persists, so this works even when called before scene loads
        if (ShopManager.Instance != null)
            ShopManager.Instance.StartRound();
    }

    // Called by MainMenuController's Play Now button — starts the game from scratch.
    public void StartGame()
    {
        PlayerHP   = playerMaxHP;
        PlayerWins = 0;
        CurrentRound = 1;
        EnterBuyPhase();
        SceneManager.LoadScene(shopSceneName);
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
            PlayerWins++;
        else if (result == BattleResult.PlayerLoss)
            TakeDamage(1);

        string resultLabel = result == BattleResult.PlayerWin ? $"Victory! ({PlayerWins}/{winsToVictory} wins)" : result == BattleResult.Draw ? "Draw" : "Defeat";
        Debug.Log($"Round {CurrentRound} — {resultLabel}");

        GlobalOverlayManager.Instance?.progressOverlay?.Show(result);
    }

    // Called when player clicks Continue after the battle
    public void ReturnToShop()
    {
        GlobalOverlayManager.Instance?.progressOverlay?.Hide();

        if (PlayerHP <= 0)               { EnterGameOver(); return; }
        if (PlayerWins >= winsToVictory) { EnterVictory();  return; }

        CurrentRound++;

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
        Debug.Log("Game Over!");
        // TODO: Load Game Over screen
    }

    private void EnterVictory()
    {
        CurrentPhase = GamePhase.Victory;
        Debug.Log("You are the Pokémon Champion!");
        // TODO: Load Victory screen
    }
}
