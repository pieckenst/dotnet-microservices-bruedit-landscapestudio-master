using System.Reflection;
using System.Text;

namespace Shared.Common.Serialization
{
    /// <summary>
    /// Pipe-delimited (|) text file serializer.
    /// Each row is one line; columns separated by '|'.
    /// Format: Header line, then one line per record.
    /// </summary>
    public class PipeDelimitedSerializer : ITextFileSerializer
    {
        public string Format => "Pipe";

        public string Serialize<T>(IEnumerable<T> data, string[]? headers = null)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var sb = new StringBuilder();

            // Header line
            var headerNames = headers ?? properties.Select(p => p.Name).ToArray();
            sb.AppendLine(string.Join("|", headerNames));

            // Data lines
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    var val = p.GetValue(item);
                    return val?.ToString() ?? string.Empty;
                });
                sb.AppendLine(string.Join("|", values));
            }

            return sb.ToString();
        }

        public IEnumerable<T> Deserialize<T>(string content) where T : new()
        {
            if (string.IsNullOrWhiteSpace(content))
                yield break;

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                yield break;

            // First line is headers
            var headerColumns = lines[0].Split('|');
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 1; i < lines.Length; i++)
            {
                var columns = lines[i].Split('|');
                var obj = new T();

                for (int j = 0; j < headerColumns.Length && j < columns.Length; j++)
                {
                    var headerName = headerColumns[j].Trim();
                    var prop = properties.FirstOrDefault(p =>
                        string.Equals(p.Name, headerName, StringComparison.OrdinalIgnoreCase));
                    if (prop != null && !string.IsNullOrWhiteSpace(columns[j]))
                    {
                        var converted = ConvertValue(columns[j].Trim(), prop.PropertyType);
                        if (converted != null)
                            prop.SetValue(obj, converted);
                    }
                }

                yield return obj;
            }
        }

        private static object? ConvertValue(string value, Type targetType)
        {
            try
            {
                if (targetType == typeof(Guid))
                    return Guid.Parse(value);
                if (targetType == typeof(DateTime))
                    return DateTime.Parse(value);
                if (targetType == typeof(decimal))
                    return decimal.Parse(value);
                if (targetType == typeof(int))
                    return int.Parse(value);
                if (targetType == typeof(bool))
                    return bool.Parse(value);
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, value, ignoreCase: true);
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }
    }
}
