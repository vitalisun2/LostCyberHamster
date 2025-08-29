using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay.Enums;
using Atomic.Elements;
using UnityEngine;

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
            _animator.enabled = true;
        }

        public void OnFinish()
        {
            _animator.enabled = false;
        }
    }
}
