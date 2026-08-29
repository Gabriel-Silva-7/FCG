using FCG.Api.Documentation;
using FCG.Api.Errors;
using FCG.Api.Logging;
using FCG.Application.Common;
using FCG.Infrastructure.Catalog;
using FCG.Infrastructure.Common;
using FCG.Infrastructure.Identity;
using FCG.Infrastructure.Library;
using FCG.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApiProblemDetails();
builder.Services.AddApiDocumentation();
builder.Services.AddApiLogging();

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddPersistenceModule(builder.Configuration)
    .AddCatalogModule()
    .AddLibraryModule();

var app = builder.Build();

app.UseApiRequestLogging();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
