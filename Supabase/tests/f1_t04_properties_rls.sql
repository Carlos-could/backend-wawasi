-- F1-T04 RLS regression (propietario/admin en properties)
-- Ejecutar manualmente en Supabase SQL Editor o psql:
--   begin;
--   \i Supabase/tests/f1_t04_properties_rls.sql
--   rollback;

do $$
declare
  v_owner_id uuid;
  v_admin_id uuid;
  v_tenant_id uuid;
  v_property_id uuid;
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
    raise exception 'F1-T04 RLS: se requieren perfiles propietario/admin/inquilino para validar casos.';
  end if;

  perform set_config('role', 'authenticated', true);
  perform set_config('request.jwt.claim.role', 'authenticated', true);

  -- Caso permitido: propietario crea su propia propiedad.
  perform set_config('request.jwt.claim.sub', v_owner_id::text, true);
  insert into public.properties (
    created_by, title, property_type, operation_type, status, price, currency, city, country
  )
  values (
    v_owner_id, 'RLS Owner Property', 'apartment', 'rent', 'draft', 1200, 'EUR', 'Berlin', 'DE'
  )
  returning id into v_property_id;

  if v_property_id is null then
    raise exception 'F1-T04 RLS: propietario no pudo crear propiedad propia.';
  end if;

  -- Caso denegado: inquilino no puede editar propiedad ajena.
  perform set_config('request.jwt.claim.sub', v_tenant_id::text, true);
  begin
    update public.properties
    set title = 'Tenant should fail'
    where id = v_property_id;

    if found then
      raise exception 'F1-T04 RLS: inquilino no deberia poder editar propiedad ajena.';
    end if;
  exception
    when insufficient_privilege then
      null;
  end;

  -- Caso permitido: admin puede editar cualquier propiedad.
  perform set_config('request.jwt.claim.sub', v_admin_id::text, true);
  update public.properties
  set title = 'Admin Updated'
  where id = v_property_id;

  if not exists (
    select 1
    from public.properties
    where id = v_property_id
      and title = 'Admin Updated'
  ) then
    raise exception 'F1-T04 RLS: admin no pudo editar propiedad.';
  end if;
end
$$;
