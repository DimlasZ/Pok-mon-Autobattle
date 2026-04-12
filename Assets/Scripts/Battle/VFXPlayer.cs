using UnityEngine;
using UnityEngine.UI;

// Plays a sprite-sheet animation on a UI Image, then destroys itself.
// Usage: instantiate the VFXPlayer prefab, call Play(frames, fps).

[RequireComponent(typeof(Image))]
public class VFXPlayer : MonoBehaviour
{
    private Image    _image;
    private Sprite[] _frames;
    private float    _fps;
    private float    _timer;
    private int      _frame;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.preserveAspect = true;
    }

    public void Play(Sprite[] frames, float fps = 15f)
    {
        _frames = frames;
        _fps    = fps;
        _frame  = 0;
        _timer  = 0f;

        if (_frames == null || _frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        _image.sprite = _frames[0];
    }

    private void Update()
    {
        if (_frames == null) return;

        _timer += Time.deltaTime;
        if (_timer < 1f / _fps) return;

        _timer = 0f;
        _frame++;

        if (_frame >= _frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        _image.sprite = _frames[_frame];
    }
}
