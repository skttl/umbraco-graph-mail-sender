# GraphMailSender implementation plan

Goal: deliver Umbraco.Community.GraphMailSender for Umbraco 13, 17 and 18.
Architecture: typed SMTP options, cached Entra credentials, IEmailSender integration and a single HTTP transport.
Tech stack: .NET 8/10, Umbraco Core, Azure.Identity, MimeKit, xUnit.
Spec: ../../design.md

## Constraints and review focus
No credentials in tracked config; reject conflicting providers, missing secrets, malformed addresses and insecure SMTP. Notifications can suppress delivery. Caller streams remain open. Remote failures propagate; never retry ambiguous sends. Version dependency intervals must match each package major.

## Tasks
- [x] Add behavioral tests for MIME fidelity, startup validation, registration, notifications, expiry and Graph HTTP request and failures; prove missing behavior fails.
- [x] Implement Options.cs, Registration.cs, EmailSender.cs and MessageConversion.cs under src/Umbraco.Community.GraphMailSender; pass the tests against all three majors.
- [x] Add per-channel documentation, marketplace metadata, schema and buildTransitive target, icon and licenses, CI and publish workflows.
- [x] Run all tests, build and pack each major, inspect nupkg contents and dependencies, validate schemas and workflows.
- [x] Review final source; create local Git repositories with main, v17/main and v13/main. Remote creation/publication depends on GitHub authentication and NuGet policy setup.

- [x] Add examples/TestSite with project reference, per-major SQLite, secret guidance, local launch profile, build and HTTP startup checks.
