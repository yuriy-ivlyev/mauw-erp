using Mauw.Erp.Contracts;
using Mauw.Erp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<SdiXmlInvoiceParser>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapPost("/api/v1/inventory/preview-sdi-xml",
    async (IFormFile file, SdiXmlInvoiceParser parser, CancellationToken cancellationToken) =>
    {
        if (file.Length == 0)
        {
            return Results.BadRequest("Il file XML è vuoto.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xml", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Il file deve avere estensione .xml.");
        }

        await using var fileStream = file.OpenReadStream();
        var preview = await parser.ParseAsync(fileStream, cancellationToken);
        return Results.Ok(preview);
    })
    .Accepts<IFormFile>("multipart/form-data")
    .Produces<SdiImportPreviewDto>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest);

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
