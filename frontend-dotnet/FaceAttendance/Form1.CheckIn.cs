// UI/Form1.checkIn.cs
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FaceAttendance
{
    public partial class Form1
    {
        private async Task AutoCheckInAsync()
        {
            if (_isProcessingCheckIn)
                return;

            if (!_isCheckInViewMode && !_isCheckInMode)
                return;

            Bitmap? snapshot = null;
            lock (_frameLock)
            {
                if (_currentFrame != null)
                    snapshot = (Bitmap)_currentFrame.Clone();
            }

            if (snapshot == null)
                return;

            _isProcessingCheckIn = true;

            try
            {
                if (_isCheckInViewMode && !_isCheckInMode && !HasRecentMotion())
                {
                    SetStatusText(true, "💤 Đang chờ phát hiện người...", _mutedText);
                    return;
                }

                if (_isCheckInViewMode && DateTime.Now < _cooldownUntil)
                {
                    SetStatusText(true, "✅ Đã chấm công. Vui lòng đợi người tiếp theo.", _successColor);
                    return;
                }

                var pose = await GetPoseAsync(snapshot);
                if (pose == null)
                {
                    HandleNoFaceDetected();
                    return;
                }

                _lastFaceDetected = DateTime.Now;

                if (_isCheckInViewMode)
                {
                    if (!_isCheckInMode)
                    {
                        BeginAutoCheckInSession();
                    }
                }
                else if (!_liveness.IsActive)
                {
                    _liveness.StartNewSession();
                    UpdateActionInstruction();
                }

                var livenessPassed = await EvaluateLivenessAsync(snapshot, pose);
                if (!livenessPassed)
                    return;

                using var ms = new MemoryStream();
                snapshot.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                ms.Position = 0;

                using var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(ms.GetBuffer(), 0, (int)ms.Length), "image", "face.jpg");

                var response = await _httpClient.PostAsync(CheckInEndpoint, form);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                UpdateCheckInStatus(result);
            }
            catch (Exception ex)
            {
                SetStatusText(_isCheckInViewMode, $"ERROR: {ex.Message}", _dangerColor);
            }
            finally
            {
                snapshot.Dispose();
                _isProcessingCheckIn = false;
            }
        }

        private void HandleNoFaceDetected()
        {
            if (_isCheckInViewMode)
            {
                if (_isCheckInMode &&
                    _lastFaceDetected != DateTime.MinValue &&
                    DateTime.Now - _lastFaceDetected > TimeSpan.FromSeconds(5))
                {
                    EndAutoCheckInSession("⏹️ Không còn người trong khung. Tạm dừng.", _mutedText);
                }
                else if (_isCheckInMode)
                {
                    SetStatusText(true, "👀 Giữ khuôn mặt trong khung hình để hoàn tất liveness.", _warningColor);
                }
                else
                {
                    SetInstructionText(true, "👋 Vui lòng đứng trước camera để bắt đầu chấm công.", _mutedText, remember: true);
                    SetStatusText(true, "💤 Đang chờ phát hiện người...", _mutedText);
                }
            }
            else if (_isCheckInMode)
            {
                SetStatusText(false, "❌ Không phát hiện khuôn mặt. Vui lòng thử lại.", _dangerColor);
            }
        }

        private void BeginAutoCheckInSession()
        {
            _isCheckInMode = true;
            _cooldownUntil = DateTime.MinValue;
            _liveness.StartNewSession();
            UpdateActionInstruction();
            SetStatusText(true, "😃 Đã phát hiện khuôn mặt. Vui lòng thực hiện theo hướng dẫn liveness.", _successColor);
        }

        private void EndAutoCheckInSession(string message, Color color)
        {
            _isCheckInMode = false;
            _liveness.Reset();
            _cooldownUntil = DateTime.MinValue;
            UpdateActionInstruction();
            SetStatusText(true, message, color);
            ClearRealtimePreview();
            ResetMotionState();
        }

        private void ClearRealtimePreview()
        {
            if (_checkInPreviewBox != null && _checkInPreviewBox.IsHandleCreated)
            {
                _checkInPreviewBox.BeginInvoke(new Action(() =>
                {
                    _checkInPreviewBox.Image?.Dispose();
                    _checkInPreviewBox.Image = null;
                }));
            }
        }

        private void UpdateCheckInStatus(string result)
        {
            string text = "Trạng thái: Không xác định";
            Color color = _mutedText;
            bool shouldStop = false;

            try
            {
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;

                string thongBao = root.TryGetProperty("thong_bao", out var tb) ? tb.GetString() ?? string.Empty : string.Empty;
                string maNv = root.TryGetProperty("ma_nv", out var ma) ? ma.GetString() ?? string.Empty : string.Empty;
                string tenNv = root.TryGetProperty("ten_nv", out var ten) ? ten.GetString() ?? string.Empty : string.Empty;
                string doGiong = root.TryGetProperty("do_giong", out var dg) ? dg.GetString() ?? string.Empty : string.Empty;
                string action = root.TryGetProperty("action", out var act) ? act.GetString() ?? string.Empty : string.Empty;
                double? score = root.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetDouble() : (double?)null;

                bool recognized =
                    !string.IsNullOrWhiteSpace(maNv) ||
                    !string.IsNullOrWhiteSpace(tenNv) ||
                    thongBao.Contains("nhận diện", StringComparison.OrdinalIgnoreCase);

                if (recognized)
                {
                    string name = string.IsNullOrWhiteSpace(tenNv) ? "(Không rõ tên)" : tenNv;
                    string code = string.IsNullOrWhiteSpace(maNv) ? "(Không rõ mã)" : maNv;
                    string accuracy = !string.IsNullOrWhiteSpace(doGiong)
                        ? doGiong
                        : (score.HasValue ? $"{Math.Max(0, Math.Min(1, 1 - score.Value / 0.7)) * 100:F1}%" : "N/A");

                    text = $"Nhận diện: {name} ({code}) – Độ chính xác: {accuracy}";
                    color = _successColor;

                    if (!string.IsNullOrWhiteSpace(action) &&
                        (action.Equals("check_in", StringComparison.OrdinalIgnoreCase) ||
                         action.Equals("check_out", StringComparison.OrdinalIgnoreCase)))
                    {
                        shouldStop = true;
                    }
                }
                else if (thongBao.Contains("không phát hiện", StringComparison.OrdinalIgnoreCase) ||
                         thongBao.Contains("no face", StringComparison.OrdinalIgnoreCase))
                {
                    text = "Không phát hiện khuôn mặt trong khung hình";
                    color = _warningColor;
                }
                else if (!string.IsNullOrEmpty(thongBao))
                {
                    text = thongBao;
                    color = _dangerColor;
                }
            }
            catch
            {
                string lower = result.ToLowerInvariant();
                if (lower.Contains("no face") || lower.Contains("không phát hiện"))
                {
                    text = "Không phát hiện khuôn mặt trong khung hình";
                    color = _warningColor;
                }
                else if (lower.Contains("unknown"))
                {
                    text = "Không nhận diện được";
                    color = _dangerColor;
                }
                else
                {
                    text = result;
                    color = _successColor;
                }
            }

            string final = $"{(color == _successColor ? "✅" : color == _warningColor ? "👀" : "❌")} {text}";
            SetStatusText(_isCheckInViewMode, final, color);

            if (shouldStop)
            {
                HandleCheckInSuccess(final, color);
            }
        }

        private void HandleCheckInSuccess(string message, Color color)
        {
            if (_isCheckInViewMode)
            {
                SetStatusText(true, message, color);
                _isCheckInMode = false;
                _liveness.Reset();
                _cooldownUntil = DateTime.Now.AddSeconds(3);
                UpdateActionInstruction();
                ClearRealtimePreview();
                ResetMotionState();
            }
            else
            {
                StopManualCheckInMode(false);
                SetStatusText(false, message, color);
            }
        }

        private async Task<bool> EvaluateLivenessAsync(Bitmap frame, PoseResult? existingPose = null)
        {
            try
            {
                var pose = existingPose ?? await GetPoseAsync(frame);
                if (pose == null)
                {
                    SetStatusText(_isCheckInViewMode, "❌ Không đọc được pose. Vui lòng thử lại.", _dangerColor);
                    return false;
                }

                if (_liveness.CheckTimeout())
                {
                    FailLiveness("❌ Liveness thất bại (quá thời gian).", critical: true);
                    return false;
                }

                var currentAction = _liveness.CurrentActions[_liveness.CurrentIndex];
                bool poseValid = ValidForAction(currentAction, pose);

                if (pose.Motion < MotionThreshold)
                {
                    SetStatusText(_isCheckInViewMode, "⚠️ Không thấy chuyển động, vui lòng thực hiện rõ hơn.", _warningColor);
                    return false;
                }

                if (!poseValid)
                {
                    SetStatusText(_isCheckInViewMode, "👉 Vui lòng làm đúng hướng dẫn.", _warningColor);
                    return false;
                }

                _liveness.MarkCurrentActionDone();

                if (_liveness.IsPassed)
                {
                    SetStatusText(_isCheckInViewMode, "✅ Hoàn thành liveness. Đang nhận diện...", _successColor);
                    SetInstructionText(true, "✅ Liveness hoàn tất", _successColor, remember: true);
                    return true;
                }

                UpdateActionInstruction();
                return false;
            }
            catch (Exception ex)
            {
                FailLiveness($"❌ Lỗi liveness: {ex.Message}", critical: true);
                return false;
            }
        }

        private async Task<PoseResult?> GetPoseAsync(Bitmap frame)
        {
            using var ms = new MemoryStream();
            frame.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            ms.Position = 0;

            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(ms.GetBuffer(), 0, (int)ms.Length), "image", "frame.jpg");

            var response = await _httpClient.PostAsync(PoseEndpoint, form);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new PoseResult
            {
                Yaw = root.GetProperty("yaw").GetDouble(),
                Pitch = root.GetProperty("pitch").GetDouble(),
                Roll = root.TryGetProperty("roll", out var r) ? r.GetDouble() : 0,
                Motion = root.TryGetProperty("motion", out var m) ? m.GetDouble() : 0
            };
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 360;
            if (angle > 180) angle -= 360;
            if (angle < -180) angle += 360;
            return angle;
        }

        private bool ValidForAction(PoseAction action, PoseResult pose)
        {
            double yaw = NormalizeAngle(pose.Yaw);
            double pitch = NormalizeAngle(pose.Pitch);

            if (pose.Roll > 90 || pose.Roll < -90)
            {
                yaw = NormalizeAngle(yaw + 180);
                pitch = NormalizeAngle(-pitch);
            }

            return action switch
            {
                PoseAction.LookCenter => Math.Abs(yaw) < 10,
                PoseAction.LookLeft => yaw < -15,
                PoseAction.LookRight => yaw > 15,
                PoseAction.LookUp => pitch < -10,
                PoseAction.LookDown => pitch > 10,
                _ => false
            };
        }

        private void FailLiveness(string message, bool critical)
        {
            SetStatusText(_isCheckInViewMode, message, _dangerColor);

            if (critical)
            {
                if (_isCheckInViewMode)
                    EndAutoCheckInSession(message, _dangerColor);
                else
                    StopManualCheckInMode();
            }
        }

        private void UpdateActionInstruction()
        {
            if (!_liveness.IsActive)
            {
                SetInstructionText(true, "🔄 Hệ thống sẵn sàng.", _mutedText, remember: true);
                return;
            }

            if (_liveness.CheckTimeout())
            {
                FailLiveness("❌ Liveness thất bại (quá thời gian).", critical: true);
                return;
            }

            var current = _liveness.CurrentActions[_liveness.CurrentIndex];
            string instruction = current switch
            {
                PoseAction.LookCenter => "Nhìn thẳng vào camera",
                PoseAction.LookLeft => "Quay mặt sang trái",
                PoseAction.LookRight => "Quay mặt sang phải",
                PoseAction.LookUp => "Ngẩng đầu",
                PoseAction.LookDown => "Cúi đầu",
                _ => "Thực hiện theo hướng dẫn"
            };

            SetInstructionText(true, $"👉 Bước {_liveness.CurrentIndex + 1}/{_liveness.CurrentActions.Count}: {instruction}", _warningColor, remember: true);
        }

        private void BtnManualCheckIn_Click(object? sender, EventArgs e)
        {
            if (_isCheckInMode && !_isCheckInViewMode)
            {
                StopManualCheckInMode();
            }
            else
            {
                EnsureCameraRunning();
                StartManualCheckInMode();
            }
        }

        private void StartManualCheckInMode()
        {
            _isCheckInMode = true;
            _isCheckInViewMode = false;

            _lastFaceDetected = DateTime.MinValue;
            _cooldownUntil = DateTime.MinValue;

            _liveness.StartNewSession();
            UpdateActionInstruction();
            SetStatusText(false, "⏳ Đang kiểm tra liveness...", _mutedText);

            if (!_checkInTimer.Enabled)
                _checkInTimer.Start();

            _btnManualCheckIn.Text = "⏹️  Dừng Chấm Công";
            _btnManualCheckIn.BackColor = _dangerColor;

            _btnCapture.Enabled = false;
        }

        private void StopManualCheckInMode(bool showStoppedMessage = true)
        {
            if (!_isCheckInMode || _isCheckInViewMode)
                return;

            _checkInTimer.Stop();
            _isCheckInMode = false;
            _isProcessingCheckIn = false;

            _lastFaceDetected = DateTime.MinValue;
            _cooldownUntil = DateTime.MinValue;

            _liveness.Reset();
            ResetMotionState();

            SetInstructionText(false, "Nhấn \"Quét Chấm Công\" để bắt đầu lại.", _mutedText);
            if (showStoppedMessage)
                SetStatusText(false, "💡 Đã dừng quét", _mutedText);

            _btnManualCheckIn.Text = "⏰  Quét Chấm Công";
            _btnManualCheckIn.BackColor = _warningColor;

            UpdateCaptureState();
        }

        private void StartRealtimeCheckInMode()
        {
            _isCheckInViewMode = true;
            _isCheckInMode = false;
            _isProcessingCheckIn = false;

            _lastFaceDetected = DateTime.MinValue;
            _cooldownUntil = DateTime.MinValue;

            _liveness.Reset();
            ResetMotionState();

            SetInstructionText(true, "👋 Vui lòng đứng trước camera để bắt đầu chấm công.", _mutedText, remember: true);
            SetStatusText(true, "💤 Đang chờ phát hiện người...", _mutedText);

            if (!_checkInTimer.Enabled)
                _checkInTimer.Start();
        }

        private void StopRealtimeCheckInMode()
        {
            if (!_isCheckInViewMode)
                return;

            _checkInTimer.Stop();
            _isCheckInMode = false;
            _isProcessingCheckIn = false;
            _isCheckInViewMode = false;

            _lastFaceDetected = DateTime.MinValue;
            _cooldownUntil = DateTime.MinValue;

            _liveness.Reset();
            ResetMotionState();

            SetInstructionText(true, "🔄 Hệ thống sẵn sàng.", _mutedText, remember: true);
            SetStatusText(true, "💡 Trạng thái: Đã dừng", _mutedText);

            ClearRealtimePreview();
        }

        #region Helper methods for labels

        private void SetInstructionText(bool realtime, string text, Color color, bool remember = false)
        {
            var label = realtime ? _lblCheckInInstruction : _lblInstruction;
            if (label == null) return;

            void Apply()
            {
                label.Text = text;
                label.ForeColor = color;

                if (remember && realtime)
                    _currentInstructionText = text;
            }

            if (label.IsHandleCreated && label.InvokeRequired)
                label.BeginInvoke(new Action(Apply));
            else
                Apply();
        }

        private void SetStatusText(bool realtime, string text, Color color)
        {
            var label = realtime ? _lblCheckInStatus : _lblStatus;
            if (label == null) return;

            void Apply()
            {
                label.Text = text;
                label.ForeColor = color;
            }

            if (label.IsHandleCreated && label.InvokeRequired)
                label.BeginInvoke(new Action(Apply));
            else
                Apply();
        }

        #endregion
    }
}