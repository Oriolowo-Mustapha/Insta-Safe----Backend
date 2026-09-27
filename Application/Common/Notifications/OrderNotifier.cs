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
    private readonly string _frontendBaseUrl;

    public OrderNotifier(
        IWhatsAppSender wa, IEmailSender email, IVendorRepository vendors,
        ILogger<OrderNotifier> logger, Microsoft.Extensions.Configuration.IConfiguration? config = null)
    {
        _wa = wa; _email = email; _vendors = vendors; _logger = logger;
        _frontendBaseUrl = (config?["Frontend:BaseUrl"] ?? "").TrimEnd('/');
    }

    private string TrackUrl(Order order) =>
        string.IsNullOrWhiteSpace(_frontendBaseUrl) || string.IsNullOrWhiteSpace(order.OrderNumber)
            ? ""
            : $"{_frontendBaseUrl}/track/{order.OrderNumber}";

    private string TrackLine(Order order)
    {
        var url = TrackUrl(order);
        return string.IsNullOrEmpty(url) ? "" : $"\nTrack it live here: {url}";
    }

    private string TrackHtml(Order order)
    {
        var url = TrackUrl(order);
        return string.IsNullOrEmpty(url)
            ? ""
            : $"<p>Track it live here: <a href=\"{url}\">{url}</a></p>";
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

    private static string Num(Order order) =>
        string.IsNullOrWhiteSpace(order.OrderNumber) ? order.Id.ToString()[..8] : order.OrderNumber;

    private static string PayRef(Order order) =>
        string.IsNullOrWhiteSpace(order.PaystackReference) ? "" : $"\n(Payment ref: {order.PaystackReference})";

    public async Task PaymentLinkAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerPhone) || string.IsNullOrWhiteSpace(order.PaystackAuthUrl)) return;
        var name = string.IsNullOrWhiteSpace(order.CustomerName) ? "there" : order.CustomerName;
        await TryWaAsync(order.CustomerPhone,
            $"Hi {name}, order {order.OrderNumber} — your InstaSafe payment link for {Money(order.AmountKobo)}:\n{order.PaystackAuthUrl}\n" +
            "Pay now — your money stays locked in escrow until you confirm delivery." + TrackLine(order));
    }

    public async Task OrderCreatedAsync(Order order, string? buyerEmail)    {
        var link = order.PaystackAuthUrl ?? "";
        await TryEmailAsync(buyerEmail, $"Pay for order {Num(order)} ({Money(order.AmountKobo)})",
            $"<p>Hi {order.CustomerName},</p><p>Your order <b>{Num(order)}</b> totals <b>{Money(order.AmountKobo)}</b>.</p><p><a href=\"{link}\">Pay securely with InstaSafe</a></p><p>Funds stay in escrow until you confirm delivery.</p>{TrackHtml(order)}{PayRef(order)}");
        await SendVendorEmailAsync(order.VendorId, "New order created",
            $"<p>Order {Num(order)} for <b>{Money(order.AmountKobo)}</b> was created. Share the payment link with your buyer.</p><p>Check your dashboard for full details.</p>");
    }

    public async Task FundsHeldAsync(Order order, string otpCode, string? buyerEmail)
    {
        await TryEmailAsync(buyerEmail, $"Payment received for order {Num(order)} — escrow holding your funds",
            $"<p>Hi {order.CustomerName},</p><p>We received <b>{Money(order.AmountKobo)}</b> for order <b>{Num(order)}</b>.</p><p>Your delivery code is <b>{otpCode}</b>. Share it with the rider only when you receive your item.</p>{TrackHtml(order)}{PayRef(order)}");
    }

    public async Task DriverAssignedAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.DriverPhone)) return;
        await TryWaAsync(order.DriverPhone,
            $"InstaSafe delivery assigned 🚚\nOrder {Num(order)}\nDeliver to: {order.DeliveryAddress}\nFee: {Money(order.DeliveryFeeKobo)}\nLog in to the driver portal to confirm on arrival. Check your dashboard for full details.");
    }

    public async Task BankTransferDetailsAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.PayVirtualAccountNumber)) return;
        var amount = Money(order.AmountKobo);
        await TryWaAsync(order.CustomerPhone,
            $"InstaSafe: pay {amount} by bank transfer to complete order {Num(order)}:\n" +
            $"Bank: {order.PayVirtualAccountBank}\nAccount: {order.PayVirtualAccountNumber}\n" +
            $"Name: {order.PayVirtualAccountName}\nTransfer EXACTLY {amount} — your payment is confirmed automatically." + TrackLine(order));
        await TryEmailAsync(order.BuyerEmail, $"Bank transfer details for order {Num(order)} ({amount})",
            $"<p>Hi {order.CustomerName},</p><p>Pay <b>{amount}</b> by bank transfer for order <b>{Num(order)}</b>:</p>" +
            $"<p>Bank: <b>{order.PayVirtualAccountBank}</b><br/>Account: <b>{order.PayVirtualAccountNumber}</b><br/>Name: {order.PayVirtualAccountName}</p>" +
            $"<p>Transfer exactly {amount} — your payment is confirmed automatically and held in escrow.</p>{TrackHtml(order)}");
    }

    public async Task DeliveredAsync(Order order, string? buyerEmail)
    {
        var window = "You have 24 hours to inspect. If anything is wrong, tap Dispute on your order page — otherwise funds release automatically.";
        await TryWaAsync(order.VendorPhone,
            $"InstaSafe: order {Num(order)} marked DELIVERED ✅\n{window}\nCheck your dashboard for full details.");
        await TryWaAsync(order.CustomerPhone,
            $"InstaSafe: your order {Num(order)} arrived ✅\n{window}");
        await TryEmailAsync(buyerEmail, $"Order {Num(order)} delivered — 24h inspection window",
            $"<p>Hi {order.CustomerName},</p><p>Order <b>{Num(order)}</b> was marked delivered.</p><p>{window}</p>{TrackHtml(order)}{PayRef(order)}");
        await SendVendorEmailAsync(order.VendorId, $"Order {Num(order)} delivered",
            $"<p>Order <b>{Num(order)}</b> was marked delivered. {window}</p>");
    }

    public async Task ReleasedAsync(Order order, string? buyerEmail, string? transferRef)
    {
        var payout = transferRef is null ? "Payout is being processed." : $"Transfer ref: {transferRef}.";
        await TryWaAsync(order.VendorPhone,
            $"InstaSafe: funds released ✅ {Money(order.AmountKobo)} for order {Num(order)}. {payout}\nCheck your dashboard for full details.");
        await TryWaAsync(order.CustomerPhone,
            $"InstaSafe: order {Num(order)} is complete. Thanks for buying safe ✅");
        await TryEmailAsync(buyerEmail, $"Order {Num(order)} complete — funds released",
            $"<p>Hi {order.CustomerName},</p><p>Order <b>{Num(order)}</b> is complete and the vendor has been paid. {payout}</p>{TrackHtml(order)}{PayRef(order)}");
        await SendVendorEmailAsync(order.VendorId, $"Funds released for order {Num(order)}",
            $"<p>{Money(order.AmountKobo)} for order <b>{Num(order)}</b> was released. {payout}</p>");
    }

    public async Task RefundedAsync(Order order, string? buyerEmail)
    {
        await TryWaAsync(order.VendorPhone,
            $"InstaSafe: order {Num(order)} was refunded. Check your dashboard for full details.");
        await TryWaAsync(order.CustomerPhone,
            $"InstaSafe: order {Num(order)} was refunded. Your money is on its way back.");
        await TryEmailAsync(buyerEmail, $"Order {Num(order)} refunded",
            $"<p>Hi {order.CustomerName},</p><p>Order <b>{Num(order)}</b> was refunded. Your money is on its way back.</p>{TrackHtml(order)}{PayRef(order)}");
        await SendVendorEmailAsync(order.VendorId, $"Order {Num(order)} refunded",
            $"<p>Order <b>{Num(order)}</b> was refunded to the buyer.</p>");
    }

    public async Task DisputeFiledAsync(Order order, string reason)
    {
        await TryWaAsync(order.VendorPhone,
            $"InstaSafe: buyer disputed order {Num(order)} ⚠️\nReason: {reason}\nFunds are frozen. Resolve it on your dashboard.");
        await SendVendorEmailAsync(order.VendorId, $"Order {Num(order)} disputed",
            $"<p>Buyer disputed order <b>{Num(order)}</b>.</p><p>Reason: {reason}</p><p>Funds are frozen until you resolve it.</p>");
    }
}
