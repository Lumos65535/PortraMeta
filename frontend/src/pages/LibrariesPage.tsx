import { useEffect, useState } from 'react';
import {
  Box, Button, Dialog, DialogActions, DialogContent, DialogTitle,
  IconButton, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import SyncIcon from '@mui/icons-material/Sync';
import { librariesApi } from '../api/libraries';
import type { Library } from '../api/libraries';

export default function LibrariesPage() {
  const [libraries, setLibraries] = useState<Library[]>([]);
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [path, setPath] = useState('');

  const load = async () => {
    const res = await librariesApi.getAll();
    if (res.success) setLibraries(res.data);
  };

  useEffect(() => { load(); }, []);

  const handleCreate = async () => {
    await librariesApi.create(name, path);
    setOpen(false);
    setName('');
    setPath('');
    load();
  };

  const handleDelete = async (id: number) => {
    await librariesApi.delete(id);
    load();
  };

  const handleScan = async (id: number) => {
    await librariesApi.scan(id);
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">媒体库</Typography>
        <Button variant="contained" onClick={() => setOpen(true)}>添加媒体库</Button>
      </Box>

      <TableContainer component={Paper}>
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
                  <IconButton onClick={() => handleScan(lib.id)} title="扫描"><SyncIcon /></IconButton>
                  <IconButton onClick={() => handleDelete(lib.id)} title="删除"><DeleteIcon /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={open} onClose={() => setOpen(false)}>
        <DialogTitle>添加媒体库</DialogTitle>
        <DialogContent>
          <TextField label="名称" fullWidth margin="dense" value={name} onChange={e => setName(e.target.value)} />
          <TextField label="路径" fullWidth margin="dense" value={path} onChange={e => setPath(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)}>取消</Button>
          <Button onClick={handleCreate} variant="contained">添加</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
