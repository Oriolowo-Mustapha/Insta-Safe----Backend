using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Chatbot.Commands;

public record ProcessChatbotMessageCommand(string PhoneNumber, string MessageText, string MessageType = "chat") : IRequest<Result<string>>;
