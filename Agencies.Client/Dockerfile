# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and projects
COPY ["Agencies.Client/Agencies.Client.csproj", "Agencies.Client/"]
COPY ["Agencies.Core/Agencies.Core.csproj", "Agencies.Core/"]

# Restore packages
RUN dotnet restore "Agencies.Client/Agencies.Client.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR "/src/Agencies.Client"
RUN dotnet build "Agencies.Client.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Agencies.Client.csproj" -c Release -o /app/publish \
    -p:UseAppHost=true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# Install required dependencies for WPF
RUN apt-get update && \
    apt-get install -y \
    libfontconfig1 \
    libx11-6 \
    libxext6 \
    libxrender1 \
    libxtst6 \
    && rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN groupadd -r appgroup && useradd -r -g appgroup appuser
USER appuser

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Entry point
ENTRYPOINT ["./Agencies.Client"]