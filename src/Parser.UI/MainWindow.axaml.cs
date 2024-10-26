using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Parser.Core.Models;
using Parser.Core.Enums;
using Parser.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;

namespace Parser.UI;

public partial class MainWindow : Window
{
    private readonly TextEditor _editor;
    private readonly TreeView _treeView;
    private readonly ObservableCollection<TreeViewItem> _treeItems;
    private readonly ParserService _parserService;
    private readonly TextHighlightService _textHighlightService;
    private readonly SearchService _searchService;
    private DispatcherTimer? _updateTreeViewDebouncer;
    private DocumentFormat _currentFormat = DocumentFormat.Unknown;
    private string _currentFilePath = string.Empty;
    private string _lastSearchQuery = string.Empty;
    private bool _isEditMode;
    private TreeViewItem? _currentlyEditingItem;

    public MainWindow()
    {
        InitializeComponent();

        _editor = this.FindControl<TextEditor>("Editor");
        _treeView = this.FindControl<TreeView>("DocumentTreeView");
        _treeItems = new ObservableCollection<TreeViewItem>();
        _parserService = new ParserService();
        _textHighlightService = new TextHighlightService();
        _searchService = new SearchService(new SearchOptions());

        _treeView.ItemsSource = _treeItems;
        var editModeButton = this.FindControl<Button>("EditModeButton");
        editModeButton.Click += OnEditModeToggle;

        InitializeEditor();
        InitializeEvents();
        InitializeSearchControls();
    }

    private void InitializeEditor()
    {
        _editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JSON");
        _editor.ShowLineNumbers = true;
        _editor.WordWrap = true;
        _editor.TextArea.TextView.LineTransformers.Add(_textHighlightService.GetTransformer());
        _editor.TextChanged += OnEditorTextChanged;
    }

    private void InitializeEvents()
    {
        var openFileButton = this.FindControl<Button>("OpenFileButton");
        openFileButton.Click += async (s, e) => await OpenFileAsync();

        var saveFileButton = this.FindControl<Button>("SaveFileButton");
        saveFileButton.Click += async (s, e) => await SaveFileAsync();

        var syntaxModeComboBox = this.FindControl<ComboBox>("SyntaxModeComboBox");
        syntaxModeComboBox.SelectionChanged += OnSyntaxModeChanged;
    }

    private void InitializeSearchControls()
    {
        var searchBox = this.FindControl<TextBox>("SearchBox");
        searchBox.TextChanged += OnSearchTextChanged;
        searchBox.KeyDown += OnSearchBoxKeyDown;

        var matchCaseButton = this.FindControl<ToggleButton>("MatchCaseButton");
        matchCaseButton.IsCheckedChanged += OnSearchOptionsChanged;

        var regexButton = this.FindControl<ToggleButton>("RegexButton");
        regexButton.IsCheckedChanged += OnSearchOptionsChanged;

        var searchValuesButton = this.FindControl<ToggleButton>("SearchValuesButton");
        searchValuesButton.IsCheckedChanged += OnSearchOptionsChanged;

        var searchNamesButton = this.FindControl<ToggleButton>("SearchNamesButton");
        searchNamesButton.IsCheckedChanged += OnSearchOptionsChanged;

        var searchAttributesButton = this.FindControl<ToggleButton>("SearchAttributesButton");
        searchAttributesButton.IsCheckedChanged += OnSearchOptionsChanged;
    }

    private async Task OpenFileAsync()
    {
        var filePickerOptions = new FilePickerOpenOptions
        {
            Title = "Open JSON/XML File",
            AllowMultiple = false,
            FileTypeFilter = new FilePickerFileType[]
            {
                new("All Supported Files") { Patterns = new[] { "*.json", "*.xml" } },
                new("JSON Files") { Patterns = new[] { "*.json" } },
                new("XML Files") { Patterns = new[] { "*.xml" } }
            }
        };

        var result = await StorageProvider.OpenFilePickerAsync(filePickerOptions);
        if (result.Count > 0)
        {
            var file = result[0];
            _currentFilePath = file.Path.LocalPath;
            var content = await File.ReadAllTextAsync(_currentFilePath);

            try
            {
                _currentFormat = _parserService.GetDocumentFormat(content);
                UpdateSyntaxHighlighting(_currentFormat);
                var formattedContent = _parserService.FormatDocument(content);
                _editor.Text = formattedContent;
                await UpdateTreeViewAsync(formattedContent);
                ClearHighlights();
            }
            catch (Exception ex)
            {
                var msbox = MessageBoxManager
                    .GetMessageBoxStandard(
                        "Error Opening File",
                        $"Failed to open the file: {ex.Message}");
                await msbox.ShowAsync();
            }
        }
    }

    private async Task SaveFileAsync()
    {
        try
        {
            var defaultExtension = _currentFormat switch
            {
                DocumentFormat.Json => ".json",
                DocumentFormat.Xml => ".xml",
                _ => ".json"
            };

            var suggestedFileName = string.Empty;
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(_currentFilePath);
                suggestedFileName = nameWithoutExt + defaultExtension;
            }

            var filePickerOptions = new FilePickerSaveOptions
            {
                Title = "Save File",
                DefaultExtension = defaultExtension,
                SuggestedFileName = suggestedFileName,
                FileTypeChoices = new[]{
                    _currentFormat switch {
                        DocumentFormat.Xml => new FilePickerFileType("XML File") { Patterns = new[] { "*.xml" } },
                        _ => new FilePickerFileType("JSON File") { Patterns = new[] { "*.json" } }
                    }
                }
            };

            var result = await StorageProvider.SaveFilePickerAsync(filePickerOptions);
            if (result != null)
            {
                var content = _editor.Text;
                var extension = Path.GetExtension(result.Path.LocalPath).ToLower();
                var targetFormat = extension switch
                {
                    ".json" => DocumentFormat.Json,
                    ".xml" => DocumentFormat.Xml,
                    _ => _currentFormat
                };

                if (targetFormat != _currentFormat && _currentFormat != DocumentFormat.Unknown)
                {
                    var msbox = MessageBoxManager
                        .GetMessageBoxStandard(
                            "Format Mismatch",
                            "Converting between formats is not supported. Please save in the original format.");
                    await msbox.ShowAsync();
                    return;
                }

                try
                {
                    var formattedContent = _parserService.FormatDocument(content);
                    await File.WriteAllTextAsync(result.Path.LocalPath, formattedContent);
                    _currentFilePath = result.Path.LocalPath;
                    _currentFormat = targetFormat;
                    UpdateSyntaxHighlighting(_currentFormat);
                }
                catch (Exception ex)
                {
                    var msbox = MessageBoxManager.GetMessageBoxStandard("Error Saving File", $"Failed to save the file: {ex.Message}");
                    await msbox.ShowAsync();
                }
            }
        }
        catch (Exception ex)
        {
            var msbox = MessageBoxManager.GetMessageBoxStandard("Error", $"An unexpected error occurred: {ex.Message}");
            await msbox.ShowAsync();
        }
    }
    private Dictionary<string, bool> CaptureExpansionStates(IEnumerable<TreeViewItem> items, string parentPath = "")
    {
        var states = new Dictionary<string, bool>();

        foreach (var item in items)
        {
            if (item.DataContext is DocumentNode node)
            {
                var currentPath = string.IsNullOrEmpty(parentPath) ?
                    node.Name :
                    $"{parentPath}/{node.Name}";

                states[currentPath] = item.IsExpanded;

                if (item.Items != null)
                {
                    var childStates = CaptureExpansionStates(
                        item.Items.Cast<TreeViewItem>(),
                        currentPath);

                    foreach (var childState in childStates)
                    {
                        states[childState.Key] = childState.Value;
                    }
                }
            }
        }

        return states;
    }

    private void RestoreExpansionStates(IEnumerable<TreeViewItem> items, Dictionary<string, bool> states, string parentPath = "")
    {
        foreach (var item in items)
        {
            if (item.DataContext is DocumentNode node)
            {
                var currentPath = string.IsNullOrEmpty(parentPath) ?
                    node.Name :
                    $"{parentPath}/{node.Name}";

                if (states.TryGetValue(currentPath, out bool isExpanded))
                {
                    item.IsExpanded = isExpanded;
                }

                if (item.Items != null)
                {
                    RestoreExpansionStates(
                        item.Items.Cast<TreeViewItem>(),
                        states,
                        currentPath);
                }
            }
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_updateTreeViewDebouncer?.IsEnabled == true)
        {
            _updateTreeViewDebouncer.Stop();
        }
        _updateTreeViewDebouncer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _updateTreeViewDebouncer.Tick += async (s, e) =>
        {
            var expansionStates = CaptureExpansionStates(_treeItems);

            await UpdateTreeViewAsync(_editor.Text);

            RestoreExpansionStates(_treeItems, expansionStates);

            if (!string.IsNullOrWhiteSpace(_lastSearchQuery))
            {
                var searchPattern = BuildSearchPattern(_lastSearchQuery);
                UpdateTreeViewHighlights(searchPattern);
            }

            _updateTreeViewDebouncer.Stop();
        };
        _updateTreeViewDebouncer.Start();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox searchBox)
        {
            _lastSearchQuery = searchBox.Text ?? string.Empty;
            PerformSearch();
        }
    }

    private void OnSearchOptionsChanged(object? sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (sender is TextBox searchBox)
            {
                searchBox.Text = string.Empty;
                e.Handled = true;
            }
        }
    }

    private void UpdateSyntaxHighlighting(DocumentFormat format)
    {
        var syntaxName = format switch
        {
            DocumentFormat.Json => "JSON",
            DocumentFormat.Xml => "XML",
            _ => "JSON"
        };

        _editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(syntaxName);

        var syntaxModeComboBox = this.FindControl<ComboBox>("SyntaxModeComboBox");
        if (syntaxModeComboBox != null)
        {
            var items = syntaxModeComboBox.Items.Cast<ComboBoxItem>();
            var targetItem = items.FirstOrDefault(i => i.Content.ToString() == syntaxName);
            if (targetItem != null)
            {
                syntaxModeComboBox.SelectedItem = targetItem;
            }
        }
    }

    private void OnSyntaxModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            var selectedItem = comboBox.SelectedItem as ComboBoxItem;
            var syntax = selectedItem?.Content.ToString();

            if (syntax != null)
            {
                _editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(syntax);
                _currentFormat = syntax.ToUpperInvariant() switch
                {
                    "JSON" => DocumentFormat.Json,
                    "XML" => DocumentFormat.Xml,
                    _ => DocumentFormat.Unknown
                };
            }
        }
    }

    private void PerformSearch()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_lastSearchQuery))
            {
                _textHighlightService.ClearHighlights();
                _editor.TextArea.TextView.InvalidateVisual();
                UpdateTreeViewHighlights(null);
                return;
            }

            var searchPattern = BuildSearchPattern(_lastSearchQuery);

            _textHighlightService.HighlightMatches(_editor.Text, searchPattern);
            _editor.TextArea.TextView.InvalidateVisual();

            UpdateTreeViewHighlights(searchPattern);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Search error: {ex.Message}");
        }
    }

    private void ClearHighlights()
    {
        _textHighlightService.ClearHighlights();
        _editor.TextArea.TextView.InvalidateVisual();
        UpdateTreeViewHighlights(new Regex(".*"));
    }

    private async Task UpdateTreeViewAsync(string content)
    {
        try
        {
            _treeItems.Clear();

            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var documentNode = await _parserService.ParseDocumentAsync(content);
            var rootNode = CreateTreeViewItem(documentNode, false);
            _treeItems.Add(rootNode);

            if (_isEditMode)
            {
                UpdateTreeEditMode(_treeItems);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating tree view: {ex.Message}");
            _treeItems.Add(new TreeViewItem { Header = "Error parsing document" });
        }
    }

    private void UpdateTreeItemHighlights(IEnumerable<TreeViewItem> items, Regex? searchPattern)
    {
        foreach (var item in items)
        {
            if (item.DataContext is DocumentNode node)
            {
                bool isMatch = searchPattern != null && IsNodeMatch(node, searchPattern);

                item.Header = searchPattern != null ?
                    BuildHighlightedNodeHeader(node, searchPattern) :
                    BuildNodeHeader(node);

                item.Background = (searchPattern != null && isMatch) ?
                    new SolidColorBrush(Color.FromRgb(255, 249, 217), 0.5) :
                    null;

                if (item.Items != null)
                {
                    UpdateTreeItemHighlights(item.Items.Cast<TreeViewItem>(), searchPattern);
                }
            }
        }
    }

    private void UpdateTreeViewHighlights(Regex? searchPattern)
    {
        UpdateTreeItemHighlights(_treeItems, searchPattern);
    }

    private TreeViewItem CreateTreeViewItem(DocumentNode node, bool isHighlighted)
    {
        var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        textBlock.Text = BuildNodeHeader(node);

        var panel = new DockPanel();

        var item = new TreeViewItem
        {
            DataContext = node,
            IsExpanded = true
        };

        var editButton = new Button
        {
            Content = "✎",
            IsVisible = false,
            Margin = new Thickness(2),
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center
        };
        editButton.Classes.Add("tree-edit-button");
        editButton.Click += (s, e) => StartEditing(item);
        DockPanel.SetDock(editButton, Dock.Right);

        var deleteButton = new Button
        {
            Content = "🗑️",
            IsVisible = false,
            Margin = new Thickness(2),
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center
        };
        deleteButton.Classes.Add("tree-delete-button");
        deleteButton.Click += (s, e) => DeleteNode(item);
        DockPanel.SetDock(deleteButton, Dock.Right);
        panel.Children.Add(editButton);
        panel.Children.Add(deleteButton);
        panel.Children.Add(textBlock);

        item.Header = panel;

        foreach (var child in node.Children)
        {
            item.Items.Add(CreateTreeViewItem(child, isHighlighted));
        }

        return item;
    }

    private TreeViewItem CreateHighlightedTreeViewItem(DocumentNode node, Regex searchPattern)
    {
        var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        textBlock.Inlines = BuildHighlightedNodeHeader(node, searchPattern).Inlines;

        var panel = new DockPanel();
        var isMatch = IsNodeMatch(node, searchPattern);

        var item = new TreeViewItem
        {
            DataContext = node,
            IsExpanded = isMatch,
            Background = isMatch ? new SolidColorBrush(Color.FromRgb(255, 249, 217), 0.5) : null
        };

        var editButton = new Button
        {
            Content = "✎",
            IsVisible = false,
            Margin = new Thickness(2),
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center
        };
        editButton.Classes.Add("tree-edit-button");
        editButton.Click += (s, e) => StartEditing(item);
        DockPanel.SetDock(editButton, Dock.Right);

        var deleteButton = new Button
        {
            Content = "🗑️",
            IsVisible = _isEditMode,
            Margin = new Thickness(2)
        };
        deleteButton.Classes.Add("tree-delete-button");
        deleteButton.Click += (s, e) => DeleteNode(item);
        DockPanel.SetDock(deleteButton, Dock.Right);

        panel.Children.Add(editButton);
        panel.Children.Add(deleteButton);
        panel.Children.Add(textBlock);

        item.Header = panel;

        foreach (var child in node.Children)
        {
            item.Items.Add(CreateHighlightedTreeViewItem(child, searchPattern));
        }

        return item;
    }

    private bool IsNodeMatch(DocumentNode node, Regex searchPattern)
    {
        var searchOptions = GetCurrentSearchOptions();

        if (searchOptions.SearchInNames && searchPattern.IsMatch(node.Name))
            return true;

        if (searchOptions.SearchInValues && !string.IsNullOrEmpty(node.Value)
            && searchPattern.IsMatch(node.Value))
            return true;

        if (searchOptions.SearchInAttributes && node.Attributes.Any(attr =>
            searchPattern.IsMatch(attr.Key) || searchPattern.IsMatch(attr.Value)))
            return true;

        return false;
    }

    private string BuildNodeHeader(DocumentNode node)
    {
        var header = new StringBuilder();

        if (node.Name.StartsWith("[") && node.Name.EndsWith("]"))
        {
            header.Append($"Item {node.Name}");
        }
        else
        {
            header.Append(node.Name);
        }

        if (!string.IsNullOrEmpty(node.Value))
        {
            var displayValue = node.Value.Trim('"');
            if (displayValue.Length > 50)
            {
                displayValue = displayValue.Substring(0, 47) + "...";
            }
            header.Append($": {displayValue}");
        }

        if (node.Attributes.Any())
        {
            header.Append(" [");
            header.Append(string.Join(", ",
                node.Attributes.Select(a => $"{a.Key}=\"{a.Value}\"")));
            header.Append("]");
        }

        return header.ToString();
    }


    private TextBlock BuildHighlightedNodeHeader(DocumentNode node, Regex searchPattern)
    {
        var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var inlines = new InlineCollection();

        void AddHighlightedText(string text, bool isMatchCandidate)
        {
            if (!isMatchCandidate || !GetCurrentSearchOptions().SearchInValues)
            {
                inlines.Add(new Run(text));
                return;
            }

            var matches = searchPattern.Matches(text);
            int currentIndex = 0;

            foreach (Match match in matches)
            {
                if (match.Index > currentIndex)
                {
                    inlines.Add(new Run(text.Substring(currentIndex, match.Index - currentIndex)));
                }

                var highlightedRun = new Run(match.Value)
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 236, 143), 0.3),
                    FontWeight = FontWeight.Normal
                };
                inlines.Add(highlightedRun);

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < text.Length)
            {
                inlines.Add(new Run(text.Substring(currentIndex)));
            }
        }

        if (!string.IsNullOrEmpty(node.Name))
        {
            var isArrayItem = node.Name.StartsWith("[") && node.Name.EndsWith("]");
            if (isArrayItem)
            {
                inlines.Add(new Run("Item "));
                AddHighlightedText(node.Name, GetCurrentSearchOptions().SearchInNames);
            }
            else
            {
                AddHighlightedText(node.Name, GetCurrentSearchOptions().SearchInNames);
            }
        }

        if (!string.IsNullOrEmpty(node.Value))
        {
            inlines.Add(new Run(": "));
            AddHighlightedText(node.Value.Trim('"'), GetCurrentSearchOptions().SearchInValues);
        }

        if (node.Attributes.Any())
        {
            inlines.Add(new Run(" ["));
            var isFirst = true;
            foreach (var attr in node.Attributes)
            {
                if (!isFirst)
                    inlines.Add(new Run(", "));

                AddHighlightedText(attr.Key, GetCurrentSearchOptions().SearchInAttributes);
                inlines.Add(new Run("=\""));
                AddHighlightedText(attr.Value, GetCurrentSearchOptions().SearchInAttributes);
                inlines.Add(new Run("\""));
                isFirst = false;
            }
            inlines.Add(new Run("]"));
        }

        textBlock.Inlines = inlines;
        return textBlock;
    }

    private SearchOptions GetCurrentSearchOptions()
    {
        var matchCaseButton = this.FindControl<ToggleButton>("MatchCaseButton");
        var regexButton = this.FindControl<ToggleButton>("RegexButton");
        var searchValuesButton = this.FindControl<ToggleButton>("SearchValuesButton");
        var searchNamesButton = this.FindControl<ToggleButton>("SearchNamesButton");
        var searchAttributesButton = this.FindControl<ToggleButton>("SearchAttributesButton");

        return new SearchOptions
        {
            MatchCase = matchCaseButton?.IsChecked ?? false,
            UseRegex = regexButton?.IsChecked ?? false,
            SearchInValues = searchValuesButton?.IsChecked ?? true,
            SearchInNames = searchNamesButton?.IsChecked ?? true,
            SearchInAttributes = searchAttributesButton?.IsChecked ?? true
        };
    }

    private Regex BuildSearchPattern(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new Regex(".*");

        var options = GetCurrentSearchOptions();
        var processedQuery = query;

        if (!options.UseRegex)
        {
            processedQuery = Regex.Replace(processedQuery, "\"([^\"]*)\"", match =>
            {
                var phrase = match.Groups[1].Value;
                return Regex.Escape(phrase);
            });

            processedQuery = processedQuery.Replace("*", ".*");

            processedQuery = string.Join(".*",
                processedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                             .Select(Regex.Escape));
        }

        var regexOptions = options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
        return new Regex(processedQuery, regexOptions);
    }


    private void UpdateItemEditMode(TreeViewItem item)
    {
        if (item.Header is DockPanel existingPanel)
        {
            foreach (var child in existingPanel.Children)
            {
                if (child is Button button &&
                    (button.Classes.Contains("tree-edit-button") ||
                     button.Classes.Contains("tree-delete-button")))
                {
                    button.IsVisible = _isEditMode;
                }
            }
            return;
        }

        if (item.Header is TextBlock textBlock)
        {
            var panel = new DockPanel();

            var editButton = new Button
            {
                Content = "✎",
                IsVisible = _isEditMode,
                Margin = new Thickness(2)
            };
            editButton.Classes.Add("tree-edit-button");
            editButton.Click += (s, e) => StartEditing(item);
            DockPanel.SetDock(editButton, Dock.Right);

            var deleteButton = new Button
            {
                Content = "🗑️",
                IsVisible = _isEditMode,
                Margin = new Thickness(2)
            };
            deleteButton.Classes.Add("tree-delete-button");
            deleteButton.Click += (s, e) => DeleteNode(item);
            DockPanel.SetDock(deleteButton, Dock.Right);

            panel.Children.Add(editButton);
            panel.Children.Add(deleteButton);
            panel.Children.Add(textBlock);

            item.Header = panel;
        }
    }



    private string SerializeToJson(DocumentNode node)
    {
        var jsonObj = new
        {
            name = node.Name,
            value = node.Value,
            type = node.Type,
            attributes = node.Attributes,
            children = node.Children.Select(c => SerializeToJson(c))
        };
        return System.Text.Json.JsonSerializer.Serialize(jsonObj, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private string SerializeToXml(DocumentNode node)
    {
        var doc = new System.Xml.Linq.XDocument();
        var element = new System.Xml.Linq.XElement(node.Name);

        foreach (var attr in node.Attributes)
        {
            element.Add(new System.Xml.Linq.XAttribute(attr.Key, attr.Value));
        }

        if (node.Children.Any())
        {
            foreach (var child in node.Children)
            {
                element.Add(System.Xml.Linq.XElement.Parse(SerializeToXml(child)));
            }
        }
        else if (!string.IsNullOrEmpty(node.Value))
        {
            element.Value = node.Value;
        }

        doc.Add(element);
        return doc.ToString();
    }
    private void UpdateDocument()
    {
        var rootNode = (_treeItems.FirstOrDefault()?.DataContext as DocumentNode)!;
        string updatedContent = _currentFormat switch
        {
            DocumentFormat.Json => SerializeToJson(rootNode),
            DocumentFormat.Xml => SerializeToXml(rootNode),
            _ => _editor.Text
        };

        _editor.Text = updatedContent;
    }

    private void StartEditing(TreeViewItem item)
    {
        if (item.DataContext is not DocumentNode node)
            return;

        var originalHeader = item.Header;
        _currentlyEditingItem = item;

        var editPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4) };

        var nameEditor = new TextBox
        {
            Text = node.Name,
            Watermark = "Name",
            Margin = new Thickness(0, 2)
        };

        var valueEditor = new TextBox
        {
            Text = node.Value,
            Watermark = "Value",
            Margin = new Thickness(0, 2)
        };

        var attributesPanel = new StackPanel { Orientation = Orientation.Vertical };
        foreach (var attr in node.Attributes)
        {
            var attrPanel = new DockPanel { Margin = new Thickness(0, 2) };
            var keyBox = new TextBox { Text = attr.Key, Width = 100 };
            var valueBox = new TextBox { Text = attr.Value };
            var deleteAttrButton = new Button
            {
                Content = "×",
                Width = 20,
                Height = 20
            };
            deleteAttrButton.Classes.Add("delete-attr-button");

            DockPanel.SetDock(keyBox, Dock.Left);
            DockPanel.SetDock(deleteAttrButton, Dock.Right);
            attrPanel.Children.Add(deleteAttrButton);
            attrPanel.Children.Add(keyBox);
            attrPanel.Children.Add(valueBox);

            deleteAttrButton.Click += (s, e) =>
            {
                node.Attributes.Remove(attr.Key);
                attributesPanel.Children.Remove(attrPanel);
                UpdateDocument();
            };

            attributesPanel.Children.Add(attrPanel);
        }

        var addAttrButton = new Button
        {
            Content = "Add Attribute",
            Margin = new Thickness(0, 2)
        };
        addAttrButton.Classes.Add("add-attr-button");
        addAttrButton.Click += (s, e) =>
        {
            var attrPanel = new DockPanel { Margin = new Thickness(0, 2) };
            var keyBox = new TextBox { Text = "key", Width = 100 };
            var valueBox = new TextBox { Text = "value" };
            var deleteAttrButton = new Button
            {
                Content = "×",
                Width = 20,
                Height = 20
            };
            deleteAttrButton.Classes.Add("delete-attr-button");

            DockPanel.SetDock(keyBox, Dock.Left);
            DockPanel.SetDock(deleteAttrButton, Dock.Right);
            attrPanel.Children.Add(deleteAttrButton);
            attrPanel.Children.Add(keyBox);
            attrPanel.Children.Add(valueBox);

            deleteAttrButton.Click += (s, e) =>
            {
                attributesPanel.Children.Remove(attrPanel);
            };

            attributesPanel.Children.Add(attrPanel);
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4)
        };

        var saveButton = new Button { Content = "Save" };
        var cancelButton = new Button { Content = "Cancel" };

        saveButton.Click += (s, e) =>
        {
            node.Name = nameEditor.Text;
            node.Value = valueEditor.Text;

            node.Attributes.Clear();
            foreach (var attrPanel in attributesPanel.Children.OfType<DockPanel>())
            {
                var children = attrPanel.Children.ToList();
                var key = ((TextBox)children[1]).Text;
                var value = ((TextBox)children[2]).Text;
                node.Attributes[key] = value;
            }

            FinishEditing();
            UpdateDocument();
        };

        cancelButton.Click += (s, e) =>
        {
            item.Header = originalHeader;
            _currentlyEditingItem = null;
        };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);

        editPanel.Children.Add(nameEditor);
        editPanel.Children.Add(valueEditor);
        editPanel.Children.Add(new TextBlock
        {
            Text = "Attributes:",
            Margin = new Thickness(0, 8, 0, 2),
            FontWeight = FontWeight.Bold
        });
        editPanel.Children.Add(attributesPanel);
        editPanel.Children.Add(addAttrButton);
        editPanel.Children.Add(buttonPanel);

        item.Header = editPanel;
    }

    private void FinishEditing()
    {
        if (_currentlyEditingItem?.DataContext is DocumentNode node)
        {
            var item = _currentlyEditingItem;
            _currentlyEditingItem = null;

            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
            textBlock.Text = BuildNodeHeader(node);

            var panel = new DockPanel();

            var editButton = new Button
            {
                Content = "✎",
                IsVisible = _isEditMode,
                Margin = new Thickness(2),
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center
            };
            editButton.Classes.Add("tree-edit-button");
            editButton.Click += (s, e) => StartEditing(item);
            DockPanel.SetDock(editButton, Dock.Right);

            var deleteButton = new Button
            {
                Content = "🗑️",
                IsVisible = _isEditMode,
                Margin = new Thickness(2),
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center
            };
            deleteButton.Classes.Add("tree-delete-button");
            deleteButton.Click += (s, e) => DeleteNode(item);
            DockPanel.SetDock(deleteButton, Dock.Right);

            panel.Children.Add(editButton);
            panel.Children.Add(deleteButton);
            panel.Children.Add(textBlock);

            item.Header = panel;
        }
    }

    private void DeleteNode(TreeViewItem item)
    {
        if (item.DataContext is not DocumentNode node)
            return;

        var parent = item.Parent as TreeViewItem;
        if (parent?.DataContext is DocumentNode parentNode)
        {
            parentNode.Children.Remove(node);
            parent.Items.Remove(item);
            UpdateDocument();
        }
    }

    private void UpdateAllTreeItemsVisibility(IEnumerable<TreeViewItem> items)
    {
        foreach (var item in items)
        {
            if (item.Header is DockPanel panel)
            {
                foreach (var control in panel.Children)
                {
                    if (control is Button button)
                    {
                        if (button.Classes.Contains("tree-edit-button") ||
                            button.Classes.Contains("tree-delete-button"))
                        {
                            button.IsVisible = _isEditMode;
                        }
                    }
                }
            }

            if (item.Items != null)
            {
                UpdateAllTreeItemsVisibility(item.Items.Cast<TreeViewItem>());
            }
        }
    }

    private void OnEditModeToggle(object? sender, RoutedEventArgs e)
    {
        _isEditMode = !_isEditMode;
        var editButton = (Button)sender!;
        var editText = this.FindControl<TextBlock>("EditModeText");

        if (_isEditMode)
        {
            editButton.Classes.Add("active");
            editText.Text = "Edit Mode (On)";
        }
        else
        {
            editButton.Classes.Remove("active");
            editText.Text = "Edit Mode";

            if (_currentlyEditingItem != null)
            {
                FinishEditing();
            }
        }

        UpdateAllTreeItemsVisibility(_treeItems);
    }


    private void UpdateTreeEditMode(IEnumerable<TreeViewItem> items)
    {
        foreach (var item in items)
        {
            UpdateItemEditMode(item);
            if (item.Items != null)
            {
                UpdateTreeEditMode(item.Items.Cast<TreeViewItem>());
            }
        }
    }

}