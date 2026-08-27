using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepo;
    private readonly IDatabaseInfoService _dbInfoService;
    private readonly DispatcherTimer _carTimer;
    private int _carIndex;
    private const int CarCount = 3;

    private readonly System.Windows.Controls.Image[] _carImages = new System.Windows.Controls.Image[CarCount];
    private readonly System.Windows.Shapes.Ellipse[] _carDots = new System.Windows.Shapes.Ellipse[CarCount];
    private static readonly string[] CarouselFiles = { "carousel_1.png", "carousel_2.png", "carousel_3.png" };

    public UserInfor? CurrentUser { get; private set; }

    public LoginWindow(IAuthService authService, IUserRepository userRepo, IDatabaseInfoService dbInfoService)
    {
        _authService = authService;
        _userRepo = userRepo;
        _dbInfoService = dbInfoService;
        InitializeComponent();

        LoadWindowIcon();
        _carImages[0] = imgCar0;
        _carImages[1] = imgCar1;
        _carImages[2] = imgCar2;
        _carDots[0] = dot0;
        _carDots[1] = dot1;
        _carDots[2] = dot2;
        _carIndex = 0;

        LoadCarouselImages();

        _carTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _carTimer.Tick += CarTimer_Tick;
        _carTimer.Start();

        TestDbConnectionAsync();
        Loaded += LoginWindow_Loaded;
    }

    private void LoadWindowIcon()
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(path))
            {
                var uri = new Uri(path, UriKind.Absolute);
                var decoder = new IconBitmapDecoder(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                    Icon = decoder.Frames[0];
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载窗口图标失败");
        }
    }

    private void LoadCarouselImages()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < CarCount; i++)
        {
            var path = Path.Combine(baseDir, "Assets", CarouselFiles[i]);
            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                _carImages[i].Source = bmp;
            }
        }
    }

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= LoginWindow_Loaded;
        await LoadUsersAsync();
    }

    private void CarTimer_Tick(object? sender, EventArgs e)
    {
        _carImages[_carIndex].Visibility = Visibility.Collapsed;
        _carDots[_carIndex].Fill = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
        _carIndex = (_carIndex + 1) % CarCount;
        _carImages[_carIndex].Visibility = Visibility.Visible;
        _carDots[_carIndex].Fill = System.Windows.Media.Brushes.White;
    }

    

    private async Task LoadUsersAsync()
    {
        try
        {
            var users = (await _userRepo.GetAllAsync())?.ToList() ?? new List<UserInfor>();
            cmbUsername.ItemsSource = users;
            if (users.Count > 0)
                cmbUsername.SelectedIndex = 0;
            txtPassword.Focus();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载用户列表失败");
        }
    }

    private async void TestDbConnectionAsync()
    {
        txtStatus.Text = "Ver 13.1 | 数据库: 正在连接...";
        txtStatus.Foreground = System.Windows.Media.Brushes.Gray;
        try
        {
            string msg = "";
            var success = await Task.Run(() => _dbInfoService.TestConnection(out msg));
            txtStatus.Text = $"Ver 13.1 | 数据库: {msg}";
            txtStatus.Foreground = success
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "检查数据库连接失败");
            txtStatus.Text = $"Ver 13.1 | 数据库异常: {ex.Message}";
            txtStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private async void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        var username = cmbUsername.SelectedValue?.ToString()
            ?? cmbUsername.Text?.Trim();
        var password = txtPassword.Password;

        if (string.IsNullOrEmpty(username))
        {
            MessageBox.Show("请选择用户", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            cmbUsername.Focus();
            return;
        }

        try
        {
            IsEnabled = false;
            CurrentUser = await _authService.LoginAsync(username, password);

            if (CurrentUser != null)
            {
                await App.PermissionService!.LoadUserPermissionsAsync(username);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("密码错误", "登录失败", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }
        catch (InvalidOperationException ex)
        {
            Serilog.Log.Warning(ex, "数据库连接失败");
            MessageBox.Show(ex.Message, "数据库连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "登录失败");
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            MessageBox.Show($"登录失败:\n{innerMsg}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }
}
