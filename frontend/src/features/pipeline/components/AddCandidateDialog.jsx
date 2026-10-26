import { useQuery } from '@tanstack/react-query';
import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { IconField } from 'primereact/iconfield';
import { InputIcon } from 'primereact/inputicon';
import { InputText } from 'primereact/inputtext';
import { useState } from 'react';
import { getCandidates } from '@/features/candidates/api';
import { useToast } from '@/shared/components/toast';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { useStartApplication } from '@/shared/recruitment/queries';
import { getErrorMessage } from '@/shared/utils/errors';

const MAX_RESULTS = 8;

/**
 * Havuzdan aday arayıp pozisyonun sürecine ekler. Pencere açık kalır; art arda birden çok aday eklenebilir.
 * @param {{ positionId: string, candidateIdsInProcess: string[], onClose: () => void }} props
 */
export default function AddCandidateDialog({ positionId, candidateIdsInProcess, onClose }) {
  const toast = useToast();
  const startApplication = useStartApplication();
  const [searchText, setSearchText] = useState('');
  const search = useDebounce(searchText);

  const { data, isFetching } = useQuery({
    queryKey: ['candidates', 'picker', search],
    queryFn: () => getCandidates({ search: search || undefined, pageSize: MAX_RESULTS }),
  });
  const candidates = data?.items ?? [];

  const add = (candidate) =>
    startApplication.mutate(
      { candidateId: candidate.id, positionId },
      {
        onSuccess: () =>
          toast.success(`${candidate.firstName} ${candidate.lastName} sürece eklendi.`),
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );

  return (
    <Dialog visible header="Sürece aday ekle" onHide={onClose} className="form-dialog" modal>
      <IconField iconPosition="left" className="picker-search">
        <InputIcon className={isFetching ? 'pi pi-spin pi-spinner' : 'pi pi-search'} />
        <InputText
          value={searchText}
          onChange={(event) => setSearchText(event.target.value)}
          placeholder="Ad, e-posta, telefon, yetkinlik veya CV içeriği"
          aria-label="Aday ara"
          // Pencere yalnızca arama için açılır; kullanıcı doğrudan yazabilsin.
          // eslint-disable-next-line jsx-a11y/no-autofocus
          autoFocus
        />
      </IconField>

      {candidates.length === 0 ? (
        <p className="muted picker-empty">
          {search
            ? 'Aramanıza uyan aday bulunamadı.'
            : 'Havuzda henüz aday yok. Önce “Aday Listesi” ekranından ekleyin.'}
        </p>
      ) : (
        <ul className="picker-list">
          {candidates.map((candidate) => {
            const inProcess = candidateIdsInProcess.includes(candidate.id);
            return (
              <li key={candidate.id} className="picker-list__item">
                <div className="picker-list__main">
                  <span className="picker-list__name">
                    {candidate.firstName} {candidate.lastName}
                  </span>
                  <span className="cell-sub">
                    {[candidate.email, candidate.phone].filter(Boolean).join(' · ')}
                  </span>
                </div>
                {inProcess ? (
                  <span className="muted">Süreçte</span>
                ) : (
                  <Button
                    label="Ekle"
                    icon="pi pi-plus"
                    outlined
                    disabled={startApplication.isPending}
                    onClick={() => add(candidate)}
                  />
                )}
              </li>
            );
          })}
        </ul>
      )}
    </Dialog>
  );
}
