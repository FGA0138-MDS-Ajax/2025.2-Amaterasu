using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using ReportsApi.Configuration;
using ReportsApi.Interfaces;
using ReportsApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddOptions<CosmosOptions>()
	.Bind(builder.Configuration.GetSection("CosmosDB"))
	.ValidateDataAnnotations()
	.Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Cosmos ConnectionString deve ser configurado.");

builder.Services.AddSingleton(sp =>
{
	var options = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;

	var clientOptions = new CosmosClientOptions
	{
		ApplicationName = builder.Environment.ApplicationName,
		ConnectionMode = ConnectionMode.Gateway,
		EnableContentResponseOnWrite = options.EnableContentResponseOnWrite,
		SerializerOptions = new CosmosSerializationOptions
		{
			PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
		}
	};

	return new CosmosClient(options.ConnectionString, clientOptions);
});

builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Incident Reports API",
		Version = "v1",
		Description = "API to manage incident reports stored in Azure Cosmos DB."
	});
});

var app = builder.Build();

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/swagger/v1/swagger.json", "Incident Reports API v1");
	});
}
app.MapControllers();
app.MapHealthChecks("/");

app.Run();
