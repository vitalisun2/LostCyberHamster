using UnityEngine;

public class ElectricStrikeUlta : MonoBehaviour
{
    [Header("Effect Sprites")]
    [SerializeField] private SpriteRenderer effectRenderer1;
    [SerializeField] private SpriteRenderer effectRenderer2;
    [SerializeField] private SpriteRenderer effectRenderer3;

    [Header("Effect Timing")]
    [SerializeField] private float fadeInDuration = 0.01f; // Плавное появление - 0.01 сек
    [SerializeField] private float visibleDuration = 0.1f; // Длительность видимости - 0.1 сек
    [SerializeField] private float fadeOutDuration = 0.6f; // Плавное угасание - 0.4 сек
    [SerializeField] private float delayBetweenEffects = 0.1f; // Задержка между эффектами - по умолчанию 0.1 сек
    [SerializeField] private float effectSpeedMultiplier = 1.0f; // Общая скорость эффекта

    private bool _isEffect1WorldYLocked;
    private bool _isEffect2WorldYLocked;
    private bool _isEffect3WorldYLocked;
    private float _effect1WorldY;
    private float _effect2WorldY;
    private float _effect3WorldY;

    /// <summary>
    /// Возвращает признак полностью настроенного визуального эффекта.
    /// </summary>
    public bool IsConfigured =>
        HasSprite(effectRenderer1) &&
        HasSprite(effectRenderer2) &&
        HasSprite(effectRenderer3);

    /// <summary>
    /// Возвращает правую мировую границу всех спрайтов удара.
    /// </summary>
    public float WorldRightEdge => Mathf.Max(
        effectRenderer1.bounds.max.x,
        effectRenderer2.bounds.max.x,
        effectRenderer3.bounds.max.x);

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError("ElectricStrikeUlta: One or more SpriteRenderers or sprites are not assigned.");
            Destroy(gameObject);
            return;
        }
        StartCoroutine(PlayElectricStrikeSequence());
    }

    private void LateUpdate()
    {
        ApplyLockedRendererWorldY(effectRenderer1, _isEffect1WorldYLocked, _effect1WorldY);
        ApplyLockedRendererWorldY(effectRenderer2, _isEffect2WorldYLocked, _effect2WorldY);
        ApplyLockedRendererWorldY(effectRenderer3, _isEffect3WorldYLocked, _effect3WorldY);
    }

    /// <summary>Масштабирует видимую дальность относительно точки начала физического удара.</summary>
    public void SetRangeMultiplier(float originX, float multiplier)
    {
        Vector3 position = transform.position;
        position.x = originX + (position.x - originX) * multiplier;
        Vector3 scale = transform.localScale;
        scale.x *= multiplier;
        transform.localScale = scale;
        transform.position = position;
    }

    private System.Collections.IEnumerator PlayElectricStrikeSequence()
    {
        float adjustedDelay = delayBetweenEffects * (1 / effectSpeedMultiplier);

        // Стартуем эффекты с учетом задержек
        StartCoroutine(AnimateFadeInAndOut(effectRenderer1));
        if (adjustedDelay > 0)
        {
            yield return new WaitForSeconds(adjustedDelay);
            StartCoroutine(AnimateFadeInAndOut(effectRenderer2));

            yield return new WaitForSeconds(adjustedDelay);
            StartCoroutine(AnimateFadeInAndOut(effectRenderer3));
        }
        else
        {
            // Параллельный запуск всех эффектов при задержке 0
            StartCoroutine(AnimateFadeInAndOut(effectRenderer2));
            StartCoroutine(AnimateFadeInAndOut(effectRenderer3));
        }

        // Общий расчет времени перед удалением объекта
        float totalEffectDuration = (fadeInDuration + visibleDuration + fadeOutDuration) * (1 / effectSpeedMultiplier);
        float totalTimeToWait = totalEffectDuration + (2 * adjustedDelay);
        yield return new WaitForSeconds(totalTimeToWait);

        Destroy(gameObject); // Уничтожение объекта после завершения эффекта
    }

    private System.Collections.IEnumerator AnimateFadeInAndOut(SpriteRenderer spriteRenderer)
    {
        spriteRenderer.gameObject.SetActive(true);
        LockRendererWorldY(spriteRenderer);

        // Fade in
        yield return Fade(spriteRenderer, 0f, 1f, fadeInDuration * (1 / effectSpeedMultiplier));

        // Keep the sprite fully visible for a moment
        yield return new WaitForSeconds(visibleDuration * (1 / effectSpeedMultiplier));

        // Fade out
        yield return Fade(spriteRenderer, 1f, 0f, fadeOutDuration * (1 / effectSpeedMultiplier));

        spriteRenderer.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator Fade(SpriteRenderer spriteRenderer, float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        Color color = spriteRenderer.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            spriteRenderer.color = color;
            yield return null;
        }
        color.a = endAlpha;
        spriteRenderer.color = color;
    }

    private void LockRendererWorldY(SpriteRenderer spriteRenderer)
    {
        float worldY = spriteRenderer.transform.position.y;
        if (spriteRenderer == effectRenderer1)
        {
            _isEffect1WorldYLocked = true;
            _effect1WorldY = worldY;
            return;
        }

        if (spriteRenderer == effectRenderer2)
        {
            _isEffect2WorldYLocked = true;
            _effect2WorldY = worldY;
            return;
        }

        if (spriteRenderer == effectRenderer3)
        {
            _isEffect3WorldYLocked = true;
            _effect3WorldY = worldY;
        }
    }

    private static void ApplyLockedRendererWorldY(SpriteRenderer spriteRenderer, bool isLocked, float worldY)
    {
        if (!isLocked || spriteRenderer == null)
            return;

        Vector3 position = spriteRenderer.transform.position;
        position.y = worldY;
        spriteRenderer.transform.position = position;
    }

    private static bool HasSprite(SpriteRenderer renderer)
    {
        return renderer != null && renderer.sprite != null;
    }
}
