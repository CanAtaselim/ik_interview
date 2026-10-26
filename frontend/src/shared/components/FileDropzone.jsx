import { useRef, useState } from 'react';
import './FileDropzone.css';

/**
 * Dosya seçme alanı: tıklayarak veya sürükleyip bırakarak. Tür ve boyut burada kullanıcıya
 * hemen söylenir; asıl kontrol sunucudadır.
 * @param {{ onSelect: (file: File) => void, extensions?: string[], maxSizeMb?: number,
 *   label?: string, disabled?: boolean, busy?: boolean }} props
 */
export default function FileDropzone({
  onSelect,
  extensions = ['.pdf', '.doc', '.docx', '.png', '.jpg', '.jpeg'],
  maxSizeMb = 10,
  label = 'Dosya yüklemek için tıklayın veya dosyayı buraya sürükleyin',
  disabled = false,
  busy = false,
}) {
  const input = useRef(null);
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState(null);

  const accept = (file) => {
    if (!file) return;

    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!extensions.includes(extension)) {
      setError('Yalnızca PDF, Word (DOC, DOCX) veya resim (PNG, JPG) dosyası yükleyebilirsiniz.');
    } else if (file.size > maxSizeMb * 1024 * 1024) {
      setError(`Dosya en fazla ${maxSizeMb} MB olabilir.`);
    } else {
      setError(null);
      onSelect(file);
    }
  };

  const onDrop = (event) => {
    event.preventDefault();
    setDragging(false);
    if (!disabled && !busy) accept(event.dataTransfer.files[0]);
  };

  return (
    <div className="file-dropzone-field">
      <button
        type="button"
        className="file-dropzone"
        data-dragging={dragging}
        disabled={disabled || busy}
        onClick={() => input.current.click()}
        onDragOver={(event) => {
          event.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
      >
        <i className={busy ? 'pi pi-spin pi-spinner' : 'pi pi-cloud-upload'} aria-hidden="true" />
        <span>{busy ? 'Yükleniyor…' : label}</span>
        <small>PDF, Word veya resim · en fazla {maxSizeMb} MB</small>
      </button>
      <input
        ref={input}
        type="file"
        className="visually-hidden"
        tabIndex={-1}
        aria-hidden="true"
        accept={extensions.join(',')}
        onChange={(event) => {
          accept(event.target.files[0]);
          // Aynı dosya yeniden seçilebilsin.
          event.target.value = '';
        }}
      />
      {error && (
        <small className="form-field__error" role="alert">
          {error}
        </small>
      )}
    </div>
  );
}
