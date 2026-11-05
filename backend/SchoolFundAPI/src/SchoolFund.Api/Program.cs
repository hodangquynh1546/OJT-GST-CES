using SchoolFund.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddSchoolFundServices();

var app = builder.Build();

app.UseSchoolFundPipeline();

app.Run();
