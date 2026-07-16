using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Auth.Queries;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<AuthResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public GetCurrentUserQueryHandler(IApplicationDbContext context, IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<AuthResult>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        if (user == null)
            return Result<AuthResult>.Failure("User not found.");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var token = await _tokenGenerator.GenerateTokenAsync(user, roles, ct);

        return Result<AuthResult>.Success(new AuthResult(token, user.RefreshToken ?? "", user.Id.ToString(), user.Email, user.FirstName, user.LastName, roles));
    }
}
