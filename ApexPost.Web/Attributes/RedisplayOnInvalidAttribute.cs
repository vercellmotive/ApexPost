namespace ApexPost.Web.Attributes;

// Marks a form POST whose invalid submission redisplays a view instead of returning 400.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RedisplayOnInvalidAttribute(string? viewName = null) : Attribute
{
    public string? ViewName { get; } = viewName;
}
