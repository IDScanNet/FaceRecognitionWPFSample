using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Documents;
using AForge.Video;
using AForge.Video.DirectShow;

namespace FaceTrackingWpfSample
{
    public class WebCamController : IDisposable
    {
        private VideoCaptureDevice _localWebCam;

        public event EventHandler<Bitmap> NewVideoFrame;
        public event EventHandler VideoFrameReset;

        public WebCamController()
        {

        }

        public List<FilterInfo> GetCamerasList()
        {
            return new FilterInfoCollection(FilterCategory.VideoInputDevice)
                .Cast<FilterInfo>()
                .ToList();            
        }

        public void OpenAvailableVideoSource()
        {
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count >= 1)
            {
                OpenVideoSource(videoDevices[0].MonikerString);
            }            
        }

        public void OpenVideoSource(string monikerString)
        {
            StopVideo();
            _localWebCam = null;
            System.Threading.Thread.Sleep(125);            
            _localWebCam = new VideoCaptureDevice(monikerString);;
            _localWebCam.NewFrame += OnNewFrame;
            StartVideo();
        }

        
        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // AForge owns the bytes of the image, you should make your own deep copy.
            // Passing the frame to the Bitmap constructor is not enough. If the framework
            // is not able to properly dispose the bitmap because you have a reference to it, there's a leak.
            try
            {
                NewVideoFrame?.Invoke(null, eventArgs.Frame);          
            }
            catch
            {
                //
                try
                {
                    eventArgs?.Frame?.Dispose();
                }
                catch
                {

                }
            }

        }

        public void StopVideo()
        {
            StopVideoSignal();
        }

        private void StopVideoSignal(bool waitForStoop = false)
        {
            if (_localWebCam != null && _localWebCam.IsRunning)
            {
                _localWebCam.SignalToStop();
                if(waitForStoop)
                {
                    _localWebCam.WaitForStop();
                }
                else Thread.Sleep(125);
            }

            VideoFrameReset?.Invoke(null, EventArgs.Empty);
        }

        public void Close()
        {
            StopVideoSignal(true);
            _localWebCam = null;
        }


        public void PauseVideo()
        {
            if (_localWebCam != null && _localWebCam.IsRunning )
            {
                _localWebCam.SignalToStop();
            }
        }



        public void StartVideo()
        {
            _localWebCam.Start();            
        }

        public void DisposeVideoElement()
        {            
            StopVideo();            
            _localWebCam = null;
        }

        public void Dispose()
        {
            DisposeVideoElement();
        }
    }
}