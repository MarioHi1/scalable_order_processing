using ScalableOrderProcessing;
using ScalableOrderProcessing.Repositories;
using ScalableOrderProcessing.Services;


var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing in appsettings.json");

builder.Services.AddNpgsqlDataSource(connectionString);
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderProcessor, OrderProcessor>();
builder.Services.AddHttpClient<IMockApiClient, MockApiClient>(client =>
{
    client.BaseAddress = new Uri("https://mockapi-ms5j.onrender.com/");
    client.DefaultRequestHeaders.Add("X-Api-Key", "56ef6b6aa58231ac727d5de50b912387");
    client.Timeout = TimeSpan.FromSeconds(90);
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