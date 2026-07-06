namespace FinancialServices.Api.Connectors;

/// <summary>
/// The single as-of selector used by every connector (architecturalPlan/08, P3): the latest item
/// whose timestamp is on or before <paramref name="asOf"/> — no hindsight. Pure, no I/O, no LLM.
/// </summary>
internal static class AsOf
{
    /// <summary>
    /// Guards the no-hindsight boundary (P3): a decision date in the future is a caller bug, not data.
    /// Connectors call this before any fetch so an <paramref name="asOf"/> after "now" fails loud with
    /// <see cref="ArgumentOutOfRangeException"/> rather than silently reading tomorrow's data.
    /// </summary>
    public static void EnsureNotFuture(DateTimeOffset asOf)
    {
        if (asOf > DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(asOf), asOf, "asOf must not be in the future (no hindsight — P3).");
        }
    }

    /// <summary>
    /// Returns the item with the greatest <paramref name="timestamp"/> that is still ≤
    /// <paramref name="asOf"/>, or <c>default</c> when none qualifies. On equal timestamps the
    /// first item encountered wins, so callers that need a secondary tie-break should pre-order the
    /// sequence by that key (EDGAR pre-orders by filing period end).
    /// </summary>
    public static T? LatestOnOrBefore<T>(
        IEnumerable<T> items, Func<T, DateTimeOffset> timestamp, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(timestamp);

        T? best = default;
        var haveBest = false;
        DateTimeOffset bestTimestamp = default;

        foreach (var item in items)
        {
            var ts = timestamp(item);
            if (ts > asOf)
            {
                continue; // future data relative to the decision date — never used.
            }

            if (!haveBest || ts > bestTimestamp)
            {
                best = item;
                bestTimestamp = ts;
                haveBest = true;
            }
        }

        return best;
    }
}
