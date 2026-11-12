# GraphMailSender test site

This is a real Umbraco site with a project reference to the local package. It is development tooling, is not packed or published, and contains no admin account or mail credentials.

From the repository root:

```sh
dotnet restore --configfile NuGet.Config
dotnet run --project examples/TestSite
```

Open http://localhost:5131/umbraco and complete the installer with your own administrator account. Development mode supplies a SQLite database under umbraco/Data/v19. The database name changes with UmbracoMajor, so switching branches never upgrades another major's database. No SQL Server is required. After installation, /testsite-status reports the Umbraco major, resolved sender type and configuration status in Development only. The launch profile binds to localhost and does not open a browser automatically.

To use another major from the same checkout, run with -p:UmbracoMajor=13, -p:UmbracoMajor=17 or -p:UmbracoMajor=18; include that property in restore too. Both the site and package follow the same value. The v13/main, v17/main and v18/main branches set the matching value by default.

## Configure a real mailbox

Copy the Smtp:Graph configuration from ../../docs/appsettings.example.json into this site's configuration using .NET user secrets or hosting environment variables. The tracked appsettings.json deliberately leaves the provider inactive until valid configuration is supplied. Do not paste real credentials into tracked files.

```sh
dotnet user-secrets set "Umbraco:CMS:Global:Smtp:Graph:TenantId" "YOUR-TENANT-GUID" --project examples/TestSite
dotnet user-secrets set "Umbraco:CMS:Global:Smtp:Graph:ClientId" "YOUR-CLIENT-GUID" --project examples/TestSite
dotnet user-secrets set "Umbraco:CMS:Global:Smtp:Graph:ClientSecret" "YOUR-SECRET" --project examples/TestSite
dotnet user-secrets set "Umbraco:CMS:Global:Smtp:Graph:Mailbox" "noreply@example.com" --project examples/TestSite
dotnet user-secrets set "Umbraco:CMS:Global:Smtp:From" "noreply@example.com" --project examples/TestSite
```

Restart, confirm /testsite-status resolves Umbraco.Community.GraphMailSender.EmailSender, and run the package's Health Check under Configuration. Follow ../../docs/nuget-readme.md to authorize the application in Entra/Exchange. A successful Health Check does not send email or prove mailbox rights.

For an actual delivery check, use Umbraco's forgotten-password flow for your own test administrator email, then check the destination inbox and provider logs. Do not enable retries around a send whose remote acceptance is unknown. There is no unauthenticated test-mail endpoint.

Keep this site local. Production deployment, starter content and a custom mail-testing UI are outside its scope. Local databases, logs, generated schemas and credentials are ignored by Git.
