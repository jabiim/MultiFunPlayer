using MultiFunPlayer.Common;
using MultiFunPlayer.Shortcut;
using MultiFunPlayer.UI;
using Newtonsoft.Json.Linq;
using NLog;
using Stylet;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MultiFunPlayer.OutputTarget.ViewModels;

[DisplayName("TCP")]
internal sealed class TcpOutputTarget(int instanceIndex, IEventAggregator eventAggregator, IDeviceAxisValueProvider valueProvider)
    : ThreadAbstractOutputTarget(instanceIndex, eventAggregator, valueProvider)
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public override ConnectionStatus Status { get; protected set; }

    public DeviceAxisUpdateType UpdateType { get; set; } = DeviceAxisUpdateType.FixedUpdate;
    public bool CanChangeUpdateType => !IsConnectBusy && !IsConnected;

    public EndPoint Endpoint { get; set; } = new IPEndPoint(IPAddress.Loopback, 8080);

    public bool IsConnected => Status == ConnectionStatus.Connected;
    public bool IsConnectBusy => Status == ConnectionStatus.Connecting || Status == ConnectionStatus.Disconnecting;
    public bool CanToggleConnect => !IsConnectBusy;

    protected override IUpdateContext RegisterUpdateContext(DeviceAxisUpdateType updateType) => updateType switch
    {
        DeviceAxisUpdateType.FixedUpdate => new TCodeThreadFixedUpdateContext(),
        DeviceAxisUpdateType.PolledUpdate => new ThreadPolledUpdateContext(),
        _ => null,
    };

    protected override void Run(CancellationToken token)
    {
        using var client = new TcpClient();

        try
        {
            Logger.Info("Connecting to {0} at \"{1}\"", Identifier, $"tcp://{Endpoint}");
            client.Connect(Endpoint);
            Status = ConnectionStatus.Connected;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error when connecting to server");
            _ = DialogHelper.ShowErrorAsync(e, "Error when connecting to server", "RootDialog");
            return;
        }

        try
        {
            EventAggregator.Publish(new SyncRequestMessage());

            var buffer = new byte[256];
            var stream = client.GetStream();
            if (UpdateType == DeviceAxisUpdateType.FixedUpdate)
            {
                var currentValues = DeviceAxis.All.ToDictionary(a => a, _ => double.NaN);
                var lastSentValues = DeviceAxis.All.ToDictionary(a => a, _ => double.NaN);
                FixedUpdate<TCodeThreadFixedUpdateContext>(() => !token.IsCancellationRequested && client.Connected, (context, elapsed) =>
                {
                    Logger.Trace("Begin FixedUpdate [Elapsed: {0}]", elapsed);
                    GetValues(currentValues);

                    if (client.Connected && client.Available > 0)
                    {
                        var message = Encoding.UTF8.GetString(stream.ReadBytes(client.Available));
                        Logger.Debug("Received \"{0}\" from \"{1}\"", message, $"tcp://{Endpoint}");
                    }

                    var values = context.SendDirtyValuesOnly ? currentValues.Where(x => DeviceAxis.IsValueDirty(x.Value, lastSentValues[x.Key])) : currentValues;
                    values = values.Where(x => AxisSettings[x.Key].Enabled);

                    var commands = context.OffloadElapsedTime ? DeviceAxis.ToString(values) : DeviceAxis.ToString(values, elapsed * 1000);
                    if (client.Connected && !string.IsNullOrWhiteSpace(commands))
                    {
                        Logger.Trace("Sending \"{0}\" to \"{1}\"", commands.Trim(), $"tcp://{Endpoint}");

                        var encoded = Encoding.UTF8.GetBytes(commands, buffer);
                        stream.Write(buffer, 0, encoded);
                        lastSentValues.Merge(values);
                    }
                });
            }
            else if (UpdateType == DeviceAxisUpdateType.PolledUpdate)
            {
                PolledUpdate(DeviceAxis.All, () => !token.IsCancellationRequested, (_, axis, snapshot, elapsed) =>
                {
                    Logger.Trace("Begin PolledUpdate [Axis: {0}, Index From: {1}, Index To: {2}, Duration: {3}, Elapsed: {4}]",
                        axis, snapshot.IndexFrom, snapshot.IndexTo, snapshot.Duration, elapsed);

                    var settings = AxisSettings[axis];
                    if (!settings.Enabled)
                        return;
                    if (snapshot.KeyframeFrom == null || snapshot.KeyframeTo == null)
                        return;

                    var value = MathUtils.Lerp(settings.Minimum / 100, settings.Maximum / 100, snapshot.KeyframeTo.Value);
                    var duration = snapshot.Duration;

                    var command = DeviceAxis.ToString(axis, value, duration);
                    if (client.Connected && !string.IsNullOrWhiteSpace(command))
                    {
                        Logger.Trace("Sending \"{0}\" to \"{1}\"", command, $"tcp://{Endpoint}");

                        var encoded = Encoding.UTF8.GetBytes($"{command}\n", buffer);
                        stream.Write(buffer, 0, encoded);
                    }
                }, token);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"{Identifier} failed with exception");
            _ = DialogHelper.ShowErrorAsync(e, $"{Identifier} failed with exception", "RootDialog");
        }
    }

    public override void HandleSettings(JObject settings, SettingsAction action)
    {
        base.HandleSettings(settings, action);

        if (action == SettingsAction.Saving)
        {
            settings[nameof(UpdateType)] = JToken.FromObject(UpdateType);
            settings[nameof(Endpoint)] = Endpoint?.ToString();
        }
        else if (action == SettingsAction.Loading)
        {
            if (settings.TryGetValue<DeviceAxisUpdateType>(nameof(UpdateType), out var updateType))
                UpdateType = updateType;
            if (settings.TryGetValue<EndPoint>(nameof(Endpoint), out var endpoint))
                Endpoint = endpoint;
        }
    }

    public override void RegisterActions(IShortcutManager s)
    {
        base.RegisterActions(s);

        #region Endpoint
        s.RegisterAction<string>($"{Identifier}::Endpoint::Set", s => s.WithLabel("Endpoint").WithDescription("ip/host:port"), endpointString =>
        {
            if (NetUtils.TryParseEndpoint(endpointString, out var endpoint))
                Endpoint = endpoint;
        });
        #endregion
    }

    public override void UnregisterActions(IShortcutManager s)
    {
        base.UnregisterActions(s);
        s.UnregisterAction($"{Identifier}::Endpoint::Set");
    }

    public override async ValueTask<bool> CanConnectAsync(CancellationToken token)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(Endpoint, token);
            client.GetStream();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
