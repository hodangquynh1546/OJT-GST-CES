using ClassFund.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddClassFundServices();

var app = builder.Build();

app.UseClassFundPipeline();

app.Run();

/// <summary>Cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
