namespace Shared.Common.Exceptions
{
    /// <summary>
    /// Thrown when an entity is not found by its key.
    /// Part of the custom exception hierarchy for обработка исключений.
    /// </summary>
    public class NotFoundException : ApplicationException
    {
        public NotFoundException(string name, object key)
            : base($"Entity \"{name}\" ({key}) was not found.")
        {
        }
    }
}
