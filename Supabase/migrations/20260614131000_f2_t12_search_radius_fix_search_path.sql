-- F2-T12 (parte 2) fix: fijar search_path en properties_within_radius.
-- Sin search_path explícito la función dispara el lint function_search_path_mutable.
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
