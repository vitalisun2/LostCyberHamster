using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.Gameplay
{
    /// <summary>Повторяет один слой по ширине камеры, сохраняя фазу прокрутки.</summary>
    public sealed class EnvironmentStrip
    {
        private readonly SpriteRenderer _source;
        private readonly Transform _copiesRoot;
        private readonly List<SpriteRenderer> _copies = new();
        private readonly float _originX;
        private readonly float _period;
        private float _speed;
        private double _phase;

        /// <summary>Запоминает положение слоя, его период и игровую скорость.</summary>
        public EnvironmentStrip(SpriteRenderer source, Transform copiesRoot, float speed)
        {
            _source = source;
            _copiesRoot = copiesRoot;
            _originX = source.transform.position.x;
            _period = source.sprite.rect.width / source.sprite.pixelsPerUnit;
            SetSpeed(speed);
            SourceX = _originX;
        }

        /// <summary>Текущая горизонтальная координата основного повторения.</summary>
        public float SourceX { get; private set; }

        /// <summary>Меняет конечную неотрицательную скорость, сохраняя фазу слоя.</summary>
        public void SetSpeed(float speed)
        {
            if (!float.IsFinite(speed) || speed < 0f)
                throw new ArgumentOutOfRangeException(nameof(speed), "Скорость должна быть конечной и неотрицательной.");
            _speed = speed;
        }

        /// <summary>Обновляет непрерывную фазу слоя и покрывает видимую область.</summary>
        public void Update(float minX, float maxX, double deltaTime)
        {
            // Сохраняем остаток движения при переходе через границу периода.
            _phase = (_phase + _speed * (double)Consts.GameSpeedBase * deltaTime) % _period;
            var originLeft = _originX + _source.sprite.bounds.min.x - (float)_phase;
            var firstIndex = Mathf.FloorToInt((minX - originLeft) / _period);
            var count = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / _period) + 2);

            // Меняем число копий только при изменении ширины видимой области.
            while (_copies.Count < count - 1)
            {
                var copy = Object.Instantiate(_source, _copiesRoot);
                copy.name = _source.name + "_Repeat";
                _copies.Add(copy);
            }
            for (var i = 0; i < _copies.Count; i++)
                if (_copies[i].gameObject.activeSelf != (i < count - 1))
                    _copies[i].gameObject.SetActive(i < count - 1);

            // Координаты по высоте остаются такими, как настроены в префабе.
            var position = _source.transform.position;
            SourceX = _originX - (float)_phase + firstIndex * _period;
            position.x = SourceX;
            _source.transform.position = position;
            for (var i = 0; i < count - 1; i++)
            {
                position.x += _period;
                _copies[i].transform.position = position;
            }
        }
    }
}
