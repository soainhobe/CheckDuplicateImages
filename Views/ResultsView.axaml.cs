using Avalonia.Controls;
using Avalonia.VisualTree;

namespace CheckDuplicate.Views;

public partial class ResultsView : UserControl
{
    private CheckDuplicate.ViewModels.ResultsViewModel? _viewModel;
    private ScrollViewer? _scrollViewer;

    public ResultsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _viewModel = DataContext as CheckDuplicate.ViewModels.ResultsViewModel;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Find the DataGrid
        var dataGrid = this.FindControl<DataGrid>("ResultsGrid"); // Needs x:Name="ResultsGrid" in XAML
        if (dataGrid != null)
        {
            // Wait for template to apply? Or try to find ScrollViewer now.
            // Often ScrollViewer is inside the template.
            // We might need to listen to TemplateApplied or try finding it later.
            // Hack: Use Dispatcher to wait for layout.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                _scrollViewer = dataGrid.MyFindExtensions_FindDescendantOfType<ScrollViewer>();
                if (_scrollViewer != null)
                {
                    _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_viewModel == null || _scrollViewer == null) return;

        // Check if we are near bottom (80-90%)
        if (_scrollViewer.Offset.Y >= (_scrollViewer.Extent.Height - _scrollViewer.Viewport.Height) * 0.9)
        {
             _viewModel.LoadMoreResultsCommand.Execute(null);
        }
    }
}

// Extension helper to find visual child if not available
public static class MyFindExtensions
{
    public static T? MyFindExtensions_FindDescendantOfType<T>(this Avalonia.Controls.Control control) where T : Avalonia.Controls.Control
    {
        return control.FindDescendantOfType<T>();
    }
}
