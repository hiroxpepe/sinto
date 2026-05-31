// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Signo.Audition;

/// <summary>
/// Real-time oscilloscope window for SIGNO.
/// Reads PCM samples from ScopeBuffer (filled by the audio thread via
/// SignoProvider) and renders two channels (DCO / LFO) at ~60 Hz.
/// Zero-cross trigger keeps the waveform stable on screen.
/// </summary>
public partial class OscilloscopeWindow : Window
{
    // ── References ──────────────────────────────────────────────────────
    readonly ScopeBuffer _scope;

    // ── State ────────────────────────────────────────────────────────────
    bool _running = true;
    bool _showDco = true;
    bool _trigZero = true;
    double _timeDiv = 15.0;  // display ms (log-mapped from slider)
    double _dcoLevel = 0.707; // display gain (log-mapped from slider)

    // Log map: slider 0..100 → [min, max] with centre (50) at geometric mean.
    static double LogMap(double v, double min, double max)
        => min * Math.Pow(max / min, v / 100.0);

    // Grid line cache
    readonly System.Windows.Shapes.Line[] _hLines = new System.Windows.Shapes.Line[5];
    readonly System.Windows.Shapes.Line[] _vLines = new System.Windows.Shapes.Line[5];

    public OscilloscopeWindow(ScopeBuffer scope)
    {
        InitializeComponent();
        _scope = scope;
        ScopeCanvas.Loaded += (_, _) => {
            BuildGrid();
            UpdateZeroLine();
            CompositionTarget.Rendering += OnRender;
        };
    }

    // ── Grid ─────────────────────────────────────────────────────────────
    void BuildGrid()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
        for (int i = 0; i < 5; i++) {
            var h = new System.Windows.Shapes.Line { Stroke = brush, StrokeThickness = 1 };
            var v = new System.Windows.Shapes.Line { Stroke = brush, StrokeThickness = 1 };
            _hLines[i] = h; _vLines[i] = v;
            ScopeCanvas.Children.Insert(0, h);
            ScopeCanvas.Children.Insert(0, v);
        }
        SizeChanged += (_, _) => { UpdateGrid(); UpdateZeroLine(); };
        UpdateGrid();
    }

    void UpdateGrid()
    {
        double w = ScopeCanvas.ActualWidth, h = ScopeCanvas.ActualHeight;
        for (int i = 0; i < 5; i++) {
            double x = w / 6 * (i + 1);
            _vLines[i].X1 = _vLines[i].X2 = x;
            _vLines[i].Y1 = 0; _vLines[i].Y2 = h;
            double y = h / 6 * (i + 1);
            _hLines[i].Y1 = _hLines[i].Y2 = y;
            _hLines[i].X1 = 0; _hLines[i].X2 = w;
        }
    }

    void UpdateZeroLine()
    {
        double w = ScopeCanvas.ActualWidth, h = ScopeCanvas.ActualHeight;
        double cy = h / 2;
        ZeroLine.X1 = 0; ZeroLine.X2 = w;
        ZeroLine.Y1 = ZeroLine.Y2 = cy;
        TrigArrow.RenderTransform = new TranslateTransform(0, cy - 4);
    }

    // ── Render loop (60Hz via CompositionTarget) ──────────────────────────
    void OnRender(object? sender, EventArgs e)
    {
        if (!_running) return;
        double w = ScopeCanvas.ActualWidth;
        double h = ScopeCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        // _timeDiv is now in milliseconds; convert to sample count.
        int displaySamples = (int)(_timeDiv / 1000.0 * 44100);
        float[] buf = _scope.GetSnapshot(displaySamples);
        if (buf.Length == 0) return;

        // DC offset removal.
        float mean = 0f;
        for (int i = 0; i < buf.Length; i++) mean += buf[i];
        mean /= buf.Length;
        for (int i = 0; i < buf.Length; i++) buf[i] -= mean;

        // Normalise to peak so the waveform fills ~90% of the screen height.
        float peak = 0f;
        for (int i = 0; i < buf.Length; i++) { float a = MathF.Abs(buf[i]); if (a > peak) peak = a; }
        if (peak > 0.001f)
            for (int i = 0; i < buf.Length; i++) buf[i] /= peak;

        WaveDco.Points = _showDco ? BuildPoints(buf, w, h, _dcoLevel) : new PointCollection();

        UpdateStatus(buf);
    }

    PointCollection BuildPoints(float[] buf, double w, double h, double level)
    {
        if (buf.Length == 0) return new PointCollection();
        double cy = h / 2;
        int start = 0;

        // Zero-cross trigger: find the first upward zero crossing.
        if (_trigZero) {
            for (int i = 1; i < buf.Length - 1; i++) {
                if (buf[i - 1] < 0 && buf[i] >= 0) { start = i; break; }
            }
        }

        int len = buf.Length - start;
        var pts = new PointCollection(len);
        double xStep = w / len;
        for (int i = 0; i < len; i++) {
            double x = i * xStep;
            float s = buf[start + i];
            double y = cy - s * cy * 0.85 * level;
            pts.Add(new Point(x, y));
        }
        return pts;
    }

    void UpdateStatus(float[] buf)
    {
        if (buf.Length == 0) return;
        // Estimate frequency from zero crossings
        int crossings = 0;
        for (int i = 1; i < buf.Length; i++)
            if (buf[i - 1] < 0 && buf[i] >= 0) crossings++;
        const int SR = 44100;
        double durationSec = (double)buf.Length / SR;
        if (crossings > 1) {
            double freq = (crossings - 1) / durationSec;
            StatDcoFreq.Text = freq < 1000
                ? $"{freq:F1}Hz"
                : $"{freq / 1000:F2}kHz";
            double period = 1.0 / freq * 1000;
            StatPeriod.Text = $"{period:F2}ms";
        }
    }

    // ── Button handlers ──────────────────────────────────────────────────
    void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        _running = true;
        BtnRun.Foreground = new SolidColorBrush(Colors.White);
        BtnRun.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    }

    void BtnHold_Click(object sender, RoutedEventArgs e)
    {
        _running = false;
        BtnRun.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        BtnRun.Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a));
    }

    void ChBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b) {
            _showDco = !_showDco;
            ToggleBtn(b, _showDco);
        }
    }

    void TrigBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b) {
            _trigZero = (string)b.Tag == "zero";
            TrigLabel.Text = _trigZero ? "TRIG · ZERO" : "TRIG · FREE";
        }
    }

    void SldTimeDiv_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Log map: 0..100 → 5ms..45ms, centre 50 = 15ms
        _timeDiv = LogMap(e.NewValue, 5.0, 45.0);
        if (LblTimeDiv != null) LblTimeDiv.Text = $"{_timeDiv:F1}ms";
    }

    void SldDcoLevel_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Log map: 0..100 → 0.2..2.5, centre 50 = 0.707 ≈ 0.7
        _dcoLevel = LogMap(e.NewValue, 0.2, 2.5);
        if (LblDcoLevel != null) LblDcoLevel.Text = $"×{_dcoLevel:F1}";
    }

    void ToggleBtn(Button b, bool on)
    {
        b.Background = new SolidColorBrush(on
            ? Color.FromRgb(0x33, 0x33, 0x33)
            : Color.FromRgb(0x1a, 0x1a, 0x1a));
        b.Foreground = new SolidColorBrush(on ? Colors.White : Color.FromRgb(0x55, 0x55, 0x55));
    }

    void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _running = false;
        CompositionTarget.Rendering -= OnRender;
    }
}
