import { useTranslation } from 'react-i18next';
import {
  Box, Divider, Paper, ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material';
import { setLanguage } from '../i18n';

export default function SettingsPage() {
  const { t, i18n } = useTranslation();

  const handleLangChange = (_: React.MouseEvent<HTMLElement>, value: 'zh' | 'en' | null) => {
    if (value) setLanguage(value);
  };

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3 }}>{t('settings.title')}</Typography>

      <Paper sx={{ p: 2, maxWidth: 480 }}>
        <Typography variant="subtitle2" color="text.secondary" gutterBottom>
          {t('settings.language.title')}
        </Typography>
        <Divider sx={{ mb: 2 }} />
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="body2">{t('settings.language.subtitle')}</Typography>
          <ToggleButtonGroup
            value={i18n.language}
            exclusive
            onChange={handleLangChange}
            size="small"
          >
            <ToggleButton value="zh">{t('settings.language.zh')}</ToggleButton>
            <ToggleButton value="en">{t('settings.language.en')}</ToggleButton>
          </ToggleButtonGroup>
        </Box>
      </Paper>
    </Box>
  );
}
