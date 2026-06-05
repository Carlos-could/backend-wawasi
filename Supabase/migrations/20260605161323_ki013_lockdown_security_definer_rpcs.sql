-- KI-013: bloquear ejecución de RPC SECURITY DEFINER por anon/public
-- y añadir guard de admin a get_users_with_roles (fuga de PII: emails + roles).
--
-- Contexto: las funciones admin/trigger heredaron EXECUTE a PUBLIC (anon, authenticated)
-- al crearse. SECURITY DEFINER ejecuta con permisos del creador, saltándose RLS.
-- - admin_update_user_role: ya valida rol admin internamente (se mantiene), se revoca anon/public.
-- - get_users_with_roles: NO validaba rol -> cualquiera con la anon key podía volcar
--   email+role de todos los usuarios. Se añade guard de admin + revoke anon/public.
-- - handle_new_user_profile / sync_role_to_auth_metadata: funciones de trigger,
--   no deben ser invocables por RPC. Se revoca execute a anon/authenticated/public.

-- 1) get_users_with_roles: añadir guard de admin (defensa principal)
create or replace function public.get_users_with_roles()
returns table(id uuid, email text, role text, created_at timestamptz)
language plpgsql
security definer
set search_path to 'public'
as $function$
begin
  -- Solo administradores pueden listar usuarios + roles
  if not exists (
    select 1 from public.profiles
    where user_id = auth.uid()
      and role = 'admin'::public.app_role
  ) then
    raise exception 'Acceso denegado: Se requiere rol de administrador';
  end if;

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
$function$;

-- 2) Revoke EXECUTE de anon/public en funciones administrativas
--    (mantienen execute para authenticated; el guard interno limita a admins)
revoke execute on function public.get_users_with_roles() from anon, public;
revoke execute on function public.admin_update_user_role(uuid, public.app_role) from anon, public;

-- 3) Funciones de trigger: no deben invocarse por RPC en absoluto
revoke execute on function public.handle_new_user_profile() from anon, authenticated, public;
revoke execute on function public.sync_role_to_auth_metadata() from anon, authenticated, public;
