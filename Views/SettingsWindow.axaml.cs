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
            if (parts(item) is not { } part)
            {
                return false;
            }

            string query = (search ?? "").Trim();
            return string.Equals(query, selectionText, StringComparison.CurrentCultureIgnoreCase)
                || FuzzySearch.Matches(part.Display, part.Key, query);
        };

        // Pressing anywhere in the field (not just the chevron) drops the list open like a dropdown.
        box.AddHandler(InputElement.PointerPressedEvent, (_, _) =>
        {
            if (!box.IsDropDownOpen)
            {
                OpenDropDown(box);
            }
        }, RoutingStrategies.Tunnel);

        chevron.Click += (_, _) =>
        {
            if (box.IsDropDownOpen)
            {
                box.IsDropDownOpen = false;
            }
            else
            {
                OpenDropDown(box);
            }
        };

        // The Fluent dropdown scrollbar auto-hides and a style selector cannot reach it (it lives in the
        // popup's nested list). Once the list is realized on open, turn auto-hide off directly.
        box.DropDownOpened += (_, _) =>
            Dispatcher.UIThread.Post(() => DisableScrollbarAutoHide(box), DispatcherPriority.Background);
    }

    private static void DisableScrollbarAutoHide(AutoCompleteBox box)
    {
        Popup? popup = box.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
        ScrollViewer? scrollViewer = popup?.Child?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer is not null)
        {
            scrollViewer.AllowAutoHide = false;
        }
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
        if (e.Source is Visual hit && hit.GetSelfAndVisualAncestors().Any(IsField))
        {
            return;
        }

        RootArea.Focus();
    }

    private static bool IsField(Visual element) =>
        element is TextBox or AutoCompleteBox or ComboBox or Button or CheckBox or Slider or ToggleSwitch;
}
