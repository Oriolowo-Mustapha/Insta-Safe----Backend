using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Auth.Commands;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public RefreshTokenCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<AuthResult>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.Merchant)
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken, ct);

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Result<AuthResult>.Failure("Invalid or expired refresh token.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var newToken = await _tokenGenerator.GenerateTokenAsync(user, roles, ct);
        
        var newRefreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        user.SetRefreshToken(newRefreshToken, DateTime.UtcNow.AddDays(7));
        
        await _unitOfWork.SaveChangesAsync(ct);

        var isVerified = user.Merchant?.IsVerified ?? false;
        var businessName = user.Merchant?.BusinessName ?? $"{user.FirstName} {user.LastName}";

        return Result<AuthResult>.Success(new AuthResult(newToken, newRefreshToken, user.Id.ToString(), user.Email, user.FirstName, user.LastName, roles, isVerified, businessName));
    }
}
