namespace Shared.Common.Common
{
    /// <summary>
    /// Abstract base entity providing common audit fields for all domain entities.
    /// Demonstrates OOP: abstraction (abstract class), encapsulation (protected set).
    /// </summary>
    public abstract class EntityBase
    {
        public Guid Id { get; protected set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }
    }
}
