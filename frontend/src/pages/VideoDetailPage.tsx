import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Box, Button, Chip, CircularProgress, Divider, Grid,
  Paper, Stack, TextField, Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import EditIcon from '@mui/icons-material/Edit';
import SaveIcon from '@mui/icons-material/Save';
import CancelIcon from '@mui/icons-material/Cancel';
import { useTranslation } from 'react-i18next';
import { videosApi } from '../api/videos';
import type { VideoFile } from '../api/videos';
import { useNotify } from '../contexts/NotifyContext';

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

interface EditState {
  title: string;
  originalTitle: string;
  year: string;
  plot: string;
  studioName: string;
}

function toEditState(v: VideoFile): EditState {
  return {
    title: v.title ?? '',
    originalTitle: v.originalTitle ?? '',
    year: v.year?.toString() ?? '',
    plot: v.plot ?? '',
    studioName: v.studioName ?? '',
  };
}

export default function VideoDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const notify = useNotify();
  const { t } = useTranslation();
  const [video, setVideo] = useState<VideoFile | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<EditState | null>(null);

  useEffect(() => {
    if (!id) return;
    videosApi.getById(Number(id))
      .then(res => {
        if (res.success) {
          setVideo(res.data);
          setForm(toEditState(res.data));
        } else {
          notify(res.error ?? t('videoDetail.loadFailed'), 'error');
        }
      })
      .catch(err => notify((err as Error).message, 'error'))
      .finally(() => setLoading(false));
  }, [id]);

  const handleEdit = () => {
    if (video) setForm(toEditState(video));
    setEditing(true);
  };

  const handleCancel = () => {
    setEditing(false);
    if (video) setForm(toEditState(video));
  };

  const handleSave = async () => {
    if (!form || !video) return;
    setSaving(true);
    try {
      const res = await videosApi.update(video.id, {
        title: form.title || null,
        originalTitle: form.originalTitle || null,
        year: form.year ? parseInt(form.year, 10) : null,
        plot: form.plot || null,
        studioName: form.studioName || null,
      });
      if (res.success) {
        setVideo(res.data);
        setForm(toEditState(res.data));
        setEditing(false);
        notify(t('videoDetail.saveSuccess'), 'success');
      } else {
        notify(res.error ?? t('videoDetail.saveFailed'), 'error');
      }
    } catch (err) {
      notify((err as Error).message, 'error');
    } finally {
      setSaving(false);
    }
  };

  const field = (key: keyof EditState) => ({
    value: form?.[key] ?? '',
    onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
      setForm(prev => prev ? { ...prev, [key]: e.target.value } : prev),
    disabled: saving,
  });

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!video) return null;

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 3 }}>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/videos')}>
          {t('videoDetail.backToList')}
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          {video.title ?? video.fileName}
        </Typography>
        {!editing ? (
          <Button variant="contained" startIcon={<EditIcon />} onClick={handleEdit}>
            {t('videoDetail.edit')}
          </Button>
        ) : (
          <Stack direction="row" spacing={1}>
            <Button
              variant="contained"
              startIcon={saving ? <CircularProgress size={16} /> : <SaveIcon />}
              onClick={handleSave}
              disabled={saving}
            >
              {t('videoDetail.save')}
            </Button>
            <Button startIcon={<CancelIcon />} onClick={handleCancel} disabled={saving}>
              {t('videoDetail.cancel')}
            </Button>
          </Stack>
        )}
      </Box>

      <Grid container spacing={3}>
        {/* File info */}
        <Grid size={12}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle2" color="text.secondary" gutterBottom>
              {t('videoDetail.fileInfo')}
            </Typography>
            <Divider sx={{ mb: 1.5 }} />
            <Grid container spacing={1}>
              <Grid size={12}>
                <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.path')}</Typography>
                <Typography variant="body2" sx={{ wordBreak: 'break-all' }}>{video.filePath}</Typography>
              </Grid>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.size')}</Typography>
                <Typography variant="body2">{formatBytes(video.fileSizeBytes)}</Typography>
              </Grid>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.scannedAt')}</Typography>
                <Typography variant="body2">{new Date(video.scannedAt).toLocaleString()}</Typography>
              </Grid>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.nfo')}</Typography>
                <Chip
                  label={video.hasNfo ? t('videoDetail.exists') : t('videoDetail.missing')}
                  color={video.hasNfo ? 'success' : 'default'}
                  size="small"
                />
              </Grid>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.poster')}</Typography>
                <Chip
                  label={video.hasPoster ? t('videoDetail.exists') : t('videoDetail.missing')}
                  color={video.hasPoster ? 'success' : 'default'}
                  size="small"
                />
              </Grid>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.fanart')}</Typography>
                <Chip
                  label={video.hasFanart ? t('videoDetail.exists') : t('videoDetail.missing')}
                  color={video.hasFanart ? 'success' : 'default'}
                  size="small"
                />
              </Grid>
            </Grid>
          </Paper>
        </Grid>

        {/* Metadata */}
        <Grid size={{ xs: 12, md: 8 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle2" color="text.secondary" gutterBottom>
              {t('videoDetail.metadata')}
            </Typography>
            <Divider sx={{ mb: 2 }} />
            {editing && form ? (
              <Grid container spacing={2}>
                <Grid size={{ xs: 12, sm: 8 }}>
                  <TextField label={t('videoDetail.fields.title')} fullWidth size="small" {...field('title')} />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <TextField label={t('videoDetail.fields.year')} fullWidth size="small" type="number" {...field('year')} />
                </Grid>
                <Grid size={12}>
                  <TextField label={t('videoDetail.fields.originalTitle')} fullWidth size="small" {...field('originalTitle')} />
                </Grid>
                <Grid size={12}>
                  <TextField label={t('videoDetail.fields.studio')} fullWidth size="small" {...field('studioName')} />
                </Grid>
                <Grid size={12}>
                  <TextField label={t('videoDetail.fields.plot')} fullWidth multiline rows={4} {...field('plot')} />
                </Grid>
              </Grid>
            ) : (
              <Grid container spacing={1.5}>
                <Grid size={{ xs: 12, sm: 8 }}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.title')}</Typography>
                  <Typography>{video.title ?? '—'}</Typography>
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.year')}</Typography>
                  <Typography>{video.year ?? '—'}</Typography>
                </Grid>
                <Grid size={12}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.originalTitle')}</Typography>
                  <Typography>{video.originalTitle ?? '—'}</Typography>
                </Grid>
                <Grid size={12}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.studio')}</Typography>
                  <Typography>{video.studioName ?? '—'}</Typography>
                </Grid>
                <Grid size={12}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.plot')}</Typography>
                  <Typography sx={{ whiteSpace: 'pre-wrap' }}>{video.plot ?? '—'}</Typography>
                </Grid>
              </Grid>
            )}
          </Paper>
        </Grid>

        {/* Actors */}
        <Grid size={{ xs: 12, md: 4 }}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle2" color="text.secondary" gutterBottom>
              {t('videoDetail.actors')}
            </Typography>
            <Divider sx={{ mb: 1.5 }} />
            {video.actors && video.actors.length > 0 ? (
              <Stack spacing={1}>
                {video.actors.map(actor => (
                  <Box key={actor.id}>
                    <Typography variant="body2">{actor.name}</Typography>
                    {actor.role && (
                      <Typography variant="caption" color="text.secondary">{actor.role}</Typography>
                    )}
                  </Box>
                ))}
              </Stack>
            ) : (
              <Typography variant="body2" color="text.secondary">{t('videoDetail.noActors')}</Typography>
            )}
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
}
