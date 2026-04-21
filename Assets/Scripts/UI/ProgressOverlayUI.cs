using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using DG.Tweening;
using TMPro;

// Fullscreen overlay shown after each battle.
// All sprites are loaded from Resources at runtime — no Inspector assignments needed.
// Call Show() after a battle result; Hide() when returning to shop.

public class ProgressOverlayUI : MonoBehaviour
{
    static readonly Color Locked   = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    static readonly Color Unlocked = Color.white;

    const int   GymCount   = 8;
    const int   EliteCount = 4;
    const float IconSize   = 100f;
    const float HeartSize  = 80f;

    private Image[] _badgeImages = new Image[GymCount];
    private Image[] _eliteImages = new Image[EliteCount];
    private Image[] _heartImages = new Image[0];
    private int     _builtHeartCount;
    private TextMeshProUGUI _resultLabel;
    private Image   _champImage;

    // -------------------------------------------------------

    private void Awake()
    {
        BuildLayout();
        gameObject.SetActive(false);
    }

    public void Show(BattleResult result = BattleResult.Draw)
    {
        if (_resultLabel != null)
        {
            switch (result)
            {
                case BattleResult.PlayerWin:
                    _resultLabel.text  = "Victory";
                    _resultLabel.color = new Color(0.2f, 0.85f, 0.2f);
                    break;
                case BattleResult.PlayerLoss:
                    _resultLabel.text  = "Defeat";
                    _resultLabel.color = new Color(0.9f, 0.2f, 0.2f);
                    break;
                default:
                    _resultLabel.text  = "Draw";
                    _resultLabel.color = new Color(0.9f, 0.75f, 0.1f);
                    break;
            }
        }

        gameObject.SetActive(true);
        Refresh(animate: true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Refresh(bool animate = false)
    {
        if (GameManager.Instance == null) return;

        int wins  = GameManager.Instance.PlayerWins;
        int hp    = GameManager.Instance.PlayerHP;
        int maxHp = GameManager.Instance.playerMaxHP;

        for (int i = 0; i < GymCount; i++)
            UpdateIcon(_badgeImages[i], wins > i, animate, isHeart: false);

        for (int i = 0; i < EliteCount; i++)
            UpdateIcon(_eliteImages[i], wins > GymCount + i, animate, isHeart: false);

        if (_champImage != null)
            UpdateIcon(_champImage, wins > GymCount + EliteCount, animate, isHeart: false);

        if (maxHp != _builtHeartCount) RebuildHearts(maxHp);
        for (int i = 0; i < _heartImages.Length; i++)
            UpdateIcon(_heartImages[i], i < hp, animate, isHeart: true);
    }

    // -------------------------------------------------------
    // Animations
    // -------------------------------------------------------

    private void UpdateIcon(Image img, bool shouldBeUnlocked, bool animate, bool isHeart)
    {
        bool wasUnlocked = img.color == Unlocked;

        if (shouldBeUnlocked && !wasUnlocked)
        {
            img.color = Unlocked;
            if (animate) PlayUnlockAnim(img.transform);
        }
        else if (!shouldBeUnlocked && wasUnlocked && isHeart)
        {
            img.color = Locked;
            if (animate) PlayHeartLostAnim(img.transform);
        }
        else
        {
            img.color = shouldBeUnlocked ? Unlocked : Locked;
        }
    }

    private void PlayUnlockAnim(Transform t)
    {
        t.localScale = Vector3.one;
        t.DOPunchScale(Vector3.one * 0.5f, 0.5f, 6, 0.4f).SetUpdate(true);
    }

    private void PlayHeartLostAnim(Transform t)
    {
        t.DOShakePosition(0.45f, new Vector3(10f, 0f, 0f), 22, 90f, false, true)
         .SetUpdate(true);
    }

    // -------------------------------------------------------
    // Layout — each group is anchored independently
    // -------------------------------------------------------

    private void BuildLayout()
    {
        // Fullscreen dark backdrop
        var bg = gameObject.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.96f);
        bg.raycastTarget = true;

        Sprite[] badges    = Resources.LoadAll<Sprite>("Icons/Badge").OrderBy(s => s.name).ToArray();
        Sprite   starSprite  = Resources.Load<Sprite>("Icons/star");
        Sprite   heartSprite = Resources.Load<Sprite>("Icons/heart");

        if (badges.Length != GymCount)
            Debug.LogWarning($"ProgressOverlay: expected {GymCount} badge sprites in Resources/Icons/Badge, found {badges.Length}.");

        // ── Badges + stars: top-centre, pushed high ──────────────────────
        var topGroup = MakePanel("TopGroup", anchorX: 0.5f, anchorY: 0.75f);
        var topLayout = topGroup.AddComponent<VerticalLayoutGroup>();
        topLayout.childAlignment         = TextAnchor.MiddleCenter;
        topLayout.childControlWidth      = false;
        topLayout.childControlHeight     = false;
        topLayout.childForceExpandWidth  = false;
        topLayout.childForceExpandHeight = false;
        topLayout.spacing = 16f;
        topGroup.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        topGroup.GetComponent<ContentSizeFitter>().verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        Sprite champSprite = Resources.Load<Sprite>("Icons/champ");

        _badgeImages = BuildRow(topGroup.transform, "Badges", GymCount,   badges, IconSize);
        _eliteImages = BuildRow(topGroup.transform, "Elite4", EliteCount, null,   IconSize);
        foreach (var img in _eliteImages) img.sprite = starSprite;

        // ── Champ icon: below the 4 stars ────────────────────────────────
        var champGO = new GameObject("ChampIcon");
        champGO.transform.SetParent(topGroup.transform, false);
        champGO.AddComponent<RectTransform>().sizeDelta = new Vector2(IconSize, IconSize);
        _champImage                = champGO.AddComponent<Image>();
        _champImage.sprite         = champSprite;
        _champImage.preserveAspect = true;
        _champImage.raycastTarget  = false;
        _champImage.color          = Locked;

        // ── Result label: centred between top group and bottom row ───────
        var resultGO = new GameObject("ResultLabel");
        resultGO.transform.SetParent(transform, false);
        var resultRT = resultGO.AddComponent<RectTransform>();
        resultRT.anchorMin        = new Vector2(0.5f, 0.45f);
        resultRT.anchorMax        = new Vector2(0.5f, 0.45f);
        resultRT.pivot            = new Vector2(0.5f, 0.5f);
        resultRT.anchoredPosition = Vector2.zero;
        resultRT.sizeDelta        = new Vector2(600f, 120f);
        _resultLabel              = resultGO.AddComponent<TextMeshProUGUI>();
        _resultLabel.text         = "";
        _resultLabel.fontSize     = 80;
        _resultLabel.fontStyle    = FontStyles.Bold;
        _resultLabel.alignment    = TextAlignmentOptions.Center;
        _resultLabel.color        = Color.white;

        // ── Bottom row: hearts left, continue button right ───────────────
        int maxHp = GameManager.Instance != null ? GameManager.Instance.playerMaxHP : 6;
        BuildBottomRow(heartSprite, maxHp);
    }

    // Creates a pivot-centred panel anchored at the given normalised position.
    private GameObject MakePanel(string name, float anchorX, float anchorY)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(anchorX, anchorY);
        rt.anchorMax        = new Vector2(anchorX, anchorY);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;
        return go;
    }

    private Image[] BuildRow(Transform parent, string rowName, int count, Sprite[] sprites, float size)
    {
        var row = new GameObject(rowName);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>().sizeDelta = new Vector2(count * (size + 12f), size);

        var hLayout = row.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment         = TextAnchor.MiddleCenter;
        hLayout.childControlWidth      = false;
        hLayout.childControlHeight     = false;
        hLayout.childForceExpandWidth  = false;
        hLayout.childForceExpandHeight = false;
        hLayout.spacing = 12f;

        var images = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var icon = new GameObject($"{rowName}_{i}");
            icon.transform.SetParent(row.transform, false);
            icon.AddComponent<RectTransform>().sizeDelta = new Vector2(size, size);

            var img = icon.AddComponent<Image>();
            if (sprites != null && i < sprites.Length) img.sprite = sprites[i];
            img.preserveAspect = true;
            img.raycastTarget  = false;
            img.color          = Locked;
            images[i] = img;
        }
        return images;
    }

    private void BuildBottomRow(Sprite heartSprite, int maxHp)
    {
        // Full-width bar anchored to the bottom of the overlay
        var bar = new GameObject("BottomRow");
        bar.transform.SetParent(transform, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin        = new Vector2(0f, 0f);
        barRT.anchorMax        = new Vector2(1f, 0f);
        barRT.pivot            = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, 30f);
        barRT.sizeDelta        = new Vector2(0f, HeartSize + 20f);

        // Hearts — left side
        var heartsGO = new GameObject("HeartsGroup");
        heartsGO.transform.SetParent(bar.transform, false);
        var heartsRT = heartsGO.AddComponent<RectTransform>();
        heartsRT.anchorMin        = new Vector2(0f, 0.5f);
        heartsRT.anchorMax        = new Vector2(0f, 0.5f);
        heartsRT.pivot            = new Vector2(0f, 0.5f);
        heartsRT.anchoredPosition = new Vector2(30f, 0f);
        heartsRT.sizeDelta        = Vector2.zero;

        var heartsLayout = heartsGO.AddComponent<HorizontalLayoutGroup>();
        heartsLayout.childAlignment         = TextAnchor.MiddleLeft;
        heartsLayout.childControlWidth      = false;
        heartsLayout.childControlHeight     = false;
        heartsLayout.childForceExpandWidth  = false;
        heartsLayout.childForceExpandHeight = false;
        heartsLayout.spacing = 12f;
        var heartsFitter = heartsGO.AddComponent<ContentSizeFitter>();
        heartsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        heartsFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        _heartImages = BuildRow(heartsGO.transform, "Hearts", maxHp, null, HeartSize);
        foreach (var img in _heartImages) img.sprite = heartSprite;
        _builtHeartCount = maxHp;

        // Continue button — right side
        var btnGO = new GameObject("ContinueButton");
        btnGO.transform.SetParent(bar.transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin        = new Vector2(1f, 0.5f);
        btnRT.anchorMax        = new Vector2(1f, 0.5f);
        btnRT.pivot            = new Vector2(1f, 0.5f);
        btnRT.anchoredPosition = new Vector2(-30f, 0f);
        btnRT.sizeDelta        = new Vector2(340, 75);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 0.2f, 1f);

        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayShopMusic();
            GameManager.Instance.ReturnToShop();
        });

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Continue";
        tmp.fontSize  = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }

    private void RebuildHearts(int maxHp)
    {
        var heartsGroup = transform.Find("BottomRow/HeartsGroup");
        if (heartsGroup == null) return;

        var existing = heartsGroup.Find("Hearts");
        if (existing != null) Destroy(existing.gameObject);

        Sprite heartSprite = Resources.Load<Sprite>("Icons/heart");
        _heartImages     = BuildRow(heartsGroup, "Hearts", maxHp, null, HeartSize);
        foreach (var img in _heartImages) img.sprite = heartSprite;
        _builtHeartCount = maxHp;
    }
}
