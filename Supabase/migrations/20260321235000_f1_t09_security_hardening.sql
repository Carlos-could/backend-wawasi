-- F1-T09: Seguridad final, RLS y Observabilidad
-- Refuerzo de políticas de acceso y creación de tabla de auditoría de errores.

-- 1. Reforzar RLS en public.properties
-- Limpiar políticas de selección previas
drop policy if exists "properties_select_authenticated" on public.properties;
drop policy if exists "properties_select_all" on public.properties;
drop policy if exists "properties_select_policy" on public.properties;

-- Nueva política: Los usuarios autenticados solo ven propiedades publicadas,
-- a menos que sean los creadores de la propiedad o administradores.
create policy "properties_select_policy"
on public.properties
for select
to authenticated
using (
  status = 'published'
  or created_by = auth.uid()
  or exists (
    select 1
    from public.profiles
    where user_id = auth.uid()
      and role = 'admin'::public.app_role
  )
);

-- Nueva política: Borrado restringido a dueño o admin
drop policy if exists "properties_delete_owner_or_admin" on public.properties;
create policy "properties_delete_owner_or_admin"
on public.properties
for delete
to authenticated
using (
  created_by = auth.uid()
  or exists (
    select 1
    from public.profiles
    where user_id = auth.uid()
      and role = 'admin'::public.app_role
  )
);

-- 2. Reforzar RLS en public.property_images
-- Sincronizar lectura de imágenes con la visibilidad de la propiedad
drop policy if exists "property_images_select_policy" on public.property_images;
create policy "property_images_select_policy"
on public.property_images
for select
to authenticated
using (
  exists (
    select 1
    from public.properties
    where id = property_images.property_id
      -- Aplicamos la misma lógica de visibilidad que en properties
      and (
        status = 'published'
        or created_by = auth.uid()
        or exists (
          select 1
          from public.profiles
          where user_id = auth.uid()
            and role = 'admin'::public.app_role
        )
      )
  )
);

-- 3. Tabla de Observabilidad: public.logs
-- Para registrar errores críticos del frontend/backend.
create table if not exists public.logs (
  id uuid primary key default gen_random_uuid(),
  created_at timestamptz not null default timezone('utc', now()),
  level text not null default 'error', -- info, warn, error, fatal
  message text not null,
  stack_trace text,
  user_id uuid references auth.users(id) on delete set null,
  context jsonb default '{}'::jsonb
);

-- Activar RLS en logs
alter table public.logs enable row level security;

-- Política: Permitir inserción "ciega" a usuarios autenticados (para reportar errores)
drop policy if exists "logs_insert_authenticated" on public.logs;
create policy "logs_insert_authenticated"
on public.logs
for insert
to authenticated
with check (auth.uid() = user_id or user_id is null);

-- Política: Solo admins pueden leer los logs
drop policy if exists "logs_select_admin" on public.logs;
create policy "logs_select_admin"
on public.logs
for select
to authenticated
using (
  exists (
    select 1
    from public.profiles
    where user_id = auth.uid()
      and role = 'admin'::public.app_role
  )
);

-- 4. Otros refuerzos: deny-all por defecto en tablas sin políticas (opcional pero recomendado)
-- Supabase ya deniega si RLS está activo y no hay políticas, pero es bueno ser explícito.
