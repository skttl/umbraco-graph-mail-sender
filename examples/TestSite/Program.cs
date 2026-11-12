using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.DataProtection;
using Umbraco.Cms.Core.Mail;

var builder = WebApplication.CreateBuilder(args);
#if UMBRACO_13
const int major = 13;
#elif UMBRACO_17
const int major = 17;
#elif UMBRACO_18
const int major = 18;
#else
const int major = 19;
#endif

if (builder.Environment.IsDevelopment())
{
    // Keep databases separate when switching supported Umbraco majors in the same checkout.
    var data = Path.Combine(builder.Environment.ContentRootPath, "umbraco", "Data", $"v{major}");
    Directory.CreateDirectory(data);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(data, "keys")));
    var connectionString = $"Data Source={Path.Combine(data, "Umbraco.sqlite.db")};Cache=Shared;Foreign Keys=True;Pooling=True";
    // Umbraco expects a configured database to exist before it can offer the schema installer.
    using (var connection = new SqliteConnection(connectionString)) connection.Open();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:umbracoDbDSN"] = connectionString,
        ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite"
    });
}

builder.CreateUmbracoBuilder().AddBackOffice().AddWebsite().AddComposers().Build();
var app = builder.Build();
await app.BootUmbracoAsync();

if (app.Environment.IsDevelopment())
    app.MapGet("/testsite-status", (IEmailSender sender) => new
    {
        UmbracoMajor = major,
        Sender = sender.GetType().FullName,
#if UMBRACO_19
        CanSend = sender.IsEmailConfigured()
#else
        CanSend = sender.CanSendRequiredEmail()
#endif
    });

app.UseUmbraco()
    .WithMiddleware(u => { u.UseBackOffice(); u.UseWebsite(); })
    .WithEndpoints(u =>
    {
#if UMBRACO_13
        u.UseInstallerEndpoints();
#endif
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
