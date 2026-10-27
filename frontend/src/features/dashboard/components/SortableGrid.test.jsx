import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { DashboardLayoutContext } from './DashboardLayoutContext';
import SortableGrid from './SortableGrid';

const Card = ({ title }) => <section>{title}</section>;

function renderGrid({ editing, saved }) {
  render(
    <DashboardLayoutContext.Provider
      value={{
        editing,
        orderFor: (_gridId, ids) => (saved ? saved.filter((id) => ids.includes(id)) : ids),
        setOrder: vi.fn(),
        widthFor: (_cardId, defaultWidth) => defaultWidth,
        setWidth: vi.fn(),
      }}
    >
      <SortableGrid id="test.1">
        <Card cardId="a" title="Kart A" />
        <Card cardId="b" title="Kart B" width={4} />
        <Card cardId="c" title="Kart C" />
      </SortableGrid>
    </DashboardLayoutContext.Provider>,
  );
}

const titles = () => [...document.querySelectorAll('section')].map((el) => el.textContent);

describe('SortableGrid', () => {
  it('düzenleme kapalıyken tutamaç göstermez ve kayıtlı sırayı uygular', () => {
    renderGrid({ editing: false, saved: ['c', 'a', 'b'] });

    expect(titles()).toEqual(['Kart C', 'Kart A', 'Kart B']);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('düzenleme açıkken her karta bir tutamaç koyar', () => {
    renderGrid({ editing: true });

    expect(screen.getAllByRole('button', { name: /Kartı taşı/ })).toHaveLength(3);
    expect(screen.getAllByRole('button', { name: /Tam satır/ })).toHaveLength(3);
  });

  it('tutamaçlar klavyeyle sürüklenebilir olarak işaretlenir', () => {
    renderGrid({ editing: true });

    const handle = screen.getByRole('button', { name: /Kartı taşı: Kart A/ });
    expect(handle).toHaveAttribute('aria-roledescription', 'sortable');
    expect(handle).toHaveAttribute('tabindex', '0');
  });
});
