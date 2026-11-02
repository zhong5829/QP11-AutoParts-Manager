using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

public partial class PartEditWindow : Window
{
    private readonly IPartRepository _partRepo = App.ServiceProvider.GetRequiredService<IPartRepository>();
    private readonly PartData? _editEntity;
    private readonly bool _isEdit;

    /// <summary>待保存的图片数据（null 表示无变化；非 null 时保存后写入/清除 part_data.picture）</summary>
    private byte[]? _pendingPicture;
    private bool _pictureChanged;

    public PartEditWindow() : this(null) { }

    public PartEditWindow(PartData? entity)
    {
        InitializeComponent();
        _editEntity = entity;
        _isEdit = entity != null;
        LoadDictData();

        if (_isEdit && entity != null)
        {
            txtPartno.Text = entity.Partno;
            txtPartno.IsReadOnly = true;
            txtPartno.Background = SystemColors.ControlBrush;
            txtName.Text = entity.Name;
            cboCarname.Text = entity.Carname;
            cboCartype.Text = entity.Cartype;
            cboUnit.Text = entity.Unit;
            cboClass.Text = entity.ClassName;
            cboPlace.Text = entity.Place;
            cboArea.Text = entity.Area;
            txtInprice.Text = entity.Inprice?.ToString() ?? "0";
            txtLsprice.Text = entity.Lsprice?.ToString() ?? "0";
            txtPfprice.Text = entity.Pfprice?.ToString() ?? "0";
            txtNamePy.Text = entity.NamePy;
            txtMemo.Text = entity.Memo;
        }
    }

    private async void LoadDictData()
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();

            var classes = (await Dapper.SqlMapper.QueryAsync<PartClass>(db,
                "SELECT * FROM CLASSES WHERE CLASS_TYPE IN ('0001','0002') ORDER BY CLASS_TYPE, CLASS_NO")).ToList();
            LoadCombo(cboClass, classes.Where(c => c.ClassId == "0001").Select(c => c.ClassName));

            var carnames = await Dapper.SqlMapper.QueryAsync<string>(db,
                "SELECT DISTINCT carname FROM part_data WHERE carname IS NOT NULL AND carname <> '' ORDER BY carname");
            LoadCombo(cboCarname, carnames);

            var cartypes = await Dapper.SqlMapper.QueryAsync<string>(db,
                "SELECT DISTINCT cartype FROM part_data WHERE cartype IS NOT NULL AND cartype <> '' ORDER BY cartype");
            LoadCombo(cboCartype, cartypes);

            var units = await Dapper.SqlMapper.QueryAsync<string>(db,
                "SELECT DISTINCT unit FROM part_data WHERE unit IS NOT NULL AND unit <> '' ORDER BY unit");
            LoadCombo(cboUnit, units);

            var places = await Dapper.SqlMapper.QueryAsync<string>(db,
                "SELECT DISTINCT place FROM part_data WHERE place IS NOT NULL AND place <> '' ORDER BY place");
            LoadCombo(cboPlace, places);

            var areas = await Dapper.SqlMapper.QueryAsync<string>(db,
                "SELECT DISTINCT area FROM part_data WHERE area IS NOT NULL AND area <> '' ORDER BY area");
            LoadCombo(cboArea, areas);

            // 仓位以库存表 part_stock.place 为准（销售开单/库存查询直读该字段），有值时覆盖档案中的值
            if (_isEdit && _editEntity != null)
            {
                var stockPlace = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<string>(db,
                    "SELECT place FROM part_stock WHERE partid=@PartId", new { PartId = _editEntity.Partid });
                if (!string.IsNullOrEmpty(stockPlace))
                    cboPlace.Text = stockPlace;

                // 加载已有图片预览（picture 列不在 PartColumns 中，需单独查询）
                var picture = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<byte[]>(db,
                    "SELECT picture FROM part_data WHERE partid=@PartId", new { PartId = _editEntity.Partid });
                if (picture is { Length: > 0 })
                {
                    imgPicture.Source = ImageCompressor.LoadBitmap(picture);
                    txtPictureInfo.Text = $"当前图片：{ImageCompressor.FormatSize(picture.Length)}";
                }
                else
                {
                    txtPictureInfo.Text = "未选择";
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载配件下拉选项失败");
        }
    }

    private static void LoadCombo(System.Windows.Controls.ComboBox cbo, System.Collections.Generic.IEnumerable<string?> items)
    {
        cbo.ItemsSource = items.Where(s => !string.IsNullOrEmpty(s)).ToList();
    }

    #region 配件图片（限制 100KB，超出自动压缩）

    /// <summary>选择图片：超过 100KB 自动压缩（JPEG 质量递减 + 尺寸递缩）</summary>
    private void BtnSelectPicture_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var raw = File.ReadAllBytes(dlg.FileName);
            var final = ImageCompressor.CompressToLimit(raw, out var compressed);
            txtPictureInfo.Text = compressed
                ? $"已压缩：{ImageCompressor.FormatSize(raw.Length)} → {ImageCompressor.FormatSize(final.Length)}"
                : $"原始大小：{ImageCompressor.FormatSize(raw.Length)}（未压缩）";

            _pendingPicture = final;
            _pictureChanged = true;
            imgPicture.Source = ImageCompressor.LoadBitmap(final);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "选择配件图片失败");
            MessageBox.Show($"读取图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>清除图片：保存后从档案中移除</summary>
    private void BtnClearPicture_Click(object sender, RoutedEventArgs e)
    {
        _pendingPicture = null;
        _pictureChanged = true;
        imgPicture.Source = null;
        txtPictureInfo.Text = "图片已清除（保存后生效）";
    }

    /// <summary>保存成功后写入图片（独立 UPDATE，不影响 UpdateAsync 的既有字段集合）</summary>
    private async System.Threading.Tasks.Task SavePictureAsync(long partId)
    {
        if (!_pictureChanged) return;
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            // _pendingPicture == null 表示用户点了清除，写入 NULL
            await db.ExecuteAsync("UPDATE part_data SET picture=@Picture WHERE partid=@Id",
                new { Picture = _pendingPicture, Id = partId });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存配件图片失败, partId={PartId}", partId);
            MessageBox.Show($"配件已保存，但图片保存失败: {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtPartno.Text) || string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("件号和名称不能为空", "提示");
            return;
        }

        var entity = new PartData
        {
            Partid = _editEntity?.Partid ?? 0,
            Partno = txtPartno.Text.Trim(),
            Name = txtName.Text.Trim(),
            Carname = cboCarname.Text?.Trim(),
            Cartype = cboCartype.Text?.Trim(),
            Unit = cboUnit.Text?.Trim(),
            ClassName = cboClass.Text?.Trim(),
            Place = cboPlace.Text?.Trim(),
            Area = cboArea.Text?.Trim(),
            Inprice = decimal.TryParse(txtInprice.Text, out var inp) ? inp : 0,
            Lsprice = decimal.TryParse(txtLsprice.Text, out var lsp) ? lsp : 0,
            Pfprice = decimal.TryParse(txtPfprice.Text, out var pfp) ? pfp : 0,
            NamePy = string.IsNullOrWhiteSpace(txtNamePy.Text)
                ? PinyinHelper.GetPinyinInitials(txtName.Text.Trim())
                : txtNamePy.Text.Trim(),
            CartypePy = PinyinHelper.GetPinyinInitials(cboCartype.Text?.Trim() ?? ""),
            Memo = txtMemo.Text.Trim()
        };

        try
        {
            if (_isEdit)
                await _partRepo.UpdateAsync(entity);
            else
                await _partRepo.InsertAsync(entity);
            // 配件档案已变更，使入库等模块的下拉联想缓存失效
            BuyEditDialog.InvalidateCache();

            // 保存图片（新增配件时 InsertAsync 已回填 entity.Partid）
            await SavePictureAsync(entity.Partid);

            DialogResult = true;
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存配件失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }
}
