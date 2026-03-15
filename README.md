# backend-wawasi

Base tecnica del backend del MVP Wawasi.

## Stack

- ASP.NET Core Web API (.NET 9)
- OpenAPI en entorno `Development`
- Healthcheck en `/health`
- Configuracion base de Supabase (Auth/DB/Storage) via `SupabaseOptions`

## Setup local

1. Restaura dependencias:

```bash
dotnet restore
```

2. Crea variables de entorno:

```bash
copy .env.example .env
```

3. Exporta variables al shell (o configuralas en tu entorno):
- `Supabase__Url`
- `Supabase__AnonKey`
- `Supabase__ServiceRoleKey`

4. Ejecuta API:

```bash
dotnet run --project backend-wawasi.csproj
```

## Endpoints base

- `GET /`: estado del servicio
- `GET /health`: healthcheck
