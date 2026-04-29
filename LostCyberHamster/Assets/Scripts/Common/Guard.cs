using System;

namespace Assets.Scripts.Common
{
    public static class Guard
    {
        public static void ThrowIfNull(params (object value, string name)[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].value == null)
                    throw new ArgumentNullException(args[i].name);
            }
        }
    }
}
