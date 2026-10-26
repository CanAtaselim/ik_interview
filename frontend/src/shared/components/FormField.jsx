import './FormField.css';

/**
 * Etiket + giriş + hata mesajı. Hata, girişe `aria-describedby` ile bağlanabilsin diye
 * `${htmlFor}-error` kimliğini taşır.
 * @param {{ label: string, htmlFor: string, error?: string, required?: boolean,
 *   hint?: string, children: import('react').ReactNode }} props
 */
export default function FormField({ label, htmlFor, error, required = false, hint, children }) {
  return (
    <div className="form-field">
      <label className="form-field__label" htmlFor={htmlFor}>
        {label}
        {required && (
          <span className="form-field__required" aria-hidden="true">
            {' '}
            *
          </span>
        )}
      </label>
      {children}
      {hint && !error && <small className="form-field__hint">{hint}</small>}
      {error && (
        <small id={`${htmlFor}-error`} className="form-field__error" role="alert">
          {error}
        </small>
      )}
    </div>
  );
}
