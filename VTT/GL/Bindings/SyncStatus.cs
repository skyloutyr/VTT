namespace VTT.GL.Bindings
{
    public enum SyncStatus
    {
        Signaled = 0x9119,
        Unsignaled = 0x9118,
        AlreadySignaled = 0x911A,
        TimeoutExpired = 0x911B,
        ConditionSatisfied = 0x911C,
        WaitFailed = 0x911D
    }
}