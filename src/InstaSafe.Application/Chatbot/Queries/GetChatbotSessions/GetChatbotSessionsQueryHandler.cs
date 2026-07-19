using InstaSafe.Application.Chatbot.DTOs;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using MediatR;

namespace InstaSafe.Application.Chatbot.Queries.GetChatbotSessions;

public class GetChatbotSessionsQueryHandler : IRequestHandler<GetChatbotSessionsQuery, Result<List<ChatbotSessionDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetChatbotSessionsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<ChatbotSessionDto>>> Handle(GetChatbotSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _unitOfWork.Repository<ChatbotSession>().GetAllAsync(cancellationToken);

        var dtos = sessions
            .OrderByDescending(s => s.LastInteractionAt)
            .Select(s => new ChatbotSessionDto(
                s.Id,
                s.PhoneNumber,
                s.CurrentState.ToString(),
                s.LastInteractionAt,
                s.TemporaryData
            ))
            .ToList();

        return Result<List<ChatbotSessionDto>>.Success(dtos);
    }
}
