namespace InstaSafe.Application.Common.Interfaces;

public interface IOtpService
{
    string GenerateOtp(int digits = 6);
    string Hash(string otp, string salt);
    string NewSalt();
}
