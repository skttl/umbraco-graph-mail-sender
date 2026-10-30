# Umbraco.Community.GraphMailSender

![Package icon](icon.png)

Microsoft Graph delivery for Umbraco IEmailSender. See [installation and configuration](docs/nuget-readme.md) and the [Marketplace guide](umbraco-marketplace-readme.md).

## Repository layout

- src/Umbraco.Community.GraphMailSender: sender, typed options, composer and Health Check.
- tests: message fidelity, configuration, transport and Health Check tests.
- docs/nuget-readme.md: packed as README.md in NuGet.
- umbraco-marketplace.json and umbraco-marketplace-readme.md: Marketplace metadata and guide.
- appsettings-schema.Umbraco.Community.GraphMailSender.json and buildTransitive: editor configuration help.
- icon.svg, icon.png and THIRD-PARTY-NOTICES.txt: Tabler mail-bolt and attribution.

## Local test site

[examples/TestSite](examples/TestSite/README.md) hosts a real Umbraco site with local SQLite and a project reference to this package. It follows the selected Umbraco major and uses user secrets for real mailbox configuration.

## Build and verify

Use the .NET 10 SDK with the .NET 8 runtime available for the 13 line. Run from the repository root:

```sh
dotnet restore Umbraco.Community.GraphMailSender.slnx --configfile NuGet.Config
dotnet test Umbraco.Community.GraphMailSender.slnx -c Release --no-restore
dotnet pack src/Umbraco.Community.GraphMailSender/Umbraco.Community.GraphMailSender.csproj -c Release --no-restore -o artifacts
python scripts/verify-package.py artifacts/Umbraco.Community.GraphMailSender.18.0.0.nupkg 18.0.0
```

Tests use fake credentials and HTTP/SMTP boundaries; they do not require or contact a Microsoft tenant. Run a real acceptance send in the target environment before production rollout. Never check credentials into fixtures.

## Version lines and release

main targets Umbraco 18, v17/main targets 17 and v13/main targets 13. Each branch uses the same package ID, version major and a bounded Umbraco dependency interval. The UmbracoMajor MSBuild property also permits local matrix verification from one checkout.

CI checks all three majors on Windows and Linux. Before publishing, create a GitHub environment named release with NUGET_USER set to the NuGet owner's account name. Configure NuGet trusted publishing for owner skttl, repository umbraco-graph-mail-sender, workflow publish.yml, environment release. No long-lived NuGet API key is stored.

Tag the relevant branch with v13.0.0, v17.0.0 or v18.0.0. The workflow verifies branch ancestry, declared branch major, tests, package identity, dependency range and included assets before using GitHub OIDC to publish. Do not reuse a version NuGet has accepted.

No repository, remote or release tag is created by installing this package. [Design](docs/design.md) records the implementation scope.

## License

[MIT](LICENSE). Adapted Tabler mail-bolt icon by Paweł Kuna; see [third-party notice](THIRD-PARTY-NOTICES.txt).
