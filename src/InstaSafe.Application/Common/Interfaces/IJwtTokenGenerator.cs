using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    Task<string> GenerateTokenAsync(User user, IReadOnlyList<string> roles, CancellationToken ct);
}
