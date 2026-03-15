-- F1-T05: gestion de fotos de propiedad (owner/admin + unicidad de principal + orden valido)
-- Ejecutar con Supabase CLI:
--   supabase db push

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
  'property-images',
  'property-images',
  true,
  8388608,
  array['image/jpeg', 'image/png', 'image/webp']::text[]
)
on conflict (id) do update
set
  public = excluded.public,
  file_size_limit = excluded.file_size_limit,
  allowed_mime_types = excluded.allowed_mime_types;

alter table public.property_images
  drop constraint if exists property_images_sort_order_non_negative;

alter table public.property_images
  add constraint property_images_sort_order_non_negative check (sort_order >= 0);

create unique index if not exists property_images_primary_unique_idx
  on public.property_images (property_id)
  where is_primary = true;

drop policy if exists "property_images_insert_admin" on public.property_images;
drop policy if exists "property_images_insert_owner_or_admin" on public.property_images;
create policy "property_images_insert_owner_or_admin"
on public.property_images
for insert
to authenticated
with check (
  exists (
    select 1
    from public.properties as pr
    join public.profiles as p
      on p.user_id = auth.uid()
    where pr.id = property_id
      and (
        (p.role = 'propietario'::public.app_role and pr.created_by = auth.uid())
        or p.role = 'admin'::public.app_role
      )
  )
);

drop policy if exists "property_images_update_admin" on public.property_images;
drop policy if exists "property_images_update_owner_or_admin" on public.property_images;
create policy "property_images_update_owner_or_admin"
on public.property_images
for update
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
)
with check (
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

drop policy if exists "property_images_delete_admin" on public.property_images;
drop policy if exists "property_images_delete_owner_or_admin" on public.property_images;
create policy "property_images_delete_owner_or_admin"
on public.property_images
for delete
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
