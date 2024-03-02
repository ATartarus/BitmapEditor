using System.ComponentModel;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BitmapEditor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            BitmapEditor.PropertyChanged += OnSelectedPixelChanged;

            BindDataGrids();
        }

        private void OnTextInput(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string str = textBox.Text;
                if (e.Key >= Key.D0 && e.Key <= Key.D9) str += (e.Key - Key.D0);
                else if (e.Key == Key.Back && str.Length > 0) str = str.Substring(0, str.Length - 1);
                if (str == "") str = "0";
                if (byte.TryParse(str, out byte color))
                {
                    ColorOffset colorOffset = ColorOffset.Blue;
                    if (sender == GreenValue) colorOffset = ColorOffset.Green;
                    else if (sender == RedValue) colorOffset = ColorOffset.Red;

                    BitmapEditor.ChangePixel(colorOffset, color);
                }
            }
        }

        private void OnBitmapMaskChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox) return;

            ColorOffset colorOffset = ColorOffset.Blue;
            if (sender == GreenMask) colorOffset = ColorOffset.Green;
            else if (sender == RedMask) colorOffset = ColorOffset.Red;

            bool hide = !((CheckBox)sender).IsChecked ?? false;

            BitmapEditor.ChangeMask(colorOffset, hide);
        }

        private void OnOpenFileClick(object sender, RoutedEventArgs e)
        {
            var filePicker = new Microsoft.Win32.OpenFileDialog();
            filePicker.FileName = "Bitmap";
            filePicker.DefaultExt = ".bmp";
            filePicker.Filter = "Bitmap files (.bmp)|*.bmp";

            bool? result = filePicker.ShowDialog();

            if (result == true)
            {
                BitmapEditor.LoadBitmap(filePicker.FileName);
            }
        }

        private void OnSaveFileClick(object sender, RoutedEventArgs e)
        {
            var filePicker = new Microsoft.Win32.SaveFileDialog();
            filePicker.FileName = "Bitmap";
            filePicker.DefaultExt = ".bmp";
            filePicker.Filter = "Bitmap files (.bmp)|*.bmp";

            bool? result = filePicker.ShowDialog();

            if (result == true)
            {
                BitmapEditor.SaveBitmap(filePicker.FileName);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double maxWidth = 0;
            foreach (TabItem tabItem in SideBar.Items)
            {
                FrameworkElement? content = tabItem.Content as FrameworkElement;
                content?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxWidth = Math.Max(maxWidth, content?.DesiredSize.Width ?? 0);
            }

            SideBar.Width = maxWidth;

            foreach (TabItem tabitem in SideBar.Items)
            {
                tabitem.Width = maxWidth / SideBar.Items.Count;
            }
        }

        private void BindDataGrids()
        {
            if (BitmapEditor.IsBitmapBinary)
            {
                ColorMatrices.IsEnabled = false;
                ColorMatrices.Visibility = Visibility.Hidden;
                BlackMatrices.IsEnabled = true;
                BlackMatrices.Visibility = Visibility.Visible;

                BitmapEditor.BindDataGrid(BlackMatrix, ColorOffset.Black);
            }
            else
            {
                ColorMatrices.IsEnabled = true;
                ColorMatrices.Visibility = Visibility.Visible;
                BlackMatrices.IsEnabled = false;
                BlackMatrices.Visibility = Visibility.Hidden;

                DataGrid[] grids = [BlueMatrix, GreenMatrix, RedMatrix];
                for (int i = 0; i < 3; i++)
                {
                    BitmapEditor.BindDataGrid(grids[i], (ColorOffset)i);
                }
            }
        }

        private void DataGrid_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is ScrollViewer scroll)
            {
                if (new DataGrid[] { RedMatrix, GreenMatrix, BlueMatrix }
                    .FirstOrDefault(grid => FindScrollViewer(grid)?.Equals(scroll) ?? false) != null)
                {
                    ScrollGrids(scroll.VerticalOffset, scroll.HorizontalOffset);
                }
            }
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject obj)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is ScrollViewer viewer) return viewer;
                else
                {
                    ScrollViewer? childOfChild = FindScrollViewer(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        private void DataGrid_OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            DataGrid senderGrid = (DataGrid)sender;
            DataGridCellInfo selectedCell = senderGrid.SelectedCells[0];

            int row = senderGrid.Items.IndexOf(selectedCell.Item);
            int col = selectedCell.Column.DisplayIndex;

            SelectGridsCell(row, col);
            BitmapEditor.PropertyChanged -= OnSelectedPixelChanged;
            BitmapEditor.SelectedPixel = (col, row);
            BitmapEditor.PropertyChanged += OnSelectedPixelChanged;
        }

        private void OnSelectedPixelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedPixel")
            {
                SelectGridsCell(BitmapEditor.SelectedPixel.Y, BitmapEditor.SelectedPixel.X);
                ScrollGridsIntoView();
            }
        }

        private void SelectGridsCell(int row, int col)
        {
            DataGrid[] grids;
            if (BlackMatrix.IsEnabled) grids = [BlackMatrix];
            else grids = [RedMatrix, GreenMatrix, BlueMatrix];
            foreach (DataGrid grid in grids)
            {
                grid.SelectedCellsChanged -= DataGrid_OnSelectedCellsChanged;
            }

            foreach (var grid in grids)
            {
                grid.SelectedCells.Clear();
                grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[row], grid.Columns[col]));
            }

            foreach (DataGrid grid in grids)
            {
                grid.SelectedCellsChanged += DataGrid_OnSelectedCellsChanged;
            }
        }

        private void ScrollGrids(double? verticalOffset, double? horizontalOffset)
        {
            DataGrid[] grids;
            if (BlackMatrix.IsEnabled) grids = [BlackMatrix];
            else grids = [RedMatrix, GreenMatrix, BlueMatrix];
            ScrollViewer?[] scrolls = new ScrollViewer[grids.Length];

            for (int i = 0; i < scrolls.Length; i++)
            {
                scrolls[i] = FindScrollViewer(grids[i]);
                if (scrolls[i] != null) scrolls[i].ScrollChanged -= DataGrid_OnScrollChanged;
            }

            foreach (var scroll in scrolls)
            {
                if (verticalOffset != null) scroll?.ScrollToVerticalOffset(verticalOffset.Value);
                if (horizontalOffset != null) scroll?.ScrollToHorizontalOffset(horizontalOffset.Value);
            }

            foreach (var scroll in scrolls)
            {
                if (scroll != null) scroll.ScrollChanged += DataGrid_OnScrollChanged;
            }
        }

        private void ScrollGridsIntoView()
        {
            Size offset = new(
                BitmapEditor.SelectedPixel.X * RedMatrix.ColumnWidth.Value,
                BitmapEditor.SelectedPixel.Y * RedMatrix.RowHeight
            );
            Size half = new(
                RedMatrix.ActualWidth / 2 - RedMatrix.ColumnWidth.Value / 2,
                RedMatrix.ActualHeight / 2 - RedMatrix.RowHeight / 2
            );

            if (offset.Width - half.Width > 0) offset.Width -= half.Width;
            else offset.Width = 0;
            if (offset.Height - half.Height > 0) offset.Height -= half.Height;
            else offset.Height = 0;

            ScrollGrids(offset.Height, offset.Width);
        }

        private void SideBar_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems[0] == MatrixEditorTab)
                BitmapEditor.PropertyChanged += OnSelectedPixelChanged;
            else
                BitmapEditor.PropertyChanged -= OnSelectedPixelChanged;
        }
    }
}