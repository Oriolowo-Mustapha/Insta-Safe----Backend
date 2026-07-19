using InstaSafe.Application.Chatbot.DTOs;
using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Chatbot.Queries.GetChatbotSessions;

public record GetChatbotSessionsQuery() : IRequest<Result<List<ChatbotSessionDto>>>;
