using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Dispatch.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Admin.Commands.SetDispatcherActive;

public class SetDispatcherActiveCommandHandler : IRequestHandler<SetDispatcherActiveCommand, Result<DispatcherDto>>
{
    private readonly IDispatcherRepository _dispatchers;
    private readonly IMapper _mapper;

    public SetDispatcherActiveCommandHandler(IDispatcherRepository dispatchers, IMapper mapper)
    {
        _dispatchers = dispatchers; _mapper = mapper;
    }

    public async Task<Result<DispatcherDto>> Handle(SetDispatcherActiveCommand req, CancellationToken ct)
    {
        var dispatcher = await _dispatchers.GetByIdAsync(req.DispatcherId, ct);
        if (dispatcher is null)
            return Result<DispatcherDto>.Failure("Dispatcher not found.");
        dispatcher.IsActive = req.Active;
        dispatcher.Touch();
        await _dispatchers.SaveAsync(ct);
        return Result<DispatcherDto>.Success(_mapper.Map<DispatcherDto>(dispatcher));
    }
}
