// UI/Form1.Capture.cs
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace FaceAttendance
{
    public partial class Form1
    {
        private void BtnCapture_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtMaNV.Text) || string.IsNullOrWhiteSpace(_txtTenNV.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin nhân viên!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int nextIndex = Array.FindIndex(_captures, bmp => bmp is null);
            if (nextIndex == -1)
            {
                MessageBox.Show("Đã chụp đủ 5 ảnh!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Bitmap snapshot;
            lock (_frameLock)
            {
                if (_currentFrame == null) return;
                snapshot = (Bitmap)_currentFrame.Clone();
            }

            _captures[nextIndex]?.Dispose();
            _captures[nextIndex] = snapshot;
            _captureBoxes[nextIndex].Image?.Dispose();
            _captureBoxes[nextIndex].Image = snapshot;
            _captureBoxes[nextIndex].BackColor = _successColor;

            UpdateCaptureState();
        }

        private void UpdateCaptureState()
        {
            int count = _captures.Count(b => b != null);
            _lblCapturedCount.Text = $"📸 Đã chụp: {count}/5 ảnh";

            int nextIndex = Array.FindIndex(_captures, bmp => bmp is null);
            if (nextIndex == -1)
            {
                _lblInstruction.Text = "✅ Hoàn tất! Nhấn 'Đăng Ký' để lưu thông tin";
                _lblInstruction.ForeColor = _successColor;
                _btnRegister.Enabled = true;
            }
            else
            {
                _lblInstruction.Text = $"👉 Bước {nextIndex + 1}: {_captureSteps[nextIndex]}";
                _lblInstruction.ForeColor = _warningColor;
                _btnRegister.Enabled = false;
            }

            _btnCapture.Enabled = !_isCheckInMode && _videoSource != null && _videoSource.IsRunning;
        }

        private void Thumbnail_Click(object? sender, EventArgs e)
        {
            if (sender is not PictureBox thumb || thumb.Tag is not int index) return;
            if (_captures[index] == null) return;

            if (MessageBox.Show($"Xóa ảnh '{_captureSteps[index]}'?", "Xác nhận", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _captures[index]?.Dispose();
            _captures[index] = null;
            thumb.Image?.Dispose();
            thumb.Image = null;
            thumb.BackColor = _darkBg;

            ResetThumbnailScroll();
            UpdateCaptureState();
        }

        private void ResetThumbnailScroll()
        {
            _thumbPanel?.AutoScrollPosition = new Point(0, 0);
        }

        private async void BtnRegister_Click(object? sender, EventArgs e)
        {
            if (_captures.Any(b => b is null))
            {
                MessageBox.Show("Cần chụp đủ 5 ảnh!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnRegister.Enabled = false;

            try
            {
                var images = _captures.Select(bmp => bmp!).ToList();
                await SendImagesToServer(images, _txtMaNV.Text.Trim(), _txtTenNV.Text.Trim());
                MessageBox.Show("✅ Đăng ký thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearCapturedImages();
                _txtMaNV.Clear();
                _txtTenNV.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Lỗi: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UpdateCaptureState();
            }
        }

        private static async Task SendImagesToServer(List<Bitmap> images, string maNV, string tenNV)
        {
            using var client = new HttpClient();
            using var form = new MultipartFormDataContent();

            form.Add(new StringContent(maNV), "ma_nv");
            form.Add(new StringContent(tenNV), "ten_nv");

            for (int i = 0; i < images.Count; i++)
            {
                using var ms = new MemoryStream();
                images[i].Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                ms.Position = 0;
                form.Add(new ByteArrayContent(ms.ToArray()), $"image_{i}", $"capture_{i + 1}.jpg");
            }

            using var response = await client.PostAsync("http://127.0.0.1:5000/register", form);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string message = "Đăng ký thất bại.";
                try
                {
                    var json = JObject.Parse(responseText);
                    message = json["message"]?.ToString() ?? message;
                }
                catch { }

                throw new Exception(message);
            }
        }

        private void ClearCapturedImages()
        {
            for (int i = 0; i < _captures.Length; i++)
            {
                _captures[i]?.Dispose();
                _captures[i] = null;
                _captureBoxes[i].Image?.Dispose();
                _captureBoxes[i].Image = null;
                _captureBoxes[i].BackColor = _darkBg;
            }

            ResetThumbnailScroll();
        }
    }
}