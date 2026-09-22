namespace ScalableOrderProcessing.Services;

public class MockApiOptions
{
    public const string SectionName = "MockApi";

    public string BaseAddress { get; set; } = "";
    // Secret: set it with `dotnet user-secrets set "MockApi:ApiKey" "<key>"` or the MockApi__ApiKey environment variable
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 90;
}
