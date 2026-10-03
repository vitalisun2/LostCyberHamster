using System.Collections.Generic;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Группа или растровый слой исходного Procreate.</summary>
    public sealed class ProcreateNode
    {
        public string Id { get; }
        public string Name { get; }
        public bool IsGroup { get; }
        public bool IsHidden { get; }
        public IReadOnlyList<ProcreateNode> Children { get; }
        internal string UnsupportedReason { get; }
        internal double Opacity { get; }

        /// <summary>Сохраняет исходные имена, вложенность и условия декодирования.</summary>
        internal ProcreateNode(string id, string name, bool isGroup, bool isHidden,
            IReadOnlyList<ProcreateNode> children, double opacity, string unsupportedReason)
        {
            Id = id;
            Name = name;
            IsGroup = isGroup;
            IsHidden = isHidden;
            Children = children;
            Opacity = opacity;
            UnsupportedReason = unsupportedReason;
        }
    }
}
