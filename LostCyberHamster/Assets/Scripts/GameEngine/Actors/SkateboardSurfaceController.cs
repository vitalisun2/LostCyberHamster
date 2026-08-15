using System;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Владеет только геометрией road/roof support Skateboard и общим surface root.
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
        /// Неизменяемый план roof landing, рассчитанный в начале jump-cycle.
        /// </summary>
        public readonly struct LandingSurfacePlan
        {
            public LandingSurfacePlan(Obstacle support, float worldTravel)
            {
                Support = support;
                WorldTravel = worldTravel;
            }

            public Obstacle Support { get; }
            public float WorldTravel { get; }
            public bool LandsOnRoof => Support != null;
        }

        /// <summary>
        /// Результат применения заранее рассчитанного roof landing plan.
        /// </summary>
        public readonly struct LandingSurfaceResult
        {
            public LandingSurfaceResult(Obstacle support, Obstacle missedRoof)
            {
                Support = support;
                MissedRoof = missedRoof;
            }

            public Obstacle Support { get; }
            public Obstacle MissedRoof { get; }
        }

        /// <summary>
        /// Read-only geometry snapshot для Skateboard diagnostic events.
        /// </summary>
        internal readonly struct RoofGeometryDiagnostic
        {
            public RoofGeometryDiagnostic(
                Bounds sensorBounds,
                Bounds polygonBounds,
                Bounds roofBounds,
                bool horizontalOverlap,
                bool verticalOverlap,
                bool topContact,
                bool sideContact,
                bool insideRoof)
            {
                SensorBounds = sensorBounds;
                PolygonBounds = polygonBounds;
                RoofBounds = roofBounds;
                HorizontalOverlap = horizontalOverlap;
                VerticalOverlap = verticalOverlap;
                TopContact = topContact;
                SideContact = sideContact;
                InsideRoof = insideRoof;
            }

            public Bounds SensorBounds { get; }
            public Bounds PolygonBounds { get; }
            public Bounds RoofBounds { get; }
            public bool HorizontalOverlap { get; }
            public bool VerticalOverlap { get; }
            public bool TopContact { get; }
            public bool SideContact { get; }
            public bool InsideRoof { get; }
        }

        [SerializeField] private Transform _surfaceTransform;
        [SerializeField] private Collider2D _skateboardCollider;
        [SerializeField, Min(0f)] private float _supportTolerance = 0.05f;
        [SerializeField, Min(0.01f)] private float _dropSpeed = 6f;

        private ObstacleSpawner _obstacleSpawner;
        private Collider2D _boardContactCollider;
        private Obstacle _currentRoof;
        private float _currentRoofTop;
        private float _roadLocalY;
        private bool _initialized;

        public SurfaceState State { get; private set; } = SurfaceState.Road;
        public Obstacle CurrentRoof => _currentRoof;
        internal float SurfaceWorldY => _surfaceTransform.position.y;
        internal float RoadTargetWorldY => ResolveRoadTargetWorldY();
        internal float CurrentRoofTop => _currentRoofTop;
        internal Bounds BoardContactBounds
        {
            get
            {
                EnsureInitialized();
                return _boardContactCollider.bounds;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Передаёт controller явный live obstacle source из runtime composition.
        /// </summary>
        public void Configure(ObstacleSpawner obstacleSpawner)
        {
            _obstacleSpawner = obstacleSpawner ??
                throw new ArgumentNullException(nameof(obstacleSpawner));
        }

        /// <summary>
        /// Сразу переводит общий surface root на сохранённую высоту дороги.
        /// </summary>
        public void EnterRoad()
        {
            EnsureInitialized();

            _currentRoof = null;
            _currentRoofTop = 0f;
            SetSurfaceLocalY(_roadLocalY);
            State = SurfaceState.Road;
        }

        /// <summary>
        /// Ставит общий visual+collision root на верх переданной крыши.
        /// </summary>
        private void EnterRoof(Obstacle roof)
        {
            PrepareRoof(roof);
            AlignBoardContactTo(_currentRoofTop);
        }

        /// <summary>
        /// Фиксирует initial roof ownership до включения skateboard colliders.
        /// </summary>
        public void PrepareRoof(Obstacle roof)
        {
            EnsureInitialized();
            ValidateRoof(roof);

            CollisionUtils.GetObstacleYInterval(roof, out _, out _currentRoofTop);
            _currentRoof = roof;
            State = SurfaceState.Roof;
        }

        /// <summary>
        /// Выравнивает уже включённый actor по стабильному board baseline.
        /// </summary>
        public void AlignToPreparedRoof()
        {
            EnsureInitialized();
            ValidateRoof(_currentRoof);
            AlignBoardContactTo(_currentRoofTop);
        }

        /// <summary>
        /// Проверяет support и выполняет переход с края крыши на дорогу.
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
        /// Возвращает true для current или проходимого продолжения текущей roof-chain в Ride.
        /// </summary>
        public bool IsRideSupport(Obstacle roof, bool isOnBottomLine)
        {
            EnsureInitialized();
            if (!IsValidRoof(roof) || !HelpMethods.IsOnSameLine(isOnBottomLine, roof))
                return false;

            if (State == SurfaceState.Roof && ReferenceEquals(roof, _currentRoof))
                return true;

            if (State != SurfaceState.Roof ||
                !IsValidRoof(_currentRoof))
            {
                return false;
            }

            // Равная или более низкая близкая крыша продолжает chain. Высокая встречается боком.
            CollisionUtils.GetObstacleYInterval(roof, out _, out float roofTop);
            return roofTop <= _currentRoofTop + _supportTolerance;
        }

        /// <summary>
        /// Прогнозирует roof support по будущему X-интервалу в момент landing contact.
        /// </summary>
        public LandingSurfacePlan PredictRoofLanding(
            bool isOnBottomLine,
            float worldTravel)
        {
            EnsureInitialized();
            EnsureConfigured();

            Obstacle bestSupport = null;
            float bestSupportTop = float.NegativeInfinity;
            float safeWorldTravel = Mathf.Max(0f, worldTravel);
            Bounds boardBounds = _boardContactCollider.bounds;

            // Крыши статичны внутри мира: прогнозируем только их общий scroll по X.
            foreach (InstantiatedObstacle spawned in _obstacleSpawner.SpawnedObstacles)
            {
                Obstacle candidate = spawned?.ObstacleScript;
                if (!IsValidRoof(candidate) ||
                    !HelpMethods.IsOnSameLine(isOnBottomLine, candidate))
                {
                    continue;
                }

                CollisionUtils.GetObstacleXInterval(
                    candidate,
                    candidate.ColliderWidth,
                    safeWorldTravel,
                    out float roofLeft,
                    out float roofRight);
                if (!CollisionUtils.IsOverlap(
                        boardBounds.min.x,
                        boardBounds.max.x,
                        roofLeft,
                        roofRight))
                {
                    continue;
                }

                CollisionUtils.GetObstacleYInterval(candidate, out _, out float roofTop);
                if (roofTop > bestSupportTop)
                {
                    bestSupport = candidate;
                    bestSupportTop = roofTop;
                }
            }

            return new LandingSurfacePlan(bestSupport, safeWorldTravel);
        }

        /// <summary>
        /// Применяет immutable roof plan без повторного geometry scan в contact frame.
        /// </summary>
        public LandingSurfaceResult ApplyRoofLandingPlan(in LandingSurfacePlan plan)
        {
            EnsureInitialized();
            if (plan.Support != null)
            {
                EnterRoof(plan.Support);
                return new LandingSurfaceResult(plan.Support, missedRoof: null);
            }

            BeginDropToRoad();
            return new LandingSurfaceResult(support: null, missedRoof: null);
        }

        /// <summary>
        /// Сохраняет road landing без попытки принять roof support.
        /// </summary>
        public void ResolveRoadLanding()
        {
            EnsureInitialized();
            if (State != SurfaceState.Road)
                BeginDropToRoad();
        }

        /// <summary>
        /// Полностью очищает runtime-состояние и восстанавливает дорожную высоту.
        /// </summary>
        public void Reset()
        {
            // Unity вызывает Reset после добавления component, когда refs ещё могут быть пустыми.
            if (_surfaceTransform == null || _skateboardCollider == null)
            {
                _boardContactCollider = null;
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
            // Stable board footprint не зависит от active sprite frame или skin scale.
            if (IsValidRoof(_currentRoof) &&
                HelpMethods.IsOnSameLine(isOnBottomLine, _currentRoof) &&
                HasBoardHorizontalOverlap(_currentRoof))
            {
                return;
            }

            Obstacle continuation = FindRideContinuation(isOnBottomLine);
            if (continuation != null)
            {
                EnterRoof(continuation);
                return;
            }

            BeginDropToRoad();
        }

        private Obstacle FindRideContinuation(bool isOnBottomLine)
        {
            EnsureConfigured();

            Obstacle best = null;
            float bestTop = float.NegativeInfinity;
            foreach (InstantiatedObstacle spawned in _obstacleSpawner.SpawnedObstacles)
            {
                Obstacle candidate = spawned?.ObstacleScript;
                if (!IsValidRoof(candidate) ||
                    !HelpMethods.IsOnSameLine(isOnBottomLine, candidate) ||
                    !HasBoardHorizontalOverlap(candidate))
                {
                    continue;
                }

                CollisionUtils.GetObstacleYInterval(candidate, out _, out float roofTop);
                if (roofTop > _currentRoofTop + _supportTolerance || roofTop <= bestTop)
                    continue;

                best = candidate;
                bestTop = roofTop;
            }

            return best;
        }

        private void UpdateDrop(float deltaTime)
        {
            float nextY = Mathf.MoveTowards(
                _surfaceTransform.localPosition.y,
                _roadLocalY,
                _dropSpeed * deltaTime);
            SetSurfaceLocalY(nextY);

            if (Mathf.Approximately(nextY, _roadLocalY))
            {
                State = SurfaceState.Road;
            }
        }

        private void BeginDropToRoad()
        {
            _currentRoof = null;
            _currentRoofTop = 0f;
            State = SurfaceState.DroppingToRoad;
        }

        /// <summary>
        /// Снимает текущую geometry без mutation collider или surface state.
        /// </summary>
        internal bool TryCaptureRoofGeometry(
            Obstacle roof,
            out RoofGeometryDiagnostic diagnostic)
        {
            EnsureInitialized();
            diagnostic = default;
            if (!IsValidRoof(roof))
                return false;

            Bounds sensorBounds = _boardContactCollider.bounds;
            Bounds polygonBounds = _skateboardCollider.bounds;
            BoxCollider2D roofCollider = roof.GetComponentInChildren<BoxCollider2D>();
            if (roofCollider == null)
                return false;

            Bounds roofBounds = roofCollider.bounds;
            bool horizontalOverlap = CollisionUtils.IsOverlap(
                sensorBounds.min.x,
                sensorBounds.max.x,
                roofBounds.min.x,
                roofBounds.max.x);
            bool verticalOverlap = CollisionUtils.IsOverlap(
                polygonBounds.min.y,
                polygonBounds.max.y,
                roofBounds.min.y,
                roofBounds.max.y);
            bool topContact = horizontalOverlap &&
                              polygonBounds.min.y <= roofBounds.max.y + _supportTolerance &&
                              polygonBounds.center.y >= roofBounds.max.y;
            bool insideRoof = horizontalOverlap &&
                              polygonBounds.center.y > roofBounds.min.y &&
                              polygonBounds.center.y < roofBounds.max.y;
            bool sideContact = horizontalOverlap && verticalOverlap && !topContact;
            diagnostic = new RoofGeometryDiagnostic(
                sensorBounds,
                polygonBounds,
                roofBounds,
                horizontalOverlap,
                verticalOverlap,
                topContact,
                sideContact,
                insideRoof);
            return true;
        }

        private bool HasBoardHorizontalOverlap(Obstacle roof)
        {
            if (!IsValidRoof(roof))
                return false;

            CollisionUtils.GetObstacleXInterval(
                roof,
                roof.ColliderWidth,
                0f,
                out float roofLeft,
                out float roofRight);
            Bounds boardBounds = _boardContactCollider.bounds;
            return CollisionUtils.IsOverlap(
                boardBounds.min.x,
                boardBounds.max.x,
                roofLeft,
                roofRight);
        }

        private void AlignBoardContactTo(float targetWorldY)
        {
            float deltaY = targetWorldY - _boardContactCollider.bounds.min.y;
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

        private float ResolveRoadTargetWorldY()
        {
            EnsureInitialized();
            Vector3 localTarget = _surfaceTransform.localPosition;
            localTarget.y = _roadLocalY;
            return _surfaceTransform.parent != null
                ? _surfaceTransform.parent.TransformPoint(localTarget).y
                : localTarget.y;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            ValidateReferences();
            SkateboardCollisionSensor sensor =
                GetComponentInChildren<SkateboardCollisionSensor>(includeInactive: true);
            _boardContactCollider = sensor.GetComponent<Collider2D>();
            if (_boardContactCollider == null)
            {
                throw new MissingComponentException(
                    "SkateboardCollisionSensor requires Collider2D board baseline.");
            }

            _roadLocalY = _surfaceTransform.localPosition.y;
            _initialized = true;
        }

        private void EnsureConfigured()
        {
            if (_obstacleSpawner == null)
            {
                throw new InvalidOperationException(
                    "SkateboardSurfaceController is not configured with ObstacleSpawner.");
            }
        }

        private static bool IsValidRoof(Obstacle roof)
        {
            return roof != null
                   && roof.isActiveAndEnabled
                   && roof.ObstacleType != null
                   && CollisionUtils.IsRoofObstacle(
                       roof.ObstacleType.ObstacleTypeEnum);
        }

        private static void ValidateRoof(Obstacle roof)
        {
            if (!IsValidRoof(roof))
            {
                throw new ArgumentException(
                    "Skateboard roof support must be an active roof obstacle.",
                    nameof(roof));
            }
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
                    "SkateboardSurfaceController requires landing Collider2D.");
            }

            if (GetComponentInChildren<SkateboardCollisionSensor>(includeInactive: true) == null)
            {
                throw new MissingReferenceException(
                    "SkateboardSurfaceController requires SkateboardCollisionSensor.");
            }

            if (GetComponentInChildren<SpritePhysicsShapeColliderSync>(includeInactive: true) == null)
            {
                throw new MissingReferenceException(
                    "SkateboardSurfaceController requires SpritePhysicsShapeColliderSync.");
            }
        }
    }
}
