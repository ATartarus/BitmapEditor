using System.Drawing.Imaging;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using win = System.Windows;


namespace BitmapEditor
{
    /// <summary>
    /// Interaction logic for EditorControl.xaml
    /// </summary>
    public partial class EditorControl : UserControl, INotifyPropertyChanged
    {
        private WriteableBitmap bitmap;
        private byte[] rawBitmapData;
        private win.Size bitmapPixelSize;

        private (int X, int Y) selectedPixel;
        private win.Shapes.Rectangle selectionRect;
        private double scrollFactor = 0.2;
        private win.Point lastMousePos;

        public event PropertyChangedEventHandler? PropertyChanged;

        public WriteableBitmap Bitmap { get => bitmap; }
        public byte[] RawBitmapData { get => (byte[])rawBitmapData.Clone(); }
        private win.Size BitmapPixelSize
        {
            get => bitmapPixelSize;
            set
            {
                bitmapPixelSize = value;
                selectionRect.Width = bitmapPixelSize.Width;
                selectionRect.Height = bitmapPixelSize.Height;
            }
        }
        public int BytesPerPixel { get; private set; }
        public int Stride { get; private set; }

        public (int X, int Y) SelectedPixel
        {
            get => selectedPixel;
            set
            {
                selectedPixel = value;
                if (!selectionRect.IsVisible) selectionRect.Visibility = Visibility.Visible;
                UpdateSelectionRectPosition();
                OnPropertyChanged();
                OnPropertyChanged("SelectedPixelValue");
            }
        }
        public win.Media.Color SelectedPixelValue
        {
            get
            {
                int ind = SelectedPixel.Y * Stride + SelectedPixel.X * BytesPerPixel;
                win.Media.Color color = new()
                {
                    R = rawBitmapData[ind + 2],
                    G = rawBitmapData[ind + 1],
                    B = rawBitmapData[ind],
                };
                return color;
            }
        }


        public EditorControl()
        {
            InitializeComponent();
            Load(@"C:\BNTU\Image Processing\IPLab1\IPLab1\black.bmp");

            selectionRect = new()
            {
                StrokeThickness = 1,
                Stroke = win.Media.Brushes.Black
            };
            BitmapCanvas.Children.Add(selectionRect);
            selectionRect.Visibility = Visibility.Hidden;
        }

        public void Load(string uri)
        {
            BitmapImage bmp = new();
            bmp.BeginInit();
            bmp.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
            bmp.EndInit();

            bitmap = new(bmp);

            Stride = Bitmap.PixelWidth * ((Bitmap.Format.BitsPerPixel + 7) / 8);
            BytesPerPixel = (Bitmap.Format.BitsPerPixel + 7) / 8;


            rawBitmapData = new byte[Bitmap.PixelHeight * Stride];
            Bitmap.CopyPixels(rawBitmapData, Stride, 0);

            BitmapPreview.Source = Bitmap;
        }

        public void Save(string uri)
        {
            Bitmap bmp = new Bitmap(Bitmap.PixelWidth, Bitmap.PixelHeight, PixelFormat.Format32bppArgb);

            BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            Bitmap.WritePixels(new Int32Rect(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight), rawBitmapData, Stride, 0);
            Bitmap.CopyPixels(Int32Rect.Empty, bitmapData.Scan0, bitmapData.Height * bitmapData.Stride, bitmapData.Stride);

            bmp.UnlockBits(bitmapData);

            bmp.Save(uri, ImageFormat.Bmp);
            bmp.Dispose();
        }

        public unsafe void ChangeMask(int colorOffset, bool hide)
        {
            //Bitmap.Lock();

            //byte* bitmapBuffer = (byte*)Bitmap.BackBuffer.ToPointer();
            //int bytesPerPixel = (Bitmap.Format.BitsPerPixel + 7) / 8;

            //for (int y = 0; y < Bitmap.PixelHeight; y++)
            //{
            //    for (int x = 0; x < Bitmap.PixelWidth; x++)
            //    {
            //        int offset = y * Stride + x * bytesPerPixel;

            //        bitmapBuffer[offset + colorOffset] = hide ? (byte)0 : rawBitmapData[offset + colorOffset];
            //    }
            //}

            //Bitmap.AddDirtyRect(new Int32Rect(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight));
            //Bitmap.Unlock();
        }

        public unsafe void ChangePixel(int colorOffset, byte colorValue)
        {
            Bitmap.Lock();

            byte* bitmapBuffer = (byte*)Bitmap.BackBuffer.ToPointer();
            int bytesPerPixel = (Bitmap.Format.BitsPerPixel + 7) / 8;

            int index = SelectedPixel.Y * Stride + SelectedPixel.X * bytesPerPixel;
            bitmapBuffer[index + colorOffset] = colorValue;
            rawBitmapData[index + colorOffset] = colorValue;

            Bitmap.AddDirtyRect(new Int32Rect(SelectedPixel.X, SelectedPixel.Y, 1, 1));
            Bitmap.Unlock();

            OnPropertyChanged("SelectedPixelValue");
        }

        private void OnPixelClick(object sender, MouseButtonEventArgs e)
        {
            win.Point pos = e.GetPosition(BitmapPreview);
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

            BitmapPixelSize = new win.Size(
                BitmapPreview.Width / Bitmap.PixelWidth,
                BitmapPreview.Height / Bitmap.PixelHeight
            );
            if (BitmapPreview.Width < BitmapPreview.MinWidth)
            {
                BitmapPixelSize = new win.Size(
                    BitmapPreview.MinWidth / Bitmap.PixelWidth,
                    BitmapPreview.MinHeight / Bitmap.PixelHeight
                );
            }

            if ((int)BitmapPreview.ActualHeight == (int)BitmapPreview.MinHeight && offset.Y < 0) return;

            win.Point mousePos = e.GetPosition(this);
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

            win.Point currentPos = e.GetPosition(this);
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
            selectionRect.Height = BitmapPreview.ActualHeight / Bitmap.PixelHeight;
            selectionRect.Width = BitmapPreview.ActualWidth / Bitmap.PixelWidth;

            BitmapPixelSize = new win.Size(
                BitmapPreview.ActualWidth / Bitmap.PixelWidth,
                BitmapPreview.ActualHeight / Bitmap.PixelHeight
            );
        }

        private void UpdateSelectionRectPosition()
        {
            if (!this.IsLoaded) return;

            win.Point bitmapPos = new(
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

        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
