using UnityEngine;
using UnityEngine.EventSystems;

// Attach this to the Release button/area.
// Dragging any owned Pokemon onto it releases (sells) that Pokemon.

public class ReleaseDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (DragDropManager.Instance == null) return;
        DragDropManager.Instance.DropOnRelease();
    }
}
