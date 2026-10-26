import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from 'primereact/button';
import { InputText } from 'primereact/inputtext';
import { Message } from 'primereact/message';
import { Password } from 'primereact/password';
import { Controller, useForm } from 'react-hook-form';
import { Navigate, useLocation } from 'react-router';
import { useAuth } from '@/shared/auth/useAuth';
import FormField from '@/shared/components/FormField';
import { ROUTES } from '@/shared/constants/routes';
import { applyFieldErrors, getErrorMessage } from '@/shared/utils/errors';
import { useLogin } from '../queries';
import { loginSchema } from '../schemas';
import './LoginPage.css';

export default function LoginPage() {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  const login = useLogin();
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(loginSchema),
    defaultValues: { username: '', password: '' },
  });

  // Giriş başarılı olunca (veya oturum zaten açıksa) kullanıcı geldiği sayfaya döner.
  if (isAuthenticated) {
    return <Navigate to={location.state?.from ?? ROUTES.home} replace />;
  }

  const onSubmit = (values) =>
    login.mutate(values, { onError: (error) => applyFieldErrors(error, setError) });

  const hasFieldErrors = Boolean(login.error?.response?.data?.errors);
  const formError = login.isError && !hasFieldErrors ? getErrorMessage(login.error) : null;

  return (
    <main className="login-page">
      <section className="login-card card" aria-labelledby="login-title">
        <span className="login-card__logo" aria-hidden="true">
          <i className="pi pi-users" />
        </span>
        <h1 id="login-title" className="login-card__title">
          İK İşe Alım
        </h1>
        <p className="login-card__subtitle">Şirket hesabınızla giriş yapın.</p>

        <form className="login-form" onSubmit={handleSubmit(onSubmit)} noValidate>
          {formError && <Message severity="error" text={formError} />}

          <FormField label="Kullanıcı adı" htmlFor="username" error={errors.username?.message}>
            <InputText
              id="username"
              autoComplete="username"
              autoCapitalize="none"
              spellCheck={false}
              // Giriş sayfasının tek işi bu form; kullanıcı doğrudan yazmaya başlayabilsin.
              // eslint-disable-next-line jsx-a11y/no-autofocus
              autoFocus
              invalid={Boolean(errors.username)}
              aria-describedby={errors.username ? 'username-error' : undefined}
              {...register('username')}
            />
          </FormField>

          <FormField label="Şifre" htmlFor="password" error={errors.password?.message}>
            <Controller
              name="password"
              control={control}
              render={({ field }) => (
                <Password
                  inputId="password"
                  inputRef={field.ref}
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  autoComplete="current-password"
                  feedback={false}
                  toggleMask
                  invalid={Boolean(errors.password)}
                  pt={{
                    input: { 'aria-describedby': errors.password ? 'password-error' : undefined },
                  }}
                />
              )}
            />
          </FormField>

          <Button type="submit" label="Giriş yap" loading={login.isPending} />
        </form>

        <p className="login-card__hint">
          Bilgisayarınızı açarken kullandığınız kullanıcı adı ve şifreyle giriş yapabilirsiniz.
        </p>
      </section>
    </main>
  );
}
