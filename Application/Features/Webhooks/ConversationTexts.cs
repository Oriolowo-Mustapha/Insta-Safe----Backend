namespace InstaSafe.Application.Features.Webhooks;

public static class ConversationTexts
{
    public const string Welcome =
        "Welcome to InstaSafe 🛡️\n" +
        "Safe escrow for social commerce. Buyer pays, we hold the money, vendor gets paid on delivery confirmation.";

    public const string Menu =
        "What would you like to do?\n" +
        "1️⃣ Create escrow link (new order)\n" +
        "2️⃣ Track an order\n" +
        "3️⃣ Help / talk to support\n" +
        "Reply with 1, 2 or 3 — or just type the full order in one message and I'll pick out the details. " +
        "Type MENU anytime to come back here, CANCEL to stop, BACK to edit the previous answer.";

    public static string MenuWithContinue(int openCount) =>
        "What would you like to do?\n" +
        "1️⃣ Create escrow link (new order)\n" +
        "2️⃣ Track an order\n" +
        "3️⃣ Help / talk to support\n" +
        $"4️⃣ Continue unfinished order ({openCount} saved)\n" +
        "Reply with 1, 2, 3 or 4 — or just type the full order in one message and I'll pick out the details. " +
        "Type MENU anytime to come back here, CANCEL to stop, BACK to edit the previous answer.";

    public const string ProgressSaved =
        "I've saved your progress — pick it up anytime with 4. Continue below.";

    public const string NoDrafts =
        "You have no unfinished orders. Reply 1 to create a new escrow link.";

    public static string DraftList(IReadOnlyList<string> summaries) =>
        "Your unfinished orders:\n" +
        string.Join("\n", summaries.Select((s, i) => $"{i + 1}. {s}")) +
        "\nReply with the number to continue, D+number to discard (e.g. D2), or MENU to go back.";

    public const string DraftDiscarded = "Discarded. Anything else? Type MENU for options.";
    public const string DraftLoaded = "Loaded — continuing where you stopped.";

    public const string AskCustomerName = "Let's create your escrow link.\nWho is the customer? Reply with the customer's full name.";
    public const string AskCustomerPhone = "Got it. What is the customer's phone number? Must be a WhatsApp number (e.g. 08012345678) — the payment link goes there.";
    public const string AskBuyerEmail = "Thanks. What is the buyer's email address? (for the receipt and payment link)";
    public const string AskAddress = "Thanks. What is the delivery address?";
    public const string AskItems =
        "Now the items. Send them in one message, e.g:\n" +
        "2x Sneakers @22500\n" +
        "1x Belt @5000";
    public const string AskAmount = "What is the total amount in naira? (numbers only, e.g. 50000)";
    public const string AskFulfillment =
        "How will this order reach the buyer?\n" +
        "1️⃣ Dispatch rider — we assign a rider, they collect and hand over\n" +
        "2️⃣ Self-delivery — you'll hand it over yourself\n" +
        "Reply 1 or 2.";
    public const string AskDeliveryFee =
        "Agreed delivery fee in naira? (numbers only, e.g. 5000 — send 0 if no dispatch rider)";
    public const string AskDriverPhone =
        "What is the driver's phone number? (e.g. 08055556666 — or send SKIP if you deliver yourself)";
    public const string AskDriverAccount = "What is the driver's account number? (10 digits)";
    public const string AskDriverBankName = "Which bank? Send the bank name, e.g. GTBank, Access, Zenith.";

    public static string DriverDetailsConfirm(string bankName, string accountNumber, string holderName) =>
        $"Please confirm the driver payout details:\n" +
        $"Bank: {bankName}\nAccount: {accountNumber}\nName: {holderName}\n" +
        "Reply YES if this is correct, or CANCEL to stop.";

    public const string AskTrackRef =
        "Please send your order reference (from your payment link or receipt).\n" +
        "Don't have it? Reply LIST and I'll show your recent orders.\n" +
        "Type MENU to go back.";

    public const string Help =
        "InstaSafe help 💬\n" +
        "• 1: create a payment link a buyer pays into — we hold the funds.\n" +
        "• 2: track an order with your reference.\n" +
        "• Funds are released to the vendor when the buyer confirms with the OTP.\n" +
        "Type MENU to start.";

    public const string Cancelled = "Cancelled. Nothing was created. Type MENU to start over.";
    public const string SessionExpired = "It's been a while, so I reset our chat. Type MENU to start again.";

    public static string ConfirmSummary(string customerName, string customerPhone, string address, string itemsLines, long totalNgn) =>
        ConfirmSummary(customerName, customerPhone, address, itemsLines, totalNgn, 0, "");

    public static string ConfirmSummary(
        string customerName, string customerPhone, string address, string itemsLines,
        long totalNgn, long deliveryFeeNgn, string driverPhone, string driverBank = "",
        string driverHolder = "", string buyerEmail = "", string fulfillment = "")
    {
        var feeLine = deliveryFeeNgn > 0 ? $"\nDelivery fee: ₦{deliveryFeeNgn:N0}" : "";
        var fulfillmentLine = string.IsNullOrWhiteSpace(fulfillment) ? "" : $"\nFulfillment: {fulfillment}";
        var driverLine = !string.IsNullOrWhiteSpace(driverPhone)
            ? $"\nDriver: {driverPhone}" + (string.IsNullOrWhiteSpace(driverBank) ? "" : $" ({driverBank})")
                + (!string.IsNullOrWhiteSpace(driverBank) && string.IsNullOrWhiteSpace(driverHolder)
                    ? " — holder unverified, please double-check" : "")
            : "";
        var emailLine = string.IsNullOrWhiteSpace(buyerEmail) ? "" : $"\nBuyer email: {buyerEmail}";
        return "Please confirm your order:\n" +
            $"Customer: {customerName} ({customerPhone})\n" +
            $"Address: {address}\n" +
            $"Items:\n{itemsLines}\n" +
            $"Total: ₦{totalNgn:N0}{feeLine}{fulfillmentLine}{emailLine}\n" +
            $"Buyer pays: ₦{totalNgn + deliveryFeeNgn:N0}{driverLine}\n" +
            "Reply YES to create the payment link, or CANCEL to stop.";
    }

    public static string OrderCreated(long amountKobo, string? paystackAuthUrl) =>
        $"InstaSafe order created ✅\nAmount: ₦{amountKobo / 100:N0}\nPayment link: {paystackAuthUrl}";

    public static string VendorOrderSent(string orderNumber, string customerName, long amountKobo)
    {
        var prefix = string.IsNullOrWhiteSpace(orderNumber) ? "Order" : $"Order {orderNumber}";
        return $"{prefix} created ✅ for {customerName} (₦{amountKobo / 100:N0}) — payment link sent to them on WhatsApp + email.";
    }

    public static string OrderFailed(string error) =>
        $"Sorry, I couldn't create that order: {error}";

    public static string MissingDetails(string missing) =>
        $"I still need: {missing}. Please send it, or type CANCEL to stop.";

    public static string TrackResult(string reference, string status, long amountKobo) =>
        $"Order {reference}\nStatus: {status}\nAmount: ₦{amountKobo / 100:N0}\nCheck your dashboard for full details. Type MENU for more options.";

    public static string TrackNotFound(string reference) =>
        $"I couldn't find order '{reference}'. Check the reference and try again, or type MENU.";
}
