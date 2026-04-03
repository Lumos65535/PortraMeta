import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Box, Button, Divider, Paper, Switch, ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material';
import { setLanguage } from '../i18n';
import { type ThemeMode, useThemeMode } from '../contexts/ThemeModeContext';
import { FIELD_DEFS, FIELD_VISIBILITY_KEY, getFieldLabelKey } from '../utils/fieldVisibility';
import type { FieldDef } from '../utils/fieldVisibility';

const FILE_MGMT_KEY = 'portrameta_file_management';

function loadFieldVisibility(): Record<string, boolean> {
  try {
    const stored = localStorage.getItem(FIELD_VISIBILITY_KEY);
    if (stored) return JSON.parse(stored);
  } catch { /* ignore */ }
  return {};
}

export default function SettingsPage() {
  const { t, i18n } = useTranslation();
  const { themeMode, setThemeMode } = useThemeMode();
  const [fileManagement, setFileManagement] = useState(
    () => localStorage.getItem(FILE_MGMT_KEY) === 'true',
  );
  const [fieldOverrides, setFieldOverrides] = useState<Record<string, boolean>>(loadFieldVisibility);

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

  const handleFieldToggle = (key: string, checked: boolean) => {
    const updated = { ...fieldOverrides, [key]: checked };
    setFieldOverrides(updated);
    localStorage.setItem(FIELD_VISIBILITY_KEY, JSON.stringify(updated));
  };

  const handleResetDefaults = () => {
    setFieldOverrides({});
    localStorage.removeItem(FIELD_VISIBILITY_KEY);
  };

  const isVisible = (def: FieldDef) => fieldOverrides[def.key] ?? def.defaultVisible;

  const renderFieldGroup = (tier: 1 | 2 | 3, label: string) => {
    const fields = FIELD_DEFS.filter(f => f.tier === tier);
    return (
      <Box sx={{ mb: 2 }}>
        <Typography variant="caption" color="text.secondary" sx={{ mb: 0.5, display: 'block' }}>
          {label}
        </Typography>
        {fields.map(def => (
          <Box key={def.key} sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', py: 0.25 }}>
            <Typography variant="body2">{t(getFieldLabelKey(def.key))}</Typography>
            <Switch
              size="small"
              checked={isVisible(def)}
              onChange={e => handleFieldToggle(def.key, e.target.checked)}
            />
          </Box>
        ))}
      </Box>
    );
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

      <Paper sx={{ p: 2, maxWidth: 480, mt: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 0.5 }}>
          <Typography variant="subtitle2" color="text.secondary">
            {t('settings.detailFields.title')}
          </Typography>
          <Button size="small" onClick={handleResetDefaults}>
            {t('settings.detailFields.resetDefaults')}
          </Button>
        </Box>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
          {t('settings.detailFields.subtitle')}
        </Typography>
        <Divider sx={{ mb: 2 }} />
        {renderFieldGroup(1, t('settings.detailFields.tier1'))}
        {renderFieldGroup(2, t('settings.detailFields.tier2'))}
        {renderFieldGroup(3, t('settings.detailFields.tier3'))}
      </Paper>
    </Box>
  );
}
