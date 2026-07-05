namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Bounds a transcript to a character budget before it is sent to the LLM. Unlike SkipWatch's
/// <c>TranscriptSlicer</c> (which keeps only the first ~5 minutes for a triage card), Video
/// Cortex needs a reasonably <b>complete</b> summary — it feeds Phase 6's cross-video report —
/// so the default budget is generous and most videos pass through untouched. Only very long
/// transcripts are trimmed, and even then the head <b>and</b> tail are kept (with an elision
/// marker) so the ending isn't lost. Chunked/map-reduce summarization for extreme outliers is a
/// v2 concern (PRD §13).
/// </summary>
public static class TranscriptWindow
{
    /// <summary>
    /// ~48k characters ≈ 12k tokens — comfortably within a modern model's context while leaving
    /// room for the prompt and completion. A typical &lt; 60-minute transcript is well under this.
    /// </summary>
    public const int DefaultBudgetChars = 48_000;

    private const string ElisionMarker = "\n\n[… transcript trimmed for length …]\n\n";

    public static string Trim(string? transcript, int budgetChars = DefaultBudgetChars)
    {
        if (string.IsNullOrEmpty(transcript)) return string.Empty;
        if (budgetChars <= 0 || transcript.Length <= budgetChars) return transcript;

        // Keep 70% head + 30% tail so both the setup and the conclusion survive.
        var available = budgetChars - ElisionMarker.Length;
        if (available <= 0) return transcript[..budgetChars];

        var headLen = (int)(available * 0.7);
        var tailLen = available - headLen;
        return string.Concat(transcript.AsSpan(0, headLen), ElisionMarker, transcript.AsSpan(transcript.Length - tailLen));
    }
}
