# MS.Cobroup.BackendAdmin

Microservicio administrativo de COBROUP. La fase 1 contiene solo la base hexagonal, un contrato inicial de autorización administrativa y un health check; no incluye CRUD ni persistencia.

## Requisitos y ejecución

```powershell
dotnet restore MS.Cobroup.BackendAdmin.slnx
dotnet build MS.Cobroup.BackendAdmin.slnx --no-restore
dotnet test MS.Cobroup.BackendAdmin.slnx --no-build
dotnet run --project MS.Cobroup.BackendAdmin.Api --urls http://127.0.0.1:5082
```

El endpoint técnico es `GET /health`. La identidad real y las APIs administrativas requieren decisiones posteriores.
