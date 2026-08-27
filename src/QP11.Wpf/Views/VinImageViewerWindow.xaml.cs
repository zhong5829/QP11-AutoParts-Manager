using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QP11.Wpf.Views;

public partial class VinImageViewerWindow : Window
{
    private readonly List<string> _urls;
    private int _current;

    public VinImageViewerWindow(List<string> imgUrls, int selectedIndex = 0)
    {
        InitializeComponent();
        _urls = imgUrls;
        _current = selectedIndex;

        // 加载大图
        LoadMainImage(_current);

        // 生成缩略图
        for (int i = 0; i < _urls.Count; i++)
        {
            var idx = i;
            var border = new Border
            {
                Width = 64, Height = 64,
                Margin = new Thickness(4, 0, 4, 0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = i == _current ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
            };
            var thumbImg = new Image
            {
                Source = new BitmapImage(new Uri(_urls[i])),
                Stretch = Stretch.UniformToFill
            };
            RenderOptions.SetBitmapScalingMode(thumbImg, BitmapScalingMode.LowQuality);
            border.Child = thumbImg;
            border.MouseLeftButtonUp += (s, e) =>
            {
                _current = idx;
                LoadMainImage(idx);
                // 高亮当前缩略图
                foreach (var child in spThumbs.Children)
                    if (child is Border b)
                        b.BorderBrush = System.Windows.Media.Brushes.Transparent;
                border.BorderBrush = System.Windows.Media.Brushes.White;
            };
            spThumbs.Children.Add(border);
        }

        // 只有一张图时隐藏缩略图栏
        if (_urls.Count <= 1)
            spThumbs.Visibility = Visibility.Collapsed;
    }

    private void LoadMainImage(int index)
    {
        if (index >= 0 && index < _urls.Count)
        {
            try
            {
                imgMain.Source = new BitmapImage(new Uri(_urls[index]));
                Title = $"图片查看 ({index + 1}/{_urls.Count})";
            }
            catch { }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Left && _current > 0)
        {
            _current--;
            LoadMainImage(_current);
            HighlightThumb(_current);
        }
        else if (e.Key == Key.Right && _current < _urls.Count - 1)
        {
            _current++;
            LoadMainImage(_current);
            HighlightThumb(_current);
        }
    }

    private void HighlightThumb(int index)
    {
        for (int i = 0; i < spThumbs.Children.Count; i++)
            if (spThumbs.Children[i] is Border b)
                b.BorderBrush = i == index ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;
    }
}
