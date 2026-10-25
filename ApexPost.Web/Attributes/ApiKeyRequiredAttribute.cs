namespace ApexPost.Web.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ApiKeyRequiredAttribute : Attribute
{
}
