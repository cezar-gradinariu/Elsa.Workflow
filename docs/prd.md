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

> _This section is to be completed as bounded contexts are defined. Use the structure below for each feature area._

### 5.1 \[Feature Area TBD\]

#### User Stories

| ID | As a… | I want to… | So that… |
|----|-------|-----------|---------|
| US-001 | | | |

#### Acceptance Criteria

| ID | Criteria |
|----|---------|
| AC-001 | |

#### Business Rules

| ID | Rule |
|----|------|
| BR-001 | |

---

## 6. Open Questions

| # | Question | Owner | Due |
|---|----------|-------|-----|
| Q1 | How are workflows triggered — HTTP endpoint, timer, domain event? | TBD | TBD |
| Q2 | JWT authority — in-house Keycloak, Azure AD, Auth0? | TBD | TBD |
| Q3 | Specific bounded contexts / aggregate names? | TBD | TBD |

---

## 7. Out of Scope (v1)

- Workflow versioning migration strategy
- Sagas / compensating transactions
- Observability (OpenTelemetry tracing / metrics)
- CI/CD pipeline definition
- Docker / Kubernetes manifests
- Horizontal scaling / multi-instance (deferred — see [ADR-001](adr/001-ddd-layer-structure.md))
