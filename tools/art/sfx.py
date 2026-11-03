import math
import struct
import wave
from pathlib import Path

RATE = 22050
OUT = Path(__file__).resolve().parents[2] / "art" / "sounds"


def note(name):
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    base, octave = name[:-1], int(name[-1])
    semitone = names[base[0]] + (1 if base.endswith("#") else 0)
    return 440 * 2 ** ((semitone + (octave - 4) * 12) / 12)


def tone(freq, length, shape="square", volume=0.25, sweep=None, decay=6.0, duty=0.25):
    samples = []
    phase = 0.0
    count = int(RATE * length)
    for i in range(count):
        t = i / count
        f = freq if sweep is None else freq + (sweep - freq) * t
        phase += f / RATE
        cycle = phase % 1.0
        if shape == "square":
            value = 1.0 if cycle < duty else -1.0
        elif shape == "triangle":
            value = 4 * abs(cycle - 0.5) - 1
        else:
            value = math.sin(phase * math.tau)
        attack = min(1.0, i / (RATE * 0.004))
        envelope = attack * math.exp(-decay * t)
        samples.append(value * envelope * volume)
    return samples


def sequence(notes, step, shape="square", volume=0.22, decay=5.0, overlap=1.6):
    total = int(RATE * step * (len(notes) - 1 + overlap))
    mix = [0.0] * total
    for index, name in enumerate(notes):
        part = tone(note(name), step * overlap, shape, volume, decay=decay)
        start = int(RATE * step * index)
        for j, value in enumerate(part):
            if start + j < total:
                mix[start + j] += value
    return mix


def layered(*tracks):
    length = max(len(t) for t in tracks)
    return [sum(t[i] for t in tracks if i < len(t)) for i in range(length)]


def write(name, samples):
    OUT.mkdir(parents=True, exist_ok=True)
    peak = max(1e-6, max(abs(s) for s in samples))
    scale = min(1.0, 0.9 / peak)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767 * 0.55)) for s in samples))


def main():
    write("click", tone(note("E6"), 0.05, "square", sweep=note("A6"), decay=9, duty=0.5))
    write("toggle", sequence(["C6", "G6"], 0.045, "square", decay=8, overlap=1.2))
    write("select", tone(note("C6"), 0.09, "triangle", volume=0.4, decay=7))
    write("pop", tone(note("C5"), 0.11, "triangle", volume=0.45, sweep=note("C6"), decay=4))
    write("copy", sequence(["E6", "G#6", "B6", "E7"], 0.04, "triangle", volume=0.35, decay=6))
    write("online", layered(
        sequence(["C5", "E5", "G5", "C6", "E6"], 0.09, "square", volume=0.16, decay=4),
        sequence(["C4", "G4", "C5"], 0.15, "triangle", volume=0.3, decay=3)))
    write("error", sequence(["E5", "C5"], 0.12, "square", volume=0.18, decay=5, overlap=1.3))
    write("create", sequence(["G5", "B5", "D6", "G6", "B6", "D7"], 0.05, "triangle", volume=0.32, decay=5, overlap=2.2))
    write("stop", sequence(["G5", "E5", "C5"], 0.08, "triangle", volume=0.35, decay=5))


if __name__ == "__main__":
    main()
