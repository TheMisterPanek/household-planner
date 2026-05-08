# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Install native compilation tools required for AOT
RUN apt-get update && apt-get install -y \
    clang \
    zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Restore packages (separate layer for cache)
COPY ProductTrackerBot.csproj .
RUN dotnet restore ProductTrackerBot.csproj

# Copy all source files
COPY . .

# Publish as AOT binary
RUN dotnet publish -c Release -r linux-x64 --self-contained /p:PublishAot=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0

WORKDIR /app

# Copy the AOT binary from build stage
COPY --from=build /src/bin/Release/net9.0/linux-x64/publish/ProductTrackerBot .

# Create .env placeholder for configuration
RUN echo "BOT_TOKEN=" > .env.example

# Set the entrypoint
ENTRYPOINT ["./ProductTrackerBot"]
