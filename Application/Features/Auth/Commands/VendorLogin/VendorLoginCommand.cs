using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;
using MediatR;

namespace InstaSafe.Application.Features.Auth.Commands.VendorLogin;

public sealed record VendorLoginCommand(string LoginId, string Password)
    : IRequest<Result<VendorAuthResponse>>;
