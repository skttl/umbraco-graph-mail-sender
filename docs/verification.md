# Verification

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
