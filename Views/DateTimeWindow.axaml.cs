using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

public partial class DateTimeWindow : OverlayWindow
{
    public DateTimeWindow()
    {
        InitializeComponent();

        // The clock defaults hidden and is shown from the tray after it is already ticking, so its
        // content is present before it first opens. Like the GPU widget, that means SizeToContent does
        // not grow it on open; force a measure pass once it is open.
        Opened += (_, _) => Dispatcher.UIThread.Post(ReapplyAutoWidth, DispatcherPriority.Loaded);
    }

    // Apply the current compact state when the view model is attached, and keep reacting to changes.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DateTimeWidgetViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyCompact(viewModel.IsCompact);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DateTimeWidgetViewModel.IsCompact)
            && DataContext is DateTimeWidgetViewModel viewModel)
        {
            ApplyCompact(viewModel.IsCompact);
        }
    }

    // Forces a fresh measure pass so SizeToContent recomputes the width against the realized content.
    private void ReapplyAutoWidth()
    {
        if (DataContext is DateTimeWidgetViewModel { IsCompact: true })
        {
            SizeToContent = SizeToContent.Width;
            InvalidateMeasure();
        }
    }

    // Compact: a single 80px-tall row whose width tracks its content. Full: the fixed clock size.
    private void ApplyCompact(bool compact)
    {
        if (compact)
        {
            SizeToContent = SizeToContent.Width;
            Height = 80;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            Width = 640;
            Height = 176;
        }
    }

    // Hovering temporarily swaps the clock to its hover format pair. When the widget is locked it is
    // click-through, so the OS delivers no pointer events here and the clock stays on the normal pair.
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (DataContext is DateTimeWidgetViewModel vm)
        {
            vm.IsHovering = true;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (DataContext is DateTimeWidgetViewModel vm)
        {
            vm.IsHovering = false;
        }
    }

    // Locking makes the window click-through, so OnPointerExited will never fire to clear a hover that
    // is active at that moment. Drop the hover now so the clock returns to its normal format pair.
    protected override void OnLockChanged(bool locked)
    {
        if (locked && DataContext is DateTimeWidgetViewModel vm)
        {
            vm.IsHovering = false;
        }
    }
}
