using FCG.Application.Common;
using FCG.Infrastructure.Catalog;
using FCG.Infrastructure.Common;
using FCG.Infrastructure.Identity;
using FCG.Infrastructure.Library;
using FCG.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddPersistenceModule(builder.Configuration)
    .AddCatalogModule()
    .AddLibraryModule();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
