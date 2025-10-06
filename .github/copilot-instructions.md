# Copilot Instructions for 2025.2-Amaterasu

These rules orient AI coding agents to be productive in this repo. Keep answers concise and follow the project's conventions.

## Architecture and boundaries
- Web (Next.js/React/Tailwind) lives in `web/`, deployed to Azure Static Web Apps with SSR/SSG/ISR. Do not implement business rules in the web layer.
- Backend lives in `api/` (.NET 9). Controllers are thin; business logic in `Services/`; cross-cutting in `Middleware/`; DTOs in `Models/`; registrations in `Configuration/`; abstractions in `Interface/`.
- Data: Azure Cosmos DB (Core SQL), camelCase JSON. Secrets via Azure Key Vault; telemetry via Application Insights.
- Communication: Next.js server-side calls the .NET API (prefer server fetch/proxy to avoid CORS). Client-side fetches must respect CORS and public base URL.

## Code and language conventions
- Code identifiers in English. Project docs (e.g., `docs/`) in Portuguese. Comments can be PT-BR.
- Naming: folders/files `kebab-case`; variables/functions `camelCase`; classes `PascalCase`; constants `UPPER_SNAKE_CASE`.
- API URL prefix: `/api/v1`. Use nouns (plural) and proper status codes; return ProblemDetails/Result patterns from services and translate in controllers.

## Developer workflows
- Branching: work from `development`; PR from `feature/*` into `development`; releases merge `development` -> `main`. Follow Conventional Commits in PT-BR summary, type/scope in EN (e.g., `feat(api): adiciona rota de login`).
- Web build/test: Next.js app in `web/` with ESLint/Prettier; unit/integration with Jest/RTL; E2E with Playwright. Tailwind configured via `tailwind.config.{js,ts}`.
- API build/test: .NET 9 in `api/` with unit/integration/contract tests in `api/tests/`. Middleware for errors/correlation; FluentValidation for DTOs (if present).
- CI/CD: GitHub Actions expected as `.github/workflows/{web-swa.yml,api-appservice.yml}` (to be added when code exists). Web deploys to SWA (SSR). API deploys to App Service with staging slot + swap.

## Environment and config
- Never commit secrets. Use App Settings/Key Vault. Provide non-secret defaults in `.env.example`. Web: use `API_BASE_URL` (private) for server calls and `NEXT_PUBLIC_*` for client.
- SWA routing/headers: `web/staticwebapp.config.json` controls navigation fallback, CSP/HSTS, caching.
- Correlation: propagate `x-correlation-id` from web to API and include in logs/telemetry.

## Patterns to follow (examples)
- Web data fetching: server components or route handlers call the API using `process.env.API_BASE_URL`; avoid duplicating business rules. Client components use `NEXT_PUBLIC_API_BASE_URL` and handle CORS.
- API layering: Controller delegates to `I<Domain>Service`; service encapsulates Cosmos access and domain rules; errors bubble to error middleware which maps to ProblemDetails (422 for domain errors, 400 validation, 404, etc.).
- Cosmos: prefer high-cardinality partition keys (e.g., `/tenantId` or `/id` for roots), camelCase properties, and projections instead of `SELECT *`.

## Where to look
- Conventions: `docs/conventions.md` (single source of truth for naming, structure, Azure, quality gates).
- Product overview and diagram: `README.md`.

If something is missing or unclear, ask for the specific file/decision in `docs/conventions.md` or propose minimal, reversible changes aligned with these rules.