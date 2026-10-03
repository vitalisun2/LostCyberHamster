namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Ссылка NSKeyedArchiver на объект архива.</summary>
    internal readonly struct ProcreateArchiveUid
    {
        internal int Index { get; }

        /// <summary>Сохраняет индекс объекта.</summary>
        internal ProcreateArchiveUid(int index)
        {
            Index = index;
        }
    }
}
