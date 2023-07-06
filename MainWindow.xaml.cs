using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Basler.Pylon;
using MVSDK;
using PixelFormat = System.Windows.Media.PixelFormat;

namespace BaslerCameraVisual
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Camera camera;
        private EventHandler<ImageGrabbedEventArgs> imageHandler;
        protected Int32 m_hCamera1 = 0;
        protected IntPtr m_ImageBuffer1; // Preview channel RGB image cache
        protected IntPtr m_ImageBufferSnapshot1; // capture channel RGB image cache
        protected tSdkCameraCapbility tCameraCapability1; // Camera characterization
        protected IntPtr m_Grabber1 = IntPtr.Zero;
        protected tSdkCameraDevInfo m_DevInfo1;
        protected pfnCameraGrabberFrameCallback m_FrameCallback1;
        protected pfnCameraGrabberSaveImageComplete m_SaveImageComplete1;
        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
            m_FrameCallback1 = new pfnCameraGrabberFrameCallback(CameraGrabberFrameCallback1);

        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MvApi.CameraGrabber_Destroy(m_Grabber1);

        }

        public void InitialCameGige() 
        {
            CameraSdkStatus status1 = 0;
            tSdkCameraDevInfo[] DevList;
            MvApi.CameraEnumerateDevice(out DevList);
            int NumDev = (DevList != null ? DevList.Length : 0);
            if (NumDev < 1)
            {
                MessageBox.Show("Camera not scanned");
                return;
            }
            else if (NumDev == 1)
            {
                status1 = MvApi.CameraGrabber_Create(out m_Grabber1, ref DevList[0]);
            }
            else
            {
                status1 = MvApi.CameraGrabber_Create(out m_Grabber1, ref DevList[0]);
                //status2 = MvApi.CameraGrabber_Create(out m_Grabber2, ref DevList[1]);
                //status3 = MvApi.CameraGrabber_Create(out m_Grabber3, ref DevList[2]);
                //status4 = MvApi.CameraGrabber_Create(out m_Grabber4, ref DevList[3]);
            }
            if (status1 == 0)
            {
                MvApi.CameraGrabber_GetCameraDevInfo(m_Grabber1, out m_DevInfo1);
                MvApi.CameraGrabber_GetCameraHandle(m_Grabber1, out m_hCamera1);
                MvApi.CameraCreateSettingPage(m_hCamera1, new WindowInteropHelper(this).Handle, m_DevInfo1.acFriendlyName, null, (IntPtr)0, 0);

                MvApi.CameraGrabber_SetRGBCallback(m_Grabber1, m_FrameCallback1, IntPtr.Zero);

                // black and white camera settings ISP output grayscale image
                // Color Camera ISP will output BGR24 image by default
                tSdkCameraCapbility cap;
                //MvApi.CameraGetCapability(m_hCamera1, out cap);
                //if (cap.sIspCapacity.bMonoSensor != 0)
                //{
                //    MvApi.CameraSetIspOutFormat(m_hCamera1, (uint)MVSDK.emImageFormat.CAMERA_MEDIA_TYPE_MONO8);

                //    // Create grayscale palette
                //    Bitmap Image = new Bitmap(1, 1, PixelFormat.Format8bppIndexed);
                //    m_GrayPal = Image.Palette;
                //    for (int Y = 0; Y < m_GrayPal.Entries.Length; Y++)
                //        m_GrayPal.Entries[Y] = Color.FromArgb(255, Y, Y, Y);
                //}

                // set VFlip, because the data output by the SDK is from bottom to top by default, VFlip can be directly converted to Bitmap
                MvApi.CameraSetMirror(m_hCamera1, 1, 1);

                // To illustrate how to use the camera data to create a Bitmap in a callback and display it in a PictureBox, we do not use the SDK's built-in drawing operations
                //MvApi.CameraGrabber_SetHWnd(m_Grabber, this.DispWnd.Handle);

                MvApi.CameraGrabber_StartLive(m_Grabber1);
            }
            else
            {
                MessageBox.Show(String.Format("Failed to open the camera, reason:{0}", status1));
            }

        }
        private void CameraGrabberFrameCallback1(IntPtr Grabber, IntPtr pFrameBuffer, ref tSdkFrameHead pFrameHead, IntPtr Context)
        {
            camera.ExecuteSoftwareTrigger();
            var bmp = (Bitmap)MvApi.CSharpImageFromFrame(pFrameBuffer, ref pFrameHead);
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                GC.Collect();
                using (MemoryStream memory = new MemoryStream())
                {
                    bmp.Save(memory, ImageFormat.Bmp);
                    memory.Position = 0;
                    BitmapImage bitmapimage = new BitmapImage();
                    bitmapimage.BeginInit();
                    bitmapimage.StreamSource = memory;
                    bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapimage.EndInit();
                    bitmapimage.Freeze();
                    img1.Source = bitmapimage;
                }
            });
            bmp.Dispose();
        }
        public void Opencam()
        {
            try
            {
                // Create a new Pylon camera object
                camera = new Camera();

                // Open the first available camera device
                camera.Open();

                // Set the camera's acquisition mode to "Continuous"
                camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);

                // Disable the camera's trigger mode (operate in free-running mode)
                camera.Parameters[PLCamera.TriggerMode].SetValue(PLCamera.TriggerMode.On);
                
                // Trigger By Software
                camera.Parameters[PLCamera.TriggerSource].SetValue(PLCamera.TriggerSource.Software);
                

                // Create the image event handler
                imageHandler = (sender, e) =>
                {
                    // Retrieve the grabbed image
                    using (IGrabResult grabResult = e.GrabResult)
                    {
                        // Check if the image is successfully grabbed
                        if (grabResult.GrabSucceeded)
                        {
                            // Update the UI with the grabbed image
                            Dispatcher.Invoke(() =>
                            {
                                img.Source = ConvertToBitmapSource(grabResult);
                            });
                        }
                    }
                };

                // Attach the image event handler to the camera's "ImageGrabbed" event
                camera.StreamGrabber.ImageGrabbed += imageHandler;

                // Start grabbing images continuously
                camera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }
        private BitmapSource ConvertToBitmapSource(IGrabResult grabResult)
        {
            // Cast the pixel data to byte[] assuming it is of type byte
            byte[] pixelData = (byte[])grabResult.PixelData;

            // Create a BitmapSource from the grabbed image data
            PixelFormat format = PixelFormats.Gray8; // Adjust the format based on your image data
            BitmapSource bitmapSource = BitmapSource.Create(
                grabResult.Width, grabResult.Height,
                96, 96, format, null,
                pixelData,
                grabResult.Width * format.BitsPerPixel / 8);

            return bitmapSource;
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            camera.StreamGrabber.Stop();
            camera.StreamGrabber.ImageGrabbed -= imageHandler;
            camera.Close();
        }

        private void btnTrigger_Click(object sender, RoutedEventArgs e)
        {
            camera.ExecuteSoftwareTrigger();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            Opencam();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnStt1_Click(object sender, RoutedEventArgs e)
        {
            if (m_hCamera1 > 0)
            {
                MvApi.CameraShowSettingPage(m_hCamera1, 1);//1 show ; 0 hide
            }
        }
    }
}
