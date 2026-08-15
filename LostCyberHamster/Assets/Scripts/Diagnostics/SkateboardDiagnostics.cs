using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Assets.Scripts.GameEngine.Actors;
using Assets.Scripts.Gameplay;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Diagnostics
{
    /// <summary>
    /// Пишет узкие event-based факты Skateboard roof flow в stability diagnostic log.
    /// </summary>
    public static class SkateboardDiagnostics
    {
        private const string _tag = "[SKATEBOARD]";
        private static readonly HashSet<RoofContactKey> _roofContactKeys = new();
        private static readonly HashSet<string> _destroyKeys = new();
        private static long _sessionSequence;
        private static string _runId;
        private static long _lastActionId;
        private static bool _lastStartedOnRoof;
        private static string _lastPhase = "Inactive";
        private static bool _modeActive;

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void BeginSession()
        {
            long sequence = Interlocked.Increment(ref _sessionSequence);
            _runId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{sequence}";
            _lastActionId = 0;
            _lastStartedOnRoof = false;
            _lastPhase = "Ride";
            _modeActive = true;
            _roofContactKeys.Clear();
            _destroyKeys.Clear();
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void EndMode()
        {
            _modeActive = false;
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Activate(
            Hamster hamster,
            HamsterActorSwitcher actorSwitcher,
            SkateboardSurfaceController surface)
        {
            if (!HasSession() || hamster == null || surface == null)
                return;

            Obstacle roof = surface.CurrentRoof;
            Bounds roofBounds = GetObstacleBounds(roof);
            Bounds sensorBounds = surface.BoardContactBounds;
            Write(
                "ACTIVATE",
                $"state={hamster.HamsterState.Value} lane={FormatLane(hamster)} " +
                $"actorPos={FormatVector(actorSwitcher.SkateboardActor.transform.position)} " +
                $"roof={FormatObstacle(roof)} roofBounds={FormatBounds(roofBounds)} " +
                $"sensorBounds={FormatBounds(sensorBounds)} baseline={sensorBounds.min.y:F3} " +
                $"roadTargetY={surface.RoadTargetWorldY:F3} " +
                $"roofTargetY={surface.CurrentRoofTop:F3} surfaceY={surface.SurfaceWorldY:F3}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void JumpStart(
            Hamster hamster,
            in SkateboardAttack.JumpCycleSnapshot snapshot,
            bool isSuper,
            Obstacle support,
            string state)
        {
            if (!HasSession())
                return;

            _lastActionId = snapshot.ActionId;
            _lastStartedOnRoof = snapshot.StartedOnRoof;
            _lastPhase = state;
            Write(
                "JUMP_START",
                $"action={snapshot.ActionId} kind={(isSuper ? "Super" : "Normal")} " +
                $"startedOnRoof={snapshot.StartedOnRoof} support={FormatObstacle(support)} " +
                $"predictedSurface={(snapshot.LandingPlan.LandsOnRoof ? "Roof" : "Road")} " +
                $"predictedSupport={FormatObstacle(snapshot.LandingPlan.Support)} " +
                $"predictionTravel={snapshot.LandingPlan.WorldTravel:F3} " +
                $"state={state} shifting={hamster.IsShifting.Value} lane={FormatLane(hamster)}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void RoofContact(
            Hamster hamster,
            SkateboardAttack attack,
            SkateboardSurfaceController surface,
            Obstacle roof,
            string source,
            SkateboardInteractionPolicy.Phase policyPhase,
            SkateboardInteractionPolicy.Outcome outcome,
            bool isRideSupport)
        {
            if (!HasSession() ||
                roof == null ||
                !surface.TryCaptureRoofGeometry(
                    roof,
                    out SkateboardSurfaceController.RoofGeometryDiagnostic geometry))
            {
                return;
            }

            long actionId = 0;
            bool startedOnRoof = false;
            if (attack != null &&
                attack.TryGetCurrentJumpSnapshot(
                    out SkateboardAttack.JumpCycleSnapshot snapshot))
            {
                actionId = snapshot.ActionId;
                startedOnRoof = snapshot.StartedOnRoof;
            }

            bool currentMatch = ReferenceEquals(roof, surface.CurrentRoof);
            string phase = ResolvePhase(attack);
            var key = new RoofContactKey(
                actionId,
                roof.GetInstanceID(),
                source,
                phase,
                geometry.TopContact,
                geometry.SideContact,
                geometry.InsideRoof,
                isRideSupport,
                currentMatch,
                outcome);
            if (!_roofContactKeys.Add(key))
                return;

            string reason = PolicyReason(
                policyPhase,
                outcome,
                startedOnRoof,
                isRideSupport,
                currentMatch);
            Bounds hamsterBounds = geometry.PolygonBounds;
            hamsterBounds.Encapsulate(geometry.SensorBounds);
            Write(
                "ROOF_CONTACT",
                $"action={actionId} startedOnRoof={startedOnRoof} source={source} " +
                $"phase={phase} roof={FormatObstacle(roof)} " +
                $"hamsterBounds={FormatBounds(hamsterBounds)} " +
                $"sensorBounds={FormatBounds(geometry.SensorBounds)} " +
                $"polygonBounds={FormatBounds(geometry.PolygonBounds)} " +
                $"roofBounds={FormatBounds(geometry.RoofBounds)} " +
                $"roofTop={geometry.RoofBounds.max.y:F3} " +
                $"horizontal={geometry.HorizontalOverlap} vertical={geometry.VerticalOverlap} " +
                $"top={geometry.TopContact} side={geometry.SideContact} " +
                $"inside={geometry.InsideRoof} currentMatch={currentMatch} " +
                $"outcome={outcome} reason={reason}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void SurfaceTransition(
            SkateboardSurfaceController.SurfaceState previous,
            SkateboardSurfaceController.SurfaceState next,
            Obstacle support,
            string reason,
            float previousY,
            float nextY,
            Obstacle previousSupport = null)
        {
            if (!HasSession())
                return;

            Write(
                "SURFACE_TRANSITION",
                $"from={FormatSurface(previous)} to={FormatSurface(next)} " +
                $"support={FormatObstacle(support)} previousSupport={FormatObstacle(previousSupport)} " +
                $"reason={reason} y={previousY:F3}->{nextY:F3}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LandingContact(
            in SkateboardAttack.JumpCycleSnapshot snapshot,
            float elapsed,
            Obstacle currentSupport,
            SkateboardSurfaceController surface)
        {
            if (!HasSession())
                return;

            _lastActionId = snapshot.ActionId;
            _lastStartedOnRoof = snapshot.StartedOnRoof;
            _lastPhase = "Landing";
            Write(
                "LANDING_CONTACT",
                $"action={snapshot.ActionId} elapsed={elapsed:F3} " +
                $"startedOnRoof={snapshot.StartedOnRoof} " +
                $"predictedSurface={(snapshot.LandingPlan.LandsOnRoof ? "Roof" : "Road")} " +
                $"predictedSupport={FormatObstacle(snapshot.LandingPlan.Support)} " +
                $"predictionTravel={snapshot.LandingPlan.WorldTravel:F3} " +
                $"currentSupport={FormatObstacle(currentSupport)} " +
                $"surface={FormatSurface(surface.State)} y={surface.SurfaceWorldY:F3}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LandingResolve(
            in SkateboardAttack.JumpCycleSnapshot snapshot,
            float elapsed,
            SkateboardSurfaceController.LandingSurfaceResult result,
            SkateboardSurfaceController surface)
        {
            if (!HasSession())
                return;

            Write(
                "LANDING_RESOLVE",
                $"action={snapshot.ActionId} elapsed={elapsed:F3} " +
                $"success={result.Support != null} " +
                $"plannedSupport={FormatObstacle(snapshot.LandingPlan.Support)} " +
                $"appliedSupport={FormatObstacle(result.Support)} " +
                $"missedRoof={FormatObstacle(result.MissedRoof)} " +
                $"finalSurface={FormatSurface(surface.State)} " +
                $"finalSupport={FormatObstacle(surface.CurrentRoof)} " +
                $"finalY={surface.SurfaceWorldY:F3}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DestroyRequest(
            Obstacle obstacle,
            long actionId,
            string phase,
            bool startedOnRoof,
            string path,
            string reason)
        {
            if (!HasSession() || obstacle == null)
                return;

            string key = $"{actionId}|{obstacle.GetInstanceID()}|{path}|{reason}";
            if (!_destroyKeys.Add(key))
                return;

            Write(
                "DESTROY_REQUEST",
                $"action={actionId} phase={phase} startedOnRoof={startedOnRoof} " +
                $"obstacle={FormatObstacle(obstacle)} path={path} reason={reason}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DestroyRequest(
            SkateboardAttack attack,
            Obstacle obstacle,
            string path,
            SkateboardInteractionPolicy.Phase policyPhase,
            SkateboardInteractionPolicy.Outcome outcome,
            bool isRideSupport = false)
        {
            long actionId = 0;
            bool startedOnRoof = false;
            if (attack != null &&
                attack.TryGetCurrentJumpSnapshot(
                    out SkateboardAttack.JumpCycleSnapshot snapshot))
            {
                actionId = snapshot.ActionId;
                startedOnRoof = snapshot.StartedOnRoof;
            }

            DestroyRequest(
                obstacle,
                actionId,
                ResolvePhase(attack),
                startedOnRoof,
                path,
                PolicyReason(
                    policyPhase,
                    outcome,
                    startedOnRoof,
                    isRideSupport,
                    isCurrentSupport: false));
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DestroyRequestFromChannel(
            Obstacle obstacle,
            string path)
        {
            if (!_modeActive)
                return;

            DestroyRequest(
                obstacle,
                _lastActionId,
                _lastPhase,
                _lastStartedOnRoof,
                path,
                "event_channel");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void DestroyRequestFromExecution(
            Obstacle obstacle,
            string path)
        {
            DestroyRequest(
                obstacle,
                _lastActionId,
                _lastPhase,
                _lastStartedOnRoof,
                path,
                "execution_path");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Damage(
            Hamster hamster,
            Obstacle obstacle,
            string reason)
        {
            if (!HasSession() || !_modeActive)
                return;

            hamster.TryGetActiveSkateboardAttack(out SkateboardAttack attack);
            SkateboardSurfaceController surface = hamster.SkateboardSurfaceController;
            _lastPhase = ResolvePhase(attack);

            Write(
                "DAMAGE",
                $"action={_lastActionId} obstacle={FormatObstacle(obstacle)} " +
                $"phase={_lastPhase} surface={FormatSurface(surface.State)} " +
                $"reason={reason} " +
                $"state={hamster.HamsterState.Value} lives={hamster.Lives.Value}");
        }

        /// <summary>
        /// Форматирует причину policy outcome без изменения gameplay state.
        /// </summary>
        public static string PolicyReason(
            SkateboardInteractionPolicy.Phase phase,
            SkateboardInteractionPolicy.Outcome outcome,
            bool startedOnRoof,
            bool isRideSupport,
            bool isCurrentSupport)
        {
            if (outcome == SkateboardInteractionPolicy.Outcome.PreserveSupport)
            {
                if (isCurrentSupport)
                    return "current_support";
                if (phase == SkateboardInteractionPolicy.Phase.Ride && isRideSupport)
                    return "passable_roof_chain";
                return startedOnRoof
                    ? "started_on_roof_roof_preserve"
                    : "policy_preserve";
            }

            if (outcome == SkateboardInteractionPolicy.Outcome.Destroy)
                return startedOnRoof
                    ? "started_on_roof_physical_destroy"
                    : "started_on_road_physical_destroy";
            if (outcome == SkateboardInteractionPolicy.Outcome.Damage)
                return "ride_physical_damage";
            if (outcome == SkateboardInteractionPolicy.Outcome.Collect)
                return "collectable";
            if (outcome == SkateboardInteractionPolicy.Outcome.BumpOnly)
                return "landing_wave_bump_only";
            return "non_physical_ignore";
        }

        private static bool HasSession()
        {
            return !string.IsNullOrEmpty(_runId);
        }

        private static string FormatSurface(
            SkateboardSurfaceController.SurfaceState state)
        {
            return state == SkateboardSurfaceController.SurfaceState.DroppingToRoad
                ? "Air"
                : state.ToString();
        }

        private static string ResolvePhase(SkateboardAttack attack)
        {
            if (attack == null)
                return _lastPhase;
            if (attack.IsLanding)
                return "Landing";
            if (attack.IsRiding)
                return "Ride";
            if (attack.IsSuperJumping)
                return "SuperJump";
            return attack.IsJumping ? "Jump" : "Inactive";
        }

        private static string FormatLane(Hamster hamster)
        {
            return hamster.IsOnBottomLine.Value ? "Bottom" : "Top";
        }

        private static string FormatObstacle(Obstacle obstacle)
        {
            if (obstacle == null)
                return "null";

            string type = obstacle.ObstacleType != null
                ? obstacle.ObstacleType.ObstacleTypeEnum.ToString()
                : "missing";
            return $"{obstacle.ObstacleId ?? obstacle.name}#{obstacle.GetInstanceID()}:{type}";
        }

        private static Bounds GetObstacleBounds(Obstacle obstacle)
        {
            BoxCollider2D collider = obstacle != null
                ? obstacle.GetComponentInChildren<BoxCollider2D>()
                : null;
            return collider != null ? collider.bounds : default;
        }

        private static string FormatBounds(Bounds bounds)
        {
            return $"min{FormatVector(bounds.min)}/max{FormatVector(bounds.max)}";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F3},{value.y:F3},{value.z:F3})";
        }

        private static void Write(string eventName, string fields)
        {
            DebugManager.DiagStability(
                $"{_tag} run={_runId} event={eventName} {fields}");
        }

        private readonly struct RoofContactKey : IEquatable<RoofContactKey>
        {
            private readonly long _actionId;
            private readonly int _obstacleId;
            private readonly string _source;
            private readonly string _phase;
            private readonly bool _top;
            private readonly bool _side;
            private readonly bool _inside;
            private readonly bool _rideSupport;
            private readonly bool _currentSupport;
            private readonly SkateboardInteractionPolicy.Outcome _outcome;

            public RoofContactKey(
                long actionId,
                int obstacleId,
                string source,
                string phase,
                bool top,
                bool side,
                bool inside,
                bool rideSupport,
                bool currentSupport,
                SkateboardInteractionPolicy.Outcome outcome)
            {
                _actionId = actionId;
                _obstacleId = obstacleId;
                _source = source;
                _phase = phase;
                _top = top;
                _side = side;
                _inside = inside;
                _rideSupport = rideSupport;
                _currentSupport = currentSupport;
                _outcome = outcome;
            }

            public bool Equals(RoofContactKey other)
            {
                return _actionId == other._actionId &&
                       _obstacleId == other._obstacleId &&
                       _source == other._source &&
                       _phase == other._phase &&
                       _top == other._top &&
                       _side == other._side &&
                       _inside == other._inside &&
                       _rideSupport == other._rideSupport &&
                       _currentSupport == other._currentSupport &&
                       _outcome == other._outcome;
            }

            public override bool Equals(object obj)
            {
                return obj is RoofContactKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _actionId.GetHashCode();
                    hash = (hash * 397) ^ _obstacleId;
                    hash = (hash * 397) ^ (_source?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ (_phase?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ _top.GetHashCode();
                    hash = (hash * 397) ^ _side.GetHashCode();
                    hash = (hash * 397) ^ _inside.GetHashCode();
                    hash = (hash * 397) ^ _rideSupport.GetHashCode();
                    hash = (hash * 397) ^ _currentSupport.GetHashCode();
                    hash = (hash * 397) ^ (int)_outcome;
                    return hash;
                }
            }
        }
    }
}
