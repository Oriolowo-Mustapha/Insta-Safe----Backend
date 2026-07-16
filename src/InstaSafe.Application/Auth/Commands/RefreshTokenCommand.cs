using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Auth.Commands;

public record RefreshTokenCommand(string Token, string RefreshToken) : IRequest<Result<AuthResult>>;
