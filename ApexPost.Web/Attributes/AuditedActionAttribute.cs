namespace ApexPost.Web.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuditedActionAttribute(string businessAction) : Attribute
{
    public string BusinessAction { get; } = businessAction;
}
