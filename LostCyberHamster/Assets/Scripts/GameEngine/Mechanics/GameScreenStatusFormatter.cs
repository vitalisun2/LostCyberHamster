using System;
using System.Text;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public sealed class GameScreenStatusFormatter
    {
    private const float _minUpdateInterval = 0.1f;

        private readonly StringBuilder _builder = new StringBuilder(128);

        private float _lastUpdateTime = float.NegativeInfinity;
        private string _lastLocationName = string.Empty;
        private string _lastPartOfDay = string.Empty;
        private string _lastPatternName = string.Empty;
        private int _lastPatternIndex = int.MinValue;
        private GameState _lastGameState;
        private HamsterStateEnum _lastHamsterState;
        private bool _lastIsDamaged;
        private string _cachedText = string.Empty;
        private bool _hasSnapshot;

        public bool TryFormat(
            float currentTime,
            GameManager gameManager,
            Hamster hamster,
            out string formattedText)
        {
            formattedText = _cachedText;

            if (gameManager == null || hamster == null)
            {
                return false;
            }

            if (_hasSnapshot && currentTime - _lastUpdateTime < _minUpdateInterval)
            {
                return false;
            }

            var locationName = LevelManager.GetLocationName() ?? string.Empty;
            var partOfDay = LevelManager.GetCurrentPartOfDay() ?? string.Empty;
            var patternIndex = GetDisplayedPatternIndex();
            var patternName = GetDisplayedPatternName(patternIndex);
            var gameState = gameManager.State;
            var hamsterState = hamster.HamsterState.Value;
            var isDamaged = hamster.IsDamaged.Value;

            var snapshotChanged = !_hasSnapshot ||
                                  !string.Equals(locationName, _lastLocationName, StringComparison.Ordinal) ||
                                  !string.Equals(partOfDay, _lastPartOfDay, StringComparison.Ordinal) ||
                                  !string.Equals(patternName, _lastPatternName, StringComparison.Ordinal) ||
                                  patternIndex != _lastPatternIndex ||
                                  gameState != _lastGameState ||
                                  hamsterState != _lastHamsterState ||
                                  isDamaged != _lastIsDamaged;

            if (!snapshotChanged)
            {
                return false;
            }

            _builder.Clear();
            _builder.Append(string.IsNullOrEmpty(locationName) ? "-" : locationName);
            _builder.Append(' ');
            _builder.Append(string.IsNullOrEmpty(partOfDay) ? "-" : partOfDay);
            _builder.Append(", ");
            _builder.Append(string.IsNullOrEmpty(patternName) ? "-" : patternName);
            _builder.Append(' ');
            if (patternIndex >= 0)
            {
                _builder.Append(patternIndex);
            }
            else
            {
                _builder.Append('-');
            }

            _builder.Append(", ");
            _builder.Append(gameState);
            _builder.Append(",\n ");
            _builder.Append(hamsterState);
            _builder.Append(", isDamaged: ");
            _builder.Append(isDamaged);

            _cachedText = _builder.ToString();
            _lastUpdateTime = currentTime;
            _lastLocationName = locationName;
            _lastPartOfDay = partOfDay;
            _lastPatternName = patternName;
            _lastPatternIndex = patternIndex;
            _lastGameState = gameState;
            _lastHamsterState = hamsterState;
            _lastIsDamaged = isDamaged;
            _hasSnapshot = true;

            formattedText = _cachedText;
            return true;
        }

        private static int GetDisplayedPatternIndex()
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
            {
                return -1;
            }

            var levelController = LevelController.Instance;
            if (levelController?.IsLevelLoaded != true)
            {
                return -1;
            }

            var index = spawner.CurrPatternIndex - 1;
            return index >= 0 ? index : -1;
        }

        private static string GetDisplayedPatternName(int patternIndex)
        {
            if (patternIndex < 0)
            {
                return string.Empty;
            }

            var levelInfo = LevelController.Instance?.LevelData?.LevelInfo;
            var patterns = levelInfo?.patterns;
            if (patterns == null || patternIndex >= patterns.Count)
            {
                return string.Empty;
            }

            return patterns[patternIndex]?.name ?? string.Empty;
        }
    }
}
