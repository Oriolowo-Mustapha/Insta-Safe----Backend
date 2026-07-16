using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Auth.Queries;

public record GetCurrentUserQuery(Guid UserId) : IRequest<Result<AuthResult>>;
