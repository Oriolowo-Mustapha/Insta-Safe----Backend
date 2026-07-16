namespace InstaSafe.Application.Common.Models;

public record AuthResult(string Token, string RefreshToken, string UserId, string Email, string FirstName, string LastName, List<string> Roles);
