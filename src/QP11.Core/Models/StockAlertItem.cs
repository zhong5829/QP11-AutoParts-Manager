using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QP11.Core.Models;

public class StockAlertItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private long _partId;
    private string? _partno;
    private string? _name;
    private string? _namePy;
    private string? _cartype;
    private string? _cartypePy;
    private string? _unit;
    private decimal _amount;
    private decimal _warning;
    private decimal? _lsprice;
    private string? _className;
    private string? _place;

    public long PartId { get => _partId; set { if (_partId != value) { _partId = value; OnPropertyChanged(); } } }
    public string? Partno { get => _partno; set { if (_partno != value) { _partno = value; OnPropertyChanged(); } } }
    public string? Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(); } } }
    public string? NamePy { get => _namePy; set { if (_namePy != value) { _namePy = value; OnPropertyChanged(); } } }
    public string? Cartype { get => _cartype; set { if (_cartype != value) { _cartype = value; OnPropertyChanged(); } } }
    public string? CartypePy { get => _cartypePy; set { if (_cartypePy != value) { _cartypePy = value; OnPropertyChanged(); } } }
    public string? Unit { get => _unit; set { if (_unit != value) { _unit = value; OnPropertyChanged(); } } }
    public decimal Amount { get => _amount; set { if (_amount != value) { _amount = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLowStock)); } } }
    public decimal Warning { get => _warning; set { if (_warning != value) { _warning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLowStock)); } } }
    public decimal? Lsprice { get => _lsprice; set { if (_lsprice != value) { _lsprice = value; OnPropertyChanged(); } } }
    public string? ClassName { get => _className; set { if (_className != value) { _className = value; OnPropertyChanged(); } } }
    public string? Place { get => _place; set { if (_place != value) { _place = value; OnPropertyChanged(); } } }

    /// <summary>库存低于预警值（预警值>0且库存量&lt;预警值）</summary>
    public bool IsLowStock => Warning > 0 && Amount < Warning;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
