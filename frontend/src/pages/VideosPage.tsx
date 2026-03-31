import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, Button, Checkbox, Chip, CircularProgress, FormControlLabel, Menu, TextField, Typography,
} from '@mui/material';
import ViewColumnIcon from '@mui/icons-material/ViewColumn';
import { useTranslation } from 'react-i18next';
import {
  DataGrid,
  type GridColDef,
  type GridColumnVisibilityModel,
  type GridPaginationModel,
  type GridSortModel,
} from '@mui/x-data-grid';
import { videosApi } from '../api/videos';
import type { PagedResult, VideoFile } from '../api/videos';
import { useNotify } from '../contexts/NotifyContext';

const PAGE_SIZE = 50;
const STORAGE_KEY = 'nfoforge_videos_grid_v1';

const DEFAULT_VISIBILITY: GridColumnVisibilityModel = {
  fileName: true,
  title: true,
  year: true,
  studioName: true,
  hasNfo: true,
  hasPoster: true,
  hasFanart: false,
  filePath: false,
  fileSizeBytes: false,
  originalTitle: false,
  plot: false,
  scannedAt: false,
  fileModifiedAt: false,
};

const COLUMN_DEFAULT_WIDTHS: Record<string, number> = {
  fileName: 360,
  title: 280,
  year: 100,
  studioName: 180,
  hasNfo: 90,
  hasPoster: 90,
  hasFanart: 100,
  filePath: 460,
  fileSizeBytes: 130,
  originalTitle: 260,
  plot: 320,
  scannedAt: 200,
  fileModifiedAt: 200,
};

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

function readGridSettings(): {
  visibilityModel: GridColumnVisibilityModel;
  widthModel: Record<string, number>;
  sortModel: GridSortModel;
  page: number;
} {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return { visibilityModel: DEFAULT_VISIBILITY, widthModel: {}, sortModel: [], page: 1 };
    }

    const parsed = JSON.parse(raw) as {
      visibilityModel?: GridColumnVisibilityModel;
      widthModel?: Record<string, number>;
      sortModel?: GridSortModel;
      page?: number;
    };

    return {
      visibilityModel: {
        ...DEFAULT_VISIBILITY,
        ...(parsed.visibilityModel ?? {}),
      },
      widthModel: parsed.widthModel ?? {},
      sortModel: parsed.sortModel ?? [],
      page: parsed.page ?? 1,
    };
  } catch {
    return { visibilityModel: DEFAULT_VISIBILITY, widthModel: {}, sortModel: [], page: 1 };
  }
}

export default function VideosPage() {
  const notify = useNotify();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [result, setResult] = useState<PagedResult<VideoFile> | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(() => readGridSettings().page);
  const [sortModel, setSortModel] = useState<GridSortModel>(() => readGridSettings().sortModel);
  const [loading, setLoading] = useState(true);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const persistRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [fieldMenuAnchor, setFieldMenuAnchor] = useState<null | HTMLElement>(null);

  const [columnVisibilityModel, setColumnVisibilityModel] =
    useState<GridColumnVisibilityModel>(() => readGridSettings().visibilityModel);
  const [columnWidthModel, setColumnWidthModel] =
    useState<Record<string, number>>(() => readGridSettings().widthModel);

  useEffect(() => {
    if (persistRef.current) {
      clearTimeout(persistRef.current);
    }

    persistRef.current = setTimeout(() => {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          visibilityModel: columnVisibilityModel,
          widthModel: columnWidthModel,
          sortModel,
          page,
        }),
      );
    }, 200);

    return () => {
      if (persistRef.current) {
        clearTimeout(persistRef.current);
      }
    };
  }, [columnVisibilityModel, columnWidthModel, sortModel, page]);

  const load = async (searchValue: string, pageNum: number, sort: GridSortModel) => {
    setLoading(true);
    try {
      const sortField = sort[0]?.field;
      const sortDir = sort[0]?.sort;
      const res = await videosApi.getAll({
        search: searchValue || undefined,
        page: pageNum,
        page_size: PAGE_SIZE,
        sort_by: sortField,
        sort_desc: sortDir === 'desc' ? true : undefined,
      });
      if (res.success) setResult(res.data);
    } catch (err) {
      notify((err as Error).message, 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load(search, page, sortModel);
  }, []);

  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPage(1);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => load(value, 1, sortModel), 300);
  };

  const handlePageChange = (_: React.ChangeEvent<unknown>, newPage: number) => {
    setPage(newPage);
    load(search, newPage, sortModel);
  };

  const handleGridPaginationChange = (model: GridPaginationModel) => {
    const newPage = model.page + 1;
    if (newPage !== page) {
      handlePageChange({} as React.ChangeEvent<unknown>, newPage);
    }
  };

  const handleSortModelChange = (model: GridSortModel) => {
    setSortModel(model);
    setPage(1);
    load(search, 1, model);
  };

  const setColumnVisibility = (field: string, checked: boolean) => {
    setColumnVisibilityModel(prev => ({
      ...prev,
      [field]: checked,
    }));
  };

  const pageCount = result ? Math.ceil(result.total / PAGE_SIZE) : 0;

  const columns: GridColDef<VideoFile>[] = [
    {
      field: 'fileName',
      headerName: t('videos.columns.filename'),
      minWidth: 180,
      width: columnWidthModel.fileName ?? COLUMN_DEFAULT_WIDTHS.fileName,
    },
    {
      field: 'title',
      headerName: t('videos.columns.title'),
      minWidth: 160,
      width: columnWidthModel.title ?? COLUMN_DEFAULT_WIDTHS.title,
      valueGetter: (_value, row) => row.title ?? '—',
    },
    {
      field: 'year',
      headerName: t('videos.columns.year'),
      minWidth: 80,
      width: columnWidthModel.year ?? COLUMN_DEFAULT_WIDTHS.year,
      valueGetter: (_value, row) => row.year ?? '—',
    },
    {
      field: 'studioName',
      headerName: t('videos.columns.studio'),
      minWidth: 120,
      width: columnWidthModel.studioName ?? COLUMN_DEFAULT_WIDTHS.studioName,
      valueGetter: (_value, row) => row.studioName ?? '—',
    },
    {
      field: 'hasNfo',
      headerName: t('videos.columns.nfo'),
      minWidth: 80,
      width: columnWidthModel.hasNfo ?? COLUMN_DEFAULT_WIDTHS.hasNfo,
      sortable: false,
      renderCell: params => (
        <Chip label={params.row.hasNfo ? '✓' : '✗'} color={params.row.hasNfo ? 'success' : 'default'} size="small" />
      ),
    },
    {
      field: 'hasPoster',
      headerName: t('videos.columns.poster'),
      minWidth: 80,
      width: columnWidthModel.hasPoster ?? COLUMN_DEFAULT_WIDTHS.hasPoster,
      sortable: false,
      renderCell: params => (
        <Chip label={params.row.hasPoster ? '✓' : '✗'} color={params.row.hasPoster ? 'success' : 'default'} size="small" />
      ),
    },
    {
      field: 'hasFanart',
      headerName: t('videos.columns.fanart'),
      minWidth: 90,
      width: columnWidthModel.hasFanart ?? COLUMN_DEFAULT_WIDTHS.hasFanart,
      sortable: false,
      renderCell: params => (
        <Chip label={params.row.hasFanart ? '✓' : '✗'} color={params.row.hasFanart ? 'success' : 'default'} size="small" />
      ),
    },
    {
      field: 'filePath',
      headerName: t('videos.columns.path'),
      minWidth: 220,
      width: columnWidthModel.filePath ?? COLUMN_DEFAULT_WIDTHS.filePath,
    },
    {
      field: 'fileSizeBytes',
      headerName: t('videos.columns.size'),
      minWidth: 110,
      width: columnWidthModel.fileSizeBytes ?? COLUMN_DEFAULT_WIDTHS.fileSizeBytes,
      valueGetter: (_value, row) => formatBytes(row.fileSizeBytes),
    },
    {
      field: 'originalTitle',
      headerName: t('videos.columns.originalTitle'),
      minWidth: 140,
      width: columnWidthModel.originalTitle ?? COLUMN_DEFAULT_WIDTHS.originalTitle,
      valueGetter: (_value, row) => row.originalTitle ?? '—',
    },
    {
      field: 'plot',
      headerName: t('videos.columns.plot'),
      minWidth: 160,
      width: columnWidthModel.plot ?? COLUMN_DEFAULT_WIDTHS.plot,
      valueGetter: (_value, row) => row.plot ?? '—',
    },
    {
      field: 'scannedAt',
      headerName: t('videos.columns.scannedAt'),
      minWidth: 160,
      width: columnWidthModel.scannedAt ?? COLUMN_DEFAULT_WIDTHS.scannedAt,
      valueGetter: (_value, row) => new Date(row.scannedAt).toLocaleString(),
    },
    {
      field: 'fileModifiedAt',
      headerName: t('videos.columns.fileModifiedAt'),
      minWidth: 160,
      width: columnWidthModel.fileModifiedAt ?? COLUMN_DEFAULT_WIDTHS.fileModifiedAt,
      valueGetter: (_value, row) => row.fileModifiedAt ? new Date(row.fileModifiedAt).toLocaleString() : '—',
    },
  ];

  return (
    <Box sx={{ width: '100%', minWidth: 0, overflowX: 'hidden' }}>
      <Box
        sx={{
          position: 'sticky',
          top: 0,
          zIndex: 10,
          backgroundColor: 'background.default',
          pb: 1,
          mb: 1,
        }}
      >
        <Box
          sx={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: { xs: 'flex-start', md: 'center' },
            flexWrap: 'wrap',
            gap: 1,
            width: '100%',
          }}
        >
          <Typography variant="h5">
            {result ? t('videos.titleWithCount', { count: result.total }) : t('videos.title')}
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            <Button
              startIcon={<ViewColumnIcon />}
              variant="outlined"
              onClick={e => setFieldMenuAnchor(e.currentTarget)}
            >
              {t('videos.selectFields')}
            </Button>
            <TextField
              label={t('videos.search')}
              size="small"
              value={search}
              onChange={e => handleSearchChange(e.target.value)}
              sx={{ minWidth: 220 }}
            />
          </Box>
        </Box>
      </Box>

      <Menu
        anchorEl={fieldMenuAnchor}
        open={Boolean(fieldMenuAnchor)}
        onClose={() => setFieldMenuAnchor(null)}
      >
        {columns.map(col => (
          <Box key={col.field} sx={{ px: 1.5 }}>
            <FormControlLabel
              control={
                <Checkbox
                  checked={columnVisibilityModel[col.field] !== false}
                  onChange={e => setColumnVisibility(col.field, e.target.checked)}
                />
              }
              label={col.headerName}
            />
          </Box>
        ))}
      </Menu>

      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          <Box sx={{ height: 680, width: '100%', minWidth: 0, overflowX: 'hidden' }}>
            <DataGrid
              rows={result?.items ?? []}
              columns={columns}
              loading={loading}
              disableRowSelectionOnClick
              onRowClick={params => navigate(`/videos/${params.row.id}`)}
              getRowId={row => row.id}
              columnVisibilityModel={columnVisibilityModel}
              onColumnVisibilityModelChange={setColumnVisibilityModel}
              onColumnWidthChange={params => {
                setColumnWidthModel(prev => ({
                  ...prev,
                  [params.colDef.field]: Math.round(params.width),
                }));
              }}
              paginationMode="server"
              paginationModel={{ page: page - 1, pageSize: PAGE_SIZE }}
              onPaginationModelChange={handleGridPaginationChange}
              pageSizeOptions={[PAGE_SIZE]}
              rowCount={result?.total ?? 0}
              sortingMode="server"
              sortModel={sortModel}
              onSortModelChange={handleSortModelChange}
              localeText={{
                noRowsLabel: t('videos.empty'),
              }}
              sx={{
                backgroundColor: 'background.paper',
                '& .MuiDataGrid-row': { cursor: 'pointer' },
                '& .MuiDataGrid-main': { overflow: 'auto' },
              }}
            />
          </Box>

          {result && pageCount > 1 && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
              {t('videos.pagination', { page: result.page, totalPages: pageCount })}
            </Typography>
          )}
        </>
      )}
    </Box>
  );
}
