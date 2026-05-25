using GrpcServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<WorkerCoordinatorService>();

var app = builder.Build();

app.MapGrpcService<DataIngestionService>();
app.MapGrpcService<WorkerCoordinatorService>();
app.MapGrpcService<QueryService>();

app.MapGet("/", () => "Quality Insight gRPC Server is running. 端口: 5001");

app.Run();
