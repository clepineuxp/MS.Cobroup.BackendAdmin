using MS.Cobroup.BackendAdmin.Core.Services;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IAdministrativeScopeAuthorizer, AdministrativeScopeAuthorizer>();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
