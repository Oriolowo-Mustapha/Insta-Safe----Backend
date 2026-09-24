using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;
using MediatR;

namespace InstaSafe.Application.Features.Auth.Commands.VerifyVendorOtp;

public sealed record VerifyVendorOtpCommand(string Phone, string Code)
    : IRequest<Result<VendorAuthResponse>>;
