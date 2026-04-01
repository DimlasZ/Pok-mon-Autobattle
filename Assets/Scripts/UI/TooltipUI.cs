using UnityEngine;
using UnityEngine.UI;
using TMPro;

// TooltipUI shows a floating panel with ability info when hovering over a Pokemon slot.
// Attach this to a UI panel GameObject in your Canvas. Assign the text fields in the Inspector.

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [Header("References")]
    public TextMeshProUGUI abilityNameText;
    public TextMeshProUGUI abilityDescText;

    private RectTransform _rect;
    private Canvas        _canvas;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _rect   = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        gameObject.SetActive(false);
    }

    // -------------------------------------------------------

    public void Show(AbilityData ability, Vector2 screenPosition)
    {
        if (ability == null) { Hide(); return; }

        abilityNameText.text = ability.abilityName;
        abilityDescText.text = ability.description;

        gameObject.SetActive(true);
        RepositionTooltip(screenPosition);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // Keeps the tooltip on screen — flips it left or up if it would go off edge
    private void RepositionTooltip(Vector2 screenPos)
    {
        // Convert screen position to canvas local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 localPos
        );

        // Offset upward so the tooltip appears above the slot
        Vector2 offset = new Vector2(-_rect.sizeDelta.x / 2f, 10f);
        _rect.localPosition = localPos + offset;

        // Clamp so it stays inside the canvas
        Vector2 canvasSize = (_canvas.transform as RectTransform).sizeDelta;
        Vector2 tooltipSize = _rect.sizeDelta;

        float clampedX = Mathf.Clamp(_rect.localPosition.x, -canvasSize.x / 2f, canvasSize.x / 2f - tooltipSize.x);
        float clampedY = Mathf.Clamp(_rect.localPosition.y, -canvasSize.y / 2f + tooltipSize.y, canvasSize.y / 2f);

        _rect.localPosition = new Vector3(clampedX, clampedY, 0f);
    }
}
