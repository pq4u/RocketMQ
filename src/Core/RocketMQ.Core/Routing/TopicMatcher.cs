namespace RocketMQ.Core.Routing;

/// <summary>
/// Provides AMQP-style topic pattern matching against dot-separated routing keys.
/// Patterns may contain '<c>*</c>' (matches exactly one word) and
/// '<c>#</c>' (matches zero or more words).
/// </summary>
public static class TopicMatcher
{
    /// <summary>
    /// Determines whether the specified <paramref name="pattern"/> matches the given
    /// <paramref name="routingKey"/>.
    /// </summary>
    /// <param name="pattern">
    /// A dot-separated pattern that may include '<c>*</c>' and '<c>#</c>' wildcards.
    /// </param>
    /// <param name="routingKey">
    /// A dot-separated routing key to test against the pattern (e.g. "orders.eu.created").
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="routingKey"/> matches
    /// <paramref name="pattern"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool Matches(string pattern, string routingKey)
    {
        var patternParts = pattern.Split('.');
        var keyParts = routingKey.Split('.');
        return MatchRecursive(patternParts, 0, keyParts, 0);
    }

    private static bool MatchRecursive(
        string[] pattern, int pi,
        string[] key, int ki)
    {
        if (pi == pattern.Length && ki == key.Length)
            return true;

        if (pi == pattern.Length)
            return false;

        if (pattern[pi] == "#")
        {
            for (var skip = 0; skip <= key.Length - ki; skip++)
            {
                if (MatchRecursive(pattern, pi + 1, key, ki + skip))
                    return true;
            }
            return false;
        }

        if (ki == key.Length)
            return false;

        if (pattern[pi] == "*")
            return MatchRecursive(pattern, pi + 1, key, ki + 1);

        if (pattern[pi] == key[ki])
            return MatchRecursive(pattern, pi + 1, key, ki + 1);

        return false;
    }
}
