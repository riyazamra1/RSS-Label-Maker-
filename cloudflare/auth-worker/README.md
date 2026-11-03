# DEPRECATED — Do not deploy this standalone authentication Worker

RSS Label Maker now uses **RSS Core** as its central identity, user database, email-verification, licensing and entitlement authority.

This directory was an earlier implementation proposal and is retained temporarily for repository history only. It is not the supported architecture and must not be deployed or configured.

Use:
- RSS Core production base URL: `https://rsscore.cv`
- Existing central customer registration and email-verification contract in the RSS Core repository.
- RSS Core customer/license/device records and central entitlement system.
- RSS Core's Payments module and the approved Payments.lk hosted checkout flow.

The desktop client must not call this Worker's API, create a separate Label Maker account database, or contain a second Resend integration.

Before enabling Label Maker login, license activation, device limits, quotas, shared users or cloud sync, verify the required RSS Core endpoints are implemented and deployed. The RSS Core account/sync contract explicitly distinguishes documented expectations from currently implemented endpoints.

Google Drive backup content belongs in the user's Google Drive under the user's OAuth authorization; RSS Core stores only the account/license/entitlement and backup metadata needed for authorization and device coordination.
