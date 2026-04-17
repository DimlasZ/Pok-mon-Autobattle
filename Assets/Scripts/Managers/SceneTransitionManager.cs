using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

// SceneTransitionManager handles all scene transitions with visual effects.
// Persists across scenes and owns its own fullscreen overlay canvas.
//
// FadeToScene        — simple fade to black and back (battle → shop)
// BattleIntroToScene — randomly picks one of four wipe styles (shop → battle)
//                      and starts the battle music immediately on transition start.

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Bar Transition Settings")]
    [Tooltip("Number of bars for bar-based transitions")]
    [SerializeField] private int   barCount     = 8;
    [Tooltip("Time for bars/panels to fully close (seconds)")]
    [SerializeField] private float closeTime    = 0.42f;
    [Tooltip("Time for bars/panels to open after scene loads (seconds)")]
    [SerializeField] private float openTime     = 0.36f;
    [Tooltip("Stagger delay between each bar (seconds)")]
    [SerializeField] private float barStagger   = 0.03f;

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutTime  = 0.48f;
    [SerializeField] private float fadeInTime   = 0.48f;

    private enum BattleTransition { HorizontalBars, VerticalBars, DiagonalSweep, CornerPanels }

    private Canvas           _canvas;
    private RectTransform    _canvasRT;
    private Image            _fadeOverlay;
    private RectTransform[]  _bars;           // shared pool for bar transitions
    private RectTransform[]  _cornerPanels;   // 4 corner panels
    private RectTransform    _diagonalPanel;  // single diagonal wipe panel

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        _canvasRT = _canvas.GetComponent<RectTransform>();

        // ── Fade overlay ──────────────────────────────────────────────────
        _fadeOverlay = MakeFullscreenPanel("FadeOverlay", new Color(0, 0, 0, 0));
        _fadeOverlay.raycastTarget = false;

        // ── Bars (shared for horizontal & vertical) ───────────────────────
        _bars = new RectTransform[barCount];
        for (int i = 0; i < barCount; i++)
        {
            var barGO           = new GameObject($"Bar_{i}");
            barGO.transform.SetParent(_canvas.transform, false);
            var rt              = barGO.AddComponent<RectTransform>();
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.zero;
            rt.pivot            = Vector2.zero;
            var img             = barGO.AddComponent<Image>();
            img.color           = Color.black;
            img.raycastTarget   = false;
            barGO.SetActive(false);
            _bars[i] = rt;
        }

        // ── Corner panels (4 squares) ─────────────────────────────────────
        _cornerPanels = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            var go          = new GameObject($"Corner_{i}");
            go.transform.SetParent(_canvas.transform, false);
            var rt          = go.AddComponent<RectTransform>();
            rt.anchorMin    = Vector2.zero;
            rt.anchorMax    = Vector2.zero;
            rt.pivot        = new Vector2(i < 2 ? 0f : 1f, i % 2 == 0 ? 0f : 1f);
            var img         = go.AddComponent<Image>();
            img.color       = Color.black;
            img.raycastTarget = false;
            go.SetActive(false);
            _cornerPanels[i] = rt;
        }

        // ── Diagonal sweep panel ──────────────────────────────────────────
        var diagGO          = new GameObject("DiagonalPanel");
        diagGO.transform.SetParent(_canvas.transform, false);
        _diagonalPanel      = diagGO.AddComponent<RectTransform>();
        _diagonalPanel.anchorMin = Vector2.zero;
        _diagonalPanel.anchorMax = Vector2.zero;
        _diagonalPanel.pivot     = new Vector2(0.5f, 0.5f);
        var diagImg         = diagGO.AddComponent<Image>();
        diagImg.color       = Color.black;
        diagImg.raycastTarget = false;
        diagGO.SetActive(false);
    }

    // -------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------

    public void FadeToScene(string sceneName)
        => StartCoroutine(FadeRoutine(sceneName));

    public void BattleIntroToScene(string sceneName)
    {
        // Start battle music immediately — before the scene loads
        AudioManager.Instance?.PlayRandomMusic("Trainerbattle");

        var style = (BattleTransition)Random.Range(0, 4);
        StartCoroutine(BattleIntroRoutine(sceneName, style));
    }

    // -------------------------------------------------------
    // FADE
    // -------------------------------------------------------

    private IEnumerator FadeRoutine(string sceneName)
    {
        yield return FadeOverlay(0f, 1f, fadeOutTime);

        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        yield return FadeOverlay(1f, 0f, fadeInTime);
    }

    // -------------------------------------------------------
    // BATTLE INTRO DISPATCHER
    // -------------------------------------------------------

    private IEnumerator BattleIntroRoutine(string sceneName, BattleTransition style)
    {
        switch (style)
        {
            case BattleTransition.HorizontalBars: yield return HorizontalBarsRoutine(sceneName); break;
            case BattleTransition.VerticalBars:   yield return VerticalBarsRoutine(sceneName);   break;
            case BattleTransition.DiagonalSweep:  yield return DiagonalSweepRoutine(sceneName);  break;
            case BattleTransition.CornerPanels:   yield return CornerPanelsRoutine(sceneName);   break;
        }
    }

    // -------------------------------------------------------
    // TRANSITION: HORIZONTAL BARS (left/right alternating)
    // -------------------------------------------------------

    private IEnumerator HorizontalBarsRoutine(string sceneName)
    {
        float canvasW = _canvasRT.rect.width;
        float canvasH = _canvasRT.rect.height;
        float barH    = canvasH / barCount;

        for (int i = 0; i < barCount; i++)
        {
            var rt = _bars[i];
            rt.sizeDelta        = new Vector2(canvasW, barH);
            bool fromRight      = (i % 2 == 0);
            rt.anchoredPosition = new Vector2(fromRight ? canvasW : -canvasW, i * barH);
            rt.gameObject.SetActive(true);
        }

        for (int i = 0; i < barCount; i++)
        {
            int   idx     = i;
            float bottomY = i * barH;
            _bars[idx].DOAnchorPos(new Vector2(0f, bottomY), closeTime)
                      .SetDelay(i * barStagger).SetEase(Ease.InCubic);
        }

        yield return new WaitForSecondsRealtime(closeTime + barCount * barStagger + 0.05f);
        yield return LoadAndOpen(sceneName, () => OpenHorizontalBars(canvasW, barH));
    }

    private void OpenHorizontalBars(float canvasW, float barH)
    {
        for (int i = 0; i < barCount; i++)
        {
            int   idx     = i;
            float bottomY = i * barH;
            bool  toRight = (i % 2 == 0);
            _bars[idx].DOAnchorPos(new Vector2(toRight ? canvasW : -canvasW, bottomY), openTime)
                      .SetDelay(i * barStagger).SetEase(Ease.OutCubic)
                      .OnComplete(() => _bars[idx].gameObject.SetActive(false));
        }
    }

    // -------------------------------------------------------
    // TRANSITION: VERTICAL BARS (top/bottom alternating)
    // -------------------------------------------------------

    private IEnumerator VerticalBarsRoutine(string sceneName)
    {
        float canvasW = _canvasRT.rect.width;
        float canvasH = _canvasRT.rect.height;
        float barW    = canvasW / barCount;

        for (int i = 0; i < barCount; i++)
        {
            var rt = _bars[i];
            rt.sizeDelta        = new Vector2(barW, canvasH);
            bool fromTop        = (i % 2 == 0);
            rt.anchoredPosition = new Vector2(i * barW, fromTop ? canvasH : -canvasH);
            rt.gameObject.SetActive(true);
        }

        for (int i = 0; i < barCount; i++)
        {
            int   idx   = i;
            float leftX = i * barW;
            _bars[idx].DOAnchorPos(new Vector2(leftX, 0f), closeTime)
                      .SetDelay(i * barStagger).SetEase(Ease.InCubic);
        }

        yield return new WaitForSecondsRealtime(closeTime + barCount * barStagger + 0.05f);
        yield return LoadAndOpen(sceneName, () => OpenVerticalBars(canvasW, canvasH, barW));
    }

    private void OpenVerticalBars(float canvasW, float canvasH, float barW)
    {
        for (int i = 0; i < barCount; i++)
        {
            int   idx   = i;
            float leftX = i * barW;
            bool  toTop = (i % 2 == 0);
            _bars[idx].DOAnchorPos(new Vector2(leftX, toTop ? canvasH : -canvasH), openTime)
                      .SetDelay(i * barStagger).SetEase(Ease.OutCubic)
                      .OnComplete(() => _bars[idx].gameObject.SetActive(false));
        }
    }

    // -------------------------------------------------------
    // TRANSITION: DIAGONAL SWEEP
    // -------------------------------------------------------

    private IEnumerator DiagonalSweepRoutine(string sceneName)
    {
        float canvasW = _canvasRT.rect.width;
        float canvasH = _canvasRT.rect.height;

        // Panel is large enough to cover the canvas when rotated 45°
        float diagonal  = Mathf.Sqrt(canvasW * canvasW + canvasH * canvasH);
        float panelSize = diagonal * 1.5f;

        _diagonalPanel.sizeDelta        = new Vector2(panelSize, panelSize);
        _diagonalPanel.localEulerAngles = new Vector3(0f, 0f, 45f);

        // Start fully to the left, slide to cover center, then continue to the right
        Vector2 offLeft   = new Vector2(-panelSize,       canvasH * 0.5f);
        Vector2 center    = new Vector2( canvasW  * 0.5f, canvasH * 0.5f);
        Vector2 offRight  = new Vector2( canvasW + panelSize, canvasH * 0.5f);

        _diagonalPanel.anchoredPosition = offLeft;
        _diagonalPanel.gameObject.SetActive(true);

        // Sweep in to center
        yield return _diagonalPanel.DOAnchorPos(center, closeTime)
                                   .SetEase(Ease.InCubic)
                                   .WaitForCompletion();

        // Load scene
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        // Sweep out to the right
        yield return _diagonalPanel.DOAnchorPos(offRight, openTime)
                                   .SetEase(Ease.OutCubic)
                                   .WaitForCompletion();

        _diagonalPanel.gameObject.SetActive(false);
    }

    // -------------------------------------------------------
    // TRANSITION: CORNER PANELS
    // -------------------------------------------------------

    private IEnumerator CornerPanelsRoutine(string sceneName)
    {
        float canvasW = _canvasRT.rect.width;
        float canvasH = _canvasRT.rect.height;
        // Each panel covers half the screen + a small overlap so no gap in center
        float halfW   = canvasW * 0.5f + 4f;
        float halfH   = canvasH * 0.5f + 4f;

        // Corner anchored positions when covering the screen (all meet at center)
        // Pivot corners: 0=bottom-left, 1=top-left, 2=bottom-right, 3=top-right
        Vector2[] onScreen = {
            new Vector2(0f,      0f),       // bottom-left corner at origin
            new Vector2(0f,      canvasH),  // top-left
            new Vector2(canvasW, 0f),       // bottom-right
            new Vector2(canvasW, canvasH),  // top-right
        };
        // Off-screen: panels slide out diagonally
        Vector2[] offScreen = {
            new Vector2(-halfW, -halfH),
            new Vector2(-halfW,  canvasH + halfH),
            new Vector2( canvasW + halfW, -halfH),
            new Vector2( canvasW + halfW,  canvasH + halfH),
        };

        for (int i = 0; i < 4; i++)
        {
            _cornerPanels[i].sizeDelta        = new Vector2(halfW, halfH);
            _cornerPanels[i].anchoredPosition = offScreen[i];
            _cornerPanels[i].gameObject.SetActive(true);
        }

        // Slide all four in simultaneously
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            _cornerPanels[idx].DOAnchorPos(onScreen[idx], closeTime).SetEase(Ease.InCubic);
        }

        yield return new WaitForSecondsRealtime(closeTime + 0.05f);

        // Load scene
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        // Slide all four back out
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            _cornerPanels[idx].DOAnchorPos(offScreen[idx], openTime)
                              .SetEase(Ease.OutCubic)
                              .OnComplete(() => _cornerPanels[idx].gameObject.SetActive(false));
        }

        yield return new WaitForSecondsRealtime(openTime + 0.05f);
    }

    // -------------------------------------------------------
    // SHARED: load scene then trigger the open animation
    // -------------------------------------------------------

    private IEnumerator LoadAndOpen(string sceneName, System.Action openAction)
    {
        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        openAction();
        yield return new WaitForSecondsRealtime(openTime + barCount * barStagger + 0.05f);
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    private Image MakeFullscreenPanel(string name, Color color)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(_canvas.transform, false);
        var rt   = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img  = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        _fadeOverlay.color = new Color(0f, 0f, 0f, to);
    }
}
