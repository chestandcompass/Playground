namespace SR2MP.Shared.Utils;

// Fixed order: Tarr, feral slimes, damage multiplier. Personal options are absent.
internal sealed class WorldRuleState
{
    internal const int Count = 3;
    internal readonly uint Revision;
    internal readonly string[] Values;
    internal WorldRuleState(uint revision, string[] values)
    {
        if (revision == 0 || values.Length != Count || values.Any(v => string.IsNullOrEmpty(v)
            || v.Length > 128 || v.Any(char.IsControl)))
            throw new InvalidDataException("Invalid host world rules.");
        Revision = revision;
        Values = (string[])values.Clone();
    }
    internal bool SameValues(WorldRuleState other) => Values.SequenceEqual(other.Values);
    internal bool IsNewerThan(WorldRuleState? other) => other == null || Revision > other.Revision;
}
