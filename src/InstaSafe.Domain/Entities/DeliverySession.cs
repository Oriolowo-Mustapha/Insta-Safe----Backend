using InstaSafe.Domain.Common;
using InstaSafe.Domain.Enums;

namespace InstaSafe.Domain.Entities;

public class DeliverySession : BaseEntity
{
    public Guid OrderId { get; init; }
    public Guid SessionId { get; init; }
    public DeliverySessionStatus Status { get; private set; } = DeliverySessionStatus.PickedUp;
    public string? PickupDeviceFingerprint { get; init; }
    public DateTime? PickupTimestamp { get; init; }
    public double? PickupLatitude { get; init; }
    public double? PickupLongitude { get; init; }
    public string? DeliveryDeviceFingerprint { get; private set; }
    public DateTime? DeliveryTimestamp { get; private set; }
    public double? DeliveryLatitude { get; private set; }
    public double? DeliveryLongitude { get; private set; }
    public DeliveryFailureReason? FailureReason { get; private set; }
    public DateTime? ExpiresAt { get; init; }

    public virtual Order Order { get; private set; } = null!;

    public void MarkAsDelivered(string? fingerprint, double? latitude, double? longitude, DateTime timestamp)
    {
        Status = DeliverySessionStatus.Delivered;
        DeliveryDeviceFingerprint = fingerprint;
        DeliveryLatitude = latitude;
        DeliveryLongitude = longitude;
        DeliveryTimestamp = timestamp;
    }

    public void MarkAsFailed(DeliveryFailureReason reason)
    {
        Status = DeliverySessionStatus.Failed;
        FailureReason = reason;
    }

    public void Expire()
    {
        Status = DeliverySessionStatus.Expired;
        FailureReason = DeliveryFailureReason.SessionExpired;
    }
}
