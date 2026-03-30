import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, Chip, CircularProgress, Pagination, Paper, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import { videosApi } from '../api/videos';
import type { PagedResult, VideoFile } from '../api/videos';
import { useNotify } from '../contexts/NotifyContext';

const PAGE_SIZE = 50;

export default function VideosPage() {
  const notify = useNotify();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [result, setResult] = useState<PagedResult<VideoFile> | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const load = async (searchValue: string, pageNum: number) => {
    setLoading(true);
    try {
      const res = await videosApi.getAll({
        search: searchValue || undefined,
        page: pageNum,
        page_size: PAGE_SIZE,
      });
      if (res.success) setResult(res.data);
    } catch (err) {
      notify((err as Error).message, 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load('', 1);
  }, []);

  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPage(1);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => load(value, 1), 300);
  };

  const handlePageChange = (_: React.ChangeEvent<unknown>, newPage: number) => {
    setPage(newPage);
    load(search, newPage);
  };

  const pageCount = result ? Math.ceil(result.total / PAGE_SIZE) : 0;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">
          {result ? t('videos.titleWithCount', { count: result.total }) : t('videos.title')}
        </Typography>
        <TextField
          label={t('videos.search')}
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
        <>
          <TableContainer component={Paper}>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>{t('videos.columns.filename')}</TableCell>
                  <TableCell>{t('videos.columns.title')}</TableCell>
                  <TableCell>{t('videos.columns.year')}</TableCell>
                  <TableCell>{t('videos.columns.studio')}</TableCell>
                  <TableCell>{t('videos.columns.nfo')}</TableCell>
                  <TableCell>{t('videos.columns.poster')}</TableCell>
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
                    <TableCell colSpan={6} align="center">{t('videos.empty')}</TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>

          {pageCount > 1 && (
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
              <Pagination
                count={pageCount}
                page={page}
                onChange={handlePageChange}
                color="primary"
                showFirstButton
                showLastButton
              />
            </Box>
          )}
        </>
      )}
    </Box>
  );
}
