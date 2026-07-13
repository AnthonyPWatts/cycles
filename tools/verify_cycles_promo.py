#!/usr/bin/env python3
"""Verify the rendered Cycles promo variants and their resolved CTA outro."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
from pathlib import Path


EXPECTED_OUTPUTS = {
    Path("output/promo/cycles-promo-15s.mp4"): (16.0, 480),
    Path("output/promo/cycles-promo-30s.mp4"): (32.0, 960),
    Path("output/promo/cycles-promo-30s-banging.mp4"): (32.0, 960),
    Path("output/promo/cycles-promo-30s-aaa.mp4"): (32.0, 960),
    Path("src/Cycles.Api/wwwroot/media/cycles-promo-32s.mp4"): (32.0, 960),
}


def run(command: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, check=True, capture_output=True, text=True)


def verify(path: Path, expected_duration: float, expected_frames: int, ffmpeg: Path, ffprobe: Path) -> None:
    if not path.exists():
        raise FileNotFoundError(path)

    probe = run(
        [
            str(ffprobe),
            "-v",
            "error",
            "-show_entries",
            "format=duration:stream=codec_type,codec_name,width,height,r_frame_rate,nb_frames,sample_rate,channels,duration",
            "-of",
            "json",
            str(path),
        ]
    )
    metadata = json.loads(probe.stdout)
    streams = metadata["streams"]
    video = next(stream for stream in streams if stream["codec_type"] == "video")
    audio = next(stream for stream in streams if stream["codec_type"] == "audio")

    actual_duration = float(metadata["format"]["duration"])
    audio_duration = float(audio["duration"])
    failures: list[str] = []
    if abs(actual_duration - expected_duration) > 0.001:
        failures.append(f"duration {actual_duration:.3f}s != {expected_duration:.3f}s")
    if abs(audio_duration - expected_duration) > 0.001:
        failures.append(f"audio duration {audio_duration:.3f}s != {expected_duration:.3f}s")
    if int(video["nb_frames"]) != expected_frames:
        failures.append(f"frame count {video['nb_frames']} != {expected_frames}")
    if (video["width"], video["height"]) != (1920, 1080):
        failures.append(f"dimensions {video['width']}x{video['height']} != 1920x1080")
    if video["r_frame_rate"] != "30/1":
        failures.append(f"frame rate {video['r_frame_rate']} != 30/1")
    if audio["sample_rate"] != "48000" or audio["channels"] != 2:
        failures.append(f"audio format {audio['sample_rate']} Hz/{audio['channels']} channels != 48000 Hz/stereo")

    final_frame = run(
        [
            str(ffmpeg),
            "-hide_banner",
            "-loglevel",
            "info",
            "-sseof",
            "-0.04",
            "-i",
            str(path),
            "-frames:v",
            "1",
            "-vf",
            "signalstats,metadata=print:key=lavfi.signalstats.YAVG",
            "-f",
            "null",
            "NUL",
        ]
    )
    luma_matches = re.findall(r"lavfi\.signalstats\.YAVG=([0-9.]+)", final_frame.stderr)
    if not luma_matches:
        failures.append("could not measure final-frame luminance")
    elif float(luma_matches[-1]) < 30.0:
        failures.append(f"final frame is too dark for the CTA (YAVG {float(luma_matches[-1]):.2f})")

    tail = run(
        [
            str(ffmpeg),
            "-hide_banner",
            "-ss",
            f"{expected_duration - 0.25:.3f}",
            "-i",
            str(path),
            "-vn",
            "-af",
            "volumedetect",
            "-f",
            "null",
            "NUL",
        ]
    )
    volume_matches = re.findall(r"max_volume:\s+(-?inf|-?[0-9.]+) dB", tail.stderr)
    if not volume_matches:
        failures.append("could not measure final audio decay")
    else:
        final_peak = float("-inf") if volume_matches[-1] == "-inf" else float(volume_matches[-1])
        if final_peak > -45.0:
            failures.append(f"final 250 ms has not decayed cleanly (peak {final_peak:.1f} dB)")

    if failures:
        raise RuntimeError(f"{path}: " + "; ".join(failures))

    run(
        [
            str(ffmpeg),
            "-v",
            "error",
            "-i",
            str(path),
            "-map",
            "0:v:0",
            "-map",
            "0:a:0",
            "-f",
            "null",
            "NUL",
        ]
    )
    print(f"PASS {path} ({actual_duration:.3f}s, {video['nb_frames']} frames)")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    args = parser.parse_args()
    ffprobe = args.ffmpeg.with_name("ffprobe.exe")
    for executable in (args.ffmpeg, ffprobe):
        if not executable.exists():
            raise FileNotFoundError(executable)

    for path, (duration, frames) in EXPECTED_OUTPUTS.items():
        verify(path, duration, frames, args.ffmpeg, ffprobe)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
