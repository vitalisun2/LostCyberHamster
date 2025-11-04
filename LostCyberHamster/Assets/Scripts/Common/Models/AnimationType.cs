namespace Assets.Scripts.Common.Models
{
    /// <summary>
    /// Тип анимации препятствия
    /// </summary>
    public enum AnimationType
    {
        /// <summary>Нет анимации (статичный спрайт)</summary>
        None,
        
        /// <summary>Idle анимация (статичное препятствие с анимацией)</summary>
        Idle,
        
        /// <summary>Walk анимация (движущееся препятствие)</summary>
        Walk
    }
}
