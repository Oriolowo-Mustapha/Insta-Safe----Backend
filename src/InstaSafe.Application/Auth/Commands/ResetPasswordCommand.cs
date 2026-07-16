using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Auth.Commands;

public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result<string>>;
