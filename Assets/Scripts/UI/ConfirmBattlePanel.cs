using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Warns the player when they try to start a battle with unspent Pokédollars.
public class ConfirmBattlePanel : MonoBehaviour
{
    public Button confirmButton;
    public Button cancelButton;
    public TextMeshProUGUI messageText;

    private void Awake()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
    }

    public void Show(int remainingMoney)
    {
        if (messageText != null)
            messageText.text = $"You still have {remainingMoney} Pokéball{(remainingMoney > 1 ? "s" : "")} left!\nStart battle anyway?";
        gameObject.SetActive(true);
    }

    void OnConfirm()
    {
        AudioManager.Instance?.PlayButtonSound();
        gameObject.SetActive(false);

        var sync = MultiplayerBattleSync.Instance;
        if (sync != null)
        {
            UIManager.Instance?.DisableStartBattleButton();
            UIManager.Instance?.SetMultiplayerStatus("Waiting for opponent...", UnityEngine.Color.yellow);
            sync.NotifyReady(UIManager.Instance?.GetBattleTeam());
        }
        else
        {
            GameManager.Instance?.StartBattle();
        }
    }

    void OnCancel()
    {
        AudioManager.Instance?.PlayButtonSound();
        gameObject.SetActive(false);
    }
}
