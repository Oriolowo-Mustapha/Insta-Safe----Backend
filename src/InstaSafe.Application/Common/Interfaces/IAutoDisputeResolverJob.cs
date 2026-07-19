namespace InstaSafe.Application.Common.Interfaces;

public interface IAutoDisputeResolverJob
{
    Task ProcessAsync(Guid disputeId);
}
