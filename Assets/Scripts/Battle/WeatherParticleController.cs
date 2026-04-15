using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural UI-based particle effects for weather types that have no sprite sheet.
/// Lives inside the game's existing Canvas so it works with any render pipeline.
/// </summary>
public class WeatherParticleController : MonoBehaviour
{
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private readonly List<Coroutine> _spawnRoutines = new();

    // -------------------------------------------------------
    // Public factory — call from BattleSceneManager
    // -------------------------------------------------------

    /// <summary>Creates and starts a weather particle effect as a child of the given canvas.</summary>
    public static WeatherParticleController Create(Canvas canvas, string weather)
    {
        var go = new GameObject("WeatherParticles_" + weather);
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling(); // render on top

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var ctrl = go.AddComponent<WeatherParticleController>();
        ctrl._canvas = canvas;
        ctrl._canvasRect = canvas.GetComponent<RectTransform>();
        ctrl.StartEffects(weather);
        return ctrl;
    }

    // -------------------------------------------------------
    // Effect dispatch
    // -------------------------------------------------------

    private void StartEffects(string weather)
    {
        switch (weather)
        {
            case "rain":
                AddTintOverlay(new Color(0.20f, 0.35f, 0.60f, 0.08f)); // faint blue
                _spawnRoutines.Add(StartCoroutine(SpawnRain()));
                break;

            case "sandstorm":
                AddTintOverlay(new Color(0.72f, 0.52f, 0.15f, 0.08f)); // faint ochre
                _spawnRoutines.Add(StartCoroutine(SpawnSandstorm()));
                break;

            case "sun":
                AddTintOverlay(new Color(1f, 0.85f, 0.2f, 0.06f)); // faint golden
                _spawnRoutines.Add(StartCoroutine(SpawnSunSparkles()));
                break;
        }
    }

    // -------------------------------------------------------
    // Shared tint overlay (replaces absent sprite-sheet tint)
    // -------------------------------------------------------

    private void AddTintOverlay(Color color)
    {
        var go = new GameObject("Tint");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    // -------------------------------------------------------
    // RAIN
    // -------------------------------------------------------

    private IEnumerator SpawnRain()
    {
        while (true)
        {
            int batch = Random.Range(2, 4);
            for (int i = 0; i < batch; i++)
                SpawnRainDrop();

            yield return new WaitForSeconds(0.06f);
        }
    }

    private void SpawnRainDrop()
    {
        var p = CreateParticle();

        // Thin streaks — tall and narrow
        float width  = Random.Range(1f, 2.5f);
        float height = Random.Range(12f, 28f);
        p.sizeDelta = new Vector2(width, height);

        // Angle the streak from top-right to bottom-left
        p.localRotation = Quaternion.Euler(0f, 0f, -15f);

        // Spawn across the full top edge (and a bit beyond each side)
        Rect r = _canvasRect.rect;
        float startX = Random.Range(r.xMin - 30f, r.xMax + 30f);
        float startY = r.yMax + 20f;
        p.anchoredPosition = new Vector2(startX, startY);

        var img = p.GetComponent<Image>();
        float brightness = Random.Range(0.70f, 0.95f);
        img.color = new Color(brightness, brightness + 0.05f, 1f, Random.Range(0.45f, 0.75f));

        // Fast downward fall with rightward-to-leftward drift
        float speed = Random.Range(500f, 800f);
        float angleDeg = Random.Range(-110f, -100f); // steep downward, leftward lean
        float rad = angleDeg * Mathf.Deg2Rad;
        var velocity = new Vector2(Mathf.Cos(rad) * speed, Mathf.Sin(rad) * speed);

        StartCoroutine(AnimateRainDrop(p, velocity));
    }

    private IEnumerator AnimateRainDrop(RectTransform p, Vector2 velocity)
    {
        if (p == null) yield break;
        var img = p.GetComponent<Image>();
        Rect bounds = _canvasRect.rect;

        while (p != null)
        {
            p.anchoredPosition += velocity * Time.deltaTime;

            // Destroy once past the bottom or left edge
            if (p.anchoredPosition.y < bounds.yMin - 20f) break;
            if (p.anchoredPosition.x < bounds.xMin - 30f) break;
            yield return null;
        }

        if (p != null) Destroy(p.gameObject);
    }

    // -------------------------------------------------------
    // SANDSTORM
    // -------------------------------------------------------

    private static readonly Color[] SandColors =
    {
        new(0.80f, 0.62f, 0.25f, 0.75f),
        new(0.72f, 0.52f, 0.15f, 0.65f),
        new(0.90f, 0.75f, 0.40f, 0.55f),
        new(0.60f, 0.42f, 0.10f, 0.70f),
    };

    private IEnumerator SpawnSandstorm()
    {
        while (true)
        {
            int batch = Random.Range(4, 8);
            for (int i = 0; i < batch; i++)
                SpawnSandParticle();

            yield return new WaitForSeconds(0.04f);
        }
    }

    private void SpawnSandParticle()
    {
        var p = CreateParticle();

        // Size: small dust only (1-4 px base)
        float size = Random.value < 0.7f ? Random.Range(1f, 3f) : Random.Range(3f, 6f);
        // Slight elongation in the direction of travel
        p.sizeDelta = new Vector2(size * Random.Range(1f, 2.5f), size);

        // Spawn across the whole screen width (and a bit off each edge) at a random height
        Rect r = _canvasRect.rect;
        float startX = Random.Range(r.xMin - 20f, r.xMax + 20f);
        float startY = Random.Range(r.yMin, r.yMax);
        p.anchoredPosition = new Vector2(startX, startY);

        // Random sand color
        var img = p.GetComponent<Image>();
        img.color = SandColors[Random.Range(0, SandColors.Length)];

        // Velocity: mostly rightward with a slight downward drift
        float speed = Random.Range(180f, 380f);
        float angleDeg = Random.Range(-25f, 5f); // negative = downward
        float rad = angleDeg * Mathf.Deg2Rad;
        var velocity = new Vector2(Mathf.Cos(rad) * speed, Mathf.Sin(rad) * speed);

        float lifetime = Random.Range(1.2f, 2.8f);
        StartCoroutine(AnimateLinearParticle(p, velocity, lifetime));
    }

    // -------------------------------------------------------
    // SUN RAYS
    // -------------------------------------------------------

    private IEnumerator SpawnSunRays()
    {
        // Initial burst of rays
        for (int i = 0; i < 7; i++)
        {
            SpawnSunRay(i * 25.7f); // ~7 evenly spread rays
            yield return new WaitForSeconds(0.12f);
        }

        // Keep cycling in new rays
        while (true)
        {
            SpawnSunRay(Random.Range(0f, 180f)); // upper half only feels natural
            yield return new WaitForSeconds(Random.Range(0.4f, 0.9f));
        }
    }

    private void SpawnSunRay(float angleDeg)
    {
        var p = CreateParticle();

        float length = Random.Range(120f, 320f);
        float width  = Random.Range(18f, 55f);
        p.sizeDelta = new Vector2(width, length);
        p.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

        // Anchor near top-center of the canvas
        Rect r = _canvasRect.rect;
        float cx = r.center.x + Random.Range(-40f, 40f);
        float cy = r.yMax - Random.Range(30f, 120f);
        p.anchoredPosition = new Vector2(cx, cy);

        var img = p.GetComponent<Image>();
        img.color = new Color(1f, 0.90f, 0.30f, 0f); // start transparent

        float duration = Random.Range(1.8f, 3.5f);
        StartCoroutine(PulseFadeParticle(p, img,
            peakColor: new Color(1f, 0.88f, 0.25f, 0.45f),
            duration: duration,
            scaleFrom: 0.6f, scaleTo: 1.0f));
    }

    // -------------------------------------------------------
    // SUN SPARKLES (tiny bright motes drifting upward)
    // -------------------------------------------------------

    private IEnumerator SpawnSunSparkles()
    {
        yield return new WaitForSeconds(0.5f); // slight delay so rays appear first
        while (true)
        {
            SpawnSparkle();
            yield return new WaitForSeconds(Random.Range(0.08f, 0.18f));
        }
    }

    private void SpawnSparkle()
    {
        var p = CreateParticle();

        float size = Random.Range(3f, 9f);
        p.sizeDelta = new Vector2(size, size);
        p.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));

        Rect r = _canvasRect.rect;
        p.anchoredPosition = new Vector2(
            Random.Range(r.xMin + 20f, r.xMax - 20f),
            Random.Range(r.center.y - 40f, r.yMax - 20f)
        );

        var img = p.GetComponent<Image>();
        img.color = new Color(1f, 0.95f, 0.60f, 0f);

        float drift = Random.Range(20f, 60f);
        var velocity = new Vector2(Random.Range(-15f, 15f), drift);
        float lifetime = Random.Range(1.0f, 2.2f);

        StartCoroutine(AnimateSparkle(p, img, velocity, lifetime));
    }

    private IEnumerator AnimateSparkle(RectTransform p, Image img, Vector2 velocity, float lifetime)
    {
        float elapsed = 0f;
        Color peakColor = new Color(1f, 0.95f, 0.7f, 0.9f);

        while (elapsed < lifetime && p != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            p.anchoredPosition += velocity * Time.deltaTime;

            // Fade in for first 30 %, fade out for last 40 %
            float alpha = t < 0.3f
                ? Mathf.Lerp(0f, peakColor.a, t / 0.3f)
                : Mathf.Lerp(peakColor.a, 0f, (t - 0.3f) / 0.7f);

            img.color = new Color(peakColor.r, peakColor.g, peakColor.b, alpha);
            yield return null;
        }

        if (p != null) Destroy(p.gameObject);
    }

    // -------------------------------------------------------
    // Shared animation coroutines
    // -------------------------------------------------------

    private IEnumerator AnimateLinearParticle(RectTransform p, Vector2 velocity, float lifetime)
    {
        if (p == null) yield break;
        var img = p.GetComponent<Image>();
        Color startColor = img.color;
        Rect bounds = _canvasRect.rect;
        float elapsed = 0f;

        while (elapsed < lifetime && p != null)
        {
            elapsed += Time.deltaTime;
            p.anchoredPosition += velocity * Time.deltaTime;

            // Fade out last 40 % of lifetime
            float t = elapsed / lifetime;
            float alpha = t > 0.6f
                ? Mathf.Lerp(startColor.a, 0f, (t - 0.6f) / 0.4f)
                : startColor.a;
            img.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            // Early exit if particle has left the canvas
            if (p.anchoredPosition.x > bounds.xMax + 30f) break;
            yield return null;
        }

        if (p != null) Destroy(p.gameObject);
    }

    /// <summary>Fades a UI particle in, holds briefly, then fades out.</summary>
    private IEnumerator PulseFadeParticle(
        RectTransform p, Image img, Color peakColor,
        float duration, float scaleFrom, float scaleTo)
    {
        if (p == null) yield break;

        float fadeIn  = duration * 0.25f;
        float hold    = duration * 0.45f;
        float fadeOut = duration * 0.30f;

        // Fade in
        for (float t = 0f; t < fadeIn && p != null; t += Time.deltaTime)
        {
            float f = t / fadeIn;
            img.color = new Color(peakColor.r, peakColor.g, peakColor.b, peakColor.a * f);
            p.localScale = Vector3.Lerp(Vector3.one * scaleFrom, Vector3.one * scaleTo, f);
            yield return null;
        }

        if (p == null) yield break;
        img.color = peakColor;
        p.localScale = Vector3.one * scaleTo;

        // Hold
        yield return new WaitForSeconds(hold);

        // Fade out
        for (float t = 0f; t < fadeOut && p != null; t += Time.deltaTime)
        {
            float f = t / fadeOut;
            img.color = new Color(peakColor.r, peakColor.g, peakColor.b, peakColor.a * (1f - f));
            yield return null;
        }

        if (p != null) Destroy(p.gameObject);
    }

    // -------------------------------------------------------
    // Particle factory
    // -------------------------------------------------------

    private RectTransform CreateParticle()
    {
        var go = new GameObject("p");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.raycastTarget = false;

        return rt;
    }

    // -------------------------------------------------------
    // Cleanup
    // -------------------------------------------------------

    private void OnDestroy()
    {
        foreach (var c in _spawnRoutines)
            if (c != null) StopCoroutine(c);
        _spawnRoutines.Clear();
    }
}
