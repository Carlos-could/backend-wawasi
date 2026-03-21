-- F1-T08: Test de regresión para RLS en property_contacts
-- Ejecutar manualmente en Supabase SQL Editor o psql:
--   begin;
--   \i Supabase/tests/f1_t08_contact_rls.sql
--   rollback;

do $$
declare
  v_owner_id uuid;
  v_other_user_id uuid;
  v_property_id uuid;
  v_contact_id uuid;
begin
  -- 1. Setup: Obtener perfiles para pruebas
  select p.user_id into v_owner_id
  from public.profiles as p
  where p.role = 'propietario'::public.app_role
  limit 1;

  select p.user_id into v_other_user_id
  from public.profiles as p
  where p.user_id != v_owner_id
  limit 1;

  if v_owner_id is null or v_other_user_id is null then
    raise exception 'F1-T08 Test: se requieren al menos dos perfiles para validar RLS.';
  end if;

  -- 2. Crear una propiedad para el dueño
  perform set_config('role', 'authenticated', true);
  perform set_config('request.jwt.claim.sub', v_owner_id::text, true);
  perform set_config('request.jwt.claim.role', 'authenticated', true);

  insert into public.properties (
    created_by, title, property_type, operation_type, status, price, currency, city, country
  )
  values (
    v_owner_id, 'Test Property F1-T08', 'apartment', 'rent', 'published', 1000, 'EUR', 'Madrid', 'ES'
  )
  returning id into v_property_id;

  -- 3. Caso: Envío anónimo (anon)
  perform set_config('role', 'anon', true);
  perform set_config('request.jwt.claim.sub', null, true);
  perform set_config('request.jwt.claim.role', 'anon', true);

  -- No usamos RETURNING id aquí porque anon NO tiene permiso de SELECT sobre la tabla.
  insert into public.property_contacts (
    property_id, full_name, email, message
  )
  values (
    v_property_id, 'Visitante Anonimo', 'anon@test.com', 'Hola, me interesa.'
  );

  -- 4. Caso: Propietario lee sus mensajes
  perform set_config('role', 'authenticated', true);
  perform set_config('request.jwt.claim.sub', v_owner_id::text, true);
  perform set_config('request.jwt.claim.role', 'authenticated', true);

  if not exists (
    select 1 
    from public.property_contacts 
    where property_id = v_property_id 
      and email = 'anon@test.com'
  ) then
    raise exception 'F1-T08 Test: el dueño no pudo leer el mensaje que recibió.';
  end if;

  -- 5. Caso: Otro usuario NO lee mensajes ajenos
  perform set_config('request.jwt.claim.sub', v_other_user_id::text, true);

  if exists (
    select 1 
    from public.property_contacts 
    where property_id = v_property_id 
      and email = 'anon@test.com'
  ) then
    raise exception 'F1-T08 Test: un usuario NO dueño pudo leer el mensaje dirigido a otro.';
  end if;

  -- 6. Caso: Propietario actualiza estado (marcar como leído)
  perform set_config('request.jwt.claim.sub', v_owner_id::text, true);
  update public.property_contacts
  set status = 'read'
  where property_id = v_property_id 
    and email = 'anon@test.com';

  if not exists (
    select 1 
    from public.property_contacts 
    where property_id = v_property_id 
      and email = 'anon@test.com'
      and status = 'read'
  ) then
    raise exception 'F1-T08 Test: el dueño no pudo marcar el mensaje como leido.';
  end if;

  raise notice 'F1-T08 Test: RLS para contactos validado correctamente.';
end
$$;
