using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;
using Atomic.Elements;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Scripts.GameEngine.Controllers
{
    public class TransformAnimatorController : MonoBehaviour,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener,
        Listeners.IGameFinishListener,
        Listeners.IGameIntroListener,
        Listeners.IGameStartListener
    {
        private static readonly int _roofRunStateHash =
            Animator.StringToHash("Base Layer.transform_roof_run");

        public Animator Animator => _animator;

        private Animator _animator;
        private AnimatorOverrideController _overrideController;
        private Dictionary<string, AnimationClip> _originalRoofClips;
        private Dictionary<string, AnimationClip> _mediumRoofClips;
        private RoofHeightTransitionCompensator _roofHeightTransitionCompensator;
        private bool _hasActiveRoofHeightTransition;
        private ObstacleTypeEnum _activeRoofHeightTransitionTargetType;
        private bool _isMediumActive;

        /// <summary>
        /// Mapping: big clip name → medium clip name.
        /// Все roof-клипы, которые нужно свопать при переходе на medium.
        /// </summary>
        private static readonly Dictionary<string, string> RoofClipMapping = new()
        {
            { "transform_jump_on_roof",                     "transform_medium_jump_on_roof" },
            { "transform_roof_run",                         "transform_medium_roof_run" },
            { "transform_roof_jump",                        "transform_medium_roof_jump" },
            { "transform_jump_from_roof",                   "transform_medium_jump_from_roof" },
            { "transform_run_from_roof",                    "transform_medium_run_from_roof" },
            { "transform_jump_on_from_roof",                "transform_medium_jump_on_from_roof" },
            { "transform_super_jump_on_roof",               "transform_medium_super_jump_on_roof" },
            { "transform_super_roof_jump",                  "transform_medium_super_roof_jump" },
            { "transform_super_jump_from_roof",             "transform_medium_super_jump_from_roof" },
            { "transform_super_jump_on_obstacle_from_roof", "transform_medium_super_jump_on_obstacle_from_roof" }
        };

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _roofHeightTransitionCompensator = new RoofHeightTransitionCompensator(transform);
        }

        private void LateUpdate()
        {
            if (!_roofHeightTransitionCompensator.IsActive)
                return;

            if (!_animator.enabled)
                return;

            _roofHeightTransitionCompensator.ApplyFrame(Time.deltaTime);
            if (!_roofHeightTransitionCompensator.IsActive)
                ClearActiveRoofHeightTransition();
        }

        public void SetRunAnimationTrigger(AtomicVariable<HamsterStateEnum> hamsterState)
        {
            switch (hamsterState.Value)
            {
                //IsRunFromRoof
                case HamsterStateEnum.RunFromRoof:
                    _animator.SetTrigger("IsRunFromRoof");
                    break;
            }
        }

        public void SetRoofJumpAnimationTrigger(AtomicVariable<HamsterStateEnum> hamsterState)
        {
            switch (hamsterState.Value)
            {
                //IsRoofJump
                case HamsterStateEnum.RoofJump:
                    _animator.SetTrigger("IsRoofJump");
                    break;
                case HamsterStateEnum.RoofJumpDamage:
                    _animator.SetTrigger("IsRoofJump");
                    break;

                //IsJumpOnObstacleFromRoof
                case HamsterStateEnum.JumpOnObstacleFromRoof:
                    _animator.SetTrigger("IsJumpOnObstacleFromRoof");
                    break;

                //IsJumpFromRoof
                case HamsterStateEnum.JumpFromRoof:
                    _animator.SetTrigger("IsJumpFromRoof");
                    break;
                case HamsterStateEnum.JumpFromRoofDamage:
                    _animator.SetTrigger("IsJumpFromRoof");
                    break;
            }
        }

        public void SetJumpAnimationTrigger(AtomicVariable<HamsterStateEnum> hamsterState)
        {
            switch (hamsterState.Value)
            {
                // IsJumpOn
                case HamsterStateEnum.JumpOnObstacle:
                    _animator.SetTrigger("IsJumpOn");
                    break;

                // IsJumpOnRoof
                case HamsterStateEnum.RoofRun:
                case HamsterStateEnum.JumpOnRoof:
                case HamsterStateEnum.JumpOnRoofDamage:
                    _animator.SetTrigger("IsJumpOnRoof");
                    break;

                // IsJump
                case HamsterStateEnum.Jump:
                case HamsterStateEnum.JumpOver:
                case HamsterStateEnum.JumpDamageForBigAlive:
                case HamsterStateEnum.JumpDamageForSmallAlive:
                case HamsterStateEnum.JumpDamageForSmallNotAlive:
                    _animator.SetTrigger("IsJump");
                    break;
            }
        }

        public void SetSuperJumpAnimationTrigger(AtomicVariable<HamsterStateEnum> hamsterState)
        {
            switch (hamsterState.Value)
            {
                // IsSuperJump
                case HamsterStateEnum.SuperJump:
                case HamsterStateEnum.SuperJumpDamage:
                case HamsterStateEnum.SuperJumpOver:
                    _animator.SetTrigger("IsSuperJump");
                    break;

                // IsSuperJumpOn
                case HamsterStateEnum.SuperJumpOnObstacle:
                    _animator.SetTrigger("IsSuperJumpOn");
                    break;

                // IsSuperJumpOnRoof
                case HamsterStateEnum.SuperJumpOnRoof:
                case HamsterStateEnum.SuperJumpOnRoofDamage:
                    _animator.SetTrigger("IsSuperJumpOnRoof");
                    break;
            }
        }

        public void SetSuperRoofJumpAnimationTrigger(AtomicVariable<HamsterStateEnum> hamsterState)
        {
            switch (hamsterState.Value)
            {
                // IsSuperRoofJump
                case HamsterStateEnum.SuperRoofJump:
                case HamsterStateEnum.SuperRoofJumpDamage:
                    _animator.SetTrigger("IsSuperRoofJump");
                    break;

                // IsSuperJumpFromRoof
                case HamsterStateEnum.SuperJumpFromRoof:
                case HamsterStateEnum.SuperJumpFromRoofDamage:
                    _animator.SetTrigger("IsSuperJumpFromRoof");
                    break;

                // IsSuperJumpOnObstacleFromRoof
                case HamsterStateEnum.SuperJumpOnObstacleFromRoof:
                    _animator.SetTrigger("IsSuperJumpOnObstacleFromRoof");
                    break;
            }
        }

        /// <summary>
        /// Возвращает количество кадров анимационного клипа по его имени.
        /// Если клип не найден, возвращается 0.
        /// </summary>
        public int GetClipFrameCount(string clipName)
        {
            // проверяем, что аниматор успел инициализироваться, так как метод может вызываться в конктрукторах других классов
            if (_animator == null) _animator = GetComponent<Animator>();

            // Проверяем, что у аниматора есть контроллер
            var controller = _animator.runtimeAnimatorController;
            if (controller == null)
            {
                Debug.LogWarning("Animator не имеет runtimeAnimatorController!");
                return 0;
            }

            // Берём все клипы, привязанные к контроллеру
            var clips = controller.animationClips;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"В контроллере {controller.name} нет анимационных клипов.");
                return 0;
            }

            // Ищем клип по названию
            foreach (var clip in clips)
            {
                if (clip.name == clipName)
                {
                    // Количество кадров = frameRate (кадров в секунду) * length (секунд)
                    float framesF = clip.frameRate * clip.length;
                    // Округлим до ближайшего целого (или просто (int) если хочется усечь)
                    int framesCount = Mathf.RoundToInt(framesF);
                    return framesCount;
                }
            }

            // Если не нашли
            Debug.LogWarning($"Клип '{clipName}' не найден в {controller.name}.");
            return 0;
        }

        /// <summary>
        /// Ищет animation clip по имени среди runtime-клипов и editor-only medium roof assets.
        /// </summary>
        public bool TryFindClip(string clipName, out AnimationClip clip)
        {
            // Проверяет animator.
            if (_animator == null) _animator = GetComponent<Animator>();
            clip = null;

            RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
            if (controller != null)
            {
                // Ищет клип среди runtime clips.
                AnimationClip[] clips = controller.animationClips;
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    AnimationClip candidate = clips[clipIndex];
                    if (candidate != null && candidate.name == clipName)
                    {
                        clip = candidate;
                        return true;
                    }
                }
            }

#if UNITY_EDITOR
            // Ищет editor-only medium roof clip asset.
            const string basePath = "Assets/Animations/Hamster/normal_mode/";
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{basePath}{clipName}.anim");
            return clip != null;
#else
            return false;
#endif
        }

        /// <summary>
        /// Переключает roof-клипы аниматора между big (оригинальные) и medium версиями.
        /// Используется механиками перед запуском roof-анимации в зависимости от типа препятствия.
        /// </summary>
        /// <param name="isMedium">true — подставить medium-клипы, false — вернуть оригинальные big-клипы</param>
        public void SwapRoofClips(bool isMedium)
        {
            if (_isMediumActive == isMedium) return; // уже в нужном состоянии

            EnsureOverrideControllerInitialized();

            if (isMedium)
            {
                foreach (var kvp in RoofClipMapping)
                {
                    if (_mediumRoofClips.TryGetValue(kvp.Value, out var mediumClip))
                        _overrideController[kvp.Key] = mediumClip;
                }
            }
            else
            {
                foreach (var kvp in _originalRoofClips)
                    _overrideController[kvp.Key] = kvp.Value;
            }

            _isMediumActive = isMedium;
        }

        /// <summary>
        /// Переключает roof-клипы для Big↔Medium roof-to-roof прыжка и запускает компенсацию разницы высоты.
        /// </summary>
        public bool TrySwapRoofClipsWithHeightTransition(
            ObstacleTypeEnum sourceRoofType,
            ObstacleTypeEnum targetRoofType,
            string actionClipName)
        {
            // Начинает новый переход и сбрасывает предыдущий контекст.
            ClearActiveRoofHeightTransition();

            // Определяет длительность компенсации по action-клипу целевой крыши.
            if (!TryGetHalfRoofTransitionDuration(actionClipName, targetRoofType, out float duration))
                return false;

            // Запускает компенсацию только для Big↔Medium переходов.
            if (!_roofHeightTransitionCompensator.TryStart(sourceRoofType, targetRoofType, duration))
                return false;

            // Компенсация применится в LateUpdate поверх target-клипов.
            RegisterActiveRoofHeightTransition(targetRoofType);
            SwapRoofClips(targetRoofType == ObstacleTypeEnum.mediumNotAlive);
            return true;
        }

        /// <summary>
        /// Перебазирует активную Big↔Medium компенсацию при upgrade обычного roof jump в super roof jump.
        /// </summary>
        public bool TryUpgradeActiveRoofHeightTransition(string actionClipName)
        {
            // Проверяет, что первый roof jump уже зарегистрировал Big↔Medium переход.
            if (!_hasActiveRoofHeightTransition)
                return false;

            if (!_roofHeightTransitionCompensator.IsActive)
            {
                ClearActiveRoofHeightTransition();
                return false;
            }

            // Определяет длительность новой компенсации по super-клипу целевой крыши.
            if (!TryGetHalfRoofTransitionDuration(
                    actionClipName,
                    _activeRoofHeightTransitionTargetType,
                    out float duration))
            {
                ClearActiveRoofHeightTransition();
                return false;
            }

            // Перебазирует текущую визуальную позицию на raw-позу Animator-а после super transition.
            if (!_roofHeightTransitionCompensator.TryRebaseToNextAnimatorPose(duration))
            {
                ClearActiveRoofHeightTransition();
                return false;
            }

            // Подтверждает, что во время super-клипа остаются активны клипы целевой крыши.
            SwapRoofClips(_activeRoofHeightTransitionTargetType == ObstacleTypeEnum.mediumNotAlive);
            return true;
        }

        /// <summary>
        /// Перебазирует текущую визуальную Y-позицию при смене roof action-клипа без активного Big↔Medium перехода.
        /// </summary>
        public bool TryRebaseRoofActionTransition(
            ObstacleTypeEnum targetRoofType,
            string actionClipName)
        {
            // Сбрасывает stale-контекст Big↔Medium перехода перед независимой перебазировкой.
            ClearActiveRoofHeightTransition();

            // Определяет длительность компенсации по action-клипу целевой крыши.
            if (!TryGetHalfRoofTransitionDuration(actionClipName, targetRoofType, out float duration))
                return false;

            // Сохраняет текущую визуальную высоту до применения нового action-клипа Animator-ом.
            if (!_roofHeightTransitionCompensator.TryRebaseToNextAnimatorPose(duration))
                return false;

            // Подтверждает, что новый action использует клипы целевой высоты крыши.
            SwapRoofClips(targetRoofType == ObstacleTypeEnum.mediumNotAlive);
            return true;
        }

        /// <summary>
        /// Запоминает параметры активного Big↔Medium перехода между крышами.
        /// </summary>
        private void RegisterActiveRoofHeightTransition(ObstacleTypeEnum targetRoofType)
        {
            _activeRoofHeightTransitionTargetType = targetRoofType;
            _hasActiveRoofHeightTransition = true;
        }

        /// <summary>
        /// Сбрасывает параметры активного Big↔Medium перехода между крышами.
        /// </summary>
        private void ClearActiveRoofHeightTransition()
        {
            _hasActiveRoofHeightTransition = false;
            _activeRoofHeightTransitionTargetType = default;
        }

        /// <summary>
        /// Возвращает половину длительности action-клипа для целевой высоты крыши.
        /// </summary>
        private bool TryGetHalfRoofTransitionDuration(
            string actionClipName,
            ObstacleTypeEnum targetRoofType,
            out float duration)
        {
            // Выбирает версию action-клипа для целевой крыши.
            string targetClipName = GetRoofClipName(actionClipName, targetRoofType);

            // Ищет целевой animation clip.
            if (!TryFindClip(targetClipName, out AnimationClip clip) || clip == null)
            {
                duration = 0f;
                return false;
            }

            // Возвращает половину длительности клипа.
            duration = clip.length * 0.5f;
            return duration > 0f;
        }

        /// <summary>
        /// Возвращает имя roof-клипа для указанной высоты крыши.
        /// </summary>
        private static string GetRoofClipName(string clipName, ObstacleTypeEnum roofType)
        {
            // Подменяет big-клип на medium-версию.
            if (roofType == ObstacleTypeEnum.mediumNotAlive &&
                RoofClipMapping.TryGetValue(clipName, out string mediumClipName))
            {
                return mediumClipName;
            }

            // Оставляет исходное имя для большой крыши.
            return clipName;
        }

        private void EnsureOverrideControllerInitialized()
        {
            if (_overrideController != null) return;

            _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
            _animator.runtimeAnimatorController = _overrideController;

            // Кешируем оригинальные roof-клипы
            _originalRoofClips = new Dictionary<string, AnimationClip>();
            foreach (var clip in _overrideController.animationClips)
            {
                if (RoofClipMapping.ContainsKey(clip.name))
                    _originalRoofClips[clip.name] = clip;
            }

            // Загружаем medium-клипы
            _mediumRoofClips = new Dictionary<string, AnimationClip>();
            LoadMediumRoofClips();
        }

        private void LoadMediumRoofClips()
        {
#if UNITY_EDITOR
            const string basePath = "Assets/Animations/Hamster/normal_mode/";
            foreach (var kvp in RoofClipMapping)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{basePath}{kvp.Value}.anim");
                if (clip != null)
                    _mediumRoofClips[kvp.Value] = clip;
                else
                    Debug.LogWarning($"[TransformAnimatorController] Medium roof clip not found: {basePath}{kvp.Value}.anim");
            }
#else
            // TODO: Загрузка через Addressables для production-билдов
            Debug.LogWarning("[TransformAnimatorController] Medium roof clips are not available in builds yet. Need Addressables integration.");
#endif
        }

        public void OnPause()
        {
            _animator.enabled = false;
        }

        public void OnResume()
        {
            _animator.enabled = true;
        }

        public void OnIntro()
        {
            _roofHeightTransitionCompensator.Reset();
            ClearActiveRoofHeightTransition();
            _animator.enabled = false;
        }

        public void OnStart()
        {
            // Rebind сбрасывает stale trigger/state, накопленный во время загрузки
            // (критично для тестового уровня без intro, где animator никогда не выключался)
            _animator.Rebind();
            _roofHeightTransitionCompensator.Reset();
            ClearActiveRoofHeightTransition();
            _animator.enabled = true;
        }

        /// <summary>
        /// Возвращает normal actor в дорожную default pose после skateboard roof mode.
        /// </summary>
        public void ResetToRunSurface()
        {
            _roofHeightTransitionCompensator.Reset();
            ClearActiveRoofHeightTransition();
            SwapRoofClips(isMedium: false);
            _animator.Rebind();
        }

        /// <summary>
        /// Восстанавливает normal actor непосредственно в устойчивую позу текущей крыши.
        /// </summary>
        public void RestoreRoofRunSurface(bool isMediumRoof)
        {
            _roofHeightTransitionCompensator.Reset();
            ClearActiveRoofHeightTransition();
            SwapRoofClips(isMediumRoof);
            _animator.Rebind();
            _animator.Play(_roofRunStateHash, layer: 0, normalizedTime: 0f);
            _animator.Update(0f);
        }

        public void OnFinish()
        {
            _roofHeightTransitionCompensator.Reset();
            ClearActiveRoofHeightTransition();
            _animator.enabled = false;
        }
    }
}
