using InstaSafe.Application.Chatbot.DTOs;
using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Chatbot.Queries.GetChatbotSession;

public record GetChatbotSessionQuery(Guid SessionId) : IRequest<Result<ChatbotSessionDto>>;
