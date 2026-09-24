using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Dispatch.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Dispatch.Commands.RegisterDispatcher;

public sealed record RegisterDispatcherCommand(
    string Phone,
    string? FirstName = null,
    string? LastName = null,
    string? AccountNumber = null,
    string? BankCode = null
) : IRequest<Result<DispatcherDto>>;
