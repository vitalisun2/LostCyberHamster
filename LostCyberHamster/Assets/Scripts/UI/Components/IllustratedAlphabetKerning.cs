using System;

namespace LostCyberHamster.UI
{
    /// <summary>Парный кернинг, рассчитанный по видимым alpha-контурам рисованных глифов.</summary>
    internal static class IllustratedAlphabetKerning
    {
        private const string Latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Cyrillic = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
        private const float MixedAlphabetMargin = -8f;
        private const float OutlineContact = -2f;

        private static readonly byte[] LatinMargins = Convert.FromBase64String(
            "+vr5+fr6+vr6+vr5+vr5+vj6+vP57/P66fr3+vr5+vr6+vr5+vr6+vr6+fr39/n29vf0+Pr69vn6+vb6+vr6+fr69fr0+vr39ff3+vb69/r6+vr6+vr69fr5+vr6+vr6+Pr69ff08vj6+vn5+vr5+vr6+vn6+vn6+Pr6+Pj4+Pr3+vH5+Pr6+vj6+u/69fj6+Pr4+vn6+fr6+fr4+vr5+fr6+fr6+vr5+vr5+vj6+vf49PT69Pn6+vn5+vr5+vr6+vn6+vn6+fr6+fn4+Pr4+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+Pr6+vr6+vr6+Pr6+vr6+vr6+fr6+Pj39vj5+fj6+vr4+vr5+ff5+vf69vr5+vn6+vn5+Pj6+fn6+vr6+vn6+fr6+fr4+vjq+e/z+On4+vr4+fr6+fr6+vr5+vr4+vj6+vf49fX69fr6+vn5+vr5+vr6+vn6+vn6+fr6+fn5+fr5+vj6+vr6+vr6+vf6+fr6+vr6+vj6+vX39PL39Pn5+vr6+fr67/r0+fr6+vr6+fr6+fn49/j6+vr6+vr6+vr6+vn6+vr6+vr6+vr29/rz+vr6+Pn6+vj6+vr6+Pr6+Pr4+vr5+Pj4+vX5+Pr6+fr6+vr6+vr6+vr6+vr6+Pf59/f49/nx+fj6+vr4+vrp+vb4+vn6+fr5+vn6+vn6+Pf6+vr6+vr6+vf6+fr6+vr6+vn6+vn5+Pj47vn0+vr69Pr67fn29/r1+vX69/r4+vr5+vfz+fb6+vr2+vrz+fb3+vf69/r3+vj6+vn69/r69vn6+vb6+vr6+fr69fr0+vr59/n5+vj66/ny+vr68fr64vn29/rz+fT69vr4+vr5+vf6+vr6+vr6+vr6+vr6+vr6+fr6+vn6+vr6+g==");

        private static readonly byte[] CyrillicMargins = Convert.FromBase64String(
            "+vj5+fn4+fn4+Pj5+vr59Pn69O/09Pr58fn57vn6+fn59fn6+fr5+vj4+fn69fn5+vn6+vPy+vf59/n58/n69/n39vn6+fr5+vn5+fn59vn5+vn6+fPx+fj59vn58/n6+fn46/r6+uz6+vb3+vr66/f6+fr6+Pr6+Pj6+vr6+vr69/r2+vn6+fr5+vr6+fn6+vr5+vn6+u30+vr5+Pn57fn6+vn6+vn6+fr4+fr6+fn5+vr5+fn6+fj4+fr5+Pn5+Pn6+vn6+vn6+vr5+vr6+fr6+vr5+vr6+fr3+vr6+fn6+fr6+vn6+Pn6+fr5+fr6+fn5+Pn59/n69/j59vr5+Pn5+fn6+vn69vn6+fr4+fn5+fn59vn5+fn6+fj2+fj5+Pn5+Pn6+Pn4+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr5+vr6+vr4+vr6+vr6+vr6+vn6+Pr6+vn6+Pn5+vf6+Pn69fr69fn68/n6+fr6+vr6+fn5+vj5+fn4+fn5+Pj5+vr59vn69vP09vn58/n58/n6+fn5+vj5+fn4+fr5+Pj5+vr5+Pn6+Pf2+Pr59/n59/n6+vn6+fr6+vr6+vr6+vr6+fn6+vr6+vr6+vr6+vr6+vr6+vr68vn6+fn5+vX1+fr68vj5+vr6+vnz+vX6+fn6+fr68/n1+vn5+fr5+fr6+fn5+vr5+fn6+fn5+fr5+fn5+fn6+vn67vr6+vT5+vT1+vr67vf5+fr6+fr2+ff6+vr6+vr69Pn29/n6+fr4+fr6+fn59/n5+Pn6+PT19/n59Pn59fn6+vn56/r6+uv6+vb3+vr66/b6+Pr6+Pr69/j6+vr6+vr69/r25vr6+uf6+Pb3+vj65vb68vr68vn68fj6+fr6+vr69/rz8vn6+fn5+vT0+fr68vj5+vr6+vjx+vL5+fn6+Pr68vn1+fn5+fr5+fr6+fj5+fn59vn69vn59Pr5+Pn5+fn6+vn6+vn6+fr5+vr6+fn6+vr5+vn6+u70+vr59vn57vn6+vn6+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fn5+fr6+vr6+vn5+vr6+fn6+vr6+vr6+vn6+vr6+vr6+fr5+vn6+fr5+vr6+fn6+vr5+vn6+vL0+vr59/n58vn6+vn69fn6+fr5+vj4+fn69fn5+vn6+vTv+vf59/n58/n69/n3+vn6+fr5+vr6+fn6+vr5+vn6+vn5+vr5+fn5+fn6+vn69fn6+fr5+vj4+fn69Pj5+vn6+vXu+vf5+Pn59fn69/n38/n6+fn5+vb2+fr68/j5+vr6+vnz+vX6+fn6+fr69fn18vn6+fn5+vb1+fr68vj5+vr6+vnz+vT6+fn6+fr69Pn1+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6+vr6");

        public static float GetMargin(char left, char right)
        {
            int leftIndex = Latin.IndexOf(left);
            int rightIndex = Latin.IndexOf(right);
            if (leftIndex >= 0 && rightIndex >= 0)
            {
                return Signed(LatinMargins[leftIndex * Latin.Length + rightIndex]) + OutlineContact;
            }

            leftIndex = Cyrillic.IndexOf(left);
            rightIndex = Cyrillic.IndexOf(right);
            if (leftIndex >= 0 && rightIndex >= 0)
            {
                return Signed(CyrillicMargins[leftIndex * Cyrillic.Length + rightIndex]) + OutlineContact;
            }

            return MixedAlphabetMargin;
        }

        private static float Signed(byte value)
        {
            return unchecked((sbyte)value);
        }
    }
}
