using AutoMapper;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Mapping;

public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<Order, OrderDto>();
        CreateMap<OrderItem, OrderItemDto>();
    }
}
