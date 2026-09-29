using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Features.Auth.Commands.RequestEmailCode;

public sealed record RequestEmailCodeCommand(string Email) : IRequest<Result<bool>>;
