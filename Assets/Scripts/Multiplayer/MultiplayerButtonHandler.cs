using UnityEngine;

// Wires the Multiplayer button to the lobby panel at runtime.
// Attached to the MultiplayerButton by the scene generator.
public class MultiplayerButtonHandler : MonoBehaviour
{
    public GameObject lobbyPanel;

    private void Start()
    {
        var btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        // lobbyPanel may have been destroyed if GlobalOverlayManager destroyed the
        // duplicate canvas on scene reload — resolve it fresh via the singleton.
        if (lobbyPanel == null && GlobalOverlayManager.Instance != null)
            lobbyPanel = GlobalOverlayManager.Instance.multiplayerLobbyPanel;

        lobbyPanel?.SetActive(true);
    }
}
