.PHONY: run build test test-e2e up down logs restart deploy up-dev down-dev logs-dev restart-dev

COMPOSE      := docker compose
COMPOSE_DEV  := docker compose -f docker-compose.dev.yml

run:
	dotnet run --project ProductTrackerBot

build:
	dotnet build

test:
	dotnet test ProductTrackerBot.Tests

test-e2e:
	dotnet test ProductTrackerBot.Web.Tests.E2E

# --- Production ---
up:
	$(COMPOSE) up --build -d

down:
	$(COMPOSE) down

logs:
	$(COMPOSE) logs -f

restart:
	$(COMPOSE) up --build -d --force-recreate

deploy:
	git push

# --- Development ---
up-dev:
	$(COMPOSE_DEV) up --build -d

down-dev:
	$(COMPOSE_DEV) down

logs-dev:
	$(COMPOSE_DEV) logs -f

restart-dev:
	$(COMPOSE_DEV) up --build -d --force-recreate
