using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;

namespace M3URadioPlayer.Android;

[Activity(
    Label = "M3U Radio Player",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    public static MainActivity? Instance { get; private set; }

    public MainActivity()
    {
        Instance = this;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // ダークテーマに合わせてステータスバー・ナビゲーションバーの背景色とアイコン色を統一
        var darkColor = Color.ParseColor("#1E1E1E");
        Window?.SetStatusBarColor(darkColor);
        Window?.SetNavigationBarColor(darkColor);
        Window?.DecorView.SetBackgroundColor(darkColor);

        if (Window != null)
        {
            var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
            if (insetsController != null)
            {
                insetsController.AppearanceLightStatusBars = false;
                insetsController.AppearanceLightNavigationBars = false;
            }
        }

        // Edge-to-Edge による被りを Android ネイティブレベルで押し返す
        var contentView = FindViewById(global::Android.Resource.Id.Content);
        if (contentView != null)
        {
            contentView.SetBackgroundColor(darkColor);
            ViewCompat.SetOnApplyWindowInsetsListener(contentView, new SystemBarInsetsListener());
        }

        CheckAndRequestPermissions();
    }

    protected override void OnResume()
    {
        base.OnResume();

        if (MainWindow.Instance != null)
        {
            MainWindow.Instance.TriggerScanPlaylists();
        }
    }

    private void CheckAndRequestPermissions()
    {
        var neededPermissions = new List<string>();

        // 電話着信状態検知権限 (READ_PHONE_STATE)
        if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.ReadPhoneState) != Permission.Granted)
        {
            neededPermissions.Add(global::Android.Manifest.Permission.ReadPhoneState);
        }

        // ストレージ権限 (Android 12以下用)
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
        {
            if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.ReadExternalStorage) != Permission.Granted)
            {
                neededPermissions.Add(global::Android.Manifest.Permission.ReadExternalStorage);
            }
        }

        if (neededPermissions.Count > 0)
        {
            ActivityCompat.RequestPermissions(this, neededPermissions.ToArray(), 1002);
        }

        // 全ファイル管理アクセス権 (Android 11+)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            if (!global::Android.OS.Environment.IsExternalStorageManager)
            {
                try
                {
                    var uri = Uri.Parse($"package:{PackageName}");
                    var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission, uri);
                    StartActivity(intent);
                }
                catch
                {
                    var fallbackIntent = new Intent(Settings.ActionManageAllFilesAccessPermission);
                    StartActivity(fallbackIntent);
                }
            }
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder);
    }

    private class SystemBarInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
        {
            var systemBars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            v.SetPadding(systemBars.Left, systemBars.Top, systemBars.Right, systemBars.Bottom);
            return insets;
        }
    }
}
