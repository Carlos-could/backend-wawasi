-- F1-T04: roles inquilino/propietario/admin + RLS propietario/admin en properties
-- Ejecutar con Supabase CLI:
--   supabase db push

do $$
begin
  if exists (select 1 from pg_type where typname = 'app_role')
     and not exists (select 1 from pg_type where typname = 'app_role_v2') then
    create type public.app_role_v2 as enum ('inquilino', 'propietario', 'admin');
  end if;
end
$$;

do $$
begin
  if exists (select 1 from pg_type where typname = 'app_role_v2') then
    alter table public.profiles alter column role drop default;

    alter table public.profiles
      alter column role type public.app_role_v2
      using (
        case lower(role::text)
          when 'admin' then 'admin'::public.app_role_v2
          when 'member' then 'propietario'::public.app_role_v2
          when 'propietario' then 'propietario'::public.app_role_v2
          when 'inquilino' then 'inquilino'::public.app_role_v2
          else 'inquilino'::public.app_role_v2
        end
      );

    drop type public.app_role;
    alter type public.app_role_v2 rename to app_role;
    alter table public.profiles alter column role set default 'inquilino'::public.app_role;
  end if;
end
$$;

create or replace function public.handle_new_user_profile()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
  metadata_role text;
begin
  metadata_role := lower(coalesce(new.raw_app_meta_data ->> 'role', 'inquilino'));

  insert into public.profiles (user_id, role)
  values (
    new.id,
    case
      when metadata_role = 'admin' then 'admin'::public.app_role
      when metadata_role = 'member' then 'propietario'::public.app_role
      when metadata_role = 'propietario' then 'propietario'::public.app_role
      else 'inquilino'::public.app_role
    end
  )
  on conflict (user_id) do nothing;

  return new;
end;
$$;

drop policy if exists "properties_insert_admin" on public.properties;
drop policy if exists "properties_insert_owner_or_admin" on public.properties;
create policy "properties_insert_owner_or_admin"
on public.properties
for insert
to authenticated
with check (
  created_by = auth.uid()
  and exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role in ('propietario'::public.app_role, 'admin'::public.app_role)
  )
);

drop policy if exists "properties_update_admin" on public.properties;
drop policy if exists "properties_update_owner_or_admin" on public.properties;
create policy "properties_update_owner_or_admin"
on public.properties
for update
to authenticated
using (
  (
    created_by = auth.uid()
    and exists (
      select 1
      from public.profiles as p
      where p.user_id = auth.uid()
        and p.role = 'propietario'::public.app_role
    )
  )
  or exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role = 'admin'::public.app_role
  )
)
with check (
  (
    created_by = auth.uid()
    and exists (
      select 1
      from public.profiles as p
      where p.user_id = auth.uid()
        and p.role = 'propietario'::public.app_role
    )
  )
  or exists (
    select 1
    from public.profiles as p
    where p.user_id = auth.uid()
      and p.role = 'admin'::public.app_role
  )
);
