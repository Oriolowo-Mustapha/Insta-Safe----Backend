using InstaSafe.Application.Chatbot.DTOs;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using MediatR;

namespace InstaSafe.Application.Chatbot.Queries.GetChatbotSession;

public class GetChatbotSessionQueryHandler : IRequestHandler<GetChatbotSessionQuery, Result<ChatbotSessionDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetChatbotSessionQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ChatbotSessionDto>> Handle(GetChatbotSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _unitOfWork.Repository<ChatbotSession>().GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
            return Result<ChatbotSessionDto>.Failure("Chatbot session not found.");

        var dto = new ChatbotSessionDto(
            session.Id,
            session.PhoneNumber,
            session.CurrentState.ToString(),
            session.LastInteractionAt,
            session.TemporaryData
        );

        return Result<ChatbotSessionDto>.Success(dto);
    }
}
