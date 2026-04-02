-- Habilitar extensión pg_trgm para búsquedas ILIKE eficientes
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Crear índices GIN en la tabla properties para acelerar las búsquedas por texto
CREATE INDEX IF NOT EXISTS properties_city_trgm_idx ON public.properties USING gin (city gin_trgm_ops);
CREATE INDEX IF NOT EXISTS properties_zone_trgm_idx ON public.properties USING gin (zone gin_trgm_ops);
CREATE INDEX IF NOT EXISTS properties_postal_code_trgm_idx ON public.properties USING gin (postal_code gin_trgm_ops);
