# syntax=docker/dockerfile:1.7
# Multi-stage. ARM64 (Oracle Ampere A1) 가정 — 공식 .NET 이미지는 multi-arch라 그대로 동작.

# ── Build stage ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 의존성 캐시 우선 — csproj만 먼저 복원
COPY Lime.Api/Lime.Api.csproj ./Lime.Api/
RUN dotnet restore Lime.Api/Lime.Api.csproj

# 본 빌드
COPY . .
RUN dotnet publish Lime.Api/Lime.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Runtime stage ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 비루트 유저로 실행
RUN groupadd --system --gid 1001 lime \
 && useradd  --system --uid 1001 --gid lime --shell /usr/sbin/nologin lime

# 영속 디렉토리 미리 생성 + 소유권 — named volume이 첫 마운트 시 권한 상속
RUN mkdir -p /app/wwwroot/uploads/avatars /app/.aspnet/DataProtection-Keys \
 && chown -R lime:lime /app

COPY --from=build --chown=lime:lime /app/publish ./

USER lime

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_GCServer=1 \
    DOTNET_TieredPGO=1

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- http://localhost:8080/health/db || exit 1

ENTRYPOINT ["dotnet", "Lime.Api.dll"]
