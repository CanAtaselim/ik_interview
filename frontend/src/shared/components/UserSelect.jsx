import { useQuery } from '@tanstack/react-query';
import { Dropdown } from 'primereact/dropdown';
import { searchUsers } from '@/shared/api/users';

/**
 * Belirli rollerdeki kullanıcılardan birini seçtiren açılır liste.
 * @param {{ roles: string[], value: string | null, onChange: (id: string | null) => void,
 *   inputId?: string, placeholder?: string, invalid?: boolean }} props
 */
export default function UserSelect({
  roles,
  value,
  onChange,
  inputId,
  placeholder = 'Seçin',
  invalid = false,
}) {
  const { data, isLoading } = useQuery({
    queryKey: ['users', 'select', roles],
    queryFn: () => searchUsers({ roles }),
    staleTime: 60_000,
  });
  const users = data?.items ?? [];

  return (
    <Dropdown
      inputId={inputId}
      value={value || null}
      options={users}
      optionLabel="displayName"
      optionValue="id"
      onChange={(event) => onChange(event.value ?? null)}
      placeholder={isLoading ? 'Yükleniyor…' : placeholder}
      emptyMessage="Seçilebilecek kullanıcı yok. Kullanıcılar ilk girişlerinden sonra listelenir."
      invalid={invalid}
      disabled={isLoading}
      showClear
      filter
      filterInputAutoFocus
    />
  );
}
