import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider, useLocation } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';
import { useAuthStore } from './authStore';
import RequireAuth from './RequireAuth';
import RequireRole from './RequireRole';
import { Role } from './roles';

const teamLead = {
  id: '1',
  username: 'takimlideri',
  displayName: 'Zeynep Kaya',
  roles: ['TeamLead'],
};

function LoginProbe() {
  const location = useLocation();
  return <p>Giriş sayfası (geldiği yer: {location.state?.from?.pathname})</p>;
}

function renderAt(path) {
  const router = createMemoryRouter(
    [
      { path: '/login', element: <LoginProbe /> },
      {
        path: '/candidates',
        element: (
          <RequireAuth>
            <RequireRole roles={[Role.HrSpecialist, Role.HrManager]}>
              <p>Aday listesi</p>
            </RequireRole>
          </RequireAuth>
        ),
      },
      {
        path: '/',
        element: (
          <RequireAuth>
            <RequireRole roles={null}>
              <p>Ana sayfa</p>
            </RequireRole>
          </RequireAuth>
        ),
      },
    ],
    { initialEntries: [path] },
  );

  render(<RouterProvider router={router} />);
}

describe('rota korumaları', () => {
  beforeEach(() => {
    useAuthStore.setState({ status: 'anonymous', accessToken: null, user: null, signedOut: false });
  });

  it('oturum yoksa girişe yönlendirir ve gelinen sayfayı hatırlar', async () => {
    renderAt('/candidates');

    expect(await screen.findByText('Giriş sayfası (geldiği yer: /candidates)')).toBeInTheDocument();
  });

  it('bilinçli çıkıştan sonra gelinen sayfayı taşımaz', async () => {
    useAuthStore.getState().signOut();

    renderAt('/candidates');

    expect(await screen.findByText('Giriş sayfası (geldiği yer: )')).toBeInTheDocument();
  });

  it('rolü yetmeyen kullanıcıya sayfa yerine 403 gösterir', async () => {
    useAuthStore.setState({ status: 'authenticated', accessToken: 't', user: teamLead });

    renderAt('/candidates');

    expect(await screen.findByText('Bu sayfaya erişim yetkiniz yok')).toBeInTheDocument();
    expect(screen.queryByText('Aday listesi')).not.toBeInTheDocument();
  });

  it('rolü yeten kullanıcıya sayfayı gösterir', async () => {
    const hrManager = { ...teamLead, roles: ['TeamLead', 'HrManager'] };
    useAuthStore.setState({ status: 'authenticated', accessToken: 't', user: hrManager });

    renderAt('/candidates');

    expect(await screen.findByText('Aday listesi')).toBeInTheDocument();
  });

  it('rol kısıtı olmayan sayfayı oturum açmış herkese gösterir', async () => {
    useAuthStore.setState({ status: 'authenticated', accessToken: 't', user: teamLead });

    renderAt('/');

    expect(await screen.findByText('Ana sayfa')).toBeInTheDocument();
  });
});
