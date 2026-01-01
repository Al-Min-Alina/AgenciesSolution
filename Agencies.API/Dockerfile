# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and projects
COPY ["Agencies.API/Agencies.API.csproj", "Agencies.API/"]
COPY ["Agencies.Core/Agencies.Core.csproj", "Agencies.Core/"]
COPY ["Agencies.Domain/Agencies.Domain.csproj", "Agencies.Domain/"]
COPY ["Agencies.Infrastructure/Agencies.Infrastructure.csproj", "Agencies.Infrastructure/"]

# Restore packages
RUN dotnet restore "Agencies.API/Agencies.API.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR "/src/Agencies.API"
RUN dotnet build "Agencies.API.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Agencies.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN groupadd -r appgroup && useradd -r -g appgroup appuser
USER appuser

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:7149
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Expose port
EXPOSE 7149

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:7149/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Agencies.API.dll"]