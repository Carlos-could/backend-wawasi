-- F1-T03 CRUD smoke (integracion SQL)
-- Ejecutar manualmente en Supabase SQL Editor o psql:
--   begin;
--   \i Supabase/tests/f1_t03_crud_smoke.sql
--   rollback;

do $$
declare
  v_user_id uuid;
  v_property_id uuid;
  v_image_id uuid;
  v_contact_id uuid;
begin
  select u.id
  into v_user_id
  from auth.users as u
  order by u.created_at asc
  limit 1;

  if v_user_id is null then
    raise exception 'F1-T03 smoke: no existe ningun usuario en auth.users';
  end if;

  insert into public.properties (
    created_by,
    title,
    description,
    property_type,
    operation_type,
    status,
    price,
    currency,
    city,
    country,
    kaltmiete,
    nebenkosten,
    warmmiete,
    kaution
  )
  values (
    v_user_id,
    'Smoke Test Property',
    'Registro temporal para validar CRUD F1-T03',
    'apartment',
    'rent',
    'draft',
    950.00,
    'EUR',
    'Berlin',
    'DE',
    800.00,
    150.00,
    950.00,
    1600.00
  )
  returning id into v_property_id;

  if v_property_id is null then
    raise exception 'F1-T03 smoke: no se pudo crear property';
  end if;

  insert into public.property_images (
    property_id,
    storage_path,
    public_url,
    sort_order,
    is_primary
  )
  values (
    v_property_id,
    'properties/smoke/image-1.jpg',
    'https://example.com/image-1.jpg',
    0,
    true
  )
  returning id into v_image_id;

  if v_image_id is null then
    raise exception 'F1-T03 smoke: no se pudo crear property_image';
  end if;

  insert into public.property_contacts (
    property_id,
    full_name,
    email,
    phone,
    message,
    status
  )
  values (
    v_property_id,
    'Smoke Contact',
    'smoke@example.com',
    '+49 000 000 000',
    'Mensaje de prueba para validar F1-T03',
    'new'
  )
  returning id into v_contact_id;

  if v_contact_id is null then
    raise exception 'F1-T03 smoke: no se pudo crear property_contact';
  end if;

  update public.properties
  set status = 'published'
  where id = v_property_id;

  if not exists (select 1 from public.properties where id = v_property_id and status = 'published') then
    raise exception 'F1-T03 smoke: no se pudo actualizar property';
  end if;

  delete from public.properties where id = v_property_id;

  if exists (select 1 from public.properties where id = v_property_id) then
    raise exception 'F1-T03 smoke: no se pudo eliminar property';
  end if;

  if exists (select 1 from public.property_images where id = v_image_id) then
    raise exception 'F1-T03 smoke: cascade delete no elimino property_image';
  end if;

  if exists (select 1 from public.property_contacts where id = v_contact_id) then
    raise exception 'F1-T03 smoke: cascade delete no elimino property_contact';
  end if;
end
$$;
