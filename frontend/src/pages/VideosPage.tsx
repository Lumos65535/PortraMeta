import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, Chip, CircularProgress, Paper, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import { videosApi } from '../api/videos';
import type { PagedResult, VideoFile } from '../api/videos';
import { useNotify } from '../contexts/NotifyContext';

export default function VideosPage() {
  const notify = useNotify();
  const navigate = useNavigate();
  const [result, setResult] = useState<PagedResult<VideoFile> | null>(null);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const load = async (searchValue: string) => {
    setLoading(true);
    try {
      const res = await videosApi.getAll({ search: searchValue || undefined, page: 1, page_size: 50 });
      if (res.success) setResult(res.data);
    } catch (err) {
      notify((err as Error).message, 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load('');
  }, []);

  const handleSearchChange = (value: string) => {
    setSearch(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => load(value), 300);
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">
          视频文件 {result && `(${result.total})`}
        </Typography>
        <TextField
          label="搜索"
          size="small"
          value={search}
          onChange={e => handleSearchChange(e.target.value)}
        />
      </Box>

      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>文件名</TableCell>
                <TableCell>标题</TableCell>
                <TableCell>年份</TableCell>
                <TableCell>厂牌</TableCell>
                <TableCell>NFO</TableCell>
                <TableCell>海报</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {result?.items.map(v => (
                <TableRow
                  key={v.id}
                  hover
                  sx={{ cursor: 'pointer' }}
                  onClick={() => navigate(`/videos/${v.id}`)}
                >
                  <TableCell>{v.fileName}</TableCell>
                  <TableCell>{v.title ?? '—'}</TableCell>
                  <TableCell>{v.year ?? '—'}</TableCell>
                  <TableCell>{v.studioName ?? '—'}</TableCell>
                  <TableCell>
                    <Chip label={v.hasNfo ? '✓' : '✗'} color={v.hasNfo ? 'success' : 'default'} size="small" />
                  </TableCell>
                  <TableCell>
                    <Chip label={v.hasPoster ? '✓' : '✗'} color={v.hasPoster ? 'success' : 'default'} size="small" />
                  </TableCell>
                </TableRow>
              ))}
              {result?.items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} align="center">暂无视频文件，请先添加媒体库并扫描</TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
