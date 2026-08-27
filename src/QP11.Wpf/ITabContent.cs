using System;

namespace QP11.Wpf;

public interface ITabContent
{
    string TabTitle { get; }
    bool HasUnsavedChanges { get; }
    event EventHandler RequestClose;
    void OnAdd();
    void OnEdit();
    void OnQuery();
    void OnDelete();
    void OnSave();
    void OnSettle();
    void OnPrint();
    void OnReturn();
    void OnCancel();
    void OnHistory();
    void OnClose();
}
