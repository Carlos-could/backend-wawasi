-- F1-T12: Funciones administrativas para gestión de usuarios y roles
-- Estas funciones permiten al admin interactuar con auth.users indirectamente.

-- Eliminar funciones previas para evitar errores de tipo de retorno corregido
drop function if exists public.get_users_with_roles();
drop function if exists public.admin_update_user_role(uuid, public.app_role);

-- 1. Función para obtener lista de usuarios con email y rol (simplificada a TEXT)
create or replace function public.get_users_with_roles()
returns table (
  id uuid,
  email text,
  role text,
  created_at timestamptz
)
language plpgsql
security definer
set search_path = public
as $$
begin
  return query
  select 
    au.id,
    cast(au.email as text),
    cast(p.role as text),
    p.created_at
  from auth.users au
  inner join public.profiles p on au.id = p.user_id
  order by p.created_at desc;
end;
$$;

-- 2. Función para actualizar el rol de un usuario
create or replace function public.admin_update_user_role(
  target_user_id uuid,
  new_role public.app_role
)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
  -- Verificar que el usuario que llama sea admin
  if not exists (
    select 1 from public.profiles
    where user_id = auth.uid()
      and role = 'admin'::public.app_role
  ) then
    raise exception 'Acceso denegado: Se requiere rol de administrador';
  end if;

  -- Actualizar en la tabla profiles (esto activará el trigger de sincronización con auth metadata)
  update public.profiles
  set role = new_role,
      updated_at = timezone('utc', now())
  where user_id = target_user_id;

  if not found then
    raise exception 'Usuario no encontrado';
  end if;
end;
$$;

-- Otorgar permisos de ejecución a usuarios autenticados
grant execute on function public.get_users_with_roles() to authenticated;
grant execute on function public.admin_update_user_role(uuid, public.app_role) to authenticated;
