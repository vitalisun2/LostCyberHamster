using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace Assets.Scripts.Gameplay
{
    /// <summary>Готовая композиция четырёх фонов и её прокрутка во время игры.</summary>
    public sealed class LocationEnvironment : MonoBehaviour,
        Listeners.IGameStartListener,
        Listeners.IGameUpdateListener,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener
    {
        private const string CopiesRootName = "_EnvironmentRepeats";
        public const float RoadBottomWorldY = Consts.RoadBottomYPos;

        [SerializeField] private SpriteRenderer _road;
        [SerializeField] private SpriteRenderer _background;
        [SerializeField] private SpriteRenderer _background2;
        [SerializeField] private SpriteRenderer _sky;
        [SerializeField, Min(0f)] private float _roadScrollSpeed = Consts.RoadScrollSpeed;
        [SerializeField, Min(0f)] private float _backgroundScrollSpeed = Consts.BackgroundScrollSpeed;
        [SerializeField, Min(0f)] private float _background2ScrollSpeed = Consts.Background2ScrollSpeed;
        [SerializeField, Min(0f)] private float _skyScrollSpeed = Consts.SkyScrollSpeed;

        private GameManager _gameManager;
        private Camera _camera;
        private AddressableLease<GameObject> _lease;
        private EnvironmentStrip[] _strips;
        private bool _scrolling;

        /// <summary>Назначает четыре роли и фиксирует порядок отрисовки.</summary>
        public void Configure(SpriteRenderer road, SpriteRenderer background, SpriteRenderer background2, SpriteRenderer sky)
        {
            // Сохраняем авторские спрайты отдельно от служебных повторов.
            _road = road;
            _background = background;
            _background2 = background2;
            _sky = sky;

            // Роли определяют порядок слоёв для редактора и игры.
            for (var i = 0; i < 4; i++)
            {
                var role = (EnvironmentLayerRole)i;
                var renderer = GetLayer(role);
                if (renderer != null)
                    renderer.sortingLayerName = GetSortingLayer(role);
            }
        }

        /// <summary>Возвращает основной спрайт указанной роли.</summary>
        public SpriteRenderer GetLayer(EnvironmentLayerRole role)
        {
            return role switch
            {
                EnvironmentLayerRole.Road => _road,
                EnvironmentLayerRole.Background => _background,
                EnvironmentLayerRole.Background2 => _background2,
                EnvironmentLayerRole.Sky => _sky,
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
        }

        /// <summary>Возвращает исходную игровую скорость прокрутки роли.</summary>
        public static float GetScrollSpeed(EnvironmentLayerRole role)
        {
            return role switch
            {
                EnvironmentLayerRole.Road => Consts.RoadScrollSpeed,
                EnvironmentLayerRole.Background => Consts.BackgroundScrollSpeed,
                EnvironmentLayerRole.Background2 => Consts.Background2ScrollSpeed,
                EnvironmentLayerRole.Sky => Consts.SkyScrollSpeed,
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
        }

        /// <summary>Возвращает сохранённую в композиции скорость указанной роли.</summary>
        public float GetLayerScrollSpeed(EnvironmentLayerRole role)
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

        /// <summary>Сохраняет скорость роли и применяет её к текущей игровой прокрутке.</summary>
        public void SetLayerScrollSpeed(EnvironmentLayerRole role, float speed)
        {
            if (!float.IsFinite(speed) || speed < 0f)
                throw new ArgumentOutOfRangeException(nameof(speed), "Скорость должна быть конечной и неотрицательной.");

            // Скорость принадлежит конкретной композиции и сериализуется вместе с ней.
            switch (role)
            {
                case EnvironmentLayerRole.Road: _roadScrollSpeed = speed; break;
                case EnvironmentLayerRole.Background: _backgroundScrollSpeed = speed; break;
                case EnvironmentLayerRole.Background2: _background2ScrollSpeed = speed; break;
                case EnvironmentLayerRole.Sky: _skyScrollSpeed = speed; break;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }

            // Живая игровая полоса сохраняет фазу после изменения скорости.
            if (_strips != null)
                _strips[(int)role].SetSpeed(speed);
        }

        /// <summary>Возвращает слой отрисовки роли.</summary>
        public static string GetSortingLayer(EnvironmentLayerRole role)
        {
            return role switch
            {
                EnvironmentLayerRole.Road => "Road",
                EnvironmentLayerRole.Background => "Background",
                EnvironmentLayerRole.Background2 => "Background2",
                EnvironmentLayerRole.Sky => "Sky",
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
        }

        /// <summary>Применяет одинаковые настройки камеры к игре и редактору фонов.</summary>
        public static void ConfigureCamera(Camera camera)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            camera.orthographic = true;
            camera.orthographicSize = Consts.CameraSize;
            camera.transform.SetPositionAndRotation(Consts.CameraPosition, Quaternion.identity);
        }

        /// <summary>Проверяет спрайты, скорости и начало координат на нижнем крае дороги.</summary>
        public void ValidateConfiguration()
        {
            // Фиксируем простой контракт: четыре непосредственных дочерних спрайта без масштаба.
            if (transform.localScale != Vector3.one || transform.localRotation != Quaternion.identity
                || !HasFinitePosition(transform.localPosition))
                throw new InvalidOperationException("Корень окружения должен иметь единичный масштаб и нулевой поворот.");
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length != 4)
                throw new InvalidOperationException("Префаб окружения должен содержать ровно четыре SpriteRenderer.");
            for (var i = 0; i < 4; i++)
            {
                var role = (EnvironmentLayerRole)i;
                var renderer = GetLayer(role);
                if (renderer == null || renderer.sprite == null || renderer.transform.parent != transform)
                    throw new InvalidOperationException($"Не задан дочерний спрайт роли {role}.");
                for (var j = 0; j < i; j++)
                    if (renderer == GetLayer((EnvironmentLayerRole)j))
                        throw new InvalidOperationException($"Спрайт роли {role} уже назначен другой роли.");
                if (!renderer.gameObject.activeSelf || !renderer.enabled || renderer.drawMode != SpriteDrawMode.Simple
                    || renderer.transform.localScale != Vector3.one || renderer.transform.localRotation != Quaternion.identity
                    || !HasFinitePosition(renderer.transform.localPosition))
                    throw new InvalidOperationException($"Слой {role} должен быть включён, иметь единичный масштаб и нулевой поворот.");
                var sprite = renderer.sprite;
                if (sprite.rect.width <= 0f || sprite.rect.height <= 0f || !Mathf.Approximately(sprite.pixelsPerUnit, Consts.PixelsPerUnit))
                    throw new InvalidOperationException($"Слой {role}: требуется ненулевой Sprite Rect и PPU {Consts.PixelsPerUnit}.");
                if (sprite.texture.width % 4 != 0 || sprite.texture.height % 4 != 0)
                    throw new InvalidOperationException($"Слой {role}: размер текстуры должен быть кратен четырём.");

                // Скорость задаёт движение влево либо неподвижный слой.
                var speed = GetLayerScrollSpeed(role);
                if (!float.IsFinite(speed) || speed < 0f)
                    throw new InvalidOperationException($"Слой {role}: скорость должна быть конечной и неотрицательной.");
            }

            // Высота рисунка произвольна; нижний край дороги всегда задаёт local Y = 0.
            if (Mathf.Abs(_road.transform.localPosition.y + _road.sprite.bounds.min.y) > 0.001f
                || Mathf.Abs(_road.transform.localPosition.x + _road.sprite.bounds.center.x) > 0.001f)
                throw new InvalidOperationException("Середина нижнего края дороги должна совпадать с началом координат префаба.");
        }

        /// <summary>Принимает владение загруженным префабом и подключает прокрутку к игре.</summary>
        public void Initialize(GameManager gameManager, Camera camera, AddressableLease<GameObject> lease)
        {
            // Проверяем композицию до передачи владения и создания копий.
            ValidateConfiguration();
            if (gameManager == null || camera == null || lease == null || !lease.IsActive)
                throw new ArgumentException("Для окружения нужны GameManager, Camera и действующий Addressables lease.");
            if (_gameManager != null)
                throw new InvalidOperationException("Окружение уже инициализировано.");
            ClearCopies();

            // Создаём независимый период для каждой роли и удерживаем ассет до уничтожения экземпляра.
            var copiesRoot = CreateCopiesRoot();
            _strips = new EnvironmentStrip[4];
            for (var i = 0; i < _strips.Length; i++)
            {
                var role = (EnvironmentLayerRole)i;
                _strips[i] = new EnvironmentStrip(GetLayer(role), copiesRoot, GetLayerScrollSpeed(role));
            }
            _camera = camera;
            _lease = lease;
            _gameManager = gameManager;
            _gameManager.AddListener(this);
            UpdateStrips(0f);
        }

        /// <summary>Покрывает область неподвижными копиями четырёх слоёв для редактора.</summary>
        public void PopulateStatic(float minX, float maxX)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Неподвижные копии окружения создаются только в редакторе.");

            // Убираем предыдущие копии перед проверкой основной композиции.
            ClearCopies();
            ValidateConfiguration();
            if (float.IsNaN(minX) || float.IsInfinity(minX) || float.IsNaN(maxX) || float.IsInfinity(maxX) || maxX <= minX)
                throw new ArgumentException("Правая граница должна быть больше левой.");
            var copiesRoot = CreateCopiesRoot();

            // Основные спрайты остаются на месте: настройки по высоте принадлежат им.
            for (var i = 0; i < 4; i++)
            {
                var renderer = GetLayer((EnvironmentLayerRole)i);
                var period = renderer.sprite.rect.width / renderer.sprite.pixelsPerUnit;
                var sourceLeft = renderer.bounds.min.x;
                var first = Mathf.FloorToInt((minX - sourceLeft) / period);
                var last = Mathf.FloorToInt((maxX - sourceLeft) / period);
                for (var tile = first; tile <= last; tile++)
                {
                    if (tile == 0)
                        continue;
                    var copy = Instantiate(renderer, copiesRoot);
                    copy.name = renderer.name + "_Repeat";
                    copy.transform.position = renderer.transform.position + Vector3.right * (tile * period);
                }
            }
        }

        /// <summary>Удаляет служебные копии, оставляя четыре авторских слоя.</summary>
        public void ClearCopies()
        {
            if (_gameManager != null)
                throw new InvalidOperationException("Копии работающего окружения принадлежат игровой прокрутке.");

            // Находим контейнер и после domain reload редактора.
            var copiesRoot = transform.Find(CopiesRootName);
            if (copiesRoot == null)
                return;

            // В редакторе копии удаляются сразу перед сохранением префаба.
            if (Application.isPlaying)
            {
                copiesRoot.gameObject.SetActive(false);
                Destroy(copiesRoot.gameObject);
            }
            else
                DestroyImmediate(copiesRoot.gameObject);
        }

        /// <summary>Начинает прокрутку после завершения загрузки игры.</summary>
        public void OnStart() => _scrolling = true;

        /// <summary>Двигает четыре слоя с независимыми скоростями и периодами.</summary>
        public void OnUpdate(float deltaTime)
        {
            if (_scrolling && _camera != null)
                UpdateStrips(deltaTime);
        }

        /// <summary>Приостанавливает прокрутку.</summary>
        public void OnPause() => _scrolling = false;

        /// <summary>Возобновляет прокрутку с сохранённой фазы.</summary>
        public void OnResume() => _scrolling = true;

        /// <summary>Создаёт контейнер повторных экземпляров слоёв.</summary>
        private Transform CreateCopiesRoot()
        {
            var root = new GameObject(CopiesRootName);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        /// <summary>Проверяет конечность координат авторского слоя.</summary>
        private static bool HasFinitePosition(Vector3 position)
        {
            return !float.IsNaN(position.x) && !float.IsInfinity(position.x)
                && !float.IsNaN(position.y) && !float.IsInfinity(position.y)
                && !float.IsNaN(position.z) && !float.IsInfinity(position.z);
        }

        /// <summary>Покрывает текущую ширину камеры четырьмя прокручиваемыми полосами.</summary>
        private void UpdateStrips(float deltaTime)
        {
            // Размер видимой области пересчитывается при изменении aspect ratio.
            var halfWidth = _camera.orthographicSize * _camera.aspect;
            var center = _camera.transform.position.x;

            // Каждая полоса сохраняет собственную фазу.
            foreach (var strip in _strips)
                strip.Update(center - halfWidth, center + halfWidth, deltaTime);
        }

        /// <summary>Удаляет подписку на игру и освобождает ресурсы готового префаба.</summary>
        private void OnDestroy()
        {
            // Подписки принадлежат экземпляру сцены.
            if (_gameManager != null)
                _gameManager.RemoveListener(this);

            // Префаб и его текстуры удерживаются до уничтожения окружения.
            _lease?.Dispose();
            _lease = null;
        }
    }
}
