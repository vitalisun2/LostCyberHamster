using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Gameplay
{
    /// <summary>Повторяет один слой по ширине камеры, сохраняя фазу прокрутки.</summary>
    internal sealed class EnvironmentStrip
    {
        private readonly SpriteRenderer _source;
        private readonly Transform _copiesRoot;
        private readonly List<SpriteRenderer> _copies = new();
        private readonly float _originX;
        private readonly float _period;
        private readonly float _speed;
        private float _phase;

        /// <summary>Запоминает положение слоя, его период и игровую скорость.</summary>
        public EnvironmentStrip(SpriteRenderer source, Transform copiesRoot, float speed)
        {
            _source = source;
            _copiesRoot = copiesRoot;
            _originX = source.transform.position.x;
            _period = source.sprite.rect.width / source.sprite.pixelsPerUnit;
            _speed = speed;
        }

        /// <summary>Обновляет непрерывную фазу слоя и покрывает видимую область.</summary>
        public void Update(float minX, float maxX, float deltaTime)
        {
            // Сохраняем остаток движения при переходе через границу периода.
            _phase = Mathf.Repeat(_phase + _speed * Consts.GameSpeedBase * deltaTime, _period);
            var originLeft = _originX + _source.sprite.bounds.min.x - _phase;
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
                _copies[i].gameObject.SetActive(i < count - 1);

            // Координаты по высоте остаются такими, как настроены в префабе.
            var position = _source.transform.position;
            position.x = _originX - _phase + firstIndex * _period;
            _source.transform.position = position;
            for (var i = 0; i < count - 1; i++)
            {
                position.x += _period;
                _copies[i].transform.position = position;
            }
        }
    }
}
