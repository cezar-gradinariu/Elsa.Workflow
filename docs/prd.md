# Product Requirements Document

**Status:** In Progress  
**Date:** 2026-04-03  
**Stack:** C# 13 / .NET 10 · Elsa Workflows 3.6.0 · MongoDB

---

## 1. Purpose

> _Describe what the system is and why it exists. One paragraph, written for a business audience._

---

## 2. Goals

| # | Goal |
|---|------|
| G1 | Customers interact with the system exclusively through a versioned REST API |
| G2 | Business rules live in a pure, dependency-free Domain Core |
| G3 | Elsa Workflows drives process orchestration inside the Application layer |
| G4 | All persistence (workflow state + domain aggregates) targets MongoDB |
| G5 | Solution compiles and runs on .NET 10 with C# 13 language features |

---

## 3. Non-Goals (v1)

- Elsa Studio is embedded internally — **not** exposed on the public customer-facing API surface
- No multi-tenancy
- No event sourcing or CQRS split (can be added later)
- No gRPC or GraphQL endpoints

---

## 4. Users & Roles

| Role | Description | Primary interaction |
|------|-------------|---------------------|
| Customer | External user consuming the API | REST API (`/api`) |
| Ops / Admin | Internal operator managing workflows | Elsa Studio (`/studio`) |

> _Add further roles as bounded contexts are defined._

---

## 5. Functional Requirements

### 5.1 Fulfilment Order Management

#### User Stories

| ID | As a… | I want to… | So that… |
|----|-------|-----------|---------|
| US-001 | Customer | Submit a new fulfilment order via `POST /api/fulfilments` | The system reserves picking capacity and begins processing |
| US-002 | Customer | Retrieve a fulfilment order by ID via `GET /api/fulfilments/{id}` | I can track its current status and allocation state |
| US-003 | Customer | Cancel a fulfilment order via `DELETE /api/fulfilments/{id}` | An unwanted order is stopped and its workflow is terminated |

#### Acceptance Criteria

| ID | Criteria |
|----|---------|
| AC-001 | `POST /api/fulfilments` returns `200 OK` with no body when all inputs are valid and the order is successfully created |
| AC-002 | `POST /api/fulfilments` returns `400 Bad Request` (ProblemDetails) when required fields are missing or malformed |
| AC-003 | `POST /api/fulfilments` returns `409 Conflict` (ProblemDetails) when a `FulfilmentOrderId` that already exists is submitted |
| AC-004 | `POST /api/fulfilments` returns `422 Unprocessable Entity` (ProblemDetails) for domain violations (negative quantities, duplicate line numbers, empty order lines) |
| AC-005 | `GET /api/fulfilments/{id}` returns `200 OK` with the fulfilment order payload |
| AC-006 | `GET /api/fulfilments/{id}` returns `404 Not Found` (ProblemDetails) when the ID does not exist |
| AC-007 | `DELETE /api/fulfilments/{id}` returns `204 No Content` on success |
| AC-008 | `DELETE /api/fulfilments/{id}` cancels the associated Elsa workflow instance |
| AC-009 | All error responses use `application/problem+json` content type — see [ADR-011](adr/011-problem-details-error-responses.md) |
| AC-010 | On successful creation, an Elsa workflow is started with `FulfilmentOrderId` as the correlation ID |
| AC-011 | The aggregate is persisted to MongoDB with optimistic concurrency (`version` field) |

#### Business Rules

| ID | Rule |
|----|------|
| BR-001 | `FulfilmentOrderId` must be a non-empty GUID; must be unique across all orders |
| BR-002 | `StoreId`, `OrderId` are required non-blank strings |
| BR-003 | `OrderLines` must contain at least one entry |
| BR-004 | Each `OrderLineNo` must be unique within the order (no duplicates) |
| BR-005 | `ExpectedQuantity` must be ≥ 0 for every order line |
| BR-006 | `UnitOfMeasure` must be `Ea` or `Kg` |
| BR-007 | `CustomerSupplyInstructions` is optional; max 256 characters |
| BR-008 | `OrderId` is carried for traceability only — it is not used in business logic processing |

#### Workflow Lifecycle (first iteration)

| Step | Description | Status |
|------|-------------|--------|
| 1 | Workflow started on order creation (same `FulfilmentOrderId` = correlation ID) | Implemented |
| 2 | Call OFA (OrderFulfilmentAllocator) — assign order lines to sub-stores | Documented — see [ADR-013](adr/013-ofa-integration-workflow-step.md) |
| 3 | `ApplyAllocation(...)` on aggregate; workflow suspends via bookmark | Schema pending confirmation (OQ-1 in ADR-013) |

---

## 6. Open Questions

| # | Question | Owner | Due |
|---|----------|-------|-----|
| Q1 | JWT authority — in-house Keycloak, Azure AD, Auth0? | TBD | TBD |
| Q2 | OFA allocation model: fully-allocated per line (total = expected) or partial allocation allowed (total ≤ expected)? — see ADR-013 OQ-1 | Business | **Blocking ADR-013 implementation** |
| Q3 | Does OFA ever return lines not in the request? Ignore or reject? | Business | Awaiting answer |
| Q4 | OFA exhausts all retries — auto-cancel workflow or require operator intervention via Elsa Alterations API? | Product | Awaiting answer |

---

## 7. Out of Scope (v1)

- Workflow versioning migration strategy
- Sagas / compensating transactions
- Observability (OpenTelemetry tracing / metrics)
- CI/CD pipeline definition
- Docker / Kubernetes manifests
- Horizontal scaling / multi-instance (deferred — see [ADR-001](adr/001-ddd-layer-structure.md))
