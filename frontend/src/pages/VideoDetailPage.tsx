import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Box, Button, Chip, CircularProgress, Divider, Grid, IconButton,
  Paper, Stack, TextField, Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import EditIcon from '@mui/icons-material/Edit';
import SaveIcon from '@mui/icons-material/Save';
import CancelIcon from '@mui/icons-material/Cancel';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
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

const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];

interface ActorEditState {
  name: string;
  role: string;
}

interface EditState {
  title: string;
  originalTitle: string;
  year: string;
  plot: string;
  studioName: string;
  actors: ActorEditState[];
}

function toEditState(v: VideoFile): EditState {
  return {
    title: v.title ?? '',
    originalTitle: v.originalTitle ?? '',
    year: v.year?.toString() ?? '',
    plot: v.plot ?? '',
    studioName: v.studioName ?? '',
    actors: (v.actors ?? []).map(a => ({ name: a.name, role: a.role ?? '' })),
  };
}

// Reusable upload panel (poster or fanart)
interface ImagePanelProps {
  label: string;
  hasImage: boolean;
  imageUrl: string;
  imageAlt: string;
  uploading: boolean;
  dragOver: boolean;
  uploadHint: string;
  noImageText: string;
  inputRef: React.RefObject<HTMLInputElement | null>;
  onUpload: (file: File) => void;
  onDragOver: () => void;
  onDragLeave: () => void;
}

function ImageUploadPanel({
  label, hasImage, imageUrl, imageAlt, uploading, dragOver,
  uploadHint, noImageText, inputRef, onUpload, onDragOver, onDragLeave,
}: ImagePanelProps) {
  const { t } = useTranslation();
  return (
    <Paper
      sx={{
        p: 2,
        cursor: uploading ? 'default' : 'pointer',
        border: '2px solid',
        borderColor: dragOver ? 'primary.main' : 'transparent',
        transition: 'border-color 0.2s',
        outline: 'none',
      }}
      tabIndex={0}
      onClick={() => !uploading && inputRef.current?.click()}
      onDragOver={e => { e.preventDefault(); onDragOver(); }}
      onDragLeave={onDragLeave}
      onDrop={e => {
        e.preventDefault();
        onDragLeave();
        const f = e.dataTransfer.files[0];
        if (f) onUpload(f);
      }}
      onPaste={e => {
        const item = Array.from(e.clipboardData.items)
          .find(i => i.kind === 'file' && i.type.startsWith('image/'));
        const f = item?.getAsFile();
        if (f) onUpload(f);
      }}
    >
      <Typography variant="subtitle2" color="text.secondary" gutterBottom>
        {label}
      </Typography>
      <Divider sx={{ mb: 1.5 }} />
      {hasImage ? (
        <Box
          component="img"
          src={imageUrl}
          alt={imageAlt}
          sx={{
            width: '100%',
            borderRadius: 1,
            display: 'block',
            mb: 1.5,
            objectFit: 'cover',
            maxHeight: 280,
          }}
          onError={e => { (e.currentTarget as HTMLImageElement).style.display = 'none'; }}
          onClick={e => e.stopPropagation()}
        />
      ) : (
        <Box
          sx={{
            height: 140,
            bgcolor: 'action.hover',
            borderRadius: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            mb: 1.5,
          }}
        >
          <Typography variant="body2" color="text.secondary">{noImageText}</Typography>
        </Box>
      )}
      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        style={{ display: 'none' }}
        onChange={e => {
          const f = e.target.files?.[0];
          if (f) onUpload(f);
          e.target.value = '';
        }}
      />
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 1 }}>
        {uploading ? (
          <>
            <CircularProgress size={14} />
            <Typography variant="caption">{t('videoDetail.uploading')}</Typography>
          </>
        ) : (
          <Typography variant="caption" color="text.secondary">{uploadHint}</Typography>
        )}
      </Box>
    </Paper>
  );
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

  const [posterKey, setPosterKey] = useState(0);
  const [uploadingPoster, setUploadingPoster] = useState(false);
  const [dragOverPoster, setDragOverPoster] = useState(false);
  const posterInputRef = useRef<HTMLInputElement>(null);

  const [fanartKey, setFanartKey] = useState(0);
  const [uploadingFanart, setUploadingFanart] = useState(false);
  const [dragOverFanart, setDragOverFanart] = useState(false);
  const fanartInputRef = useRef<HTMLInputElement>(null);

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
        actors: form.actors
          .filter(a => a.name.trim())
          .map((a, i) => ({ name: a.name.trim(), role: a.role.trim() || null, order: i })),
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

  const handleImageUpload = async (
    file: File,
    uploader: (id: number, file: File) => Promise<{ success: boolean; data: VideoFile; error?: string }>,
    setUploading: (v: boolean) => void,
    bumpKey: () => void,
    successKey: string,
    failKey: string,
  ) => {
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      notify(t('videoDetail.posterInvalidType'), 'error');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      notify(t('videoDetail.posterTooLarge'), 'error');
      return;
    }
    setUploading(true);
    try {
      const res = await uploader(video!.id, file);
      if (res.success) {
        setVideo(res.data);
        bumpKey();
        notify(t(successKey), 'success');
      } else {
        notify((res as { error?: string }).error ?? t(failKey), 'error');
      }
    } catch {
      notify(t(failKey), 'error');
    } finally {
      setUploading(false);
    }
  };

  const field = (key: Exclude<keyof EditState, 'actors'>) => ({
    value: form?.[key] ?? '',
    onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
      setForm(prev => prev ? { ...prev, [key]: e.target.value } : prev),
    disabled: saving,
  });

  const setActorField = (idx: number, field: keyof ActorEditState, value: string) =>
    setForm(prev => prev ? {
      ...prev,
      actors: prev.actors.map((a, i) => i === idx ? { ...a, [field]: value } : a),
    } : prev);

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

        {/* Right column: Poster + Fanart + Actors */}
        <Grid size={{ xs: 12, md: 4 }}>
          <Stack spacing={2}>
            {/* Poster Panel */}
            <ImageUploadPanel
              label={t('videoDetail.fields.poster')}
              hasImage={video.hasPoster}
              imageUrl={`${videosApi.getPosterUrl(video.id)}?v=${posterKey}`}
              imageAlt={video.title ?? video.fileName}
              uploading={uploadingPoster}
              dragOver={dragOverPoster}
              uploadHint={t('videoDetail.uploadPoster')}
              noImageText={t('videoDetail.noPoster')}
              inputRef={posterInputRef}
              onUpload={f => handleImageUpload(
                f, videosApi.uploadPoster, setUploadingPoster,
                () => setPosterKey(k => k + 1),
                'videoDetail.posterUploadSuccess', 'videoDetail.posterUploadFailed',
              )}
              onDragOver={() => setDragOverPoster(true)}
              onDragLeave={() => setDragOverPoster(false)}
            />

            {/* Fanart Panel */}
            <ImageUploadPanel
              label={t('videoDetail.fields.fanart')}
              hasImage={video.hasFanart}
              imageUrl={`${videosApi.getFanartUrl(video.id)}?v=${fanartKey}`}
              imageAlt={video.title ?? video.fileName}
              uploading={uploadingFanart}
              dragOver={dragOverFanart}
              uploadHint={t('videoDetail.uploadFanart')}
              noImageText={t('videoDetail.noFanart')}
              inputRef={fanartInputRef}
              onUpload={f => handleImageUpload(
                f, videosApi.uploadFanart, setUploadingFanart,
                () => setFanartKey(k => k + 1),
                'videoDetail.fanartUploadSuccess', 'videoDetail.fanartUploadFailed',
              )}
              onDragOver={() => setDragOverFanart(true)}
              onDragLeave={() => setDragOverFanart(false)}
            />

            {/* Actors Panel */}
            <Paper sx={{ p: 2 }}>
              <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                {t('videoDetail.actors')}
              </Typography>
              <Divider sx={{ mb: 1.5 }} />
              {editing && form ? (
                <Stack spacing={1.5}>
                  {form.actors.map((actor, idx) => (
                    <Box key={idx} sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                      <TextField
                        size="small"
                        label={t('videoDetail.actorName')}
                        value={actor.name}
                        onChange={e => setActorField(idx, 'name', e.target.value)}
                        disabled={saving}
                        sx={{ flex: 2 }}
                      />
                      <TextField
                        size="small"
                        label={t('videoDetail.actorRole')}
                        value={actor.role}
                        onChange={e => setActorField(idx, 'role', e.target.value)}
                        disabled={saving}
                        sx={{ flex: 2 }}
                      />
                      <IconButton
                        size="small"
                        onClick={() => setForm(prev => prev ? {
                          ...prev,
                          actors: prev.actors.filter((_, i) => i !== idx),
                        } : prev)}
                        disabled={saving}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Box>
                  ))}
                  <Button
                    size="small"
                    startIcon={<AddIcon />}
                    onClick={() => setForm(prev => prev ? {
                      ...prev,
                      actors: [...prev.actors, { name: '', role: '' }],
                    } : prev)}
                    disabled={saving}
                  >
                    {t('videoDetail.addActor')}
                  </Button>
                </Stack>
              ) : (
                video.actors && video.actors.length > 0 ? (
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
                )
              )}
            </Paper>
          </Stack>
        </Grid>
      </Grid>
    </Box>
  );
}
