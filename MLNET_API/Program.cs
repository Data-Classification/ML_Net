using MLNET_API.Options;
using MLNET_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<PredictionExplanationOptions>(
    builder.Configuration.GetSection(PredictionExplanationOptions.SectionName));
builder.Services.AddSingleton<IPredictionExplanationService, GoogleGenAiPredictionExplanationService>();
builder.Services.AddSingleton<IPredictionApiService, PredictionApiService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
