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

## F1-T02/F1-T04 - Autenticacion y roles (FULLSTACK backend)

- Migracion SQL en `Supabase/migrations/20260315093000_f1_t02_auth_roles.sql`
  - crea `public.profiles`
  - define roles base de perfiles
  - crea trigger de perfil al alta de usuario
  - aplica politicas RLS minimas
- Endpoints protegidos:
  - `GET /api/v1/auth/me` (rol minimo `inquilino`)
  - `GET /api/v1/admin/health` (rol minimo `admin`)
- Ambos endpoints requieren header `Authorization: Bearer <access_token>` de Supabase Auth.

## F1-T02A - Registro de usuario (email + Google) y activacion inicial (backend)

- El alta de usuario ocurre en Supabase Auth (email/password u OAuth).
- La activacion inicial de perfil/rol se realiza con trigger SQL:
  - `public.handle_new_user_profile()` en `Supabase/migrations/20260315093000_f1_t02_auth_roles.sql`
  - crea fila en `public.profiles` con rol inicial por defecto
- Con esto, usuarios nuevos quedan listos para login sin creacion manual en dashboard.

## F1-T04 - Crear/editar propiedad (backend)

- Migracion SQL en `Supabase/migrations/20260315190000_f1_t04_roles_and_properties_rls.sql`
  - migra enum de rol a `inquilino`, `propietario`, `admin`
  - migra `member -> propietario`
  - ajusta policies RLS de `properties` para `propietario` dueno y `admin`
- Endpoints de propiedades:
  - `POST /api/v1/properties`
  - `GET /api/v1/properties/{id}`
  - `PUT /api/v1/properties/{id}`
- Reglas:
  - crear: `propietario`/`admin`
  - editar: solo dueno o `admin`
  - estado permitido en este flujo: `draft` o `published`
- Test SQL de regresion:
  - `Supabase/tests/f1_t04_properties_rls.sql`

## F1-T05 - Gestion de fotos de propiedad (backend)

- Migracion SQL en `Supabase/migrations/20260315203000_f1_t05_property_photos_management.sql`
  - crea/actualiza bucket publico `property-images` (8MB, JPG/PNG/WEBP)
  - ajusta policies RLS de `property_images` para `propietario` dueno y `admin`
  - agrega constraint de orden no negativo y unicidad de imagen principal por propiedad
- Endpoints de fotos:
  - `GET /api/v1/properties/{id}/photos`
  - `POST /api/v1/properties/{id}/photos` (`multipart/form-data`, campo `files[]`)
  - `PATCH /api/v1/properties/{id}/photos/order`
  - `PATCH /api/v1/properties/{id}/photos/{photoId}/primary`
  - `DELETE /api/v1/properties/{id}/photos/{photoId}`
- Reglas:
  - maximo 15 fotos por propiedad
  - formatos permitidos: JPG, PNG, WEBP
  - tamano maximo: 8MB por archivo
  - si no hay principal, la primera foto subida se marca como principal
  - si se elimina la principal, se promueve la primera por `sort_order`
- Test SQL de regresion:
  - `Supabase/tests/f1_t05_property_images_rls.sql`

## F1-T06 - Listado publico con busqueda, filtros basicos y mapa/lista (backend)

- Migracion SQL en `Supabase/migrations/20260316103000_f1_t06_public_listing_search_map.sql`
  - agrega campos en `properties`: `zone`, `available_from`, `lat`, `lng`, `location_precision`
  - agrega constraints de precision de ubicacion y rango de coordenadas
  - ajusta policies RLS de `properties` y `property_images`:
    - lectura publica (`anon`) solo para `status='published'`
    - lectura privada por dueno/admin se mantiene para usuarios autenticados
- Endpoints publicos:
  - `GET /api/v1/public/properties/suggestions?q=...` (maximo 5)
  - `GET /api/v1/public/properties?q=&cityPostal=&priceMax=&bedrooms=&areaMin=&availableFrom=&sort=&offset=&limit=`
- `sort` soportado:
  - `recent` (default), `price_asc`, `price_desc`
- Paginacion:
  - `offset` default `0`
  - `limit` default `20`, maximo `50`

## F1-T07 - Detalle publico estilo Redfin-like (backend)

- Endpoint publico de detalle:
  - `GET /api/v1/public/properties/{id}`
- Reglas:
  - solo devuelve propiedades `published`
  - `404` cuando el inmueble no existe o no esta publicado
  - incluye galeria ordenada, key facts, breakdown de costes y ubicacion (`exact`/`approximate`)
  - si no hay `lat/lng`, retorna `location: null` sin romper el contrato
- Test SQL de regresion:
  - `Supabase/tests/f1_t07_public_detail_access.sql`

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
