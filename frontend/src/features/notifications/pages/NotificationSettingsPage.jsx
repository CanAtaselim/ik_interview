import { Button } from 'primereact/button';
import { InputSwitch } from 'primereact/inputswitch';
import { useState } from 'react';
import PageHeader from '@/shared/components/PageHeader';
import TableSkeleton from '@/shared/components/TableSkeleton';
import { useToast } from '@/shared/components/toast';
import { NOTIFICATION_GROUPS, NOTIFICATION_TYPE_LABELS } from '@/shared/constants/labels';
import {
  useNotificationPreferences,
  useUpdateNotificationPreferences,
} from '@/shared/notifications/queries';
import { getErrorMessage } from '@/shared/utils/errors';
import './NotificationSettingsPage.css';

/**
 * Hangi bildirimlerin e-postayla da gelmesini istediğini kullanıcı seçer. Uygulama içi bildirimler
 * her zaman gelir.
 */
export default function NotificationSettingsPage() {
  const toast = useToast();
  const { data, isLoading } = useNotificationPreferences();
  const save = useUpdateNotificationPreferences();
  const [changes, setChanges] = useState({});

  if (isLoading || !data) return <TableSkeleton rows={6} />;

  const enabled = (type) =>
    changes[type] ?? data.find((item) => item.type === type)?.emailEnabled ?? true;
  const dirty = Object.keys(changes).length > 0;

  const submit = () =>
    save.mutate(
      Object.entries(changes).map(([type, emailEnabled]) => ({ type, emailEnabled })),
      {
        onSuccess: () => {
          setChanges({});
          toast.success('Bildirim tercihleriniz kaydedildi.');
        },
        onError: (error) => toast.error(getErrorMessage(error)),
      },
    );

  return (
    <>
      <PageHeader
        title="Bildirim tercihlerim"
        subtitle="Uygulama içi bildirimler her zaman gelir. Hangilerinin e-postayla da gelmesini istediğinizi seçin."
        actions={
          <Button label="Kaydet" disabled={!dirty} loading={save.isPending} onClick={submit} />
        }
      />
      <div className="detail-sections">
        {NOTIFICATION_GROUPS.map((group) => (
          <section
            key={group.title}
            className="card card--padded"
            aria-labelledby={`group-${group.key}`}
          >
            <h2 id={`group-${group.key}`} className="section-title">
              {group.title}
            </h2>
            <ul className="preference-list">
              {group.types.map((type) => (
                <li key={type} className="preference-item">
                  <label htmlFor={`pref-${type}`} className="preference-item__text">
                    <strong>{NOTIFICATION_TYPE_LABELS[type].label}</strong>
                    <span className="cell-sub">{NOTIFICATION_TYPE_LABELS[type].description}</span>
                  </label>
                  <span className="preference-item__switch">
                    <span className="cell-sub">E-posta</span>
                    <InputSwitch
                      inputId={`pref-${type}`}
                      checked={enabled(type)}
                      onChange={(event) =>
                        setChanges((current) => ({ ...current, [type]: event.value }))
                      }
                    />
                  </span>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>
    </>
  );
}
