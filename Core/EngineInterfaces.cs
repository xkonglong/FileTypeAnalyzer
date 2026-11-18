namespace FileTypeAnalyzer.Core;

public interface ITridEngine : IDisposable
{
    bool IsReady { get; }
    string LibraryInfo { get; }
    TridResult Analyze(string filePath);
}

public interface IMagikaEngine : IDisposable
{
    bool IsReady { get; }
    string ModelInfo { get; }
    Task<MagikaResult> AnalyzeAsync(string filePath, CancellationToken ct = default);
}