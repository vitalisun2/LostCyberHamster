using Assets.Scripts.GameManagerLogic;
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
        public Animator Animator => _animator;

        private Animator _animator;
        private AnimatorOverrideController _overrideController;
        private Dictionary<string, AnimationClip> _originalRoofClips;
        private Dictionary<string, AnimationClip> _mediumRoofClips;
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
            const string basePath = "Assets/Animations/Hamster/";
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
            _animator.enabled = false;
        }

        public void OnStart()
        {
            // Rebind сбрасывает stale trigger/state, накопленный во время загрузки
            // (критично для тестового уровня без intro, где animator никогда не выключался)
            var wasEnabled = _animator.enabled;
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            DebugManager.DiagLog($"[TransformAnimatorController] OnStart: wasEnabled={wasEnabled} animState={stateInfo.shortNameHash} normalizedTime={stateInfo.normalizedTime:F2}. Calling Rebind().");
            _animator.Rebind();
            _animator.enabled = true;
        }

        public void OnFinish()
        {
            _animator.enabled = false;
        }
    }
}
