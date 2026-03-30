import { useEffect, useState } from 'react';
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, IconButton, LinearProgress, Paper, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import SyncIcon from '@mui/icons-material/Sync';
import { librariesApi } from '../api/libraries';
import type { Library } from '../api/libraries';
import { useNotify } from '../contexts/NotifyContext';

export default function LibrariesPage() {
  const notify = useNotify();
  const [libraries, setLibraries] = useState<Library[]>([]);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [path, setPath] = useState('');
  const [creating, setCreating] = useState(false);
  const [scanningLib, setScanningLib] = useState<Library | null>(null);

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
        setOpen(false);
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

  const handleDelete = async (id: number, libName: string) => {
    try {
      await librariesApi.delete(id);
      notify(`媒体库 "${libName}" 已删除`, 'success');
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

  const isScanning = scanningLib !== null;

  return (
    <Box>
      {/* Scanning progress bar — pinned to top of content area */}
      {isScanning && <LinearProgress sx={{ mb: 0 }} />}

      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, mt: isScanning ? 1 : 0 }}>
        <Typography variant="h5">媒体库</Typography>
        <Button variant="contained" onClick={() => setOpen(true)} disabled={isScanning}>
          添加媒体库
        </Button>
      </Box>

      {/* Persistent scan-in-progress banner */}
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
                    >
                      {scanningLib?.id === lib.id
                        ? <CircularProgress size={20} />
                        : <SyncIcon />}
                    </IconButton>
                    <IconButton
                      onClick={() => handleDelete(lib.id, lib.name)}
                      title="删除"
                      disabled={isScanning}
                    >
                      <DeleteIcon />
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

      <Dialog open={open} onClose={() => !creating && setOpen(false)} fullWidth maxWidth="sm">
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
          <Button onClick={() => setOpen(false)} disabled={creating}>取消</Button>
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
