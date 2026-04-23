using System.Diagnostics;
using System.Text.Json;
using ClassroomToolkit.Infra.Settings;

var options = ParseArgs(args);
if (string.IsNullOrWhiteSpace(options.SettingsPath) || string.IsNullOrWhiteSpace(options.OutputJsonPath))
{
    Console.Error.WriteLine("Usage: --settings-path <path> --output-json <path> [--label <name>] [--cold-iterations <n>] [--hot-iterations <n>]");
    return 2;
}

if (!File.Exists(options.SettingsPath))
{
    Console.Error.WriteLine($"settings file not found: {options.SettingsPath}");
    return 3;
}

var fileInfo = new FileInfo(options.SettingsPath);
var coldSamplesMs = MeasureColdLoads(options.SettingsPath, options.ColdIterations);
var hotSamplesMs = MeasureHotLoads(options.SettingsPath, options.HotIterations);

var report = new
{
    generated_at = DateTimeOffset.UtcNow.ToString("o"),
    label = options.Label,
    settings_path = Path.GetFullPath(options.SettingsPath),
    file_size_bytes = fileInfo.Length,
    cold = BuildSummary(coldSamplesMs),
    hot = BuildSummary(hotSamplesMs),
    oversized_reject_count = JsonSettingsDocumentStoreAdapter.OversizedSettingsRejectCount
};

var outputDirectory = Path.GetDirectoryName(options.OutputJsonPath);
if (!string.IsNullOrWhiteSpace(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

File.WriteAllText(
    options.OutputJsonPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"[settings-load-perf-probe] label={options.Label} output={options.OutputJsonPath}");
return 0;

static double[] MeasureColdLoads(string settingsPath, int iterations)
{
    var samples = new double[iterations];
    for (var i = 0; i < iterations; i++)
    {
        var adapter = new JsonSettingsDocumentStoreAdapter(settingsPath);
        var stopwatch = Stopwatch.StartNew();
        _ = adapter.Load();
        stopwatch.Stop();
        samples[i] = stopwatch.Elapsed.TotalMilliseconds;
    }

    return samples;
}

static double[] MeasureHotLoads(string settingsPath, int iterations)
{
    var adapter = new JsonSettingsDocumentStoreAdapter(settingsPath);
    var samples = new double[iterations];
    for (var i = 0; i < iterations; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        _ = adapter.Load();
        stopwatch.Stop();
        samples[i] = stopwatch.Elapsed.TotalMilliseconds;
    }

    return samples;
}

static object BuildSummary(double[] samples)
{
    if (samples.Length == 0)
    {
        return new
        {
            count = 0,
            avg_ms = (double?)null,
            p50_ms = (double?)null,
            p95_ms = (double?)null,
            max_ms = (double?)null
        };
    }

    var sorted = samples.OrderBy(v => v).ToArray();
    var avg = samples.Average();
    var max = samples.Max();
    return new
    {
        count = samples.Length,
        avg_ms = Math.Round(avg, 4),
        p50_ms = Math.Round(Percentile(sorted, 0.50), 4),
        p95_ms = Math.Round(Percentile(sorted, 0.95), 4),
        max_ms = Math.Round(max, 4)
    };
}

static double Percentile(double[] sorted, double percentile)
{
    if (sorted.Length == 0)
    {
        return 0;
    }

    var rawIndex = Math.Ceiling(percentile * sorted.Length) - 1;
    var index = (int)Math.Clamp(rawIndex, 0, sorted.Length - 1);
    return sorted[index];
}

static ProbeOptions ParseArgs(string[] args)
{
    var options = new ProbeOptions();
    for (var i = 0; i < args.Length; i++)
    {
        var token = args[i];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        if (i + 1 >= args.Length)
        {
            continue;
        }

        var value = args[++i];
        switch (token)
        {
            case "--settings-path":
                options.SettingsPath = value;
                break;
            case "--output-json":
                options.OutputJsonPath = value;
                break;
            case "--label":
                options.Label = value;
                break;
            case "--cold-iterations":
                if (int.TryParse(value, out var coldIterations) && coldIterations > 0)
                {
                    options.ColdIterations = coldIterations;
                }
                break;
            case "--hot-iterations":
                if (int.TryParse(value, out var hotIterations) && hotIterations > 0)
                {
                    options.HotIterations = hotIterations;
                }
                break;
        }
    }

    return options;
}

file sealed class ProbeOptions
{
    public string SettingsPath { get; set; } = string.Empty;

    public string OutputJsonPath { get; set; } = string.Empty;

    public string Label { get; set; } = "settings-load";

    public int ColdIterations { get; set; } = 20;

    public int HotIterations { get; set; } = 200;
}

