-- F1-T03: completar modelo MVP propiedades
-- Ejecutar con Supabase CLI:
--   supabase db push

alter table public.properties
  add column if not exists kaltmiete numeric(12,2),
  add column if not exists nebenkosten numeric(12,2),
  add column if not exists warmmiete numeric(12,2),
  add column if not exists kaution numeric(12,2);

alter table public.properties
  drop constraint if exists properties_kaltmiete_non_negative,
  drop constraint if exists properties_nebenkosten_non_negative,
  drop constraint if exists properties_warmmiete_non_negative,
  drop constraint if exists properties_kaution_non_negative,
  drop constraint if exists properties_warmmiete_gte_kaltmiete;

alter table public.properties
  add constraint properties_kaltmiete_non_negative check (kaltmiete is null or kaltmiete >= 0),
  add constraint properties_nebenkosten_non_negative check (nebenkosten is null or nebenkosten >= 0),
  add constraint properties_warmmiete_non_negative check (warmmiete is null or warmmiete >= 0),
  add constraint properties_kaution_non_negative check (kaution is null or kaution >= 0),
  add constraint properties_warmmiete_gte_kaltmiete check (
    warmmiete is null or kaltmiete is null or warmmiete >= kaltmiete
  );

create index if not exists properties_kaltmiete_idx on public.properties (kaltmiete);
create index if not exists properties_warmmiete_idx on public.properties (warmmiete);
create index if not exists properties_postal_code_idx on public.properties (postal_code);

create table if not exists public.property_images (
  id uuid primary key default gen_random_uuid(),
  property_id uuid not null references public.properties(id) on delete cascade,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  storage_path text not null,
  public_url text,
  sort_order integer not null default 0,
  is_primary boolean not null default false
);

create index if not exists property_images_property_id_idx on public.property_images (property_id);
create index if not exists property_images_property_sort_idx on public.property_images (property_id, sort_order);

drop trigger if exists trg_set_property_images_updated_at on public.property_images;
create trigger trg_set_property_images_updated_at
before update on public.property_images
for each row
execute procedure public.set_properties_updated_at();

alter table public.property_images enable row level security;

drop policy if exists "property_images_select_authenticated" on public.property_images;
create policy "property_images_select_authenticated"
on public.property_images
for select
to authenticated
using (true);

drop policy if exists "property_images_insert_admin" on public.property_images;
create policy "property_images_insert_admin"
on public.property_images
for insert
to authenticated
with check (
  exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role = 'admin'::public.app_role
  )
);

drop policy if exists "property_images_update_admin" on public.property_images;
create policy "property_images_update_admin"
on public.property_images
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

drop policy if exists "property_images_delete_admin" on public.property_images;
create policy "property_images_delete_admin"
on public.property_images
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

create table if not exists public.property_contacts (
  id uuid primary key default gen_random_uuid(),
  property_id uuid not null references public.properties(id) on delete cascade,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now()),
  full_name text not null,
  email text not null,
  phone text,
  message text not null,
  status text not null default 'new' check (status in ('new', 'read'))
);

create index if not exists property_contacts_property_id_idx on public.property_contacts (property_id);
create index if not exists property_contacts_status_idx on public.property_contacts (status);
create index if not exists property_contacts_created_at_idx on public.property_contacts (created_at desc);

drop trigger if exists trg_set_property_contacts_updated_at on public.property_contacts;
create trigger trg_set_property_contacts_updated_at
before update on public.property_contacts
for each row
execute procedure public.set_properties_updated_at();

alter table public.property_contacts enable row level security;

drop policy if exists "property_contacts_select_admin" on public.property_contacts;
create policy "property_contacts_select_admin"
on public.property_contacts
for select
to authenticated
using (
  exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role = 'admin'::public.app_role
  )
);

drop policy if exists "property_contacts_insert_authenticated" on public.property_contacts;
create policy "property_contacts_insert_authenticated"
on public.property_contacts
for insert
to authenticated
with check (true);

drop policy if exists "property_contacts_update_admin" on public.property_contacts;
create policy "property_contacts_update_admin"
on public.property_contacts
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

drop policy if exists "property_contacts_delete_admin" on public.property_contacts;
create policy "property_contacts_delete_admin"
on public.property_contacts
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
