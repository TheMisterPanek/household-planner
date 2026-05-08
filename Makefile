.PHONY: run build test up down logs restart deploy

COMPOSE := $(shell which podman > /dev/null 2>&1 && echo "podman compose" || echo "docker compose")

run:
	dotnet run --project ProductTrackerBot

build:
	dotnet build

test:
	dotnet test

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
