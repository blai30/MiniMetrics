using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

public partial class MetricWidgetWindow : OverlayWindow
{
    public MetricWidgetWindow()
    {
        InitializeComponent();

        // A widget whose content is already present when it first opens (the GPU widget, shown
        // reactively once a snapshot confirms a GPU) opens at its explicit width and gets no later
        // measure pass, so SizeToContent never grows it. Force one once it is open.
        Opened += (_, _) => Dispatcher.UIThread.Post(ReapplyAutoWidth, DispatcherPriority.Loaded);
    }

    // Apply the current compact state when the view model is attached, and keep reacting to changes.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MetricWidgetViewModel viewModel) return;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyCompact(viewModel.IsCompact);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MetricWidgetViewModel viewModel) return;

        if (e.PropertyName == nameof(MetricWidgetViewModel.IsCompact))
            ApplyCompact(viewModel.IsCompact);
        else if (viewModel.IsCompact
                 && (e.PropertyName == nameof(MetricWidgetViewModel.Compute)
                     || e.PropertyName == nameof(MetricWidgetViewModel.Memory)))
            // The rows changed membership (e.g. the GPU widget's rows arrived on the first snapshot);
            // re-fit the width once the new content has been laid out.
            Dispatcher.UIThread.Post(ReapplyAutoWidth, DispatcherPriority.Loaded);
    }

    // Forces a fresh measure pass so SizeToContent recomputes the width against the realized content.
    private void ReapplyAutoWidth()
    {
        if (DataContext is not MetricWidgetViewModel { IsCompact: true }) return;
        SizeToContent = SizeToContent.Width;
        InvalidateMeasure();
    }

    // Compact: a single 80px-tall row whose width tracks its content. Full: the fixed two-card size.
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
            Width = 210;
            Height = 176;
        }
    }
}
