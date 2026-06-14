-- F2-T12 fix: forzar security_invoker en la vista property_map_pins.
-- La vista (preexistente desde F1-T10) se creaba como SECURITY DEFINER, lo que
-- dispara el lint security_definer_view de Supabase. Al recrearla con
-- security_invoker = true respeta las RLS del usuario que consulta.
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
  p.amenities,
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
