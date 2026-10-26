import { Button } from 'primereact/button';
import { confirmDialog } from 'primereact/confirmdialog';
import { Tag } from 'primereact/tag';
import { useState } from 'react';
import DictionarySelect from '@/shared/components/DictionarySelect';
import FileDropzone from '@/shared/components/FileDropzone';
import FormField from '@/shared/components/FormField';
import { useToast } from '@/shared/components/toast';
import { getErrorMessage } from '@/shared/utils/errors';
import { formatDateTime, formatFileSize } from '@/shared/utils/format';
import { canPreview, openOrDownload } from '@/shared/utils/openDocument';
import { getDocumentDownloadUrl } from '../api';
import { useDocumentType } from '../documentTypes';
import { useDeleteDocument, useUploadDocument } from '../queries';

/**
 * Adayın dosyaları (CV, sertifika, case…). Yüklerken tür seçilir; türler yönetim panelindeki
 * "Dosya türleri" listesinden gelir. Açma/indirme, birkaç dakika geçerli bağlantıyla yapılır ve
 * kayda geçer: PDF ve resimler yan sekmede açılır, Word gibi dosyalar indirilir.
 * @param {{ candidate: object }} props
 */
export default function DocumentsTab({ candidate }) {
  const toast = useToast();
  const uploadDocument = useUploadDocument();
  const deleteDocument = useDeleteDocument();
  const [selectedTypeId, setSelectedTypeId] = useState(null);
  const { typeId, hasTypes } = useDocumentType(selectedTypeId);

  const upload = (file) =>
    uploadDocument.mutate(
      { id: candidate.id, file, typeId },
      {
        onSuccess: () =>
          toast.success('Dosya yüklendi. İçeriği birkaç saniye içinde aranabilir olacak.'),
        onError: (error) =>
          toast.error(error.response?.data?.errors?.file?.[0] ?? getErrorMessage(error)),
      },
    );

  const open = async (item) => {
    try {
      await openOrDownload(() => getDocumentDownloadUrl(candidate.id, item.id), item.contentType);
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  const download = async (item) => {
    try {
      await openOrDownload(() => getDocumentDownloadUrl(candidate.id, item.id), null);
    } catch (error) {
      toast.error(getErrorMessage(error));
    }
  };

  const confirmDelete = (item) =>
    confirmDialog({
      header: 'Dosya kaldırılsın mı?',
      message: `“${item.fileName}” aday kartından kaldırılacak ve içeriği aramalarda çıkmayacak.`,
      acceptLabel: 'Dosyayı kaldır',
      rejectLabel: 'Vazgeç',
      acceptClassName: 'p-button-danger',
      accept: () =>
        deleteDocument.mutate(
          { id: candidate.id, documentId: item.id },
          {
            onSuccess: () => toast.success('Dosya kaldırıldı.'),
            onError: (error) => toast.error(getErrorMessage(error)),
          },
        ),
    });

  return (
    <div className="detail-sections">
      {hasTypes && (
        <FormField
          label="Dosya türü"
          htmlFor="document-type"
          hint="Türleri yönetim panelinden ekleyebilirsiniz."
        >
          <DictionarySelect
            type="DocumentType"
            inputId="document-type"
            value={typeId}
            onChange={setSelectedTypeId}
          />
        </FormField>
      )}
      <FileDropzone onSelect={upload} busy={uploadDocument.isPending} />

      {candidate.documents.length > 0 && (
        <ul className="document-list">
          {candidate.documents.map((item) => (
            <li key={item.id} className="document-list__item">
              <i className="pi pi-file" aria-hidden="true" />
              <div className="document-list__main">
                <div>
                  <button
                    type="button"
                    className="table-link"
                    title={canPreview(item.contentType) ? 'Yan sekmede aç' : 'İndir'}
                    onClick={() => open(item)}
                  >
                    {item.fileName}
                  </button>
                  <Tag value={item.typeName} severity="info" className="inline-tag" />
                </div>
                <div className="cell-sub">
                  {[
                    formatFileSize(item.sizeBytes),
                    item.uploadedByName,
                    formatDateTime(item.uploadedAt),
                  ]
                    .filter(Boolean)
                    .join(' · ')}
                </div>
              </div>
              <Button
                icon="pi pi-download"
                text
                severity="secondary"
                aria-label={`${item.fileName} dosyasını indir`}
                onClick={() => download(item)}
              />
              <Button
                icon="pi pi-trash"
                text
                severity="secondary"
                aria-label={`${item.fileName} dosyasını kaldır`}
                onClick={() => confirmDelete(item)}
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
