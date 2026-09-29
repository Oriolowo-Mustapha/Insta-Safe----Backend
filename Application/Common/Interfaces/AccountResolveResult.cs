namespace InstaSafe.Application.Common.Interfaces;

public enum ResolveFailureKind
{
    Invalid = 0,
    Unavailable = 1
}

public sealed record AccountResolveResult(
    bool Success,
    string? AccountName,
    ResolveFailureKind FailureKind,
    string Error);
