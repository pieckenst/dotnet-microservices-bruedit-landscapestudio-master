using System.Reflection;
using System.Text;

namespace Shared.Common.Serialization
{
    /// <summary>
    /// Comma-separated values (CSV) text file serializer.
    /// Handles quoted fields containing commas.
    /// </summary>
    public class CsvTextSerializer : ITextFileSerializer
    {
        public string Format => "CSV";

        public string Serialize<T>(IEnumerable<T> data, string[]? headers = null)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var sb = new StringBuilder();

            // Header line
            var headerNames = headers ?? properties.Select(p => p.Name).ToArray();
            sb.AppendLine(string.Join(",", headerNames.Select(EscapeCsvField)));

            // Data lines
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    var val = p.GetValue(item);
                    return EscapeCsvField(val?.ToString() ?? string.Empty);
                });
                sb.AppendLine(string.Join(",", values));
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

            var headerColumns = ParseCsvLine(lines[0]);
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 1; i < lines.Length; i++)
            {
                var columns = ParseCsvLine(lines[i]);
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

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
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
