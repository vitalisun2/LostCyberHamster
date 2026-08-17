#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>
/// Описывает результат обработанного Skateboard-контакта для DEV diagnostics.
/// </summary>
public enum SkateboardCollisionOutcome
{
    Destroy,
    Support,
    Collect,
    Ignored
}
#endif
