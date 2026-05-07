.PHONY: run build test up down logs restart

run:
	dotnet run --project ProductTrackerBot

build:
	dotnet build

test:
	dotnet test

up:
	docker compose up --build -d

down:
	docker compose down

logs:
	docker compose logs -f

restart:
	docker compose up --build -d --force-recreate
