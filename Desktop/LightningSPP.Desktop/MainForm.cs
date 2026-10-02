using System.Windows.Forms;
using wwwmmm.PublicAPI;

namespace LightningSPP.Desktop;

public class MainForm : Form
{
    private readonly ListBox _files = new();
    private readonly Button _processButton = new();
    private readonly Label _status = new();

    public MainForm()
    {
        Text = "Lightning SPP Desktop";
        Width = 900;
        Height = 580;

        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        var title = new Label
        {
            Text = "Drag & Drop files here",
            Dock = DockStyle.Top,
            Height = 44,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14, FontStyle.Bold)
        };

        _files.Dock = DockStyle.Fill;

        _processButton.Text = "Process Selected";
        _processButton.Height = 42;
        _processButton.Dock = DockStyle.Bottom;
        _processButton.Click += async (_, __) => await ProcessSelectedAsync();

        _status.Text = "Ready";
        _status.Dock = DockStyle.Bottom;
        _status.Height = 28;
        _status.TextAlign = ContentAlignment.MiddleCenter;

        Controls.Add(_files);
        Controls.Add(title);
        Controls.Add(_status);
        Controls.Add(_processButton);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] dropped)
        {
            return;
        }

        foreach (var path in dropped)
        {
            if (!_files.Items.Contains(path))
            {
                _files.Items.Add(path);
            }
        }

        _status.Text = $"Loaded {_files.Items.Count} file(s)";
    }

    private async Task ProcessSelectedAsync()
    {
        if (_files.SelectedItem is not string source)
        {
            _status.Text = "Select a file first";
            return;
        }

        try
        {
            LightningAPI.UseSimulatedServices();
            _status.Text = "Processing...";
            var result = await LightningAPI.ExecuteLightningPipelineAsync(source);
            _status.Text = result.Success ? "Success" : $"Failed: {result.Error}";
        }
        catch (Exception ex)
        {
            _status.Text = $"Error: {ex.Message}";
        }
    }
}
