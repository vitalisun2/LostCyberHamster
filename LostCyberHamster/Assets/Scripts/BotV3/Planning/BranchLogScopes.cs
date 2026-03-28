namespace Assets.Scripts.BotV3
{
    internal static class BranchLogScopes
    {
        public const string Root = "root";

        private const string ProjectionPrefix = "projection:d";

        public static string ProjectionAtDepth(int depth)
        {
            return ProjectionPrefix + depth;
        }
    }
}
