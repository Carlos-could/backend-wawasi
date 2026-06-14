-- F2-T12: amenidades en properties + exposición en vista de pines
-- Origen: derivado de F2-T06 (filtros que requerían cambio de esquema).
-- Añade columna amenities (catálogo cerrado, slugs) con índice GIN para el filtro
-- multi-select de búsqueda (.contains -> @>). Recrea property_map_pins para que el
-- filtro de amenidades también aplique al mapa.

alter table public.properties
  add column if not exists amenities text[] not null default '{}';

create index if not exists idx_properties_amenities
  on public.properties using gin (amenities);

-- Recrear la vista de pines para exponer amenities al filtro del mapa.
-- security_invoker = true: la vista respeta las RLS del usuario que consulta,
-- no las del creador (evita el lint security_definer_view).
drop view if exists public.property_map_pins;
create view public.property_map_pins
with (security_invoker = true) as
select
  p.id,
  p.title,
  p.price,
  p.currency,
  p.lat,
  p.lng,
  p.city,
  p.zone,
  p.postal_code,
  p.area_m2,
  p.bedrooms,
  p.property_type,
  p.operation_type,
  p.status,
  p.available_from,
  p.amenities,          -- nuevo: permite filtrar pines por amenidad
  p.created_at,
  (
    select pi.storage_path
    from public.property_images pi
    where pi.property_id = p.id
    order by pi.is_primary desc, pi.sort_order asc
    limit 1
  ) as thumbnail_path
from public.properties p
where p.status = 'published';
