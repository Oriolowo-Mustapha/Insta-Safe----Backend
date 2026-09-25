using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Common.Notifications;

public class OrderNotifier
{
    private readonly IWhatsAppSender _wa;
    private readonly IEmailSender _email;
    private readonly IVendorRepository _vendors;
    private readonly ILogger<OrderNotifier> _logger;

    public OrderNotifier(
        IWhatsAppSender wa, IEmailSender email, IVendorRepository vendors, ILogger<OrderNotifier> logger)
    {
        _wa = wa; _email = email; _vendors = vendors; _logger = logger;
    }

    public static bool IsRealEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Contains('@')
        && !email.EndsWith("@whatsapp.instasafe", StringComparison.OrdinalIgnoreCase);

    private async Task SendVendorEmailAsync(Guid? vendorId, string subject, string html)
    {
        try
        {
            if (vendorId is null) return;
            var vendor = await _vendors.GetByIdAsync(vendorId.Value, CancellationToken.None);
            if (vendor is not null && IsRealEmail(vendor.Email))
                await _email.SendAsync(vendor.Email!, subject, html, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vendor email notify failed");
        }
    }

    private async Task TryWaAsync(string phone, string text)
    {
        try
        {
            await _wa.SendTextAsync(phone, text, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp notify failed for {Phone}", phone);
        }
    }

    private async Task TryEmailAsync(string? email, string subject, string html)
    {
        try
        {
            if (IsRealEmail(email))
                await _email.SendAsync(email!, subject, html, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email notify failed for {Email}", email);
        }
    }

    private static string Money(long kobo) => $"₦{kobo / 100:N0}";

    public async Task OrderCreatedAsync(Order order, string? buyerEmail)
    {
        var link = order.PaystackAuthUrl ?? "";
        await TryEmailAsync(buyerEmail, $"Pay for your order ({Money(order.AmountKobo)})",
            $"<p>Hi {order.CustomerName},</p><p>Your order totals <b>{Money(order.AmountKobo)}</b>.</p><p><a href=\"{link}\">Pay securely with InstaSafe</a></p><p>Funds stay in escrow until you confirm delivery.</p>");
        await SendVendorEmailAsync(order.VendorId, "New order created",
            $"<p>Order {order.Id} for <b>{Money(order.AmountKobo)}</b> was created. Share the payment link with your buyer.</p><p>Check your dashboard for full details.</p>");
    }

    public async Task FundsHeldAsync(Order order, string otpCode, string? buyerEmail)
    {
        await TryEmailAsync(buyerEmail, "Payment received — escrow holding your funds",
            $"<p>Hi {order.CustomerName},</p><p>We received <b>{Money(order.AmountKobo)}</b> for order {order.Id}.</p><p>Your delivery code is <b>{otpCode}</b>. Share it with the rider only when you receive your item.</p>");
    }

    public async Task DriverAssignedAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.DriverPhone)) return;
        await TryWaAsync(order.DriverPhone,
            $"InstaSafe delivery assigned 🚚\nOrder {order.Id}\nDeliver to: {order.DeliveryAddress}\nFee: {Money(order.DeliveryFeeKobo)}\nLog in to the driver portal to confirm on arrival. Check your dashboard for full details.");
    }

    public async Task BankTransferDetailsAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.PayVirtualAccountNumber)) return;
        var amount = Money(order.AmountKobo);
        await TryWaAsync(order.CustomerPhone,
            $"InstaSafe: pay {amount} by bank transfer to complete order {order.Id}:\n" +
            $"Bank: {order.PayVirtualAccountBank}\nAccount: {order.PayVirtualAccountNumber}\n" +
            $"Name: {order.PayVirtualAccountName}\nTransfer EXACTLY {amount} — your payment is confirmed automatically.");
        await TryEmailAsync(order.BuyerEmail, $"Bank transfer details for your order ({amount})",
            $"<p>Hi {order.CustomerName},</p><p>Pay <b>{amount}</b> by bank transfer:</p>" +
            $"<p>Bank: <b>{order.PayVirtualAccountBank}</b><br/>Account: <b>{order.PayVirtualAccountNumber}</b><br/>Name: {order.PayVirtualAccountName}</p>" +
            $"<p>Transfer exactly {amount} — your payment is confirmed automatically and held in escrow.</p>");
    }

    public async Task DeliveredAsync(Order order, string? buyerEmail)
    {
        var window = "You have 24 hours to inspect. If anything is wrong, tap Dispute on your order page — otherwise funds release automatically.";
        await TryWaAsync(order.VendorPhone,
            $"InstaSafe: order {order.Id} marked DELIVERED ✅\n{window}\nCheck your dashboard for full details.");
        await TryWaAsync(order.CustomerPhone,
            $"InstaSafe: your order arrived ✅\n{window}");
        await TryEmailAsync(buyerEmail, "Order delivered — 24h inspection window",
            $"<p>Hi {order.CustomerName},</p><p>Order {order.Id} was marked delivered.</p><p>{window}</p>");
        await SendVendorEmailAsync(order.VendorId, "Order delivered",
            $"<p>Order {order.Id} was marked delivered. {window}</p>");
    }

    public async Task ReleasedAsync(Order order, string? buyerEmail, string? transferRef)
    {
        var payout = transferRef is null ? "Payout is being processed." : $"Transfer ref: {transferRef}.";
        await TryWaAsync(order.VendorPhone,
            $"InstaSafe: funds released ✅ {Money(order.AmountKobo)} for order {order.Id}. {payout}\nCheck your dashboard for full details.");
        await TryWaAsync(order.CustomerPhone,
            $"InstaSafe: order {order.Id} is complete. Thanks for buying safe ✅");
        await TryEmailAsync(buyerEmail, "Order complete — funds released",
            $"<p>Hi {order.CustomerName},</p><p>Order {order.Id} is complete and the vendor has been paid. {payout}</p>");
        await SendVendorEmailAsync(order.VendorId, "Funds released",
            $"<p>{Money(order.AmountKobo)} for order {order.Id} was released. {payout}</p>");
    }

    public async Task RefundedAsync(Order order, string? buyerEmail)
    {
        await TryWaAsync(order.VendorPhone,
            $"InstaSafe: order {order.Id} was refunded. Check your dashboard for full details.");
        await TryWaAsync(order.CustomerPhone,
            $"InstaSafe: order {order.Id} was refunded. Your money is on its way back.");
        await TryEmailAsync(buyerEmail, "Order refunded",
            $"<p>Hi {order.CustomerName},</p><p>Order {order.Id} was refunded. Your money is on its way back.</p>");
        await SendVendorEmailAsync(order.VendorId, "Order refunded",
            $"<p>Order {order.Id} was refunded to the buyer.</p>");
    }

    public async Task DisputeFiledAsync(Order order, string reason)
    {
        await TryWaAsync(order.VendorPhone,
            $"InstaSafe: buyer disputed order {order.Id} ⚠️\nReason: {reason}\nFunds are frozen. Resolve it on your dashboard.");
        await SendVendorEmailAsync(order.VendorId, "Order disputed",
            $"<p>Buyer disputed order {order.Id}.</p><p>Reason: {reason}</p><p>Funds are frozen until you resolve it.</p>");
    }
}
