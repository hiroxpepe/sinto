// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NAudio.Wave;
using Sinto.Core.Synth;

namespace Sinto.Audition;

/// <summary>SINTO α — Roland-style WPF audition.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public partial class MainWindow : Window
{
    const int SR = 44100;
    const int CH = 2;

    Engine? _engine;
    WaveOutEvent? _output;
    SintoProvider? _provider;

    bool _loaded = false;
    int _octave = 4;
    WaveType _current_wave = WaveType.Saw;
    FilterKind _current_filter = FilterKind.Moog;

    // PC key → semitone offset from C0 base
    static readonly Dictionary<Key, int> KEY_MAP = new() {
        // Lower row (z=C, m=B in selected octave)
        { Key.Z, 0  }, { Key.S, 1  }, { Key.X, 2  }, { Key.D, 3  }, { Key.C, 4  },
        { Key.V, 5  }, { Key.G, 6  }, { Key.B, 7  }, { Key.H, 8  }, { Key.N, 9  },
        { Key.J, 10 }, { Key.M, 11 },
        // Upper row (q=C+1 octave)
        { Key.Q, 12 }, { Key.D2, 13 }, { Key.W, 14 }, { Key.D3, 15 }, { Key.E, 16 },
        { Key.R, 17 }, { Key.D5, 18 }, { Key.T, 19 }, { Key.D6, 20 }, { Key.Y, 21 },
        { Key.D7, 22 }, { Key.U, 23 },
    };
    readonly HashSet<int> _held_notes = new();
    readonly Dictionary<int, Rectangle> _key_to_rect = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _engine = new Engine(SR, CH, 32, 1024);
        _engine.SetWave(_current_wave);
        ApplyFilter();
        ApplyEnvelope();

        _provider = new SintoProvider(_engine);
        _output = new WaveOutEvent { DesiredLatency = 50 };
        _output.Init(_provider);
        _output.Play();

        BuildKeyboard();
        HighlightWave();
        HighlightFilter();
        UpdateDisplay();
        // マウスホイールでスライダー操作
        RegisterMouseWheel(SldCutoff,  Filter_Changed, 5);
        RegisterMouseWheel(SldReso,    Filter_Changed, 5);
        RegisterMouseWheel(SldEnvAmt,  Filter_Changed, 5);
        RegisterMouseWheel(SldLevel,   Level_Changed,  5);
        RegisterMouseWheel(SldA,       Env_Changed,    2);
        RegisterMouseWheel(SldD,       Env_Changed,    2);
        RegisterMouseWheel(SldS,       Env_Changed,    2);
        RegisterMouseWheel(SldR,       Env_Changed,    2);
        RegisterMouseWheel(SldFA,       FilterEnv_Changed, 2);
        RegisterMouseWheel(SldFD,       FilterEnv_Changed, 2);
        RegisterMouseWheel(SldFS,       FilterEnv_Changed, 2);
        RegisterMouseWheel(SldFR,       FilterEnv_Changed, 2);
        RegisterMouseWheel(SldOsc1Lvl, Osc_Changed,    5);
        RegisterMouseWheel(SldOsc2Lvl, Osc_Changed,    5);
        RegisterMouseWheel(SldDetune,  Osc_Changed,    2);
        Focus();
        _loaded = true;
        // Apply initial slider values (events were suppressed during XAML init)
        ApplyOsc();
        ApplyFilter();
        ApplyFilterEnv();
        ApplyEnvelope();
    }

    void MainWindow_Closed(object? sender, EventArgs e)
    {
        _output?.Stop();
        _output?.Dispose();
        _engine?.Dispose();
    }

    // ── Keyboard rendering ──────────────────────────────────────────────
    void BuildKeyboard()
    {
        Keyboard.Children.Clear();
        _key_to_rect.Clear();
        // 21 white keys (3 octaves), with black keys layered on top
        const double WW = 30, WH = 78, BW = 18, BH = 48;
        int[] white_semitones = { 0,2,4,5,7,9,11, 12,14,16,17,19,21,23, 24,26,28,29,31,33,35 };
        int[] black_semitones = { 1,3,-1,6,8,10,-1, 13,15,-1,18,20,22,-1, 25,27,-1,30,32,34,-1 };

        for (int i = 0; i < white_semitones.Length; i++)
        {
            int semi = white_semitones[i];
            var rect = new Rectangle
            {
                Width = WW - 1, Height = WH,
                Fill = Brushes.WhiteSmoke,
                Stroke = new SolidColorBrush(Color.FromRgb(0xaa, 0xaa, 0xaa)),
                StrokeThickness = 1,
                RadiusX = 0, RadiusY = 0,
                Tag = semi,
            };
            rect.MouseLeftButtonDown += KeyRect_Down;
            rect.MouseLeftButtonUp += KeyRect_Up;
            rect.MouseLeave += KeyRect_Up;
            Canvas.SetLeft(rect, i * WW);
            Canvas.SetTop(rect, 0);
            Keyboard.Children.Add(rect);
            _key_to_rect[semi] = rect;
        }
        for (int i = 0; i < black_semitones.Length; i++)
        {
            int semi = black_semitones[i];
            if (semi < 0) continue;
            var rect = new Rectangle
            {
                Width = BW, Height = BH,
                Fill = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x3a)),
                StrokeThickness = 1,
                Tag = semi,
            };
            rect.MouseLeftButtonDown += KeyRect_Down;
            rect.MouseLeftButtonUp += KeyRect_Up;
            rect.MouseLeave += KeyRect_Up;
            Canvas.SetLeft(rect, (i + 1) * WW - BW / 2);
            Canvas.SetTop(rect, 0);
            Panel.SetZIndex(rect, 2);
            Keyboard.Children.Add(rect);
            _key_to_rect[semi] = rect;
        }
    }

    // ── Mouse interaction on keyboard ───────────────────────────────────
    void KeyRect_Down(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle r && r.Tag is int semi)
        {
            int midi = (_octave + 1) * 12 + semi;
            PressNote(midi, r);
        }
    }
    void KeyRect_Up(object sender, MouseEventArgs e)
    {
        if (sender is Rectangle r && r.Tag is int semi)
        {
            int midi = (_octave + 1) * 12 + semi;
            ReleaseNote(midi, r);
        }
    }

    // ── PC keyboard ─────────────────────────────────────────────────────
    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat) return;
        // Function keys for waves
        switch (e.Key)
        {
            case Key.F1: SetWave(WaveType.Saw);      return;
            case Key.F2: SetWave(WaveType.Square);   return;
            case Key.F3: SetWave(WaveType.Triangle); return;
            case Key.F4: SetWave(WaveType.Sine);     return;
            case Key.F5: SetWave(WaveType.Noise);    return;
            case Key.OemOpenBrackets:   AdjustCutoff(-5); return;
            case Key.OemCloseBrackets:  AdjustCutoff(+5); return;
            case Key.OemSemicolon:      AdjustReso(-5);   return;
            case Key.OemQuotes:         AdjustReso(+5);   return;
        }
        if (KEY_MAP.TryGetValue(e.Key, out int semi))
        {
            int midi = (_octave + 1) * 12 + semi;
            _key_to_rect.TryGetValue(semi, out var rect);
            PressNote(midi, rect);
            e.Handled = true;
        }
    }
    void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (KEY_MAP.TryGetValue(e.Key, out int semi))
        {
            int midi = (_octave + 1) * 12 + semi;
            _key_to_rect.TryGetValue(semi, out var rect);
            ReleaseNote(midi, rect);
            e.Handled = true;
        }
    }

    void PressNote(int midi, Rectangle? rect)
    {
        if (_engine == null || _held_notes.Contains(midi)) return;
        _engine.SendNoteOn(midi, 0.8f, 2, 5, 0);
        _held_notes.Add(midi);
        if (rect != null)
            rect.Fill = (Brush)FindResource(IsBlackKey(midi) ? "DcoColor" : "VcfColor");
        UpdateDisplay(midi);
    }
    void ReleaseNote(int midi, Rectangle? rect)
    {
        if (_engine == null || !_held_notes.Contains(midi)) return;
        _engine.SendNoteOff(midi, 2, 0);
        _held_notes.Remove(midi);
        if (rect != null)
            rect.Fill = IsBlackKey(midi)
                ? new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a))
                : Brushes.WhiteSmoke;
    }
    static bool IsBlackKey(int midi)
    {
        int n = midi % 12;
        return n == 1 || n == 3 || n == 6 || n == 8 || n == 10;
    }

    // ── Mouse wheel helper ─────────────────────────────────────────────────
    void RegisterMouseWheel(Slider sld,
        RoutedPropertyChangedEventHandler<double> handler, double step)
    {
        sld.MouseWheel += (s, e) => {
            sld.Value = Math.Max(sld.Minimum,
                        Math.Min(sld.Maximum,
                        sld.Value + (e.Delta > 0 ? step : -step)));
            e.Handled = true;
        };
    }

    // ── OSC params ─────────────────────────────────────────────────────────
    void Osc_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        ValOsc1Lvl.Text = ((int)SldOsc1Lvl.Value).ToString();
        ValOsc2Lvl.Text = ((int)SldOsc2Lvl.Value).ToString();
        // Detune: slider 0-100 → -50 to +50 cents (center=50 → 0)
        int detune = (int)SldDetune.Value - 50;
        ValDetune.Text = (detune >= 0 ? "+" : "") + detune.ToString();
        ApplyOsc();
    }
    void ApplyOsc()
    {
        float l1 = (float)(SldOsc1Lvl?.Value ?? 100) / 100f;
        float l2 = (float)(SldOsc2Lvl?.Value ?? 50)  / 100f;
        float dt = (float)(SldDetune  ?.Value ?? 50) - 50f; // -50 to +50 cents
        _engine?.SetOscParams(l1, l2, dt);
    }

    // ── Wave selection ──────────────────────────────────────────────────
    void WaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag && int.TryParse(tag, out int t))
        {
            WaveType w = t switch
            {
                0  => WaveType.Saw,
                3  => WaveType.Square,
                2  => WaveType.Triangle,
                -1 => WaveType.Sine,
                4  => WaveType.Noise,
                _  => WaveType.Saw,
            };
            SetWave(w);
        }
        Focus();
    }
    void SetWave(WaveType w)
    {
        _current_wave = w;
        _engine?.SetWave(w);
        HighlightWave();
        UpdateDisplay();
    }
    void HighlightWave()
    {
        Brush sel  = (Brush)FindResource("DcoColor");
        Brush def  = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));
        Brush selF = Brushes.White;
        Brush defF = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77));
        WaveSaw.Background = _current_wave == WaveType.Saw      ? sel : def;
        WaveSqr.Background = _current_wave == WaveType.Square   ? sel : def;
        WaveTri.Background = _current_wave == WaveType.Triangle ? sel : def;
        WaveSin.Background = _current_wave == WaveType.Sine     ? sel : def;
        WaveNoi.Background = _current_wave == WaveType.Noise    ? sel : def;
        WaveSaw.Foreground = _current_wave == WaveType.Saw      ? selF : defF;
        WaveSqr.Foreground = _current_wave == WaveType.Square   ? selF : defF;
        WaveTri.Foreground = _current_wave == WaveType.Triangle ? selF : defF;
        WaveSin.Foreground = _current_wave == WaveType.Sine     ? selF : defF;
        WaveNoi.Foreground = _current_wave == WaveType.Noise    ? selF : defF;
    }

    // ── Filter selection ────────────────────────────────────────────────
    void FiltBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag && int.TryParse(tag, out int t))
        {
            _current_filter = t == 1 ? FilterKind.Moog : FilterKind.Roland;
            ApplyFilter();
            HighlightFilter();
        }
        Focus();
    }
    void HighlightFilter()
    {
        Brush sel  = (Brush)FindResource("VcfColor");
        Brush def  = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));
        Brush selF = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a));
        Brush defF = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77));
        FiltMoog.Background = _current_filter == FilterKind.Moog   ? sel : def;
        FiltRol .Background = _current_filter == FilterKind.Roland ? sel : def;
        FiltMoog.Foreground = _current_filter == FilterKind.Moog   ? selF : defF;
        FiltRol .Foreground = _current_filter == FilterKind.Roland ? selF : defF;
    }
    void Filter_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        ValCutoff.Text = ((int)SldCutoff.Value).ToString();
        ValReso.Text   = ((int)SldReso.Value).ToString();
        ValEnvAmt.Text = ((int)SldEnvAmt.Value).ToString();
        ApplyFilter();
    }
    void ApplyFilter()
    {
        float c = (float)(SldCutoff?.Value ?? 100) / 100f;
        float r = (float)(SldReso  ?.Value ?? 0) / 100f;
        float ea = (float)(SldEnvAmt?.Value ?? 0) / 100f;
        _engine?.SetFilterParams(c, r, _current_filter);
        _engine?.SetFilterEnvAmount(ea);
    }
    void AdjustCutoff(int delta)
    {
        SldCutoff.Value = Math.Max(0, Math.Min(100, SldCutoff.Value + delta));
    }
    void AdjustReso(int delta)
    {
        SldReso.Value = Math.Max(0, Math.Min(100, SldReso.Value + delta));
    }

    // ── Filter Envelope ────────────────────────────────────────────────────
    void FilterEnv_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        ValFA.Text = ((int)SldFA.Value).ToString();
        ValFD.Text = ((int)SldFD.Value).ToString();
        ValFS.Text = ((int)SldFS.Value).ToString();
        ValFR.Text = ((int)SldFR.Value).ToString();
        ApplyFilterEnv();
    }
    void ApplyFilterEnv()
    {
        float a = MapEnvTime(SldFA?.Value ?? 5);
        float d = MapEnvTime(SldFD?.Value ?? 30);
        float s = (float)(SldFS?.Value ?? 0) / 100f;
        float r = MapEnvTime(SldFR?.Value ?? 20);
        _engine?.SetFilterEnv(a, d, s, r);
    }

    // ── VCA Level ───────────────────────────────────────────────────────
    void Level_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        ValLevel.Text = ((int)SldLevel.Value).ToString();
        // VCA level not yet wired to Engine (placeholder for β version)
    }

    // ── Envelope ────────────────────────────────────────────────────────
    void Env_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        ValA.Text = ((int)SldA.Value).ToString();
        ValD.Text = ((int)SldD.Value).ToString();
        ValS.Text = ((int)SldS.Value).ToString();
        ValR.Text = ((int)SldR.Value).ToString();
        ApplyEnvelope();
    }
    void ApplyEnvelope()
    {
        float a = MapEnvTime(SldA?.Value ?? 5);
        float d = MapEnvTime(SldD?.Value ?? 30);
        float s = (float)(SldS?.Value ?? 75) / 100f;
        float r = MapEnvTime(SldR?.Value ?? 40);
        _engine?.SetAmpEnv(a, d, s, r);
    }
    static float MapEnvTime(double v)
    {
        // 0..100 → 0.001..2.0 seconds (exponential feel)
        double n = v / 100.0;
        return (float)(0.001 + n * n * 2.0);
    }

    // ── Octave ──────────────────────────────────────────────────────────
    void OctDown_Click(object sender, RoutedEventArgs e)
    {
        _octave = Math.Max(0, _octave - 1);
        OctVal.Text = _octave.ToString();
        UpdateDisplay();
        Focus();
    }
    void OctUp_Click(object sender, RoutedEventArgs e)
    {
        _octave = Math.Min(8, _octave + 1);
        OctVal.Text = _octave.ToString();
        UpdateDisplay();
        Focus();
    }

    // ── Display ─────────────────────────────────────────────────────────
    void UpdateDisplay(int? midi = null)
    {
        string note = midi.HasValue ? MidiName(midi.Value) : $"C{_octave}";
        DisplayText.Text = $"{note} · {_current_wave.ToString().ToUpper()} · POLY";
    }
    static string MidiName(int midi)
    {
        string[] names = { "C","C#","D","D#","E","F","F#","G","G#","A","A#","B" };
        return names[midi % 12] + (midi / 12 - 1);
    }
}

/// <summary>NAudio bridge — converts Sinto's Span output to NAudio sample provider.</summary>
internal sealed class SintoProvider : ISampleProvider
{
    readonly Engine _engine;
    public WaveFormat WaveFormat { get; }
    public SintoProvider(Engine engine)
    {
        _engine = engine;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    }
    public int Read(float[] buffer, int offset, int count)
    {
        _engine.ProcessAudioCallback(buffer.AsSpan(offset, count));
        return count;
    }
}
