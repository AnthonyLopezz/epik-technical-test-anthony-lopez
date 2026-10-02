using Epik.Crud.Api.Api;
using Epik.Crud.Api.Application;
using Epik.Crud.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Epik")
    ?? throw new InvalidOperationException("Missing connection string 'Epik'.");

builder.Services.AddSingleton<IPersonRepository>(new SqlitePersonRepository(connectionString));
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// In production too: binding errors (malformed JSON, page=abc) go through ApiExceptionHandler
// and come out with the same { success, message } shape.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

DatabaseInitializer.RunScript(connectionString, Path.Combine(AppContext.BaseDirectory, "schema.sql"));
// `dotnet run -- seed` loads the sample data (INSERT OR IGNORE: safe to repeat, never duplicates).
if (args.Contains("seed"))
    DatabaseInitializer.RunScript(connectionString, Path.Combine(AppContext.BaseDirectory, "seed.sql"));

// Security headers on every response (OnStarting: they survive the error handler's Response.Clear).
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        // Swagger UI relies on inline scripts and styles; the rest of the site does not.
        if (!context.Request.Path.StartsWithSegments("/swagger"))
            headers.ContentSecurityPolicy = "default-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";
        return Task.CompletedTask;
    });
    return next();
});
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapPersonEndpoints();

app.Run();

// Exposed for the integration tests (WebApplicationFactory).
public partial class Program;
