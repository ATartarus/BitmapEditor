using System.ComponentModel;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BitmapEditor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            BitmapEditor.PropertyChanged += OnSelectedPixelChanged;

            BindBitmapToDataGrids();
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
                    int colorOffset = 0;
                    if (sender == GreenValue) colorOffset = 1;
                    else if (sender == RedValue) colorOffset = 2;

                    BitmapEditor.ChangePixel(colorOffset, color);
                }
            }
        }

        private void OnBitmapMaskChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox) return;

            int colorOffset = 0;
            if (sender == GreenMask) colorOffset = 1;
            else if (sender == RedMask) colorOffset = 2;

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
                BitmapEditor.Load(filePicker.FileName);
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
                BitmapEditor.Save(filePicker.FileName);
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

        private void BindBitmapToDataGrids()
        {
            byte[] bitmapData = BitmapEditor.RawBitmapData;

            bool black = true;
            for (int i = 0; i < bitmapData.Length; i++)
            {
                if (bitmapData[i] != 0 && bitmapData[i] != 255) black = false;
            }

            if (black)
            {
                ColorMatrices.IsEnabled = false;
                ColorMatrices.Visibility = Visibility.Hidden;
                BlackMatrices.IsEnabled = true;
                BlackMatrices.Visibility = Visibility.Visible;

                DataTable dataTable = new DataTable();

                for (int i = 0; i < BitmapEditor.Bitmap.PixelWidth; i++)
                {
                    dataTable.Columns.Add(null, typeof(byte));
                }

                for (int i = 0; i < BitmapEditor.Bitmap.PixelHeight; i++)
                {
                    DataRow row = dataTable.NewRow();
                    for (int j = 0; j < BitmapEditor.Bitmap.PixelWidth; j++)
                    {
                        row[j] = bitmapData[i * BitmapEditor.Stride + j * BitmapEditor.BytesPerPixel] > 0 ? 0 : 1;
                    }
                    dataTable.Rows.Add(row);
                }

                dataTable.ColumnChanged += (sender, e) =>
                {
                    if (e.ProposedValue is byte color)
                    {
                        BitmapEditor.ChangePixel(0, color == (byte)0 ? (byte)255 : (byte)0);
                        BitmapEditor.ChangePixel(1, color == (byte)0 ? (byte)255 : (byte)0);
                        BitmapEditor.ChangePixel(2, color == (byte)0 ? (byte)255 : (byte)0);
                    }
                };

                BlackMatrix.ItemsSource = dataTable.DefaultView;
            }
            else
            {
                for (int n = 0; n < 3; n++)
                {
                    DataTable dataTable = new DataTable();

                    for (int i = 0; i < BitmapEditor.Bitmap.PixelWidth; i++)
                    {
                        dataTable.Columns.Add(null, typeof(byte));
                    }

                    for (int i = 0; i < BitmapEditor.Bitmap.PixelHeight; i++)
                    {
                        DataRow row = dataTable.NewRow();
                        for (int j = 0; j < BitmapEditor.Bitmap.PixelWidth; j++)
                        {
                            row[j] = bitmapData[i * BitmapEditor.Stride + j * BitmapEditor.BytesPerPixel + n];
                        }
                        dataTable.Rows.Add(row);
                    }

                    int offset = n;
                    dataTable.ColumnChanged += (sender, e) =>
                    {
                        if (e.ProposedValue is byte color)
                        {
                            BitmapEditor.ChangePixel(offset, color);
                        }
                    };

                    switch (n)
                    {
                        case 0:
                            BlueMatrix.ItemsSource = dataTable.DefaultView;
                            break;
                        case 1:
                            GreenMatrix.ItemsSource = dataTable.DefaultView;
                            break;
                        case 2:
                            RedMatrix.ItemsSource = dataTable.DefaultView;
                            break;
                    }
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