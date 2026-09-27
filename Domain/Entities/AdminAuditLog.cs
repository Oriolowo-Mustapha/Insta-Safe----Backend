namespace InstaSafe.Domain.Entities;

public class AdminAuditLog : Common.BaseEntity
{
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string? Note { get; set; }
}
