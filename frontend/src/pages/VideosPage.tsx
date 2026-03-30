import { useEffect, useState } from 'react';
import {
  Box, Chip, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import { videosApi } from '../api/videos';
import type { VideoFile, PagedResult } from '../api/videos';

export default function VideosPage() {
  const [result, setResult] = useState<PagedResult<VideoFile> | null>(null);
  const [search, setSearch] = useState('');

  const load = async () => {
    const res = await videosApi.getAll({ search: search || undefined, page: 1, page_size: 50 });
    if (res.success) setResult(res.data);
  };

  useEffect(() => { load(); }, [search]);

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">视频文件 {result && `(${result.total})`}</Typography>
        <TextField
          label="搜索"
          size="small"
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
      </Box>

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
              <TableRow key={v.id}>
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
    </Box>
  );
}
