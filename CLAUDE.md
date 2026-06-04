# CLAUDE.md — backend-wawasi

Contexto global del workspace: `d:\wawa\CLAUDE.md`

## Este repositorio

.NET 9 ASP.NET Core Minimal API. **Uso reservado** — solo para lógica que Supabase no puede manejar: tareas asíncronas/cron, procesamiento complejo de imágenes, pagos, integraciones de terceros, lógica de negocio pesada.

**Todo CRUD estándar va directo desde Next.js a Supabase. No crear endpoints en .NET para eso.**

## Estructura

```
backend-wawasi/
├── Program.cs                          → Entry point, middleware, DI
├── Auth/
│   ├── AppRole.cs                      → Definición de roles
│   └── AuthModels.cs                   → DTOs de auth
├── Configuration/
│   └── SupabaseOptions.cs              → Config tipada de Supabase
├── Services/
│   └── SupabaseAuthService.cs          → Validación JWT (JWKS + HS256 legacy)
└── Supabase/
    ├── migrations/                     → 16 migraciones SQL numeradas
    └── tests/                          → 5 archivos de test RLS en SQL
```

## Auth y JWT

- Validación híbrida: JWKS asimétrico (ES256) como principal + HS256 legacy mientras no se revoque.
- Discovery OIDC: `{SupabaseUrl}/auth/v1/.well-known/openid-configuration` — NO apuntar directo al `jwks.json` (deja SigningKeys vacío, ver KI-012).
- Roles leídos de `raw_app_meta_data` del JWT.

## Secretos de desarrollo

Usar `dotnet user-secrets`. Nunca `.env` versionado (ver KI-011 — fuga previa).

```bash
dotnet user-secrets init
dotnet user-secrets set "Supabase:Url" "..."
dotnet user-secrets set "Supabase:AnonKey" "..."
dotnet user-secrets set "Supabase:ServiceRoleKey" "..."
```

`ServiceRoleKey` solo para operaciones que requieren saltarse RLS (ej. admin RPCs).

## Reglas específicas de backend

- No retornar modelos de DB directamente — siempre DTOs serializables.
- JSON global configurado en `Program.cs`: camelCase, sin ciclos, null handling.
- CORS explícito por entorno (origen frontend + `Authorization` + preflight OPTIONS).
- No arquitectura por capas hasta que el volumen de endpoints lo justifique.
- Errores: `ProblemDetails` o esquema equivalente. No exponer detalles internos en producción.

## Comandos útiles

```bash
dotnet build    # compilar
dotnet run      # arrancar (puerto default: 5000/5001)
```

## Issues conocidos relevantes para BE

- **KI-009:** `.env` no se carga automáticamente — usar `dotnet user-secrets`.
- **KI-010:** CORS preflight 405 — `AddCors` + `UseCors` con origen FE explícito.
- **KI-011:** `.env` con secretos fue commiteado y publicado — claves legacy pendientes de rotar en Supabase.
- **KI-012:** JWT validación JWKS — usar discovery OIDC, no URL directa del jwks.json.
- **KI-003:** Upsert con Guid por defecto `00000000...` — asignar `Guid.NewGuid()` antes de `.Upsert()`.
