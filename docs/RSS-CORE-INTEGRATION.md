## RSS ecosystem placement and distribution
- RSS Label Maker (RLM) is an official product in the RSS ecosystem. It must be managed through the existing RSS Core Portal and presented on the existing public website at https://rssapps.net.
- Do not create a separate RLM portal, standalone account database, separate licensing authority, or separate public website/domain.
- RSS Core Portal: provide the authorized product administration and customer account workflows for RLM, including product configuration, user registration and email verification, the central user database, 14-day trial eligibility/status, license plans and administrator-configurable prices, license-key issuance/redemption, annual/lifetime entitlements, Payments.lk payment fulfillment, device activations, network shared-user seats, and audit history.
- rssapps.net: provide a public RLM product page with product description, features, screenshots, system requirements, proposed pricing, trial terms, and clear registration/purchase calls to action. Registration and purchase must route into the existing RSS Core identity, licensing and payment workflows; do not duplicate accounts or payment records.
- Keep RLM product branding and customer-facing information consistent between RSS Core Portal and rssapps.net. RSS Core remains the source of truth for all user, license, trial, pricing and entitlement data.
- The native Windows RLM application consumes RSS Core APIs for identity and entitlements. The public website must not expose administrative APIs, secrets, internal service URLs or private customer data.

# RSS Core integration contract — RSS Label Maker

**Status:** Required architecture. This document separates existing RSS Core behavior from missing work. It does not claim that missing APIs are implemented or deployed.

## Authority and ownership

- Production Core URL: `https://rsscore.cv`.
- Project identifier intended for registration: `rss-label-maker`. This key must be registered as an active RSS Core project before production registration can succeed.
- RSS Core owns the canonical account profile, email verification, app registration/license, license keys, subscriptions/entitlements, device activations, shared-user seats, trial state and security audit events.
- Use the existing central customer account database. Do not create a separate Label Maker account/authentication database or deploy the deprecated Worker under `cloudflare/auth-worker`.
- Label Maker local SQLite stores product and label content. Google Drive stores encrypted backup archives under the user's explicit Google OAuth authorization. RSS Core may store backup metadata/ownership and entitlements, not the Drive file contents.

## Existing RSS Core implementation observed in source

The following routes exist in the RSS Core `main` source at the time this contract was prepared. Their production availability still needs a live health/functional test.

| Route | Existing behavior |
|---|---|
| `POST /api/v1/license/register` (legacy alias `POST /v1/license/register`) | Creates/updates a central customer by email/display name, creates a 14-day Premium Trial entitlement if eligible (the current Free-license behavior must be replaced), records device ID, sends email verification link for unverified customer. Requires `project_key`, `device_id`, `terms_accepted=true`. |
| `GET /v1/customer/verify-email?token=...` | Consumes one-time verification token and marks central customer email verified. |
| `POST /api/v1/license/session` | Checks email, project key, device ID and app key; records device and returns current license. This is not a password-authenticated customer session and must not be treated as sufficient secure login by itself. |
| `GET /api/v1/license/verification-status?email=...&project_key=...` | Returns central registration/email verification and license status. |
| `POST /api/v1/license/resend-verification` | Issues a new email verification link. |
| `GET /api/v1/entitlements/check?app_key=...&email=...` | Checks existing Premium entitlement; this is not a complete feature-flag/quota contract. |
| `POST /api/v1/payments/route` | Existing signed-in RSS account payment routing; extend fulfillment to annual and lifetime RLM licenses. |

The existing customer registration/session routes currently do not provide the complete secure email/password login, bearer-session lifecycle, 14-day trial issuance/expiry, annual/lifetime license-key redemption, device-slot enforcement or network shared-user seat management required by RSS Label Maker.

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
   - Return a signed app entitlement containing account ID, project key, plan key, status, validity dates, feature flags, trial start/end and watermark state, feature flags, device limit, network shared-user seat limit, backup/restore permission and offline grace policy.
   - The server remains authoritative. Client-supplied plan, trial dates, price, currency, device count, user seats and feature flags are untrusted.
5. **Device activations**
   - Upgrade existing `customer_devices` behavior to enforce per-entitlement activation slots.
   - Add activation/deactivation, list devices, last-seen, device replacement and recovery APIs.
   - Use a generated stable device ID and privacy-preserving fingerprint; do not upload raw hardware serials.
6. **Shared users**
   - Add owner-scoped invitations, acceptance, revocation, role assignment and seat enforcement.
   - Shared users use their own central RSS accounts. A user seat and a PC device slot are separate limits.
7. **14-day trial and expiry**
   - Replace automatic Free-license creation with one 14-day Premium Trial entitlement after verified registration and first successful RLM activation.
   - Enforce one eligible trial per central account/project; reinstalling or creating a new local device profile must not reset the trial.
   - Return authoritative trial start/end timestamps and a signed entitlement with full Premium feature flags plus a mandatory RSS TRIAL page-watermark flag.
   - Trial allows Premium print quality, PDF export and other Premium features during the 14-day period; watermark every printed/PDF page.
   - At expiry, prevent new print/PDF and Premium-only operations until a paid entitlement is active. Preserve all local data; do not downgrade to a permanent Free plan.
   - Use server timestamps and signed cached entitlements; the PC clock cannot extend the trial. Offline use ends at signed entitlement expiry or configured offline grace, whichever occurs first.

8. **Backup metadata**
   - Store account/workspace ownership, Drive provider, opaque Drive file ID, archive version, checksum, size and last backup/restore timestamps as metadata only.
   - Google OAuth tokens and backup encryption keys must not be stored as plaintext in RSS Core or the app database.

## Desktop client requirements

- Use RSS Core HTTPS APIs only for account, verification, trial/paid licensing, devices, network shared users and entitlement.
- Protect device/session credentials with Windows Credential Manager / DPAPI.
- Show the user's current Trial/Single PC/Shared License/Multiple PC (Network) status, trial end or paid validity, license term, purchase currency/price, device slots and network-user seats.
- Provide a license-key entry screen and an explicit Upgrade action.
- Do not claim Premium from a checkout return URL; refresh entitlement from RSS Core.
- Preserve local data on sign-out, license expiry, network errors and restore errors.
- Offline behavior is bounded by the signed entitlement and configured offline grace. A disconnected PC cannot immediately learn about remote revocation.

## Payments and proposed pricing baseline

- Use the existing RSS Core Payments module and Payments.lk integration; do not build a separate RSS Pay service.
- Support annual (12-month) and lifetime (one-time) license terms; no monthly recurring billing or automatic renewal is assumed.
- Support independently configured LKR and USD prices. Proposed prices: Single PC LKR 5,000/$25 annual and LKR 12,000/$60 lifetime; Shared License (one owner, 3 PCs) LKR 9,000/$50 annual and LKR 22,000/$120 lifetime; Multiple PC (Network, 5 PCs) LKR 18,000/$100 annual and LKR 40,000/$220 lifetime.
- Proposed add-ons: Shared License extra PC LKR 2,000/$12.50 annual and LKR 5,000/$30 lifetime; Network extra PC LKR 3,250/$18.75 annual and LKR 7,000/$40 lifetime. Network user seats are separate from PC slots; their included count and any additional-user fee remain administrator-configurable.
- All prices remain proposals until configured/approved by RSS Core Admin. Store a purchase price/currency snapshot; future price changes must not alter existing customer licenses.
- Payment fulfillment happens only after RSS Core verifies the signed webhook, amount, currency, account linkage and idempotency.

## Google Drive backup design

- Use Google OAuth 2.0 Authorization Code + PKCE from the Windows app; no embedded client secret.
- Request the minimum Drive scope needed for a dedicated RSS Label Maker backup folder. Never scan unrelated user files.
- Encrypt the archive locally using authenticated encryption before upload. Keep key recovery separate from the archive; do not upload the encryption key beside the archive.
- Archive contains local database, product data, label designs, templates and referenced assets that the user's plan permits. It excludes passwords, Core tokens, license keys and Google refresh tokens.
- Restore verifies account/workspace ownership, archive integrity and schema compatibility, then previews contents and creates a local safety backup before merge/replace.
- First release: manual backup and restore. Scheduled backup follows only after manual restore is validated.

## Trial and paid-plan behavior

- No permanent Free plan or daily 20-label quota.
- One 14-day Premium trial after verified registration/first activation; full Premium feature access with a mandatory RSS TRIAL watermark on every printed/PDF page.
- After expiry, printing and Premium-only operations require an active annual or lifetime license; preserve local data.
- Paid plans: Premium Single PC; Premium Shared License (one owner, multiple registered PCs); Premium Multiple PC (Network) (shared LAN workspace, multiple authorized users).
- Premium printing includes 300/600 DPI options where supported by the selected printer/driver.

## Remaining administrator settings — do not hard-code

- Network shared-user seat count and any additional-user fee.
- Exact offline grace duration and offline print policy.
- Plan-specific backup, import/export and other feature flags; the trial receives full Premium features.

## Release gates

- Confirm the RSS Core project key is registered.
- Implement and test the missing Core APIs before integrating desktop calls.
- Verify email registration, login, password recovery and session revocation against production Core.
- Verify license key redemption, device-slot enforcement, seat enforcement and trial start/expiry and payment idempotency.
- Verify Google Drive OAuth, encrypted backup and full restore round-trip.
- Build and test the Windows app on Windows 10/11.
- Production Core deployment and DNS changes require separate explicit approval. A repository commit is not proof of deployment.
