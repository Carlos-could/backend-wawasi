-- F1-T10: Corrección de vista optimizada para pines de mapa
-- Objetivo: Añadir postal_code y available_from para permitir filtrado en el frontend.

drop view if exists public.property_map_pins;
create view public.property_map_pins as
select 
  p.id,
  p.title,
  p.price,
  p.currency,
  p.lat,
  p.lng,
  p.city,
  p.zone,
  p.postal_code,      -- Añadido para filtro de búsqueda
  p.area_m2,
  p.bedrooms,
  p.property_type,
  p.operation_type,
  p.status,
  p.available_from,   -- Añadido para filtro de disponibilidad
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
