import { BrowserRouter, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import {
  AppBar, Box, CssBaseline, Drawer, List, ListItemButton,
  ListItemText, ThemeProvider, Toolbar, Typography, createTheme,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import LibrariesPage from './pages/LibrariesPage';
import VideosPage from './pages/VideosPage';
import VideoDetailPage from './pages/VideoDetailPage';
import SettingsPage from './pages/SettingsPage';
import { NotifyProvider } from './contexts/NotifyContext';

const theme = createTheme({ palette: { mode: 'dark' } });
const DRAWER_WIDTH = 200;

function Layout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { t } = useTranslation();

  const NAV_ITEMS = [
    { labelKey: 'nav.videos', path: '/videos' },
    { labelKey: 'nav.libraries', path: '/libraries' },
    { labelKey: 'nav.settings', path: '/settings' },
  ] as const;

  return (
    <Box sx={{ display: 'flex', width: '100%', minWidth: 0, overflowX: 'hidden' }}>
      <AppBar position="fixed" sx={{ zIndex: t => t.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6">NfoForge</Typography>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="permanent"
        sx={{
          width: DRAWER_WIDTH,
          flexShrink: 0,
          '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        <List>
          {NAV_ITEMS.map(item => (
            <ListItemButton
              key={item.path}
              selected={location.pathname.startsWith(item.path)}
              onClick={() => navigate(item.path)}
            >
              <ListItemText primary={t(item.labelKey)} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, overflowX: 'hidden', p: 3, mt: 8 }}>
        <Routes>
          <Route path="/" element={<Navigate to="/videos" replace />} />
          <Route path="/videos" element={<VideosPage />} />
          <Route path="/videos/:id" element={<VideoDetailPage />} />
          <Route path="/libraries" element={<LibrariesPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </Box>
    </Box>
  );
}

export default function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <NotifyProvider>
        <BrowserRouter>
          <Layout />
        </BrowserRouter>
      </NotifyProvider>
    </ThemeProvider>
  );
}
