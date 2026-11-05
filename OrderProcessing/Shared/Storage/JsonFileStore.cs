using System.Text.Json;
using System.Text.Json.Serialization;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Shared.Storage
{
    public static class JsonFileStore
    {
        private static readonly SemaphoreSlim _lock = new(1, 1);

        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        public static async Task<List<T>> ReadAsync<T>(string filePath, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(filePath)) return new List<T>();

                string json = await File.ReadAllTextAsync(filePath, ct);
                if (string.IsNullOrWhiteSpace(json)) return new List<T>();

                return JsonSerializer.Deserialize<List<T>>(json, _options) ?? new List<T>();
            }
            catch (JsonException ex)
            {
                throw new DataFileCorruptedException($"Invalid JSON inside file: {Path.GetFileName(filePath)}", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task WriteAsync<T>(string filePath, List<T> data, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                string tempPath = filePath + ".tmp";
                string json = JsonSerializer.Serialize(data, _options);

                await File.WriteAllTextAsync(tempPath, json, ct);
                File.Move(tempPath, filePath, overwrite: true);
            }
            finally
            {
                _lock.Release();
            }
        }

        
        public static List<T> ReadSync<T>(string filePath)
        {
            _lock.Wait();
            try
            {
                if (!File.Exists(filePath)) return new List<T>();
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<T>>(json, _options) ?? new List<T>();
            }
            catch (JsonException ex)
            {
                throw new DataFileCorruptedException($"Invalid JSON inside file: {Path.GetFileName(filePath)}", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public static void WriteSync<T>(string filePath, List<T> data)
        {
            _lock.Wait();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                string tempPath = filePath + ".tmp";
                string json = JsonSerializer.Serialize(data, _options);

                File.WriteAllText(tempPath, json);
                File.Move(tempPath, filePath, overwrite: true);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}

