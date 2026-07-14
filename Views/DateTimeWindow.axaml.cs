using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

public partial class DateTimeWindow : OverlayWindow
{
    public DateTimeWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is DateTimeWidgetViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            ApplyWidthMode(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not DateTimeWidgetViewModel vm) return;

        if (e.PropertyName == nameof(DateTimeWidgetViewModel.WidthMode))
            ApplyWidthMode(vm);
        else if (e.PropertyName == nameof(DateTimeWidgetViewModel.ScaledWidth) && vm.WidthMode == ClockWidthMode.Fixed)
            Width = vm.ScaledWidth;
        else if (e.PropertyName == nameof(DateTimeWidgetViewModel.ScaledHeight))
            Height = vm.ScaledHeight;
        else if (e.PropertyName == nameof(DateTimeWidgetViewModel.IsCompact))
        {
            // OverlayWindow.ApplyCompact sets width to ScaledWidth when leaving compact mode.
            // Re-apply our width mode so Fixed stays fixed and Auto reverts to content-sizing.
            if (!vm.IsCompact)
                ApplyWidthMode(vm);
        }
    }

    private void ApplyWidthMode(DateTimeWidgetViewModel vm)
    {
        if (vm.WidthMode == ClockWidthMode.Auto)
        {
            SizeToContent = SizeToContent.Width;
            Width = double.NaN;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            Width = vm.ScaledWidth;
        }
    }

    // Hovering temporarily swaps the clock to its hover format pair. When the widget is locked it is
    // click-through, so the OS delivers no pointer events here and the clock stays on the normal pair.
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (DataContext is DateTimeWidgetViewModel vm) vm.IsHovering = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (DataContext is DateTimeWidgetViewModel vm) vm.IsHovering = false;
    }

    // Locking makes the window click-through, so OnPointerExited will never fire to clear a hover that
    // is active at that moment. Drop the hover now so the clock returns to its normal format pair.
    protected override void OnLockChanged(bool locked)
    {
        if (locked && DataContext is DateTimeWidgetViewModel vm) vm.IsHovering = false;
    }
}
