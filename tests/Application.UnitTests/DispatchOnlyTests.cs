using FluentValidation;
using InstaSafe.Application.Features.Orders.Commands.CreateOrder;
using InstaSafe.Domain.Enums;
using InstaSafe.Infrastructure.Outbox;

namespace Application.UnitTests;

/// <summary>
/// Orders are physical only. The vendor states which of two flows they are in,
/// and each shape is unambiguous:
///
///   Dispatch     (0) - a rider carries it, and a rider is REQUIRED
///   SelfDelivery (2) - the vendor hands it over, and a rider is FORBIDDEN
///   Digital      (1) - disabled
///
/// The ambiguous middle ("dispatch, but nobody to carry it") is what left the
/// track page unable to tell whether verify-otp would 200 or 400, so it is no
/// longer accepted.
/// </summary>
public class DispatchOnlyTests
{
    private static CreateOrderCommand Cmd(
        FulfillmentType fulfillment = FulfillmentType.Dispatch,
        long deliveryFeeNgn = 5_000,
        string? driverPhone = "08055556666") =>
        new(
            VendorPhone: "08010000000",
            CustomerName: "Chidi",
            CustomerPhone: "08087654321",
            DeliveryAddress: "Lekki Phase 1",
            Items: [new OrderItemInput("Sneakers", 2, 22_500)],
            AmountNgn: 45_000,
            BuyerEmail: "buyer@example.com",
            Fulfillment: fulfillment,
            DeliveryFeeNgn: deliveryFeeNgn,
            DriverPhone: driverPhone);

    [Fact]
    public void Dispatch_RequiresARider()
    {
        var result = new CreateOrderCommandValidator().Validate(Cmd(driverPhone: null));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors, e => e.PropertyName == "DriverPhone");
        Assert.Equal("fulfillment.dispatch_needs_rider", failure.ErrorCode);
        Assert.Contains("self-delivery", failure.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispatch_WithRider_IsValid()
    {
        Assert.True(new CreateOrderCommandValidator().Validate(Cmd()).IsValid);
    }

    [Fact]
    public void SelfDelivery_MustNotNameARider()
    {
        var result = new CreateOrderCommandValidator().Validate(
            Cmd(FulfillmentType.SelfDelivery, deliveryFeeNgn: 0));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors, e => e.PropertyName == "DriverPhone");
        Assert.Equal("fulfillment.selfdelivery_no_rider", failure.ErrorCode);
    }

    [Fact]
    public void SelfDelivery_WithoutRider_IsValid()
    {
        Assert.True(new CreateOrderCommandValidator()
            .Validate(Cmd(FulfillmentType.SelfDelivery, deliveryFeeNgn: 0, driverPhone: null)).IsValid);
    }

    [Fact]
    public void Digital_IsDisabled()
    {
        var result = new CreateOrderCommandValidator().Validate(
            Cmd(FulfillmentType.Digital, deliveryFeeNgn: 0, driverPhone: null));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors, e => e.PropertyName == "Fulfillment");
        Assert.Equal("fulfillment.unsupported", failure.ErrorCode);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(99)]
    [InlineData(-1)]
    public void UnknownFulfillment_IsRejected(int raw)
    {
        var result = new CreateOrderCommandValidator().Validate(
            Cmd((FulfillmentType)raw, deliveryFeeNgn: 0, driverPhone: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Fulfillment");
    }

    [Fact]
    public void SelfDeliveryBackstop_GracePeriod_IsSet()
    {
        // The safety net: a self-delivery order has no rider, so nothing can
        // confirm it. Without this the buyer's escrow is stranded if they
        // never enter their code.
        Assert.Equal(TimeSpan.FromHours(24), ReleaseDueOrdersWorker.SelfDeliveryAutoReleaseAfter);
    }
}
