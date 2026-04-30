using Deadpool.Core.Domain.Enums;

namespace Deadpool.Core.Domain.ValueObjects;

public record DatabasePulseStatus(
    HealthStatus Status,
    DateTime LastCheckedUtc,
    string? ErrorMessage = null);
