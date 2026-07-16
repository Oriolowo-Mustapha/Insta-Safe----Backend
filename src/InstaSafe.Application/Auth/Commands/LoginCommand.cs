using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResult>>;
