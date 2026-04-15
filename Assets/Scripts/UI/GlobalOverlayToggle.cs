using UnityEngine;
using UnityEngine.UI;

// Attach to any Button in any scene.
// At runtime, wires the click to toggle the matching GlobalOverlayManager panel.
// Works without a direct scene reference — looks up the singleton at click time.

public class GlobalOverlayToggle : MonoBehaviour
{
    public enum Target { Settings, Pokedex }
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
        if (target == Target.Settings)
            GlobalOverlayManager.Instance?.ToggleSettings();
        else
            GlobalOverlayManager.Instance?.TogglePokedex();
    }
}