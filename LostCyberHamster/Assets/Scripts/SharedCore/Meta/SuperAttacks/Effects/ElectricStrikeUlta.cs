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

    public bool IsConfigured =>
        effectRenderer1 != null &&
        effectRenderer2 != null &&
        effectRenderer3 != null;

    public float WorldRightEdge => Mathf.Max(
        effectRenderer1.bounds.max.x,
        effectRenderer2.bounds.max.x,
        effectRenderer3.bounds.max.x);

    private void Start()
    {
        if (!IsConfigured)
        {
            Debug.LogError("ElectricStrikeUlta: One or more SpriteRenderers are not assigned.");
            Destroy(gameObject);
            return;
        }
        StartCoroutine(PlayElectricStrikeSequence());
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
}
