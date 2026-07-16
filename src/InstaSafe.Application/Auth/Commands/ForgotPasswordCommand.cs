using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Auth.Commands;

public record ForgotPasswordCommand(string Email) : IRequest<Result<string>>;
