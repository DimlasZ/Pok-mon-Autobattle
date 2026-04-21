using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Simple yes/no confirmation before returning to the main menu.
// Attach to the ConfirmReturnPanel root GameObject.
public class ConfirmReturnPanel : MonoBehaviour
{
    public Button confirmButton;
    public Button cancelButton;

    private void Awake()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
        // Initial hidden state is set by the generator — do NOT call SetActive(false) here.
        // Awake fires the first time the object becomes active, so calling it here would
        // immediately hide the panel on the very first Show() call.
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    void OnConfirm()
    {
        AudioManager.Instance?.PlayButtonSound();
        GameManager.Instance?.ReturnToMainMenu();
    }

    void OnCancel()
    {
        AudioManager.Instance?.PlayButtonSound();
        gameObject.SetActive(false);
    }
}
