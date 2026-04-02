-- F1-T12: Función RPC para reordenar fotos de forma masiva en una sola petición.

begin;

create or replace function public.reorder_property_photos(
  p_property_id uuid,
  p_photo_ids uuid[]
) returns void
language plpgsql
security invoker
as $$
declare
  i integer;
begin
  for i in 1..array_length(p_photo_ids, 1) loop
    update public.property_images
    set sort_order = i - 1
    where id = p_photo_ids[i]
      and property_id = p_property_id;
  end loop;
end;
$$;

commit;
