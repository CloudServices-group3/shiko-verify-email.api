# Shiko - Email Verification

This repository contains the Email Verification microservice for the Shiko application.

## Responsibilities

The Email Verification Service is responsible for:

- Generating verification codes.
- Validating email verification codes submitted by users.
- Managing temporary verification code storage and expiration.
- Sending verification codes by email to users through Azure Service Bus and an email provider.
- Publishing validated email verification events to Azure Service Bus that the Authentication Service listens to.

Verification codes are generated and stored temporarily using `IMemoryCache`. Each verification code automatically expires after a configured period of time.

When a user successfully verifies their email address, the service publishes an event to Azure Service Bus. The Authentication microservice consumes this event and updates the user's verification status accordingly.
