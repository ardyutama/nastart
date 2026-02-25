# Week 11: Containerization & Docker Deployment 🐳

> **Goal**: Containerize all Nastart services with production-ready Dockerfiles, understand multi-stage builds, optimize images for size and security, and orchestrate with Docker Compose.

---

## Table of Contents
1. [Day 1: Docker Fundamentals for Nastart](#day-1-docker-fundamentals-for-nastart)
2. [Day 2: .NET API Dockerfile (Multi-Stage)](#day-2-net-api-dockerfile-multi-stage)
3. [Day 3: PaddleOCR Service Dockerfile](#day-3-paddleocr-service-dockerfile)
4. [Day 4: Nuxt 4 Frontend Dockerfile](#day-4-nuxt-4-frontend-dockerfile)
5. [Day 5: Docker Compose Orchestration](#day-5-docker-compose-orchestration)
6. [Day 6: Production Hardening & Security](#day-6-production-hardening--security)
7. [Day 7: Review & Practice](#day-7-review--practice)
8. [Resources](#resources) *(Microsoft & Docker Official Docs)*

---

# Day 1: Docker Fundamentals for Nastart

## 🧒 Explain Like I'm 5

Imagine you have a toy box 📦 that contains EVERYTHING a toy needs to work — batteries included! You can carry this box anywhere, and the toy will work the same way.

**Docker** is like that magic toy box for software:
- Put your app + all its needs in a "container"
- The container runs the same on any computer
- No more "it works on my machine" problems!

## 🔧 Engineer Language

**Docker** provides containerization — packaging applications with their dependencies into isolated, reproducible environments. For Nastart, we containerize:

| Service | Technology | Why Containerize? |
|---------|------------|-------------------|
| PostgreSQL | Database | Consistent version, easy reset |
| .NET API | ASP.NET Core 10 | Isolated runtime, scalable |
| OCR Service | Python/PaddleOCR | Heavy ML dependencies isolated |
| Frontend | Nuxt 4/Node.js | Static build, CDN-ready |

> **Note**: The Telegram bot runs *inside* the API service (webhook-based).
> It is NOT a separate worker service. If you need to extract it later for
> independent scaling, that's a Phase 5+ optimization.

> 📖 **Microsoft Docs**: *"Docker enables developers to package applications into containers—standardized executable components combining application source code with the OS libraries and dependencies required to run that code in any environment."*
>
> — [Docker overview](https://learn.microsoft.com/en-us/dotnet/core/docker/introduction)

### Nastart Container Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Docker Host (Your Machine)                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────── nastart-frontend network ───────────────┐ │
│  │                                                             │ │
│  │  ┌──────────────┐         ┌──────────────┐                 │ │
│  │  │   Frontend   │◄───────►│     API      │                 │ │
│  │  │  (Nuxt 4)    │  HTTP   │  (.NET 10)   │                 │ │
│  │  │  :3000       │         │  :8080       │                 │ │
│  │  └──────────────┘         └──────┬───────┘                 │ │
│  │                                   │                         │ │
│  └───────────────────────────────────┼─────────────────────────┘ │
│                                      │                           │
│  ┌─────────────────── nastart-backend network ────────────────┐ │
│  │                           │                                 │ │
│  │  ┌──────────────┐         │         ┌──────────────┐       │ │
│  │  │  PostgreSQL  │◄────────┼────────►│  OCR Service │       │ │
│  │  │  :5432       │         │         │  (PaddleOCR) │       │ │
│  │  └──────────────┘         │         │  :8001       │       │ │
│  │                                     └──────────────┘       │ │
│  │                                                             │ │
│  │  Note: Bot webhook runs inside the API container.          │ │
│  │  No separate Bot Worker service needed.                    │ │
│  │                                                             │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  Volume: nastart_postgres_data (persistent)                     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Project Structure with Dockerfiles

```
nastart/
├── .env.example                    # Environment template
├── .dockerignore                   # Build context exclusions
├── docker-compose.yml              # Service orchestration
│
├── backend/
│   ├── Nastart.slnx
│   └── src/
│       └── Nastart.Api/
│           └── Dockerfile          # API Dockerfile (includes bot webhook handler)
│
├── ocr-service/
│   ├── app/
│   │   ├── main.py
│   │   └── ...
│   ├── requirements.txt
│   └── Dockerfile                  # OCR Dockerfile
│
└── frontend/
    ├── nuxt.config.ts
    └── Dockerfile                  # Frontend Dockerfile
```

### Your Task (Day 1):

1. Install Docker Desktop for Windows
2. Verify installation: `docker --version` and `docker compose version`
3. Copy `.env.example` to `.env` and fill in your values
4. Review the architecture diagram above

---

# Day 2: .NET API Dockerfile (Multi-Stage)

## 🧒 Explain Like I'm 5

Building a LEGO castle 🏰 has two parts:
1. **Building** — You need ALL the LEGO pieces, instruction manual, tools
2. **Playing** — You only need the finished castle!

Multi-stage Docker builds work the same way:
1. **Build stage** — Has compilers, SDKs, all building tools
2. **Runtime stage** — Only the finished app, tiny and fast!

## 🔧 Engineer Language

**Multi-stage builds** separate compilation from runtime, dramatically reducing image size and attack surface. For .NET:
- **Build stage**: Uses SDK image (~700MB) to compile
- **Runtime stage**: Uses ASP.NET runtime image (~100MB)

> 📖 **Microsoft Docs**: *"Multi-stage builds are useful when you want to build in a full SDK environment, but you want to deploy to a smaller runtime-only image."*
>
> — [Docker images for ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images)

### Dockerfile: backend/src/Nastart.Api/Dockerfile

```dockerfile
# ==========================================
# Nastart API Dockerfile
# Multi-stage build for .NET 10 Minimal API
# ==========================================

# ==========================================
# STAGE 1: Build
# Restore and compile in SDK environment
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

# Set working directory
WORKDIR /src

# Copy project files first (layer caching optimization)
# This way, dependencies are only restored when .csproj changes
COPY src/Nastart.Api/Nastart.Api.csproj Nastart.Api/

# Restore dependencies (cached unless .csproj changes)
RUN dotnet restore Nastart.Api/Nastart.Api.csproj

# Copy all source code
COPY src/Nastart.Api/ Nastart.Api/

# Build in Release mode
WORKDIR /src/Nastart.Api
RUN dotnet build -c Release -o /app/build

# ==========================================
# STAGE 2: Publish
# Create optimized production artifacts
# ==========================================
FROM build AS publish

# Publish with optimizations
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:PublishTrimmed=false

# ==========================================
# STAGE 3: Runtime (Production)
# Minimal, secure runtime image
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Security: Create non-root user with specific UID/GID
# This prevents container escape vulnerabilities
RUN addgroup -g 1001 -S appgroup && \
    adduser -S appuser -u 1001 -G appgroup

# Set working directory
WORKDIR /app

# Copy published artifacts with correct ownership
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Security: Switch to non-root user
USER 1001

# Expose the port ASP.NET listens on
EXPOSE 8080

# Health check for container orchestration
# Verifies the /health endpoint responds
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "Nastart.Api.dll"]
```

### Understanding Each Section

| Stage | Image Size | Purpose |
|-------|------------|---------|
| `build` | ~700MB | Has .NET SDK, compiles code |
| `publish` | ~700MB | Creates optimized output |
| `runtime` | ~100MB | Only ASP.NET runtime + your app |

### Layer Caching Strategy

```dockerfile
# ✅ GOOD: Copy .csproj first, then restore
COPY Nastart.Api.csproj ./
RUN dotnet restore

# ✅ THEN: Copy source code
COPY . .
RUN dotnet build

# ❌ BAD: Copy everything, then restore (no caching benefit)
COPY . .
RUN dotnet restore && dotnet build
```

### Build and Test

```powershell
cd C:\Users\AU1833\Documents\personal\nastart

# Build the API image
docker build -t nastart-api:dev -f backend/src/Nastart.Api/Dockerfile ./backend

# Check image size
docker images nastart-api:dev

# Run locally for testing
docker run -d -p 5000:8080 --name test-api nastart-api:dev

# Check health
curl http://localhost:5000/health

# Cleanup
docker stop test-api && docker rm test-api
```

### Your Task (Day 2):

1. Create `backend/src/Nastart.Api/Dockerfile` with the content above
2. Build the image and verify size is under 150MB
3. Test the health endpoint works

---

# Day 3: PaddleOCR Service Dockerfile

## 🧒 Explain Like I'm 5

PaddleOCR is like a very smart friend 🧠 who can read text from any picture. But this friend needs:
- A big brain (the ML model, ~1GB!)
- Special glasses (image processing libraries)
- A comfy room to work in (Python environment)

We give our friend everything they need in one container!

## 🔧 Engineer Language

**PaddleOCR** requires specific system libraries (OpenCV, libGL) and downloads large ML models on first run. We optimize by:
- Installing minimal system dependencies
- Using `--no-cache-dir` to reduce layer size
- Setting proper health checks with model load time

> 📖 **FastAPI Docs**: *"FastAPI is a modern, fast web framework for building APIs with Python based on standard Python type hints."*
>
> — [FastAPI Documentation](https://fastapi.tiangolo.com/)

### Dockerfile: ocr-service/Dockerfile

```dockerfile
# ==========================================
# Nastart OCR Service Dockerfile
# PaddleOCR with FastAPI
# ==========================================

# Use slim Python image (smaller than full python image)
FROM python:3.12-slim AS runtime

# Set working directory
WORKDIR /app

# Install system dependencies required by PaddleOCR
# - libgl1-mesa-glx: OpenGL support for image processing
# - libglib2.0-0: GLib library
# - libsm6, libxext6, libxrender-dev: X11 libraries for OpenCV
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1-mesa-glx \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    wget \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Copy requirements first (layer caching)
COPY requirements.txt .

# Install Python dependencies
# --no-cache-dir reduces image size
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

# Security: Create non-root user
RUN groupadd -g 1001 appgroup && \
    useradd -u 1001 -g appgroup -s /bin/bash appuser

# Copy application code with correct ownership
COPY --chown=appuser:appgroup app/ ./app/

# Security: Switch to non-root user
USER 1001

# Expose the FastAPI port
EXPOSE 8001

# Health check
# Note: start_period is 60s because PaddleOCR model takes time to load
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD python -c "import httpx; httpx.get('http://localhost:8001/health').raise_for_status()" || exit 1

# Environment variables
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

# Run FastAPI with uvicorn
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8001"]
```

### Optimized requirements.txt

```txt
# ocr-service/requirements.txt
# Core web framework
fastapi==0.115.0
uvicorn[standard]==0.32.0
python-multipart==0.0.12

# PaddleOCR and dependencies
paddlepaddle==2.6.2
paddleocr==2.8.1

# Image processing
pillow==11.0.0
numpy==2.1.0

# Data validation
pydantic==2.9.0
pydantic-settings==2.5.0

# HTTP client for health checks
httpx==0.27.2
```

### Build and Test

```powershell
cd C:\Users\AU1833\Documents\personal\nastart

# Build the OCR image (this takes longer due to ML dependencies)
docker build -t nastart-ocr:dev -f ocr-service/Dockerfile ./ocr-service

# Check image size (expect ~2-3GB due to PaddleOCR)
docker images nastart-ocr:dev

# Run and test
docker run -d -p 8001:8001 --name test-ocr nastart-ocr:dev

# Wait for model to load (check logs)
docker logs -f test-ocr

# Test health endpoint
curl http://localhost:8001/health

# Cleanup
docker stop test-ocr && docker rm test-ocr
```

### Your Task (Day 3):

1. Create `ocr-service/Dockerfile` with the content above
2. Create `ocr-service/requirements.txt`
3. Build and test the health endpoint
4. Note: First build downloads ~1GB of models

---

# Day 4: Nuxt 4 Frontend Dockerfile

## 🧒 Explain Like I'm 5

Building a website is like building a LEGO display 🎨:
1. **Build time**: Put all the pieces together (compile Vue/Nuxt)
2. **Display time**: Show the finished creation (serve static files)

For the display, we don't need all our LEGO tools — just a nice shelf (nginx) to show our work!

## 🔧 Engineer Language

**Nuxt 4** can generate static sites or run as a server. We use multi-stage builds:
1. **Build stage**: Node.js compiles TypeScript, bundles assets
2. **Runtime stage**: Nginx serves static files OR Node.js runs SSR

> 📖 **Nuxt Docs**: *"Nuxt uses Nitro server engine that can run on various platforms including Node.js, Cloudflare Workers, and more."*
>
> — [Nuxt Deployment](https://nuxt.com/docs/getting-started/deployment)

### Dockerfile: frontend/Dockerfile (SSR Mode)

```dockerfile
# ==========================================
# Nastart Frontend Dockerfile
# Nuxt 4 with Node.js runtime (SSR)
# ==========================================

# ==========================================
# STAGE 1: Dependencies
# Install node_modules for caching
# ==========================================
FROM node:22-alpine AS deps

WORKDIR /app

# Copy package files
COPY package.json package-lock.json* ./

# Install dependencies
RUN npm ci --prefer-offline

# ==========================================
# STAGE 2: Build
# Compile Nuxt application
# ==========================================
FROM node:22-alpine AS build

WORKDIR /app

# Copy dependencies from deps stage
COPY --from=deps /app/node_modules ./node_modules

# Copy source code
COPY . .

# Build the Nuxt application
RUN npm run build

# ==========================================
# STAGE 3: Runtime
# Minimal Node.js runtime for SSR
# ==========================================
FROM node:22-alpine AS runtime

# Security: Create non-root user
RUN addgroup -g 1001 -S nodejs && \
    adduser -S nuxtjs -u 1001 -G nodejs

WORKDIR /app

# Copy built application
COPY --from=build --chown=nuxtjs:nodejs /app/.output ./.output
COPY --from=build --chown=nuxtjs:nodejs /app/package.json ./

# Switch to non-root user
USER 1001

# Expose the Nuxt port
EXPOSE 3000

# Environment variables
ENV NODE_ENV=production \
    NUXT_HOST=0.0.0.0 \
    NUXT_PORT=3000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:3000 || exit 1

# Start Nuxt server
CMD ["node", ".output/server/index.mjs"]
```

### Alternative: Static Site with Nginx

If you generate static files (`nuxt generate`), use this lighter Dockerfile:

```dockerfile
# ==========================================
# Nastart Frontend Dockerfile (Static)
# Nuxt 4 with Nginx for static serving
# ==========================================

# ==========================================
# STAGE 1: Build
# ==========================================
FROM node:22-alpine AS build

WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm ci

COPY . .
RUN npm run generate

# ==========================================
# STAGE 2: Runtime (Nginx)
# ==========================================
FROM nginx:alpine AS runtime

# Copy static files
COPY --from=build /app/.output/public /usr/share/nginx/html

# Copy custom nginx config (optional)
# COPY nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
```

### Build and Test

```powershell
cd C:\Users\AU1833\Documents\personal\nastart

# Build the frontend image
docker build -t nastart-frontend:dev -f frontend/Dockerfile ./frontend

# Run locally
docker run -d -p 3000:3000 --name test-frontend nastart-frontend:dev

# Test
curl http://localhost:3000

# Cleanup
docker stop test-frontend && docker rm test-frontend
```

### Your Task (Day 4):

1. Initialize Nuxt if not exists: `npx nuxi@latest init frontend`
2. Create `frontend/Dockerfile` with SSR or Static version
3. Build and test the image

---

# Day 5: Docker Compose Orchestration

## 🧒 Explain Like I'm 5

Docker Compose is like a conductor 🎼 of an orchestra:
- The conductor tells each musician when to start playing
- Makes sure the drums start before the melody
- Keeps everyone in sync!

Docker Compose tells our containers:
- "Database, start first!"
- "API, wait until database is healthy!"
- "Everyone, connect on this network!"

## 🔧 Engineer Language

**Docker Compose** defines multi-container applications declaratively. Key concepts:
- **Services**: Container definitions
- **Networks**: Isolated communication channels
- **Volumes**: Persistent data storage
- **Depends_on**: Startup ordering with health conditions

> 📖 **Docker Docs**: *"Compose is a tool for defining and running multi-container Docker applications using a YAML file."*
>
> — [Docker Compose overview](https://docs.docker.com/compose/)

### Complete docker-compose.yml Explanation

```yaml
# docker-compose.yml - Nastart Development Environment

services:
  # ==========================================
  # PostgreSQL Database
  # ==========================================
  postgres:
    image: postgres:18-alpine          # Official PostgreSQL Alpine image
    container_name: nastart-db         # Human-readable name
    restart: unless-stopped            # Auto-restart unless manually stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-nastart}           # From .env or default
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-nastart_dev_password}
      POSTGRES_DB: ${POSTGRES_DB:-nastart}
    ports:
      - "5432:5432"                     # Host:Container port mapping
    volumes:
      - nastart_postgres_data:/var/lib/postgresql/data   # Persist data
    networks:
      - nastart-backend                 # Internal network
    healthcheck:                        # Verify database is ready
      test: ["CMD-SHELL", "pg_isready -U nastart"]
      interval: 10s                     # Check every 10 seconds
      timeout: 5s                       # Fail if no response in 5s
      retries: 5                        # Healthy after 5 successful checks
      start_period: 10s                 # Wait 10s before first check
    deploy:
      resources:
        limits:
          cpus: '1.0'                   # Max 1 CPU core
          memory: 1G                    # Max 1GB RAM
        reservations:
          cpus: '0.5'                   # Reserve 0.5 cores
          memory: 512M                  # Reserve 512MB

  # ==========================================
  # PaddleOCR Python Microservice
  # ==========================================
  ocr-service:
    build:
      context: ./ocr-service            # Build context directory
      dockerfile: Dockerfile            # Dockerfile location
    container_name: nastart-ocr
    restart: unless-stopped
    environment:
      - PYTHONUNBUFFERED=1              # Real-time Python logging
      - OCR_LANG=${OCR_LANG:-id}        # Indonesian OCR model
    ports:
      - "8001:8001"
    networks:
      - nastart-backend
    healthcheck:
      test: ["CMD", "python", "-c", "import httpx; httpx.get('http://localhost:8001/health').raise_for_status()"]
      interval: 30s
      timeout: 10s
      start_period: 60s                 # PaddleOCR model takes time to load
      retries: 3
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 2G                    # PaddleOCR needs ~1.5GB
        reservations:
          memory: 1G

  # ==========================================
  # .NET API Service
  # ==========================================
  api:
    build:
      context: ./backend
      dockerfile: src/Nastart.Api/Dockerfile
      target: runtime                   # Use runtime stage from multi-stage build
    container_name: nastart-api
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=${POSTGRES_DB:-nastart};Username=${POSTGRES_USER:-nastart};Password=${POSTGRES_PASSWORD:-nastart_dev_password}
      - OcrService__BaseUrl=http://ocr-service:8001    # Service discovery by name
      - Telegram__BotToken=${TELEGRAM_BOT_TOKEN}
      - Telegram__WebhookUrl=${TELEGRAM_WEBHOOK_URL}
      - Telegram__SecretToken=${TELEGRAM_SECRET_TOKEN}
    ports:
      - "5000:8080"                      # Access API on localhost:5000
    depends_on:
      postgres:
        condition: service_healthy       # Wait for DB health check
      ocr-service:
        condition: service_healthy       # Wait for OCR health check
    networks:
      - nastart-backend                  # Can talk to postgres, ocr-service
      - nastart-frontend                 # Can be accessed by frontend
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M

networks:
  nastart-backend:
    driver: bridge                       # Default Docker network driver
  nastart-frontend:
    driver: bridge

volumes:
  nastart_postgres_data:
    driver: local                        # Store on local filesystem
```

### Essential Docker Compose Commands

```powershell
cd C:\Users\AU1833\Documents\personal\nastart

# Build all images
docker compose build

# Start all services (detached mode)
docker compose up -d

# View logs
docker compose logs -f              # All services
docker compose logs -f api          # Specific service

# Check service status
docker compose ps

# Stop all services
docker compose down

# Stop and remove volumes (DELETES DATA!)
docker compose down -v

# Rebuild specific service
docker compose build api
docker compose up -d api

# Execute command in running container
docker compose exec api sh          # Shell access
docker compose exec postgres psql -U nastart   # PostgreSQL CLI
```

### Your Task (Day 5):

1. Copy `.env.example` to `.env` and configure values
2. Run `docker compose up -d`
3. Verify all services are healthy: `docker compose ps`
4. Test endpoints: `curl http://localhost:5000/health`

---

# Day 6: Production Hardening & Security

## 🧒 Explain Like I'm 5

When you have a real store 🏪, you need:
- Locks on the doors (security)
- Fire alarms (monitoring)
- Rules about who can enter (access control)

Production containers need the same protections!

## 🔧 Engineer Language

**Production hardening** involves security best practices, resource management, and operational readiness. Key areas:
1. **Non-root users**: Prevent container escape
2. **Secrets management**: Don't expose credentials
3. **Resource limits**: Prevent runaway containers
4. **Network isolation**: Minimize attack surface
5. **Health checks**: Enable orchestrator recovery

> 📖 **Docker Docs**: *"Build images from Dockerfiles that exercise certain security principles: use minimal base images, run as non-root, scan for vulnerabilities."*
>
> — [Docker Security Best Practices](https://docs.docker.com/build/building/best-practices/)

### Security Checklist

| Check | Development | Production |
|-------|-------------|------------|
| Non-root user | ✅ Recommended | ✅ Required |
| Secrets in env vars | ⚠️ OK for dev | ❌ Use Docker secrets |
| Resource limits | ⚠️ Optional | ✅ Required |
| Health checks | ✅ Recommended | ✅ Required |
| Network isolation | ⚠️ Optional | ✅ Required |
| Read-only filesystem | ❌ Inconvenient | ✅ When possible |
| Image scanning | ⚠️ Optional | ✅ Required |

### Production docker-compose.prod.yml

```yaml
# docker-compose.prod.yml - Production overrides
# Usage: docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

services:
  postgres:
    environment:
      # Use Docker secrets in production
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password
    secrets:
      - db_password
    networks:
      nastart-backend:
        aliases:
          - database

  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    deploy:
      replicas: 2                      # Run 2 instances for availability
      update_config:
        parallelism: 1                 # Rolling update
        delay: 10s
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3

secrets:
  db_password:
    external: true                     # Created outside compose

networks:
  nastart-backend:
    internal: true                     # No external access
```

### .dockerignore Best Practices

```gitignore
# .dockerignore - Exclude from build context

# Version control
.git/
.gitignore

# IDE
.idea/
.vscode/
*.swp

# Build outputs
bin/
obj/
node_modules/
dist/
.nuxt/
.output/

# Python
__pycache__/
*.pyc
venv/
.venv/

# Secrets - NEVER include!
.env
*.env
appsettings.*.json
*.pem
*.key

# Documentation
*.md
docs/

# Tests (unless needed in container)
tests/
**/test/
**/*.test.*

# OS files
.DS_Store
Thumbs.db

# Docker files (meta)
Dockerfile*
docker-compose*.yml
```

### Image Security Scanning

```powershell
# Scan images for vulnerabilities
docker scout quickview nastart-api:dev
docker scout cves nastart-api:dev

# Alternative: Trivy scanner
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy image nastart-api:dev
```

### Your Task (Day 6):

1. Review all Dockerfiles for security best practices
2. Verify non-root users are configured
3. Run `docker scout` on your images
4. Create `.dockerignore` if not exists

---

# Resources

## Official Documentation

| Topic | Link |
|-------|------|
| Docker Images for .NET | [Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/core/docker/building-net-docker-images) |
| ASP.NET Core Docker | [Microsoft Docs](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images) |
| Docker Compose | [Docker Docs](https://docs.docker.com/compose/) |
| Dockerfile Best Practices | [Docker Docs](https://docs.docker.com/build/building/best-practices/) |
| FastAPI Deployment | [FastAPI Docs](https://fastapi.tiangolo.com/deployment/docker/) |
| Nuxt Deployment | [Nuxt Docs](https://nuxt.com/docs/getting-started/deployment) |

## Quick Reference

### Docker Commands Cheat Sheet

```powershell
# Images
docker build -t name:tag .              # Build image
docker images                           # List images
docker rmi image_name                   # Remove image
docker system prune -a                  # Clean unused images

# Containers
docker run -d -p 8080:80 image_name     # Run detached
docker ps                               # List running
docker ps -a                            # List all
docker stop container_name              # Stop
docker rm container_name                # Remove
docker logs -f container_name           # Follow logs
docker exec -it container_name sh       # Shell access

# Compose
docker compose up -d                    # Start all
docker compose down                     # Stop all
docker compose logs -f service_name     # Service logs
docker compose exec service_name sh     # Service shell
docker compose build --no-cache         # Force rebuild
```

### Environment Variables in Docker

| Method | Development | Production |
|--------|-------------|------------|
| `.env` file | ✅ Convenient | ⚠️ Not secure |
| `docker compose --env-file` | ✅ Good | ⚠️ Limited |
| Docker secrets | ❌ Complex | ✅ Secure |
| External vault | ❌ Overkill | ✅ Enterprise |

---

# Day 7: Review & Practice

Review all Dockerfiles created this week and practice the following:

1. **Build all images** from scratch and verify sizes
2. **Run `docker compose up`** and test the full stack end-to-end
3. **Inspect running containers** — check logs, resource usage, health status
4. **Practice debugging** — use `docker exec -it <container> sh` to investigate issues
5. **Review security** — ensure all containers run as non-root, no secrets in images

> **Architecture reminder**: The Telegram bot runs inside `Nastart.Api` via webhook endpoints.
> There is no separate `Nastart.Bot` worker service. If scaling demands it in the future,
> the bot handlers can be extracted into a dedicated Worker Service (Phase 5+).

---

## Summary

| Day | Topic | Key Takeaway |
|-----|-------|--------------|
| 1 | Docker Fundamentals | Containers = portable, reproducible environments |
| 2 | .NET API Dockerfile | Multi-stage builds reduce image size 7x |
| 3 | OCR Service Dockerfile | Python ML containers need system libs |
| 4 | Frontend Dockerfile | SSR = Node.js, Static = Nginx |
| 5 | Docker Compose | Orchestrate dependencies with health checks |
| 6 | Production Security | Non-root, secrets, scanning, limits |
| 7 | Review & Practice | Consolidation and practice day |

---

**Next Week**: Week 12 — CI/CD with GitHub Actions 🚀
