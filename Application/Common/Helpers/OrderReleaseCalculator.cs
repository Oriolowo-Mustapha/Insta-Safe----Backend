using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Helpers;

public static class OrderReleaseCalculator
{
    public static long VendorRemainderKobo(Order order)
    {
        var driverPaid = order.DriverTransferReference is not null ? order.DeliveryFeeKobo : 0;
        var remainder = order.AmountKobo - driverPaid;
        return remainder < 0 ? 0 : remainder;
    }
}
