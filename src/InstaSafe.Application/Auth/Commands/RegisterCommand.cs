using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Auth.Commands;

public record RegisterCommand(string FirstName, string LastName, string Email, string Password, string BusinessName, string? Phone, DateTime DateOfBirth) : IRequest<Result<string>>;
