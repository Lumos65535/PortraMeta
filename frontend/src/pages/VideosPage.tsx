import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, Button, Checkbox, Chip, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, FormControlLabel, Grid, IconButton, Menu, MenuItem, Radio, RadioGroup, Select, TextField, Typography,
} from '@mui/material';
import { Trash2, Pencil, MoreVertical } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  DataGrid,
  type GridColDef,
  type GridColumnVisibilityModel,
  type GridFilterModel,
  type GridFilterOperator,
  type GridPaginationModel,
  type GridSortModel,
} from '@mui/x-data-grid';
import { videosApi } from '../api/videos';
import type { BatchUpdateRequest, DeleteMode, PagedResult, VideoFile } from '../api/videos';
import { useNotify } from '../contexts/NotifyContext';
import { getFieldLabelKey, getFieldVisibility, isFieldVisible } from '../utils/fieldVisibility';
import type { FieldVisibility } from '../utils/fieldVisibility';

const PAGE_SIZE = 50;
const STORAGE_KEY = 'portrameta_videos_grid_v1';

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
  sortTitle: false,
  plot: false,
  outline: false,
  tagline: false,
  directors: false,
  genres: false,
  tags: false,
  runtime: false,
  mpaa: false,
  premiered: false,
  userRating: false,
  top250: false,
  credits: false,
  countries: false,
  setName: false,
  dateAdded: false,
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
  sortTitle: 260,
  plot: 320,
  outline: 320,
  tagline: 260,
  directors: 200,
  genres: 200,
  tags: 200,
  runtime: 100,
  mpaa: 100,
  premiered: 140,
  userRating: 120,
  top250: 100,
  credits: 200,
  countries: 180,
  setName: 200,
  dateAdded: 180,
  scannedAt: 200,
  fileModifiedAt: 200,
};

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

function BooleanFilterInput(props: { item: { value?: string; field: string; operator: string }; applyValue: (item: { value?: string; field: string; operator: string }) => void }) {
  const { item, applyValue } = props;
  const { t } = useTranslation();
  return (
    <Select
      value={item.value ?? ''}
      onChange={e => applyValue({ ...item, value: e.target.value || undefined })}
      size="small"
      fullWidth
      displayEmpty
      sx={{ mt: 1 }}
    >
      <MenuItem value="">{t('videos.filter.all')}</MenuItem>
      <MenuItem value="true">{t('videos.filter.yes')}</MenuItem>
      <MenuItem value="false">{t('videos.filter.no')}</MenuItem>
    </Select>
  );
}

const booleanFilterOperators: GridFilterOperator[] = [
  {
    label: 'is',
    value: 'is',
    getApplyFilterFn: () => null,
    InputComponent: BooleanFilterInput,
  },
];

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

// ── Batch Edit Dialog ─────────────────────────────────────────────────────────

// Batch-editable field definitions: key, type, and layout hints.
// 'ratings' and 'uniqueIds' are excluded from batch editing (per-video structured data).
interface BatchField {
  key: string;
  type: 'text' | 'number' | 'multiline' | 'comma';
  sm?: number;
  rows?: number;
}

const BATCH_FIELDS: BatchField[] = [
  { key: 'title', type: 'text', sm: 8 },
  { key: 'year', type: 'number', sm: 4 },
  { key: 'originalTitle', type: 'text' },
  { key: 'sortTitle', type: 'text' },
  { key: 'studioName', type: 'text' },
  { key: 'directors', type: 'comma' },
  { key: 'genres', type: 'comma' },
  { key: 'tags', type: 'comma' },
  { key: 'runtime', type: 'number', sm: 4 },
  { key: 'mpaa', type: 'text', sm: 4 },
  { key: 'premiered', type: 'text', sm: 4 },
  { key: 'userRating', type: 'number', sm: 4 },
  { key: 'top250', type: 'number', sm: 4 },
  { key: 'credits', type: 'comma' },
  { key: 'countries', type: 'comma' },
  { key: 'setName', type: 'text' },
  { key: 'dateAdded', type: 'text' },
  { key: 'outline', type: 'multiline', rows: 2 },
  { key: 'tagline', type: 'text' },
  { key: 'plot', type: 'multiline', rows: 3 },
];

function splitComma(s: string): string[] | null {
  const items = s.split(',').map(x => x.trim()).filter(Boolean);
  return items.length > 0 ? items : null;
}

interface BatchEditDialogProps {
  open: boolean;
  count: number;
  onClose: () => void;
  onSubmit: (payload: Omit<BatchUpdateRequest, 'ids'>) => Promise<void>;
}

function BatchEditDialog({ open, count, onClose, onSubmit }: BatchEditDialogProps) {
  const { t } = useTranslation();
  const [fieldVis, setFieldVis] = useState<FieldVisibility>({});
  const emptyForm: Record<string, string> = {};
  for (const f of BATCH_FIELDS) emptyForm[f.key] = '';
  const [form, setForm] = useState<Record<string, string>>(emptyForm);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      setFieldVis(getFieldVisibility());
      const reset: Record<string, string> = {};
      for (const f of BATCH_FIELDS) reset[f.key] = '';
      setForm(reset);
    }
  }, [open]);

  const visibleFields = BATCH_FIELDS.filter(f => isFieldVisible(fieldVis, f.key));
  const hasAnyField = visibleFields.some(f => form[f.key]?.trim());

  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      const payload: Omit<BatchUpdateRequest, 'ids'> = {};
      for (const f of visibleFields) {
        const val = form[f.key]?.trim();
        if (!val) continue;
        if (f.type === 'number') {
          (payload as Record<string, unknown>)[f.key] = parseInt(val, 10) || null;
        } else if (f.type === 'comma') {
          (payload as Record<string, unknown>)[f.key] = splitComma(val);
        } else {
          (payload as Record<string, unknown>)[f.key] = val || null;
        }
      }
      await onSubmit(payload);
    } finally {
      setSubmitting(false);
    }
  };

  const setField = (key: string, value: string) =>
    setForm(p => ({ ...p, [key]: value }));

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t('videos.batchEdit.title', { count })}</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2, mt: 0.5 }}>
          {t('videos.batchEdit.hint')}
        </Typography>
        <Grid container spacing={2}>
          {visibleFields.map(f => (
            <Grid key={f.key} size={{ xs: 12, sm: f.sm ?? 12 }}>
              <TextField
                label={t(getFieldLabelKey(f.key))}
                fullWidth
                size="small"
                type={f.type === 'number' ? 'number' : undefined}
                multiline={f.type === 'multiline'}
                rows={f.rows}
                value={form[f.key] ?? ''}
                onChange={e => setField(f.key, e.target.value)}
                disabled={submitting}
                helperText={f.type === 'comma' ? t('videoDetail.commaSeparatedHint') : undefined}
                placeholder={f.key === 'premiered' ? 'YYYY-MM-DD' : undefined}
              />
            </Grid>
          ))}
          <Grid size={12}>
            <Typography variant="caption" color="warning.main">
              {t('videos.batchEdit.warning')}
            </Typography>
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting}>{t('common.cancel')}</Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={submitting || !hasAnyField}
          startIcon={submitting ? <CircularProgress size={16} /> : <Pencil size={18} />}
        >
          {t('videos.batchEdit.submit', { count })}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Delete Confirm Dialog ─────────────────────────────────────────────────────
interface DeleteConfirmDialogProps {
  open: boolean;
  count: number;
  onClose: () => void;
  onConfirm: (mode: DeleteMode) => Promise<void>;
}

function DeleteConfirmDialog({ open, count, onClose, onConfirm }: DeleteConfirmDialogProps) {
  const { t } = useTranslation();
  const [mode, setMode] = useState<DeleteMode>('Metadata');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) setMode('Metadata');
  }, [open]);

  const handleConfirm = async () => {
    setSubmitting(true);
    try {
      await onConfirm(mode);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t('videos.batchDelete.title', { count })}</DialogTitle>
      <DialogContent>
        <RadioGroup value={mode} onChange={e => setMode(e.target.value as DeleteMode)}>
          <FormControlLabel value="Metadata" control={<Radio />} label={t('videos.batchDelete.modeMetadata')} disabled={submitting} />
          <FormControlLabel value="Video" control={<Radio />} label={t('videos.batchDelete.modeVideo')} disabled={submitting} />
          <FormControlLabel value="All" control={<Radio />} label={t('videos.batchDelete.modeAll')} disabled={submitting} />
        </RadioGroup>
        <Typography variant="caption" color="error" sx={{ display: 'block', mt: 1.5 }}>
          {t('videos.batchDelete.warning')}
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting}>{t('common.cancel')}</Button>
        <Button
          variant="contained"
          color="error"
          onClick={handleConfirm}
          disabled={submitting}
          startIcon={submitting ? <CircularProgress size={16} /> : <Trash2 size={18} />}
        >
          {t('videos.batchDelete.submit')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

const FILE_MGMT_KEY = 'portrameta_file_management';

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
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [batchDialogOpen, setBatchDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const fileManagementEnabled = localStorage.getItem(FILE_MGMT_KEY) === 'true';

  const [columnVisibilityModel, setColumnVisibilityModel] =
    useState<GridColumnVisibilityModel>(() => readGridSettings().visibilityModel);
  const [columnWidthModel, setColumnWidthModel] =
    useState<Record<string, number>>(() => readGridSettings().widthModel);
  const [filterModel, setFilterModel] = useState<GridFilterModel>({ items: [] });
  const filterModelRef = useRef<GridFilterModel>({ items: [] });

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
      const filters = filterModelRef.current;
      const boolFilter = (field: string): boolean | undefined => {
        const item = filters.items.find(i => i.field === field);
        if (!item?.value) return undefined;
        return item.value === 'true';
      };
      const res = await videosApi.getAll({
        search: searchValue || undefined,
        page: pageNum,
        page_size: PAGE_SIZE,
        sort_by: sortField,
        sort_desc: sortDir === 'desc' ? true : undefined,
        has_nfo: boolFilter('hasNfo'),
        has_poster: boolFilter('hasPoster'),
        has_fanart: boolFilter('hasFanart'),
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

  const handleFilterModelChange = (model: GridFilterModel) => {
    setFilterModel(model);
    filterModelRef.current = model;
    setPage(1);
    load(search, 1, sortModel);
  };

  const setColumnVisibility = (field: string, checked: boolean) => {
    setColumnVisibilityModel(prev => ({
      ...prev,
      [field]: checked,
    }));
  };

  const handleBatchEdit = async (payload: Omit<BatchUpdateRequest, 'ids'>) => {
    try {
      const res = await videosApi.batchUpdate({ ids: selectedIds, ...payload });
      if (res.success) {
        const { updated, failed } = res.data;
        if (failed.length === 0) {
          notify(t('videos.batchEdit.successMsg', { updated }), 'success');
        } else {
          notify(t('videos.batchEdit.partialMsg', { updated, failed: failed.length }), 'warning');
        }
        setSelectedIds([]);
        setBatchDialogOpen(false);
        load(search, page, sortModel);
      } else {
        notify(res.error ?? t('videos.batchEdit.failedMsg', { count: selectedIds.length }), 'error');
      }
    } catch (err) {
      notify((err as Error).message, 'error');
    }
  };

  const handleBatchDelete = async (mode: DeleteMode) => {
    try {
      const res = await videosApi.batchDelete({ ids: selectedIds, mode });
      if (res.success) {
        const { deleted, failed } = res.data;
        if (failed.length === 0) {
          notify(t('videos.batchDelete.successMsg', { deleted }), 'success');
        } else {
          notify(t('videos.batchDelete.partialMsg', { deleted, failed: failed.length }), 'warning');
        }
        setSelectedIds([]);
        setDeleteDialogOpen(false);
        load(search, page, sortModel);
      } else {
        notify(res.error ?? t('videos.batchDelete.failedMsg', { count: selectedIds.length }), 'error');
      }
    } catch (err) {
      notify((err as Error).message, 'error');
    }
  };

  const pageCount = result ? Math.ceil(result.total / PAGE_SIZE) : 0;

  const dataColumns: GridColDef<VideoFile>[] = [
    {
      field: 'fileName',
      headerName: t('videos.columns.filename'),
      minWidth: 180,
      width: columnWidthModel.fileName ?? COLUMN_DEFAULT_WIDTHS.fileName,
      filterable: false,
    },
    {
      field: 'title',
      headerName: t('videos.columns.title'),
      minWidth: 160,
      width: columnWidthModel.title ?? COLUMN_DEFAULT_WIDTHS.title,
      filterable: false,
      valueGetter: (_value, row) => row.title ?? '—',
    },
    {
      field: 'year',
      headerName: t('videos.columns.year'),
      minWidth: 80,
      width: columnWidthModel.year ?? COLUMN_DEFAULT_WIDTHS.year,
      filterable: false,
      valueGetter: (_value, row) => row.year ?? '—',
    },
    {
      field: 'studioName',
      headerName: t('videos.columns.studio'),
      minWidth: 120,
      width: columnWidthModel.studioName ?? COLUMN_DEFAULT_WIDTHS.studioName,
      filterable: false,
      valueGetter: (_value, row) => row.studioName ?? '—',
    },
    {
      field: 'hasNfo',
      headerName: t('videos.columns.nfo'),
      minWidth: 80,
      width: columnWidthModel.hasNfo ?? COLUMN_DEFAULT_WIDTHS.hasNfo,
      sortable: false,
      filterOperators: booleanFilterOperators,
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
      filterOperators: booleanFilterOperators,
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
      filterOperators: booleanFilterOperators,
      renderCell: params => (
        <Chip label={params.row.hasFanart ? '✓' : '✗'} color={params.row.hasFanart ? 'success' : 'default'} size="small" />
      ),
    },
    {
      field: 'filePath',
      headerName: t('videos.columns.path'),
      minWidth: 220,
      width: columnWidthModel.filePath ?? COLUMN_DEFAULT_WIDTHS.filePath,
      filterable: false,
    },
    {
      field: 'fileSizeBytes',
      headerName: t('videos.columns.size'),
      minWidth: 110,
      width: columnWidthModel.fileSizeBytes ?? COLUMN_DEFAULT_WIDTHS.fileSizeBytes,
      filterable: false,
      valueGetter: (_value, row) => formatBytes(row.fileSizeBytes),
    },
    {
      field: 'originalTitle',
      headerName: t('videos.columns.originalTitle'),
      minWidth: 140,
      width: columnWidthModel.originalTitle ?? COLUMN_DEFAULT_WIDTHS.originalTitle,
      filterable: false,
      valueGetter: (_value, row) => row.originalTitle ?? '—',
    },
    {
      field: 'plot',
      headerName: t('videos.columns.plot'),
      minWidth: 160,
      width: columnWidthModel.plot ?? COLUMN_DEFAULT_WIDTHS.plot,
      filterable: false,
      valueGetter: (_value, row) => row.plot ?? '—',
    },
    {
      field: 'scannedAt',
      headerName: t('videos.columns.scannedAt'),
      minWidth: 160,
      width: columnWidthModel.scannedAt ?? COLUMN_DEFAULT_WIDTHS.scannedAt,
      filterable: false,
      valueGetter: (_value, row) => new Date(row.scannedAt).toLocaleString(),
    },
    {
      field: 'fileModifiedAt',
      headerName: t('videos.columns.fileModifiedAt'),
      minWidth: 160,
      width: columnWidthModel.fileModifiedAt ?? COLUMN_DEFAULT_WIDTHS.fileModifiedAt,
      filterable: false,
      valueGetter: (_value, row) => row.fileModifiedAt ? new Date(row.fileModifiedAt).toLocaleString() : '—',
    },
    {
      field: 'sortTitle',
      headerName: t('videos.columns.sortTitle'),
      minWidth: 140,
      width: columnWidthModel.sortTitle ?? COLUMN_DEFAULT_WIDTHS.sortTitle,
      filterable: false,
      valueGetter: (_value, row) => row.sortTitle ?? '—',
    },
    {
      field: 'outline',
      headerName: t('videos.columns.outline'),
      minWidth: 160,
      width: columnWidthModel.outline ?? COLUMN_DEFAULT_WIDTHS.outline,
      filterable: false,
      valueGetter: (_value, row) => row.outline ?? '—',
    },
    {
      field: 'tagline',
      headerName: t('videos.columns.tagline'),
      minWidth: 140,
      width: columnWidthModel.tagline ?? COLUMN_DEFAULT_WIDTHS.tagline,
      filterable: false,
      valueGetter: (_value, row) => row.tagline ?? '—',
    },
    {
      field: 'directors',
      headerName: t('videos.columns.directors'),
      minWidth: 120,
      width: columnWidthModel.directors ?? COLUMN_DEFAULT_WIDTHS.directors,
      filterable: false,
      valueGetter: (_value, row) => row.directors?.join(', ') ?? '—',
    },
    {
      field: 'genres',
      headerName: t('videos.columns.genres'),
      minWidth: 120,
      width: columnWidthModel.genres ?? COLUMN_DEFAULT_WIDTHS.genres,
      filterable: false,
      valueGetter: (_value, row) => row.genres?.join(', ') ?? '—',
    },
    {
      field: 'tags',
      headerName: t('videos.columns.tags'),
      minWidth: 120,
      width: columnWidthModel.tags ?? COLUMN_DEFAULT_WIDTHS.tags,
      filterable: false,
      valueGetter: (_value, row) => row.tags?.join(', ') ?? '—',
    },
    {
      field: 'runtime',
      headerName: t('videos.columns.runtime'),
      minWidth: 80,
      width: columnWidthModel.runtime ?? COLUMN_DEFAULT_WIDTHS.runtime,
      filterable: false,
      valueGetter: (_value, row) => row.runtime != null ? `${row.runtime} min` : '—',
    },
    {
      field: 'mpaa',
      headerName: t('videos.columns.mpaa'),
      minWidth: 80,
      width: columnWidthModel.mpaa ?? COLUMN_DEFAULT_WIDTHS.mpaa,
      filterable: false,
      valueGetter: (_value, row) => row.mpaa ?? '—',
    },
    {
      field: 'premiered',
      headerName: t('videos.columns.premiered'),
      minWidth: 120,
      width: columnWidthModel.premiered ?? COLUMN_DEFAULT_WIDTHS.premiered,
      filterable: false,
      valueGetter: (_value, row) => row.premiered ?? '—',
    },
    {
      field: 'userRating',
      headerName: t('videos.columns.userRating'),
      minWidth: 100,
      width: columnWidthModel.userRating ?? COLUMN_DEFAULT_WIDTHS.userRating,
      filterable: false,
      valueGetter: (_value, row) => row.userRating ?? '—',
    },
    {
      field: 'top250',
      headerName: t('videos.columns.top250'),
      minWidth: 80,
      width: columnWidthModel.top250 ?? COLUMN_DEFAULT_WIDTHS.top250,
      filterable: false,
      valueGetter: (_value, row) => row.top250 ?? '—',
    },
    {
      field: 'credits',
      headerName: t('videos.columns.credits'),
      minWidth: 120,
      width: columnWidthModel.credits ?? COLUMN_DEFAULT_WIDTHS.credits,
      filterable: false,
      valueGetter: (_value, row) => row.credits?.join(', ') ?? '—',
    },
    {
      field: 'countries',
      headerName: t('videos.columns.countries'),
      minWidth: 120,
      width: columnWidthModel.countries ?? COLUMN_DEFAULT_WIDTHS.countries,
      filterable: false,
      valueGetter: (_value, row) => row.countries?.join(', ') ?? '—',
    },
    {
      field: 'setName',
      headerName: t('videos.columns.setName'),
      minWidth: 120,
      width: columnWidthModel.setName ?? COLUMN_DEFAULT_WIDTHS.setName,
      filterable: false,
      valueGetter: (_value, row) => row.setName ?? '—',
    },
    {
      field: 'dateAdded',
      headerName: t('videos.columns.dateAdded'),
      minWidth: 120,
      width: columnWidthModel.dateAdded ?? COLUMN_DEFAULT_WIDTHS.dateAdded,
      filterable: false,
      valueGetter: (_value, row) => row.dateAdded ?? '—',
    },
  ];

  const columns: GridColDef<VideoFile>[] = dataColumns;

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
            {selectedIds.length > 0 && (
              <>
                <Button
                  variant="contained"
                  startIcon={<Pencil size={18} />}
                  onClick={() => setBatchDialogOpen(true)}
                >
                  {t('videos.batchEdit.button')}（{selectedIds.length}）
                </Button>
                {fileManagementEnabled && (
                  <Button
                    variant="contained"
                    color="error"
                    startIcon={<Trash2 size={18} />}
                    onClick={() => setDeleteDialogOpen(true)}
                  >
                    {t('videos.batchDelete.button')}（{selectedIds.length}）
                  </Button>
                )}
              </>
            )}
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
        {dataColumns.map(col => (
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
          <Box sx={{ height: 'calc(100vh - 180px)', minHeight: 400, width: '100%', minWidth: 0, overflowX: 'hidden', position: 'relative' }}>
            <IconButton
              size="small"
              onClick={e => setFieldMenuAnchor(e.currentTarget)}
              sx={{
                position: 'absolute',
                top: 4,
                right: 4,
                zIndex: 2,
                backgroundColor: 'background.paper',
                '&:hover': { backgroundColor: 'action.hover' },
              }}
            >
              <MoreVertical size={18} />
            </IconButton>
            <DataGrid
              rows={result?.items ?? []}
              columns={columns}
              loading={loading}
              checkboxSelection
              disableRowSelectionOnClick
              rowSelectionModel={{ type: 'include', ids: new Set(selectedIds) }}
              onRowSelectionModelChange={model => {
                if (model.type === 'include') {
                  setSelectedIds([...model.ids] as number[]);
                } else {
                  // 'exclude' means all rows selected except those in ids
                  const excludedIds = new Set(model.ids);
                  setSelectedIds((result?.items.map(r => r.id) ?? []).filter(id => !excludedIds.has(id)));
                }
              }}
              onRowClick={params => navigate(`/videos/${params.row.id}`, {
            state: {
              ids: result?.items.map(r => r.id) ?? [],
              navPage: page,
              navSearch: search || undefined,
              navSortBy: sortModel[0]?.field,
              navSortDesc: sortModel[0]?.sort === 'desc' ? true : undefined,
              navTotal: result?.total ?? 0,
            },
          })}
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
              filterMode="server"
              filterModel={filterModel}
              onFilterModelChange={handleFilterModelChange}
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

      <BatchEditDialog
        open={batchDialogOpen}
        count={selectedIds.length}
        onClose={() => setBatchDialogOpen(false)}
        onSubmit={handleBatchEdit}
      />

      <DeleteConfirmDialog
        open={deleteDialogOpen}
        count={selectedIds.length}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={handleBatchDelete}
      />
    </Box>
  );
}
