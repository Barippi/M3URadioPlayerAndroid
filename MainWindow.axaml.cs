using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ManagedBass;

namespace M3URadioPlayer.Android;

public record StationItem(string Name, string Url);

public record PlaylistItem(string DisplayName, string FullPath)
{
    public override string ToString() => DisplayName;
}

public partial class MainWindow : UserControl
{
    public static MainWindow? Instance { get; private set; }

    private static readonly char[] TrimChars = new char[] { (char)39, ';', ' ' };

    private int _streamHandle = 0;
    private int _metaSyncHandle = 0;
    private SyncProcedure? _metaSyncProc;
    private List<StationItem> _stations = new();
    private string _currentStationName = "";

    // ローカル中継プロキシ
    private HttpListener? _localProxy;
    private CancellationTokenSource? _proxyCts;
    private const int LocalPort = 51234;
    private readonly string _localProxyUrl = $"http://127.0.0.1:{LocalPort}/stream/";

    private static readonly HttpClient _httpClient;

    static MainWindow()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Linux; Android)");
    }

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();

        this.Loaded += (s, e) =>
        {
            try
            {
                bool bassOk = Bass.Init(-1, 44100, DeviceInitFlags.Default, IntPtr.Zero);
                if (!bassOk && Bass.LastError != Errors.Already)
                {
                    StatusText.Text = "BASS初期化失敗: " + Bass.LastError;
                    return;
                }

                int flacPlugin = Bass.PluginLoad("libbassflac.so");
                if (flacPlugin == 0)
                {
                    StatusText.Text = "FLAC読込警告: " + Bass.LastError;
                }

                Bass.NetAgent = "Mozilla/5.0 (Linux; Android 9) AppleWebKit/537.36";

                ScanPlaylists();
                PlaylistCombo.SelectionChanged += OnPlaylistChanged;
            }
            catch (DllNotFoundException)
            {
                StatusText.Text = "エラー: libbass.so が見つかりません";
            }
            catch (Exception ex)
            {
                StatusText.Text = "初期化例外: " + ex.Message;
            }
        };
    }

    public void TriggerScanPlaylists()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ScanPlaylists();
        });
    }

    private void ScanPlaylists()
    {
        PlaylistCombo.ItemsSource = null;
        var playlistItems = new List<PlaylistItem>();

        var internalDirs = new[]
        {
            "/storage/emulated/0/Playlists",
            "/storage/emulated/0/Music/Playlists",
            "/sdcard/Playlists",
            "/storage/emulated/0/Android/data/com.barippi.m3uradioplayer/files/Playlists"
        };

        foreach (var dir in internalDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.m3u");
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);
                        if (!playlistItems.Exists(p => p.FullPath == file))
                        {
                            playlistItems.Add(new PlaylistItem($"本体: {fileName}", file));
                        }
                    }
                }
            }
            catch { }
        }

        if (Directory.Exists("/storage"))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories("/storage"))
                {
                    if (!dir.Contains("emulated") && !dir.Contains("self"))
                    {
                        var sdPlaylistsDir = Path.Combine(dir, "Playlists");
                        if (Directory.Exists(sdPlaylistsDir))
                        {
                            var files = Directory.GetFiles(sdPlaylistsDir, "*.m3u");
                            foreach (var file in files)
                            {
                                var fileName = Path.GetFileName(file);
                                if (!playlistItems.Exists(p => p.FullPath == file))
                                {
                                    playlistItems.Add(new PlaylistItem($"SD: {fileName}", file));
                                }
                            }
                        }

                        var sdAppDir = Path.Combine(dir, "Android", "data", "com.barippi.m3uradioplayer", "files", "Playlists");
                        if (Directory.Exists(sdAppDir))
                        {
                            var files = Directory.GetFiles(sdAppDir, "*.m3u");
                            foreach (var file in files)
                            {
                                var fileName = Path.GetFileName(file);
                                if (!playlistItems.Exists(p => p.FullPath == file))
                                {
                                    playlistItems.Add(new PlaylistItem($"SD(App): {fileName}", file));
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        PlaylistCombo.ItemsSource = playlistItems;

        if (playlistItems.Count > 0)
        {
            PlaylistCombo.SelectedIndex = 0;
        }
        else
        {
            StatusText.Text = "Playlists フォルダに m3u が見つかりません";
        }
    }

    private void OnPlaylistChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PlaylistCombo.SelectedItem is PlaylistItem item && File.Exists(item.FullPath))
        {
            LoadM3U(item.FullPath);
        }
    }

    private void LoadM3U(string path)
    {
        _stations.Clear();
        try
        {
            var lines = File.ReadAllLines(path);
            string currentTitle = "";

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#EXTINF:"))
                {
                    var commaIndex = trimmed.IndexOf(',');
                    currentTitle = commaIndex >= 0 ? trimmed.Substring(commaIndex + 1) : trimmed;
                }
                else if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                {
                    _stations.Add(new StationItem(string.IsNullOrEmpty(currentTitle) ? trimmed : currentTitle, trimmed));
                    currentTitle = "";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "読込エラー: " + ex.Message;
        }

        StationList.ItemsSource = null;
        StationList.ItemsSource = _stations;
    }

    private void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        if (StationList.SelectedItem is StationItem station)
        {
            try
            {
                StopPlayback();

                _currentStationName = station.Name;
                StatusText.Text = "接続中: " + station.Name;
                TitleText.Text = "曲名: (取得中...)";

                _proxyCts = new CancellationTokenSource();
                var ct = _proxyCts.Token;

                StartLocalProxy(station.Url, ct);

                Task.Run(async () =>
                {
                    await Task.Delay(200, ct);

                    _streamHandle = Bass.CreateStream(_localProxyUrl, 0, BassFlags.Default | BassFlags.AutoFree, null, IntPtr.Zero);

                    if (_streamHandle != 0)
                    {
                        _metaSyncProc = new SyncProcedure(OnMetadataReceived);
                        _metaSyncHandle = Bass.ChannelSetSync(_streamHandle, SyncFlags.MetadataReceived, 0, _metaSyncProc, IntPtr.Zero);
                        Bass.ChannelSetSync(_streamHandle, SyncFlags.OggChange, 0, _metaSyncProc, IntPtr.Zero);

                        Bass.ChannelPlay(_streamHandle);

                        Dispatcher.UIThread.Post(() =>
                        {
                            StatusText.Text = "再生中: " + station.Name;
                            FetchCurrentTrackTitle();
                        });

                        try
                        {
                            StartPlaybackService(station.Name, "");
                        }
                        catch { }
                    }
                    else
                    {
                        var err = Bass.LastError;
                        Dispatcher.UIThread.Post(() =>
                        {
                            StatusText.Text = $"再生失敗: {err} (コード: {(int)err})";
                        });
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                StatusText.Text = "エラー: " + ex.Message;
            }
        }
    }

    private void StartLocalProxy(string targetUrl, CancellationToken ct)
    {
        try
        {
            _localProxy = new HttpListener();
            _localProxy.Prefixes.Add(_localProxyUrl);
            _localProxy.Start();

            Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested && _localProxy != null && _localProxy.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _localProxy.GetContextAsync();
                    }
                    catch
                    {
                        break;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var req = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                            req.Headers.Add("Icy-MetaData", "1");

                            using var upstream = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

                            context.Response.StatusCode = (int)upstream.StatusCode;

                            if (upstream.Content.Headers.ContentType != null)
                            {
                                context.Response.ContentType = upstream.Content.Headers.ContentType.ToString();
                            }

                            foreach (var header in upstream.Headers)
                            {
                                if (header.Key.StartsWith("icy-", StringComparison.OrdinalIgnoreCase))
                                {
                                    context.Response.Headers[header.Key] = string.Join(",", header.Value);
                                }
                            }

                            using var inStream = await upstream.Content.ReadAsStreamAsync(ct);
                            using var outStream = context.Response.OutputStream;

                            byte[] buf = new byte[16384];
                            int read;
                            while (!ct.IsCancellationRequested && (read = await inStream.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                            {
                                await outStream.WriteAsync(buf, 0, read, ct);
                                await outStream.FlushAsync(ct);
                            }
                        }
                        catch { }
                        finally
                        {
                            try { context.Response.Close(); } catch { }
                        }
                    }, ct);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText.Text = "プロキシ起動失敗: " + ex.Message;
            });
        }
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        StopPlayback();
    }

    private void StopPlayback()
    {
        if (_proxyCts != null)
        {
            _proxyCts.Cancel();
            _proxyCts.Dispose();
            _proxyCts = null;
        }

        if (_localProxy != null)
        {
            try { _localProxy.Stop(); _localProxy.Close(); } catch { }
            _localProxy = null;
        }

        if (_streamHandle != 0)
        {
            Bass.ChannelStop(_streamHandle);
            Bass.StreamFree(_streamHandle);
            _streamHandle = 0;
            StatusText.Text = "停止";
            TitleText.Text = "曲名: ---";
        }

        StopPlaybackService();
    }

    private void OnReloadClicked(object? sender, RoutedEventArgs e)
    {
        ScanPlaylists();
    }

    private void OnMetadataReceived(int handle, int channel, int data, IntPtr user)
    {
        Dispatcher.UIThread.Post(() =>
        {
            FetchCurrentTrackTitle();
        });
    }

    private void FetchCurrentTrackTitle()
    {
        if (_streamHandle == 0) return;

        string track = "";

        IntPtr metaPtr = Bass.ChannelGetTags(_streamHandle, TagType.META);
        if (metaPtr != IntPtr.Zero)
        {
            string? icy = Marshal.PtrToStringAnsi(metaPtr);
            if (!string.IsNullOrEmpty(icy))
            {
                int titleIdx = icy.IndexOf("StreamTitle='", StringComparison.OrdinalIgnoreCase);
                if (titleIdx >= 0)
                {
                    int start = titleIdx + 13;
                    int end = icy.IndexOf("';", start, StringComparison.OrdinalIgnoreCase);
                    if (end > start)
                    {
                        track = icy.Substring(start, end - start).Trim();
                    }
                    else
                    {
                        track = icy.Substring(start).TrimEnd(TrimChars);
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(track))
        {
            IntPtr oggPtr = Bass.ChannelGetTags(_streamHandle, TagType.OGG);
            if (oggPtr != IntPtr.Zero)
            {
                track = ParseOggTags(oggPtr);
            }
        }

        if (!string.IsNullOrEmpty(track))
        {
            Dispatcher.UIThread.Post(() =>
            {
                TitleText.Text = "曲名: " + track;
            });
            UpdateServiceNotification(track);
        }
    }

    private string ParseOggTags(IntPtr p)
    {
        string artist = "";
        string title = "";
        IntPtr current = p;
        while (true)
        {
            string? line = Marshal.PtrToStringAnsi(current);
            if (string.IsNullOrEmpty(line)) break;

            if (line.StartsWith("TITLE=", StringComparison.OrdinalIgnoreCase))
                title = line.Substring(6);
            else if (line.StartsWith("ARTIST=", StringComparison.OrdinalIgnoreCase))
                artist = line.Substring(7);

            current = IntPtr.Add(current, line.Length + 1);
        }

        if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
            return $"{artist} - {title}";
        return title;
    }

    private void StartPlaybackService(string station, string title)
    {
        try
        {
            var context = MainActivity.Instance ?? global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(RadioPlaybackService));
            intent.SetAction(RadioPlaybackService.ActionStart);
            intent.PutExtra(RadioPlaybackService.ExtraStation, station);
            intent.PutExtra(RadioPlaybackService.ExtraTitle, title);

            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }
        catch { }
    }

    private void UpdateServiceNotification(string title)
    {
        try
        {
            var context = MainActivity.Instance ?? global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(RadioPlaybackService));
            intent.SetAction(RadioPlaybackService.ActionUpdateTitle);
            intent.PutExtra(RadioPlaybackService.ExtraTitle, title);
            context.StartService(intent);
        }
        catch { }
    }

    private void StopPlaybackService()
    {
        try
        {
            var context = MainActivity.Instance ?? global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(RadioPlaybackService));
            intent.SetAction(RadioPlaybackService.ActionStop);
            context.StartService(intent);
        }
        catch { }
    }
}
