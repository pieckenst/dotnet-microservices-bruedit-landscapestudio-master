using System.Text.Json;

namespace Shared.Common.Serialization
{
    /// <summary>
    /// JSON-based state serializer for сохранение и восстановление состояния приложения.
    /// Serializes/deserializes arbitrary state objects to/from JSON text files.
    /// </summary>
    public class JsonStateSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>Serializes a state object to JSON string.</summary>
        public string Serialize<T>(T state)
        {
            return JsonSerializer.Serialize(state, Options);
        }

        /// <summary>Deserializes JSON string back to a state object.</summary>
        public T? Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        /// <summary>Saves state to a file at the specified path.</summary>
        public void SaveToFile<T>(T state, string filePath)
        {
            var json = Serialize(state);
            File.WriteAllText(filePath, json);
        }

        /// <summary>Loads state from a file at the specified path.</summary>
        public T? LoadFromFile<T>(string filePath)
        {
            if (!File.Exists(filePath))
                return default;
            var json = File.ReadAllText(filePath);
            return Deserialize<T>(json);
        }
    }
}
