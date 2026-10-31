using System.Security.Cryptography;
using System.Text;
using ApexPost.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Middleware;

public sealed class PartnerApiKeyValidator
{
    private readonly IOptionsMonitor<IntegrationOptions> _options;

    public PartnerApiKeyValidator(IOptionsMonitor<IntegrationOptions> options)
    {
        _options = options;
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
        var options = _options.CurrentValue;
        var credentials = options.Partners
            .Where(partner => !string.IsNullOrWhiteSpace(partner.Value.ApiKey))
            .Select(partner => new PartnerCredential(partner.Key, partner.Value.ApiKey!))
            .ToList();

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            credentials.Add(new PartnerCredential("default", options.ApiKey));
        }

        return credentials;
    }

    private sealed record PartnerCredential(string PartnerId, string ApiKey);
}
