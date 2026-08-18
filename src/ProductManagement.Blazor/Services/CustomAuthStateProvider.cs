using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace ProductManagement.Blazor.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _httpClient;
    private readonly AuthenticationState _anonymous;

    public CustomAuthStateProvider(ILocalStorageService localStorage, HttpClient httpClient)
    {
        _localStorage = localStorage;
        _httpClient = httpClient;
        _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            if (string.IsNullOrWhiteSpace(token))
            {
                return _anonymous;
            }

            var identity = CreateIdentityFromToken(token);
            if (identity == null)
            {
                await _localStorage.RemoveItemAsync("authToken");
                return _anonymous;
            }

            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return _anonymous;
        }
    }

    public void NotifyUserAuthentication(string token)
    {
        var identity = CreateIdentityFromToken(token);
        if (identity == null) return;

        var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        NotifyAuthenticationStateChanged(authState);
    }

    public void NotifyUserLogout()
    {
        var authState = Task.FromResult(_anonymous);
        NotifyAuthenticationStateChanged(authState);
    }

    /// <summary>
    /// Builds a ClaimsIdentity from a JWT, mapping short JWT claim names
    /// (e.g. given_name, family_name, email, role) to the long .NET
    /// ClaimTypes values so that ClaimTypes.GivenName, ClaimTypes.Email,
    /// ClaimTypes.Role etc. resolve correctly.
    /// </summary>
    private static ClaimsIdentity? CreateIdentityFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                return null; // expired
            }

            var claims = jwtToken.Claims
                .Select(c => new Claim(MapClaimType(c.Type), c.Value, c.ValueType, c.Issuer, c.OriginalIssuer))
                .ToList();

            // Fallback: if no nameidentifier claim present, add one from "sub" or "nameid"
            if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
            {
                var sub = jwtToken.Claims.FirstOrDefault(c => c.Type is "sub" or "nameid")?.Value;
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
                }
            }

            return new ClaimsIdentity(claims, "jwt");
        }
        catch
        {
            return null;
        }
    }

    private static string MapClaimType(string claimType) => claimType switch
    {
        "given_name" or "unique_name" or "firstname" => ClaimTypes.GivenName,
        "family_name" or "surname" or "lastname" => ClaimTypes.Surname,
        "email" or "emailaddress" => ClaimTypes.Email,
        "role" => ClaimTypes.Role,
        "nameid" or "sub" => ClaimTypes.NameIdentifier,
        _ => claimType
    };
}