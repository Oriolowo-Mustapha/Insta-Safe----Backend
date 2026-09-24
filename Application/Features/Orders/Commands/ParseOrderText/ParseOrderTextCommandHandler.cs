using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.ParseOrderText;

public class ParseOrderTextCommandHandler : IRequestHandler<ParseOrderTextCommand, Result<ParsedOrder>>
{
    private readonly IGroqParser _parser;

    public ParseOrderTextCommandHandler(IGroqParser parser) => _parser = parser;

    public async Task<Result<ParsedOrder>> Handle(ParseOrderTextCommand req, CancellationToken ct)
    {
        var parsed = await _parser.ParseOrderTextAsync(req.RawText, ct);
        return Result<ParsedOrder>.Success(parsed);
    }
}
