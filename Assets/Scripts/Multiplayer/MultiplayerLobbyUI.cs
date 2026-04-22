using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Drives the multiplayer lobby panel on the main menu.
// States: Idle → Hosting (waiting for opponent) | Joining (entering code)

public class MultiplayerLobbyUI : MonoBehaviour
{
    [Header("Root panels")]
    public GameObject idlePanel;    // Host / Join buttons
    public GameObject hostPanel;    // Shows generated room code + waiting message
    public GameObject joinPanel;    // Code input + confirm button

    [Header("Idle panel")]
    public Button hostButton;
    public Button joinButton;

    [Header("Host panel")]
    public TextMeshProUGUI roomCodeLabel;   // "Room code: XK47"
    public TextMeshProUGUI hostStatusLabel; // "Waiting for opponent..."
    public Button          hostCancelButton;

    [Header("Join panel")]
    public TMP_InputField codeInputField;
    public Button         joinConfirmButton;
    public TextMeshProUGUI joinStatusLabel; // "Connecting..." / error messages
    public Button          joinCancelButton;

    [Header("Shared")]
    public Button backButton; // returns to idle from any sub-panel

    // -------------------------------------------------------

    private void Awake()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        hostCancelButton.onClick.AddListener(OnCancel);
        joinCancelButton.onClick.AddListener(OnCancel);
        backButton.onClick.AddListener(OnBackClicked);
        joinConfirmButton.onClick.AddListener(OnJoinConfirmClicked);
    }

    private void OnEnable()
    {
        ShowIdle();

        var mp = MultiplayerNetworkManager.Instance;
        if (mp == null) return;
        mp.OnRoomCodeGenerated  += OnRoomCodeGenerated;
        mp.OnOpponentConnected  += OnOpponentConnected;
        mp.OnError              += OnError;
    }

    private void OnDisable()
    {
        var mp = MultiplayerNetworkManager.Instance;
        if (mp == null) return;
        mp.OnRoomCodeGenerated  -= OnRoomCodeGenerated;
        mp.OnOpponentConnected  -= OnOpponentConnected;
        mp.OnError              -= OnError;
    }

    // ── Button handlers ────────────────────────────────────────────────────

    private async void OnHostClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        hostButton.interactable = false;
        joinButton.interactable = false;

        try
        {
            await MultiplayerNetworkManager.Instance.HostGame();
            // OnRoomCodeGenerated fires → switches to host panel
        }
        catch
        {
            hostButton.interactable = true;
            joinButton.interactable = true;
        }
    }

    private void OnJoinClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        ShowJoin();
    }

    private async void OnJoinConfirmClicked()
    {
        string code = codeInputField.text.Trim().ToUpper();
        if (code.Length != 4)
        {
            SetJoinStatus("Enter a 4-letter code.", Color.red);
            return;
        }

        AudioManager.Instance?.PlayButtonSound();
        joinConfirmButton.interactable = false;
        SetJoinStatus("Connecting...", Color.yellow);

        try
        {
            await MultiplayerNetworkManager.Instance.JoinGame(code);
            SetJoinStatus("Connected! Waiting for host...", Color.green);
        }
        catch
        {
            joinConfirmButton.interactable = true;
            // Error message shown via OnError callback
        }
    }

    private void OnCancel()
    {
        AudioManager.Instance?.PlayButtonSound();
        MultiplayerNetworkManager.Instance?.Disconnect();
        ShowIdle();
    }

    private void OnBackClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        MultiplayerNetworkManager.Instance?.Disconnect();
        gameObject.SetActive(false);
    }

    // ── Network callbacks ──────────────────────────────────────────────────

    private void OnRoomCodeGenerated(string code)
    {
        roomCodeLabel.text  = $"Room Code\n<size=72><b>{code}</b></size>";
        hostStatusLabel.text = "Waiting for opponent...";
        hostStatusLabel.color = Color.yellow;
        ShowHost();
    }

    private void OnOpponentConnected()
    {
        hostStatusLabel.text  = "Opponent connected!";
        hostStatusLabel.color = Color.green;
        // Battle starts automatically once both players press Start Battle in the shop.
        // Transition to game after a short delay so the player can see the message.
        StartCoroutine(StartGameDelayed());
    }

    private IEnumerator StartGameDelayed()
    {
        yield return new WaitForSeconds(1.2f);
        gameObject.SetActive(false);
        GlobalOverlayManager.Instance?.CloseAll();
        GameManager.Instance?.StartGame();
    }

    private void OnError(string message)
    {
        SetJoinStatus(message, Color.red);
        hostStatusLabel.text  = message;
        hostStatusLabel.color = Color.red;
        joinConfirmButton.interactable = true;
    }

    // ── State helpers ──────────────────────────────────────────────────────

    private void ShowIdle()
    {
        idlePanel.SetActive(true);
        hostPanel.SetActive(false);
        joinPanel.SetActive(false);
        hostButton.interactable = true;
        joinButton.interactable = true;
    }

    private void ShowHost()
    {
        idlePanel.SetActive(false);
        hostPanel.SetActive(true);
        joinPanel.SetActive(false);
    }

    private void ShowJoin()
    {
        idlePanel.SetActive(false);
        hostPanel.SetActive(false);
        joinPanel.SetActive(true);
        codeInputField.text = "";
        SetJoinStatus("", Color.white);
    }

    private void SetJoinStatus(string msg, Color color)
    {
        joinStatusLabel.text  = msg;
        joinStatusLabel.color = color;
    }
}
