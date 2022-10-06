﻿using MultiFunPlayer.Common;
using Newtonsoft.Json.Linq;
using NLog;

namespace MultiFunPlayer.Settings.Migrations;

public class Migration__1_23_0__1 : AbstractConfigMigration
{
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public override int TargetVersion => 8;

    public override void Migrate(JObject settings)
    {
        if (settings.TryGetObject(out var logBlacklistSettings, "LogBlacklist"))
        {
            MigrateLogBlacklist(logBlacklistSettings);
        }

        base.Migrate(settings);
    }

    private void MigrateLogBlacklist(JObject settings)
    {
        Logger.Info("Migrating LogBlacklist");
        var filterMap = new Dictionary<string, string>()
        {
            ["MultiFunPlayer.UI.Controls.ViewModels.ShortcutViewModel"] = "MultiFunPlayer.UI.Controls.ViewModels.ShortcutSettingsViewModel"
        };

        foreach (var (from, to) in filterMap)
        {
            if (!settings.ContainsKey(from))
                continue;

            var value = settings[from];
            settings.Remove(from);
            settings.Add(to, value);

            Logger.Info("Migrated from \"{0}\" to \"{1}\"", from, to);
        }
    }
}