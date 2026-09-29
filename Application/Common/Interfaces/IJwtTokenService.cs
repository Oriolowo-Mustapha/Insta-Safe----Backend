namespace InstaSafe.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string CreateVendorToken(Guid vendorId, string phone);
    string CreateDispatcherToken(Guid dispatcherId, string phone);
    string CreateAdminToken(string email);
}
