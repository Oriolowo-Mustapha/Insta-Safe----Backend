using System.Security.Claims;

namespace InstaSafe.Application.Common.Helpers;

public static class ClaimsPrincipalExtensions
{
    public const string VendorIdClaim = "vendor_id";
    public const string DispatcherIdClaim = "dispatcher_id";

    public static Guid? VendorId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst(VendorIdClaim)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string? VendorPhone(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.MobilePhone)?.Value;

    public static Guid? DispatcherId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst(DispatcherIdClaim)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
