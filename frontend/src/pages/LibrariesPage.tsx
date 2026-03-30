import { useEffect, useState } from 'react';
import {
  Alert, Box, Button, Checkbox, CircularProgress, Dialog, DialogActions,
  DialogContent, DialogTitle, Divider, FormControlLabel, IconButton,
  LinearProgress, ListItemIcon, ListItemText, Menu, MenuItem, Paper,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  TextField, Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import SyncIcon from '@mui/icons-material/Sync';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import FolderOffIcon from '@mui/icons-material/FolderOff';
import { librariesApi } from '../api/libraries';
import type { Library } from '../api/libraries';
import { useNotify } from '../contexts/NotifyContext';

// ── Excluded Folders Dialog ────────────────────────────────────────────────

interface ExcludedFoldersDialogProps {
  library: Library;
  onClose: () => void;
}

function ExcludedFoldersDialog({ library, onClose }: ExcludedFoldersDialogProps) {
  const notify = useNotify();
  const [subdirs, setSubdirs] = useState<string[]>([]);
  const [excluded, setExcluded] = useState<Set<string>>(new Set());
  const [loadingDirs, setLoadingDirs] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    Promise.all([
      librariesApi.getSubdirectories(library.id),
      librariesApi.getExcludedFolders(library.id),
    ]).then(([subRes, exRes]) => {
      if (subRes.success) setSubdirs(subRes.data);
      if (exRes.success) setExcluded(new Set(exRes.data));
    }).catch(err => {
      notify((err as Error).message, 'error');
    }).finally(() => setLoadingDirs(false));
  }, [library.id]);

  const toggle = (path: string) => {
    setExcluded(prev => {
      const next = new Set(prev);
      if (next.has(path)) next.delete(path);
      else next.add(path);
      return next;
    });
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await librariesApi.setExcludedFolders(library.id, [...excluded]);
      notify('排除文件夹设置已保存', 'success');
      onClose();
    } catch (err) {
      notify((err as Error).message, 'error');
    } finally {
      setSaving(false);
    }
  };

  // Shorten path to relative form for display
  const relative = (p: string) => {
    const base = library.path.endsWith('/') ? library.path : library.path + '/';
    return p.startsWith(base) ? p.slice(base.length) : p;
  };

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>
        排除文件夹
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          {library.name} — 勾选的文件夹将在扫描时被跳过
        </Typography>
      </DialogTitle>
      <Divider />
      <DialogContent sx={{ p: 0, minHeight: 200 }}>
        {loadingDirs ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : subdirs.length === 0 ? (
          <Typography color="text.secondary" sx={{ p: 3 }}>
            该媒体库根目录下没有子文件夹
          </Typography>
        ) : (
          <Box>
            {subdirs.map(dir => (
              <Box key={dir} sx={{ px: 2 }}>
                <FormControlLabel
                  control={
                    <Checkbox
                      checked={excluded.has(dir)}
                      onChange={() => toggle(dir)}
                      size="small"
                    />
                  }
                  label={
                    <Typography variant="body2" sx={{ fontFamily: 'monospace' }}>
                      {relative(dir)}
                    </Typography>
                  }
                  sx={{ width: '100%', py: 0.5 }}
                />
                <Divider />
              </Box>
            ))}
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Typography variant="caption" color="text.secondary" sx={{ flexGrow: 1, pl: 1 }}>
          {excluded.size > 0 ? `已排除 ${excluded.size} 个文件夹` : '未排除任何文件夹'}
        </Typography>
        <Button onClick={onClose} disabled={saving}>取消</Button>
        <Button
          onClick={handleSave}
          variant="contained"
          disabled={saving || loadingDirs}
          startIcon={saving ? <CircularProgress size={16} /> : null}
        >
          保存
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Main Page ──────────────────────────────────────────────────────────────

export default function LibrariesPage() {
  const notify = useNotify();
  const [libraries, setLibraries] = useState<Library[]>([]);
  const [loading, setLoading] = useState(true);

  // Create dialog
  const [createOpen, setCreateOpen] = useState(false);
  const [name, setName] = useState('');
  const [path, setPath] = useState('');
  const [creating, setCreating] = useState(false);

  // Scan state
  const [scanningLib, setScanningLib] = useState<Library | null>(null);

  // Three-dot menu
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);
  const [menuLib, setMenuLib] = useState<Library | null>(null);

  // Excluded folders dialog
  const [excludeLib, setExcludeLib] = useState<Library | null>(null);

  const load = async () => {
    try {
      const res = await librariesApi.getAll();
      if (res.success) setLibraries(res.data);
    } catch (err) {
      notify((err as Error).message, 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleCreate = async () => {
    if (!name.trim() || !path.trim()) return;
    setCreating(true);
    try {
      const res = await librariesApi.create(name.trim(), path.trim());
      if (res.success) {
        notify(`媒体库 "${res.data.name}" 添加成功`, 'success');
        setCreateOpen(false);
        setName('');
        setPath('');
        await load();
      } else {
        notify(res.error ?? '添加失败', 'error');
      }
    } catch (err) {
      notify((err as Error).message, 'error');
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (lib: Library) => {
    closeMenu();
    try {
      await librariesApi.delete(lib.id);
      notify(`媒体库 "${lib.name}" 已删除`, 'success');
      await load();
    } catch (err) {
      notify((err as Error).message, 'error');
    }
  };

  const handleScan = async (lib: Library) => {
    setScanningLib(lib);
    try {
      const res = await librariesApi.scan(lib.id);
      if (res.success) {
        notify(res.data, 'success');
      } else {
        notify(res.error ?? '扫描失败', 'error');
      }
    } catch (err) {
      notify((err as Error).message, 'error');
    } finally {
      setScanningLib(null);
    }
  };

  const openMenu = (e: React.MouseEvent<HTMLElement>, lib: Library) => {
    setMenuAnchor(e.currentTarget);
    setMenuLib(lib);
  };

  const closeMenu = () => {
    setMenuAnchor(null);
    setMenuLib(null);
  };

  const isScanning = scanningLib !== null;

  return (
    <Box>
      {isScanning && <LinearProgress sx={{ mb: 0 }} />}

      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, mt: isScanning ? 1 : 0 }}>
        <Typography variant="h5">媒体库</Typography>
        <Button variant="contained" onClick={() => setCreateOpen(true)} disabled={isScanning}>
          添加媒体库
        </Button>
      </Box>

      {isScanning && (
        <Alert
          severity="info"
          icon={<CircularProgress size={18} color="inherit" />}
          sx={{ mb: 2 }}
        >
          正在扫描「{scanningLib.name}」，请稍候，扫描完成前请勿离开此页面…
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer component={Paper} sx={{ opacity: isScanning ? 0.6 : 1, transition: 'opacity 0.2s' }}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>名称</TableCell>
                <TableCell>路径</TableCell>
                <TableCell>创建时间</TableCell>
                <TableCell align="right">操作</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {libraries.map(lib => (
                <TableRow key={lib.id}>
                  <TableCell>{lib.name}</TableCell>
                  <TableCell>{lib.path}</TableCell>
                  <TableCell>{new Date(lib.createdAt).toLocaleDateString()}</TableCell>
                  <TableCell align="right">
                    <IconButton
                      onClick={() => handleScan(lib)}
                      title="扫描"
                      disabled={isScanning}
                      size="small"
                    >
                      {scanningLib?.id === lib.id
                        ? <CircularProgress size={20} />
                        : <SyncIcon />}
                    </IconButton>
                    <IconButton
                      onClick={e => openMenu(e, lib)}
                      title="更多选项"
                      disabled={isScanning}
                      size="small"
                    >
                      <MoreVertIcon />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {libraries.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} align="center">暂无媒体库，请添加</TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Three-dot dropdown menu */}
      <Menu
        anchorEl={menuAnchor}
        open={Boolean(menuAnchor)}
        onClose={closeMenu}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        <MenuItem onClick={() => { closeMenu(); setExcludeLib(menuLib); }}>
          <ListItemIcon><FolderOffIcon fontSize="small" /></ListItemIcon>
          <ListItemText>排除文件夹</ListItemText>
        </MenuItem>
        <Divider />
        <MenuItem onClick={() => menuLib && handleDelete(menuLib)} sx={{ color: 'error.main' }}>
          <ListItemIcon><DeleteIcon fontSize="small" color="error" /></ListItemIcon>
          <ListItemText>删除媒体库</ListItemText>
        </MenuItem>
      </Menu>

      {/* Excluded folders dialog */}
      {excludeLib && (
        <ExcludedFoldersDialog
          library={excludeLib}
          onClose={() => setExcludeLib(null)}
        />
      )}

      {/* Create library dialog */}
      <Dialog open={createOpen} onClose={() => !creating && setCreateOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>添加媒体库</DialogTitle>
        <DialogContent>
          <TextField
            label="名称"
            fullWidth
            margin="dense"
            value={name}
            onChange={e => setName(e.target.value)}
            disabled={creating}
          />
          <TextField
            label="路径"
            fullWidth
            margin="dense"
            value={path}
            onChange={e => setPath(e.target.value)}
            disabled={creating}
            placeholder="/path/to/media"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)} disabled={creating}>取消</Button>
          <Button
            onClick={handleCreate}
            variant="contained"
            disabled={creating || !name.trim() || !path.trim()}
            startIcon={creating ? <CircularProgress size={16} /> : null}
          >
            添加
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
