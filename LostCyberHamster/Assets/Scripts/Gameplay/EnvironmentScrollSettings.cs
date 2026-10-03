using System;
using UnityEngine;

namespace Assets.Scripts.Gameplay
{
    /// <summary>Хранит общие скорости четырёх ролей для всех композиций и игры.</summary>
    public sealed class EnvironmentScrollSettings : ScriptableObject
    {
        public const string ResourcePath = "EnvironmentScrollSettings";

        [SerializeField, Min(0f)] private float _roadScrollSpeed = Consts.RoadScrollSpeed;
        [SerializeField, Min(0f)] private float _backgroundScrollSpeed = Consts.BackgroundScrollSpeed;
        [SerializeField, Min(0f)] private float _background2ScrollSpeed = Consts.Background2ScrollSpeed;
        [SerializeField, Min(0f)] private float _skyScrollSpeed = Consts.SkyScrollSpeed;

        private static EnvironmentScrollSettings _current;

        /// <summary>Загружает единый ассет скоростей из Resources.</summary>
        public static EnvironmentScrollSettings Current
        {
            get
            {
                if (_current == null)
                    _current = UnityEngine.Resources.Load<EnvironmentScrollSettings>(ResourcePath);
                if (_current == null)
                    throw new InvalidOperationException("Не найден общий ассет скоростей: Assets/Resources/EnvironmentScrollSettings.asset.");
                return _current;
            }
        }

        /// <summary>Возвращает общую скорость указанной роли.</summary>
        public float GetSpeed(EnvironmentLayerRole role)
        {
            return role switch
            {
                EnvironmentLayerRole.Road => _roadScrollSpeed,
                EnvironmentLayerRole.Background => _backgroundScrollSpeed,
                EnvironmentLayerRole.Background2 => _background2ScrollSpeed,
                EnvironmentLayerRole.Sky => _skyScrollSpeed,
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
        }

        /// <summary>Меняет общую конечную неотрицательную скорость указанной роли.</summary>
        public void SetSpeed(EnvironmentLayerRole role, float speed)
        {
            if (!float.IsFinite(speed) || speed < 0f)
                throw new ArgumentOutOfRangeException(nameof(speed), "Скорость должна быть конечной и неотрицательной.");

            // Настройка сохраняется в едином ассете всех окружений.
            switch (role)
            {
                case EnvironmentLayerRole.Road: _roadScrollSpeed = speed; break;
                case EnvironmentLayerRole.Background: _backgroundScrollSpeed = speed; break;
                case EnvironmentLayerRole.Background2: _background2ScrollSpeed = speed; break;
                case EnvironmentLayerRole.Sky: _skyScrollSpeed = speed; break;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        /// <summary>Проверяет конечность и неотрицательность четырёх общих скоростей.</summary>
        public void Validate()
        {
            for (var i = 0; i < 4; i++)
            {
                var role = (EnvironmentLayerRole)i;
                var speed = GetSpeed(role);
                if (!float.IsFinite(speed) || speed < 0f)
                    throw new InvalidOperationException($"Слой {role}: общая скорость должна быть конечной и неотрицательной.");
            }
        }
    }
}
