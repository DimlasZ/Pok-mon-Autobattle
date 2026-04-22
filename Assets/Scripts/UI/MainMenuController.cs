using UnityEngine;
using UnityEngine.UI;

// Drives the Main Menu — Play button callback only.
// Settings and Pokédex buttons are handled by GlobalOverlayToggle components
// on their respective GameObjects; overlays live on the persistent GlobalOverlayManager canvas.

public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button playButton;
    public Button continueButton;
    public Button multiplayerButton;
    public Button quitButton;

    // -------------------------------------------------------

    private void Start()
    {
        if (playButton     != null) playButton.onClick.AddListener(OnPlayClicked);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (quitButton     != null) quitButton.onClick.AddListener(OnQuitClicked);

        // Show Continue only when a valid save exists
        if (continueButton != null)
            continueButton.gameObject.SetActive(AutoSaveManager.SaveExists());

        AudioManager.Instance?.PlayMusic("mainmenu");
    }

    // -------------------------------------------------------

    public void OnPlayClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        GlobalOverlayManager.Instance?.CloseAll();   // hide any open overlays before scene switch
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
        else
            Debug.LogError("MainMenuController: GameManager.Instance is null — make sure GameManager is in the scene.");
    }

    public void OnContinueClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        GlobalOverlayManager.Instance?.CloseAll();
        if (GameManager.Instance != null)
            GameManager.Instance.ContinueGame();
    }

    public void OnQuitClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}