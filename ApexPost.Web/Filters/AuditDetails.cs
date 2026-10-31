namespace ApexPost.Web.Filters;

public static class AuditDetails
{
    private const string ItemKey = "Audit:Details";

    // Callers pass only non-sensitive values: never passwords, document numbers, API keys, card data or names.
    public static void AddAuditDetail(this HttpContext context, string key, string value)
    {
        if (context.Items[ItemKey] is not Dictionary<string, string> details)
        {
            details = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            context.Items[ItemKey] = details;
        }

        details[key] = value;
    }

    public static IReadOnlyDictionary<string, string> GetAuditDetails(this HttpContext context) =>
        context.Items[ItemKey] as Dictionary<string, string> ?? new Dictionary<string, string>();
}
