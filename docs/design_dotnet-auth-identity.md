# Chassis — Auth & Identity (Multi-Tenant Web + Mobile)

**Status:** Draft v1 — companion document to `design_dotnet-electron-react-ddd-template.md`
(the "Chassis" template). Extends that document's §7.2 auth row and §10 tech-stack line
("ASP.NET Core Identity + OIDC provider") into a full design.
**Scope:** Authentication and identity for Chassis's **multi-tenant web build, and any
mobile clients** hitting the same hosted API. The desktop build is explicitly **out of
scope** — per the main template's §7.2, desktop uses no login or a local PIN, since it's
single-tenant and local-only, and never talks to the authorization server described here.

---

## 1. Problem This Document Solves

The multi-tenant web deployment (and, per the main template, any future mobile clients)
needs real login, not the desktop build's "local, no auth" shortcut. Requirements:

1. Sign-in via **Google**, **Microsoft**, and **Facebook** OAuth.
2. Sign-in via **email** (password-based).
3. Sign-in via **passkeys** (WebAuthn/FIDO2), using .NET 10's native support.
4. Correctly serve **more than one kind of client** — a same-origin React SPA today,
   native mobile apps later — each with an appropriate, distinct token-handling model.
5. Sit on top of the main template's existing multi-tenant data model (§4.2 of the
   Chassis doc): every aggregate carries a `TenantId`.

---

## 2. Two Layers, Not One

Easy to conflate — worth stating explicitly:

- **ASP.NET Core Identity** — the user-management layer. Stores users, password hashes,
  roles, claims, linked external logins, and passkey credentials. Provides
  `UserManager`/`SignInManager` to check a password, run a WebAuthn ceremony, or accept a
  callback from Google/Microsoft/Facebook. Has no concept of OAuth scopes or issuing
  tokens to other applications.
- **OpenIddict** — the OAuth 2.0 / OIDC **authorization server**. Exposes `/authorize`,
  `/token`, `/userinfo`, `.well-known/openid-configuration`. Issues signed access and ID
  tokens to registered clients. When it needs to know who a user is, it delegates to
  Identity's login page; Identity hands back a verified user, and OpenIddict does the
  actual token issuance.

Both live inside `Chassis.Api` for the web deployment — not a separate microservice (§8).

**Decision, confirmed: OpenIddict, not Duende IdentityServer.** Duende is free for
dev/test only; production requires a paid annual license (~$1,500/yr entry tier) unless a
project qualifies for its capped/eligibility-gated Community Edition. OpenIddict is free
and fully open-source (Apache-2.0), no such gate. Per the same "least-opinionated free
default" logic as the main doc's §9.5 codegen choice, OpenIddict is the template default.
A specific downstream project can swap to Duende later if it needs its extra polish and
can justify the license cost.

---

## 3. Login Methods

| Method | Package / mechanism | Notes |
|---|---|---|
| Google | `Microsoft.AspNetCore.Authentication.Google` | Standard OAuth external login via Identity's `ExternalLoginSignInAsync`. |
| Facebook | `Microsoft.AspNetCore.Authentication.Facebook` | Same pattern. |
| Microsoft | `Microsoft.AspNetCore.Authentication.MicrosoftAccount` | **Confirmed:** personal Microsoft accounts only — no Entra ID/work-account SSO for now. If org SSO is added later, that's `Microsoft.Identity.Web` instead — a different library and config shape (tenant ID, Entra app registration). |
| Email | ASP.NET Core Identity's built-in local account store | Password + email confirmation, built in. |
| Passkeys | ASP.NET Core Identity's native WebAuthn support (.NET 10) | First-class as of .NET 10 (`MakePasskeyCreationOptionsAsync` / `PasskeySignInAsync` and friends). Scoped to "authenticate into this app" — no attestation validation, no hardware allowlisting. `fido2-net-lib` is the escape hatch if a downstream project ever needs those; not needed for the template default. |

**Account model:** a single `IdentityUser` can hold a password, one or more linked
external logins, and one or more passkeys at once — Identity supports this natively. A
user who signs up via Google can add a passkey later, add a password later, etc., with no
extra design needed.

---

## 4. Tenancy & Signup — Decisions

1. **One tenant per user — confirmed.** A login is not shared across multiple tenants (no
   "one identity, many workspaces" model). Each `IdentityUser` carries exactly one
   `TenantId`. This keeps tenant scoping identical to the main doc's §4.2 without a
   many-to-many user↔tenant table, per-tenant roles, or a tenant-switcher UI.
   **Cost of this decision:** someone needing access to two separate tenants (e.g. a
   consultant working with two customer orgs) needs two separate logins. Recommendation:
   keep email uniqueness **global** (not scoped per tenant) — the edge case above then
   just requires a second email address, an acceptable template-default limitation.
2. **Self-service signup — confirmed.** Anyone can sign up; doing so creates a brand-new
   tenant with that user as its first member. No invite-only gate, no admin approval, by
   default.

**Deliberately deferred:** whether an existing tenant can later invite additional users
into itself. That's a distinct question from "how do you get your first tenant," which
self-service signup answers. A follow-on "invite teammates" flow is a reasonable near-term
feature but isn't specified in this draft.

---

## 5. Client Types & Token Handling

Not every client should hold tokens the same way.

### 5.1 Web SPA — Backend-for-Frontend (BFF)

**Decision, confirmed: BFF.** The React SPA never sees an OAuth token.

1. Browser hits a login route on `Chassis.Api` — not OpenIddict's `/authorize` directly
   from JS.
2. `Chassis.Api` performs the full Authorization Code + PKCE exchange with OpenIddict
   **server-side**.
3. `Chassis.Api` sets a plain, `HttpOnly`, `Secure` session cookie on the browser — the
   only artifact the browser ever holds.
4. Every subsequent SPA→API call rides that cookie, same-origin — the OAuth machinery is
   invisible to the frontend entirely.

This avoids the usual SPA-holds-tokens pitfalls (XSS exfiltration from `localStorage`,
refresh logic living in JS), at the cost of `Chassis.Api` now also acting as an OAuth
*client* to its own OpenIddict server, not just as the authorization server. Unusual
sounding, but this is exactly what the BFF pattern exists for.

### 5.2 Mobile — standard public-client PKCE

Mobile apps can't share a browser's cookie jar with a backend, so the BFF pattern doesn't
apply. Mobile registers as a public OAuth client with OpenIddict, runs Authorization Code
+ PKCE through the system browser (`ASWebAuthenticationSession` on iOS, Custom Tabs on
Android — never an embedded webview, for phishing-resistance), and holds the resulting
tokens itself in platform secure storage (iOS Keychain / Android Keystore).

### 5.3 Desktop — out of scope

Per the main Chassis doc's §7.2, desktop has no login or a local PIN, and is
single-tenant/local-only. It never talks to this authorization server. Noted here only to
make the boundary explicit.

---

## 6. Data Storage

- OpenIddict's and Identity's EF Core tables live in the **same database and schema** as
  the rest of the tenant data — consistent with the main doc's §4.2 "shared database,
  shared schema" decision. No separate identity database for the template default.
- **Not yet decided:** signing-key storage/rotation for OpenIddict's token-signing
  certificate. A dev certificate is fine locally; production wants a managed key store
  (e.g. a cloud provider's key vault). Deferred as a deployment-time concern, not a
  template-architecture one.

---

## 7. Explicitly Deferred (Not Blocking This Draft)

- **MFA / TOTP** beyond passkeys — Identity supports it natively if wanted later; not
  specified here since it wasn't requested.
- **Token/refresh lifetimes and rotation policy** — OpenIddict's defaults until there's a
  reason to tune them.
- **"Invite teammates to an existing tenant" flow** — see §4.

---

## 8. Why Not a Separate Auth Microservice

Both Identity and OpenIddict register inside `Chassis.Api` itself, not a standalone
identity service — consistent with the main template's bias toward one deployable per
environment (§2, §5.6). A separate auth service becomes worth it if multiple independent
backends need to share one login authority; that's not this template's situation, and
splitting it out preemptively would add an extra deployable, an extra network hop per
token validation, and cross-service data-consistency questions for no present benefit.
