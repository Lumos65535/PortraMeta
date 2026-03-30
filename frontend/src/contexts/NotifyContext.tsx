import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import { Alert, Snackbar } from '@mui/material';

type Severity = 'success' | 'error' | 'warning' | 'info';

interface NotifyContextValue {
  notify: (message: string, severity?: Severity) => void;
}

const NotifyContext = createContext<NotifyContextValue | null>(null);

interface Notification {
  message: string;
  severity: Severity;
  key: number;
}

export function NotifyProvider({ children }: { children: ReactNode }) {
  const [current, setCurrent] = useState<Notification | null>(null);
  const [open, setOpen] = useState(false);

  const notify = useCallback((message: string, severity: Severity = 'info') => {
    setCurrent({ message, severity, key: Date.now() });
    setOpen(true);
  }, []);

  return (
    <NotifyContext.Provider value={{ notify }}>
      {children}
      <Snackbar
        key={current?.key}
        open={open}
        autoHideDuration={4000}
        onClose={() => setOpen(false)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity={current?.severity} onClose={() => setOpen(false)} variant="filled">
          {current?.message}
        </Alert>
      </Snackbar>
    </NotifyContext.Provider>
  );
}

export function useNotify() {
  const ctx = useContext(NotifyContext);
  if (!ctx) throw new Error('useNotify must be used within NotifyProvider');
  return ctx.notify;
}
