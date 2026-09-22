using Microsoft.Extensions.Options;
using ScalableOrderProcessing;
using ScalableOrderProcessing.Repositories;
using ScalableOrderProcessing.Services;


var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing in appsettings.json");

builder.Services.AddNpgsqlDataSource(connectionString);
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderProcessor, OrderProcessor>();

builder.Services.AddOptions<MockApiOptions>()
    .Bind(builder.Configuration.GetSection(MockApiOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.BaseAddress, UriKind.Absolute, out _), "MockApi:BaseAddress must be an absolute URL")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
        "MockApi:ApiKey is missing, set it with: dotnet user-secrets set \"MockApi:ApiKey\" \"<key>\"")
    .Validate(o => o.TimeoutSeconds > 0, "MockApi:TimeoutSeconds must be greater than 0")
    .ValidateOnStart();
builder.Services.AddHttpClient<IMockApiClient, MockApiClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<MockApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseAddress);
    client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .Validate(o => o.PollIntervalSeconds > 0 && o.MaxQueueSize > 0 && o.MaxParallelJobs > 0 && o.JobTimeoutMinutes > 0,
        "Worker options must be greater than 0")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();