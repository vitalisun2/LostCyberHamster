using System;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Удерживает skateboard actor на дороге или текущей крыше и опускает его на дорогу после края.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkateboardSurfaceController : MonoBehaviour
    {
        /// <summary>
        /// Текущая опорная поверхность skateboard actor.
        /// </summary>
        public enum SurfaceState
        {
            Road,
            Roof,
            DroppingToRoad
        }

        /// <summary>
        /// Роль контакта с roof obstacle для последующей collision policy.
        /// </summary>
        public enum RoofContact
        {
            Support,
            Obstacle
        }

        [SerializeField] private Transform _surfaceTransform;
        [SerializeField] private Collider2D _skateboardCollider;
        [SerializeField, Min(0f)] private float _supportTolerance = 0.05f;
        [SerializeField, Min(0.01f)] private float _dropSpeed = 6f;

        private Obstacle _currentRoof;
        private float _currentRoofTop;
        private float _roadLocalY;
        private bool _initialized;

        public SurfaceState State { get; private set; } = SurfaceState.Road;
        public Obstacle CurrentRoof => _currentRoof;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Сразу переводит actor на сохранённую высоту дороги и очищает roof support.
        /// </summary>
        public void EnterRoad()
        {
            EnsureInitialized();

            // Убираем roof ownership и возвращаем общий surface root на дорожную высоту.
            _currentRoof = null;
            _currentRoofTop = 0f;
            SetSurfaceLocalY(_roadLocalY);
            State = SurfaceState.Road;
        }

        /// <summary>
        /// Ставит actor на верх переданной крыши. Вызывать только при входе в режим из RoofRun.
        /// </summary>
        public void EnterRoof(Obstacle roof)
        {
            EnsureInitialized();
            ValidateRoof(roof);

            // Запоминаем разрешённую support-chain и совмещаем низ collider с верхом крыши.
            CollisionUtils.GetObstacleYInterval(roof, out _, out _currentRoofTop);
            _currentRoof = roof;
            AlignColliderBottomTo(_currentRoofTop);
            State = SurfaceState.Roof;
        }

        /// <summary>
        /// Проверяет roof support и выполняет переход с края крыши на дорогу.
        /// </summary>
        public void Tick(float deltaTime, bool isOnBottomLine)
        {
            EnsureInitialized();

            if (State == SurfaceState.Roof)
            {
                UpdateRoofSupport(isOnBottomLine);
                return;
            }

            if (State == SurfaceState.DroppingToRoad)
                UpdateDrop(Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// Отличает разрешённый контакт сверху с текущей roof-chain от столкновения с препятствием.
        /// </summary>
        public RoofContact ClassifyRoofContact(Obstacle roof, bool isOnBottomLine)
        {
            EnsureInitialized();

            if (State != SurfaceState.Roof || !IsValidRoof(roof))
                return RoofContact.Obstacle;

            // Road никогда не получает новую roof support через collision. Допускается только текущая chain.
            bool belongsToCurrentChain =
                roof == _currentRoof ||
                IsContinuationCandidate(roof, isOnBottomLine, allowHigherRoof: true);
            return belongsToCurrentChain && IsTopContact(roof)
                ? RoofContact.Support
                : RoofContact.Obstacle;
        }

        /// <summary>
        /// При приземлении с roof-chain выбирает любую крышу текущей линии под actor.
        /// </summary>
        public bool ResolveLandingSupport(bool isOnBottomLine)
        {
            EnsureInitialized();

            // Прыжок с дороги не создаёт roof support.
            if (State != SurfaceState.Roof)
                return false;

            Obstacle support = FindRoofUnderActor(
                isOnBottomLine,
                allowHigherRoof: true);
            if (support != null)
            {
                EnterRoof(support);
                return true;
            }

            BeginDropToRoad();
            return false;
        }

        /// <summary>
        /// Полностью очищает runtime-состояние и восстанавливает исходную дорожную высоту.
        /// </summary>
        public void Reset()
        {
            // Unity вызывает Reset сразу после добавления component, когда refs ещё могут быть пустыми.
            if (_surfaceTransform == null || _skateboardCollider == null)
            {
                _currentRoof = null;
                _currentRoofTop = 0f;
                State = SurfaceState.Road;
                _initialized = false;
                return;
            }

            EnterRoad();
        }

        private void UpdateRoofSupport(bool isOnBottomLine)
        {
            // Пока collider пересекает текущую крышу по X, support остаётся прежним.
            if (IsValidRoof(_currentRoof) &&
                HelpMethods.IsOnSameLine(isOnBottomLine, _currentRoof) &&
                HasHorizontalOverlap(_currentRoof))
            {
                return;
            }

            // Roof может продолжиться другой крышей той же линии. Road-to-roof здесь невозможен.
            Obstacle continuation = FindRoofUnderActor(
                isOnBottomLine,
                allowHigherRoof: false);
            if (continuation != null)
            {
                EnterRoof(continuation);
                return;
            }

            BeginDropToRoad();
        }

        private void UpdateDrop(float deltaTime)
        {
            float nextY = Mathf.MoveTowards(
                _surfaceTransform.localPosition.y,
                _roadLocalY,
                _dropSpeed * deltaTime);
            SetSurfaceLocalY(nextY);

            if (Mathf.Approximately(nextY, _roadLocalY))
                State = SurfaceState.Road;
        }

        private Obstacle FindRoofUnderActor(
            bool isOnBottomLine,
            bool allowHigherRoof)
        {
            if (ObstacleSpawner.Instance == null)
                return null;

            Obstacle best = null;
            float bestTop = float.NegativeInfinity;
            foreach (InstantiatedObstacle spawned in ObstacleSpawner.Instance.SpawnedObstacles)
            {
                Obstacle candidate = spawned?.ObstacleScript;
                if (!IsContinuationCandidate(
                        candidate,
                        isOnBottomLine,
                        allowHigherRoof) ||
                    !HasHorizontalOverlap(candidate))
                {
                    continue;
                }

                // Более высокая доступная крыша даёт ближайшую поверхность под actor.
                CollisionUtils.GetObstacleYInterval(candidate, out _, out float candidateTop);
                if (candidateTop > bestTop)
                {
                    best = candidate;
                    bestTop = candidateTop;
                }
            }

            return best;
        }

        private bool IsContinuationCandidate(
            Obstacle roof,
            bool isOnBottomLine,
            bool allowHigherRoof)
        {
            if (!IsValidRoof(roof) || _currentRoof == null)
                return false;

            if (!HelpMethods.IsOnSameLine(isOnBottomLine, roof))
                return false;

            // Более высокая крыша встречается боком. Равная или нижняя продолжает roof-chain.
            CollisionUtils.GetObstacleYInterval(roof, out _, out float roofTop);
            return allowHigherRoof || roofTop <= _currentRoofTop + _supportTolerance;
        }

        private void BeginDropToRoad()
        {
            _currentRoof = null;
            _currentRoofTop = 0f;
            State = SurfaceState.DroppingToRoad;
        }

        private bool IsTopContact(Obstacle roof)
        {
            if (!HasHorizontalOverlap(roof))
                return false;

            CollisionUtils.GetObstacleYInterval(roof, out _, out float roofTop);
            return Mathf.Abs(_skateboardCollider.bounds.min.y - roofTop) <= _supportTolerance;
        }

        private bool HasHorizontalOverlap(Obstacle roof)
        {
            if (!IsValidRoof(roof))
                return false;

            CollisionUtils.GetObstacleXInterval(
                roof,
                roof.ColliderWidth,
                0f,
                out float roofLeft,
                out float roofRight);
            Bounds skateboardBounds = _skateboardCollider.bounds;
            return CollisionUtils.IsOverlap(
                skateboardBounds.min.x,
                skateboardBounds.max.x,
                roofLeft,
                roofRight);
        }

        private void AlignColliderBottomTo(float targetWorldY)
        {
            float deltaY = targetWorldY - _skateboardCollider.bounds.min.y;
            Vector3 position = _surfaceTransform.position;
            position.y += deltaY;
            _surfaceTransform.position = position;
        }

        private void SetSurfaceLocalY(float localY)
        {
            Vector3 localPosition = _surfaceTransform.localPosition;
            localPosition.y = localY;
            _surfaceTransform.localPosition = localPosition;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            ValidateReferences();
            _roadLocalY = _surfaceTransform.localPosition.y;
            _initialized = true;
        }

        private static bool IsValidRoof(Obstacle roof)
        {
            return roof != null
                   && roof.isActiveAndEnabled
                   && roof.ObstacleType != null
                   && CollisionUtils.IsRoofObstacle(roof.ObstacleType.ObstacleTypeEnum);
        }

        private static void ValidateRoof(Obstacle roof)
        {
            if (!IsValidRoof(roof))
                throw new ArgumentException("Skateboard roof support must be an active roof obstacle.", nameof(roof));
        }

        private void ValidateReferences()
        {
            if (_surfaceTransform == null)
            {
                throw new MissingReferenceException(
                    "SkateboardSurfaceController requires surface Transform.");
            }

            if (_skateboardCollider == null)
            {
                throw new MissingReferenceException(
                    "SkateboardSurfaceController requires skateboard Collider2D.");
            }
        }
    }
}
