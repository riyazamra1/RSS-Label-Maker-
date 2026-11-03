# RSS Label Maker Authentication Service

This service is separate from RSS Core. It stores account, verification-challenge and device-session metadata only. Product data, label content, templates, watermark files and print jobs remain in the Windows app's local SQLite database.

## Components

- Cloudflare Worker: registration, email verification, sign-in, session validation, sign-out and password recovery.
- Cloudflare D1: account, one-time challenge, device session and rate-limit tables.
- Resend: transactional email delivery. The API key is used only as a Worker secret.
- Windows client: stores the device session in Windows Credential Manager and permits offline use after successful first-time verification.

## Required configuration before deployment

1. Create a Cloudflare D1 database named `rss-label-maker-auth`.
2. Apply `migrations/0001_initial.sql` using Wrangler D1 migrations.
3. Set the real D1 database ID in `wrangler.toml`.
4. Verify a sender domain in Resend and configure its SPF/DKIM DNS records.
5. Add Worker secrets:
   - `RESEND_API_KEY`: a Resend sending-only API key.
   - `CODE_PEPPER`: a cryptographically random secret (at least 32 random bytes, hex encoded).
6. Configure the non-secret Worker variable `MAIL_FROM` to a sender address on the verified Resend domain.
7. Configure the intended HTTPS hostname and DNS route only after the user approves production deployment.
8. Deploy one Worker instance, then test the live `GET /health` endpoint. It must return `configured: true`.
9. Set the Windows client endpoint through `RSS_LABEL_MAKER_AUTH_URL` if the final hostname differs from the default `https://auth.rssapps.net`.

Example local commands (run from this directory after configuring Wrangler):

```powershell
npx wrangler d1 create rss-label-maker-auth
npx wrangler d1 migrations apply rss-label-maker-auth --remote
npx wrangler secret put RESEND_API_KEY
npx wrangler secret put CODE_PEPPER
npx wrangler deploy
```

Set `MAIL_FROM` in the Worker variables to the verified sender address. Never put Resend keys, code peppers, database credentials or other secrets in the desktop app or Git.

## API routes

- `POST /auth/register` — create pending account and send email code.
- `POST /auth/verify` — verify six-digit code and issue device session.
- `POST /auth/resend-verification` — resend pending verification code.
- `POST /auth/login` — sign in verified account and issue device session.
- `POST /auth/session` — validate a bearer device session.
- `POST /auth/logout` — revoke current device session.
- `POST /auth/forgot-password` — send reset code.
- `POST /auth/reset-password` — reset password and revoke active sessions.
- `GET /health` — service/configuration health (does not expose secrets).

## Security and operational notes

- Passwords are salted and hashed using PBKDF2-HMAC-SHA-256 (310,000 iterations); plaintext passwords are never stored.
- Email codes are random six-digit values, stored as peppered hashes, expire after 10 minutes and are single-use.
- Verification/reset attempts and registration/sign-in requests are rate limited; email resend has a 60-second cooldown.
- Device session tokens are random, stored only as hashes server-side and expire after one year.
- The desktop client stores its bearer token in Windows Credential Manager, not SQLite or a plaintext settings file.
- An already-verified PC can work offline. While offline it cannot learn that an account/session was remotely revoked; revocation is enforced on the next successful online validation.
- A production Resend sender domain, D1 database, Worker secrets, hostname and DNS route are required. This repository commit does not mean the service is deployed or email delivery is operational.
