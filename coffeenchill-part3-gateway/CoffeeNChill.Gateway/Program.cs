using System.Reflection;
using CoffeeNChill.Gateway.Auth;
using CoffeeNChill.Gateway.Middleware;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Controllers (model validation errors are returned automatically as 400 problem details) ----
builder.Services.AddControllers();

// ---- Storage services: the gateway talks to Tables, Queues and Files through the Azure SDKs ----
builder.Services.AddSingleton<IMenuService, MenuService>();
builder.Services.AddSingleton<IOrderService, OrderService>();
builder.Services.AddSingleton<IDocumentService, DocumentService>();

// ---- Swagger / OpenAPI ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CoffeeNChill Gateway API",
        Version = "v1",
        Description = "Unified API gateway for the CoffeeNChill canteen system: menu, orders (queue) and staff documents."
    });

    // Pull endpoint, request-body and response-code descriptions from the /// XML comments.
    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    // TODO (Thandokuhle): add the "Bearer" security definition + requirement here so Swagger shows the Authorize button.
});

// ===== AUTH PLACEHOLDER =====================================================================
// Thandokuhle: replace this block with AddAuthentication().AddJwtBearer(...) and your role policies.
// Until then every [Authorize] endpoint returns 401 (public endpoints work normally).
builder.Services
    .AddAuthentication(PlaceholderAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, PlaceholderAuthHandler>(PlaceholderAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
// ===== END AUTH PLACEHOLDER =================================================================

var app = builder.Build();

// Must be first so it can catch exceptions thrown by everything after it.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger stays on in every environment: the VPS demo (http://<ip>:5000/swagger) runs in Production.
app.UseSwagger();
app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/swagger/v1/swagger.json", "CoffeeNChill Gateway v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Lightweight endpoints for humans and uptime checks.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).ExcludeFromDescription();

app.Run();

// Makes the entry point visible to the integration tests (WebApplicationFactory<Program>).
public partial class Program { }
