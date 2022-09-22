namespace VTT.Network
{
    public enum DisconnectReason
    {
        Timeout,
        InternalServerError,
        InternalClientError,
        NetworkStateCorrupted,
        ProtocolMismatch,
        Banned,
        AlreadyConnected,
        ManualDisconnect
    }
}
