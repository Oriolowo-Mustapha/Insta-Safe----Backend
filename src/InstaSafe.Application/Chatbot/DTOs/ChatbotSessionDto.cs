namespace InstaSafe.Application.Chatbot.DTOs;

public record ChatbotSessionDto(
    Guid Id,
    string PhoneNumber,
    string CurrentState,
    DateTime LastInteractionAt,
    string? TemporaryData
);
