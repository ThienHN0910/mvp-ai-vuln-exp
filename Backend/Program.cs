using Backend.Services;
using Backend.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
// register infrastructure services
builder.Services.AddSingleton<AstExtractorService>();
builder.Services.AddSingleton<ElasticsearchService>();
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<SecretsDetectService>();

// Gemini depends on IHttpClientFactory and optionally MongoDbService
builder.Services.AddSingleton<GeminiService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseGeminiRateLimit();
app.UseCors("Frontend");
app.MapControllers();

app.Run();
