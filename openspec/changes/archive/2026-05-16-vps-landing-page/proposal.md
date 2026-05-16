## Why

The VPS root domain today returns nothing (port 80/443 is unbound). Anyone who lands on the URL has no idea what the service is or how to get access. A landing page turns the domain into the front door of the product: it explains what the household bot does, shows its key features, and gives household members a direct path into the web workspace.

This also establishes the nginx reverse-proxy layer that the Blazor web UI (`blazor-web-auth-scaffold`) will run behind, so SSL termination and clean URLs (`/app`) are handled in one place.

## What Changes

### `html-landing/index.html`

A single-file static landing page. No build step, no framework, no dependencies beyond a Tailwind CDN script tag. Served by nginx as the root of the domain.

Sections:
1. **Hero** — Product name, one-sentence tagline, two CTAs: "Open Workspace" (→ `/app`) and "Add to Telegram" (→ `https://t.me/<BOT_USERNAME>`)
2. **Features** — Six feature cards: Shopping list, Expiry tracking, Price log, Meal library, Weekly plan, AI queries
3. **How it works** — Three-step visual flow: Add bot to your group → Track items with commands → Manage everything on the web
4. **Footer** — Bot username link, language badge (EN/RU/PL)

Design: white background, `indigo-600` accent, system font stack, generous whitespace, fully responsive.

### nginx (`nginx/nginx.conf` + Docker service)

- Serves `html-landing/` as static files at `/`
- Reverse-proxies `/app` → Blazor web container at `http://web:8080`
- Listens on port 80; SSL (port 443) handled via a separate Certbot task (see tasks)
- Docker service added to `docker-compose.yml`

### `docker-compose.yml` updates

- New `nginx` service (image: `nginx:alpine`; mounts `html-landing/` + `nginx/nginx.conf`)
- New `web` service for the Blazor app (`ProductTrackerBot.Web`) — placeholder until `blazor-web-auth-scaffold` is implemented; nginx config is written to support it from the start
- Bot service gains no new ports (internal only; nginx proxies to web)

## Capabilities

### New Capabilities

- `landing-page`: Visitors to the VPS domain see a branded landing page explaining the product.
- `workspace-link`: "Open Workspace" button navigates to the Blazor web UI at `/app`.
- `nginx-proxy`: nginx terminates HTTP (and later HTTPS) and routes traffic to the appropriate service.

## Out of Scope

- SSL certificate provisioning (documented as a manual step; Certbot one-liner provided).
- Auth-gating the landing page itself (it's public).
- Analytics or cookie banners.
- Internationalised landing copy (English only).

## Impact

- **Code**: 1 new HTML file (`html-landing/index.html`), 1 new nginx config (`nginx/nginx.conf`), updated `docker-compose.yml`.
- **APIs**: No changes to bot or web application code.
- **Dependencies**: `nginx:alpine` Docker image (no new language packages).
- **Systems**: Ports 80 and 443 on the VPS now bound to nginx; bot container no longer needs to expose any port directly.
- **Compatibility**: Fully independent of the Blazor changes; works today with just the bot running.

## Rollback Plan

1. Remove the `nginx` service from `docker-compose.yml`.
2. Remove `nginx/nginx.conf`.
3. The bot and (future) web service continue running on internal Docker ports.

## Affected Teams

Single-user/household deployment. Affects the public-facing VPS address.

## Cross-Cutting Notes

- The Blazor web service is named `web` in Docker Compose; nginx upstream uses that name. If `blazor-web-auth-scaffold` is not yet deployed, the `/app` proxy will 502 — that is acceptable until the web service exists.
- Static file caching: nginx sets `Cache-Control: max-age=86400` for the landing page assets.
- Bot username (`BOT_USERNAME`) is embedded in the landing page "Add to Telegram" link. It is set as an nginx variable via an environment-substituted config, or hardcoded if the username is stable.
