namespace Chassis.Domain.Common;

/// <summary>
/// Base for entities: identity-based equality over a strongly-typed id.
/// </summary>
public abstract class Entity<TId>
    where TId : struct
{
    protected Entity(TId id) => Id = id;

    /// <summary>Parameterless ctor for the persistence layer's materialization only.</summary>
    protected Entity()
    {
    }

    public TId Id { get; protected set; }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && other.GetType() == GetType() && other.Id.Equals(Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
