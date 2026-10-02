namespace ManicMiner.Core;

/// <summary>
/// Software model of the three-channel CPC firmware sound manager (SOUND queue, amplitude and
/// tone envelopes) driving an AY-3-8912 style square/noise mixer.
/// </summary>
public sealed class CpcSound
{
    sealed class Note
    {
        public int Period, Noise, Vol, AmpEnv, ToneEnv;
        public double Duration;     // seconds
    }

    sealed class EnvState
    {
        public byte[]? Data; public int Section, Count, Pause; public double Timer; public bool Done = true;
    }

    sealed class Channel
    {
        public readonly Queue<Note> Queue = new();
        public Note? Cur; public double Left;
        public double Phase; public int Period, Vol;
        public EnvState Amp = new(), Tone = new();
        public double NoiseLfsrPhase;
    }

    readonly Channel[] _ch = { new(), new(), new() };
    readonly byte[][] _ampEnv = new byte[16][];
    readonly byte[][] _toneEnv = new byte[16][];
    uint _lfsr = 1;
    public bool Muted;

    public void Reset()
    {
        foreach (var c in _ch) { c.Queue.Clear(); c.Cur = null; c.Left = 0; c.Vol = 0; }
    }

    public void DefineAmpEnvelope(int n, byte[] m, int addr) => _ampEnv[n & 15] = ReadEnv(m, addr);
    public void DefineToneEnvelope(int n, byte[] m, int addr) => _toneEnv[n & 15] = ReadEnv(m, addr);

    static byte[] ReadEnv(byte[] m, int a)
    {
        int sections = m[a];
        var e = new byte[1 + sections * 3];
        Array.Copy(m, a, e, 0, e.Length);
        return e;
    }

    /// <summary>SOUND QUEUE (0xBCAA): reads a 9-byte parameter block from memory.</summary>
    public void Queue(byte[] m, int a)
    {
        int status = m[a];
        var n = new Note
        {
            AmpEnv = m[a + 1], ToneEnv = m[a + 2],
            Period = m[a + 3] | (m[a + 4] << 8), Noise = m[a + 5], Vol = m[a + 6]
        };
        short dur = (short)(m[a + 7] | (m[a + 8] << 8));
        for (int c = 0; c < 3; c++)
        {
            if ((status & (1 << c)) == 0) continue;
            var ch = _ch[c];
            if ((status & 0x80) != 0) { ch.Queue.Clear(); ch.Cur = null; ch.Left = 0; }
            if (ch.Queue.Count >= 4) continue;
            ch.Queue.Enqueue(new Note
            {
                AmpEnv = n.AmpEnv, ToneEnv = n.ToneEnv, Period = n.Period, Noise = n.Noise, Vol = n.Vol,
                Duration = dur > 0 ? dur / 100.0 : dur == 0 ? EnvLength(n.AmpEnv) : -dur * EnvLength(n.AmpEnv)
            });
        }
    }

    double EnvLength(int env)
    {
        var e = _ampEnv[env & 15];
        if (env == 0 || e == null) return 0.01;
        double t = 0;
        for (int i = 0; i < e[0]; i++) t += e[1 + i * 3] * e[3 + i * 3] / 100.0;
        return Math.Max(t, 0.01);
    }

    /// <summary>SOUND CHECK (0xBCAD): bits 0-2 free queue slots, bit 7 note playing.</summary>
    public int Check(int mask)
    {
        int c = mask == 1 ? 0 : mask == 2 ? 1 : 2;
        var ch = _ch[c];
        return (4 - ch.Queue.Count) | (ch.Cur != null ? 0x80 : 0);
    }

    void StartEnv(EnvState s, byte[]? d)
    {
        s.Data = d; s.Section = 0; s.Done = d == null || d[0] == 0;
        if (!s.Done) { s.Count = d![1]; s.Pause = d[3]; s.Timer = 0; }
    }

    /// <summary>Advance logical time (queue, envelopes).</summary>
    public void Step(double dt)
    {
        StepLogic(dt);
        if (CaptureRate > 0)
        {
            _owed += dt * CaptureRate;
            int n = (int)_owed; _owed -= n;
            if (n > 0)
            {
                var tmp = new short[n];
                MixSamples(tmp, n, CaptureRate);
                lock (_pcm) { foreach (var v in tmp) _pcm.Enqueue(v); while (_pcm.Count > 16384) _pcm.Dequeue(); }
            }
        }
    }

    /// <summary>When above zero, Step also generates audio at this rate so short notes are never missed.</summary>
    public int CaptureRate;
    double _owed;
    readonly Queue<short> _pcm = new();

    void StepLogic(double dt)
    {
        foreach (var ch in _ch)
        {
            if (ch.Cur != null)
            {
                ch.Left -= dt;
                RunEnv(ch, ch.Amp, dt, false);
                RunEnv(ch, ch.Tone, dt, true);
                if (ch.Left <= 0) { ch.Cur = null; ch.Vol = 0; }
            }
            if (ch.Cur == null && ch.Queue.Count > 0)
            {
                var n = ch.Queue.Dequeue();
                ch.Cur = n; ch.Left = n.Duration; ch.Period = n.Period; ch.Vol = n.Vol & 15;
                StartEnv(ch.Amp, n.AmpEnv != 0 ? _ampEnv[n.AmpEnv & 15] : null);
                StartEnv(ch.Tone, n.ToneEnv != 0 ? _toneEnv[n.ToneEnv & 15] : null);
            }
        }
    }

    static void RunEnv(Channel ch, EnvState s, double dt, bool tone)
    {
        if (s.Done || s.Data == null) return;
        s.Timer += dt;
        while (!s.Done && s.Timer >= s.Pause / 100.0)
        {
            s.Timer -= s.Pause / 100.0;
            int step = (sbyte)s.Data[2 + s.Section * 3];
            if (tone) ch.Period = Math.Clamp(ch.Period + step, 1, 4095);
            else ch.Vol = Math.Clamp(ch.Vol + step, 0, 15);
            if (--s.Count <= 0)
            {
                s.Section++;
                if (s.Section >= s.Data[0]) { s.Done = true; break; }
                s.Count = s.Data[1 + s.Section * 3]; s.Pause = s.Data[3 + s.Section * 3];
            }
        }
    }

    static readonly double[] VolTable =
    { 0, 0.004, 0.006, 0.009, 0.013, 0.019, 0.027, 0.038, 0.054, 0.077, 0.108, 0.153, 0.216, 0.306, 0.432, 0.6 };

    /// <summary>Mix mono 16-bit samples.</summary>
    public void Render(short[] buf, int count, int rate)
    {
        if (CaptureRate > 0 && rate == CaptureRate)
        {
            lock (_pcm)
                for (int i = 0; i < count; i++) buf[i] = _pcm.Count > 0 ? _pcm.Dequeue() : (short)0;
            return;
        }
        MixSamples(buf, count, rate);
    }

    void MixSamples(short[] buf, int count, int rate)
    {
        for (int i = 0; i < count; i++)
        {
            double sum = 0;
            _lfsr = (_lfsr >> 1) ^ ((_lfsr & 1) != 0 ? 0x12000u : 0);
            foreach (var ch in _ch)
            {
                if (ch.Cur == null || ch.Vol == 0) continue;
                double v = VolTable[ch.Vol];
                if (ch.Period > 0)
                {
                    double f = 125000.0 / ch.Period;
                    ch.Phase += f / rate;
                    ch.Phase -= Math.Floor(ch.Phase);
                    sum += (ch.Phase < 0.5 ? 1 : -1) * v;
                }
                if (ch.Cur.Noise != 0) sum += (((_lfsr & 1) != 0) ? 1 : -1) * v * 0.5;
            }
            buf[i] = Muted ? (short)0 : (short)Math.Clamp(sum * 9000, -32000, 32000);
        }
    }
}
