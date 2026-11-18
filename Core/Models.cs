namespace FileTypeAnalyzer.Core;

public enum ConsistencyStatus { Match, Mismatch, Unknown, PartialFailure }

public sealed class TridMatch
{
    public int Rank { get; init; }
    public string TypeDescription { get; init; } = "";
    public string Extension { get; init; } = "";
    public int Points { get; init; }
    public double Confidence { get; init; } // 0.0 ~ 1.0
}

public sealed class TridResult
{
    public IReadOnlyList<TridMatch> Matches { get; init; } = Array.Empty<TridMatch>();
    public TridMatch? Top => Matches.Count > 0 ? Matches[0] : null;
    public string? Error { get; init; }
    public bool Succeeded => Error is null;
}

public sealed class MagikaResult
{
    public string Label { get; init; } = "";
    public string MimeType { get; init; } = "";
    public string Description { get; init; } = "";
    public double Score { get; init; }
    public bool IsText { get; init; }
    public string? Error { get; init; }
    public bool Succeeded => Error is null;
}

public sealed class FileAnalysisResult
{
    public string FilePath { get; init; } = "";
    public long FileSize { get; init; }
    public DateTime ModifiedAt { get; init; }
    public TridResult Trid { get; init; } = new();
    public MagikaResult Magika { get; init; } = new();
    public ConsistencyStatus Consistency { get; init; }
    public string ConsistencyText { get; init; } = "";
}