# ==========================================
# STAGE 1: Build
# Restore and compile in one stage for reliability
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy only project files first (layer caching optimization)
COPY src/Nastart.Domain/Nastart.Domain.csproj Nastart.Domain/
COPY src/Nastart.Application/Nastart.Application.csproj Nastart.Application/
COPY src/Nastart.Infrastructure/Nastart.Infrastructure.csproj Nastart.Infrastructure/
COPY src/Nastart.Api/Nastart.Api.csproj Nastart.Api/

# Restore dependencies (cached unless .csproj changes)
RUN dotnet restore Nastart.Api/Nastart.Api.csproj

# Copy source code
COPY src/ .

# Build in Release mode (allow implicit restore since COPY overwrites obj folders)
RUN dotnet build Nastart.Api/Nastart.Api.csproj -c Release

# ==========================================
# STAGE 2: Publish
# Prepare runtime artifacts
# ==========================================
FROM build AS publish
WORKDIR /src
RUN dotnet publish Nastart.Api/Nastart.Api.csproj -c Release -o /app/publish --no-build

# ==========================================
# STAGE 3: Runtime (Production)
# Minimal, secure runtime image
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Security: Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -S appuser -u 1001 -G appgroup

WORKDIR /app

# Copy published artifacts with correct ownership
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Security: Run as non-root user
USER 1001

# Health check for container monitoring
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "Nastart.Api.dll"]