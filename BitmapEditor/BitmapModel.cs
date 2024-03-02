using System.Data;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace BitmapEditor
{
    public enum ColorOffset
    {
        Blue,
        Green,
        Red,
        Black
    }

    public class BitmapModel
    {
        private WriteableBitmap bitmap;
        private byte[] rawBitmapData;

        public WriteableBitmap Bitmap { get => bitmap; }
        public byte[] RawBitmapData { get => (byte[])rawBitmapData.Clone(); }
        public int BytesPerPixel { get; private set; }
        public int Stride { get; private set; }

        public event Action<(int X, int Y), Color>? PixelChanged;

        public bool IsBinary
        {
            get
            {
                foreach (var b in rawBitmapData)
                {
                    if (b != 0 && b != 255) return false;
                }
                return true;
            }
        }

        public BitmapModel() : this(@"pack://application:,,,/BitmapEditor;component/images/testImage.bmp") { }

        public BitmapModel(string uri)
        {
            Load(uri);
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
        }

        public void Save(string uri)
        {
            Drawing.Bitmap bmp = new (
                Bitmap.PixelWidth,
                Bitmap.PixelHeight,
                Drawing.Imaging.PixelFormat.Format32bppArgb
            );

            BitmapData bitmapData = bmp.LockBits(
                new Drawing.Rectangle(0, 0,
                    Bitmap.PixelWidth,
                    Bitmap.PixelHeight),
                Drawing.Imaging.ImageLockMode.WriteOnly,
                Drawing.Imaging.PixelFormat.Format32bppArgb
            );

            Bitmap.WritePixels(
                new Int32Rect(0, 0,
                    Bitmap.PixelWidth,
                    Bitmap.PixelHeight),
                RawBitmapData,
                Stride,
                0
            );

            Bitmap.CopyPixels(
                Int32Rect.Empty,
                bitmapData.Scan0,
                bitmapData.Height * bitmapData.Stride,
                bitmapData.Stride
            );

            bmp.UnlockBits(bitmapData);

            bmp.Save(uri, ImageFormat.Bmp);
            bmp.Dispose();
        }

        public unsafe void ChangeMask(ColorOffset colorOffset, bool hide)
        {
            Bitmap.Lock();

            byte* bitmapBuffer = (byte*)Bitmap.BackBuffer.ToPointer();
            int bytesPerPixel = (Bitmap.Format.BitsPerPixel + 7) / 8;

            for (int y = 0; y < Bitmap.PixelHeight; y++)
            {
                for (int x = 0; x < Bitmap.PixelWidth; x++)
                {
                    int offset = y * Stride + x * bytesPerPixel;

                    bitmapBuffer[offset + (int)colorOffset] = hide ? (byte)0 : rawBitmapData[offset + (int)colorOffset];
                }
            }

            Bitmap.AddDirtyRect(new Int32Rect(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight));
            Bitmap.Unlock();
        }

        public unsafe void ChangePixel((int X, int Y) pixel, ColorOffset colorOffset, byte colorValue)
        {
            Bitmap.Lock();

            byte* bitmapBuffer = (byte*)Bitmap.BackBuffer.ToPointer();
            int bytesPerPixel = (Bitmap.Format.BitsPerPixel + 7) / 8;

            int index = pixel.Y * Stride + pixel.X * bytesPerPixel;
            int i = (int)colorOffset;
            if (colorOffset == ColorOffset.Black) i = 0;
            else ++colorOffset;

            for (; i < (int)colorOffset; i++)
            {
                bitmapBuffer[index + i] = colorValue;
                rawBitmapData[index + i] = colorValue;
            }

            Bitmap.AddDirtyRect(new Int32Rect(pixel.X, pixel.Y, 1, 1));
            Bitmap.Unlock();

            OnPixelChanged(
                pixel,
                new Color { 
                    R = rawBitmapData[index + 2],
                    G = rawBitmapData[index + 1],
                    B = rawBitmapData[index]
                }
            );
        }

        public void BindDataGrid(DataGrid dataGrid, ColorOffset colorOffset)
        {
            DataTable dataTable = new DataTable();

            for (int i = 0; i < Bitmap.PixelWidth; i++)
            {
                dataTable.Columns.Add(null, typeof(byte));
            }

            for (int i = 0; i < Bitmap.PixelHeight; i++)
            {
                DataRow row = dataTable.NewRow();
                for (int j = 0; j < Bitmap.PixelWidth; j++)
                {
                    if (colorOffset == ColorOffset.Black)
                    {
                        row[j] = rawBitmapData[i * Stride + j * BytesPerPixel] > 0 ? 0 : 1;
                    } 
                    else
                    {
                        row[j] = rawBitmapData[i * Stride + j * BytesPerPixel + (int)colorOffset];
                    }
                }
                dataTable.Rows.Add(row);
            }

            bool changing = false;
            dataTable.ColumnChanged += (sender, e) =>
            {
                if (changing) return;
                if (e.ProposedValue is byte color && sender is DataTable table)
                {
                    changing = true;
                    if (colorOffset == ColorOffset.Black) color = color > 0 ? (byte)0 : (byte)255;
                    (int X, int Y) pixel = (table.Columns.IndexOf(e.Column), table.Rows.IndexOf(e.Row));
                    table.Rows[pixel.X][pixel.Y] = color > 0 ? 0 : 1;
                    ChangePixel(pixel, colorOffset, color);
                    changing = false;
                }
            };

            dataGrid.ItemsSource = dataTable.DefaultView;
        }


        public Color GetPixelColor((int X, int Y) pixel)
        {
            int ind = pixel.Y * Stride + pixel.X * BytesPerPixel;
            Color color = new()
            {
                R = RawBitmapData[ind + 2],
                G = RawBitmapData[ind + 1],
                B = RawBitmapData[ind],
            };
            return color;
        }


        private void OnPixelChanged((int X, int Y) pixel, Color color)
        {
            PixelChanged?.Invoke(pixel, color);
        }
    }
}
