namespace SR2MP.Shared.Utils;

internal enum SessionState { Singleplayer, HostingAlone, HostingTogether, Joining, Connected, Disconnected }
internal static class SessionPolicy
{
    internal static SessionState Evaluate(string role, bool hosting, int peers, bool expected, bool transport, bool joined, double silenceSeconds)
    {
        if (role == "host") return hosting ? peers > 0 ? SessionState.HostingTogether : SessionState.HostingAlone : SessionState.Disconnected;
        if (role != "guest") return SessionState.Singleplayer;
        if (!expected) return SessionState.Joining;
        if (!transport || (joined && silenceSeconds >= 10)) return SessionState.Disconnected;
        return joined ? SessionState.Connected : SessionState.Joining;
    }
}
