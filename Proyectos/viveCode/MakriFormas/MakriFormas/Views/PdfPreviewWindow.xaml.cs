using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MakriFormas.Views
{
    public partial class PdfPreviewWindow : Window
    {
        private readonly string _pdfPath;
        public bool ExportConfirmed { get; private set; }

        public PdfPreviewWindow(string pdfPath)
        {
            InitializeComponent();
            _pdfPath = pdfPath;
            Loaded += async (_, _) => await LoadPdfPreviewAsync();
        }

        private async Task LoadPdfPreviewAsync()
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(_pdfPath);
                var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
                
                var images = new List<BitmapImage>();

                for (uint i = 0; i < pdfDoc.PageCount; i++)
                {
                    using var page = pdfDoc.GetPage(i);
                    using var stream = new InMemoryRandomAccessStream();
                    
                    var renderOptions = new Windows.Data.Pdf.PdfPageRenderOptions
                    {
                        DestinationWidth = (uint)(page.Size.Width * 2), // 2x scale for better quality
                        DestinationHeight = (uint)(page.Size.Height * 2)
                    };

                    await page.RenderToStreamAsync(stream, renderOptions);
                    
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream.AsStream();
                    image.EndInit();
                    image.Freeze(); // Allow access from UI thread
                    
                    images.Add(image);
                }

                icPages.ItemsSource = images;
            }
            catch (Exception ex)
            {
                txtLoading.Text = "Error al cargar la previsualización: " + ex.Message;
                txtLoading.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                if (icPages.Items.Count > 0)
                {
                    loadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            ExportConfirmed = false;
            Close();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            ExportConfirmed = true;
            Close();
        }
    }
}
