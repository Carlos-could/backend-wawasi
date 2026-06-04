-- F2-T03 RLS regression (property_templates: aislamiento por autor)
-- Ejecutar manualmente en Supabase SQL Editor o psql:
--   begin;
--   \i Supabase/tests/f2_t03_templates_rls.sql
--   rollback;

do $$
declare
  v_owner_a uuid;
  v_owner_b uuid;
  v_template_id uuid;
  v_visible_count integer;
begin
  -- Dos propietarios distintos (o cualquier par de usuarios con perfil).
  select p.user_id into v_owner_a
  from public.profiles as p
  where p.role = 'propietario'::public.app_role
  limit 1;

  select p.user_id into v_owner_b
  from public.profiles as p
  where p.user_id <> v_owner_a
  limit 1;

  if v_owner_a is null or v_owner_b is null then
    raise exception 'F2-T03 RLS: se requieren al menos 2 perfiles distintos para validar el aislamiento.';
  end if;

  perform set_config('role', 'authenticated', true);
  perform set_config('request.jwt.claim.role', 'authenticated', true);

  -- Caso permitido: owner_a crea su propia plantilla.
  perform set_config('request.jwt.claim.sub', v_owner_a::text, true);
  insert into public.property_templates (created_by, name, data)
  values (v_owner_a, 'Plantilla A', '{"title":"Modelo A","city":"Berlin"}'::jsonb)
  returning id into v_template_id;

  if v_template_id is null then
    raise exception 'F2-T03 RLS: owner_a no pudo crear su propia plantilla.';
  end if;

  -- Caso denegado: owner_b no puede ver la plantilla de owner_a.
  perform set_config('request.jwt.claim.sub', v_owner_b::text, true);
  select count(*) into v_visible_count
  from public.property_templates
  where id = v_template_id;

  if v_visible_count <> 0 then
    raise exception 'F2-T03 RLS: owner_b no deberia poder ver la plantilla de owner_a.';
  end if;

  -- Caso denegado: owner_b no puede borrar la plantilla de owner_a.
  delete from public.property_templates where id = v_template_id;
  if not found then
    null; -- esperado: la fila no es visible/borrable para owner_b
  end if;

  perform set_config('request.jwt.claim.sub', v_owner_a::text, true);
  if not exists (select 1 from public.property_templates where id = v_template_id) then
    raise exception 'F2-T03 RLS: la plantilla de owner_a fue borrada por otro usuario.';
  end if;

  raise notice 'F2-T03 RLS: OK (aislamiento por autor verificado).';
end
$$;
