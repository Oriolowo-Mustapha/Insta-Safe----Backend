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
        "Reply with 1, 2 or 3. Type MENU anytime to come back here, CANCEL to stop.";

    public const string AskCustomerName = "Let's create your escrow link.\nWho is the customer? Reply with the customer's full name.";
    public const string AskCustomerPhone = "Got it. What is the customer's phone number? (e.g. 08012345678)";
    public const string AskAddress = "Thanks. What is the delivery address?";
    public const string AskItems =
        "Now the items. Send them in one message, e.g:\n" +
        "2x Sneakers @22500\n" +
        "1x Belt @5000";
    public const string AskAmount = "What is the total amount in naira? (numbers only, e.g. 50000)";

    public const string AskTrackRef = "Please send your order reference (from your payment link or receipt). Type MENU to go back.";

    public const string Help =
        "InstaSafe help 💬\n" +
        "• 1: create a payment link a buyer pays into — we hold the funds.\n" +
        "• 2: track an order with your reference.\n" +
        "• Funds are released to the vendor when the buyer confirms with the OTP.\n" +
        "Type MENU to start.";

    public const string Cancelled = "Cancelled. Nothing was created. Type MENU to start over.";
    public const string SessionExpired = "It's been a while, so I reset our chat. Type MENU to start again.";

    public static string ConfirmSummary(string customerName, string customerPhone, string address, string itemsLines, long totalNgn) =>
        "Please confirm your order:\n" +
        $"Customer: {customerName} ({customerPhone})\n" +
        $"Address: {address}\n" +
        $"Items:\n{itemsLines}\n" +
        $"Total: ₦{totalNgn:N0}\n" +
        "Reply YES to create the payment link, or CANCEL to stop.";

    public static string OrderCreated(long amountKobo, string? paystackAuthUrl) =>
        $"InstaSafe order created ✅\nAmount: ₦{amountKobo / 100:N0}\nPayment link: {paystackAuthUrl}";

    public static string OrderFailed(string error) =>
        $"Sorry, I couldn't create that order: {error}";

    public static string MissingDetails(string missing) =>
        $"I still need: {missing}. Please send it, or type CANCEL to stop.";

    public static string TrackResult(string reference, string status, long amountKobo) =>
        $"Order {reference}\nStatus: {status}\nAmount: ₦{amountKobo / 100:N0}\nCheck your dashboard for full details. Type MENU for more options.";

    public static string TrackNotFound(string reference) =>
        $"I couldn't find order '{reference}'. Check the reference and try again, or type MENU.";
}
