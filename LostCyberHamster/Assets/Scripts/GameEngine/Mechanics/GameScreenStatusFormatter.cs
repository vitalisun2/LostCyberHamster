using System;
using System.Text;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using GameManagement;

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
        private int _lastLevelNumber = int.MinValue;
        private int _lastPatternIndex = int.MinValue;
        private GameState _lastGameState;
        private HamsterStateEnum _lastHamsterState;
        private int _lastEnergy;
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
            var levelNumber = GetDisplayedLevelNumber();
            var patternIndex = GetDisplayedPatternIndex(hamster);
            var patternName = GetDisplayedPatternName(patternIndex);
            var gameState = gameManager.State;
            var hamsterState = hamster.HamsterState.Value;
            var energy = hamster.Energy.Value;
            var isDamaged = hamster.IsDamaged.Value;

            var snapshotChanged = !_hasSnapshot ||
                                  !string.Equals(locationName, _lastLocationName, StringComparison.Ordinal) ||
                                  !string.Equals(partOfDay, _lastPartOfDay, StringComparison.Ordinal) ||
                                  !string.Equals(patternName, _lastPatternName, StringComparison.Ordinal) ||
                                  levelNumber != _lastLevelNumber ||
                                  patternIndex != _lastPatternIndex ||
                                  gameState != _lastGameState ||
                                  hamsterState != _lastHamsterState ||
                                  energy != _lastEnergy ||
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
            _builder.Append("LVL ");
            if (levelNumber > 0)
            {
                _builder.Append(levelNumber);
            }
            else
            {
                _builder.Append('-');
            }

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
            _builder.Append(", energy: ");
            _builder.Append(energy);
            _builder.Append(", isDamaged: ");
            _builder.Append(isDamaged);

            _cachedText = _builder.ToString();
            _lastUpdateTime = currentTime;
            _lastLocationName = locationName;
            _lastPartOfDay = partOfDay;
            _lastPatternName = patternName;
            _lastLevelNumber = levelNumber;
            _lastPatternIndex = patternIndex;
            _lastGameState = gameState;
            _lastHamsterState = hamsterState;
            _lastEnergy = energy;
            _lastIsDamaged = isDamaged;
            _hasSnapshot = true;

            formattedText = _cachedText;
            return true;
        }

        private static int GetDisplayedLevelNumber()
        {
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            if (!LevelManager.TryResolveLevelKey(currentLevel, out _, out _, out var levelOrder))
            {
                return 0;
            }

            return levelOrder + 1;
        }

        private static int GetDisplayedPatternIndex(Hamster hamster)
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

            return spawner.VisiblePatternTracker.GetCurrentPatternIndex(hamster.LeftX, hamster.RightX);
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
