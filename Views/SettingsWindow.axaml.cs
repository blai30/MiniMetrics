using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniMetrics.Lib;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        SetUpSearchableDropDown(LocaleBox, LocaleChevron, LocaleParts);
        SetUpSearchableDropDown(TimeZoneBox, TimeZoneChevron, TimeZoneParts);
    }

    private static (string Display, string Key)? LocaleParts(object? item) =>
        item is CultureInfo culture ? (culture.DisplayName, culture.Name) : null;

    private static (string Display, string Key)? TimeZoneParts(object? item) =>
        item is TimeZoneInfo zone ? (zone.DisplayName, zone.Id) : null;

    // Wires an AutoCompleteBox + chevron to behave like a searchable dropdown: clicking the field or the
    // chevron opens the full list, the current selection stays listed until the user types, typing
    // filters leniently over the item's display name and key, and opening highlights the text so the
    // first keystroke starts a fresh search.
    private void SetUpSearchableDropDown(
        AutoCompleteBox box, Button chevron, Func<object?, (string Display, string Key)?> parts)
    {
        string selectionText = parts(box.SelectedItem)?.Display ?? "";
        box.SelectionChanged += (_, _) => selectionText = parts(box.SelectedItem)?.Display ?? "";

        box.ItemFilter = (search, item) =>
        {
            if (parts(item) is not { } part) return false;

            string query = (search ?? "").Trim();
            return string.Equals(query, selectionText, StringComparison.CurrentCultureIgnoreCase)
                   || FuzzySearch.Matches(part.Display, part.Key, query);
        };

        // Pressing anywhere in the field (not just the chevron) drops the list open like a dropdown.
        box.AddHandler(PointerPressedEvent, (_, _) =>
        {
            if (!box.IsDropDownOpen) OpenDropDown(box);
        }, RoutingStrategies.Tunnel);

        chevron.Click += (_, _) =>
        {
            if (box.IsDropDownOpen)
                box.IsDropDownOpen = false;
            else
                OpenDropDown(box);
        };

        // The Fluent dropdown scrollbar auto-hides and a style selector cannot reach it (it lives in the
        // popup's nested list). Once the list is realized on open, turn auto-hide off directly.
        box.DropDownOpened += (_, _) =>
            Dispatcher.UIThread.Post(() => DisableScrollbarAutoHide(box), DispatcherPriority.Background);
    }

    private static void DisableScrollbarAutoHide(AutoCompleteBox box)
    {
        var popup = box.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
        var scrollViewer = popup?.Child?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        scrollViewer?.AllowAutoHide = false;
    }

    // Focus the field, open the full list, and highlight the current text so the first keystroke starts
    // a fresh search. The select-all is deferred so the field's own click handling does not clear it.
    private static void OpenDropDown(AutoCompleteBox box)
    {
        box.Focus();
        box.IsDropDownOpen = true;
        Dispatcher.UIThread.Post(
            () => box.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.SelectAll(),
            DispatcherPriority.Input);
    }

    // Pressing anything that is not an interactive field drops focus from the current field, so its
    // caret and selection clear and any open dropdown closes.
    private void OnSettingsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual hit && hit.GetSelfAndVisualAncestors().Any(IsField)) return;

        RootArea.Focus();
    }

    private static bool IsField(Visual element) =>
        element is TextBox or AutoCompleteBox or ComboBox or Button or CheckBox or Slider or ToggleSwitch
            or NumericUpDown;

    // The percentage fields apply to the view model only when editing finishes (Enter or losing focus),
    // not on every keystroke, so the widget is not re-rendered mid-typing.
    private void OnPercentInputKeyDown(object? sender, KeyEventArgs e)
    {
        // Commit on Enter by dropping focus, which runs the same path as a normal blur. NumericUpDown only
        // refreshes the displayed text to the clamped value when it loses focus, not from its own Enter
        // handling, so committing through blur keeps an out-of-range entry from lingering on screen.
        if (e.Key == Key.Enter) RootArea.Focus();
    }

    private void OnPercentInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is NumericUpDown input) CommitPercentInput(input);
    }

    // Writes the field's value back to the view model. An empty or unparsed field has a null value; rather
    // than push null into the int-backed setting (which throws), restore the display to the current value.
    private void CommitPercentInput(NumericUpDown input)
    {
        if (DataContext is not SettingsViewModel viewModel) return;

        if (input.Value is { } value)
        {
            int committed = (int)Math.Clamp(value, input.Minimum, input.Maximum);
            switch (input.Name)
            {
                case nameof(OpacityInput):
                    viewModel.Opacity = committed;
                    break;
                case nameof(WidgetScaleInput):
                    viewModel.WidgetScale = committed;
                    break;
            }
        }
        else
        {
            input.Value = input.Name switch
            {
                nameof(OpacityInput) => viewModel.Opacity,
                nameof(WidgetScaleInput) => viewModel.WidgetScale,
                _ => input.Value
            };
        }
    }
}
