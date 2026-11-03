# Automatic Transliteration — Implementation Contract

## User-visible behaviour

- The product form exposes separate Sinhala and Tamil generation actions.
- Each language independently supports Transliteration, Translation, and Manual entry.
- Free allowance: 5 successful Sinhala generations and 5 successful Tamil generations per Asia/Colombo calendar day.
- Premium entitlement: unlimited automatic generations while active.
- Manual Sinhala/Tamil entry, editing, cached values, label design and printing remain free.
- Show per-language remaining counts. When one language reaches zero, block only that language's automatic action.

## Request lifecycle

1. User explicitly invokes an action for one language and selects Transliteration or Translation.
2. Client checks the local successful-result cache. A valid cache hit is returned without a quota charge.
3. On cache miss, client sends language, mode, source English text, and a stable request/idempotency key to the secure service.
4. Service authenticates the user/device, validates Premium entitlement, and atomically checks/consumes the appropriate daily quota.
5. Service calls ChatGPT using a server-held credential and returns the generated text plus authoritative remaining allowance and entitlement state.
6. Client caches successful output and updates the usage display. A failed request does not consume quota.

## Proposed service contract

POST /v1/label-maker/name-generation

Request JSON fields:
- sourceText: required English product name
- targetLanguage: si or ta
- mode: transliteration or translation
- requestId: stable idempotency key

Response JSON fields:
- text: generated Sinhala or Tamil string
- targetLanguage: si or ta
- mode: requested mode
- premium: validated entitlement state
- remainingToday: integer 0–5 for Free; null for Premium
- resetsAt: next Asia/Colombo midnight timestamp for Free

Error responses must distinguish authentication/entitlement errors, quota exhaustion, validation errors, provider errors, and transient network/service failures. Only successful, non-cached generations consume quota. Idempotency must prevent retrying the same request from charging twice.

## Security and correctness

- Never embed ChatGPT API credentials in the WPF executable or repository.
- Premium status and quota enforcement must be authoritative on the service, not based on a client-side boolean or editable local file.
- Use Asia/Colombo calendar dates for quota windows, independent of the PC's configured display timezone.
- Keep the two language quotas independent.
- Do not require RSS Core for local product entry, design, preview, or printing.
- Manual entry remains available when the service is unreachable or the daily quota is exhausted.

## Delivery sequence

1. Implement and test the secure service adapter, authentication and authoritative quota/entitlement enforcement.
2. Implement WPF language/mode controls, usage display, caching and error states.
3. Add tests for 5+5 limits, midnight reset, idempotent retries, failed calls, Premium entitlement and offline manual entry.
4. Verify end-to-end against the deployed service before marking automatic generation complete.