namespace Scynapse.Security;

/// <summary>
/// NATS-style dot-separated subject matching with wildcards.
///
/// Rules:
///   - Exact match: "scynapse.app.IOrderGrain.PlaceOrder" matches itself
///   - * matches exactly one segment: "scynapse.app.*.GetItem" matches "scynapse.app.IOrderGrain.GetItem"
///   - > matches one or more trailing segments: "scynapse.app.>" matches "scynapse.app.IOrderGrain.GetItem"
///   - > must be the last token if present
/// </summary>
public static class SubjectNameMatcher
{
    /// <summary>
    /// Returns true if the <paramref name="pattern"/> matches the <paramref name="subject"/>.
    /// Both are dot-separated strings. Pattern may contain * and > wildcards.
    /// </summary>
    public static bool Matches(string pattern, string subject)
    {
        if (string.Equals(pattern, subject, StringComparison.Ordinal))
            return true;

        // Full wildcard
        if (pattern == ">")
            return true;

        var patternSegments = pattern.Split('.');
        var subjectSegments = subject.Split('.');

        return MatchSegments(patternSegments, subjectSegments, 0, 0);
    }

    private static bool MatchSegments(string[] pattern, string[] subject, int pi, int si)
    {
        while (pi < pattern.Length && si < subject.Length)
        {
            var p = pattern[pi];

            // > matches one or more remaining segments (must be last token)
            if (p == ">")
                return true; // si < subject.Length is already guaranteed

            // * matches exactly one segment
            if (p == "*")
            {
                pi++;
                si++;
                continue;
            }

            // Exact segment match
            if (!string.Equals(p, subject[si], StringComparison.Ordinal))
                return false;

            pi++;
            si++;
        }

        // Both exhausted = match
        return pi == pattern.Length && si == subject.Length;
    }
}
