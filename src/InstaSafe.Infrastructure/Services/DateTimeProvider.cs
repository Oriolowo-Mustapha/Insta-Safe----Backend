using InstaSafe.Application.Common.Interfaces;

namespace InstaSafe.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
