import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Box, Divider, Paper, Switch, ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material';
import { setLanguage } from '../i18n';
import { type ThemeMode, useThemeMode } from '../contexts/ThemeModeContext';

const FILE_MGMT_KEY = 'nfoforge_file_management';

export default function SettingsPage() {
  const { t, i18n } = useTranslation();
  const { themeMode, setThemeMode } = useThemeMode();
  const [fileManagement, setFileManagement] = useState(
    () => localStorage.getItem(FILE_MGMT_KEY) === 'true',
  );

  const handleFileManagementChange = (checked: boolean) => {
    setFileManagement(checked);
    localStorage.setItem(FILE_MGMT_KEY, String(checked));
  };

  const handleLangChange = (_: React.MouseEvent<HTMLElement>, value: 'zh' | 'en' | null) => {
    if (value) setLanguage(value);
  };

  const handleThemeChange = (_: React.MouseEvent<HTMLElement>, value: ThemeMode | null) => {
    if (value) setThemeMode(value);
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

      <Paper sx={{ p: 2, maxWidth: 480, mt: 2 }}>
        <Typography variant="subtitle2" color="text.secondary" gutterBottom>
          {t('settings.theme.title')}
        </Typography>
        <Divider sx={{ mb: 2 }} />
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="body2">{t('settings.theme.subtitle')}</Typography>
          <ToggleButtonGroup
            value={themeMode}
            exclusive
            onChange={handleThemeChange}
            size="small"
          >
            <ToggleButton value="light">{t('settings.theme.light')}</ToggleButton>
            <ToggleButton value="dark">{t('settings.theme.dark')}</ToggleButton>
            <ToggleButton value="system">{t('settings.theme.system')}</ToggleButton>
          </ToggleButtonGroup>
        </Box>
      </Paper>

      <Paper sx={{ p: 2, maxWidth: 480, mt: 2 }}>
        <Typography variant="subtitle2" color="text.secondary" gutterBottom>
          {t('settings.fileManagement.title')}
        </Typography>
        <Divider sx={{ mb: 2 }} />
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="body2">{t('settings.fileManagement.subtitle')}</Typography>
          <Switch
            checked={fileManagement}
            onChange={e => handleFileManagementChange(e.target.checked)}
          />
        </Box>
      </Paper>
    </Box>
  );
}
