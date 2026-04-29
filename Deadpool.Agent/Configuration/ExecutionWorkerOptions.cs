namespace Deadpool.Agent.Configuration;

public class ExecutionWorkerOptions
{
    // Maximum time a job can remain in Running state before being considered stale/abandoned.
    // If an executor crashes during backup execution, the job will be stuck in Running state.
    // After this threshold, another executor can reclaim and retry the job.
    public TimeSpan StaleJobThreshold { get; set; } = TimeSpan.FromHours(2);
}
