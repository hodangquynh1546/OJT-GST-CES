using SchoolFund.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddSchoolFundServices();

var app = builder.Build();

app.UseSchoolFundPipeline();

app.Run();

/// <summary>Cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
