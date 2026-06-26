namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Уровень подробности bot diagnostics.
    /// Essential оставляет постоянные факты прогона, Verbose включает разбор решений,
    /// Trace предназначен для шумных per-candidate/per-obstacle расследований.
    /// </summary>
    internal enum BotDiagnosticLevel
    {
        Essential = 0,
        Verbose = 1,
        Trace = 2
    }
}
