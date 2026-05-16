## 1. Landing Page HTML

- [x] 1.1 Create `html-landing/index.html` — single self-contained file:
  - `<head>`: charset, viewport, title "HouseholdPlanner — Organise your home", Tailwind CDN script, inline `<style>` for dot-grid background and blob
  - Fixed navbar: logo left, three links right (Features, How it works, GitHub → `https://github.com/TheMisterPanek/household-planner`)
  - Hero section:
    - Blurred indigo blob (absolutely positioned, `blur-[80px]`)
    - Dot-grid background via `radial-gradient`
    - H1: "Your household, always organised."
    - Subheading: "Track groceries, plan meals, and manage your home inventory straight from Telegram — with a clean web workspace for everything else."
    - Two CTA buttons: **Open Workspace** (href `/app`, indigo filled) and **Add to Telegram** (href `https://t.me/BOT_USERNAME`, outlined; replace `BOT_USERNAME` with the real handle)
  - Features section (`id="features"`): 3-column card grid (1 col mobile, 2 col tablet, 3 col desktop), 6 cards:
    - 🛒 Shopping list, ⏰ Expiry tracking, 💰 Price history, 🍽 Meal library, 📅 Weekly plan, 🤖 AI queries (copy from design.md §Feature Cards)
  - How it works section (`id="how-it-works"`): 3-step grid with numbered indigo circles (copy from design.md §How It Works Steps)
  - Footer: "HouseholdPlanner" · EN/RU/PL · [GitHub](https://github.com/TheMisterPanek/household-planner) · @BOT_USERNAME
  - Smooth-scroll: `html { scroll-behavior: smooth; }`
  - Navbar scroll shadow: add/remove `shadow-sm` class on scroll via 10-line inline `<script>`

- [x] 1.2 Verify the page looks correct by opening `html-landing/index.html` directly in a browser (no server needed) and checking:
  - Navbar sticky, hero text readable, feature cards aligned, steps horizontal on desktop, footer links correct

---

## 2. nginx Config

- [x] 2.1 Create `nginx/` directory
- [x] 2.2 Create `nginx/nginx.conf` (see design.md §nginx Config):
  - Listens on port 80
  - Serves `html-landing/` as static root with `Cache-Control: max-age=86400`
  - Proxies `/app` → `http://web:8080` with WebSocket upgrade headers
  - Proxies `/app/_blazor` → `http://web:8080` with `Connection: Upgrade`

---

## 3. Docker Compose Update

- [x] 3.1 Update `docker-compose.yml`:
  - Add `networks: internal` to the existing `bot` service
  - Add `web` service (profile: `web`; see design.md §docker-compose; use placeholder Dockerfile path — update when `blazor-web-auth-scaffold` is implemented)
  - Add `nginx` service (image: `nginx:alpine`; ports `80:80` and `443:443`; mounts `html-landing/`, `nginx/nginx.conf`, `nginx/certs/`)
  - Add `internal` bridge network and `db-data` volume (already exists — keep it)
  - Add `nginx/certs/` to `.gitignore` (certificates must not be committed)

---

## 4. `.gitignore` Update

- [x] 4.1 Add `nginx/certs/` to `.gitignore`

---

## 5. SSL Reference (manual post-deploy — not automated)

- [x] 5.1 Add a comment block at the bottom of `nginx/nginx.conf` with the ready-to-paste HTTPS server block:

  ```nginx
  # --- HTTPS (uncomment after running Certbot) ---
  # server {
  #     listen 443 ssl;
  #     server_name yourdomain.com;
  #     ssl_certificate     /etc/nginx/certs/live/yourdomain.com/fullchain.pem;
  #     ssl_certificate_key /etc/nginx/certs/live/yourdomain.com/privkey.pem;
  #     ssl_protocols TLSv1.2 TLSv1.3;
  #
  #     location / { ... same as HTTP block ... }
  #     location /app { ... same as HTTP block ... }
  #     location /app/_blazor { ... same as HTTP block ... }
  # }
  # server {
  #     listen 80;
  #     server_name yourdomain.com;
  #     return 301 https://$host$request_uri;
  # }
  ```

- [x] 5.2 Add a `DEPLOY.md` (or section in `README.md`) with the Certbot one-liner:

  ```bash
  # Run once after nginx is live on port 80:
  docker run --rm \
    -v $(pwd)/nginx/certs:/etc/letsencrypt \
    -v $(pwd)/html-landing:/var/www/html \
    certbot/certbot certonly --webroot \
    -w /var/www/html -d yourdomain.com \
    --email you@example.com --agree-tos --non-interactive
  # Then: uncomment the HTTPS server block in nginx/nginx.conf
  # Then: docker compose restart nginx
  ```

---

## 6. Smoke Test (manual — do not mark complete without user confirmation)

- [x] 6.1 `docker compose up nginx` → nginx starts; `curl http://localhost/` returns the landing page HTML
- [x] 6.2 Open `http://localhost` in a browser → landing page renders correctly:
  - Navbar visible and sticky on scroll
  - Hero heading, subtext, and both CTA buttons displayed
  - "Open Workspace" button visible; clicking it goes to `/app` (502 is acceptable until web service is up)
  - "Add to Telegram" button links to the correct bot URL
  - Feature cards: 3-column grid on desktop, no overflow
  - "How it works" steps display horizontally on desktop
  - GitHub link in navbar and footer both point to `https://github.com/TheMisterPanek/household-planner`
  - Footer shows correct bot username
- [x] 6.3 `curl http://localhost/app` → 502 (expected; web service not yet running) — not a crash
- [x] 6.4 `docker compose up` (with bot) → bot still starts and responds to Telegram messages normally
- [x] 6.5 Mobile view (Chrome DevTools 375 px) → hero text readable, feature cards stack to 1 column, nav links visible
