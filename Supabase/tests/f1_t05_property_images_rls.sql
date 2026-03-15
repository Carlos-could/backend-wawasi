-- F1-T05 RLS regression (propietario/admin en property_images)
-- Ejecutar manualmente en Supabase SQL Editor o psql:
--   begin;
--   \i Supabase/tests/f1_t05_property_images_rls.sql
--   rollback;

do $$
declare
  v_owner_id uuid;
  v_admin_id uuid;
  v_tenant_id uuid;
  v_property_id uuid;
  v_photo_id uuid;
begin
  select p.user_id into v_owner_id
  from public.profiles as p
  where p.role = 'propietario'::public.app_role
  limit 1;

  select p.user_id into v_admin_id
  from public.profiles as p
  where p.role = 'admin'::public.app_role
  limit 1;

  select p.user_id into v_tenant_id
  from public.profiles as p
  where p.role = 'inquilino'::public.app_role
  limit 1;

  if v_owner_id is null or v_admin_id is null or v_tenant_id is null then
    raise exception 'F1-T05 RLS: se requieren perfiles propietario/admin/inquilino para validar casos.';
  end if;

  perform set_config('role', 'authenticated', true);
  perform set_config('request.jwt.claim.role', 'authenticated', true);

  perform set_config('request.jwt.claim.sub', v_owner_id::text, true);
  insert into public.properties (
    created_by, title, property_type, operation_type, status, price, currency, city, country
  )
  values (
    v_owner_id, 'RLS Photos Property', 'apartment', 'rent', 'draft', 1200, 'EUR', 'Berlin', 'DE'
  )
  returning id into v_property_id;

  insert into public.property_images (
    property_id, storage_path, public_url, sort_order, is_primary
  )
  values (
    v_property_id,
    'properties/rls/image-1.jpg',
    'https://example.com/rls-image-1.jpg',
    0,
    true
  )
  returning id into v_photo_id;

  if v_photo_id is null then
    raise exception 'F1-T05 RLS: propietario no pudo crear property_image propia.';
  end if;

  perform set_config('request.jwt.claim.sub', v_tenant_id::text, true);
  begin
    update public.property_images
    set sort_order = 10
    where id = v_photo_id;

    if found then
      raise exception 'F1-T05 RLS: inquilino no deberia poder editar property_image ajena.';
    end if;
  exception
    when insufficient_privilege then
      null;
  end;

  perform set_config('request.jwt.claim.sub', v_admin_id::text, true);
  update public.property_images
  set sort_order = 1, is_primary = true
  where id = v_photo_id;

  if not exists (
    select 1
    from public.property_images
    where id = v_photo_id
      and sort_order = 1
      and is_primary = true
  ) then
    raise exception 'F1-T05 RLS: admin no pudo editar property_image.';
  end if;

  perform set_config('request.jwt.claim.sub', v_owner_id::text, true);
  delete from public.property_images where id = v_photo_id;
  if exists (select 1 from public.property_images where id = v_photo_id) then
    raise exception 'F1-T05 RLS: propietario no pudo eliminar property_image propia.';
  end if;
end
$$;
