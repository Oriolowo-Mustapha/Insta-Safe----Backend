using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Auth.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IPasswordHasher _passwordHasher;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator tokenGenerator,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthResult>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.Merchant)
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result<AuthResult>.Failure("Invalid email or password.");

        if (!user.IsActive)
            return Result<AuthResult>.Failure("Account is disabled.");

        if (!user.IsEmailVerified)
            return Result<AuthResult>.Failure("Email is not verified. Please verify your email before logging in.");

        user.LastLoginAt = DateTime.UtcNow;
        
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
        
        await _unitOfWork.SaveChangesAsync(ct);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var token = await _tokenGenerator.GenerateTokenAsync(user, roles, ct);

        var isVerified = user.Merchant?.IsVerified ?? false;
        var businessName = user.Merchant?.BusinessName ?? $"{user.FirstName} {user.LastName}";

        return Result<AuthResult>.Success(new AuthResult(token, refreshToken, user.Id.ToString(), user.Email, user.FirstName, user.LastName, roles, isVerified, businessName));
    }
}
