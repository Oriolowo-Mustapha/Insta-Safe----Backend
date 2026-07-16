using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Auth.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator tokenGenerator,
        IPasswordHasher passwordHasher,
        IEmailService emailService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    public async Task<Result<string>> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existing = await _context.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (existing)
            return Result<string>.Failure("User with this email already exists.");

        var passwordHash = _passwordHasher.Hash(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = passwordHash,
            Phone = request.Phone,
            IsActive = true
        };

        var roles = await _unitOfWork.Repository<Role>()
            .FindAsync(r => r.Name == "Merchant", ct);

        var role = roles.FirstOrDefault();
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Name = "Merchant", Description = "Merchant role" };
            _unitOfWork.Repository<Role>().Add(role);
        }

        _unitOfWork.Repository<User>().Add(user);

        _context.Set<UserRole>().Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        var merchant = new Merchant
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BusinessName = $"{request.FirstName} {request.LastName}",
            Email = request.Email,
            Phone = request.Phone ?? ""
        };

        _context.Merchants.Add(merchant);

        var verificationToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        user.SetEmailVerificationToken(verificationToken);

        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));

        await _unitOfWork.SaveChangesAsync(ct);

        // Send email
        var verifyLink = $"https://instasafe.com/verify-email?email={user.Email}&token={Uri.EscapeDataString(verificationToken)}";
        await _emailService.SendEmailAsync(user.Email, "Verify your InstaSafe Account", 
            $"Please verify your account by clicking this link: <a href=\"{verifyLink}\">{verifyLink}</a>", ct);

        return Result<string>.Success("Registration successful. Please check your email to verify your account.");
    }
}
