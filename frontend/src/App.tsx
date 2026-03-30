import { useState } from 'react';
import {
  AppBar, Box, CssBaseline, Drawer, List, ListItemButton,
  ListItemText, ThemeProvider, Toolbar, Typography, createTheme,
} from '@mui/material';
import LibrariesPage from './pages/LibrariesPage';
import VideosPage from './pages/VideosPage';

const theme = createTheme({ palette: { mode: 'dark' } });
const DRAWER_WIDTH = 200;

const NAV_ITEMS = [
  { label: '视频文件', page: 'videos' },
  { label: '媒体库', page: 'libraries' },
] as const;

type Page = (typeof NAV_ITEMS)[number]['page'];

export default function App() {
  const [page, setPage] = useState<Page>('videos');

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
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
              <ListItemButton key={item.page} selected={page === item.page} onClick={() => setPage(item.page)}>
                <ListItemText primary={item.label} />
              </ListItemButton>
            ))}
          </List>
        </Drawer>

        <Box component="main" sx={{ flexGrow: 1, p: 3, mt: 8 }}>
          {page === 'videos' && <VideosPage />}
          {page === 'libraries' && <LibrariesPage />}
        </Box>
      </Box>
    </ThemeProvider>
  );
}
