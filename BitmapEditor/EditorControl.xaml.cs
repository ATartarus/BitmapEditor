using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace BitmapEditor
{
    public partial class EditorControl : UserControl, INotifyPropertyChanged
    {
        private BitmapModel bitmapModel;

        private Size bitmapPixelSize;
        private (int X, int Y) selectedPixel;
        private Rectangle selectionRect;

        private double scrollFactor = 0.2;
        private Point lastMousePos;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsBitmapBinary
        {
            get => bitmapModel.IsBinary;
        }

        private Size BitmapPixelSize
        {
            get => bitmapPixelSize;
            set
            {
                bitmapPixelSize = value;
                selectionRect.Width = bitmapPixelSize.Width;
                selectionRect.Height = bitmapPixelSize.Height;
            }
        }

        public (int X, int Y) SelectedPixel
        {
            get => selectedPixel;
            set
            {
                selectedPixel = value;
                if (!selectionRect.IsVisible) selectionRect.Visibility = Visibility.Visible;
                UpdateSelectionRectPosition();
                OnPropertyChanged();
                OnPropertyChanged("SelectedPixelColor");
            }
        }

        public Color SelectedPixelColor
        {
            get => bitmapModel.GetPixelColor(selectedPixel);
        }


        public EditorControl()
        {
            InitializeComponent();
            bitmapModel = new BitmapModel(@"pack://application:,,,/BitmapEditor;component/images/testImage.bmp");
            BitmapPreview.Source = bitmapModel.Bitmap;
            bitmapModel.PixelChanged += ((int X, int Y) pixel, Color color) =>
            {
                if (pixel == SelectedPixel) OnPropertyChanged("SelectedPixelColor");
            };

            selectionRect = new()
            {
                StrokeThickness = 1,
                Stroke = Brushes.Black
            };
            BitmapCanvas.Children.Add(selectionRect);
            selectionRect.Visibility = Visibility.Hidden;
        }

        public void LoadBitmap(string url)
        {
            bitmapModel.Load(url);
        }

        public void SaveBitmap(string url)
        {
            bitmapModel.Save(url);
        }

        public void BindDataGrid(DataGrid dataGrid, ColorOffset colorOffset)
        {
            bitmapModel.BindDataGrid(dataGrid, colorOffset);
        }

        public unsafe void ChangeMask(ColorOffset colorOffset, bool hide)
        {
            bitmapModel.ChangeMask(colorOffset, hide);
        }

        public unsafe void ChangePixel(ColorOffset colorOffset, byte colorValue)
        {
            bitmapModel.ChangePixel(SelectedPixel, colorOffset, colorValue);

            OnPropertyChanged("SelectedPixelColor");
        }

        private void OnPixelClick(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(BitmapPreview);
            SelectedPixel = new(
                (int)(pos.X / BitmapPixelSize.Width),
                (int)(pos.Y / BitmapPixelSize.Height)
            );
        }

        private void Canvas_OnScroll(object sender, MouseWheelEventArgs e)
        {
            (double X, double Y) offset = new(
                e.Delta * scrollFactor,
                e.Delta * scrollFactor
            );

            BitmapPreview.Width = BitmapPreview.ActualWidth + offset.X;
            BitmapPreview.Height = BitmapPreview.ActualHeight + offset.Y;

            BitmapPixelSize = new Size(
                BitmapPreview.Width / bitmapModel.Bitmap.PixelWidth,
                BitmapPreview.Height / bitmapModel.Bitmap.PixelHeight
            );

            if (BitmapPreview.Width < BitmapPreview.MinWidth)
            {
                BitmapPixelSize = new Size(
                    BitmapPreview.MinWidth / bitmapModel.Bitmap.PixelWidth,
                    BitmapPreview.MinHeight / bitmapModel.Bitmap.PixelHeight
                );
            }

            if ((int)BitmapPreview.ActualHeight == (int)BitmapPreview.MinHeight && offset.Y < 0) return;

            Point mousePos = e.GetPosition(this);
            (double X, double Y) shiftRatio = new(
                (mousePos.X - Canvas.GetLeft(BitmapPreview)) / BitmapPreview.ActualWidth,
                (mousePos.Y - Canvas.GetTop(BitmapPreview)) / BitmapPreview.ActualHeight
            );

            if ((shiftRatio.X < 0 || shiftRatio.X > 1) ||
                (shiftRatio.Y < 0 || shiftRatio.Y > 1))
            {
                shiftRatio.X = 0.5;
                shiftRatio.Y = 0.5;
            }

            MoveElement(BitmapPreview, (-offset.X * shiftRatio.X, -offset.Y * shiftRatio.Y));
            UpdateSelectionRectPosition();
        }

        private void Canvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Canvas.SetLeft(BitmapPreview, e.NewSize.Width / 2 - BitmapPreview.ActualWidth / 2);
            Canvas.SetTop(BitmapPreview, e.NewSize.Height / 2 - BitmapPreview.ActualHeight / 2);
        }

        private void Canvas_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.RightButton != MouseButtonState.Pressed) return;

            Point currentPos = e.GetPosition(this);
            (double X, double Y) delta = new(
                currentPos.X - lastMousePos.X,
                currentPos.Y - lastMousePos.Y
            );

            MoveElement(BitmapPreview, delta);
            UpdateSelectionRectPosition();
            lastMousePos = currentPos;
        }

        private void Canvas_OnRightMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastMousePos = e.GetPosition(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            selectionRect.Height = BitmapPreview.ActualHeight / bitmapModel.Bitmap.PixelHeight;
            selectionRect.Width = BitmapPreview.ActualWidth / bitmapModel.Bitmap.PixelWidth;

            BitmapPixelSize = new Size(
                BitmapPreview.ActualWidth / bitmapModel.Bitmap.PixelWidth,
                BitmapPreview.ActualHeight / bitmapModel.Bitmap.PixelHeight
            );
        }

        private void UpdateSelectionRectPosition()
        {
            if (!this.IsLoaded) return;

            Point bitmapPos = new(
                Canvas.GetLeft(BitmapPreview),
                Canvas.GetTop(BitmapPreview)
            );

            Canvas.SetLeft(selectionRect, bitmapPos.X + SelectedPixel.X * BitmapPixelSize.Width);
            Canvas.SetTop(selectionRect, bitmapPos.Y + SelectedPixel.Y * bitmapPixelSize.Height);
        }

        private static void MoveElement(UIElement element, (double x, double y) distance)
        {
            Canvas.SetLeft(element, Canvas.GetLeft(element) + distance.x);
            Canvas.SetTop(element, Canvas.GetTop(element) + distance.y);
        }

        private void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
