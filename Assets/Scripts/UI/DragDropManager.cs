using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// DragDropManager handles all drag-and-drop interactions in the shop scene.
// Both drag-and-drop and click-to-place go through ShopManager.PlaceSelected().
//
// Flow:
//   1. BeginDrag / SelectSlot  → sets _dragSource, shows ghost, highlights valid targets
//   2. Drop / ClickTarget      → calls ShopManager.PlaceSelected(target)
//   3. CancelDrag              → clears state, removes highlights

public class DragDropManager : MonoBehaviour
{
    public static DragDropManager Instance { get; private set; }

    [Header("Ghost Image")]
    public Image ghostImage;

    private PokemonSlotUI _dragSource;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // -------------------------------------------------------
    // DRAG LIFECYCLE  (called by PokemonSlotUI)
    // -------------------------------------------------------

    public void BeginDrag(PokemonSlotUI source, Sprite sprite)
    {
        _dragSource = source;
        ghostImage.sprite = sprite;
        ghostImage.gameObject.SetActive(true);

        // Register selection so release button and highlights react immediately
        SelectSource(source);
        UIManager.Instance.HighlightValidTargets(source.source);
        UIManager.Instance.RefreshActionButtons();
    }

    public void UpdatePosition(Vector2 screenPosition)
    {
        ghostImage.transform.position = screenPosition;
    }

    // Called when drag ends without landing on a valid target
    public void CancelDrag()
    {
        _dragSource = null;
        ghostImage.gameObject.SetActive(false);
        UIManager.Instance.ClearTargetHighlights();
    }

    // Called by the TARGET slot's OnDrop
    public void Drop(PokemonSlotUI target)
    {
        bool success = false;

        if (_dragSource != null && _dragSource != target)
        {
            // Make sure the drag source is selected in ShopManager
            SelectSource(_dragSource);
            success = ShopManager.Instance.PlaceSelected(target.source, target.slotIndex);
        }

        _dragSource = null;
        ghostImage.gameObject.SetActive(false);
        UIManager.Instance.ClearTargetHighlights();
        UIManager.Instance.RefreshAll();
    }

    // Called by the Release drop zone
    public void DropOnRelease()
    {
        if (_dragSource != null)
        {
            SelectSource(_dragSource);
            ShopManager.Instance.ReleaseSelected();
        }

        _dragSource = null;
        ghostImage.gameObject.SetActive(false);
        UIManager.Instance.ClearTargetHighlights();
        UIManager.Instance.RefreshAll();
    }

    // -------------------------------------------------------
    // HELPER — makes ShopManager aware of which slot is being dragged
    // -------------------------------------------------------

    private void SelectSource(PokemonSlotUI slot)
    {
        switch (slot.source)
        {
            case ShopManager.SelectionSource.Shop:
                ShopManager.Instance.SelectShopPokemon(slot.slotIndex);
                break;
            case ShopManager.SelectionSource.Bench:
                ShopManager.Instance.SelectBenchPokemon(slot.slotIndex);
                break;
            case ShopManager.SelectionSource.Battle:
                ShopManager.Instance.SelectBattlePokemon(slot.slotIndex);
                break;
        }
    }
}
