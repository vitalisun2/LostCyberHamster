namespace GameManagement.CloudSave
{
    public enum ExistingAccountRestoreResult
    {
        Restored,
        SignInFailed,
        SnapshotMissing,
        OwnerMismatch,
        SnapshotRejected,
        LoadFailed,
        ApplyFailed
    }
}
