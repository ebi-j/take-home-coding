using System.Text.Json;


namespace ReleaseRetention.Core.Tests.Helpers;
public static class JsonReader
{
    public static async Task<T> ReadFromJson<T>(string filePath) where T : class
    {
        string jsonString = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(
            jsonString,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }
}
