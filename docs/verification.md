# Verification

## Umbraco 19 RC1, 2026-11-12

The 19 line targets net11.0 and Umbraco 19.0.0-rc1, with package version 19.0.0-beta01 and dependency interval [19.0.0-rc1,20.0.0). The previous main is preserved on v18/main.

Reviewed the [RC announcement](https://umbraco.com/blog/umbraco-19-release-candidate/) and [v19 breaking changes](https://docs.umbraco.com/umbraco-cms/19.latest/get-started/upgrading-and-migrating/version-specific). The package is affected by the .NET 11 requirement and removal of IEmailSender.CanSendRequiredEmail. It does not use Examine, backoffice authentication tokens, property editors or the removed Management/Delivery API endpoints. Runtime async requires no opt-in for this package.

Windows verification uses .NET SDK 11.0.100 installed in the ignored .dotnet directory. The older lines use the system .NET 8/10 runtimes. All four solutions, including the test site, build and their packages pass scripts/verify-package.py:

| Umbraco | Passed | Failed | Skipped | Framework |
| --- | --- | --- | --- | --- |
| 13 | 36 | 0 | 0 | net8.0 |
| 17 | 37 | 0 | 0 | net10.0 |
| 18 | 38 | 0 | 0 | net10.0 |
| 19 RC1 | 44 | 0 | 0 | net11.0 |

New sender checks cover configuration without I/O, token acquisition and a Graph metadata request without sending mail, unsuccessful Graph responses, authentication failure, caller cancellation and the configured default email expiration. Availability checks confirm authentication and Graph reachability, not mailbox send permission or delivery.

Both GitHub Actions workflows pass actionlint 1.7.7. Negative package checks reject missing icons, net10.0 assemblies in the v19 package, incorrect dependency ranges and unsupported or malformed versions. CI includes all four majors on Windows and Linux, and publishing maps v19 tags to main and v18 tags to v18/main. The release tag is v19.0.0-beta01; pushing it triggers the NuGet publishing workflow.

Restore reports NU1902/NU1903 for the existing transitive System.Security.Cryptography.Pkcs 10.0.0 dependency in the 19 package. The tests also report SYSLIB0066 for the existing synchronous IStartupValidator assertion. Older baseline Umbraco versions report additional dependency advisories. Dependency remediation is outside this upgrade.

The test site was built but not started, and no database was opened or migrated. The v19 site selects its own umbraco/Data/v19 directory. Protected configuration files were not inspected. No real tenant, mailbox, backoffice login or email delivery was exercised. Linux/GitHub CI execution remains unverified locally.

## Earlier verification

Verified locally on Windows, 2026-10-30, using .NET SDK 10.0.401 and .NET 8/10 runtimes.

| Umbraco | Passed tests | Build and pack | Package version |
| --- | --- | --- | --- |
| 13 | 36 | Pass | 13.0.0 |
| 17 | 37 | Pass | 17.0.0 |
| 18 | 37 | Pass | 18.0.0 |

The package checker verified the actual nupkg identity, matching Umbraco major interval, target framework and included README, icon, notices, configuration schema and buildTransitive target. Negative artifact checks proved that missing icons and incorrect dependency ranges are rejected.

Configuration schemas and Marketplace metadata validate against their JSON schemas. The configuration schema rejects simultaneous secret and certificate authentication. GitHub Actions files pass actionlint 1.7.7. The two package icons were visually inspected.

## Behavior evidence

| Requirement | Evidence |
| --- | --- |
| Existing SMTP configuration and activation | BindsExistingSmtpSettings; AbsentSectionKeepsExistingSender; ComposerRegistersResolvableSenderWithoutOverridingOtherCredentials |
| Authentication and startup validation | MissingAuthenticationFailsValidation; StartupValidationRejectsMissingCredentials; CertificateCredentialLoadsFromConfiguredPfx; InvalidSettingsFailValidation |
| MIME fidelity and stream ownership | MimePreservesContentRecipientsAndAttachments; ExplicitFromWinsOverDefault; AttachmentReadLimitRejectsOversizedInputAndLeavesStreamOpen |
| Notifications and expiry | HandledNotificationSuppressesDelivery; ExpiredMessageDoesNotReachTransport for 17/18; CancellationInterruptsAnActiveAttachmentRead |
| Health Check without credential disclosure | ValidConfigurationAndTokenReportSuccess; AuthenticationFailureHidesSensitiveDetails; InvalidConfigurationReportsError; DisabledConfigurationReportsInfo |
| Transport | GraphSendsMimeWithBccAndApplicationToken; GraphFailurePropagatesWithoutResponseDetails; OversizedMessageFailsBeforeHttpRequest |

HTTP and SMTP boundaries use test doubles. Both test sites built successfully on all three majors. Both Umbraco 18 sites started and returned HTTP 200 for the installer and backoffice entry point. Interactive installation was not completed. No real Microsoft tenant, mailbox authorization, SMTP TLS handshake, authenticated backoffice UI or message delivery was exercised. CI defines Windows/Linux runs but has not run on GitHub yet. Health Check success confirms token acquisition, not send authorization. NuGet publication and Marketplace ingestion have not run.
