// UI/Form1.Report.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FaceAttendance
{
    public partial class Form1
    {
        private async Task LoadReportAsync()
        {
            try
            {
                var from = _dtpReportFrom.Value.Date;
                var to = _dtpReportTo.Value.Date;

                if (from > to)
                {
                    ShowReportMessage("⚠️ Ngày bắt đầu không được lớn hơn ngày kết thúc.", _warningColor);
                    MessageBox.Show("Ngày bắt đầu không được lớn hơn ngày kết thúc.", "Cảnh báo",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Disable button và show loading
                _btnReportReload.Enabled = false;
                _btnReportReload.Text = "⏳ Đang tải...";
                ShowReportMessage($"⏳ Đang tải báo cáo từ {from:dd/MM/yyyy} đến {to:dd/MM/yyyy}...", _warningColor);

                string url = $"http://127.0.0.1:5000/report?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var rows = ParseReportJson(json);

                // Bind data
            var bindingSource = new BindingSource(new BindingList<ReportRow>(rows), string.Empty);
            _gridReport.DataSource = bindingSource;

                // Clear selection và resize columns
                _gridReport.ClearSelection();
                _gridReport.AutoResizeColumns();

                // Format columns
                FormatReportColumns();

                // Show success message with stats
                var uniqueEmployees = rows.Select(r => r.MaNV).Distinct().Count();
                var totalDays = rows.Select(r => r.Ngay).Distinct().Count();
                
                ShowReportMessage(
                    $"✅ Tải thành công {rows.Count} bản ghi • {uniqueEmployees} nhân viên • {totalDays} ngày làm việc",
                    _successColor
                );
            }
            catch (System.Net.Http.HttpRequestException)
            {
                ShowReportMessage("❌ Không thể kết nối đến server. Vui lòng kiểm tra lại.", _dangerColor);
                MessageBox.Show("Không thể kết nối đến server.\nVui lòng đảm bảo server đang chạy tại http://127.0.0.1:5000", 
                    "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                ShowReportMessage($"❌ Lỗi: {ex.Message}", _dangerColor);
                MessageBox.Show($"Đã xảy ra lỗi:\n{ex.Message}", "Lỗi", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Re-enable button
                _btnReportReload.Enabled = true;
                _btnReportReload.Text = "Làm mới";
            }
        }

        private void ShowReportMessage(string message, Color color)
        {
            if (_lblReportStatus != null)
            {
                _lblReportStatus.Text = message;
                _lblReportStatus.ForeColor = color;
            }
        }

        private void FormatReportColumns()
        {
            if (_gridReport.Columns.Count == 0) return;

            // Set minimum width for each column
            var columnWidths = new Dictionary<string, int>
            {
                { nameof(ReportRow.MaNV), 120 },
                { nameof(ReportRow.TenNV), 200 },
                { nameof(ReportRow.Ngay), 130 },
                { nameof(ReportRow.GioVao), 100 },
                { nameof(ReportRow.GioRa), 100 }
            };

            foreach (DataGridViewColumn col in _gridReport.Columns)
            {
                if (columnWidths.ContainsKey(col.DataPropertyName))
                {
                    col.MinimumWidth = columnWidths[col.DataPropertyName];
                }

                // Center align for date and time columns
                if (col.DataPropertyName == nameof(ReportRow.Ngay) ||
                    col.DataPropertyName == nameof(ReportRow.GioVao) ||
                    col.DataPropertyName == nameof(ReportRow.GioRa))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                // Bold text for employee name
                if (col.DataPropertyName == nameof(ReportRow.TenNV))
                {
                    col.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
                }
            }
        }

        private List<ReportRow> ParseReportJson(string json)
        {
            var list = new List<ReportRow>();
            
            try
            {
                using var doc = JsonDocument.Parse(json);

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var row = new ReportRow
                    {
                        MaNV = GetJsonStringValue(item, "ma_nv"),
                        TenNV = GetJsonStringValue(item, "ten_nv"),
                        Ngay = GetJsonStringValue(item, "ngay"),
                        GioVao = GetJsonStringValue(item, "gio_vao"),
                        GioRa = GetJsonStringValue(item, "gio_ra")
                    };

                    // Format date
                    if (DateTime.TryParse(row.Ngay, out var date))
                    {
                        row.Ngay = date.ToString("dd/MM/yyyy");
                    }

                    // Format time values
                    row.GioVao = FormatTimeValue(row.GioVao);
                    row.GioRa = FormatTimeValue(row.GioRa);

                    list.Add(row);
                }
            }
            catch (JsonException ex)
            {
                throw new Exception($"Lỗi phân tích dữ liệu JSON: {ex.Message}");
            }

            return list;
        }

        private string GetJsonStringValue(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.ValueKind == JsonValueKind.Null ? string.Empty : (prop.GetString() ?? string.Empty);
            }
            return string.Empty;
        }

        private string FormatTimeValue(string timeValue)
        {
            if (string.IsNullOrWhiteSpace(timeValue))
                return "--:--";

            // If already in HH:mm format
            if (TimeSpan.TryParse(timeValue, out var time))
            {
                return time.ToString(@"hh\:mm");
            }

            // If it's a full datetime string
            if (DateTime.TryParse(timeValue, out var dateTime))
            {
                return dateTime.ToString("HH:mm");
            }

            return timeValue;
        }
    }
}