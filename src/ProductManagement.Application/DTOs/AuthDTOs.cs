namespace ProductManagement.Application.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponse(
    bool Success,
    string Token,
    string Email,
    string Role,
    string Message
);