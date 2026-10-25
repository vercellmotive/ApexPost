using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApexPost.Test;

public sealed class ApexPostWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging =>
        {
            logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Debug);
            logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Debug);
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DemoPassword"] = "TestPassword123!",
                ["Integration:ApiKey"] = "test-integration-key",
                ["Security:AllowedIps:0"] = "127.0.0.1",
                ["Security:AllowedIps:1"] = "::1",
                ["Security:AllowedIps:2"] = "127.0.0.25",
                ["Security:AllowedIps:3"] = "127.0.0.30",
                ["Security:AllowedIps:4"] = "127.0.0.40"
            });
        });
    }
}
