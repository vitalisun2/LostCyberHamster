using System;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Функциональная область bot diagnostics.
    /// Категории позволяют включать глубокие логи только для слоя, где расследуется регресс.
    /// </summary>
    [Flags]
    internal enum BotDiagnosticCategory
    {
        None = 0,
        Execution = 1 << 0,
        RuntimeEvents = 1 << 1,
        Replan = 1 << 2,
        Planning = 1 << 3,
        BranchSelection = 1 << 4,
        DeadEnd = 1 << 5,
        Strategy = 1 << 6,
        RuntimeSafety = 1 << 7,
        Pattern = 1 << 8,
        Economy = 1 << 9,
        TestResult = 1 << 10,
        All = Execution
              | RuntimeEvents
              | Replan
              | Planning
              | BranchSelection
              | DeadEnd
              | Strategy
              | RuntimeSafety
              | Pattern
              | Economy
              | TestResult
    }
}
