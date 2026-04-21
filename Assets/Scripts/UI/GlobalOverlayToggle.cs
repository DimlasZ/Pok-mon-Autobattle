using UnityEngine;
using UnityEngine.UI;

// Attach to any Button in any scene.
// At runtime, wires the click to toggle the matching GlobalOverlayManager panel.
// Works without a direct scene reference — looks up the singleton at click time.

public class GlobalOverlayToggle : MonoBehaviour
{
    public enum Target { Settings, Pokedex, Helper, ReturnToMainMenu, HallOfFame }
    public Target target;

    private void Start()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        switch (target)
        {
            case Target.Settings:         GlobalOverlayManager.Instance?.ToggleSettings(); break;
            case Target.Pokedex:          GlobalOverlayManager.Instance?.TogglePokedex();  break;
            case Target.Helper:           GlobalOverlayManager.Instance?.ToggleHelper();   break;
            case Target.HallOfFame:       GlobalOverlayManager.Instance?.ToggleHallOfFame(); break;
            case Target.ReturnToMainMenu:
                var confirmPanel = Object.FindAnyObjectByType<ConfirmReturnPanel>(FindObjectsInactive.Include);
                if (confirmPanel != null) confirmPanel.Show();
                else GameManager.Instance?.ReturnToMainMenu(); // fallback if panel not in scene
                break;
        }
    }
}