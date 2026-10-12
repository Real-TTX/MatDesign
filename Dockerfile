# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
# The build runs on the builder's own platform: the published app is portable (no native host), so the arm64 image needs no emulation here.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG Channel=local
ARG BuildNumber=0
ARG BuildDate=
WORKDIR /src

# Restore first (better layer caching).
COPY ["global.json", "Directory.Build.props", "CHANGELOG.md", "./"]
COPY ["src/MatUi/MatUi.csproj", "src/MatUi/"]
COPY ["src/MatDesign.Core/MatDesign.Core.csproj", "src/MatDesign.Core/"]
COPY ["src/MatDesign.Web/MatDesign.Web.csproj", "src/MatDesign.Web/"]
RUN dotnet restore "src/MatDesign.Web/MatDesign.Web.csproj"

COPY src src
# Fall back to today's date when BuildDate is not supplied, so the version never ends in "-".
RUN BUILD_DATE="${BuildDate:-$(date -u +%Y%m%d)}" && \
    dotnet publish "src/MatDesign.Web/MatDesign.Web.csproj" \
    -c Release -o /app/publish --no-restore \
    -p:UseAppHost=false \
    -p:Channel=$Channel -p:BuildNumber=$BuildNumber -p:BuildDate=$BUILD_DATE

# ---------------------------------------------------------------------------
# Runtime stage (ASP.NET Core base image)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8060 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0 \
    DOTNET_TieredPGO=1

EXPOSE 8060

COPY --from=build /app/publish .

# The site keeps no state of its own. Only the Studio (docs/STUDIO.md) writes, and only into /data, which is a volume the unprivileged user may write to.
# No socket, no secrets in the image: the administrator is created in the setup assistant on the first visit and kept, as a salted hash, in the volume.
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data
ENV MATDESIGN_DATA=/data
USER $APP_UID

# The app checks itself, so the image needs no extra tools.
HEALTHCHECK --interval=30s --timeout=8s --start-period=20s --retries=3 \
    CMD ["dotnet", "MatDesign.Web.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "MatDesign.Web.dll"]
