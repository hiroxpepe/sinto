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
using NAudio.Midi;
using Signo.Core.Synth;

namespace Signo.Audition;

/// <summary>SIGNO α — Roland-style WPF audition.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public partial class MainWindow : Window
{
    const int SR = 44100;
    const int CH = 2;

    Engine? _engine;
    WasapiOut? _output;
    SignoProvider? _provider;
    readonly ScopeBuffer _scope = new();
    OscilloscopeWindow? _scopeWin;
    MidiIn? _midi_in;

    bool _loaded = false;
    int _octave = 4;
    WaveType _current_wave = WaveType.Saw;
    FilterKind _current_filter = FilterKind.Moog;
    int _osc1_range_oct = 0;
    int _osc2_range_oct = 0;
    ArpMode _arp_mode = ArpMode.Up;
    bool _arp_on = false;
    bool _porta_on = false;
    LfoWave _lfo_wave = LfoWave.Sine;

    // Debug log path: signo_debug.log in working directory
    // DebugLog is compiled only in DEBUG builds (#if DEBUG).
    // Release builds contain zero logging code — no runtime overhead.
#if DEBUG
    static readonly string LOG_PATH = System.IO.Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "signo_debug.log");

    static void DebugLog(string msg)
    {
        try {
            System.IO.File.AppendAllText(LOG_PATH,
                $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        } catch { /* never throw from logging */ }
    }
#else
    static void DebugLog(string msg) { }
#endif

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

        _provider = new SignoProvider(_engine);
        _provider.Scope = _scope;
        // WASAPI exclusive mode for low-latency MIDI playing (no ASIO).
        // Exclusive + small latency gives ~5-15ms; fall back to shared if the
        // device refuses exclusive or the IEEE-float format is unsupported.
        try {
            _output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Exclusive, 10);
            _output.Init(_provider);
            _output.Play();
            DebugLog("OUT: WASAPI exclusive, 10ms latency.");
        } catch (Exception ex) {
            DebugLog($"OUT: exclusive failed ({ex.Message}); using shared mode.");
            _output?.Dispose();
            _output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 30);
            _output.Init(_provider);
            _output.Play();
        }

        BuildKeyboard();
        HighlightFilter();
        RegisterAllGroupButtons(this);
        // Initial selections.
        SelectInitialByTag("osc1wave:saw");
        SelectInitialByTag("osc2wave:saw");
        SelectInitialByTag("osc1range:8");
        SelectInitialByTag("osc2range:8");
        SelectInitialByTag("lfowave:sine");
        SelectInitialByTag("arpmode:up");
        SelectInitialByTag("arponoff:off");
        SelectInitialByTag("portaonoff:off");
        UpdateDisplay();
        // Register mouse wheel events for sliders
        // Cutoff/Reso/EnvAmt use fine 0.25 step for smooth filter sweeps (Synth1-class).
        RegisterMouseWheel(SldCutoff,  Filter_Changed, 0.25);
        RegisterMouseWheel(SldReso,    Filter_Changed, 0.25);
        RegisterMouseWheel(SldEnvAmt,  Filter_Changed, 0.25);
        RegisterMouseWheel(SldLevel,   Level_Changed,  5);
        RegisterMouseWheel(SldA,       Env_Changed,    2);
        RegisterMouseWheel(SldD,       Env_Changed,    2);
        RegisterMouseWheel(SldS,       Env_Changed,    2);
        RegisterMouseWheel(SldR,       Env_Changed,    2);
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
        _engine?.SetDcaLevel((float)(SldLevel.Value / 100.0));
        _engine?.SetHpf((float)SldHpf.Value);
        ApplyLfoRouting();
        ApplyPortamento();
        _engine?.SetArpRate(40f + (float)(SldArpRate.Value / 100.0) * 200f);
        UpdateAllKnobVisuals();
        ApplyFx();
        DebugLog($"=== SIGNO started. Log: {LOG_PATH} ===");
        InitMidi();
    }

    void InitMidi()
    {
        // MVP: auto-connect the first available MIDI input device.
        if (MidiIn.NumberOfDevices == 0) {
            DebugLog("MIDI: no input device found (play with PC keys or on-screen keys).");
            return;
        }
        try {
            string name = MidiIn.DeviceInfo(0).ProductName;
            _midi_in = new MidiIn(0);
            _midi_in.MessageReceived += Midi_MessageReceived;
            _midi_in.Start();
            DebugLog($"MIDI: connected to \"{name}\" (device 0).");
        } catch (Exception ex) {
            DebugLog($"MIDI: failed to open device 0: {ex.Message}");
            _midi_in = null;
        }
    }

    // Panic: force-release every held note. Recovers from any MIDI note-off that
    // was dropped by the driver/port (small controllers can miss note-offs during
    // fast chords), which otherwise leaves a note ringing forever.
    void AllNotesOff()
    {
        if (_engine == null) return;
        foreach (int midi in new List<int>(_held_notes))
            SoundOff(midi);                       // via the intermediate queue (SPSC-safe)
        _held_notes.Clear();
        // reset on-screen key colours
        foreach (var kv in _key_to_rect)
            kv.Value.Fill = IsBlackKey((_octave + 1) * 12 + kv.Key)
                ? new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a))
                : Brushes.WhiteSmoke;
        DebugLog("PANIC: all notes off.");
    }

    void Midi_MessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        // Sound is triggered on the MIDI thread immediately (lowest latency,
        // independent of UI load). Only the visual update is marshalled to UI.
        var ev = e.MidiEvent;
        if (ev is NoteOnEvent on)
        {
            int midi = on.NoteNumber;
            if (on.Velocity > 0) {
                float vel = on.Velocity / 127f;
                SoundOn(midi, vel);                                   // MIDI thread, instant
                Dispatcher.BeginInvoke(() => NoteVisualOn(midi, FindKeyRect(midi), vel));
            } else {
                SoundOff(midi);                                       // vel 0 == note-off
                Dispatcher.BeginInvoke(() => NoteVisualOff(midi, FindKeyRect(midi)));
            }
        }
        else if (ev is NoteEvent off && off.CommandCode == MidiCommandCode.NoteOff)
        {
            int midi = off.NoteNumber;
            SoundOff(midi);
            Dispatcher.BeginInvoke(() => NoteVisualOff(midi, FindKeyRect(midi)));
        }
        else if (ev is ControlChangeEvent cc &&
                 ((int)cc.Controller == 123 || (int)cc.Controller == 120))
        {
            Dispatcher.BeginInvoke(AllNotesOff);
        }
    }

    void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (_midi_in != null) {
            _midi_in.Stop();
            _midi_in.Dispose();
            _midi_in = null;
        }
        _output?.Stop();
        _output?.Dispose();
        _engine?.Dispose();
    }

    // ── Keyboard rendering ──────────────────────────────────────────────
    // Map an absolute MIDI note to the on-screen key rect for the current octave.
    // Returns null if the note is outside the visible keyboard (sound still plays).
    Rectangle? FindKeyRect(int midi)
    {
        int semi = midi - (_octave + 1) * 12;
        return _key_to_rect.TryGetValue(semi, out var rect) ? rect : null;
    }

    void BuildKeyboard()
    {
        Keyboard.Children.Clear();
        _key_to_rect.Clear();
        // 30 white keys (right end extended by two: C5, D5).
        const double WW = 30, WH = 78, BW = 18, BH = 48;
        int[] white_semitones = { -12,-10,-8,-7,-5,-3,-1, 0,2,4,5,7,9,11, 12,14,16,17,19,21,23, 24,26,28,29,31,33,35, 36,38 };
        // black key sits to the right of white index i; -99 = no black key after this white.
        int[] black_semitones = { -11,-9,-99,-6,-4,-2,-99, 1,3,-99,6,8,10,-99, 13,15,-99,18,20,22,-99, 25,27,-99,30,32,34,-99, 37,-99 };

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
            if (semi == -99) continue;
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
            case Key.Escape:            AllNotesOff();    return;
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

    // Sound path: thread-agnostic, lowest latency. Safe to call from the MIDI
    // thread. Enqueues into the provider; the audio callback forwards to engine.
    void SoundOn(int midi, float velocity) => _provider?.EnqueueNote(new NoteCommand(true, midi, velocity));
    void SoundOff(int midi)                 => _provider?.EnqueueNote(new NoteCommand(false, midi, 0f));

    // UI path: must run on the UI thread (touches _held_notes, key colours, log).
    void NoteVisualOn(int midi, Rectangle? rect, float velocity)
    {
        if (!_held_notes.Add(midi)) return;
        if (rect != null)
            rect.Fill = (Brush)FindResource(IsBlackKey(midi) ? "DcoColor" : "DcfColor");
        UpdateDisplay(midi);
        DebugLog($"NOTE_ON  midi={midi,3} ({MidiName(midi),-4}) vel={velocity:F2} " +
                 $"CUTOFF={SldCutoff.Value,3:F0} RESO={SldReso.Value,3:F0} wave={_current_wave}");
    }
    void NoteVisualOff(int midi, Rectangle? rect)
    {
        if (!_held_notes.Remove(midi)) return;
        if (rect != null)
            rect.Fill = IsBlackKey(midi)
                ? new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a))
                : Brushes.WhiteSmoke;
        DebugLog($"NOTE_OFF midi={midi,3} ({MidiName(midi),-4})");
    }

    // Convenience for UI-thread callers (PC keys, on-screen keys): sound + visual.
    void PressNote(int midi, Rectangle? rect, float velocity = 0.8f)
    {
        if (_engine == null || _held_notes.Contains(midi)) return;
        SoundOn(midi, velocity);
        NoteVisualOn(midi, rect, velocity);
    }
    void ReleaseNote(int midi, Rectangle? rect)
    {
        if (_engine == null || !_held_notes.Contains(midi)) return;
        SoundOff(midi);
        NoteVisualOff(midi, rect);
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
        if (ValOsc1Pw != null) ValOsc1Pw.Text = ((int)SldOsc1Pw.Value).ToString();
        if (ValOsc2Pw != null) ValOsc2Pw.Text = ((int)SldOsc2Pw.Value).ToString();
        ApplyOsc();
    }
    void ApplyOsc()
    {
        float l1 = (float)(SldOsc1Lvl?.Value ?? 100) / 100f;
        float l2 = (float)(SldOsc2Lvl?.Value ?? 50)  / 100f;
        float dt = (float)(SldDetune  ?.Value ?? 50) - 50f; // -50 to +50 cents
        _engine?.SetOscParams(l1, l2, dt);
        float pw1 = (float)(SldOsc1Pw?.Value ?? 50) / 100f; // 0.01..0.99
        float pw2 = (float)(SldOsc2Pw?.Value ?? 50) / 100f;
        _engine?.SetPulseWidth(pw1, pw2);
    }

    // ── Wave selection ──────────────────────────────────────────────────
    // ── Grouped button control ──────────────────────────────────────────
    // Tag format "group:id". Exclusive groups light exactly one button.
    // Waveform groups (osc1wave/osc2wave) allow up to TWO, FIFO: pressing a
    // third drops the oldest. DSP is not wired for stacking yet (visual + log).
    readonly Dictionary<string, List<Button>> _groupButtons = new();
    readonly Dictionary<string, List<Button>> _waveSelection = new(); // osc -> ordered lit buttons
    static readonly HashSet<string> _waveGroups = new() { "osc1wave", "osc2wave" };

    void RegisterGroup(Button b)
    {
        if (b.Tag is not string tag || !tag.Contains(':')) return;
        string group = tag.Split(':')[0];
        if (!_groupButtons.TryGetValue(group, out var list)) {
            list = new List<Button>(); _groupButtons[group] = list;
        }
        if (!list.Contains(b)) list.Add(b);
    }

    // Lit colour per group = that section's panel colour (JP-8 rainbow).
    // ARP / portamento light purple (Signo's own accent).
    Color GroupLitColor(string group)
    {
        string key = group switch {
            "osc1wave" or "osc2wave" or "osc1range" or "osc2range" => "DcoColor",
            "lfowave"                                              => "LfoColor",
            "filtmode"                                             => "DcfColor",
            "arpmode" or "arponoff" or "portaonoff"                => "FxColor", // purple
            _                                                      => "DcoColor",
        };
        return ((SolidColorBrush)FindResource(key)).Color;
    }

    void LitOn(Button b)
    {
        Color c = Colors.Gray;
        if (b.Tag is string tag && tag.Contains(':')) c = GroupLitColor(tag.Split(':')[0]);
        b.Background = new SolidColorBrush(c);
        // Dark text on the bright panel colour for contrast.
        b.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
        b.Tag = b.Tag; // keep
        b.SetValue(LitMarkerProperty, true);
    }
    void LitOff(Button b)
    {
        b.Background = new SolidColorBrush(Color.FromRgb(0x1e,0x1e,0x1e));
        b.Foreground = new SolidColorBrush(Color.FromRgb(0x88,0x88,0x88));
        b.SetValue(LitMarkerProperty, false);
    }
    static readonly DependencyProperty LitMarkerProperty =
        DependencyProperty.RegisterAttached("LitMarker", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
    static bool IsLit(Button b) => (bool)b.GetValue(LitMarkerProperty);

    void GroupBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag && tag.Contains(':'))
        {
            string group = tag.Split(':')[0];
            if (_waveGroups.Contains(group)) {
                // Up to 2, FIFO.
                if (!_waveSelection.TryGetValue(group, out var sel)) {
                    sel = new List<Button>(); _waveSelection[group] = sel;
                }
                if (sel.Contains(b)) {            // toggle off
                    sel.Remove(b); LitOff(b);
                } else {
                    if (sel.Count >= 2) { var oldest = sel[0]; sel.RemoveAt(0); LitOff(oldest); }
                    sel.Add(b); LitOn(b);
                }
            } else {
                // Exclusive: light only this one in the group.
                if (_groupButtons.TryGetValue(group, out var list))
                    foreach (var btn in list) LitOff(btn);
                LitOn(b);
                ApplyExclusiveGroupToDsp(group, tag);
            }
            // Waveform groups: rebuild the per-oscillator [Flags] stack and send.
            if (group == "osc1wave" || group == "osc2wave")
                ApplyWaveformsToDsp();
            DebugLog($"BTN {tag}");
        }
        Focus();
    }

    // Combine the lit waveform buttons of each oscillator into a [Flags] value
    // and send to the engine (W = white noise, P = pink).
    void ApplyWaveformsToDsp()
    {
        WaveType w1 = WaveFromSelection("osc1wave");
        WaveType w2 = WaveFromSelection("osc2wave");
        _engine?.SetOscWaves(w1, w2);
        // PW is only meaningful for the square wave: grey out (disable) the PW
        // slider, value and label when the oscillator's waveform isn't square.
        bool pw1 = (w1 & WaveType.Square) != 0;
        bool pw2 = (w2 & WaveType.Square) != 0;
        if (SldOsc1Pw   != null) SldOsc1Pw.IsEnabled   = pw1;
        if (ValOsc1Pw   != null) ValOsc1Pw.IsEnabled   = pw1;
        if (Osc1PwPanel != null) Osc1PwPanel.IsEnabled = pw1;
        if (SldOsc2Pw   != null) SldOsc2Pw.IsEnabled   = pw2;
        if (ValOsc2Pw   != null) ValOsc2Pw.IsEnabled   = pw2;
        if (Osc2PwPanel != null) Osc2PwPanel.IsEnabled = pw2;
    }

    WaveType WaveFromSelection(string group)
    {
        WaveType w = WaveType.None;
        if (_waveSelection.TryGetValue(group, out var sel)) {
            foreach (var b in sel) {
                if (b.Tag is string t && t.Contains(':')) {
                    w |= t.Split(':')[1] switch {
                        "saw" => WaveType.Saw,
                        "sqr" => WaveType.Square,
                        "tri" => WaveType.Triangle,
                        "sin" => WaveType.Sine,
                        "w"   => WaveType.Noise,
                        "p"   => WaveType.Pink,
                        _     => WaveType.None,
                    };
                }
            }
        }
        return w == WaveType.None ? WaveType.Saw : w;
    }

    // Exclusive groups that drive DSP directly (ranges; arp/porta handled elsewhere).
    void ApplyExclusiveGroupToDsp(string group, string tag)
    {
        string id = tag.Contains(':') ? tag.Split(':')[1] : tag;
        if (group == "osc1range" || group == "osc2range") {
            int oct = id switch { "16" => -1, "8" => 0, "4" => 1, _ => 0 };
            if (group == "osc1range") _osc1_range_oct = oct; else _osc2_range_oct = oct;
            _engine?.SetOscRange(_osc1_range_oct, _osc2_range_oct);
        } else if (group == "lfowave") {
            LfoWave w = id switch {
                "sine" => LfoWave.Sine, "tri" => LfoWave.Triangle,
                "square" => LfoWave.Square, "sh" => LfoWave.SH, _ => LfoWave.Sine };
            _lfo_wave = w;
            _engine?.SetLfoWave(w);
            UpdateLfoRateDisplay();
        } else if (group == "arpmode") {
            _arp_mode = id switch {
                "up" => ArpMode.Up, "down" => ArpMode.Down,
                "updn" => ArpMode.UpDown, "rnd" => ArpMode.Random, _ => ArpMode.Up };
            _engine?.SetArpMode(_arp_mode);
        } else if (group == "arponoff") {
            _arp_on = id == "on";
            _engine?.SetArpEnabled(_arp_on);
        } else if (group == "portaonoff") {
            _porta_on = id == "on";
            ApplyPortamento();
        }
    }

    // Portamento glide time is applied only when ON; OFF forces instant (0s).
    void BtnScope_Click(object sender, RoutedEventArgs e)
    {
        if (_scopeWin == null || !_scopeWin.IsVisible) {
            _scopeWin = new OscilloscopeWindow(_scope);
            _scopeWin.Show();
        } else {
            _scopeWin.Activate();
        }
    }

    void ApplyPortamento()
    {
        float seconds = _porta_on ? (float)(SldPortamento.Value / 100.0 * 0.5) : 0f;
        _engine?.SetPortamentoTime(seconds);
        DebugLog($"PORTAMENTO on={_porta_on} time={seconds:F3}s");
    }

    // LFO RATE/DLY sliders: update readouts (DSP routing not wired yet).
    void LfoSld_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        UpdateLfoRateDisplay();
        if (ValLfoDelay != null) ValLfoDelay.Text = ((int)SldLfoDelay.Value).ToString();
        ApplyLfoRouting();
    }

    // LFO RATE readout: for S&H show BPM (label "BPM", value 40..240) since its
    // stepped nature is clock-like; for continuous shapes show the raw rate.
    void UpdateLfoRateDisplay()
    {
        if (ValLfoRate == null) return;
        if (_lfo_wave == LfoWave.SH) {
            if (LblLfoRate != null) LblLfoRate.Text = "BPM";
            int bpm = (int)(40f + (float)(SldLfoRate.Value / 100.0) * 200f);
            ValLfoRate.Text = bpm.ToString();
        } else {
            if (LblLfoRate != null) LblLfoRate.Text = "RATE";
            ValLfoRate.Text = ((int)SldLfoRate.Value).ToString();
        }
    }

    // ARP RATE slider: update readout.
    void ArpRate_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        // 0..100 -> 40..240 BPM
        float bpm = 40f + (float)(SldArpRate.Value / 100.0) * 200f;
        if (ValArpRate != null) ValArpRate.Text = ((int)bpm).ToString();
        _engine?.SetArpRate(bpm);
    }

    // LFO-amount sliders (DCF/DCA): readout + route to engine.
    void ModLfo_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        if (ValDcfLfo != null) ValDcfLfo.Text = ((int)SldDcfLfo.Value).ToString();
        if (ValDcaLfo != null) ValDcaLfo.Text = ((int)SldDcaLfo.Value).ToString();
        ApplyLfoRouting();
    }

    // HPF frequency 0..100 -> Engine.SetHpf.
    void Hpf_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        if (ValHpf != null) ValHpf.Text = ((int)SldHpf.Value).ToString();
        _engine?.SetHpf((float)SldHpf.Value);
        DebugLog($"HPF {(int)SldHpf.Value}");
    }

    // ── FX rotary knobs (BOSS-style, drag to turn) ─────────────────────
    readonly Dictionary<string, double> _fx_knob = new() {
        ["ch:send"]=0, ["ch:rate"]=50, ["ch:depth"]=50,
        ["dl:send"]=0, ["dl:time"]=50, ["dl:fb"]=45,
        ["rv:send"]=0, ["rv:size"]=50, ["rv:damp"]=50,
    };
    bool _chorus_on, _delay_on, _reverb_on;
    string? _drag_knob;
    double _drag_start_y, _drag_start_val;

    void Knob_Down(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag) {
            _drag_knob = tag;
            _drag_start_y = e.GetPosition(this).Y;
            _drag_start_val = _fx_knob.TryGetValue(tag, out var v) ? v : 0;
            fe.CaptureMouse();
        }
    }

    void Knob_Move(object sender, MouseEventArgs e)
    {
        if (_drag_knob == null || e.LeftButton != MouseButtonState.Pressed) return;
        double dy = _drag_start_y - e.GetPosition(this).Y; // up = increase
        double val = _drag_start_val + dy; // 1px = 1 unit
        if (val < 0) val = 0; else if (val > 100) val = 100;
        _fx_knob[_drag_knob] = val;
        UpdateKnobVisual(_drag_knob);
        ApplyFx();
    }

    void Knob_Up(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe) fe.ReleaseMouseCapture();
        _drag_knob = null;
    }

    // Rotate the indicator: 0..100 -> -135°..+135°.
    void UpdateKnobVisual(string tag)
    {
        double angle = -135 + (_fx_knob[tag] / 100.0) * 270.0;
        RotateTransform? rot = tag switch {
            "ch:send" => RotChSend, "ch:rate" => RotChRate, "ch:depth" => RotChDepth,
            "dl:send" => RotDlSend, "dl:time" => RotDlTime, "dl:fb" => RotDlFb,
            "rv:send" => RotRvSend, "rv:size" => RotRvSize, "rv:damp" => RotRvDamp,
            _ => null,
        };
        if (rot != null) rot.Angle = angle;
    }

    void UpdateAllKnobVisuals()
    {
        foreach (var k in _fx_knob.Keys) UpdateKnobVisual(k);
    }

    // FX foot switch: toggle the pedal on/off (lit ring = on).
    void Foot_Down(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse el && el.Tag is string tag) {
            bool on;
            if (tag == "chorus") { _chorus_on = !_chorus_on; on = _chorus_on; }
            else if (tag == "delay") { _delay_on = !_delay_on; on = _delay_on; }
            else { _reverb_on = !_reverb_on; on = _reverb_on; }
            el.Fill = new SolidColorBrush(on ? Color.FromRgb(0xc8,0xff,0x88) : Color.FromRgb(0x1a,0x1a,0x2a));
            ApplyFx();
            DebugLog($"FX {tag} {(on ? "on" : "off")}");
        }
    }

    // Apply all FX sends/params to the engine (gated by each pedal's on state).
    // Ranges are musical and centred: knob at 50 gives a usable default.
    void ApplyFx()
    {
        if (!_loaded || _engine == null) return;
        _engine.SetChorusSend(_chorus_on ? (float)(_fx_knob["ch:send"] / 100.0) : 0f);
        _engine.SetDelaySend (_delay_on  ? (float)(_fx_knob["dl:send"] / 100.0) : 0f);
        _engine.SetReverbSend(_reverb_on ? (float)(_fx_knob["rv:send"] / 100.0) : 0f);

        // CHORUS RATE: 0.1..2.0 Hz, log (centre ~0.45 Hz). DEPTH: 0..0.7 linear (centre 0.35).
        _engine.SetChorusParams(LogMap(_fx_knob["ch:rate"], 0.1f, 2.0f),
                                (float)(_fx_knob["ch:depth"] / 100.0) * 0.7f);
        // DELAY TIME: 60..600 ms, log (centre ~190 ms). F.B: 0..0.85 linear (centre ~0.43).
        _engine.SetDelayParams(LogMap(_fx_knob["dl:time"], 0.06f, 0.6f),
                               (float)(_fx_knob["dl:fb"] / 100.0) * 0.85f);
        // REVERB SIZE: 0.3..0.95 linear (centre ~0.63). DAMP: 0..0.8 linear (centre 0.4).
        _engine.SetReverbParams(0.3f + (float)(_fx_knob["rv:size"] / 100.0) * 0.65f,
                                (float)(_fx_knob["rv:damp"] / 100.0) * 0.8f);
    }

    // Logarithmic map of a 0..100 knob to [min,max] so the knob centre lands on
    // the geometric mean (musically even across the range).
    static float LogMap(double knob0to100, float min, float max)
    {
        double t = knob0to100 / 100.0;
        return (float)(min * System.Math.Pow(max / min, t));
    }

    // Map LFO RATE slider (0..100) to ~0.1..20 Hz and route DCF/DCA depths.
    void ApplyLfoRouting()
    {
        float rateHz = 0.1f + (float)(SldLfoRate.Value / 100.0) * 19.9f;
        float dcfDepth = (float)(SldDcfLfo.Value / 100.0);
        float dcaDepth = (float)(SldDcaLfo.Value / 100.0);
        _engine?.SetLfoToCutoff(dcfDepth, rateHz);
        _engine?.SetLfoToAmp(dcaDepth, rateHz);
    }

    void MockBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag)
        {
            if (IsLit(b)) LitOff(b); else LitOn(b);
            DebugLog($"MOCK {tag} (DSP not implemented yet)");
        }
        Focus();
    }

    // Portamento glide time. DSP (Portamento.cs) is implemented; this wires the UI.
    void Portamento_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        if (ValPortamento != null) ValPortamento.Text = ((int)SldPortamento.Value).ToString();
        ApplyPortamento(); // honours ON/OFF
    }

    // F1-F5 keys: set OSC1 waveform via the same grouped (2-max FIFO) path.
    void SetWave(WaveType w)
    {
        Button? target = w switch {
            WaveType.Saw      => WaveSaw,
            WaveType.Square   => WaveSqr,
            WaveType.Triangle => WaveTri,
            WaveType.Sine     => WaveSin,
            WaveType.Noise    => WaveNoi,
            _                 => WaveSaw,
        };
        if (target != null) GroupBtn_Click(target, new RoutedEventArgs());
    }

    // Walk the visual tree and register every grouped button (Tag "group:id").
    void RegisterAllGroupButtons(DependencyObject root)
    {
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++) {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Button b && b.Tag is string tag && tag.Contains(':'))
                RegisterGroup(b);
            RegisterAllGroupButtons(child);
        }
    }

    // Light an initial selection in a group without toggling logic side effects.
    void GroupSelectInitial(string group, Button b)
    {
        if (_waveGroups.Contains(group)) {
            if (!_waveSelection.TryGetValue(group, out var sel)) {
                sel = new List<Button>(); _waveSelection[group] = sel;
            }
            if (!sel.Contains(b)) { sel.Add(b); LitOn(b); }
            ApplyWaveformsToDsp();
        } else {
            if (_groupButtons.TryGetValue(group, out var list))
                foreach (var btn in list) LitOff(btn);
            LitOn(b);
            if (b.Tag is string t) ApplyExclusiveGroupToDsp(group, t);
        }
    }

    // Find a registered grouped button by its full "group:id" tag and select it.
    void SelectInitialByTag(string fullTag)
    {
        string group = fullTag.Split(':')[0];
        if (_groupButtons.TryGetValue(group, out var list)) {
            foreach (var btn in list)
                if (btn.Tag is string t && t == fullTag) { GroupSelectInitial(group, btn); return; }
        }
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
        Brush sel  = (Brush)FindResource("DcfColor");
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
        ValCutoff.Text = SldCutoff.Value.ToString("0.0");
        ValReso.Text   = SldReso.Value.ToString("0.0");
        ValEnvAmt.Text = SldEnvAmt.Value.ToString("0.0");
        ApplyFilter();
    }
    void ApplyFilter()
    {
        float c  = (float)(SldCutoff?.Value ?? 100) / 100f;
        float r  = (float)(SldReso  ?.Value ?? 0)   / 100f;
        float ea = (float)(SldEnvAmt?.Value ?? 0)   / 100f;
        _engine?.SetFilterParams(c, r, _current_filter);
        _engine?.SetFilterEnvAmount(ea);
        // Diagnostics come straight from Filter so the log always matches the running DSP.
        var diag = Filter.Diagnose(c, r, _current_filter, (int)SR);
        DebugLog($"DCF mode={_current_filter,-6} " +
                 $"CUTOFF={SldCutoff?.Value ?? 0,3:F0} RESO={SldReso?.Value ?? 0,3:F0} " +
                 $"ENVAMT={SldEnvAmt?.Value ?? 0,3:F0} " +
                 $"| cutoff_hz={diag.cutoffHz,6:F0} p={diag.p:F4} r_norm={diag.rNorm:F3} " +
                 $"{(diag.oscillates ? "[OSC]" : "")} (osc_threshold=RESO0.75)");
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
        ApplyFilterEnv();
    }
    void ApplyFilterEnv()
    {
        // Filter-envelope ADSR sliders are not in the current UI yet; use defaults.
        float a = MapEnvTime(5);
        float d = MapEnvTime(30);
        float s = 0f;
        float r = MapEnvTime(20);
        _engine?.SetFilterEnv(a, d, s, r);
    }

    // ── VCA Level ───────────────────────────────────────────────────────
    void Level_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        ValLevel.Text = ((int)SldLevel.Value).ToString();
        _engine?.SetDcaLevel((float)(SldLevel.Value / 100.0));
        DebugLog($"DCA level={(int)SldLevel.Value}");
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

/// <summary>NAudio bridge — converts Signo's Span output to NAudio sample provider.</summary>
// A note command queued from any thread (MIDI thread, UI thread). The audio
// callback is the SINGLE consumer that forwards them to the engine, so the
// engine's SPSC event queue keeps exactly one producer (the audio thread) and
// notes are applied right before they are rendered = lowest, jitter-free latency.
internal readonly struct NoteCommand
{
    public readonly bool On;
    public readonly int Midi;
    public readonly float Velocity;
    public NoteCommand(bool on, int midi, float velocity)
    {
        On = on; Midi = midi; Velocity = velocity;
    }
}

internal sealed class SignoProvider : ISampleProvider
{
    readonly Engine _engine;
    readonly System.Collections.Concurrent.ConcurrentQueue<NoteCommand> _notes = new();
    public WaveFormat WaveFormat { get; }
    public ScopeBuffer? Scope { get; set; }

    public SignoProvider(Engine engine)
    {
        _engine = engine;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    }
    public void EnqueueNote(in NoteCommand cmd) => _notes.Enqueue(cmd);

    public int Read(float[] buffer, int offset, int count)
    {
        while (_notes.TryDequeue(out var cmd)) {
            if (cmd.On) _engine.SendNoteOn(cmd.Midi, cmd.Velocity, 2, 5, 0);
            else        _engine.SendNoteOff(cmd.Midi, 2, 0);
        }
        _engine.ProcessAudioCallback(buffer.AsSpan(offset, count));
        Scope?.Push(buffer, offset, count, 2);
        return count;
    }
}
