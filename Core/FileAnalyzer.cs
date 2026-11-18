namespace FileTypeAnalyzer.Core;

public sealed class FileAnalyzer : IDisposable
{
    private readonly ITridEngine _trid;
    private readonly IMagikaEngine _magika;

    public FileAnalyzer(ITridEngine trid, IMagikaEngine magika)
    {
        _trid = trid;
        _magika = magika;
    }

    public bool TridReady => _trid.IsReady;
    public bool MagikaReady => _magika.IsReady;
    public string TridInfo => _trid.LibraryInfo;
    public string MagikaInfo => _magika.ModelInfo;

    public async Task<FileAnalysisResult> AnalyzeAsync(string filePath, CancellationToken ct = default)
    {
        var fi = new FileInfo(filePath);
        var size = fi.Exists ? fi.Length : 0;
        var modified = fi.Exists ? fi.LastWriteTime : DateTime.MinValue;

        var tridTask = Task.Run(() => _trid.Analyze(filePath), ct);
        var magikaTask = _magika.AnalyzeAsync(filePath, ct);
        await Task.WhenAll(tridTask, magikaTask);

        var trid = tridTask.Result;
        var magika = magikaTask.Result;
        var status = ConsistencyEvaluator.Evaluate(trid, magika, out var text);

        return new FileAnalysisResult
        {
            FilePath = filePath,
            FileSize = size,
            ModifiedAt = modified,
            Trid = trid,
            Magika = magika,
            Consistency = status,
            ConsistencyText = text
        };
    }

    public void Dispose() { _trid.Dispose(); _magika.Dispose(); }
}