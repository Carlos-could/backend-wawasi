-- F1-T11: Habilitar Supabase Realtime para property_contacts
-- Agregamos la tabla a la publicación para que supabase.channel reciba eventos

begin;
  -- Por defecto, Supabase tiene una publicación llamada 'supabase_realtime'
  alter publication supabase_realtime add table public.property_contacts;
commit;
