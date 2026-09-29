using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Auth.Commands.VerifyEmailCode;

public sealed record VerifyEmailCodeCommand(string Email, string Code)
    : IRequest<Result<VendorDto>>;
