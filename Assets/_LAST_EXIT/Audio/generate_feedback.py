"""Deterministic LAST EXIT prototype feedback SFX. Requires numpy + soundfile.

Run with D:/LAST_EXIT_AudioLab/.venv/Scripts/python.exe generate_feedback.py
No recordings, model outputs, voices or hidden-passenger-type inputs are used.
Periodic loops are synthesized on an integer-frequency grid; do not add end fades.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
import soundfile as sf

SR = 44100
ROOT = Path(__file__).resolve().parent
RNG = np.random.default_rng(20261107)


def time(seconds):
    return np.arange(round(SR * seconds), dtype=np.float64) / SR


def tone(t, frequency, phase=0):
    return np.sin(2 * np.pi * frequency * t + phase)


def noise(t, low=120, high=4200):
    """FFT-filtered noise, also exactly periodic at the buffer length."""
    n = len(t)
    f = np.fft.rfftfreq(n, 1 / SR)
    x = RNG.normal(size=n)
    spectrum = np.fft.rfft(x)
    weight = np.exp(-(f / high) ** 4) * (1 - np.exp(-(f / low) ** 4))
    y = np.fft.irfft(spectrum * weight, n=n)
    return y / max(np.std(y), 1e-12)


def fade(x, attack=.006, release=.035):
    y = x.copy()
    na, nr = min(round(attack * SR), len(x)//2), min(round(release * SR), len(x)//2)
    if na:
        y[:na] *= np.sin(np.linspace(0, np.pi/2, na)) ** 2
    if nr:
        y[-nr:] *= np.cos(np.linspace(0, np.pi/2, nr)) ** 2
    return y


def burst(t, at, frequency, decay=.035, amplitude=1):
    u = np.maximum(t-at, 0)
    return amplitude * (t >= at) * (1 - np.exp(-u/.0015)) * np.exp(-u/decay) * tone(u, frequency)


def measure(path):
    x, rate = sf.read(path, dtype='float64', always_2d=True)
    peak = float(np.max(np.abs(x)))
    rms = float(np.sqrt(np.mean(x*x)))
    sample_peaks = np.max(np.abs(x),axis=1)
    onset_absolute = np.flatnonzero(sample_peaks > 10**(-50/20))
    onset_relative = np.flatnonzero(sample_peaks > peak*.1)
    window = round(rate*.005)
    blocks = len(x)//window
    block_power = np.mean(x[:blocks*window]**2,axis=1).reshape(blocks,window)
    envelope = np.sqrt(np.mean(block_power,axis=1))
    half_envelope = np.flatnonzero(envelope >= np.max(envelope)*.5)
    initial_rms = float(np.sqrt(np.mean(x[:round(rate*.01)]**2)))
    return {
        'file': str(path), 'sample_rate': rate, 'channels': x.shape[1],
        'samples': len(x), 'duration_s': len(x)/rate, 'subtype': sf.info(path).subtype,
        'peak_dbfs': float(20*np.log10(max(peak, 1e-15))),
        'rms_dbfs': float(20*np.log10(max(rms, 1e-15))),
        'finite': bool(np.isfinite(x).all()), 'clipped_samples': int(np.sum(np.abs(x)>=1)),
        'onset_minus50dbfs_ms': float(onset_absolute[0]*1000/rate) if len(onset_absolute) else None,
        'onset_peak_minus20db_ms': float(onset_relative[0]*1000/rate) if len(onset_relative) else None,
        'envelope_5ms_half_peak_at_ms': float(half_envelope[0]*window*1000/rate),
        'envelope_5ms_peak_at_ms': float(np.argmax(envelope)*window*1000/rate),
        'first_10ms_rms_dbfs': float(20*np.log10(max(initial_rms,1e-15))),
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
    }


assets = []


def save(name, x, target_rms, loop=False, description='', attack=.006, release=.035):
    x = np.asarray(x, dtype=np.float64)
    x -= np.mean(x)
    if not loop:
        x = fade(x, attack, release)
    x *= 10**(target_rms/20) / max(np.sqrt(np.mean(x*x)), 1e-12)
    x *= min(1, 10**(-3.2/20) / max(np.max(np.abs(x)), 1e-12))
    assert np.isfinite(x).all()
    path = ROOT / (name + '.wav')
    sf.write(path, x, SR, subtype='PCM_16')
    row = measure(path)
    row.update(id=name, loop=loop, description=description, provenance='procedural_numpy',
               seed=20261107, target_rms_dbfs=target_rms)
    if loop:
        data = sf.read(path)[0]
        row['wrap_step'] = float(abs(data[0]-data[-1]))
        row['max_internal_step'] = float(np.max(np.abs(np.diff(data))))
        assert row['wrap_step'] <= row['max_internal_step']
    assert row['finite'] and row['clipped_samples'] == 0 and row['peak_dbfs'] <= -3
    assets.append(row)


t = time(.095)
save('ui_click', burst(t,0,1350,.014)+.3*burst(t,.008,710,.016), -29,
     description='Soft compact console click')
t = time(.14)
save('bus_select', burst(t,0,430,.025)+.25*burst(t,.025,940,.025), -27,
     description='Soft relay latch on bus selection')
t = time(.6)
x = noise(t,180,1800) * (.7+.1*tone(t,5)) + .17*tone(t,100)
save('bus_drag_loop', x, -32, True, 'Quiet periodic friction and wheel/rail hum')
t = time(.18)
save('bus_snap', burst(t,.002,165,.035)+.35*burst(t,.014,740,.022)+.09*noise(t,500,2200)*np.exp(-t/.025), -25,
     description='Short cushioned mechanical stop')
t = time(.2)
save('action_rejected', (tone(t,190)+.2*tone(t,285))*np.exp(-t/.06), -27,
     description='Low brief terminal rejection')
# Preserve the original RNG draws and their lengths so all other assets stay byte-identical.
boarding_noise = noise(time(.24),350,1600)
t = time(.14)
x = burst(t,0,820,.015)+.5*burst(t,.002,1250,.012)+.25*burst(t,0,185,.018)
x += .12*boarding_noise[:len(t)]*np.exp(-t/.018)
save('passenger_board', x, -24,
     description='Bright neutral threshold contact with low thud; identical for all passengers',
     attack=.0015, release=.015)
departure_noise = noise(time(1.2),250,2200)
t = time(1.1)
traction = (1-np.exp(-t/.003))*np.exp(-t/.7)
x = (.62*tone(t,120+60*t)+.2*tone(t,400+65*t))*traction
x += .18*departure_noise[:len(t)]*traction
x += .65*burst(t,0,920,.02)+.4*burst(t,.001,430,.03)
save('bus_depart', x, -26,
     description='Immediate mechanical release and quiet electric traction with a bright onset',
     attack=.0015, release=.09)
t = time(.28)
save('scan_start', burst(t,0,310,.023)+burst(t,.06,970,.055)+.1*noise(t,600,4300)*np.exp(-t/.09), -26,
     description='Relay then short scan engagement tone')
t = time(1)
x = .3*tone(t,120)+.12*tone(t,360)+noise(t,650,4000)*(.3+.06*tone(t,2))
save('scan_loop', x, -34, True, 'Quiet periodic scanner hum and hiss; no passenger-type cue')
t = time(.19)
save('scan_end', burst(t,0,640,.025)+.6*burst(t,.055,300,.035), -28,
     description='Neutral scanner disengagement')
t = time(.48)
save('security_alert', burst(t,.005,580,.075)+.7*burst(t,.21,420,.08), -25,
     description='Restrained two-pulse security notice')
t = time(.5)
x = noise(t,180,4200)*np.exp(-t/.115)*(.4+.6*np.sin(2*np.pi*41*t)**2)
x += .3*tone(t,83)*np.exp(-t/.18)
save('quarantine_discharge', x, -26, description='Contained electric discharge; no sudden loud crack')
t = time(.65)
x = tone(t,78-28*t)*np.exp(-t/.15)+.35*noise(t,250,1900)*np.exp(-t/.09)
x += .12*tone(t,150)*np.exp(-t/.25)*(1+.3*tone(t,19))
save('stability_loss', x, -24, description='Low damped impact and brief electrical sag')
t = time(.65)
save('level_win', burst(t,.008,440,.17)+.6*burst(t,.16,660,.21), -26,
     description='Short calm rising confirmation, no music bed')
t = time(.8)
save('level_lose', tone(t,165-58*t)*np.exp(-t/.23)+.15*noise(t,120,900)*np.exp(-t/.2), -26,
     description='Low falling terminal tone, no scream')

selected_a = [measure(ROOT.parent/'samples'/'normalized'/name) for name in
              ['station_A.wav','quarantine_door_A.wav','bus_braking_A.wav']]


def save_extract(name, source_name, duration, search_start, search_end):
    """Pick strongest RMS window in stated region, with 10 ms scan increments.

    This is an energy-based prototype edit, not semantic identification by ear.
    """
    source = ROOT.parent/'samples'/'normalized'/source_name
    x, rate = sf.read(source, dtype='float64', always_2d=True)
    n = round(duration * rate)
    first = round(search_start * rate)
    last = round(search_end * rate) - n
    positions = np.arange(first, max(last,first)+1, round(rate*.01))
    power = np.mean(x*x,axis=1)
    cumulative = np.concatenate([[0],np.cumsum(power)])
    energy = cumulative[positions+n] - cumulative[positions]
    offset = int(positions[np.argmax(energy)])
    y = x[offset:offset+n].copy()
    # Preserve source level except a peak safety cap and short end fades.
    attack, release = round(rate*.008), round(rate*.025)
    y[:attack] *= np.sin(np.linspace(0,np.pi/2,attack))[:,None]**2
    y[-release:] *= np.cos(np.linspace(0,np.pi/2,release))[:,None]**2
    peak = float(np.max(np.abs(y)))
    gain = min(1,10**(-3.2/20)/max(peak,1e-12))
    y *= gain
    path = ROOT/(name+'.wav')
    sf.write(path,y,rate,subtype='PCM_16')
    row = measure(path)
    row.update(id=name,loop=False,provenance='selected_Stable_Audio_SmallSFX_A_edit',
        source=str(source), source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        source_offset_s=offset/rate, selection='Maximum RMS 10ms-grid window in region; not auditory classification',
        search_region_s=[search_start,search_end],fade_in_s=.008,fade_out_s=.025,gain_db=20*np.log10(gain))
    assert row['finite'] and row['clipped_samples']==0 and row['peak_dbfs'] <= -3
    assets.append(row)


save_extract('quarantine_open_A','quarantine_door_A.wav',.30,0,2.4)
save_extract('quarantine_close_A','quarantine_door_A.wav',.30,3.6,6)
save_extract('bus_arrive_A','bus_braking_A.wav',.8,0,6)
save_extract('bus_stop_A','bus_braking_A.wav',.25,2.5,6)

source = ROOT.parent/'samples'/'normalized'/'station_A.wav'
x, rate = sf.read(source,dtype='float64',always_2d=True)
overlap = round(.5*rate)
u = np.arange(overlap)/(overlap-1)
crossfade = x[-overlap:]*np.cos(u*np.pi/2)[:,None]+x[:overlap]*np.sin(u*np.pi/2)[:,None]
y = np.concatenate([x[overlap:-overlap],crossfade])
gain = min(1,10**(-3.2/20)/max(float(np.max(np.abs(y))),1e-12))
y *= gain
path = ROOT/'station_A_loop.wav'
sf.write(path,y,rate,subtype='PCM_16')
row = measure(path)
row.update(id='station_A_loop',loop=True,provenance='selected_Stable_Audio_SmallSFX_A_edit',
    source=str(source),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
    edit='Middle from .5 to 19.5 sec followed by equal-power overlap of 19.5..20 sec and 0...5 sec; loop returns to .5 sec',
    crossfade_s=.5,gain_db=20*np.log10(gain),
    wrap_step=float(np.max(np.abs(y[0]-y[-1]))),max_internal_step=float(np.max(np.abs(np.diff(y,axis=0)))))
assert row['finite'] and row['clipped_samples']==0 and row['peak_dbfs']<=-3
assert row['wrap_step'] <= row['max_internal_step']
assets.append(row)

manifest = {
    'version': 1, 'generator': str(Path(__file__).resolve()),
    'generator_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'seed': 20261107, 'numpy_version': np.__version__, 'soundfile_version': sf.__version__,
    'rights': 'Original procedural prototype assets created for this project; no third-party source audio.',
    'listening_status': 'Not auditioned; objective signal checks only.',
    'assets': assets, 'existing_selected_A_read_only_checks': selected_a,
}
archive = ROOT/'archive'/'pre_timingfix'
if archive.is_dir():
    changed = sorted(path.name for path in ROOT.glob('*.wav')
                     if path.read_bytes() != (archive/path.name).read_bytes())
    assert changed == ['bus_depart.wav','passenger_board.wav'], changed
    manifest['timing_revision'] = {
        'changed_only': changed, 'unchanged_wav_sha256_count': len(assets)-len(changed),
        'archive': str(archive),
        'comparison': {name: {'before':measure(archive/name),'after':measure(ROOT/name)} for name in changed},
        'mix_estimate': 'With normalize=false stereo-to-mono mean, station cue .65, boarding cue .9: '
            'default .32/.8 preferences yield ~11.29 dB broadband RMS boarding/station ratio; '
            '.65/.72 preferences yield ~4.22 dB. This does not prove subjective readability.'
    }
    report = dict(manifest['timing_revision'])
    report['verification'] = 'Objective signal/file checks only; Unity import and subjective listening not verified here.'
    report['required_unity_import'] = {'normalize': False, 'forceToMono': True}
    report['unchanged_wavs'] = [{'file':asset['id']+'.wav','sha256':asset['sha256']} for asset in assets
                                if asset['id']+'.wav' not in changed]
    station_data, _ = sf.read(ROOT/'station_A_loop.wav',always_2d=True)
    station_rms = float(np.sqrt(np.mean(np.mean(station_data,axis=1)**2)))
    board_data, _ = sf.read(ROOT/'passenger_board.wav',always_2d=True)
    board_rms = float(np.sqrt(np.mean(board_data**2)))
    report['mix_predictions_without_import_normalization'] = []
    for label, atmosphere, effects in [('defaults',.32,.8),('browser_slider_test',.65,.72)]:
        station_level = 20*np.log10(station_rms*atmosphere*.65)
        board_level = 20*np.log10(board_rms*effects*.9)
        report['mix_predictions_without_import_normalization'].append({
            'preferences': label, 'atmosphere':atmosphere, 'effects':effects,
            'station_rms_dbfs':float(station_level),'boarding_rms_dbfs':float(board_level),
            'boarding_over_station_rms_db':float(board_level-station_level),
        })
    (ROOT/'timing_comparison.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'generated': len(assets), 'assets': [dict(id=a['id'],duration_s=a['duration_s'],
    rms_dbfs=round(a['rms_dbfs'],2),peak_dbfs=round(a['peak_dbfs'],2)) for a in assets],
    'existing_A': selected_a},ensure_ascii=False,indent=2))
