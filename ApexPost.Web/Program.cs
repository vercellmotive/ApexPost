using ApexPost.Infrastructure.Services;
using ApexPost.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApexPostOptions()
    .AddApexPostMvc()
    .AddApexPostSecurity()
    .AddApexPostRateLimiting()
    .AddApexPostApplication()
    .AddJsonPersistence(builder.Environment.ContentRootPath);

var app = builder.Build();

app.UseApexPostPipeline();
app.MapApexPostEndpoints();

app.Run();

public partial class Program { }
