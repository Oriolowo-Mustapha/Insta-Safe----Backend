using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.ParseOrderText;

public sealed record ParseOrderTextCommand(string VendorPhone, string RawText) : IRequest<Result<ParsedOrder>>;
