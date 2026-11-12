# Umbraco.Community.GraphMailSender

![GraphMailSender icon](https://raw.githubusercontent.com/skttl/umbraco-graph-mail-sender/main/icon.png)

Send Umbraco email through Microsoft Graph. Configuration extends the existing SMTP section.

## Install

Choose a version matching Umbraco:

| Umbraco | Package | Framework | Umbraco dependency |
| --- | --- | --- | --- |
| 13 | 13.x.x | .NET 8 | [13.0.0,14.0.0) |
| 17 | 17.x.x | .NET 10 | [17.0.0,18.0.0) |
| 18 | 18.x.x | .NET 10 | [18.0.0,19.0.0) |
| 19 RC1 | 19.0.0-beta01 | .NET 11 | [19.0.0-rc1,20.0.0) |

```sh
dotnet add package Umbraco.Community.GraphMailSender --version 19.0.0-beta01
```

Installation requires no Program.cs changes. The composer activates this sender only when `Umbraco:CMS:Global:Smtp:Graph` exists. Without that section, Umbraco's existing sender remains registered. Configure only one of Graph and OAuth. NuGet may warn rather than reject an incompatible version when the application directly overrides dependencies.

## Configure

Umbraco 19 uses IsEmailConfigured for configuration-only checks. IsEmailAvailableAsync acquires an Entra token and checks the Graph metadata endpoint with a ten-second timeout. It sends no email and does not verify mailbox send permissions. Caller cancellation is propagated; authentication, HTTP and timeout failures report unavailable.

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

## Microsoft Entra and Exchange setup

Register a single-tenant application in the customer's Microsoft Entra tenant and record its tenant and client GUIDs. Configure a secret or certificate.

Grant the application permission to send from the intended mailbox. Prefer Exchange Online RBAC for Applications with a scoped `Application Mail.Send` role. Create the Exchange pointer to the Entra service principal, define a mailbox resource scope, and assign the role to that scope. The service-principal object ID comes from Enterprise Applications, not the App Registration object ID.

Exchange RBAC permissions and Entra application permissions are additive. Do not also grant tenant-wide Graph Mail.Send in Entra if the intention is to restrict access using RBAC. A tenant-wide Mail.Send grant with admin consent is another setup, but grants broader access. Verify both an allowed mailbox and a mailbox outside the scope, including any independent Entra grants.

The package requests `https://graph.microsoft.com/.default` and calls `POST /v1.0/users/{Mailbox}/sendMail`. It does not require Mail.ReadWrite for the supported send path.

Follow [Microsoft's RBAC setup and authorization tests](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac) and [sendMail documentation](https://learn.microsoft.com/en-us/graph/api/user-sendmail?view=graph-rest-1.0).

## Check and troubleshoot

Open Umbraco's Health Checks, Configuration group, and run the Graph mail sender check. It validates settings and requests an access token with a ten-second timeout. A token may come from the credential cache. A success confirms configuration and token acquisition only; it does not prove mailbox authorization, SMTP connectivity or delivery. No test mail is sent and provider exception details are hidden. When unconfigured, the check reports that the sender is inactive. Invalid startup configuration can prevent the application starting; correct it before attempting to use the backoffice.

The sender preserves plain text/HTML, named addresses, Cc/Bcc/ReplyTo and attachments. Attachment streams are read from their current position and remain owned by the caller. With enableNotification, SendEmailNotification runs first; HandleEmail suppresses delivery. Notifications describe the original message, following Umbraco's handled-notification contract.

The complete MIME message is limited to 3 MiB before the outer base64 encoding. Attachment reads are bounded too; MIME attachment encoding and headers count toward the final limit. Larger messages throw explicitly. Upload sessions and large attachment support are not included. HTTP 202 means accepted by Graph, not delivered.

For Umbraco 17/18/19, expires is treated as a cancellation deadline from the start of the send, not a queued-mail TTL. Umbraco 18/19 also uses Smtp.EmailExpiration when expires is omitted. Network timeout or cancellation can happen after remote acceptance, so delivery status may be unknown. There is no automatic retry or background queue.

For 401/authentication errors check credential expiry, tenant/client IDs, certificate access and token resource. For 403/SMTP authorization errors check mailbox scope, Send As rights and protocol policy. Graph 429/503 failures propagate without retries. Never add blanket retry around sends without accounting for duplicates.

This replaces Umbraco's IEmailSender only. Code and packages that send directly through another SMTP client are unaffected.

## License

MIT. The icon adapts Tabler mail-bolt; see THIRD-PARTY-NOTICES.txt for its MIT license.
