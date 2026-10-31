using ApexPost.Infrastructure.Persistence;
using ApexPost.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApexPostOptions()
    .AddApexPostMvc()
    .AddApexPostSecurity()
    .AddApexPostRateLimiting()
    .AddApexPostApplication()
    .AddSqlServerPersistence();

var app = builder.Build();

app.UseApexPostPipeline();
app.MapApexPostEndpoints();

app.Run();

public partial class Program { }
