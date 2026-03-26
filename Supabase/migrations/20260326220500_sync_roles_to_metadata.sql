-- Función para sincronizar el rol a auth.users metadata
-- Esta función se ejecuta con "security definer" para poder modificar el esquema auth.
create or replace function public.sync_role_to_auth_metadata()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  update auth.users
  set raw_app_meta_data = coalesce(raw_app_meta_data, '{}'::jsonb) || jsonb_build_object('role', new.role)
  where id = new.user_id;
  return new;
end;
$$;

-- Trigger para actualizaciones y creaciones en la tabla profiles
drop trigger if exists trg_sync_role_to_auth_metadata on public.profiles;
create trigger trg_sync_role_to_auth_metadata
after insert or update of role on public.profiles
for each row
execute procedure public.sync_role_to_auth_metadata();

-- Sincronización inicial para el usuario actual (opcional, pero útil para aplicar el cambio de inmediato)
-- Nota: Esto aplica el rol actual de profiles a auth de todos los usuarios.
update auth.users
set raw_app_meta_data = coalesce(raw_app_meta_data, '{}'::jsonb) || jsonb_build_object('role', p.role)
from public.profiles p
where auth.users.id = p.user_id;
