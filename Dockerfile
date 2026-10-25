# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY wwwmmm.csproj ./
COPY wwwmmm.FluidEngine/src/wwwmmm.FluidEngine.Core/wwwmmm.FluidEngine.Core.csproj ./wwwmmm.FluidEngine/src/wwwmmm.FluidEngine.Core/
RUN dotnet restore
COPY . .
RUN --mount=type=secret,id=sixlabors_license,env=SixLaborsLicenseKey,required=true \
    dotnet publish wwwmmm.csproj -c Release -o /app/publish \
    /p:UseAppHost=false

FROM build AS test
COPY --from=eclipse-temurin:25-jdk /opt/java/openjdk /opt/java/openjdk
ENV JAVA_HOME=/opt/java/openjdk
ENV PATH="/opt/java/openjdk/bin:${PATH}"
RUN apt-get update && apt-get install -y --no-install-recommends poppler-utils fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
RUN --mount=type=secret,id=sixlabors_license,env=SixLaborsLicenseKey,required=true \
    SPP_TEST_NATIVE_DOCUMENTS=1 dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj \
    -c Release \
    --logger 'trx;LogFileName=linux-tests.trx' --results-directory /verification

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl poppler-utils cups-client sane-utils fontconfig \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data \
    && chown app:app /data
WORKDIR /app
COPY --from=build /app/publish .
ENV XDG_CONFIG_HOME=/data
USER app
EXPOSE 13080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS "http://localhost:${PORT:-13080}/api/health" || exit 1
ENTRYPOINT ["dotnet", "wwwmmm.dll"]
CMD ["serve"]
