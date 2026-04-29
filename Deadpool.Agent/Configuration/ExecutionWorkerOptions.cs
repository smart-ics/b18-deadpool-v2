namespace Deadpool.Agent.Configuration;

public class ExecutionWorkerOptions
{
    public TimeSpan StaleJobThreshold { get; set; } = TimeSpan.FromMinutes(30);
}
