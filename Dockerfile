FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY MS.Cobroup.BackendAdmin.Domain/*.csproj MS.Cobroup.BackendAdmin.Domain/
COPY MS.Cobroup.BackendAdmin.Shared/*.csproj MS.Cobroup.BackendAdmin.Shared/
COPY MS.Cobroup.BackendAdmin.Core/*.csproj MS.Cobroup.BackendAdmin.Core/
COPY MS.Cobroup.BackendAdmin.Infrastructure/*.csproj MS.Cobroup.BackendAdmin.Infrastructure/
COPY MS.Cobroup.BackendAdmin.Api/*.csproj MS.Cobroup.BackendAdmin.Api/
COPY MS.Cobroup.BackendAdmin.slnx .

RUN dotnet restore MS.Cobroup.BackendAdmin.Api/MS.Cobroup.BackendAdmin.Api.csproj

COPY . .
RUN dotnet publish MS.Cobroup.BackendAdmin.Api/MS.Cobroup.BackendAdmin.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

RUN addgroup --system --gid 1000 appgroup && \
    adduser --system --uid 1000 --ingroup appgroup --shell /sbin/nologin appuser

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser
EXPOSE 8080

ENTRYPOINT ["dotnet", "MS.Cobroup.BackendAdmin.Api.dll"]
