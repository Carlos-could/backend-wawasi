-- F1-T07 public detail regression (lectura publica de published por id)
-- Ejecutar manualmente en Supabase SQL Editor o psql:
--   begin;
--   \i Supabase/tests/f1_t07_public_detail_access.sql
--   rollback;

do $$
declare
  v_owner_id uuid;
  v_published_id uuid;
  v_draft_id uuid;
  v_visible_count int;
  v_hidden_count int;
begin
  select p.user_id into v_owner_id
  from public.profiles as p
  where p.role = 'propietario'::public.app_role
  limit 1;

  if v_owner_id is null then
    raise exception 'F1-T07 public detail: se requiere un perfil propietario.';
  end if;

  perform set_config('role', 'authenticated', true);
  perform set_config('request.jwt.claim.role', 'authenticated', true);
  perform set_config('request.jwt.claim.sub', v_owner_id::text, true);

  insert into public.properties (
    created_by, title, property_type, operation_type, status, price, currency, city, country, lat, lng, location_precision
  )
  values (
    v_owner_id, 'F1-T07 Published Detail', 'apartment', 'rent', 'published', 1350, 'EUR', 'Berlin', 'DE', 52.52, 13.405, 'approximate'
  )
  returning id into v_published_id;

  insert into public.properties (
    created_by, title, property_type, operation_type, status, price, currency, city, country
  )
  values (
    v_owner_id, 'F1-T07 Draft Detail', 'apartment', 'rent', 'draft', 980, 'EUR', 'Berlin', 'DE'
  )
  returning id into v_draft_id;

  insert into public.property_images (property_id, storage_path, public_url, sort_order, is_primary)
  values (v_published_id, 'properties/f1-t07/published-primary.jpg', 'https://example.com/f1-t07-primary.jpg', 0, true);

  perform set_config('role', 'anon', true);
  perform set_config('request.jwt.claim.role', 'anon', true);
  perform set_config('request.jwt.claim.sub', '', true);

  select count(*)
  into v_visible_count
  from public.properties as pr
  where pr.id = v_published_id
    and pr.status = 'published';

  if v_visible_count <> 1 then
    raise exception 'F1-T07 public detail: anon no puede leer propiedad published.';
  end if;

  select count(*)
  into v_hidden_count
  from public.properties as pr
  where pr.id = v_draft_id;

  if v_hidden_count <> 0 then
    raise exception 'F1-T07 public detail: anon no deberia leer propiedad draft.';
  end if;

  if not exists (
    select 1
    from public.properties as pr
    where pr.id = v_published_id
      and pr.location_precision in ('approximate', 'exact')
  ) then
    raise exception 'F1-T07 public detail: falta location_precision valida en published.';
  end if;
end
$$;
