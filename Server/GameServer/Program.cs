using GameServer.Administration;
using GameServer.Configuration;
using GameServer.Persistence.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddJsonConsole();
builder.Services.AddGameServer(builder.Configuration);

DatabaseOptions database = builder.Configuration
    .GetSection(GameServerOptions.SectionName)
    .GetSection(nameof(GameServerOptions.Database))
    .Get<DatabaseOptions>() ?? new DatabaseOptions();
builder.Services.AddPostgresCharacterPersistence(database);

// La console d'administration n'est pas enregistrée dans AddGameServer : les tests n'ont pas de terminal.
builder.Services.AddHostedService<AdminConsole>();

using IHost host = builder.Build();

// Le schéma doit exister avant que le serveur n'accepte des connexions.
await host.Services.GetRequiredService<MigrationRunner>().RunAsync();

await host.RunAsync();
