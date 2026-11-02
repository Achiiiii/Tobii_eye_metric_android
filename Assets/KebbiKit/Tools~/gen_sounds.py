"""KebbiKit sound library: every cue synthesised from a short recipe, written as 48 kHz 16-bit mono WAV into
../Resources/KebbiKit/Sounds, plus sounds.json (name, group, length, use). Character: a small friendly robot -
round sine / triangle blips, short upward chirps, a little glassy FM bell for the technical side - all in D major
pentatonic so any two cues sound well together; energy kept in 500 Hz - 3 kHz for older ears.
Run: python -I gen_sounds.py"""
import json
import os
import wave

import numpy as np

SR = 48000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Resources', 'KebbiKit', 'Sounds')

# D major pentatonic
N = {'A4': 440.0, 'B4': 493.88, 'D5': 587.33, 'E5': 659.25, 'F#5': 739.99, 'A5': 880.0, 'B5': 987.77,
     'D6': 1174.66, 'E6': 1318.51, 'F#6': 1479.98, 'A6': 1760.0}


def t_of(seconds):
    return np.arange(int(SR * seconds)) / SR


def env(t, attack, decay, hold=0.0):
    """attack ramp, optional hold, exponential decay (decay = time to -40 dB)"""
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.where(t < attack + hold, 1.0, np.exp(-(t - attack - hold) * 4.6 / max(decay, 1e-4)))
    return a * d


def glide(f0, f1, length, t, curve=2.0):
    """frequency from f0 to f1 over `length`, eased"""
    k = np.clip(t / length, 0, 1) ** (1 / curve)
    return f0 * (f1 / f0) ** k


def osc(freq, t, shape='sine'):
    phase = 2 * np.pi * np.cumsum(freq) / SR if np.ndim(freq) else 2 * np.pi * freq * t
    if shape == 'sine':
        return np.sin(phase)
    if shape == 'tri':
        return 2 / np.pi * np.arcsin(np.sin(phase))
    raise ValueError(shape)


def blip(f, length, gain=0.5, shape='sine', f_end=None, attack=0.003, octave=0.15):
    """a round robot blip: sine/triangle, optional glide, a touch of octave for presence"""
    t = t_of(length * 2.2)
    freq = glide(f, f_end, length * 0.8, t) if f_end else np.full_like(t, f)
    s = osc(freq, t, shape) + octave * osc(freq * 2, t)
    return s * env(t, attack, length) * gain


def bell(f, length, gain=0.4, ratio=3.5, index=1.2):
    """a small glassy FM bell"""
    t = t_of(length * 2.5)
    mod = index * np.exp(-t * 6 / length) * np.sin(2 * np.pi * f * ratio * t)
    s = np.sin(2 * np.pi * f * t + mod) + 0.2 * np.sin(2 * np.pi * f * 2 * t)
    return s * env(t, 0.002, length) * gain


def chirp(f0, f1, length, gain=0.5, vibrato=0.0):
    """a cute rising / falling robot chirp with an optional warble"""
    t = t_of(length * 1.6)
    freq = glide(f0, f1, length, t, 1.6)
    if vibrato:
        freq = freq * (1 + vibrato * np.sin(2 * np.pi * 18 * t))
    s = osc(freq, t, 'tri') * 0.7 + osc(freq, t) * 0.5
    return s * env(t, 0.005, length, length * 0.3) * gain


def air(length, gain=0.12, f0=800, f1=3000):
    """a soft filtered-noise swish (band moving from f0 to f1)"""
    t = t_of(length)
    rng = np.random.default_rng(3)
    noise = rng.standard_normal(len(t))
    spec = np.fft.rfft(noise)
    freqs = np.fft.rfftfreq(len(t), 1 / SR)
    centre = np.sqrt(f0 * f1)
    spec *= np.exp(-((np.log(freqs + 1) - np.log(centre)) ** 2) / 0.5)
    s = np.fft.irfft(spec, len(t))
    s /= np.max(np.abs(s)) + 1e-9
    shape = np.sin(np.pi * np.clip(t / length, 0, 1)) ** 2
    return s * shape * gain


def seq(*parts):
    """(start seconds, sound) pairs mixed together"""
    end = max(int(start * SR) + len(s) for start, s in parts)
    out = np.zeros(end)
    for start, s in parts:
        i = int(start * SR)
        out[i:i + len(s)] += s
    return out


def finish(s, peak=0.7):
    s = s.copy()
    fade = min(len(s), int(0.012 * SR))
    s[-fade:] *= np.linspace(1, 0, fade)
    s[:64] *= np.linspace(0, 1, 64)
    return s / (np.max(np.abs(s)) + 1e-9) * peak


SOUNDS = [
    # name, group, use, builder, peak
    ('ui_tap', 'ui', 'any key pressed', lambda: blip(N['D6'], 0.035, shape='tri', f_end=N['A5'], octave=0.05), 0.45),
    ('ui_confirm', 'ui', 'an answer or option chosen', lambda: seq((0, blip(N['A5'], 0.06)), (0.06, bell(N['D6'], 0.16, 0.45))), 0.6),
    ('ui_back', 'ui', 'back, previous, cancel', lambda: seq((0, blip(N['D6'], 0.05, 0.4)), (0.055, blip(N['A5'], 0.09, 0.4))), 0.5),
    ('ui_toggle_on', 'ui', 'a switch or tick turned on', lambda: blip(N['A5'], 0.06, f_end=N['D6'], shape='tri'), 0.5),
    ('ui_toggle_off', 'ui', 'a switch or tick turned off', lambda: blip(N['D6'], 0.06, f_end=N['A5'], shape='tri'), 0.45),
    ('ui_dwell_done', 'ui', 'a gaze dwell completed', lambda: seq((0, bell(N['D6'], 0.12, 0.4)), (0.07, bell(N['F#6'], 0.22, 0.35))), 0.6),
    ('flow_page', 'flow', 'a page changes', lambda: seq((0, air(0.22)), (0.12, blip(N['A5'], 0.05, 0.25))), 0.45),
    ('flow_step', 'flow', 'one step further', lambda: bell(N['F#5'], 0.18, 0.45), 0.55),
    ('flow_tick', 'flow', 'countdown 3, 2', lambda: blip(N['A5'], 0.07, 0.5, octave=0.3), 0.6),
    ('flow_go', 'flow', 'countdown 1 / start', lambda: seq((0, blip(N['D6'], 0.12, 0.5, octave=0.3)), (0, bell(N['D6'], 0.25, 0.3))), 0.65),
    ('flow_done', 'flow', 'a stage completed', lambda: seq((0, bell(N['D5'], 0.12)), (0.09, bell(N['F#5'], 0.12)), (0.18, bell(N['A5'], 0.3))), 0.6),
    ('state_attention', 'state', 'please look / pause (gentle)', lambda: seq((0, blip(N['A5'], 0.12, 0.45)), (0.16, blip(N['F#5'], 0.2, 0.45))), 0.55),
    ('state_inrange', 'state', 'position right / back in place', lambda: seq((0, chirp(N['F#5'], N['A5'], 0.09, 0.4)), (0.09, bell(N['A5'], 0.2, 0.35))), 0.55),
    ('state_softfail', 'state', 'did not work, try again (warm, not an alarm)', lambda: seq((0, blip(N['B4'], 0.16, 0.5, octave=0.25)), (0.17, blip(N['A4'], 0.3, 0.5, octave=0.25))), 0.55),
    ('test_dot_show', 'test', 'a calibration dot appears', lambda: bell(N['A5'], 0.14, 0.45, index=0.8), 0.5),
    ('test_dot_done', 'test', 'a calibration dot taken', lambda: bell(N['D6'], 0.1, 0.45, index=0.6), 0.5),
    ('kebbi_hi', 'kebbi', 'Kebbi says hello', lambda: seq((0, chirp(520, 980, 0.12, 0.5, 0.015)), (0.15, chirp(880, 1250, 0.09, 0.45))), 0.65),
    ('kebbi_happy', 'kebbi', 'a happy moment', lambda: seq((0, chirp(N['D5'], N['F#5'], 0.06, 0.45)), (0.08, chirp(N['F#5'], N['A5'], 0.06, 0.45)), (0.16, chirp(N['A5'], N['D6'], 0.12, 0.5))), 0.65),
    ('kebbi_thinking', 'kebbi', 'thinking / waiting', lambda: seq((0, blip(N['B5'], 0.05, 0.35)), (0.11, blip(N['A5'], 0.05, 0.32)), (0.22, blip(N['F#5'], 0.07, 0.3))), 0.5),
    ('kebbi_curious', 'kebbi', 'a question', lambda: chirp(N['D5'], N['A5'], 0.22, 0.5, 0.02), 0.55),
    ('kebbi_encourage', 'kebbi', 'warm encouragement', lambda: seq((0, bell(N['D5'], 0.5, 0.3)), (0.08, bell(N['F#5'], 0.5, 0.3)), (0.16, bell(N['A5'], 0.5, 0.3)), (0.24, bell(N['D6'], 0.6, 0.3))), 0.6),
    ('kebbi_cheer', 'kebbi', 'well done (the same for every result)', lambda: seq((0, blip(N['D5'], 0.07)), (0.1, blip(N['E5'], 0.07)), (0.2, blip(N['F#5'], 0.07)), (0.3, blip(N['A5'], 0.1)), (0.45, bell(N['D6'], 0.55, 0.5)), (0.45, blip(N['A5'], 0.3, 0.25))), 0.65),
    ('kebbi_bye', 'kebbi', 'goodbye', lambda: seq((0, chirp(N['D6'], N['A5'], 0.1, 0.45)), (0.16, chirp(N['A5'], N['D5'], 0.18, 0.45))), 0.6),
    ('brand_intro', 'brand', 'the app opens', lambda: seq((0, air(0.6, 0.1, 400, 2400)), (0.15, bell(N['D5'], 0.6, 0.3)), (0.3, bell(N['A5'], 0.6, 0.3)), (0.45, bell(N['D6'], 0.7, 0.3)), (0.6, bell(N['F#6'], 0.9, 0.25))), 0.6),
]


def write(name, samples):
    path = os.path.join(OUT, name + '.wav')
    data = (np.clip(samples, -1, 1) * 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def main():
    os.makedirs(OUT, exist_ok=True)
    manifest = []
    for name, group, use, build, peak in SOUNDS:
        s = finish(build(), peak)
        write(name, s)
        manifest.append({'name': name, 'group': group, 'seconds': round(len(s) / SR, 3), 'use': use})
    with open(os.path.join(OUT, '..', 'sounds.json'), 'w', encoding='utf-8') as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print('wrote', len(manifest), 'sounds to', os.path.normpath(OUT))


if __name__ == '__main__':
    main()
