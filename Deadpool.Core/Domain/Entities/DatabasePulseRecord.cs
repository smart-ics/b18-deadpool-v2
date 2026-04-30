using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.Entities;

public class DatabasePulseRecord
{
    public int Id { get; set; }
    public DateTime CheckTime { get; }
    public HealthStatus Status { get; }
    public string? ErrorMessage { get; }

    public DatabasePulseRecord(DateTime checkTime, HealthStatus status, string? errorMessage = null)
    {
        CheckTime = checkTime;
        Status = status;
        ErrorMessage = errorMessage;
    }
}
