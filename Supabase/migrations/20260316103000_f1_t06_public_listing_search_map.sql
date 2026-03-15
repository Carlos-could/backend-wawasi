-- F1-T06: listado publico con busqueda/filtros/mapa
-- Ejecutar con Supabase CLI:
--   supabase db push

alter table public.properties
  add column if not exists zone text,
  add column if not exists available_from date,
  add column if not exists lat numeric(9,6),
  add column if not exists lng numeric(9,6),
  add column if not exists location_precision text not null default 'approximate';

alter table public.properties
  drop constraint if exists properties_location_precision_check,
  drop constraint if exists properties_lat_range_check,
  drop constraint if exists properties_lng_range_check;

alter table public.properties
  add constraint properties_location_precision_check
    check (location_precision in ('approximate', 'exact')),
  add constraint properties_lat_range_check
    check (lat is null or (lat >= -90 and lat <= 90)),
  add constraint properties_lng_range_check
    check (lng is null or (lng >= -180 and lng <= 180));

create index if not exists properties_zone_idx on public.properties (zone);
create index if not exists properties_available_from_idx on public.properties (available_from);
create index if not exists properties_lat_idx on public.properties (lat);
create index if not exists properties_lng_idx on public.properties (lng);

drop policy if exists "properties_select_authenticated" on public.properties;
drop policy if exists "properties_select_published_public" on public.properties;
create policy "properties_select_published_public"
on public.properties
for select
to anon, authenticated
using (status = 'published');

drop policy if exists "properties_select_owner_or_admin" on public.properties;
create policy "properties_select_owner_or_admin"
on public.properties
for select
to authenticated
using (
  exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and (
        (p.role = 'propietario'::public.app_role and properties.created_by = auth.uid())
        or p.role = 'admin'::public.app_role
      )
  )
);

drop policy if exists "property_images_select_authenticated" on public.property_images;
drop policy if exists "property_images_select_published_public" on public.property_images;
create policy "property_images_select_published_public"
on public.property_images
for select
to anon, authenticated
using (
  exists (
    select 1
    from public.properties as pr
    where pr.id = property_images.property_id
      and pr.status = 'published'
  )
);

drop policy if exists "property_images_select_owner_or_admin" on public.property_images;
create policy "property_images_select_owner_or_admin"
on public.property_images
for select
to authenticated
using (
  exists (
    select 1
    from public.properties as pr
    join public.profiles as p
      on p.user_id = auth.uid()
    where pr.id = property_images.property_id
      and (
        (p.role = 'propietario'::public.app_role and pr.created_by = auth.uid())
        or p.role = 'admin'::public.app_role
      )
  )
);
