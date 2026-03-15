-- F1-T03: modelo de datos MVP propiedades
-- Ejecutar con Supabase CLI:
--   supabase db push

create table if not exists public.properties (
  id uuid primary key default gen_random_uuid(),
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  created_by uuid not null references auth.users(id) on delete restrict default auth.uid(),
  title text not null,
  description text,
  property_type text not null check (property_type in ('apartment', 'house', 'studio', 'land', 'commercial', 'other')),
  operation_type text not null check (operation_type in ('rent', 'sale')),
  status text not null default 'draft' check (status in ('draft', 'published', 'archived')),
  price numeric(12,2) not null check (price >= 0),
  currency char(3) not null default 'USD',
  bedrooms smallint check (bedrooms is null or bedrooms >= 0),
  bathrooms smallint check (bathrooms is null or bathrooms >= 0),
  area_m2 numeric(10,2) check (area_m2 is null or area_m2 > 0),
  address_line text,
  district text,
  city text not null,
  country text not null,
  postal_code text,
  image_urls text[] not null default '{}',
  is_active boolean not null default true
);

create index if not exists properties_status_idx on public.properties (status);
create index if not exists properties_city_idx on public.properties (city);
create index if not exists properties_operation_type_idx on public.properties (operation_type);
create index if not exists properties_price_idx on public.properties (price);
create index if not exists properties_created_by_idx on public.properties (created_by);
create index if not exists properties_created_at_idx on public.properties (created_at desc);

create or replace function public.set_properties_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = timezone('utc', now());
  return new;
end;
$$;

drop trigger if exists trg_set_properties_updated_at on public.properties;
create trigger trg_set_properties_updated_at
before update on public.properties
for each row
execute procedure public.set_properties_updated_at();

alter table public.properties enable row level security;

drop policy if exists "properties_select_authenticated" on public.properties;
create policy "properties_select_authenticated"
on public.properties
for select
to authenticated
using (true);

drop policy if exists "properties_insert_admin" on public.properties;
create policy "properties_insert_admin"
on public.properties
for insert
to authenticated
with check (
  created_by = auth.uid()
  and exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role = 'admin'::public.app_role
  )
);

drop policy if exists "properties_update_admin" on public.properties;
create policy "properties_update_admin"
on public.properties
for update
to authenticated
using (
  exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role = 'admin'::public.app_role
  )
)
with check (
  exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role = 'admin'::public.app_role
  )
);

drop policy if exists "properties_delete_admin" on public.properties;
create policy "properties_delete_admin"
on public.properties
for delete
to authenticated
using (
  exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role = 'admin'::public.app_role
  )
);
