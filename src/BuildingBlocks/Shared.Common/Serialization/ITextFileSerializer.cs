using System.Text.Json;

namespace Shared.Common.Serialization
{
    /// <summary>
    /// Strategy pattern interface for serializing/deserializing tabular data to and from text files.
    /// Used for экспорт табличных результатов в текстовый файл and импорт из текстового файла.
    /// </summary>
    public interface ITextFileSerializer
    {
        /// <summary>Gets the format name (e.g. "Pipe", "CSV").</summary>
        string Format { get; }

        /// <summary>Serializes a collection of objects into a text representation.</summary>
        string Serialize<T>(IEnumerable<T> data, string[]? headers = null);

        /// <summary>Deserializes text content back into a collection of objects.</summary>
        IEnumerable<T> Deserialize<T>(string content) where T : new();
    }
}
