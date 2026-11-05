# AI service (UC-22.1, UC-22.2)

One project `Ai.API` (port 5012, Compose `ai.api:8080`, Gateway `/ai/...`), own database `AcadPrep_AiServiceDb`.
JWT RS256 validated with Identity's public key only; UserId from `sub`. Chat is **Learner only**; administration
is **Admin only** (Huy, 2026-11-05). The legacy `AcadPrep_AiDb` (old schema, no migration history) is untouched.

## Decisions applied (Huy, 2026-11-05)
- Model `gemini-3.5-flash-lite` (verified available with `generateContent`/`countTokens` for the provided key),
  `maxOutputTokens` 1024, temperature 0.3, topP 0.9, 30 s timeout, 2 retries for 429/5xx/timeout, no streaming.
- Token quota per **rolling 24h window** (replaces spec v0.2 monthly period): FREE 15,000; PRO_MONTH 60,000;
  PRO_QUARTER 60,000 (seeded, editable by Admin). The window opens at the first successfully answered chat.
- Quota rule: already at/over the limit (or locked) -> 429 before calling Gemini. Otherwise Gemini `countTokens`
  gives the exact input (conservative estimate if unavailable): if at least 1 token remains the answer is
  returned in full and `usageMetadata.totalTokenCount` is charged (may overshoot, then the window locks); if
  the input alone uses up the remaining budget there is no generation, nothing is charged and the window is
  locked until reset. Requests rejected before Gemini consume nothing.
- Idempotency: every message carries a client `requestId` (unique per user). A completed id is replayed from
  storage (no Gemini call, no charge); Rejected/Failed ids can be retried. One request in flight per user
  (filtered unique index; 2-minute lease for crashed requests). Processing is not tied to the browser
  connection, so a lost connection after Gemini answered is recovered by retrying the same id.
- Plan comes from Billing `GET /api/subscriptions/me` with the caller's JWT; any Billing failure = FREE.
- FTOEIC behaviours ported (brand renamed to AcadPrep): tutor system prompt (separate `systemInstruction`;
  learner text only in user turns), out-of-scope subject redirect without calling Gemini, answer-leak guard
  replacing a disclosed multiple-choice answer with a pedagogical hint, `OUT_OF_SCOPE` marker -> configured
  OutOfScopeMessage, safety blocks -> polite refusal (tokens still charged). 10 context messages.
- No user id/email is sent to Gemini; message contents and the API key are never logged.

## API
| Method | Path | Notes |
|---|---|---|
| POST | /api/ai/chat | `{requestId, sessionId?, message (1-2000)}`; 200 reply + usage; 404 foreign session; 409 in progress; 429 quota (`code`, `usage`); 502 Gemini; 503 config |
| GET | /api/ai/sessions | own, UpdatedAt desc, paged |
| GET | /api/ai/sessions/{id}/messages | own, chronological, paged; others 404 |
| DELETE | /api/ai/sessions/{id} | soft delete; tokens never refunded |
| GET | /api/ai/usage/me | limit, used, remaining, percent, windowStart/resetAt (null before first chat), locked; read-only (also used by Billing UC-23.2) |
| GET | /api/ai/usage/me/daily | last 7 Vietnam days ending today, zero-filled (ledger of completed requests) |
| GET | /api/admin/ai/usage | active windows, tokensUsed desc, `userId` filter, paged |
| GET/PUT | /api/admin/ai/limits[/{planCode}] | 0..100,000,000; effective immediately; audited |
| POST | /api/admin/ai/usage/{userId}/reset | closes the current window (0, unlocked); audited; 204 |

## Storefront
- Floating "AcadPrep AI Tutor" widget (FTOEIC-style) in `_Layout` for Learner sessions: conversation picker,
  new/delete conversation, markdown (marked + DOMPurify), typing indicator, Enter/Shift+Enter, usage ring ->
  popover (percent, bar, used/limit, remaining, reset time, "Chưa sử dụng" before first chat) -> "Xem tất cả"
  panel with a 7-day CSS bar chart; quota lock banner with countdown and Retry (same requestId).
- `/Ai/Chat` JSON handlers (antiforgery on POST) relay to the Gateway server-side; the JWT never reaches JS.
- `/Admin/Ai/Usage`: limits per plan (custom `AiTokenLimit` FE/BE validation), active windows, reset.

## Configuration
`Gemini:ApiKey` from user-secrets (F5) or `GEMINI_API_KEY` in `.env` (Compose) only. Same DB pattern as
Notification/Billing: `ConnectionStrings:Default` in appsettings.json (Windows auth, no secret),
`ConnectionStrings__Default` overrides it. Set `AI_CONNECTION_STRING` in Compose's ignored `.env` for
`AcadPrep_AiServiceDb`; EF tooling uses `Data/AiDbContextFactory`. JWT uses the shared BuildingBlocks helper
(same as Exam) plus `AddJwtAuthentication` in `DependencyInjection`. Billing uses `AiService:BaseUrl`
(local `http://localhost:5012/`, Compose `http://ai.api:8080/`).
Migrations `InitialAi` + `SeedTokenLimits` are applied to `AcadPrep_AiServiceDb` (approved 2026-11-05).

## Tests (executed 2026-11-05)
Ai.API.Tests 40 passed (fake Gemini/Billing, in-memory SQLite, offline SQL Server model/seed check);
Ai.UI.Tests 16 passed; ai-admin-validation JS 9 passed. Real Gemini/Billing/Gateway/browser: NOT TESTED.
