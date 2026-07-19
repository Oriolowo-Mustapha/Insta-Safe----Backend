using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Chatbot.Commands;

public record ProcessChatbotMessageCommand(string PhoneNumber, string MessageText) : IRequest<Result<string>>;
