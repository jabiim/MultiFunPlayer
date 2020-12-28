﻿using MaterialDesignThemes.Wpf;
using MultiFunPlayer.Common;
using MultiFunPlayer.Common.Controls;
using Stylet;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MultiFunPlayer.VideoSource
{
    public class DeoVRVideoSource : PropertyChangedBase, IVideoSource
    {
        private readonly IEventAggregator _eventAggregator;
        private CancellationTokenSource _cancellationSource;
        private Task _task;

        public string Name => "DeoVR";
        public VideoSourceStatus Status { get; private set; }

        public DeoVRVideoSource(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public void Start()
        {
            Stop();

            _cancellationSource = new CancellationTokenSource();
            _task = Task.Factory.StartNew(() => RunAsync(_cancellationSource.Token),
                _cancellationSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
                .Unwrap();
            _ = _task.ContinueWith(_ => Stop());
        }

        public void Stop()
        {
            Status = VideoSourceStatus.Disconnected;

            _cancellationSource?.Cancel();
            _task?.Wait();
            _cancellationSource?.Dispose();

            _cancellationSource = null;
            _task = null;
        }

        private async Task RunAsync(CancellationToken token)
        {
            static async Task<byte[]> ReadAllBytesAsync(NetworkStream stream, CancellationToken token)
            {
                var result = 0;
                var buffer = new ArraySegment<byte>(new byte[1024]);
                using var memory = new MemoryStream();
                do
                {
                    result = await stream.ReadAsync(buffer, token);
                    await memory.WriteAsync(buffer.AsMemory(buffer.Offset, result), token);
                }
                while (result > 0 && stream.DataAvailable);

                memory.Seek(0, SeekOrigin.Begin);
                return memory.ToArray();
            }

            try
            {
                if (Process.GetProcessesByName("DeoVR").Length == 0)
                    throw new Exception("Could not find a running DeoVR process.");

                using var client = new TcpClient("localhost", 23554);
                using var stream = client.GetStream();

                _ = Task.Factory.StartNew(async () =>
                {
                    var pingBuffer = new byte[4];
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(500, token);
                        await stream.WriteAsync(pingBuffer, token);
                        await stream.FlushAsync(token);
                    }
                }, token);

                Status = VideoSourceStatus.Connected;
                while (!token.IsCancellationRequested && client.Connected)
                {
                    var data = await ReadAllBytesAsync(stream, token);
                    if (data.Length <= 4)
                        continue;

                    var length = BitConverter.ToInt32(data[0..4], 0);
                    if (length <= 0 || data.Length != length + 4)
                        continue;

                    try
                    {
                        var document = JsonDocument.Parse(data.AsMemory(4..(length+4)));

                        if (document.RootElement.TryGetProperty("playerState", out var stateProperty))
                            _eventAggregator.Publish(new VideoPlayingMessage(isPlaying: stateProperty.GetInt32() == 0));

                        if (document.RootElement.TryGetProperty("duration", out var durationProperty))
                            _eventAggregator.Publish(new VideoDurationMessage(TimeSpan.FromSeconds(durationProperty.GetDouble())));

                        if (document.RootElement.TryGetProperty("currentTime", out var timeProperty))
                            _eventAggregator.Publish(new VideoPositionMessage(TimeSpan.FromSeconds(timeProperty.GetDouble())));

                        if (document.RootElement.TryGetProperty("path", out var pathProperty))
                            _eventAggregator.Publish(new VideoFileChangedMessage(pathProperty.GetString()));

                        if (document.RootElement.TryGetProperty("playbackSpeed", out var speedProperty))
                            _eventAggregator.Publish(new VideoSpeedMessage((float)speedProperty.GetDouble()));
                    }
                    catch (JsonException) { }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (Exception e)
            {
                _ = Execute.OnUIThreadAsync(() => DialogHost.Show(new ErrorMessageDialog($"DeoVR failed with exception:\n\n{e}")));
            }

            Status = VideoSourceStatus.Disconnected;

            _eventAggregator.Publish(new VideoFileChangedMessage(null));
            _eventAggregator.Publish(new VideoPlayingMessage(isPlaying: false));
        }

        protected virtual void Dispose(bool disposing)
        {
            Stop();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
