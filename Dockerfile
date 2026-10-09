FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY wwwmmm.csproj ./
RUN dotnet restore wwwmmm.csproj
COPY . .
RUN dotnet publish wwwmmm.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl poppler-utils cups-client sane-utils \
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
