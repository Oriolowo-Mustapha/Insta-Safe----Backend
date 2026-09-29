namespace InstaSafe.Application.Features.Admin.DTOs;

public sealed record AdminStatsDto(
    int VendorTotal,
    int VendorActive,
    Dictionary<string, long> OrdersByStatus,
    long HeldGmvKobo,
    long ReleasedTodayKobo,
    int OpenDisputes,
    int OpenDraftTickets,
    int FailedWebhooks24h,
    int OutboxBacklog);
