using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace QP11.Wpf.Helpers
{
    /// <summary>
    /// DatePicker 附加行为：支持方向键切换年/月/日段、手动输入自动覆盖
    /// 用法：helpers:DatePickerHelper.EnableSmartEdit="True" 或全局 Style 中设置
    /// </summary>
    public static class DatePickerHelper
    {
        #region 附加属性

        public static readonly DependencyProperty EnableSmartEditProperty =
            DependencyProperty.RegisterAttached("EnableSmartEdit", typeof(bool), typeof(DatePickerHelper),
                new PropertyMetadata(false, OnEnableSmartEditChanged));

        public static bool GetEnableSmartEdit(DependencyObject obj) => (bool)obj.GetValue(EnableSmartEditProperty);
        public static void SetEnableSmartEdit(DependencyObject obj, bool value) => obj.SetValue(EnableSmartEditProperty, value);

        // 标记：是否通过键盘触发了日历打开（需要阻止），鼠标点击日历按钮则放行
        private static bool _suppressCalendarOpen;

        #endregion

        #region 附加属性变更回调

        private static void OnEnableSmartEditChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DatePicker dp) return;
            dp.Loaded -= OnDatePickerLoaded;
            dp.Unloaded -= OnDatePickerUnloaded;
            dp.IsVisibleChanged -= OnDatePickerIsVisibleChanged;
            if ((bool)e.NewValue)
            {
                dp.Loaded += OnDatePickerLoaded;
                dp.Unloaded += OnDatePickerUnloaded;
                dp.IsVisibleChanged += OnDatePickerIsVisibleChanged;
                // 控件已加载且可见时，立即初始化
                if (dp.IsLoaded && dp.IsVisible)
                    OnDatePickerLoaded(dp, new RoutedEventArgs());
            }
        }

        /// <summary>DatePicker 可见性变化时：变为可见时初始化（解决 Collapsed→Visible 场景）</summary>
        private static void OnDatePickerIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not DatePicker dp) return;
            if ((bool)e.NewValue) // 变为可见
            {
                dp.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    OnDatePickerLoaded(dp, new RoutedEventArgs());
                }));
            }
        }

        #endregion

        #region DatePicker 生命周期

        private static void OnDatePickerLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DatePicker dp) return;

            var textBox = FindDatePickerTextBox(dp);
            if (textBox == null) return;

            // 事件绑定
            textBox.PreviewKeyDown -= OnTextBoxPreviewKeyDown;
            textBox.PreviewTextInput -= OnTextBoxPreviewTextInput;
            textBox.GotFocus -= OnTextBoxGotFocus;

            textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
            textBox.PreviewTextInput += OnTextBoxPreviewTextInput;
            textBox.GotFocus += OnTextBoxGotFocus;

            dp.PreviewKeyDown -= OnDpPreviewKeyDown;
            dp.CalendarOpened -= OnCalendarOpened;
            dp.SelectedDateChanged -= OnSelectedDateChanged;

            dp.PreviewKeyDown += OnDpPreviewKeyDown;
            dp.CalendarOpened += OnCalendarOpened;
            dp.SelectedDateChanged += OnSelectedDateChanged;

            // 延迟补零（确保 DatePicker 内部初始化完成后再修改文本）
            dp.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var tb = FindDatePickerTextBox(dp);
                if (tb != null) NormalizeTextBoxText(tb, dp);
            }));
        }

        /// <summary>SelectedDate 变更后，延迟补零（打断同步调用链，防止 StackOverflow）</summary>
        private static void OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not DatePicker dp) return;
            dp.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var textBox = FindDatePickerTextBox(dp);
                if (textBox != null) NormalizeTextBoxText(textBox, dp);
            }));
        }

        /// <summary>将 TextBox 文本强制纠偏为 yyyy/MM/dd 补零格式</summary>
        private static void NormalizeTextBoxText(TextBox textBox, DatePicker dp)
        {
            if (dp.SelectedDate == null) return;

            var expected = dp.SelectedDate.Value.ToString("yyyy/MM/dd");
            if (textBox.Text == expected) return;

            // 保存光标位置
            int caret = textBox.CaretIndex;
            textBox.Text = expected;

            // 恢复光标位置
            if (caret > expected.Length) caret = expected.Length;
            textBox.CaretIndex = caret;
        }

        private static void OnDatePickerUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DatePicker dp) return;
            var textBox = FindDatePickerTextBox(dp);
            if (textBox != null)
            {
                textBox.PreviewKeyDown -= OnTextBoxPreviewKeyDown;
                textBox.PreviewTextInput -= OnTextBoxPreviewTextInput;
                textBox.GotFocus -= OnTextBoxGotFocus;
            }
            dp.PreviewKeyDown -= OnDpPreviewKeyDown;
            dp.CalendarOpened -= OnCalendarOpened;
            dp.SelectedDateChanged -= OnSelectedDateChanged;
            dp.IsVisibleChanged -= OnDatePickerIsVisibleChanged;
        }

        #endregion

        #region 焦点处理

        /// <summary>获得焦点时：取消全选，光标定位到年段首位</summary>
        private static void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            var dp = FindParentDatePicker(textBox);
            if (dp == null) return;

            // 如果没有日期，设置默认日期
            if (dp.SelectedDate == null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                dp.SelectedDate = DateTime.Now;
            }

            // 延迟执行以确保 DatePicker 内部全选逻辑已完成
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (textBox.IsFocused && !string.IsNullOrEmpty(textBox.Text))
                {
                    // 光标定位到年段首位（取消默认全选）
                    var (_, segStart, _) = GetSegmentInfo(textBox.Text, 0);
                    if (segStart >= 0)
                    {
                        textBox.Select(segStart, 0);
                    }
                }
            }));
        }

        /// <summary>日历打开时：仅当由键盘方向键触发时才关闭，鼠标点击日历按钮放行</summary>
        private static void OnCalendarOpened(object sender, RoutedEventArgs e)
        {
            if (_suppressCalendarOpen)
            {
                if (sender is DatePicker dp)
                {
                    dp.IsDropDownOpen = false;
                    e.Handled = true;
                }
                _suppressCalendarOpen = false;
            }
        }

        #endregion

        #region 方向键处理

        /// <summary>
        /// 拦截 DatePicker 级别的按键。
        /// - 方向键：隧道事件从外到内，必须设 e.Handled=true 并手动执行逻辑
        /// - Backspace/Delete：禁止删除日期
        /// </summary>
        private static void OnDpPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not DatePicker dp) return;

            // 禁止删除日期
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                e.Handled = true;
                return;
            }

            if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right)) return;

            var textBox = FindDatePickerTextBox(dp);
            if (textBox == null) return;

            switch (e.Key)
            {
                case Key.Up:
                    HandleUpDown(textBox, 1);
                    e.Handled = true;
                    break;
                case Key.Down:
                    HandleUpDown(textBox, -1);
                    e.Handled = true;
                    break;
                case Key.Left:
                    HandleLeftRight(textBox, forward: false);
                    e.Handled = true;
                    break;
                case Key.Right:
                    HandleLeftRight(textBox, forward: true);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>TextBox 级别方向键：已全部由 OnDpPreviewKeyDown 处理，此方法不再需要</summary>
        private static void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 所有方向键逻辑已在 OnDpPreviewKeyDown 中统一处理
        }

        #endregion

        #region 手动输入覆盖

        private static void OnTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            if (string.IsNullOrEmpty(e.Text)) return;
            if (!char.IsDigit(e.Text[0])) return;

            e.Handled = HandleDigitInput(textBox, e.Text[0]);
        }

        #endregion

        #region 核心算法

        private enum DateSegment { Year, Month, Day, None }

        /// <summary>固定使用 / 作为分隔符（与强制补零格式 yyyy/MM/dd 一致）</summary>
        private static char GetSeparator() => '/';

        /// <summary>固定使用 年/月/日 顺序（与强制补零格式 yyyy/MM/dd 一致）</summary>
        private static List<DateSegment> GetSegmentOrder() =>
            new() { DateSegment.Year, DateSegment.Month, DateSegment.Day };

        /// <summary>根据光标位置判断当前处于哪个日期段</summary>
        private static (DateSegment segment, int segStart, int segEnd) GetSegmentInfo(string text, int caretIndex)
        {
            if (string.IsNullOrEmpty(text)) return (DateSegment.None, -1, -1);

            var sep = GetSeparator();
            var sepPositions = new List<int>();
            for (int i = 0; i < text.Length; i++)
                if (text[i] == sep) sepPositions.Add(i);

            if (sepPositions.Count < 2) return (DateSegment.None, -1, -1);

            var order = GetSegmentOrder();
            if (order.Count < 3) return (DateSegment.None, -1, -1);

            var ranges = new (int start, int end)[]
            {
                (0, sepPositions[0]),
                (sepPositions[0] + 1, sepPositions[1]),
                (sepPositions[1] + 1, text.Length)
            };

            for (int i = 0; i < 3; i++)
            {
                if (caretIndex >= ranges[i].start && caretIndex <= ranges[i].end)
                    return (order[i], ranges[i].start, ranges[i].end);
            }

            return (DateSegment.None, -1, -1);
        }

        /// <summary>查找指定段的文本范围</summary>
        private static (DateSegment segment, int segStart, int segEnd) FindSegmentRange(string text, DateSegment target)
        {
            if (string.IsNullOrEmpty(text)) return (DateSegment.None, -1, -1);

            var sep = GetSeparator();
            var sepPositions = new List<int>();
            for (int i = 0; i < text.Length; i++)
                if (text[i] == sep) sepPositions.Add(i);

            if (sepPositions.Count < 2) return (DateSegment.None, -1, -1);

            var order = GetSegmentOrder();
            if (order.Count < 3) return (DateSegment.None, -1, -1);

            var ranges = new (int start, int end)[]
            {
                (0, sepPositions[0]),
                (sepPositions[0] + 1, sepPositions[1]),
                (sepPositions[1] + 1, text.Length)
            };

            for (int i = 0; i < 3; i++)
            {
                if (order[i] == target)
                    return (target, ranges[i].start, ranges[i].end);
            }

            return (DateSegment.None, -1, -1);
        }

        /// <summary>从文本中提取指定段的整数值</summary>
        private static int GetSegmentValue(string text, int segStart, int segEnd)
        {
            if (segStart < 0 || segEnd < 0 || segStart >= text.Length || segEnd > text.Length) return 0;
            var segText = text.Substring(segStart, segEnd - segStart).TrimStart('0');
            return int.TryParse(segText, out int val) ? val : 0;
        }

        /// <summary>将修改后的段值写回文本</summary>
        private static string SetSegmentValue(string text, int segStart, int segEnd, int value, int digits)
        {
            var strVal = value.ToString().PadLeft(digits, '0');
            if (strVal.Length > segEnd - segStart)
                strVal = strVal.Substring(0, segEnd - segStart);
            return text.Substring(0, segStart) + strVal + text.Substring(segEnd);
        }

        /// <summary>获取段的标准位数</summary>
        private static int GetSegmentDigits(DateSegment segment) => segment switch
        {
            DateSegment.Year => 4,
            DateSegment.Month => 2,
            DateSegment.Day => 2,
            _ => 2
        };

        /// <summary>获取月份的最大天数</summary>
        private static int GetMaxDay(int year, int month)
        {
            if (month < 1 || month > 12) return 31;
            if (year < 1) year = DateTime.Now.Year;
            return DateTime.DaysInMonth(year, month);
        }

        /// <summary>获取有效光标位置（排除选中状态）</summary>
        private static int GetEffectiveCaretIndex(TextBox textBox)
        {
            if (textBox.SelectionLength > 0)
                return textBox.SelectionStart;
            return textBox.CaretIndex;
        }

        /// <summary>上下方向键处理：增减当前段值</summary>
        private static void HandleUpDown(TextBox textBox, int delta)
        {
            var text = textBox.Text;
            if (string.IsNullOrEmpty(text)) return;

            int caret = GetEffectiveCaretIndex(textBox);
            var (segment, segStart, segEnd) = GetSegmentInfo(text, caret);
            if (segment == DateSegment.None) return;

            int value = GetSegmentValue(text, segStart, segEnd);
            int digits = GetSegmentDigits(segment);

            switch (segment)
            {
                case DateSegment.Year:
                    value = Math.Max(1, value + delta);
                    break;
                case DateSegment.Month:
                    value += delta;
                    if (value > 12) value = 1;
                    if (value < 1) value = 12;
                    break;
                case DateSegment.Day:
                    var (_, yStart, yEnd) = FindSegmentRange(text, DateSegment.Year);
                    var (_, mStart, mEnd) = FindSegmentRange(text, DateSegment.Month);
                    int year = yStart >= 0 ? GetSegmentValue(text, yStart, yEnd) : DateTime.Now.Year;
                    int month = mStart >= 0 ? GetSegmentValue(text, mStart, mEnd) : DateTime.Now.Month;
                    int maxDay = GetMaxDay(year, month);
                    value += delta;
                    if (value > maxDay) value = 1;
                    if (value < 1) value = maxDay;
                    break;
            }

            string newText = SetSegmentValue(text, segStart, segEnd, value, digits);
            UpdateTextBox(textBox, newText, segStart, segEnd);
        }

        /// <summary>左右方向键处理：段内移动一位，遇到分隔符边界跳到相邻段首位/末位</summary>
        private static void HandleLeftRight(TextBox textBox, bool forward)
        {
            var text = textBox.Text;
            if (string.IsNullOrEmpty(text)) return;

            var sep = GetSeparator();
            int caret = textBox.CaretIndex;

            if (forward) // Right
            {
                // 光标在分隔符上 → 跳到下一段首位
                if (caret < text.Length && text[caret] == sep)
                {
                    textBox.CaretIndex = caret + 1;
                    return;
                }
                // 光标在段内最后一位（下一个字符是分隔符）→ 跳到下一段首位
                if (caret < text.Length - 1 && text[caret + 1] == sep)
                {
                    textBox.CaretIndex = caret + 2;
                    return;
                }
                // 光标在文本末尾 → 不动
                if (caret >= text.Length) return;
                // 段内普通移动一位
                textBox.CaretIndex = caret + 1;
            }
            else // Left
            {
                // 光标在分隔符后面一位 → 跳到上一段末位（分隔符位置）
                if (caret > 0 && text[caret - 1] == sep)
                {
                    textBox.CaretIndex = caret - 1;
                    return;
                }
                // 光标在段内第一位（前一个字符是分隔符）→ 跳到上一段末位（分隔符前一位）
                if (caret > 1 && text[caret - 2] == sep)
                {
                    textBox.CaretIndex = caret - 2;
                    return;
                }
                // 光标在文本开头 → 不动
                if (caret <= 0) return;
                // 段内普通移动一位
                textBox.CaretIndex = caret - 1;
            }
        }

        /// <summary>数字输入自动覆盖处理</summary>
        private static bool HandleDigitInput(TextBox textBox, char digit)
        {
            var text = textBox.Text;
            if (string.IsNullOrEmpty(text)) return false;

            int caret = GetEffectiveCaretIndex(textBox);
            var (segment, segStart, segEnd) = GetSegmentInfo(text, caret);

            if (segment == DateSegment.None)
            {
                // 光标可能在分隔符上，尝试跳到下一段
                if (caret < text.Length)
                {
                    var nextInfo = GetSegmentInfo(text, caret + 1);
                    if (nextInfo.segment != DateSegment.None)
                    {
                        segment = nextInfo.segment;
                        segStart = nextInfo.segStart;
                        segEnd = nextInfo.segEnd;
                        caret = segStart;
                    }
                    else return false;
                }
                else return false;
            }

            // 如果光标在分隔符上（刚好在段间的分隔符），跳到下一段
            var sep = GetSeparator();
            if (caret < text.Length && text[caret] == sep)
            {
                var nextInfo = GetSegmentInfo(text, caret + 1);
                if (nextInfo.segment != DateSegment.None)
                {
                    segment = nextInfo.segment;
                    segStart = nextInfo.segStart;
                    segEnd = nextInfo.segEnd;
                    caret = segStart;
                }
                else return false;
            }

            int segLen = segEnd - segStart;

            // 如果光标超出当前段范围
            if (caret < segStart || caret >= segEnd)
            {
                // 光标在段末后一位，尝试跳到下一段
                if (caret == segEnd)
                {
                    var nextInfo = GetSegmentInfo(text, Math.Min(caret + 1, text.Length - 1));
                    if (nextInfo.segment != DateSegment.None)
                    {
                        segment = nextInfo.segment;
                        segStart = nextInfo.segStart;
                        segEnd = nextInfo.segEnd;
                        caret = segStart;
                        segLen = segEnd - segStart;
                    }
                    else return false;
                }
                else return false;
            }

            // 覆盖当前光标位置的字符
            char[] chars = text.ToCharArray();
            if (caret >= 0 && caret < chars.Length && chars[caret] != sep)
            {
                chars[caret] = digit;
                string newText = new string(chars);

                // 段值校验
                string segText = newText.Substring(segStart, segLen);
                if (int.TryParse(segText, out int segVal))
                {
                    bool corrected = false;
                    switch (segment)
                    {
                        case DateSegment.Month:
                            if (segVal > 12) { segVal = 12; corrected = true; }
                            if (segVal == 0) { segVal = 1; corrected = true; }
                            break;
                        case DateSegment.Day:
                            var (_, yStart, yEnd) = FindSegmentRange(newText, DateSegment.Year);
                            var (_, mStart, mEnd) = FindSegmentRange(newText, DateSegment.Month);
                            int year = yStart >= 0 ? GetSegmentValue(newText, yStart, yEnd) : DateTime.Now.Year;
                            int month = mStart >= 0 ? GetSegmentValue(newText, mStart, mEnd) : DateTime.Now.Month;
                            int maxDay = GetMaxDay(year, month);
                            if (segVal > maxDay) { segVal = maxDay; corrected = true; }
                            if (segVal == 0) { segVal = 1; corrected = true; }
                            break;
                    }
                    if (corrected)
                    {
                        newText = SetSegmentValue(newText, segStart, segEnd, segVal, segLen);
                    }
                }

                // 计算新光标位置
                int newCaret = caret + 1;

                // 段末自动跳到下一段首位
                if (newCaret >= segEnd && newCaret < newText.Length)
                {
                    newCaret = segEnd + 1; // 跳过分隔符
                }
                if (newCaret > newText.Length)
                    newCaret = newText.Length;

                UpdateTextBox(textBox, newText, newCaret, newCaret);
            }

            return true;
        }

        /// <summary>更新 TextBox 文本并同步 SelectedDate，保护光标位置</summary>
        private static void UpdateTextBox(TextBox textBox, string newText, int selectionStart, int selectionEnd)
        {
            textBox.Text = newText;
            textBox.Select(selectionStart, selectionEnd - selectionStart);

            // 同步到 DatePicker.SelectedDate
            var dp = FindParentDatePicker(textBox);
            if (dp != null && DateTime.TryParseExact(newText, "yyyy/MM/dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                dp.SelectedDate = date;
            }

            // 延迟重新断言光标位置（DatePicker 内部可能在之后重置光标）
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (textBox.IsFocused)
                {
                    textBox.Select(selectionStart, selectionEnd - selectionStart);
                }
            }));
        }

        #endregion

        #region VisualTree 辅助

        private static DatePickerTextBox? FindDatePickerTextBox(DependencyObject parent)
        {
            if (parent is DatePickerTextBox textBox) return textBox;

            int children = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < children; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindDatePickerTextBox(child);
                if (result != null) return result;
            }
            return null;
        }

        private static DatePicker? FindParentDatePicker(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is DatePicker dp) return dp;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        #endregion
    }
}
