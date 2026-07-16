namespace InstaSafe.Domain.Exceptions;

public class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string entityName, string fromStatus, string toStatus)
        : base($"Invalid state transition for {entityName}: cannot move from {fromStatus} to {toStatus}.")
    {
    }
}
