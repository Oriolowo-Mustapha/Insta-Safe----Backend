namespace InstaSafe.Domain.Enums;

public enum ChatbotState
{
    Idle,
    AwaitingOrderAmount,
    AwaitingOrderDescription,
    AwaitingOrderBuyerEmail,
    ConfirmingOrder,
    AwaitingOrderStatusReference
}
