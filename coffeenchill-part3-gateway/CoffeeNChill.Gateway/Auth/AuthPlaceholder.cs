using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CoffeeNChill.Gateway.Auth;

/// <summary>Role names used by [Authorize(Roles = ...)] and stored as the Users table PartitionKey.</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Customer = "Customer";

    /// <summary>Roles allowed to modify menu items, documents and order statuses.</summary>
    public const string AdminOrStaff = "Admin,Staff";
}

/// <summary>
/// TEMPORARY authentication scheme that never authenticates anyone, so every
/// [Authorize] endpoint answers 401 until real JWT auth is merged.
///
/// Thandokuhle: delete this file and the "AUTH PLACEHOLDER" block in Program.cs, then register
/// AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...). The controller
/// attributes need no changes.
/// </summary>
public sealed class PlaceholderAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Placeholder";

    public PlaceholderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    // No token is ever accepted. The base class turns the resulting challenge into HTTP 401.
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());
}
