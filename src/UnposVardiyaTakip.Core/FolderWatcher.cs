namespace UnposVardiyaTakip.Core;

public sealed class WatcherStatus
{
    public bool Running { get; set; }
    public string WatchPath { get; set; } = "";
    public string LastScan { get; set; } = "";
    public string LastFile { get; set; } = "";
    public string LastResult { get; set; } = "Beklemede";
    public int FilesOk { get; set; }
    public int FilesError { get; set; }
    public string Message { get; set; } = "Klasör izleme henüz başlamadı.";
}

public sealed class FolderWatcher : IDisposable
{
    private readonly IngestService _ingest = new();
    private readonly Dictionary<string, DateTime> _seen = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public WatcherStatus Status { get; } = new();
    public event Action? Changed;

    public void Start()
    {
        if (_loop is { IsCompleted: false }) return;
        _cts = new CancellationTokenSource();
        Status.Running = true;
        Status.Message = "Klasör izleme çalışıyor.";
        _loop = Task.Run(() => Loop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        Status.Running = false;
        Status.Message = "Klasör izleme durdu.";
    }

    public WatcherStatus ScanOnce()
    {
        var config = AppConfig.Load();
        var raw = (config.WatchPath ?? "").Trim();
        Status.WatchPath = raw;
        if (string.IsNullOrWhiteSpace(raw))
        {
            Status.Message = "İzleme klasörü ayarlanmadı.";
            Status.LastScan = DateTime.Now.ToString("HH:mm:ss");
            return Status;
        }
        if (!Directory.Exists(raw))
        {
            Status.Message = "İzleme klasörü yok veya erişilemiyor.";
            Status.LastScan = DateTime.Now.ToString("HH:mm:ss");
            return Status;
        }

        var files = Directory.GetFiles(raw).Where(IngestService.IsWatchFile).OrderBy(f => f).ToList();
        foreach (var file in files)
        {
            var mtime = File.GetLastWriteTimeUtc(file);
            var isLive = Path.GetFileName(file).StartsWith("sales", StringComparison.OrdinalIgnoreCase);
            if (!isLive && _seen.TryGetValue(file, out var seen) && seen == mtime)
                continue;
            if (!IsStable(file)) continue;
            var result = _ingest.Ingest(file);
            _seen[file] = File.GetLastWriteTimeUtc(file);
            Status.LastFile = Path.GetFileName(file);
            if (result.Status == "ok")
            {
                Status.FilesOk++;
                Status.LastResult = $"Aktarıldı: {Path.GetFileName(file)}";
            }
            else if (result.Status == "skip")
                Status.LastResult = $"Atlandı: {Path.GetFileName(file)}";
            else
            {
                Status.FilesError++;
                Status.LastResult = $"Hata: {Path.GetFileName(file)} — {result.Message}";
            }
        }
        Status.LastScan = DateTime.Now.ToString("HH:mm:ss");
        Status.Message = $"{files.Count} dosya tarandı, son durum: {Status.LastResult}";
        Changed?.Invoke();
        return Status;
    }

    private async Task Loop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { ScanOnce(); }
            catch (Exception ex) { Status.Message = $"İzleme hatası: {ex.Message}"; }
            var seconds = Math.Max(3, AppConfig.Load().PollSeconds);
            try { await Task.Delay(TimeSpan.FromSeconds(seconds), token); }
            catch (TaskCanceledException) { break; }
        }
        Status.Running = false;
    }

    private static bool IsStable(string path)
    {
        try
        {
            var first = new FileInfo(path).Length;
            Thread.Sleep(800);
            var second = new FileInfo(path).Length;
            return first == second && first > 0;
        }
        catch { return false; }
    }

    public void Dispose() => Stop();
}
