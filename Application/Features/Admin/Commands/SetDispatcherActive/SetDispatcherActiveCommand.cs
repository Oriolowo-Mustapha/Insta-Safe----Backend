using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Dispatch.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Admin.Commands.SetDispatcherActive;

public sealed record SetDispatcherActiveCommand(Guid DispatcherId, bool Active)
    : IRequest<Result<DispatcherDto>>;
