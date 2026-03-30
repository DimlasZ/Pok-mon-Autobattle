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
    public int winsToVictory = 10;
    public int PlayerWins { get; private set; } = 0;
    public int CurrentRound { get; private set; } = 1;

    [Header("Player Health")]
    public int playerMaxHP = 3;
    public int PlayerHP { get; private set; }

    [Header("Scene Names")]
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

    // Called when player clicks START BATTLE
    // Captures the battle team then loads the battle scene.
    // Reverses the array so the rightmost shop slot (index 2) becomes the front fighter (index 0).
    public void StartBattle()
    {
        var row = ShopManager.Instance.BattleRow;
        PlayerBattleTeam = new PokemonInstance[row.Length];
        for (int i = 0; i < row.Length; i++)
            PlayerBattleTeam[i] = row[row.Length - 1 - i];

        CurrentPhase = GamePhase.Battle;
        SceneManager.LoadScene(battleSceneName);
    }

    // Called by BattleSceneManager when the battle finishes
    public void OnBattleComplete(BattleManager.BattleResult result)
    {
        bool playerWon = result == BattleManager.BattleResult.PlayerWin;
        CurrentPhase   = GamePhase.Results;

        if (playerWon)
            PlayerWins++;
        else
            TakeDamage(1);

        Debug.Log($"Round {CurrentRound} — {(playerWon ? $"Victory! ({PlayerWins}/{winsToVictory} wins)" : "Defeat")}");
    }

    // Called when player clicks Continue after the battle
    public void ReturnToShop()
    {
        if (PlayerHP <= 0)               { EnterGameOver(); return; }
        if (PlayerWins >= winsToVictory) { EnterVictory();  return; }

        CurrentRound++;

        // Set up the next round now — ShopManager persists so StartRound() runs immediately.
        // UIManager doesn't exist yet (it lives in the shop scene), so RefreshAll() is skipped.
        // UIManager.Start() calls RefreshAll() once the shop scene finishes loading.
        EnterBuyPhase();
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
