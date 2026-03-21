-- F1-T10: Vista optimizada para pines de mapa
-- Objetivo: traer solo lo necesario para el mapa y su preview card.

create or replace view public.property_map_pins as
select 
  p.id,
  p.title,
  p.price,
  p.currency,
  p.lat,
  p.lng,
  p.city,
  p.zone,
  p.area_m2,
  p.bedrooms,
  p.property_type,
  p.operation_type,
  p.status,
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

-- RLS para la vista (Supabase aplica RLS de las tablas base, pero es bueno habilitarlo si queremos políticas específicas)
-- En Supabase, las vistas no tienen RLS propio por defecto, heredan de las tablas base.
-- Como properties y property_images tienen RLS, la vista ya está protegida.
