namespace InstaSafe.Domain.Enums;

public enum FulfillmentType
{
    /// <summary>A rider carries the order. Released by the rider's confirmation.</summary>
    Dispatch = 0,

    /// <summary>Disabled. Rejected at validation; kept so historic rows still read.</summary>
    Digital = 1,

    /// <summary>The vendor hands the order over in person. No rider, no driver fee.</summary>
    SelfDelivery = 2
}
