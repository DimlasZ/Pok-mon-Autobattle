using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Full-screen Game Over overlay. Call Show() from GameManager.EnterGameOver().
// Continue button returns to Main Menu.

public class GameOverOverlayUI : MonoBehaviour
{
    private void Awake()
    {
        BuildLayout();
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        AudioManager.Instance?.PlayMusic("gameover");
        transform.localScale = Vector3.one * 0.85f;
        transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        AudioManager.Instance?.StopMusic();
    }

    void BuildLayout()
    {
        var bg = gameObject.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.985f);
        bg.raycastTarget = true;

        // Title
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(transform, false);
        var titleRT  = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0.5f, 0.5f);
        titleRT.anchorMax        = new Vector2(0.5f, 0.5f);
        titleRT.pivot            = new Vector2(0.5f, 0.5f);
        titleRT.anchoredPosition = new Vector2(0f, 80f);
        titleRT.sizeDelta        = new Vector2(1200f, 140f);
        var titleTMP  = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "Game Over";
        titleTMP.fontSize  = 90;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = new Color(0.85f, 0.1f, 0.1f);

        // Subtitle
        var subGO = new GameObject("Subtitle");
        subGO.transform.SetParent(transform, false);
        var subRT  = subGO.AddComponent<RectTransform>();
        subRT.anchorMin        = new Vector2(0.5f, 0.5f);
        subRT.anchorMax        = new Vector2(0.5f, 0.5f);
        subRT.pivot            = new Vector2(0.5f, 0.5f);
        subRT.anchoredPosition = new Vector2(0f, -20f);
        subRT.sizeDelta        = new Vector2(900f, 60f);
        var subTMP  = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text      = "Better luck next time!";
        subTMP.fontSize  = 40;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color     = new Color(0.75f, 0.75f, 0.75f);

        // Continue button
        var btnGO = new GameObject("ContinueButton");
        btnGO.transform.SetParent(transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin        = new Vector2(0.5f, 0f);
        btnRT.anchorMax        = new Vector2(0.5f, 0f);
        btnRT.pivot            = new Vector2(0.5f, 0f);
        btnRT.anchoredPosition = new Vector2(0f, 60f);
        btnRT.sizeDelta        = new Vector2(380f, 80f);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.55f, 0.12f, 0.12f);

        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(OnContinueClicked);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = "Main Menu";
        labelTMP.fontSize  = 36;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color     = Color.white;
    }

    void OnContinueClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        Hide();
        GameManager.Instance?.ReturnToMainMenu();
    }
}
