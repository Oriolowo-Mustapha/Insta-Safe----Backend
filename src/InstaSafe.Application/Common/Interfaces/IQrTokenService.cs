using InstaSafe.Application.Common.Models;

namespace InstaSafe.Application.Common.Interfaces;

public interface IQrTokenService
{
    string GenerateSignedToken(QrPayload payload);
    QrPayload? ValidateAndDecodeToken(string token);
}
