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

    public static string MenuWithContinue(int openCount) =>
        "What would you like to do?\n" +
        "1️⃣ Create escrow link (new order)\n" +
        "2️⃣ Track an order\n" +
        "3️⃣ Help / talk to support\n" +
        $"4️⃣ Continue unfinished order ({openCount} saved)\n" +
        "Reply with 1, 2, 3 or 4. Type MENU anytime to come back here, CANCEL to stop.";

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
    public const string AskCustomerPhone = "Got it. What is the customer's phone number? (e.g. 08012345678)";
    public const string AskAddress = "Thanks. What is the delivery address?";
    public const string AskItems =
        "Now the items. Send them in one message, e.g:\n" +
        "2x Sneakers @22500\n" +
        "1x Belt @5000";
    public const string AskAmount = "What is the total amount in naira? (numbers only, e.g. 50000)";
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

    public const string AskTrackRef = "Please send your order reference (from your payment link or receipt). Type MENU to go back.";

    public const string Help =
        "InstaSafe help 💬\n" +
        "• 1: create a payment link a buyer pays into — we hold the funds.\n" +
        "• 2: track an order with your reference.\n" +
        "• Funds are released to the vendor when the buyer confirms with the OTP.\n" +
        "Type MENU to start.";

    public const string Cancelled = "Cancelled. Nothing was created. Type MENU to start over.";
    public const string SessionExpired = "It's been a while, so I reset our chat. Type MENU to start again.";
    public const string AccountDeactivated = "This vendor account is deactivated. Contact InstaSafe support for help.";

    public static string SignupRequired(string baseUrl) =>
        string.IsNullOrWhiteSpace(baseUrl)
            ? "You're not signed up yet — create your InstaSafe vendor account on the web dashboard, verify your email, then come back here."
            : $"You're not signed up yet — create your account here to continue: {baseUrl}/signup";

    public static string OnboardingRequired(string baseUrl) =>
        string.IsNullOrWhiteSpace(baseUrl)
            ? "One more step: add your payout (bank) details on the web dashboard, then come back here."
            : $"One more step: finish setup (payout details) here: {baseUrl}/onboarding";

    public static string ConfirmSummary(string customerName, string customerPhone, string address, string itemsLines, long totalNgn) =>
        ConfirmSummary(customerName, customerPhone, address, itemsLines, totalNgn, 0, "");

    public static string ConfirmSummary(
        string customerName, string customerPhone, string address, string itemsLines,
        long totalNgn, long deliveryFeeNgn, string driverPhone, string driverBank = "",
        string driverHolder = "")
    {
        var feeLine = deliveryFeeNgn > 0 ? $"\nDelivery fee: ₦{deliveryFeeNgn:N0}" : "";
        var driverLine = !string.IsNullOrWhiteSpace(driverPhone)
            ? $"\nDriver: {driverPhone}" + (string.IsNullOrWhiteSpace(driverBank) ? "" : $" ({driverBank})")
                + (!string.IsNullOrWhiteSpace(driverBank) && string.IsNullOrWhiteSpace(driverHolder)
                    ? " — holder unverified, please double-check" : "")
            : "";
        return "Please confirm your order:\n" +
            $"Customer: {customerName} ({customerPhone})\n" +
            $"Address: {address}\n" +
            $"Items:\n{itemsLines}\n" +
            $"Total: ₦{totalNgn:N0}{feeLine}\n" +
            $"Buyer pays: ₦{totalNgn + deliveryFeeNgn:N0}{driverLine}\n" +
            "Reply YES to create the payment link, or CANCEL to stop.";
    }

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
