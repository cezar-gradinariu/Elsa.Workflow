# ADR-007: Elsa Studio Embedded in the Api Project

**Status:** Accepted  
**Date:** 2026-04-03

## Context

The ops team needs a visual interface to monitor workflow instances, inspect incidents, manually retry or skip faulted activities, and manage workflow definitions. Two hosting models were evaluated:

| Model | Description |
|-------|-------------|
| Embedded | Studio served from the same process as the API via `MapElsaStudio()`. Shared auth, no CORS, one deployment unit. |
| Standalone | Separate Blazor WASM project connecting to the API over HTTP. Separate deployment, CORS required, independent auth flow. |

Studio must never be accessible on the public customer-facing API surface.

## Decision

**Embed Elsa Studio in the `Api` project.**

**NuGet package (in `Api`):**

| Package | Version | Purpose |
|---------|---------|---------|
| `Elsa.Studio` | 3.6.0 | Blazor Studio components |

**`Program.cs` wiring:**

```csharp
// Registrations
builder.Services.AddRazorPages();
builder.Services.AddElsaStudio(studio => studio.UseBackendUrl("/elsa/api"));

builder.Services.AddAuthentication()
    .AddJwtBearer("CustomerApi", /* ... */)   // customer-facing API
    .AddCookie("Studio", /* ... */);           // Studio session cookie

// Route mapping
app.MapRazorPages();

app.MapGroup("/api").MapCustomerEndpoints()
   .RequireAuthorization(p => p.AddAuthenticationSchemes("CustomerApi")
                                .RequireAuthenticatedUser());

app.MapGroup("/elsa/api").MapElsaWorkflowsApi()
   .RequireAuthorization(p => p.AddAuthenticationSchemes("Studio")
                                .RequireRole("ElsaAdmin"));

app.MapElsaStudio("/studio")
   .RequireAuthorization(p => p.AddAuthenticationSchemes("Studio")
                                .RequireRole("ElsaAdmin"));
```

**Auth configuration skeleton:**

```jsonc
{
  "Authentication": {
    "JwtBearer": {
      "Authority": "https://your-idp",
      "Audience":  "your-api"
    },
    "Studio": {
      "CookieName":        ".ElsaStudio",
      "ExpireTimeSpan":    "08:00:00",
      "SlidingExpiration": true,
      "LoginPath":         "/studio/login"
    }
  }
}
```

**Access control rules:**
- `/studio` and `/elsa/api` require the `ElsaAdmin` role via cookie auth scheme.
- `/api` uses JWT Bearer and is completely independent.
- Block `/studio` and `/elsa/api` at the **reverse proxy / ingress** on the public-facing listener — do not rely solely on application-level auth.
- `ElsaAdmin` role is assigned only to internal ops users.

## Consequences

**Positive:**
- Single deployment unit — no separate Studio service to manage or version independently.
- Shared authentication context — Studio and the Elsa backend API use the same cookie session; no CORS or cross-origin token flow.
- Minimal setup: ~10 lines in `Program.cs` and one package.
- Studio connects to the same MongoDB-backed Elsa runtime already in place — no additional configuration.

**Negative:**
- Studio increases the Api project's dependency surface (Blazor, Razor Pages).
- Two auth schemes in the same process require careful policy configuration — misconfiguration could expose Studio routes to JWT-authenticated customers.
- Ingress-level blocking must be configured separately from application auth — this is an operational dependency that must be documented in the deployment runbook.
