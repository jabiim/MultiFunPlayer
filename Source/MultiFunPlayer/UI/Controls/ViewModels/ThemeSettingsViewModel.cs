﻿using MaterialDesignColors.ColorManipulation;
using MaterialDesignThemes.Wpf;
using MultiFunPlayer.Common;
using Newtonsoft.Json.Linq;
using Stylet;
using System.Windows.Media;

namespace MultiFunPlayer.UI.Controls.ViewModels;

internal class ThemeSettingsViewModel : Screen, IHandle<SettingsMessage>
{
    private readonly PaletteHelper _paletteHelper;

    private bool _ignorePropertyChanged;

    public Color PrimaryColor { get; set; } = Color.FromRgb(0x71, 0x87, 0x92);
    public bool EnableColorAdjustment { get; set; } = false;
    public Contrast Contrast { get; set; } = Contrast.Medium;
    public double ContrastRatio { get; set; } = 4.5;
    public bool IsDarkTheme { get; set; } = false;

    public ThemeSettingsViewModel(IEventAggregator eventAggregator)
    {
        DisplayName = "Theme";
        eventAggregator.Subscribe(this);
        _paletteHelper = new PaletteHelper();

        if (_paletteHelper.GetTheme() is Theme theme)
        {
            IgnorePropertyChanged(() =>
            {
                PrimaryColor = theme.PrimaryDark.Color.Lighten();

                var colorAdjustment = new ColorAdjustment();
                Contrast = colorAdjustment.Contrast;
                ContrastRatio = colorAdjustment.DesiredContrastRatio;
            });
            ApplyTheme();
        }
    }

    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);

        if (_ignorePropertyChanged)
            return;

        if (propertyName is nameof(EnableColorAdjustment) or nameof(PrimaryColor)
                         or nameof(Contrast) or nameof(ContrastRatio) or nameof(IsDarkTheme))
            ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (_paletteHelper.GetTheme() is not Theme theme)
            return;

        theme.SetBaseTheme(IsDarkTheme ? Theme.Dark : Theme.Light);

        if (EnableColorAdjustment)
            theme.ColorAdjustment ??= new ColorAdjustment();
        else
            theme.ColorAdjustment = null;

        if (theme.ColorAdjustment is ColorAdjustment colorAdjustment)
        {
            colorAdjustment.DesiredContrastRatio = (float)ContrastRatio;
            colorAdjustment.Contrast = Contrast;
        }

        theme.SetPrimaryColor(PrimaryColor);
        _paletteHelper.SetTheme(theme);
    }

    public void Handle(SettingsMessage message)
    {
        if (message.Action == SettingsAction.Saving)
        {
            if (!message.Settings.EnsureContainsObjects("Theme")
             || !message.Settings.TryGetObject(out var settings, "Theme"))
                return;

            settings[nameof(EnableColorAdjustment)] = EnableColorAdjustment;
            settings[nameof(PrimaryColor)] = JToken.FromObject(PrimaryColor);
            settings[nameof(Contrast)] = JToken.FromObject(Contrast);
            settings[nameof(ContrastRatio)] = ContrastRatio;
            settings[nameof(IsDarkTheme)] = IsDarkTheme;
        }
        else if (message.Action == SettingsAction.Loading)
        {
            if (!message.Settings.TryGetObject(out var settings, "Theme"))
                return;

            IgnorePropertyChanged(() =>
            {
                if (settings.TryGetValue<bool>(nameof(EnableColorAdjustment), out var enableColorAdjustment))
                    EnableColorAdjustment = enableColorAdjustment;
                if (settings.TryGetValue<Color>(nameof(PrimaryColor), out var color))
                    PrimaryColor = color;
                if (settings.TryGetValue<Contrast>(nameof(Contrast), out var contrast))
                    Contrast = contrast;
                if (settings.TryGetValue<double>(nameof(ContrastRatio), out var contrastRatio))
                    ContrastRatio = contrastRatio;
                if (settings.TryGetValue<bool>(nameof(IsDarkTheme), out var isDarkTheme))
                    IsDarkTheme = isDarkTheme;
            });
            ApplyTheme();
        }
    }

    private void IgnorePropertyChanged(Action action)
    {
        _ignorePropertyChanged = true;
        action();
        _ignorePropertyChanged = false;
    }
}