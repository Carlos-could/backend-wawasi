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

## F1-T02 - Autenticacion y roles minimos (FULLSTACK backend)

- Migracion SQL en `Supabase/migrations/20260315093000_f1_t02_auth_roles.sql`
  - crea `public.profiles`
  - define roles `member` y `admin`
  - crea trigger de perfil al alta de usuario
  - aplica politicas RLS minimas
- Endpoints protegidos:
  - `GET /api/v1/auth/me` (rol minimo `member`)
  - `GET /api/v1/admin/health` (rol minimo `admin`)
- Ambos endpoints requieren header `Authorization: Bearer <access_token>` de Supabase Auth.

## F1-T02A - Registro de usuario (email + Google) y activacion inicial (backend)

- El alta de usuario ocurre en Supabase Auth (email/password u OAuth).
- La activacion inicial de perfil/rol se realiza con trigger SQL:
  - `public.handle_new_user_profile()` en `Supabase/migrations/20260315093000_f1_t02_auth_roles.sql`
  - crea fila en `public.profiles` con rol inicial `member` por defecto
- Con esto, usuarios nuevos quedan listos para login sin creacion manual en dashboard.

## F1-T03 - Modelo de datos MVP propiedades (backend)

- Migracion SQL en `Supabase/migrations/20260315121500_f1_t03_mvp_properties_model.sql`
  - crea tabla `public.properties`
  - define constraints para `property_type`, `operation_type` y `status`
  - agrega trigger para `updated_at`
  - agrega indices para filtros/sorting MVP
  - habilita RLS
    - `SELECT`: usuarios autenticados
    - `INSERT/UPDATE/DELETE`: solo `admin` (segun `public.profiles`)
- Migracion de cierre en `Supabase/migrations/20260315145000_f1_t03_complete_mvp_property_model.sql`
  - agrega campos alemanes de coste: `kaltmiete`, `nebenkosten`, `warmmiete`, `kaution`
  - crea `public.property_images` y `public.property_contacts`
  - agrega indices basicos para busqueda/listado
  - aplica RLS en tablas hijas
- Validacion CRUD base:
  - script smoke en `Supabase/tests/f1_t03_crud_smoke.sql`
