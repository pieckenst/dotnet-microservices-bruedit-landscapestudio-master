namespace Shared.Common.Exceptions
{
    /// <summary>
    /// Thrown when a user lacks the required permission to perform an action.
    /// Maps to HTTP 403 Forbidden.
    /// </summary>
    public class ForbiddenException : ApplicationException
    {
        public ForbiddenException(string message) : base(message) { }

        public ForbiddenException() : base("You do not have permission to perform this action.") { }
    }
}
