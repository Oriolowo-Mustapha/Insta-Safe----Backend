using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Auth.Commands;

public record VerifyEmailCommand(string Email, string Token) : IRequest<Result<string>>;
