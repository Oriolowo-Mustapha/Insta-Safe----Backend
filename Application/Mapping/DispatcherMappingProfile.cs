using AutoMapper;
using InstaSafe.Application.Features.Dispatch.DTOs;
using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Mapping;

public class DispatcherMappingProfile : Profile
{
    public DispatcherMappingProfile()
    {
        CreateMap<Dispatcher, DispatcherDto>();
    }
}
