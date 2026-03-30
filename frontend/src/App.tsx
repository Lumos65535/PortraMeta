import { BrowserRouter, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import {
  AppBar, Box, CssBaseline, Drawer, List, ListItemButton,
  ListItemText, ThemeProvider, Toolbar, Typography, createTheme,
} from '@mui/material';
import LibrariesPage from './pages/LibrariesPage';
import VideosPage from './pages/VideosPage';
import VideoDetailPage from './pages/VideoDetailPage';
import { NotifyProvider } from './contexts/NotifyContext';

const theme = createTheme({ palette: { mode: 'dark' } });
const DRAWER_WIDTH = 200;

const NAV_ITEMS = [
  { label: '视频文件', path: '/videos' },
  { label: '媒体库', path: '/libraries' },
] as const;

function Layout() {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Box sx={{ display: 'flex' }}>
      <AppBar position="fixed" sx={{ zIndex: t => t.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6">NfoForge</Typography>
        </Toolbar>
      </AppBar>

      <Drawer variant="permanent" sx={{ width: DRAWER_WIDTH, '& .MuiDrawer-paper': { width: DRAWER_WIDTH } }}>
        <Toolbar />
        <List>
          {NAV_ITEMS.map(item => (
            <ListItemButton
              key={item.path}
              selected={location.pathname.startsWith(item.path)}
              onClick={() => navigate(item.path)}
            >
              <ListItemText primary={item.label} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>

      <Box component="main" sx={{ flexGrow: 1, p: 3, mt: 8 }}>
        <Routes>
          <Route path="/" element={<Navigate to="/videos" replace />} />
          <Route path="/videos" element={<VideosPage />} />
          <Route path="/videos/:id" element={<VideoDetailPage />} />
          <Route path="/libraries" element={<LibrariesPage />} />
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
