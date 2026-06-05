-- KI-014: fijar search_path en funciones con search_path mutable (lint 0011).
-- set_properties_updated_at / set_profiles_updated_at (triggers) y
-- reorder_property_photos (RPC). Son SECURITY INVOKER, así que el riesgo es bajo,
-- pero fijar el search_path evita secuestro vía objetos en schemas manipulables.

alter function public.set_properties_updated_at() set search_path = public, pg_temp;
alter function public.set_profiles_updated_at() set search_path = public, pg_temp;
alter function public.reorder_property_photos(uuid, uuid[]) set search_path = public, pg_temp;
