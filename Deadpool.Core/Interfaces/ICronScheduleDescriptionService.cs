namespace Deadpool.Core.Interfaces;

/// <summary>
/// Converts limited cron schedule patterns into concise operator-readable text.
/// </summary>
public interface ICronScheduleDescriptionService
{
    string Describe(string cronExpression);
}
