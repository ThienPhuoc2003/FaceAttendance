// UI/Form1.Camera.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace FaceAttendance
{
    public partial class Form1
    {
        private void InitializeCameraList()
        {
            try
            {
                _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (_videoDevices.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy camera nào!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _lblCameraStatus.Text = $"📷 Camera: {_videoDevices[0].Name}";
                _lblCameraStatus.ForeColor = _successColor;
                BtnStart_Click(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tìm camera: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                StopCamera();
                return;
            }

            EnsureCameraRunning();
        }

        private void EnsureCameraRunning()
        {
            if (_videoSource != null && _videoSource.IsRunning)
                return;

            if (_videoDevices == null || _videoDevices.Count == 0)
                return;

            var device = _videoDevices[0];
            _videoSource = new VideoCaptureDevice(device.MonikerString);
            _videoSource.NewFrame += VideoSource_NewFrame;
            _videoSource.Start();

            _lblCameraStatus.Text = $"📷 Camera: {device.Name} (Đang chạy)";
            _lblCameraStatus.ForeColor = _successColor;
            _btnStart.Text = "Tắt Camera";
            _btnStart.BackColor = _dangerColor;
            _btnCapture.Enabled = true;
        }

        private void StopCamera()
        {
            if (_videoSource == null)
                return;

            try
            {
                if (_videoSource.IsRunning)
                {
                    _videoSource.SignalToStop();
                    _videoSource.WaitForStop();
                }
            }
            catch { }
            finally
            {
                _videoSource.NewFrame -= VideoSource_NewFrame;
                _videoSource = null;
            }

            _btnStart.Text = " Bật Camera";
            _btnStart.BackColor = _primaryColor;
            _btnCapture.Enabled = false;
            _lblCameraStatus.Text = "📷 Camera: Đã tắt";
            _lblCameraStatus.ForeColor = _mutedText;

            _previewBox?.Image?.Dispose();
            _previewBox!.Image = null;

            if (_checkInPreviewBox != null)
            {
                _checkInPreviewBox.Image?.Dispose();
                _checkInPreviewBox.Image = null;
            }

            lock (_frameLock)
            {
                _currentFrame?.Dispose();
                _currentFrame = null;
            }

            ResetMotionState();
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            var frame = (Bitmap)eventArgs.Frame.Clone();

            UpdateMotionState(frame);

            lock (_frameLock)
            {
                _currentFrame?.Dispose();
                _currentFrame = (Bitmap)frame.Clone();
            }

            if (_currentView == MenuView.Register && _previewBox != null && _previewBox.IsHandleCreated)
            {
                _previewBox.BeginInvoke(new Action(() =>
                {
                    _previewBox.Image?.Dispose();
                    _previewBox.Image = (Bitmap)frame.Clone();
                }));
            }
            else if (_currentView == MenuView.CheckIn && _checkInPreviewBox != null && _checkInPreviewBox.IsHandleCreated)
            {
                _checkInPreviewBox.BeginInvoke(new Action(() =>
                {
                    _checkInPreviewBox.Image?.Dispose();
                    _checkInPreviewBox.Image = frame;
                }));
            }
            else
            {
                frame.Dispose();
            }
        }

        private void UpdateMotionState(Bitmap frame)
        {
            try
            {
                const int sampleW = 32;
                const int sampleH = 18;

                using var scaled = new Bitmap(sampleW, sampleH);
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.Low;
                    g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                    g.SmoothingMode = SmoothingMode.None;
                    g.DrawImage(frame, new Rectangle(0, 0, sampleW, sampleH));
                }

                var rect = new Rectangle(0, 0, sampleW, sampleH);
                var data = scaled.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                int stride = data.Stride;
                int bytes = stride * sampleH;
                var current = new byte[bytes];
                Marshal.Copy(data.Scan0, current, 0, bytes);

                scaled.UnlockBits(data);

                lock (_motionLock)
                {
                    if (_prevMotionFrame == null || _prevMotionFrame.Length != bytes)
                    {
                        _prevMotionFrame = current;
                        return;
                    }

                    double diff = 0;
                    int count = 0;
                    for (int y = 0; y < sampleH; y++)
                    {
                        int row = y * stride;
                        for (int x = 0; x < sampleW * 3; x += 6)
                        {
                            int idx = row + x;
                            diff += Math.Abs(current[idx] - _prevMotionFrame[idx]);
                            count++;
                        }
                    }

                    double avgDiff = diff / Math.Max(1, count);

                    if (avgDiff > MotionDiffThreshold)
                    {
                        _lastMotionTime = DateTime.Now;
                    }

                    Buffer.BlockCopy(current, 0, _prevMotionFrame, 0, bytes);
                }
            }
            catch
            {
                // Ignore motion errors
            }
        }

        private void ResetMotionState()
        {
            lock (_motionLock)
            {
                _prevMotionFrame = null;
                _lastMotionTime = DateTime.MinValue;
            }
        }

        private bool HasRecentMotion()
        {
            lock (_motionLock)
            {
                if (_lastMotionTime == DateTime.MinValue)
                    return false;

                return DateTime.Now - _lastMotionTime < _motionPersistence;
            }
        }
    }
}