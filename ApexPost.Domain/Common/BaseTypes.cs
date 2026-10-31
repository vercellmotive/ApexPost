using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

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
        [JsonIgnore]
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();


        public void AddDomainEvent(IDomainEvent domainEvent) =>
            _domainEvents.Add(domainEvent);
        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    public interface IDomainEvent
    {
        Guid EventId { get; }
        DateTimeOffset OccurredOnUtc { get; }
    }

    public abstract record DomainEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
    }

    public sealed record LowStockRule
    {
        public static LowStockRule Default { get; } = new(5);

        public int Threshold { get; }

        public LowStockRule(int threshold)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(threshold);
            Threshold = threshold;
        }

        public bool IsLow(int quantityOnHand) => quantityOnHand <= Threshold;
    }

    public class DomainValidationException : Exception
    {
        public DomainValidationException(string message) : base(message) { }
    }

    public class UnauthorizedDomainActionException : Exception
    {
        public UnauthorizedDomainActionException(string message) : base(message) { }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotBlankAttribute : Attribute
    {
    }

    public static class DomainAttributeValidator
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ValidatedProperties = new();

        public static void Validate(object instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            var properties = ValidatedProperties.GetOrAdd(
                instance.GetType(),
                type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.IsDefined(typeof(NotBlankAttribute), inherit: true))
                    .ToArray());

            foreach (var property in properties)
            {
                if (property.GetValue(instance) is not string value || string.IsNullOrWhiteSpace(value))
                {
                    throw new DomainValidationException($"{property.Name} must not be blank.");
                }
            }
        }
    }
}
