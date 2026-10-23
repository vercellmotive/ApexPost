using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Domain.Common
{
    public abstract class Entity<TId> where TId : notnull
    {
        public required TId Id { get; init; }
    }

    public interface IAggregateRoot { }

    public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot where TId : notnull
    {
        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();


        public void AddDomainEvent(IDomainEvent domainEvent) =>
            _domainEvents.Add(domainEvent);
        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    public interface IDomainEvent
    {
        Guid EventId => Guid.NewGuid();
        DateTimeOffset OccurredOnUtc => DateTimeOffset.UtcNow;
    }

    public class DomainValidationException : Exception
    {
        public DomainValidationException(string message) : base(message) { }
    }

    public class UnauthorizedDomainActionException : Exception
    {
        public UnauthorizedDomainActionException(string message) : base(message) { }
    }
}
