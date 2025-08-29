namespace Assets.Scripts.Common
{
    /// <summary>
    /// Доля ширины хомяка, необходимая для «удачного напрыга».
    /// Если перекрытие ≤ порога — хомяк лишь задевает препятствие и получает урон.
    /// Если перекрытие  > порога — хомяк полностью напрыгивает, разрушает препятствие и получает бонус.
    /// </summary>
    public static class Consts
    {
        public const float JumpOverlapCrushThreshold = 0.5f;
    }
}
