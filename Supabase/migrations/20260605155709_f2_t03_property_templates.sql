-- F2-T03: plantillas de anuncio (property_templates)
-- Cada propietario guarda modelos de formulario reutilizables. Las plantillas
-- son privadas: solo su autor puede verlas/gestionarlas (RLS created_by = auth.uid()).
-- Ejecutar con Supabase CLI:
--   supabase db push

create table if not exists public.property_templates (
  id uuid primary key default gen_random_uuid(),
  created_by uuid not null references auth.users(id) on delete cascade,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  name text not null,
  data jsonb not null default '{}'::jsonb,
  constraint property_templates_name_not_blank check (length(trim(name)) > 0)
);

create index if not exists property_templates_created_by_idx
  on public.property_templates (created_by);
create index if not exists property_templates_created_by_created_at_idx
  on public.property_templates (created_by, created_at desc);

drop trigger if exists trg_set_property_templates_updated_at on public.property_templates;
create trigger trg_set_property_templates_updated_at
before update on public.property_templates
for each row
execute procedure public.set_properties_updated_at();

alter table public.property_templates enable row level security;

-- Solo el autor puede leer sus plantillas.
drop policy if exists "property_templates_select_owner" on public.property_templates;
create policy "property_templates_select_owner"
on public.property_templates
for select
to authenticated
using (created_by = auth.uid());

-- Solo el autor puede crear plantillas a su nombre.
drop policy if exists "property_templates_insert_owner" on public.property_templates;
create policy "property_templates_insert_owner"
on public.property_templates
for insert
to authenticated
with check (created_by = auth.uid());

-- Solo el autor puede actualizar sus plantillas.
drop policy if exists "property_templates_update_owner" on public.property_templates;
create policy "property_templates_update_owner"
on public.property_templates
for update
to authenticated
using (created_by = auth.uid())
with check (created_by = auth.uid());

-- Solo el autor puede borrar sus plantillas.
drop policy if exists "property_templates_delete_owner" on public.property_templates;
create policy "property_templates_delete_owner"
on public.property_templates
for delete
to authenticated
using (created_by = auth.uid());
