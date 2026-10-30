# Graph Mail Sender for Umbraco

Send Umbraco email using Microsoft Graph. No backoffice login or custom Program.cs registration is needed.

Choose a version matching Umbraco:

| Umbraco | Package | Framework | Umbraco dependency |
| --- | --- | --- | --- |
| 13 | 13.x.x | .NET 8 | [13.0.0,14.0.0) |
| 17 | 17.x.x | .NET 10 | [17.0.0,18.0.0) |
| 18 | 18.x.x | .NET 10 | [18.0.0,19.0.0) |

```sh
dotnet add package Umbraco.Community.GraphMailSender --version 17.0.0
```

Installation requires no Program.cs changes. The composer activates this sender only when `Umbraco:CMS:Global:Smtp:Graph` exists. Without that section, Umbraco's existing sender remains registered. Configure only one of Graph and OAuth. NuGet may warn rather than reject an incompatible version when the application directly overrides dependencies.

## Configuration

Add the following to appsettings.json, then supply the credential separately:

```json
{
  "Umbraco": {
    "CMS": {
      "Global": {
        "Smtp": {
          "From": "noreply@example.com",
          "Graph": {
            "TenantId": "11111111-1111-1111-1111-111111111111",
            "ClientId": "22222222-2222-2222-2222-222222222222",
            "Mailbox": "noreply@example.com"
          }
        }
      }
    }
  }
}
```

Set `Umbraco__CMS__Global__Smtp__Graph__ClientSecret` in the hosting environment. Local development can use .NET user secrets with the key `Umbraco:CMS:Global:Smtp:Graph:ClientSecret`. JSON values such as `"${SECRET}"` are not expanded automatically.

Alternatively, omit ClientSecret and configure `Graph:CertificatePath` as an absolute PFX path. Register the public certificate on the Entra application, grant the application identity read access to the private certificate file and supply `Graph:CertificatePassword` through a secret provider if needed. Private keys are loaded with ephemeral storage. Configure exactly one credential method. Plan certificate/secret rotation before expiry. Never commit credentials, private keys or PFX files.

Restart after changing settings or credentials. Options validate at application startup and sender resolution. The authentication credential is reused to preserve its token cache. No interactive login or stored refresh token is required.

Smtp.From supplies the default From address; an explicit EmailMessage.From takes precedence. Graph.Mailbox chooses the target postbox by email/UPN or user object GUID. Using a different From address still requires Exchange to authorize the operation. Host, Port, Username and Password are unused by Graph. Pickup-directory delivery is not supported while this sender is active.

## Before sending

An administrator must authorize the app for the intended mailbox. Use Exchange Application RBAC to limit mailbox access; independent broad Entra permissions can defeat that restriction.

The Configuration Health Check verifies settings and token acquisition without sending email. Mailbox permission and delivery require a real send test. Messages above the 3 MiB MIME limit fail explicitly.

See the [complete setup and troubleshooting guide](https://github.com/skttl/umbraco-graph-mail-sender/blob/main/docs/nuget-readme.md). Icon: Tabler mail-bolt, MIT licensed.
