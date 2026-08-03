using System;
using System.Collections.Generic;
using System.Linq;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Выбирает набор из трёх дневных квестов.
    /// </summary>
    public sealed class DailyQuestGenerator
    {
        private static readonly DailyQuestDifficulty[]
            _requiredDifficulties =
            {
                DailyQuestDifficulty.Simple,
                DailyQuestDifficulty.Medium,
                DailyQuestDifficulty.Hard
            };

        private readonly Random _random = new();

        /// <summary>
        /// Сохраняет защищённые квесты и заполняет свободные сложности
        /// случайными квестами с разными механиками.
        /// </summary>
        public IReadOnlyList<string> Generate(
            IReadOnlyList<QuestDefinition> pool,
            IReadOnlyList<string> activeQuestIds,
            IReadOnlyCollection<string> protectedQuestIds,
            IReadOnlyCollection<string> lastGeneratedQuestIds)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            // Разрешаем сохранённые ID через текущий пул.
            Dictionary<string, QuestDefinition> definitionsById =
                pool.ToDictionary(
                    definition => definition.Id,
                    StringComparer.Ordinal);
            var protectedIds = new HashSet<string>(
                protectedQuestIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            List<QuestDefinition> selectedDefinitions =
                (activeQuestIds ?? Array.Empty<string>())
                .Where(protectedIds.Contains)
                .Concat(protectedIds)
                .Distinct(StringComparer.Ordinal)
                .Select(questId => ResolveDefinition(
                    definitionsById,
                    questId))
                .ToList();
            ValidateProtectedDefinitions(selectedDefinitions);

            // Строим полные варианты для незанятых сложностей.
            var usedMechanicIds = new HashSet<string>(
                selectedDefinitions.Select(
                    definition => definition.DailyMechanicId),
                StringComparer.Ordinal);
            var usedDifficulties =
                new HashSet<DailyQuestDifficulty>(
                    selectedDefinitions.Select(
                        definition => definition.DailyDifficulty));
            List<DailyQuestDifficulty> missingDifficulties =
                _requiredDifficulties
                    .Where(difficulty =>
                        !usedDifficulties.Contains(difficulty))
                    .ToList();
            var combinations =
                new List<List<QuestDefinition>>();
            BuildCombinations(
                pool,
                missingDifficulties,
                0,
                usedMechanicIds,
                new List<QuestDefinition>(),
                combinations);
            if (combinations.Count == 0)
            {
                throw new InvalidOperationException(
                    "Пул дневных квестов не позволяет собрать " +
                    "три разные сложности и механики.");
            }

            // Предпочитаем вариант без механик прошлого набора.
            HashSet<string> lastGeneratedMechanicIds =
                ResolveMechanicIds(
                    definitionsById,
                    lastGeneratedQuestIds);
            int minimumRepeatCount = combinations.Min(
                combination => combination.Count(definition =>
                    lastGeneratedMechanicIds.Contains(
                        definition.DailyMechanicId)));
            List<List<QuestDefinition>> bestCombinations =
                combinations
                    .Where(combination =>
                        combination.Count(definition =>
                            lastGeneratedMechanicIds.Contains(
                                definition.DailyMechanicId)) ==
                        minimumRepeatCount)
                    .ToList();
            lock (_random)
            {
                selectedDefinitions.AddRange(
                    bestCombinations[_random.Next(
                        bestCombinations.Count)]);
            }

            // Возвращаем стабильный порядок Simple, Medium, Hard.
            return selectedDefinitions
                .OrderBy(definition => definition.DailyDifficulty)
                .Select(definition => definition.Id)
                .ToList()
                .AsReadOnly();
        }

        private static QuestDefinition ResolveDefinition(
            IReadOnlyDictionary<string, QuestDefinition> definitionsById,
            string questId)
        {
            if (!definitionsById.TryGetValue(
                    questId,
                    out QuestDefinition definition))
            {
                throw new ArgumentException(
                    $"Защищённый квест отсутствует в пуле: {questId}.",
                    nameof(questId));
            }

            return definition;
        }

        private static void ValidateProtectedDefinitions(
            IReadOnlyCollection<QuestDefinition> definitions)
        {
            bool hasRepeatedDifficulty = definitions
                .GroupBy(definition => definition.DailyDifficulty)
                .Any(group => group.Count() > 1);
            bool hasRepeatedMechanic = definitions
                .Select(definition => definition.DailyMechanicId)
                .Distinct(StringComparer.Ordinal)
                .Count() != definitions.Count;
            if (definitions.Count > _requiredDifficulties.Length ||
                hasRepeatedDifficulty ||
                hasRepeatedMechanic)
            {
                throw new InvalidOperationException(
                    "Защищённые дневные квесты не помещаются " +
                    "в три разные сложности и механики.");
            }
        }

        private static void BuildCombinations(
            IReadOnlyList<QuestDefinition> pool,
            IReadOnlyList<DailyQuestDifficulty> missingDifficulties,
            int difficultyIndex,
            ISet<string> usedMechanicIds,
            List<QuestDefinition> currentCombination,
            ICollection<List<QuestDefinition>> combinations)
        {
            if (difficultyIndex >= missingDifficulties.Count)
            {
                combinations.Add(
                    new List<QuestDefinition>(currentCombination));
                return;
            }

            DailyQuestDifficulty requiredDifficulty =
                missingDifficulties[difficultyIndex];
            foreach (QuestDefinition definition in pool)
            {
                if (definition.DailyDifficulty != requiredDifficulty ||
                    usedMechanicIds.Contains(
                        definition.DailyMechanicId))
                {
                    continue;
                }

                usedMechanicIds.Add(definition.DailyMechanicId);
                currentCombination.Add(definition);
                BuildCombinations(
                    pool,
                    missingDifficulties,
                    difficultyIndex + 1,
                    usedMechanicIds,
                    currentCombination,
                    combinations);
                currentCombination.RemoveAt(
                    currentCombination.Count - 1);
                usedMechanicIds.Remove(definition.DailyMechanicId);
            }
        }

        private static HashSet<string> ResolveMechanicIds(
            IReadOnlyDictionary<string, QuestDefinition> definitionsById,
            IReadOnlyCollection<string> questIds)
        {
            var mechanicIds =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (string questId in
                     questIds ?? Array.Empty<string>())
            {
                if (definitionsById.TryGetValue(
                        questId,
                        out QuestDefinition definition))
                {
                    mechanicIds.Add(definition.DailyMechanicId);
                }
            }

            return mechanicIds;
        }
    }
}
