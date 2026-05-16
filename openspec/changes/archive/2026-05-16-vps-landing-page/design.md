## File Layout

```
html-landing/
└── index.html           (single-file, self-contained)
nginx/
└── nginx.conf
docker-compose.yml       (updated)
```

---

## Landing Page: Full Wireframe

```
┌──────────────────────────────────────────────────────────────────────┐
│  NAVBAR                                                              │
│  HouseholdPlanner                    Features  How it works  GitHub  │
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  HERO                                                                │
│                                                                      │
│  Your household,                                                     │
│  always organised.                                                   │
│                                                                      │
│  Track groceries, plan meals, and manage your home inventory         │
│  straight from Telegram — with a clean web workspace for the rest.  │
│                                                                      │
│  [ Open Workspace ]   [ Add to Telegram ]                           │
│                                                                      │
│  (subtle grid background, indigo gradient blob)                      │
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  FEATURES  (6 cards, 3-col grid)                                    │
│                                                                      │
│  🛒 Shopping list      ⏰ Expiry tracking    💰 Price history        │
│  Track what you need   Never throw away      See how prices change   │
│  — mark done, edit,    food again. Bot        over time per item.    │
│  add from chat.        reminds you early.                            │
│                                                                      │
│  🍽 Meal library       📅 Weekly plan        🤖 AI queries           │
│  Store recipes with    Assign meals to        Ask "what should I     │
│  ingredients & steps.  each day of the week. buy?" in plain text.   │
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  HOW IT WORKS  (3 numbered steps, horizontal)                       │
│                                                                      │
│  1 ──────────────  2 ──────────────  3 ──────────────               │
│  Add the bot       Use commands in    Open the web                  │
│  to your           your group chat.   workspace for                 │
│  Telegram group.   /buy /list /meals  full CRUD &                   │
│                    and more.          history views.                 │
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  FOOTER                                                              │
│  HouseholdPlanner  ·  EN / RU / PL  ·  GitHub  ·  @BotUsername     │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Visual Design Spec

| Token | Value |
|---|---|
| Background | `#ffffff` |
| Accent | `indigo-600` (`#4f46e5`) |
| Accent hover | `indigo-700` |
| Text primary | `gray-900` |
| Text secondary | `gray-500` |
| Font | `ui-sans-serif, system-ui, sans-serif` |
| Base size | `16px` |
| Hero heading | `4rem`, `font-extrabold`, `tracking-tight` |
| Section heading | `1.875rem`, `font-bold` |
| Card radius | `rounded-xl` |
| Card shadow | `shadow-sm` |
| Card bg | `gray-50` |
| Max content width | `1120px` (`max-w-5xl`) |
| Section padding | `py-24` |

**Hero background**: subtle dot-grid via `background-image: radial-gradient(#e0e7ff 1px, transparent 1px)` at 24 px spacing, plus a blurred `indigo-100` blob (`filter: blur(80px)`, absolutely positioned).

---

## HTML Structure (`index.html`)

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>HouseholdPlanner — Organise your home</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <style>/* dot grid + blob + smooth scroll */</style>
</head>
<body class="antialiased text-gray-900">

  <!-- Navbar -->
  <header class="fixed top-0 w-full bg-white/80 backdrop-blur border-b border-gray-100 z-50">
    <nav class="max-w-5xl mx-auto px-6 h-16 flex items-center justify-between">
      <span class="font-bold text-lg tracking-tight">HouseholdPlanner</span>
      <div class="flex gap-6 text-sm text-gray-600">
        <a href="#features">Features</a>
        <a href="#how-it-works">How it works</a>
        <a href="https://github.com/TheMisterPanek/household-planner" target="_blank">GitHub</a>
      </div>
    </nav>
  </header>

  <!-- Hero -->
  <section class="relative pt-40 pb-28 px-6 text-center overflow-hidden">
    <!-- blob -->
    <div class="absolute top-0 left-1/2 -translate-x-1/2 w-[600px] h-[400px]
                bg-indigo-100 rounded-full opacity-60 blur-[80px] -z-10"></div>
    <!-- dot grid via inline style on section -->
    <h1 class="text-6xl font-extrabold tracking-tight leading-tight max-w-2xl mx-auto">
      Your household,<br>always organised.
    </h1>
    <p class="mt-6 text-xl text-gray-500 max-w-xl mx-auto">
      Track groceries, plan meals, and manage your home inventory straight from Telegram —
      with a clean web workspace for everything else.
    </p>
    <div class="mt-10 flex flex-col sm:flex-row gap-4 justify-center">
      <a href="/app" class="btn-primary px-8 py-3 text-base">Open Workspace</a>
      <a href="https://t.me/BOT_USERNAME" target="_blank" class="btn-secondary px-8 py-3 text-base">
        Add to Telegram
      </a>
    </div>
  </section>

  <!-- Features -->
  <section id="features" class="py-24 px-6 bg-white">
    <div class="max-w-5xl mx-auto">
      <h2 class="text-3xl font-bold text-center mb-14">Everything your household needs</h2>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        <!-- 6 feature cards (see below) -->
      </div>
    </div>
  </section>

  <!-- How it works -->
  <section id="how-it-works" class="py-24 px-6 bg-gray-50">
    <div class="max-w-5xl mx-auto">
      <h2 class="text-3xl font-bold text-center mb-14">Up and running in minutes</h2>
      <div class="grid grid-cols-1 md:grid-cols-3 gap-10">
        <!-- 3 steps (see below) -->
      </div>
    </div>
  </section>

  <!-- Footer -->
  <footer class="border-t border-gray-100 py-10 px-6 text-center text-sm text-gray-400">
    <p class="font-medium text-gray-600 mb-1">HouseholdPlanner</p>
    <p class="flex flex-wrap gap-4 justify-center">
      <span>EN / RU / PL</span>
      <a href="https://github.com/TheMisterPanek/household-planner" target="_blank"
         class="hover:text-gray-700">GitHub</a>
      <a href="https://t.me/BOT_USERNAME" target="_blank" class="hover:text-gray-700">@BOT_USERNAME</a>
    </p>
  </footer>

</body>
</html>
```

---

## Feature Cards (6)

| Icon | Title | Body |
|---|---|---|
| 🛒 | Shopping list | Track what your household needs. Add items with `/buy`, mark them done, edit quantities — from chat or the web. |
| ⏰ | Expiry tracking | Log expiry dates when you buy. The bot alerts your group before food goes to waste. |
| 💰 | Price history | Record prices at checkout. See how costs change over time and spot the best deals. |
| 🍽 | Meal library | Build a shared recipe library with ingredients and step-by-step instructions. |
| 📅 | Weekly plan | Assign meals to each day of the week. Everyone in the group sees what's cooking. |
| 🤖 | AI queries | Ask anything in plain language — "what should I buy this week?" — and get a smart answer from your own data. |

Card markup:
```html
<div class="bg-gray-50 rounded-xl p-6 flex flex-col gap-3">
  <span class="text-3xl">🛒</span>
  <h3 class="font-semibold text-gray-900">Shopping list</h3>
  <p class="text-sm text-gray-500 leading-relaxed">…</p>
</div>
```

---

## How It Works Steps (3)

| # | Heading | Body |
|---|---|---|
| 1 | Add the bot to your Telegram group | Search for `@BOT_USERNAME` and add it. The bot introduces itself and is ready to go. |
| 2 | Use commands from chat | `/buy` to log a purchase, `/list` to see what's needed, `/meals` for recipes, `/ai` for smart queries. |
| 3 | Open the web workspace | Sign in with one tap — the bot sends you a code. Then manage everything from a clean dashboard. |

Step markup:
```html
<div class="flex flex-col gap-4">
  <div class="w-10 h-10 rounded-full bg-indigo-600 text-white flex items-center
              justify-center font-bold text-lg shrink-0">1</div>
  <h3 class="font-semibold text-gray-900 text-lg">Add the bot…</h3>
  <p class="text-gray-500 text-sm leading-relaxed">…</p>
</div>
```

---

## nginx Config (`nginx/nginx.conf`)

```nginx
events {}

http {
    include       /etc/nginx/mime.types;
    default_type  application/octet-stream;
    sendfile      on;

    server {
        listen 80;
        server_name _;

        # Landing page (static)
        root /usr/share/nginx/html;
        index index.html;

        location / {
            try_files $uri $uri/ /index.html;
            add_header Cache-Control "max-age=86400";
        }

        # Blazor web app
        location /app {
            proxy_pass         http://web:8080;
            proxy_http_version 1.1;
            proxy_set_header   Upgrade $http_upgrade;
            proxy_set_header   Connection "upgrade";
            proxy_set_header   Host $host;
            proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header   X-Forwarded-Proto $scheme;
        }

        # WebSocket support for Blazor Server
        location /app/_blazor {
            proxy_pass         http://web:8080;
            proxy_http_version 1.1;
            proxy_set_header   Upgrade $http_upgrade;
            proxy_set_header   Connection "Upgrade";
            proxy_set_header   Host $host;
        }
    }
}
```

SSL (port 443) is handled via Certbot post-deploy (see tasks §5).

---

## `docker-compose.yml` (full updated version)

```yaml
services:
  bot:
    build:
      context: ./ProductTrackerBot
      dockerfile: Dockerfile
    env_file:
      - ./.env
    environment:
      - DB_PATH=/data/product-tracker.db
    volumes:
      - db-data:/data
    restart: unless-stopped
    networks:
      - internal

  web:
    build:
      context: .
      dockerfile: ProductTrackerBot.Web/Dockerfile
    env_file:
      - ./.env
    environment:
      - DB_PATH=/data/product-tracker.db
      - ASPNETCORE_URLS=http://+:8080
      - WEB_SESSION_TTL_HOURS=24
    volumes:
      - db-data:/data
    restart: unless-stopped
    networks:
      - internal
    profiles:
      - web   # only started when web UI is ready; nginx still starts without it

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./html-landing:/usr/share/nginx/html:ro
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/certs:/etc/nginx/certs:ro   # populated by Certbot
    depends_on:
      - bot
    restart: unless-stopped
    networks:
      - internal

volumes:
  db-data:

networks:
  internal:
    driver: bridge
```

The `web` service uses a Docker Compose profile (`--profile web`) so it doesn't fail before `blazor-web-auth-scaffold` is implemented. nginx starts immediately and serves the landing page; `/app` returns 502 until `web` is started.

---

## SSL: Manual Certbot Step (post-deploy, not automated)

Once nginx is running on port 80:
```bash
docker run --rm -v $(pwd)/nginx/certs:/etc/letsencrypt \
  -v $(pwd)/html-landing:/var/www/html \
  certbot/certbot certonly --webroot \
  -w /var/www/html -d yourdomain.com \
  --email you@example.com --agree-tos --non-interactive
```

Then add the SSL server block to `nginx.conf` and `docker compose restart nginx`.

A ready-to-paste SSL server block is included in the tasks as a comment so it's available when needed.
