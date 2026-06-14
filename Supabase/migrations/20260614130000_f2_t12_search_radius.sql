-- F2-T12 (parte 2): radio de búsqueda geográfico.
-- Filtra propiedades dentro de X km de una coordenada de referencia usando
-- earthdistance (ll_to_earth) sobre las columnas lat/lng existentes. Sin columna nueva.

-- Habilitar earthdistance (depende de cube). En schema extensions para no
-- engordar el lint extension_in_public.
create extension if not exists cube with schema extensions;
create extension if not exists earthdistance with schema extensions;

-- Índice GiST para acelerar el filtro de distancia (bounding box vía earth_box).
create index if not exists idx_properties_earth
  on public.properties
  using gist (extensions.ll_to_earth(lat, lng))
  where status = 'published' and lat is not null and lng is not null;

-- IDs de propiedades publicadas dentro de radius_km de (center_lat, center_lng).
-- security invoker -> respeta RLS del usuario que consulta (solo published es público).
create or replace function public.properties_within_radius(
  center_lat double precision,
  center_lng double precision,
  radius_km double precision
) returns table (id uuid)
language sql
security invoker
stable
set search_path = public, extensions
as $$
  select p.id
  from public.properties p
  where p.status = 'published'
    and p.lat is not null and p.lng is not null
    and extensions.earth_box(extensions.ll_to_earth(center_lat, center_lng), radius_km * 1000)
        @> extensions.ll_to_earth(p.lat, p.lng)
    and extensions.earth_distance(
          extensions.ll_to_earth(center_lat, center_lng),
          extensions.ll_to_earth(p.lat, p.lng)
        ) <= radius_km * 1000;
$$;
