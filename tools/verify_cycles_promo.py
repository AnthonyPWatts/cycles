#!/usr/bin/env python3
"""Verify the Cycles promo master and its web-delivery derivative."""

from __future__ import annotations

import argparse
import re
import subprocess
from pathlib import Path


EXPECTED_OUTPUTS = {
    Path("tools/promo/cycles-promo-30s-master.mp4"): (30.0, 900, 0.011, -43.0, None),
    Path("src/Cycles.Api/wwwroot/media/cycles-promo.mp4"): (30.0, 900, 0.025, -45.0, 25 * 1024 * 1024),
}


def run(command: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, check=True, capture_output=True, text=True)


def read_stream_metadata(path: Path, ffmpeg: Path) -> tuple[float, int, int, float, int, str]:
    inspection = subprocess.run(
        [str(ffmpeg), "-hide_banner", "-i", str(path)],
        check=False,
        capture_output=True,
        text=True,
    )
    output = inspection.stderr
    duration_match = re.search(r"Duration:\s+(\d+):(\d+):(\d+(?:\.\d+)?)", output)
    video_match = re.search(
        r"Video:.*?,\s+(\d{2,5})x(\d{2,5})(?:\s|,).*?,\s+([0-9.]+)\s+fps",
        output,
    )
    audio_match = re.search(r"Audio:.*?,\s+(\d+)\s+Hz,\s+([^,\r\n]+)", output)
    if not duration_match or not video_match or not audio_match:
        raise RuntimeError(f"Could not read stream metadata from {path}.")
    hours, minutes, seconds = duration_match.groups()
    duration = int(hours) * 3600 + int(minutes) * 60 + float(seconds)
    width, height, frame_rate = video_match.groups()
    sample_rate, channel_layout = audio_match.groups()
    return duration, int(width), int(height), float(frame_rate), int(sample_rate), channel_layout.strip()


def count_decoded_frames(path: Path, ffmpeg: Path) -> int:
    decode = run(
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
            "-progress",
            "pipe:1",
            "-nostats",
            "-f",
            "null",
            "NUL",
        ]
    )
    frames = re.findall(r"^frame=(\d+)$", decode.stdout, re.MULTILINE)
    if not frames:
        raise RuntimeError(f"Could not count decoded frames in {path}.")
    return int(frames[-1])


def verify(
    path: Path,
    expected_duration: float,
    expected_frames: int,
    duration_tolerance: float,
    tail_peak_limit: float,
    max_bytes: int | None,
    ffmpeg: Path,
) -> None:
    if not path.exists():
        raise FileNotFoundError(path)

    actual_duration, width, height, frame_rate, sample_rate, channel_layout = read_stream_metadata(path, ffmpeg)
    actual_frames = count_decoded_frames(path, ffmpeg)
    failures: list[str] = []
    if abs(actual_duration - expected_duration) > duration_tolerance:
        failures.append(f"duration {actual_duration:.3f}s != {expected_duration:.3f}s")
    if actual_frames != expected_frames:
        failures.append(f"frame count {actual_frames} != {expected_frames}")
    if (width, height) != (1920, 1080):
        failures.append(f"dimensions {width}x{height} != 1920x1080")
    if abs(frame_rate - 30.0) > 0.001:
        failures.append(f"frame rate {frame_rate:g} != 30")
    if sample_rate != 48000 or "stereo" not in channel_layout.lower():
        failures.append(f"audio format {sample_rate} Hz/{channel_layout} != 48000 Hz/stereo")

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
        if final_peak > tail_peak_limit:
            failures.append(
                f"final 250 ms peak {final_peak:.1f} dB exceeds {tail_peak_limit:.1f} dB"
            )
    if max_bytes is not None and path.stat().st_size > max_bytes:
        failures.append(f"file size {path.stat().st_size} bytes exceeds {max_bytes} bytes")

    if failures:
        raise RuntimeError(f"{path}: " + "; ".join(failures))

    print(
        f"PASS {path} ({actual_duration:.3f}s, {actual_frames} frames, "
        f"{path.stat().st_size / 1024 / 1024:.2f} MiB)"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    args = parser.parse_args()
    if not args.ffmpeg.exists():
        raise FileNotFoundError(args.ffmpeg)

    for path, (duration, frames, tolerance, tail_limit, max_bytes) in EXPECTED_OUTPUTS.items():
        verify(path, duration, frames, tolerance, tail_limit, max_bytes, args.ffmpeg)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
