-- TICKET-01: Políticas RLS para Supabase Storage (property-images)
-- Sólo los usuarios autenticados pueden subir fotos.
-- Cualquier persona puede ver fotos (lectura pública).

-- Asegurarse de que RLS está activo en objects (suele venir activo por defecto en Supabase)
-- alter table storage.objects enable row level security;

-- Política de lectura pública para el bucket property-images
drop policy if exists "property_images_read_public" on storage.objects;
create policy "property_images_read_public"
on storage.objects
for select
to public
using (bucket_id = 'property-images');

-- Política de subida (insert) para usuarios autenticados en property-images
drop policy if exists "property_images_insert_authenticated" on storage.objects;
create policy "property_images_insert_authenticated"
on storage.objects
for insert
to authenticated
with check (
  bucket_id = 'property-images'
);

-- Política de actualización/eliminación igual que la subida
drop policy if exists "property_images_update_authenticated" on storage.objects;
create policy "property_images_update_authenticated"
on storage.objects
for update
to authenticated
using (
  bucket_id = 'property-images'
);

drop policy if exists "property_images_delete_authenticated" on storage.objects;
create policy "property_images_delete_authenticated"
on storage.objects
for delete
to authenticated
using (
  bucket_id = 'property-images'
);
