namespace ScalableOrderProcessing;

public class WorkerOptions
{
    public const string SectionName = "Worker";

    public int PollIntervalSeconds { get; set; } = 10;
    public int MaxQueueSize { get; set; } = 20;
    public int MaxParallelJobs { get; set; } = 5;
    public int JobTimeoutMinutes { get; set; } = 5;
}
