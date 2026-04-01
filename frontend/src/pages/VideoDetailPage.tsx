import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import {
  Box, Button, Chip, CircularProgress, Dialog, DialogContent, DialogTitle,
  Divider, Grid, IconButton, Paper, Stack, TextField, Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import ArrowBackIosNewIcon from '@mui/icons-material/ArrowBackIosNew';
import ArrowForwardIosIcon from '@mui/icons-material/ArrowForwardIos';
import CloseIcon from '@mui/icons-material/Close';
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

// ── Upload Dialog ─────────────────────────────────────────────────────────────
interface ImageUploadDialogProps {
  open: boolean;
  label: string;
  onClose: () => void;
  onFile: (file: File) => Promise<boolean>;
  onPathImport: (path: string) => Promise<boolean>;
}

function ImageUploadDialog({ open, label, onClose, onFile, onPathImport }: ImageUploadDialogProps) {
  const { t } = useTranslation();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const dropZoneRef = useRef<HTMLDivElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [pathValue, setPathValue] = useState('');
  const [pathSubmitting, setPathSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      setPathValue('');
      setTimeout(() => dropZoneRef.current?.focus(), 50);
    }
  }, [open]);

  const busy = submitting || pathSubmitting;

  const handleFile = async (f: File) => {
    if (!ALLOWED_IMAGE_TYPES.includes(f.type)) return;
    setSubmitting(true);
    const ok = await onFile(f);
    setSubmitting(false);
    if (ok) onClose();
  };

  const handlePathConfirm = async () => {
    const p = pathValue.trim();
    if (!p) return;
    setPathSubmitting(true);
    const ok = await onPathImport(p);
    setPathSubmitting(false);
    if (ok) onClose();
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', pb: 1 }}>
        <Typography variant="h6">{t('videoDetail.uploadDialog.title', { label })}</Typography>
        <IconButton size="small" onClick={onClose} disabled={busy}>
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        {/* Drop zone */}
        <Box
          ref={dropZoneRef}
          tabIndex={0}
          sx={{
            border: '2px dashed',
            borderColor: dragOver ? 'primary.main' : 'divider',
            borderRadius: 2,
            minHeight: 130,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 0.5,
            cursor: submitting ? 'default' : 'pointer',
            transition: 'border-color 0.2s, background-color 0.2s',
            bgcolor: dragOver ? 'action.hover' : 'transparent',
            outline: 'none',
          }}
          onClick={() => !submitting && fileInputRef.current?.click()}
          onDragOver={e => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={e => {
            e.preventDefault();
            setDragOver(false);
            const f = e.dataTransfer.files[0];
            if (f && !busy) handleFile(f);
          }}
          onPaste={e => {
            const item = Array.from(e.clipboardData.items)
              .find(i => i.kind === 'file' && i.type.startsWith('image/'));
            const f = item?.getAsFile();
            if (f && !busy) handleFile(f);
          }}
        >
          <input
            ref={fileInputRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            style={{ display: 'none' }}
            onChange={e => {
              const f = e.target.files?.[0];
              if (f) handleFile(f);
              e.target.value = '';
            }}
          />
          {submitting ? (
            <CircularProgress size={28} />
          ) : (
            <>
              <Typography variant="body2" color="text.secondary">
                {t('videoDetail.uploadDialog.dropHint')}{' '}
                <Box component="span" sx={{ color: 'primary.main', textDecoration: 'underline' }}>
                  {t('videoDetail.uploadDialog.uploadFile')}
                </Box>
              </Typography>
              <Typography variant="caption" color="text.disabled">
                {t('videoDetail.uploadDialog.pasteHint')}
              </Typography>
            </>
          )}
        </Box>

        {/* Divider */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, my: 2 }}>
          <Divider sx={{ flex: 1 }} />
          <Typography variant="caption" color="text.secondary">
            {t('videoDetail.uploadDialog.or')}
          </Typography>
          <Divider sx={{ flex: 1 }} />
        </Box>

        {/* Path input */}
        <Box sx={{ display: 'flex', gap: 1 }}>
          <TextField
            fullWidth
            size="small"
            placeholder={t('videoDetail.uploadDialog.pathPlaceholder')}
            value={pathValue}
            onChange={e => setPathValue(e.target.value)}
            disabled={busy}
            onKeyDown={e => { if (e.key === 'Enter') handlePathConfirm(); }}
          />
          <Button
            variant="contained"
            size="small"
            onClick={handlePathConfirm}
            disabled={!pathValue.trim() || busy}
            sx={{ whiteSpace: 'nowrap', flexShrink: 0, minWidth: 64 }}
          >
            {pathSubmitting
              ? <CircularProgress size={16} color="inherit" />
              : t('videoDetail.uploadDialog.pathConfirm')}
          </Button>
        </Box>
      </DialogContent>
    </Dialog>
  );
}

// ── Compact image panel ────────────────────────────────────────────────────────
interface CompactImagePanelProps {
  label: string;
  hasImage: boolean;
  imageUrl: string;
  imageAlt: string;
  dragOver: boolean;
  noImageText: string;
  aspectRatio: string;
  onOpenDialog: () => void;
  onDrop: (file: File) => void;
  onDragOver: () => void;
  onDragLeave: () => void;
}

function CompactImagePanel({
  label, hasImage, imageUrl, imageAlt, dragOver,
  noImageText, aspectRatio, onOpenDialog, onDrop, onDragOver, onDragLeave,
}: CompactImagePanelProps) {
  const { t } = useTranslation();
  return (
    <Paper
      sx={{
        display: 'flex',
        flexDirection: 'column',
        width: '100%',
        aspectRatio,
        overflow: 'hidden',
        outline: 'none',
        cursor: 'pointer',
        border: '1px solid',
        borderColor: dragOver ? 'primary.main' : 'divider',
        transition: 'border-color 0.2s',
      }}
      tabIndex={0}
      onClick={onOpenDialog}
      onDragOver={e => { e.preventDefault(); onDragOver(); }}
      onDragLeave={onDragLeave}
      onDrop={e => {
        e.preventDefault();
        onDragLeave();
        const f = e.dataTransfer.files[0];
        if (f) onDrop(f);
      }}
    >
      {/* Header bar: label + action */}
      <Box sx={{
        px: 1.5, py: 0.5, flexShrink: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1,
        bgcolor: 'action.hover', borderBottom: '1px solid', borderColor: 'divider',
        whiteSpace: 'nowrap',
      }}>
        <Typography variant="caption" color="text.secondary" fontWeight={500}>
          {label}
        </Typography>
        {hasImage && (
          <Button
            size="small"
            variant="text"
            sx={{ minWidth: 0, py: 0, px: 0.5, fontSize: '0.7rem', lineHeight: 1.5 }}
            onClick={e => { e.stopPropagation(); onOpenDialog(); }}
          >
            {t('videoDetail.replaceImage')}
          </Button>
        )}
      </Box>
      {/* Image / placeholder */}
      <Box sx={{ flex: 1, minHeight: 0, overflow: 'hidden', bgcolor: 'background.default' }}>
        {hasImage ? (
          <Box
            component="img"
            src={imageUrl}
            alt={imageAlt}
            sx={{ width: '100%', height: '100%', objectFit: 'contain', display: 'block' }}
            onError={e => { (e.currentTarget as HTMLImageElement).style.display = 'none'; }}
          />
        ) : (
          <Box sx={{
            width: '100%', height: '100%',
            display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
            gap: 0.5, p: 1,
          }}>
            <Typography variant="caption" color="text.secondary" sx={{ textAlign: 'center' }}>
              {noImageText}
            </Typography>
          </Box>
        )}
      </Box>
    </Paper>
  );
}

export default function VideoDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const notify = useNotify();
  const { t } = useTranslation();

  const navIds: number[] = (location.state as { ids?: number[] } | null)?.ids ?? [];
  const currentIndex = navIds.indexOf(Number(id));
  const prevId = currentIndex > 0 ? navIds[currentIndex - 1] : null;
  const nextId = currentIndex < navIds.length - 1 ? navIds[currentIndex + 1] : null;

  const navigateTo = (targetId: number) => {
    navigate(`/videos/${targetId}`, { state: { ids: navIds } });
  };

  const [video, setVideo] = useState<VideoFile | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<EditState | null>(null);

  const [posterKey, setPosterKey] = useState(0);
  const [dragOverPoster, setDragOverPoster] = useState(false);

  const [fanartKey, setFanartKey] = useState(0);
  const [dragOverFanart, setDragOverFanart] = useState(false);

  const [dialogTarget, setDialogTarget] = useState<'poster' | 'fanart' | null>(null);

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
    bumpKey: () => void,
    successKey: string,
    failKey: string,
  ): Promise<boolean> => {
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      notify(t('videoDetail.posterInvalidType'), 'error');
      return false;
    }
    if (file.size > 10 * 1024 * 1024) {
      notify(t('videoDetail.posterTooLarge'), 'error');
      return false;
    }
    try {
      const res = await uploader(video!.id, file);
      if (res.success) {
        setVideo(res.data);
        bumpKey();
        notify(t(successKey), 'success');
        return true;
      } else {
        notify((res as { error?: string }).error ?? t(failKey), 'error');
        return false;
      }
    } catch {
      notify(t(failKey), 'error');
      return false;
    }
  };

  const handlePathImport = async (path: string): Promise<boolean> => {
    try {
      const importer = dialogTarget === 'fanart' ? videosApi.importFanartFromPath : videosApi.importPosterFromPath;
      const res = await importer(video!.id, path);
      if (res.success) {
        setVideo(res.data);
        if (dialogTarget === 'fanart') setFanartKey(k => k + 1);
        else setPosterKey(k => k + 1);
        notify(t(dialogTarget === 'fanart' ? 'videoDetail.fanartUploadSuccess' : 'videoDetail.posterUploadSuccess'), 'success');
        return true;
      } else {
        notify(res.error ?? t(dialogTarget === 'fanart' ? 'videoDetail.fanartUploadFailed' : 'videoDetail.posterUploadFailed'), 'error');
        return false;
      }
    } catch (err) {
      notify((err as Error).message, 'error');
      return false;
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
        {navIds.length > 0 && (
          <Stack direction="row" spacing={0.5}>
            <IconButton
              size="small"
              disabled={prevId === null}
              onClick={() => prevId !== null && navigateTo(prevId)}
              title={t('videoDetail.prevFile')}
            >
              <ArrowBackIosNewIcon fontSize="small" />
            </IconButton>
            <IconButton
              size="small"
              disabled={nextId === null}
              onClick={() => nextId !== null && navigateTo(nextId)}
              title={t('videoDetail.nextFile')}
            >
              <ArrowForwardIosIcon fontSize="small" />
            </IconButton>
          </Stack>
        )}
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          {(video.hasNfo && video.title) ? video.title : video.fileName}
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
        {/* ── 左列：FileInfo + Metadata + Actors ── */}
        <Grid size={{ xs: 12, md: 9 }}>
          <Stack spacing={3}>
            {/* 1. FileInfo */}
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
                <Grid size={{ xs: 6, sm: 4 }}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.size')}</Typography>
                  <Typography variant="body2">{formatBytes(video.fileSizeBytes)}</Typography>
                </Grid>
                <Grid size={{ xs: 6, sm: 8 }}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.scannedAt')}</Typography>
                  <Typography variant="body2">{new Date(video.scannedAt).toLocaleString()}</Typography>
                </Grid>
                <Grid size={{ xs: 4, sm: 4 }}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.nfo')}</Typography>
                  <Chip label={video.hasNfo ? t('videoDetail.exists') : t('videoDetail.missing')} color={video.hasNfo ? 'success' : 'default'} size="small" />
                </Grid>
                <Grid size={{ xs: 4, sm: 4 }}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.poster')}</Typography>
                  <Chip label={video.hasPoster ? t('videoDetail.exists') : t('videoDetail.missing')} color={video.hasPoster ? 'success' : 'default'} size="small" />
                </Grid>
                <Grid size={{ xs: 4, sm: 4 }}>
                  <Typography variant="body2" color="text.secondary">{t('videoDetail.fields.fanart')}</Typography>
                  <Chip label={video.hasFanart ? t('videoDetail.exists') : t('videoDetail.missing')} color={video.hasFanart ? 'success' : 'default'} size="small" />
                </Grid>
              </Grid>
            </Paper>

            {/* 2. Metadata */}
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

            {/* 3. Actors */}
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

        {/* ── 右列：Poster (3:4) + Fanart (16:9) ── */}
        <Grid size={{ xs: 12, md: 3 }}>
          <Stack spacing={3}>
            {/* 4. Poster */}
            <CompactImagePanel
              label={t('videoDetail.fields.poster')}
              hasImage={video.hasPoster}
              imageUrl={`${videosApi.getPosterUrl(video.id)}?v=${posterKey}`}
              imageAlt={video.title ?? video.fileName}
              dragOver={dragOverPoster}
              noImageText={t('videoDetail.noPoster')}
              aspectRatio="3/4"
              onOpenDialog={() => setDialogTarget('poster')}
              onDrop={f => handleImageUpload(
                f, videosApi.uploadPoster, () => setPosterKey(k => k + 1),
                'videoDetail.posterUploadSuccess', 'videoDetail.posterUploadFailed',
              )}
              onDragOver={() => setDragOverPoster(true)}
              onDragLeave={() => setDragOverPoster(false)}
            />

            {/* 5. Fanart */}
            <CompactImagePanel
              label={t('videoDetail.fields.fanart')}
              hasImage={video.hasFanart}
              imageUrl={`${videosApi.getFanartUrl(video.id)}?v=${fanartKey}`}
              imageAlt={video.title ?? video.fileName}
              dragOver={dragOverFanart}
              noImageText={t('videoDetail.noFanart')}
              aspectRatio="16/9"
              onOpenDialog={() => setDialogTarget('fanart')}
              onDrop={f => handleImageUpload(
                f, videosApi.uploadFanart, () => setFanartKey(k => k + 1),
                'videoDetail.fanartUploadSuccess', 'videoDetail.fanartUploadFailed',
              )}
              onDragOver={() => setDragOverFanart(true)}
              onDragLeave={() => setDragOverFanart(false)}
            />
          </Stack>
        </Grid>
      </Grid>

      {/* Image upload dialog */}
      <ImageUploadDialog
        open={dialogTarget !== null}
        label={dialogTarget === 'fanart' ? t('videoDetail.fields.fanart') : t('videoDetail.fields.poster')}
        onClose={() => setDialogTarget(null)}
        onFile={async f => handleImageUpload(
          f,
          dialogTarget === 'fanart' ? videosApi.uploadFanart : videosApi.uploadPoster,
          () => { if (dialogTarget === 'fanart') setFanartKey(k => k + 1); else setPosterKey(k => k + 1); },
          dialogTarget === 'fanart' ? 'videoDetail.fanartUploadSuccess' : 'videoDetail.posterUploadSuccess',
          dialogTarget === 'fanart' ? 'videoDetail.fanartUploadFailed' : 'videoDetail.posterUploadFailed',
        )}
        onPathImport={handlePathImport}
      />
    </Box>
  );
}
