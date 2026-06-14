using System.Text.Json;
using System.Text.Json.Serialization;

namespace MomsLove.Core;

public sealed class AppDataStore
{
    private readonly string _configPath;
    private readonly string _usagePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public AppDataStore(string? baseDirectory = null)
    {
        var root = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".MomsLove");

        Directory.CreateDirectory(root);
        _configPath = Path.Combine(root, "config.json");
        _usagePath = Path.Combine(root, "usage.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(), new DateOnlyJsonConverter() }
        };
    }

    public string ConfigPath => _configPath;
    public string UsagePath => _usagePath;

    public async Task<AppConfig> LoadConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configPath))
        {
            return new AppConfig();
        }

        await using var stream = File.OpenRead(_configPath);
        return await JsonSerializer.DeserializeAsync<AppConfig>(stream, _jsonOptions, cancellationToken)
            ?? new AppConfig();
    }

    public async Task SaveConfigAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_configPath);
        await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, cancellationToken);
    }

    public async Task<DailyUsage> LoadUsageAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        DailyUsage usage;
        if (!File.Exists(_usagePath))
        {
            usage = new DailyUsage { Date = today };
        }
        else
        {
            await using var stream = File.OpenRead(_usagePath);
            usage = await JsonSerializer.DeserializeAsync<DailyUsage>(stream, _jsonOptions, cancellationToken)
                ?? new DailyUsage { Date = today };
        }

        return usage.Date == today ? usage : new DailyUsage { Date = today };
    }

    public async Task SaveUsageAsync(DailyUsage usage, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_usagePath);
        await JsonSerializer.SerializeAsync(stream, usage, _jsonOptions, cancellationToken);
    }

    private sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        private const string Format = "yyyy-MM-dd";

        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateOnly.ParseExact(reader.GetString() ?? "", Format);
        }

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(Format));
        }
    }
}
