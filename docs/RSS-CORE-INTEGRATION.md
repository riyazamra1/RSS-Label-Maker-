# RSS Core integration contract — RSS Label Maker

**Status:** Required architecture. This document separates existing RSS Core behavior from missing work. It does not claim that missing APIs are implemented or deployed.

## Authority and ownership

- Production Core URL: `https://rsscore.cv`.
- Project identifier intended for registration: `rss-label-maker`. This key must be registered as an active RSS Core project before production registration can succeed.
- RSS Core owns the canonical account profile, email verification, app registration/license, license keys, subscriptions/entitlements, device activations, shared-user seats, usage quota ledger and security audit events.
- Use the existing central customer account database. Do not create a separate Label Maker account/authentication database or deploy the deprecated Worker under `cloudflare/auth-worker`.
- Label Maker local SQLite stores product and label content. Google Drive stores encrypted backup archives under the user's explicit Google OAuth authorization. RSS Core may store backup metadata/ownership and entitlements, not the Drive file contents.

## Existing RSS Core implementation observed in source

The following routes exist in the RSS Core `main` source at the time this contract was prepared. Their production availability still needs a live health/functional test.

| Route | Existing behavior |
|---|---|
| `POST /api/v1/license/register` (legacy alias `POST /v1/license/register`) | Creates/updates a central customer by email/display name, creates a Free app license if absent, records device ID, sends email verification link for unverified customer. Requires `project_key`, `device_id`, `terms_accepted=true`. |
| `GET /v1/customer/verify-email?token=...` | Consumes one-time verification token and marks central customer email verified. |
| `POST /api/v1/license/session` | Checks email, project key, device ID and app key; records device and returns current license. This is not a password-authenticated customer session and must not be treated as sufficient secure login by itself. |
| `GET /api/v1/license/verification-status?email=...&project_key=...` | Returns central registration/email verification and license status. |
| `POST /api/v1/license/resend-verification` | Issues a new email verification link. |
| `GET /api/v1/entitlements/check?app_key=...&email=...` | Checks existing Premium entitlement; this is not a complete feature-flag/quota contract. |
| `POST /api/v1/payments/route` | Existing signed-in RSS account payment routing for the one-year Premium plan. |

The existing customer registration/session routes currently do not provide the complete secure email/password login, bearer-session lifecycle, license-key redemption, device-slot enforcement, shared-user seat management or daily quota accounting required by RSS Label Maker.

## Required RSS Core additions before client integration

1. **Customer account authentication**
   - Add secure password credential storage (salted password hash only) and customer sessions to the central account system.
   - Add register, login, current-session, logout, password-reset and token-refresh/revoke APIs.
   - Registration must require email verification before the Label Maker main screen is unlocked.
   - Bind account identity to the central customer record; never create a second project-specific user database.
2. **Project registration**
   - Register `rss-label-maker` as an active project and issue its public app key / project configuration.
   - The desktop app may contain public app identifiers, never admin/API/payment secrets.
3. **License-key redemption**
   - Provide authenticated key redemption against RSS Core.
   - Keys are random, one-time or explicitly reusable according to server policy; store only a secure hash of redeemable key material.
   - A key must grant an entitlement only after server-side validation. Client-side format checks never activate a license.
   - Link payment records to the entitlement; Payments.lk success is confirmed only by verified, idempotent webhook handling.
4. **Entitlement document**
   - Return a signed app entitlement containing account ID, project key, plan key, status, validity dates, feature flags, daily print quota, device limit, shared-user seat limit, backup/restore permission and offline grace policy.
   - The server remains authoritative. Client-supplied plan, quota, price, device count and feature flags are untrusted.
5. **Device activations**
   - Upgrade existing `customer_devices` behavior to enforce per-entitlement activation slots.
   - Add activation/deactivation, list devices, last-seen, device replacement and recovery APIs.
   - Use a generated stable device ID and privacy-preserving fingerprint; do not upload raw hardware serials.
6. **Shared users**
   - Add owner-scoped invitations, acceptance, revocation, role assignment and seat enforcement.
   - Shared users use their own central RSS accounts. A user seat and a PC device slot are separate limits.
7. **Daily label quota**
   - Add an atomic server-side usage ledger keyed by account, project and service-local calendar date.
   - Count completed print operations / labels according to a clearly documented rule. Preview and cancelled jobs do not count.
   - Use idempotency keys per print job to prevent retries or replay from consuming quota multiple times.
   - Return used, remaining, limit and next reset timestamp. Define timezone centrally (default Asia/Colombo only if RSS Core confirms this project setting).
   - Support signed bounded offline quota grants and reconcile usage events when reconnected. Define behavior for clock rollback, duplicate events and offline grace expiry.
8. **Backup metadata**
   - Store account/workspace ownership, Drive provider, opaque Drive file ID, archive version, checksum, size and last backup/restore timestamps as metadata only.
   - Google OAuth tokens and backup encryption keys must not be stored as plaintext in RSS Core or the app database.

## Desktop client requirements

- Use RSS Core HTTPS APIs only for account, verification, licensing, devices, shared users, quota and entitlement.
- Protect device/session credentials with Windows Credential Manager / DPAPI.
- Show the user's current Free/Premium status, expiry, daily label usage, device slots and shared-user seats.
- Provide a license-key entry screen and an explicit Upgrade action.
- Do not claim Premium from a checkout return URL; refresh entitlement from RSS Core.
- Preserve local data on sign-out, license expiry, network errors and restore errors.
- Offline behavior is bounded by the signed entitlement and cached quota. A disconnected PC cannot immediately learn about remote revocation.

## Payments baseline

- Use the existing RSS Core Payments module and Payments.lk integration; do not build a separate RSS Pay service.
- Existing accepted plan baseline is `RSS_PREMIUM_1_YEAR`, one-time payment for 12 months, not recurring monthly billing.
- Price is server-configured and has not yet been selected.
- Payment fulfillment happens only after RSS Core verifies the signed webhook, amount, currency, account linkage and idempotency.

## Google Drive backup design

- Use Google OAuth 2.0 Authorization Code + PKCE from the Windows app; no embedded client secret.
- Request the minimum Drive scope needed for a dedicated RSS Label Maker backup folder. Never scan unrelated user files.
- Encrypt the archive locally using authenticated encryption before upload. Keep key recovery separate from the archive; do not upload the encryption key beside the archive.
- Archive contains local database, product data, label designs, templates and referenced assets that the user's plan permits. It excludes passwords, Core tokens, license keys and Google refresh tokens.
- Restore verifies account/workspace ownership, archive integrity and schema compatibility, then previews contents and creates a local safety backup before merge/replace.
- First release: manual backup and restore. Scheduled backup follows only after manual restore is validated.

## Free plan restrictions confirmed so far

- Configurable daily label-print quota.
- No PDF export.
- No saving reusable Import By / Distributed By master profiles.
- Other restrictions are server-controlled feature flags and must be approved in a plan matrix before release. Do not silently hard-code additional limits.

## Unresolved product settings — do not guess

- Free daily print quota (number of labels per account per day).
- Whether quota is counted per account, workspace or device (recommended: account/workspace, shared across activated devices).
- Exact paid device options (single PC and multi-PC counts).
- Exact shared-user seat options and role set.
- Premium feature matrix beyond PDF export, reusable Import By / Distributed By profiles, backup/restore and multi-device/shared users.
- Annual Premium price in LKR.
- Offline grace duration and offline print allowance.

## Release gates

- Confirm the RSS Core project key is registered.
- Implement and test the missing Core APIs before integrating desktop calls.
- Verify email registration, login, password recovery and session revocation against production Core.
- Verify license key redemption, device-slot enforcement, seat enforcement and quota idempotency.
- Verify Google Drive OAuth, encrypted backup and full restore round-trip.
- Build and test the Windows app on Windows 10/11.
- Production Core deployment and DNS changes require separate explicit approval. A repository commit is not proof of deployment.
