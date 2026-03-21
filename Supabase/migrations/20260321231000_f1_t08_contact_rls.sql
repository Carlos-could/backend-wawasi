-- F1-T08: políticas RLS para contactos/mensajes
-- Objetivo: permitir envío anónimo y lectura restringida al dueño de la propiedad.

-- 1. Asegurar que la tabla existe (ya debería existir por F1-T03)
-- public.property_contacts

-- 2. Limpiar políticas previas si las hay (evitar conflictos)
drop policy if exists "property_contacts_select_admin" on public.property_contacts;
drop policy if exists "property_contacts_insert_authenticated" on public.property_contacts;
drop policy if exists "property_contacts_update_admin" on public.property_contacts;
drop policy if exists "property_contacts_delete_admin" on public.property_contacts;
drop policy if exists "property_contacts_insert_all" on public.property_contacts;
drop policy if exists "property_contacts_select_owner" on public.property_contacts;
drop policy if exists "property_contacts_update_owner" on public.property_contacts;

-- 3. Política: Inserción pública (anónimos y autenticados)
-- Permite que cualquier visitante envíe un mensaje de contacto.
create policy "property_contacts_insert_all"
on public.property_contacts
for insert
to anon, authenticated
with check (true);

-- 4. Política: Lectura restringida al dueño
-- Solo el usuario que creó la propiedad puede ver los contactos asociados.
create policy "property_contacts_select_owner"
on public.property_contacts
for select
to authenticated
using (
  exists (
    select 1
    from public.properties as p
    where p.id = property_contacts.property_id
      and p.created_by = auth.uid()
  )
);

-- 5. Política: Actualización del estado (marcar como leído)
-- Solo el dueño puede cambiar el estado de los mensajes que recibe.
create policy "property_contacts_update_owner"
on public.property_contacts
for update
to authenticated
using (
  exists (
    select 1
    from public.properties as p
    where p.id = property_contacts.property_id
      and p.created_by = auth.uid()
  )
)
with check (
  exists (
    select 1
    from public.properties as p
    where p.id = property_contacts.property_id
      and p.created_by = auth.uid()
  )
);

-- 6. Política: Admins mantienen acceso total (select)
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
