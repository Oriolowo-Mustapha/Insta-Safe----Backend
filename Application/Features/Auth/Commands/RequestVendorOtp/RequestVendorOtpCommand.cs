using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;

public sealed record RequestVendorOtpCommand(string Phone) : IRequest<Result<bool>>;

public sealed record VendorAuthResponse(string Token, VendorDto? Vendor, double ExpiresInHours, string Role = "vendor");
