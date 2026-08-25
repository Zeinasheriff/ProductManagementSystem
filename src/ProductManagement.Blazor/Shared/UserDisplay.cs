using System.Security.Claims;

namespace ProductManagement.Blazor.Shared;

/// <summary>
/// Shared helpers for rendering user identity information in the UI,
/// so every component displays names consistently.
/// </summary>
public static class UserDisplay
{
    public static string GetDisplayName(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return string.Empty;
        }

        var firstName = user.FindFirst(ClaimTypes.GivenName)?.Value ?? string.Empty;
        var lastName = user.FindFirst(ClaimTypes.Surname)?.Value ?? string.Empty;
        var email = user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        return string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName)
            ? email
            : $"{firstName} {lastName}".Trim();
    }

    public static string GetInitials(ClaimsPrincipal user)
    {
        var name = GetDisplayName(user);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0][..1].ToUpperInvariant();
        }

        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}