using System;
using System.Linq;
using System.Windows;
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
            DialogResult = true;
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存配件失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }
}
