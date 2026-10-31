using System.Security.Cryptography;
using System.Text;

namespace ApexPost.Web.Middleware;

public sealed class PartnerApiKeyValidator
{
    private readonly IConfiguration _configuration;

    public PartnerApiKeyValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool HasConfiguredKeys => GetCredentials().Count > 0;

    public string? FindPartnerId(string? providedKey)
    {
        if (string.IsNullOrEmpty(providedKey))
        {
            return null;
        }

        // Hashing gives both sides a fixed length, so the comparison does not reveal the key length.
        var providedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        string? matchedPartnerId = null;
        foreach (var credential in GetCredentials())
        {
            var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(credential.ApiKey));
            if (CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
            {
                matchedPartnerId = credential.PartnerId;
            }
        }

        return matchedPartnerId;
    }

    private List<PartnerCredential> GetCredentials()
    {
        var credentials = _configuration.GetSection("Integration:Partners")
            .GetChildren()
            .Where(section => !string.IsNullOrWhiteSpace(section["ApiKey"]))
            .Select(section => new PartnerCredential(section.Key, section["ApiKey"]!))
            .ToList();

        var fallbackKey = _configuration["Integration:ApiKey"] ?? _configuration["ApiKeys:Courier"];
        if (!string.IsNullOrWhiteSpace(fallbackKey))
        {
            credentials.Add(new PartnerCredential("default", fallbackKey));
        }

        return credentials;
    }

    private sealed record PartnerCredential(string PartnerId, string ApiKey);
}
