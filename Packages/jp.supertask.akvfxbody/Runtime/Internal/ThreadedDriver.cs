using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Akvfx {

public sealed class ThreadedDriver : IDisposable
{
    #region Public properties and methods

    public static int ImageWidth => 640;
    public static int ImageHeight => 576;

    //Added for body tracking!
    private BodyProvider provider;
    public BackgroundData lastBackgroundData = new BackgroundData();
    public GameObject bodyObj;

    public ThreadedDriver(DeviceSettings settings)
    {
        // FIXME: Dangerous. We should do this only on Player.
        K4aExtensions.DisableSafeCopyNativeBuffers();

        Settings = settings;

        this.provider = new BodyProvider();

        this.bodyObj = GameObject.Find("Kinect4AzureTracker"); //TODO: Remove later

        _captureThread = new Thread(CaptureThread);
        _captureThread.Start();
    }

    public void Dispose()
    {
        _terminate = true;
        _captureThread.Join();

        TrimQueue(0);
        ReleaseLastFrame();

        GC.SuppressFinalize(this);
    }

    public DeviceSettings Settings { get; set; }

    public ReadOnlySpan<float> XYTable
      => _xyTable != null ? _xyTable.Data : null;

    public (
            ReadOnlyMemory<byte> color,
            ReadOnlyMemory<byte> depth,
            ReadOnlyMemory<byte> bodyIndexMap
    ) LockLastFrame()
    {
        // Try retrieving the last frame.
        if (_lockedFrame.capture == null) _queue.TryDequeue(out _lockedFrame);

        // Return null if it failed to retrieve.
        if (_lockedFrame.capture == null) return (null, null, null);

        // Return null if it failed to retrieve.
        if (_lockedFrame.bodyIndexMap == null) {
            _queue.TryDequeue(out _lockedFrame);
            return (null, null, null);
        }

        return (
            _lockedFrame.color.Memory,
            _lockedFrame.capture.Depth.Memory,
            _lockedFrame.bodyIndexMap.Memory
        );
    }

    public void ReleaseLastFrame()
    {
        _lockedFrame.capture?.Dispose();
        _lockedFrame.color?.Dispose();
        _lockedFrame.bodyIndexMap?.Dispose();
        _lockedFrame = (null, null, null);
    }

    #endregion

    #region Private objects

    XYTable _xyTable;

    #endregion

    #region Capture queue

    ConcurrentQueue<(Capture capture, Image color, Image bodyIndexMap)>
        _queue = new ConcurrentQueue<(Capture, Image, Image)>();

    (Capture capture, Image color, Image bodyIndexMap) _lockedFrame;

    // Trim the queue to a specified count.
    void TrimQueue(int count)
    {
        while (_queue.Count > count)
        {
            (Capture capture, Image color, Image bodyIndexMap) temp;
            _queue.TryDequeue(out temp);
            temp.capture?.Dispose();
            temp.color?.Dispose();
            temp.bodyIndexMap?.Dispose();
        }
    }

    #endregion

    #region Capture thread

    Thread _captureThread;
    bool _terminate;

    void CaptureThread()
    {
        // If there is no available device, do nothing.
        if (Device.GetInstalledCount() == 0) return;

        // Open the default device.
        var device = Device.Open();

        // Start capturing with custom settings.
        device.StartCameras
          (new DeviceConfiguration
            { ColorFormat = ImageFormat.ColorBGRA32,
              ColorResolution = ColorResolution.R1536p, // 2048 x 1536 (4:3)
              DepthMode = DepthMode.NFOV_Unbinned,      // 640x576
              SynchronizedImagesOnly = true });

        // Construct XY table as a background task.
        Task.Run(() => _xyTable =
          new XYTable(device.GetCalibration(), ImageWidth, ImageHeight));

        // Set up the transformation object.
        var transformation = new Transformation(device.GetCalibration());

        // Initially apply the device settings.
        var setter = new DeviceSettingController(device, Settings);

        //Body tracking setup
        Tracker tracker = Tracker.Create(
            device.GetCalibration(),
            new TrackerConfiguration() {
                ProcessingMode = TrackerProcessingMode.Gpu,
                SensorOrientation = SensorOrientation.Default
            }
        );
        BackgroundData currentFrameData = new BackgroundData(); //Only use for body tracking

        while (!_terminate)
        {
            // Get a frame capture.
            var capture = device.GetCapture();

            // Transform the color image to the depth perspective.
            // https://docs.microsoft.com/ja-jp/azure/kinect-dk/use-image-transformation#k4a_transformation_color_image_to_depth_camera
            Image color = transformation.ColorImageToDepthCamera(capture);

            Image bodyIndexMap = null;

            tracker.EnqueueCapture(capture);
            Frame frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false);
            if (frame != null) {
                //
                //Sync body tracking data
                //
                this.provider.IsRunning = true;
                currentFrameData.NumOfBodies = frame.NumberOfBodies;

                //https://docs.microsoft.com/en-us/azure/kinect-dk/body-index-map
                bodyIndexMap = frame.BodyIndexMap;
                //var iii = frame.GetBodyId(0);
                //Debug.Log(iii);

/*
                for(int i = 0; i < 10; i++) {
                    Color p = bodyIndexMap.GetPixel<Color>(64 + i * 4, 64 + i * 4);
                    if (float.IsNaN(p.r) || float.IsNaN(p.g) || float.IsNaN(p.b) || float.IsNaN(p.a)) {
                        continue;
                    }
                    if (p.r > 0 || p.g > 0 || p.b > 0 || p.a > 0) {
                        Debug.Log("bodyIndexMap: ");
                        Debug.Log(p.r + ", " + p.g + ", "  + p.b + ", " + p.a);
                    }
                }
*/

                // Copy bodies.
                for (uint i = 0; i < currentFrameData.NumOfBodies; i++) {
                    currentFrameData.Bodies[i].CopyFromBodyTrackingSdk(
                        frame.GetBody(i),
                        device.GetCalibration()
                    );
                }
                this.provider.SetCurrentFrameData(ref currentFrameData);
            }

            // Push the frame to the capture queue.
            _queue.Enqueue((capture, color, bodyIndexMap));

            // Remove old frames.
            TrimQueue(1);

            // Apply changes on the device settings.
            setter.ApplySettings(device, Settings);
        }

        // Cleaning up.
        transformation.Dispose();
        device.Dispose();
    }

    public void UpdateSkelton()
    {
        if (this.provider.IsRunning)
        {
            if (this.provider.GetCurrentFrameData(ref lastBackgroundData))
            {
                if (lastBackgroundData.NumOfBodies != 0)
                {
                    this.bodyObj.GetComponent<TrackerHandler>().updateTracker(lastBackgroundData);
                }
            }
        }
    }

    #endregion
}

}
