using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video.DirectShow;
using IDScan.ComputerVision.FacialRecognition;
using Microsoft.Win32;
using Brushes = System.Windows.Media.Brushes;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;
using ThreadState = System.Threading.ThreadState;

namespace FaceTrackingWpfSample
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
            _camController?.Dispose();
            _facialRecognitionProcessor?.Dispose();            
        }

        private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {            
            _camController = new WebCamController();
            _camController.NewVideoFrame += OnNewVideoFrame;
            _camController.VideoFrameReset += OnVideoFrameReset;

            //fill video source combobox
            var list = _camController.GetCamerasList();
            var comboboxSource = list.Select(ToComboboxItem).ToList();
            var emptyVideoSourceComboboxItem = new VideoSourceComboboxItem();
            comboboxSource.Add(emptyVideoSourceComboboxItem);
            VideoSourceCombobox.ItemsSource = comboboxSource;
            VideoSourceCombobox.SelectedItem = emptyVideoSourceComboboxItem;

            await InitializeSdk();
        }

        private async Task InitializeSdk()
        {
            try
            {
                //looking for data file
                var dataDirectoryPath = File.Exists("data.bin") ? "" : "..\\..\\..\\";
                var processingThreadsCount = 2;

                _facialRecognitionProcessor = new FacialRecognitionProcessor(dataDirectoryPath, processingThreadsCount);
                _facialRecognitionProcessor.Tracking += OnTracking;
                _facialRecognitionProcessor.Identified += OnIdentified;
                _facialRecognitionProcessor.Initialized += OnInitialized;
                await _facialRecognitionProcessor.InitializeAsync();
            }
            catch (Exception e)
            {
                Dispatcher.Invoke(() =>
                {                    
                    ReadyText.Text = "Initialization failed";
                });
                MessageBox.Show($"Sdk initialization failed. {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnVideoFrameReset(object sender, EventArgs eventArgs)
        {
            //reset frame image when video source closed
            if (Dispatcher.Thread.ThreadState == ThreadState.Running)
            {
                Dispatcher.Invoke(() =>
                {
                    if (VideoSourceFrameImage.Source is BitmapImage image)
                    {
                        image.StreamSource.Dispose();
                        image.StreamSource.Close();
                    }

                    VideoSourceFrameImage.Source = null;                   
                }, DispatcherPriority.Render);
            }
        }

        private VideoSourceComboboxItem ToComboboxItem(FilterInfo arg)
        {
            return new VideoSourceComboboxItem{FilterInfo = arg};
        }

        private void OnInitialized(object sender, EventArgs eventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                //enable buttons when sdk initialized
                ReadyText.Text = "SDK Ready";
                ButtonsPanel.IsEnabled = true;
            });            
        }

        private void OnIdentified(object sender, IdentificationInfo identificationInfo)
        {
            // identification result
            try
            {
                ImageConverter converter = new ImageConverter();
                var bytes = (byte[])converter.ConvertTo(identificationInfo.CroppedFace, typeof(byte[]));                

                if (bytes == null)
                {
                    return;
                }

                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = new MemoryStream(bytes);
                bi.EndInit();
                //Using the freeze function to avoid cross thread operations 
                bi.Freeze();           

                if (Dispatcher.Thread.ThreadState == ThreadState.Running)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (IdentifiedImage.Source is BitmapImage image)
                        {
                            image.StreamSource.Dispose();
                            image.StreamSource.Close();
                        }

                        IdentifiedImage.Source = null;

                        IdentifiedImage.Source = bi;                    
                    }, DispatcherPriority.Render);
                }
            }
            catch (Exception exception)
            {
                
            }
        }

        private void OnNewVideoFrame(object sender, Bitmap frame)
        {
            if (frame == null) return;

            var ms = new MemoryStream();
            frame.Save(ms, ImageFormat.Png);

            // show frame on screen
            SetFrame(ms);
            
            //process frame
            _facialRecognitionProcessor.ProcessFrameAsync(frame);
        }

        private void SetFrame(Stream stream)
        {
            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = stream;
                bi.EndInit();
                //Using the freeze function to avoid cross thread operations 
                bi.Freeze();           

                if (Dispatcher.Thread.ThreadState == ThreadState.Running)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (VideoSourceFrameImage.Source is BitmapImage image)
                        {
                            image.StreamSource.Dispose();
                            image.StreamSource.Close();
                        }

                        VideoSourceFrameImage.Source = null;

                        VideoSourceFrameImage.Source = bi;                    
                    }, DispatcherPriority.Render);
                }
            }
            catch (Exception exception)
            {
                
            }
        }

        private FacialRecognitionProcessor _facialRecognitionProcessor;
        private WebCamController _camController;

        DropShadowEffect _dropShadowEffect = new DropShadowEffect {Color = Colors.Black, BlurRadius = 3, ShadowDepth = 2};

        private void OnTracking(object sender, TrackingInfo trackingInfo)
        {
            if (Dispatcher.HasShutdownStarted)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {                
                Canvas.Children.Clear();
                if (trackingInfo.Faces == null) return;                

                foreach (var face in trackingInfo.Faces.OrderBy(t => t.IsRecognized))
                {
                    var faceRectangle = new Rectangle();
                    faceRectangle.Width = face.Width * (Canvas.ActualWidth / trackingInfo.FrameWidth);
                    faceRectangle.Height = face.Height * (Canvas.ActualHeight / trackingInfo.FrameHeight);
                    faceRectangle.Stroke = face.IsRecognized ? Brushes.LawnGreen : Brushes.Crimson;
                    faceRectangle.StrokeThickness = 2;
                    faceRectangle.Fill = null;
                    faceRectangle.IsHitTestVisible = false;
                    faceRectangle.Effect = _dropShadowEffect;

                    Canvas.Children.Add(faceRectangle);

                    var y = face.PositionY * (Canvas.ActualHeight / trackingInfo.FrameHeight);
                    Canvas.SetTop(faceRectangle, y);
                    var x = face.PositionX * (Canvas.ActualWidth / trackingInfo.FrameWidth);
                    Canvas.SetLeft(faceRectangle, x);                    


                    var trackText = new TextBlock();
                    trackText.FontSize = 12;
                    trackText.Text = $"Track#: {face.TrackId}";
                    trackText.Foreground = Brushes.GhostWhite;
                    trackText.Effect = _dropShadowEffect;

                    Canvas.Children.Add(trackText);

                    trackText.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
                    trackText.Arrange(new Rect(trackText.DesiredSize));

                    var heightOffset = trackText.ActualHeight + 1;

                    Canvas.SetTop(trackText, y );
                    Canvas.SetLeft(trackText, x + faceRectangle.Width);

                    AddTextBlockToCanvas($"Angry: {face.EmotionAngry}", y + heightOffset, x + faceRectangle.Width);

                    AddTextBlockToCanvas($"Happy: {face.EmotionHappy}", y + heightOffset * 2, x + faceRectangle.Width);

                    AddTextBlockToCanvas($"Neutral: {face.EmotionNeutral}", y + heightOffset * 3, x + faceRectangle.Width);

                    AddTextBlockToCanvas($"Surprise: {face.EmotionSurprise}", y + heightOffset * 4, x + faceRectangle.Width);

                    AddTextBlockToCanvas($"Age Group: {face.AgeGroup}", y + heightOffset * 5, x + faceRectangle.Width);

                    AddTextBlockToCanvas($"Gender: {face.Gender}", y + heightOffset * 6, x + faceRectangle.Width);

                    if (face.IsRecognized)
                    {
                        var percentText = new TextBlock();
                        percentText.Text = $"{face.Percent} %";
                        percentText.Foreground = Brushes.GhostWhite;
                        percentText.Effect =
                            _dropShadowEffect;

                        Canvas.Children.Add(percentText);

                        percentText.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
                        percentText.Arrange(new Rect(percentText.DesiredSize));

                        Canvas.SetTop(percentText, y + 5);
                        Canvas.SetLeft(percentText, x + faceRectangle.Width - percentText.ActualWidth - 5);
                    }
                }
            });
        }

        private void AddTextBlockToCanvas(string text, double y, double x)
        {
            var angryText = new TextBlock();
            angryText.FontSize = 12;
            angryText.Text = text;
            angryText.Foreground = Brushes.GhostWhite;
            angryText.Effect = _dropShadowEffect;

            Canvas.Children.Add(angryText);

            Canvas.SetTop(angryText, y);
            Canvas.SetLeft(angryText, x);
        }

        private async void EnrollVideoButton_Click(object sender, RoutedEventArgs e)
        {            
            if (VideoSourceFrameImage.Source is BitmapSource bitmapSource)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    var bytes = stream.ToArray();

                    var template = await _facialRecognitionProcessor.CreateTemplateAsync(bytes);

                    var matchResult = await _facialRecognitionProcessor.MatchTemplateAsync(template);

                    if (!matchResult.IsMatched)
                    {
                        var matchResultTemplate = matchResult.Template;
                        //  manually set template id
                        matchResultTemplate.Id = FaceTemplates.Count + 1;
                        FaceTemplates.Add(matchResultTemplate);

                        _facialRecognitionProcessor.AddTemplate(matchResultTemplate);
                    }
                }
            }
        }

        private async void EnrollFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            var result = dialog.ShowDialog(this);

            if (result == true)
            {
                var path = dialog.FileName;
                var bytes = File.ReadAllBytes(path);

                var template = await _facialRecognitionProcessor.CreateTemplateAsync(bytes);
                
                var matchResult = await _facialRecognitionProcessor.MatchTemplateAsync(template);

                if (!matchResult.IsMatched)
                {
                    var matchResultTemplate = matchResult.Template;
                    //  manually set template id
                    matchResultTemplate.Id = FaceTemplates.Count + 1;
                    FaceTemplates.Add(matchResultTemplate);

                    _facialRecognitionProcessor.AddTemplate(matchResultTemplate);
                }                
            }
        }

        private async void MatchButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            var result = dialog.ShowDialog(this);

            if (result == true)
            {
                var path = dialog.FileName;
                var bytes = File.ReadAllBytes(path);

                var matchResult = await _facialRecognitionProcessor.MatchImageAsync(new Bitmap(new MemoryStream(bytes)));
                if (matchResult.IsMatched)
                {
                    MessageBox.Show($"Matched with template Id # {matchResult.TemplateId} {matchResult.Percent} %.", "Match result");
                }
                else
                {
                    MessageBox.Show($"No match", "Match result");
                }
            }
        }

        public List<FaceTemplate> FaceTemplates = new List<FaceTemplate>();

        private async void QualityVideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoSourceFrameImage.Source is BitmapSource bitmapSource)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    var bytes = stream.ToArray();

                    var qualityResult = await _facialRecognitionProcessor.GetImageQualityAsync(bytes);
                    MessageBox.Show($"Quality: {qualityResult}."); 
                }
            }
        }

        private async void QualityFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.CheckFileExists = true;
            var result = dialog.ShowDialog(this);

            if (result == true)
            {
                var path = dialog.FileName;
                var bytes = File.ReadAllBytes(path);

                var qualityResult = await _facialRecognitionProcessor.GetImageQualityAsync(bytes);
                MessageBox.Show($"Quality: {qualityResult}.");               
            }
        }

        private async void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select first image";
            dialog.CheckFileExists = true;
            var result = dialog.ShowDialog(this);

            byte[] bytes1 = null;
            if (result == true)
            {
                var path = dialog.FileName;
                bytes1 = File.ReadAllBytes(path);                
            }


            dialog.Title = "Select second image";
            result = dialog.ShowDialog(this);

            byte[] bytes2 = null;
            if (result == true)
            {
                var path = dialog.FileName;
                bytes2 = File.ReadAllBytes(path);
            }

            var first = await _facialRecognitionProcessor.CreateTemplateAsync(bytes1);
            var second = await _facialRecognitionProcessor.CreateTemplateAsync(bytes2);
            var compareResult = await _facialRecognitionProcessor.CompareTemplatesAsync(first, second);

            MessageBox.Show($"Match: {compareResult} %."); 
        }

        private void VideoSourceCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            var t = VideoSourceCombobox.SelectedItem as VideoSourceComboboxItem;
            if (t?.FilterInfo == null)
            {                
                _camController.StopVideo();
                Canvas.Children.Clear();
                return;
            }
            _camController.OpenVideoSource(t.FilterInfo.MonikerString);            
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // set empty list to clear templates loaded in memory
            _facialRecognitionProcessor.SetTemplates(new List<FaceTemplate>());
        }
    }
}
