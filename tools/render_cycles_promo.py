#!/usr/bin/env python3
"""Render the Cycles promo master and its smaller 1080p web-delivery derivative."""

from __future__ import annotations

import argparse
import math
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps


WIDTH = 1920
HEIGHT = 1080
FPS = 30
DURATION = 30.0
FRAME_COUNT = int(FPS * DURATION)
SAMPLE_RATE = 48_000
FINAL_HIT_TIME = 28.0
AUDIO_FADE_START = 28.0

INK = "#0A0D0B"
INK_2 = "#111611"
TEXT = "#F3F1E8"
MUTED = "#B8C1B4"
GREEN = "#8ED8BC"
GOLD = "#E1BD67"
RED = "#D86455"
PAPER = "#F1E7CF"

FONT_DISPLAY_PATH = Path(r"C:\Windows\Fonts\ARIALNB.TTF")
FONT_BODY_PATH = Path(r"C:\Windows\Fonts\segoeui.ttf")
FONT_BOLD_PATH = Path(r"C:\Windows\Fonts\segoeuib.ttf")


def clamp(value: float, low: float = 0.0, high: float = 1.0) -> float:
    return max(low, min(high, value))


def smooth(value: float) -> float:
    value = clamp(value)
    return value * value * (3.0 - 2.0 * value)


def ease_out(value: float) -> float:
    value = clamp(value)
    return 1.0 - (1.0 - value) ** 3


def scene_opacity(
    t: float,
    start: float,
    fade_in: float,
    fade_out_start: float,
    end: float,
) -> float:
    if t < start or t > end:
        return 0.0
    if t < start + fade_in:
        return smooth((t - start) / max(fade_in, 0.001))
    if t > fade_out_start:
        return 1.0 - smooth((t - fade_out_start) / max(end - fade_out_start, 0.001))
    return 1.0


def hex_rgb(value: str) -> tuple[int, int, int]:
    value = value.lstrip("#")
    return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))


def rgba(value: str, alpha: float = 255, opacity: float = 1.0) -> tuple[int, int, int, int]:
    return (*hex_rgb(value), int(clamp(alpha * opacity, 0, 255)))


def font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(path), size=size)


FONTS = {
    "hero": font(FONT_DISPLAY_PATH, 240),
    "hero_small": font(FONT_DISPLAY_PATH, 168),
    "headline": font(FONT_DISPLAY_PATH, 116),
    "headline_small": font(FONT_DISPLAY_PATH, 88),
    "subhead": font(FONT_DISPLAY_PATH, 56),
    "label": font(FONT_BOLD_PATH, 22),
    "label_small": font(FONT_BOLD_PATH, 18),
    "body": font(FONT_BODY_PATH, 26),
    "body_small": font(FONT_BODY_PATH, 20),
    "ui": font(FONT_BOLD_PATH, 30),
    "ui_big": font(FONT_DISPLAY_PATH, 72),
    "card_title": font(FONT_DISPLAY_PATH, 76),
}


def tracked_width(text: str, text_font: ImageFont.FreeTypeFont, spacing: float) -> float:
    if not text:
        return 0.0
    return sum(text_font.getlength(character) for character in text) + spacing * (len(text) - 1)


def draw_tracked(
    draw: ImageDraw.ImageDraw,
    position: tuple[float, float],
    text: str,
    text_font: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int, int],
    spacing: float = 0,
    align: str = "left",
) -> None:
    x, y = position
    width = tracked_width(text, text_font, spacing)
    if align == "center":
        x -= width / 2
    elif align == "right":
        x -= width
    for character in text:
        draw.text((x, y), character, font=text_font, fill=fill)
        x += text_font.getlength(character) + spacing


def draw_glow_text(
    layer: Image.Image,
    position: tuple[float, float],
    text: str,
    text_font: ImageFont.FreeTypeFont,
    colour: str,
    opacity: float,
    spacing: float = 0,
    align: str = "left",
    blur: float = 18,
) -> None:
    glow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    draw_tracked(
        glow_draw,
        position,
        text,
        text_font,
        rgba(colour, 90, opacity),
        spacing,
        align,
    )
    layer.alpha_composite(glow.filter(ImageFilter.GaussianBlur(blur)))
    draw_tracked(
        ImageDraw.Draw(layer),
        position,
        text,
        text_font,
        rgba(colour, 255, opacity),
        spacing,
        align,
    )


def create_vignette() -> Image.Image:
    y, x = np.ogrid[-1:1:complex(0, HEIGHT), -1:1:complex(0, WIDTH)]
    distance = np.sqrt((x * 0.92) ** 2 + (y * 1.08) ** 2)
    alpha = np.clip((distance - 0.44) / 0.76, 0, 1) ** 1.7
    image = np.zeros((HEIGHT, WIDTH, 4), dtype=np.uint8)
    image[..., 3] = (alpha * 188).astype(np.uint8)
    return Image.fromarray(image, "RGBA")


def create_left_gradient() -> Image.Image:
    x = np.linspace(0, 1, WIDTH, dtype=np.float32)
    alpha = np.clip(1 - x / 0.68, 0, 1) ** 1.4 * 180
    image = np.zeros((HEIGHT, WIDTH, 4), dtype=np.uint8)
    image[..., 3] = np.broadcast_to(alpha.astype(np.uint8), (HEIGHT, WIDTH))
    return Image.fromarray(image, "RGBA")


def create_grid() -> Image.Image:
    layer = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    for index in range(-14, 29):
        x = index * 86
        draw.line((x - 180, 0, x + 180, HEIGHT), fill=rgba(TEXT, 15), width=1)
    for index in range(-5, 20):
        y = index * 78
        draw.line((0, y + 135, WIDTH, y - 135), fill=rgba(TEXT, 13), width=1)
    return layer


def create_grain_variants() -> list[Image.Image]:
    variants: list[Image.Image] = []
    rng = np.random.default_rng(71421)
    for _ in range(4):
        values = rng.normal(128, 25, (HEIGHT // 2, WIDTH // 2)).clip(0, 255).astype(np.uint8)
        rgba_values = np.zeros((HEIGHT // 2, WIDTH // 2, 4), dtype=np.uint8)
        rgba_values[..., :3] = values[..., None]
        rgba_values[..., 3] = 10
        variants.append(
            Image.fromarray(rgba_values, "RGBA").resize((WIDTH, HEIGHT), Image.Resampling.BILINEAR)
        )
    return variants


def crop_background(background: Image.Image, t: float) -> Image.Image:
    pan_x = 84 * math.sin(t * 0.115) + 45 * (t / DURATION)
    pan_y = 34 * math.cos(t * 0.09)
    left = int((background.width - WIDTH) / 2 + pan_x)
    top = int((background.height - HEIGHT) / 2 + pan_y)
    return background.crop((left, top, left + WIDTH, top + HEIGHT)).convert("RGBA")


def orbital_mark(
    layer: Image.Image,
    centre: tuple[float, float],
    radius: float,
    colour: str,
    opacity: float,
    pulse: float = 0.0,
) -> None:
    x, y = centre
    glow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    for spread, alpha in ((1.0, 120), (1.7, 48), (2.6, 20)):
        r = radius * spread + pulse
        glow_draw.ellipse((x - r, y - r, x + r, y + r), outline=rgba(colour, alpha, opacity), width=max(2, int(radius * 0.13)))
    layer.alpha_composite(glow.filter(ImageFilter.GaussianBlur(max(6, radius * 0.38))))
    draw = ImageDraw.Draw(layer)
    draw.ellipse(
        (x - radius, y - radius, x + radius, y + radius),
        outline=rgba(colour, 235, opacity),
        width=max(2, int(radius * 0.12)),
    )


MAP_NODES = {
    "ASTER VALE": (330, 735, GREEN),
    "NADIR CROSSING": (730, 515, PAPER),
    "PALE HARBOUR": (620, 865, GOLD),
    "TREATY GATE": (1185, 620, RED),
    "KHEPRI REACH": (1530, 400, PAPER),
    "GLASS MERIDIAN": (1040, 885, PAPER),
    "RED LATTICE": (1480, 825, PAPER),
}

MAP_EDGES = (
    ("ASTER VALE", "NADIR CROSSING"),
    ("ASTER VALE", "PALE HARBOUR"),
    ("NADIR CROSSING", "TREATY GATE"),
    ("PALE HARBOUR", "TREATY GATE"),
    ("TREATY GATE", "KHEPRI REACH"),
    ("TREATY GATE", "GLASS MERIDIAN"),
    ("TREATY GATE", "RED LATTICE"),
    ("GLASS MERIDIAN", "RED LATTICE"),
)


def draw_route(
    layer: Image.Image,
    start: tuple[float, float],
    end: tuple[float, float],
    opacity: float,
    progress: float = 1.0,
    colour: str = MUTED,
    width: int = 2,
) -> None:
    progress = clamp(progress)
    ex = start[0] + (end[0] - start[0]) * progress
    ey = start[1] + (end[1] - start[1]) * progress
    glow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).line((*start, ex, ey), fill=rgba(colour, 62, opacity), width=width + 8)
    layer.alpha_composite(glow.filter(ImageFilter.GaussianBlur(8)))
    ImageDraw.Draw(layer).line((*start, ex, ey), fill=rgba(colour, 120, opacity), width=width)


def draw_system(
    layer: Image.Image,
    name: str,
    x: float,
    y: float,
    colour: str,
    opacity: float,
    t: float,
    important: bool = False,
) -> None:
    radius = 10 if not important else 16
    pulse = (math.sin(t * 3.1 + x * 0.01) + 1.0) * 5
    glow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    glow_draw.ellipse((x - radius * 2, y - radius * 2, x + radius * 2, y + radius * 2), fill=rgba(colour, 120, opacity))
    layer.alpha_composite(glow.filter(ImageFilter.GaussianBlur(17)))
    draw = ImageDraw.Draw(layer)
    draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=rgba(colour, 245, opacity))
    if important:
        ring = radius + 12 + pulse
        draw.ellipse((x - ring, y - ring, x + ring, y + ring), outline=rgba(colour, 120, opacity), width=2)
    draw_tracked(draw, (x + radius + 13, y - 14), name, FONTS["label_small"], rgba(TEXT, 220, opacity), 0.5)


def draw_map(layer: Image.Image, t: float, opacity: float, reveal: float = 1.0) -> None:
    route_progress = smooth(reveal)
    for start_name, end_name in MAP_EDGES:
        start = MAP_NODES[start_name]
        end = MAP_NODES[end_name]
        draw_route(layer, start[:2], end[:2], opacity, route_progress)
    for index, (name, (x, y, colour)) in enumerate(MAP_NODES.items()):
        node_reveal = smooth((reveal * 1.4) - index * 0.055)
        if node_reveal > 0:
            draw_system(layer, name, x, y, colour, opacity * node_reveal, t, name in {"ASTER VALE", "TREATY GATE"})


def draw_frame_corners(layer: Image.Image, opacity: float) -> None:
    draw = ImageDraw.Draw(layer)
    margin = 56
    length = 44
    colour = rgba(TEXT, 44, opacity)
    for x, y, sx, sy in (
        (margin, margin, 1, 1),
        (WIDTH - margin, margin, -1, 1),
        (margin, HEIGHT - margin, 1, -1),
        (WIDTH - margin, HEIGHT - margin, -1, -1),
    ):
        draw.line((x, y, x + sx * length, y), fill=colour, width=1)
        draw.line((x, y, x, y + sy * length), fill=colour, width=1)


def scene_logo(frame: Image.Image, t: float, opacity: float) -> None:
    if opacity <= 0:
        return
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    intro = ease_out((t - 0.05) / 0.75)
    x = 142 - (1 - intro) * 56
    orbital_mark(layer, (x + 18, 258), 15, GOLD, opacity * intro, math.sin(t * 3.6) * 2)
    draw_tracked(
        ImageDraw.Draw(layer),
        (x + 52, 240),
        "A PERSISTENT GALACTIC STRATEGY GAME",
        FONTS["label_small"],
        rgba(GOLD, 245, opacity * intro),
        3.0,
    )
    draw_glow_text(layer, (x, 302), "CYCLES", FONTS["hero"], TEXT, opacity * intro, -4.0, blur=24)
    line_progress = smooth((t - 0.55) / 0.65)
    ImageDraw.Draw(layer).line(
        (x + 5, 575, x + 665 * line_progress, 575),
        fill=rgba(GOLD, 170, opacity),
        width=2,
    )
    tagline_opacity = opacity * smooth((t - 0.88) / 0.52)
    draw_tracked(
        ImageDraw.Draw(layer),
        (x + 4, 608),
        "COMMAND THE PRESENT.",
        FONTS["subhead"],
        rgba(PAPER, 250, tagline_opacity),
        1.2,
    )
    draw_tracked(
        ImageDraw.Draw(layer),
        (x + 6, 692),
        "WHEN THE GALAXY RESETS, THE STORY DOES NOT.",
        FONTS["label_small"],
        rgba(MUTED, 220, opacity * smooth((t - 1.65) / 0.75)),
        2.0,
    )
    orbital_mark(layer, (1321, 326), 34, GOLD, opacity * 0.85, math.sin(t * 2.4) * 5)
    orbital_mark(layer, (1554, 545), 19, GREEN, opacity * 0.55, math.sin(t * 2.8 + 1) * 4)
    draw_frame_corners(layer, opacity)
    frame.alpha_composite(layer)


def scene_influence(frame: Image.Image, t: float, opacity: float) -> None:
    if opacity <= 0:
        return
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    layer.alpha_composite(Image.new("RGBA", frame.size, rgba(INK, 76, opacity)))
    local = clamp((t - 3.95) / 5.35)
    draw_map(layer, t, opacity, local)
    draw_tracked(
        ImageDraw.Draw(layer),
        (118, 112),
        "01 / INFLUENCE",
        FONTS["label"],
        rgba(GOLD, 235, opacity),
        3.5,
    )
    draw_glow_text(layer, (112, 148), "COMMAND THE PRESENT.", FONTS["headline"], TEXT, opacity, -0.8, blur=14)
    aster = MAP_NODES["ASTER VALE"][:2]
    nadir = MAP_NODES["NADIR CROSSING"][:2]
    travel = smooth((t - 5.35) / 3.0)
    draw_route(layer, aster, nadir, opacity, travel, GREEN, 4)
    fx = aster[0] + (nadir[0] - aster[0]) * travel
    fy = aster[1] + (nadir[1] - aster[1]) * travel
    fleet = ImageDraw.Draw(layer)
    fleet.polygon(
        ((fx + 14, fy), (fx - 9, fy - 8), (fx - 4, fy), (fx - 9, fy + 8)),
        fill=rgba(GREEN, 250, opacity),
    )
    for index in range(3):
        ring = 34 + ((t * 58 + index * 42) % 126)
        ring_alpha = 92 * (1 - ((ring - 34) / 126))
        fleet.ellipse(
            (aster[0] - ring, aster[1] - ring, aster[0] + ring, aster[1] + ring),
            outline=rgba(GREEN, ring_alpha, opacity),
            width=2,
        )
    draw_tracked(
        fleet,
        (118, 982),
        "FLEETS CREATE PRESSURE. THE MAP RECORDS CONSEQUENCE.",
        FONTS["label_small"],
        rgba(MUTED, 210, opacity),
        2.3,
    )
    frame.alpha_composite(layer)


def rounded_card(image: Image.Image, size: tuple[int, int], radius: int = 18) -> Image.Image:
    card = ImageOps.fit(image.convert("RGB"), size, method=Image.Resampling.LANCZOS).convert("RGBA")
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255)
    card.putalpha(mask)
    return card


def scene_commands(
    frame: Image.Image,
    t: float,
    opacity: float,
    dashboard_card: Image.Image | None,
) -> None:
    if opacity <= 0:
        return
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    layer.alpha_composite(Image.new("RGBA", frame.size, rgba(INK, 130, opacity)))
    local = ease_out((t - 9.45) / 0.75)
    card_x = int(548 + (1 - local) * 160)
    card_y = 178
    if dashboard_card is not None:
        shadow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
        shadow_draw = ImageDraw.Draw(shadow)
        shadow_draw.rounded_rectangle(
            (card_x - 18, card_y - 18, card_x + dashboard_card.width + 18, card_y + dashboard_card.height + 18),
            radius=26,
            fill=rgba(INK, 220, opacity),
        )
        layer.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(28)))
        faded_card = dashboard_card.copy()
        faded_card.putalpha(faded_card.getchannel("A").point(lambda a: int(a * opacity)))
        layer.alpha_composite(faded_card, (card_x, card_y))
        ImageDraw.Draw(layer).rounded_rectangle(
            (card_x, card_y, card_x + dashboard_card.width, card_y + dashboard_card.height),
            radius=18,
            outline=rgba(GREEN, 120, opacity),
            width=2,
        )
    else:
        draw_map(layer, t, opacity * 0.72, 1.0)

    panel_x = int(92 - (1 - local) * 120)
    panel_y = 220
    panel_w = 600
    panel_h = 672
    draw = ImageDraw.Draw(layer)
    draw.rounded_rectangle(
        (panel_x, panel_y, panel_x + panel_w, panel_y + panel_h),
        radius=14,
        fill=rgba(INK_2, 242, opacity),
        outline=rgba(TEXT, 45, opacity),
        width=1,
    )
    draw_tracked(draw, (panel_x + 34, panel_y + 30), "COMMIT YOUR INTENT", FONTS["label"], rgba(GOLD, 245, opacity), 3.2)
    commands = (
        ("01", "MOVE", "ASTER VALE  →  NADIR CROSSING", GREEN),
        ("02", "COLONISE", "ESTABLISH AT PALE HARBOUR", GOLD),
        ("03", "ATTACK", "ENGAGE AT TREATY GATE", RED),
    )
    beat = min(2, max(0, int((t - 10.15) / 1.85)))
    for index, (number, title, detail, colour) in enumerate(commands):
        y = panel_y + 98 + index * 164
        active = index == beat
        draw.rounded_rectangle(
            (panel_x + 26, y, panel_x + panel_w - 26, y + 138),
            radius=9,
            fill=rgba(colour if active else TEXT, 23 if active else 10, opacity),
            outline=rgba(colour if active else TEXT, 190 if active else 38, opacity),
            width=2 if active else 1,
        )
        draw_tracked(draw, (panel_x + 48, y + 23), number, FONTS["label"], rgba(colour, 245, opacity), 1.8)
        draw_tracked(draw, (panel_x + 110, y + 15), title, FONTS["ui_big"], rgba(TEXT, 250, opacity), -0.8)
        draw_tracked(draw, (panel_x + 110, y + 92), detail, FONTS["label_small"], rgba(MUTED, 225, opacity), 0.7)
        if active:
            draw.ellipse((panel_x + panel_w - 68, y + 56, panel_x + panel_w - 48, y + 76), fill=rgba(colour, 245, opacity))
    draw_tracked(
        draw,
        (panel_x + 34, panel_y + panel_h - 56),
        "THE NEXT TICK RESOLVES EVERY COMMITMENT.",
        FONTS["label_small"],
        rgba(MUTED, 220, opacity),
        1.4,
    )
    frame.alpha_composite(layer)


def impact_effect(layer: Image.Image, centre: tuple[float, float], strength: float, opacity: float) -> None:
    if strength <= 0:
        return
    x, y = centre
    glow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(glow)
    radius = 24 + strength * 185
    draw.ellipse((x - radius * 0.55, y - radius * 0.55, x + radius * 0.55, y + radius * 0.55), fill=rgba(PAPER, 220 * (1 - strength * 0.5), opacity))
    draw.ellipse((x - radius, y - radius, x + radius, y + radius), outline=rgba(GOLD, 220 * (1 - strength), opacity), width=5)
    draw.ellipse((x - radius * 1.35, y - radius * 1.35, x + radius * 1.35, y + radius * 1.35), outline=rgba(RED, 150 * (1 - strength), opacity), width=3)
    layer.alpha_composite(glow.filter(ImageFilter.GaussianBlur(22)))
    crisp = ImageDraw.Draw(layer)
    for ray in range(18):
        angle = ray * math.tau / 18 + 0.13
        inner = radius * 0.18
        outer = radius * (0.72 + (ray % 4) * 0.1)
        crisp.line(
            (
                x + math.cos(angle) * inner,
                y + math.sin(angle) * inner,
                x + math.cos(angle) * outer,
                y + math.sin(angle) * outer,
            ),
            fill=rgba(GOLD if ray % 2 else RED, 130 * (1 - strength), opacity),
            width=2,
        )


def scene_tick(frame: Image.Image, t: float, opacity: float) -> None:
    if opacity <= 0:
        return
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    layer.alpha_composite(Image.new("RGBA", frame.size, rgba(INK, 166, opacity)))
    draw_map(layer, t, opacity * 0.32, 1.0)
    local = clamp((t - 15.9) / 5.25)
    scan = smooth((t - 16.4) / 3.1)
    scan_x = int(-70 + (WIDTH + 140) * scan)
    scan_layer = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    scan_draw = ImageDraw.Draw(scan_layer)
    scan_draw.rectangle((scan_x - 10, 0, scan_x + 10, HEIGHT), fill=rgba(GREEN, 95, opacity))
    scan_draw.rectangle((scan_x - 1, 0, scan_x + 1, HEIGHT), fill=rgba(PAPER, 210, opacity))
    layer.alpha_composite(scan_layer.filter(ImageFilter.GaussianBlur(18)))
    draw = ImageDraw.Draw(layer)
    draw_tracked(draw, (WIDTH / 2, 170), "AUTHORITATIVE TICK", FONTS["label"], rgba(GOLD, 245, opacity), 4.8, "center")
    draw_tracked(draw, (298, 420), "T0", FONTS["hero_small"], rgba(MUTED, 105, opacity), -2.0, "center")
    draw_tracked(draw, (1622, 420), "T1", FONTS["hero_small"], rgba(GREEN, 125, opacity), -2.0, "center")
    main_alpha = opacity * smooth((t - 17.05) / 0.7)
    draw_glow_text(layer, (WIDTH / 2, 338), "EVERY ORDER", FONTS["headline"], TEXT, main_alpha, 0.2, "center", 18)
    draw_glow_text(layer, (WIDTH / 2, 472), "BECOMES A FACT.", FONTS["headline"], GOLD, main_alpha, 0.2, "center", 22)
    draw_tracked(
        draw,
        (WIDTH / 2, 652),
        "MOVEMENT  ·  GROWTH  ·  COMBAT  ·  CONSEQUENCE",
        FONTS["label"],
        rgba(MUTED, 225, main_alpha),
        3.0,
        "center",
    )
    impact = clamp((t - 19.15) / 1.35)
    impact_effect(layer, MAP_NODES["TREATY GATE"][:2], impact, opacity)
    if local > 0.67:
        draw.rounded_rectangle((747, 810, 1173, 876), radius=33, fill=rgba(GREEN, 24, opacity), outline=rgba(GREEN, 140, opacity), width=2)
        draw_tracked(draw, (960, 827), "TICK COMMITTED", FONTS["label"], rgba(GREEN, 245, opacity), 3.1, "center")
    frame.alpha_composite(layer)


def draw_successor_map(layer: Image.Image, t: float, opacity: float) -> None:
    points = (
        (1060, 244, PAPER),
        (1394, 332, PAPER),
        (1212, 548, GOLD),
        (1558, 666, PAPER),
        (1010, 815, PAPER),
        (1430, 884, PAPER),
        (1718, 442, GREEN),
    )
    links = ((0, 1), (0, 2), (1, 2), (1, 6), (2, 3), (2, 4), (3, 5), (4, 5))
    reveal = smooth((t - 21.2) / 2.3)
    for a, b in links:
        draw_route(layer, points[a][:2], points[b][:2], opacity * 0.58, reveal, GOLD if 2 in (a, b) else MUTED, 2)
    draw = ImageDraw.Draw(layer)
    for index, (x, y, colour) in enumerate(points):
        local = smooth(reveal * 1.3 - index * 0.05)
        if local <= 0:
            continue
        radius = 9 if index != 2 else 16
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=rgba(colour, 220, opacity * local))
        if index == 2:
            ring = 32 + (math.sin(t * 3.1) + 1) * 7
            draw.ellipse((x - ring, y - ring, x + ring, y + ring), outline=rgba(GOLD, 170, opacity), width=3)
            draw_tracked(draw, (x + 34, y - 14), "TREATY GATE", FONTS["label_small"], rgba(GOLD, 240, opacity), 0.8)


def scene_history(frame: Image.Image, t: float, opacity: float) -> None:
    if opacity <= 0:
        return
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    layer.alpha_composite(Image.new("RGBA", frame.size, rgba(INK, 185, opacity)))
    draw_successor_map(layer, t, opacity)
    local = ease_out((t - 20.9) / 0.85)
    left_x = int(96 - (1 - local) * 80)
    draw = ImageDraw.Draw(layer)
    draw_tracked(draw, (left_x, 202), "THE CHRONICLE", FONTS["label"], rgba(GOLD, 245, opacity), 4.2)
    draw_glow_text(layer, (left_x, 248), "SOME BECOME", FONTS["headline_small"], TEXT, opacity, -0.4, blur=12)
    draw_glow_text(layer, (left_x, 350), "HISTORY.", FONTS["headline"], GOLD, opacity, -0.8, blur=20)
    draw_tracked(
        draw,
        (left_x + 4, 508),
        "WHEN THE GALAXY RESETS, THE STORY DOES NOT.",
        FONTS["label_small"],
        rgba(MUTED, 235, opacity),
        2.1,
    )
    card_x = int(960 + (1 - local) * 120)
    card_y = 240
    card_w = 824
    card_h = 590
    draw.rounded_rectangle(
        (card_x, card_y, card_x + card_w, card_y + card_h),
        radius=16,
        fill=rgba(INK_2, 235, opacity),
        outline=rgba(GOLD, 122, opacity),
        width=2,
    )
    draw.line((card_x + 40, card_y + 94, card_x + card_w - 40, card_y + 94), fill=rgba(TEXT, 40, opacity), width=1)
    draw_tracked(draw, (card_x + 42, card_y + 34), "CYCLE 01  /  SELECTED RECORD", FONTS["label_small"], rgba(GOLD, 240, opacity), 2.5)
    draw.text((card_x + 42, card_y + 132), "THE BATTLE OF\nTREATY GATE", font=FONTS["card_title"], fill=rgba(TEXT, 250, opacity), spacing=2)
    draw_tracked(draw, (card_x + 44, card_y + 326), "ENTERED THE CHRONICLE", FONTS["label"], rgba(RED, 245, opacity), 2.0)
    draw.multiline_text(
        (card_x + 44, card_y + 384),
        "Fleet actions were resolved by the simulation\nand preserved as authoritative history.",
        font=FONTS["body"],
        fill=rgba(MUTED, 235, opacity),
        spacing=10,
    )
    draw.rounded_rectangle(
        (card_x + 42, card_y + card_h - 92, card_x + 282, card_y + card_h - 40),
        radius=26,
        fill=rgba(GOLD, 28, opacity),
        outline=rgba(GOLD, 130, opacity),
        width=1,
    )
    draw_tracked(draw, (card_x + 162, card_y + card_h - 78), "HISTORIC SYSTEM", FONTS["label_small"], rgba(GOLD, 245, opacity), 1.6, "center")
    frame.alpha_composite(layer)


def scene_end(frame: Image.Image, t: float, opacity: float) -> None:
    if opacity <= 0:
        return
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    layer.alpha_composite(Image.new("RGBA", frame.size, rgba(INK, 208, opacity)))
    local = ease_out((t - 25.4) / 0.85)
    title_width = tracked_width("CYCLES", FONTS["hero_small"], -2.5)
    group_width = title_width + 122
    start_x = (WIDTH - group_width) / 2
    orbital_mark(layer, (start_x + 42, 379), 31, GOLD, opacity * local, math.sin(t * 3.5) * 3)
    draw_glow_text(layer, (start_x + 104, 282), "CYCLES", FONTS["hero_small"], TEXT, opacity * local, -2.5, blur=20)
    draw_tracked(
        ImageDraw.Draw(layer),
        (WIDTH / 2, 522),
        "COMMAND THE PRESENT.",
        FONTS["subhead"],
        rgba(PAPER, 250, opacity * smooth((t - 26.15) / 0.65)),
        1.2,
        "center",
    )
    draw_tracked(
        ImageDraw.Draw(layer),
        (WIDTH / 2, 594),
        "BECOME PART OF THE PAST.",
        FONTS["subhead"],
        rgba(GOLD, 250, opacity * smooth((t - 26.65) / 0.65)),
        1.2,
        "center",
    )
    cta_alpha = opacity * smooth((t - 27.35) / 0.7)
    draw = ImageDraw.Draw(layer)
    draw.rounded_rectangle(
        (765, 722, 1155, 806),
        radius=8,
        fill=rgba(PAPER, 245, cta_alpha),
    )
    draw_tracked(draw, (960, 745), "ENTER THE BUILD", FONTS["label"], rgba(INK, 255, cta_alpha), 2.2, "center")
    draw_tracked(
        draw,
        (WIDTH / 2, 860),
        "PLAYABLE DEVELOPMENT BUILD",
        FONTS["label_small"],
        rgba(MUTED, 225, cta_alpha),
        3.0,
        "center",
    )
    orbital_mark(layer, (1321, 326), 25, GOLD, opacity * 0.36, math.sin(t * 2.2) * 4)
    draw_frame_corners(layer, opacity)
    frame.alpha_composite(layer)


class PromoRenderer:
    def __init__(
        self,
        gateway_path: Path,
        command_path: Path,
        galaxy_path: Path,
        sector_path: Path,
        battle_path: Path,
        legacy_path: Path,
    ) -> None:
        self.gateway = Image.open(gateway_path).convert("RGB")
        self.command = Image.open(command_path).convert("RGB")
        self.galaxy = Image.open(galaxy_path).convert("RGB")
        self.sector = Image.open(sector_path).convert("RGB")
        self.battle = Image.open(battle_path).convert("RGB")
        self.legacy = Image.open(legacy_path).convert("RGB")
        self.vignette = create_vignette()
        self.left_gradient = create_left_gradient()
        self.grid = create_grid()
        self.grain = create_grain_variants()

    @staticmethod
    def cinematic_crop(
        source: Image.Image,
        progress: float,
        start_zoom: float,
        end_zoom: float,
        direction: float,
    ) -> Image.Image:
        progress = smooth(progress)
        zoom = start_zoom + (end_zoom - start_zoom) * progress
        size = (max(WIDTH, int(WIDTH * zoom)), max(HEIGHT, int(HEIGHT * zoom)))
        fitted = ImageOps.fit(source, size, method=Image.Resampling.LANCZOS)
        spare_x = fitted.width - WIDTH
        spare_y = fitted.height - HEIGHT
        x_bias = clamp(0.5 + direction * (progress - 0.5) * 0.72)
        y_bias = clamp(0.5 - direction * (progress - 0.5) * 0.16)
        left = int(spare_x * x_bias)
        top = int(spare_y * y_bias)
        return fitted.crop((left, top, left + WIDTH, top + HEIGHT)).convert("RGBA")

    def composite_scene(
        self,
        frame: Image.Image,
        source: Image.Image,
        t: float,
        start: float,
        fade_in: float,
        fade_out_start: float,
        end: float,
        start_zoom: float,
        end_zoom: float,
        direction: float,
        shade: int,
    ) -> float:
        opacity = scene_opacity(t, start, fade_in, fade_out_start, end)
        if opacity <= 0:
            return 0.0
        progress = clamp((t - start) / max(end - start, 0.001))
        visual = self.cinematic_crop(source, progress, start_zoom, end_zoom, direction)
        if shade:
            visual.alpha_composite(Image.new("RGBA", visual.size, rgba(INK, shade)))
        visual.putalpha(visual.getchannel("A").point(lambda alpha: int(alpha * opacity)))
        frame.alpha_composite(visual)
        return opacity

    @staticmethod
    def scene_copy(
        frame: Image.Image,
        opacity: float,
        eyebrow: str,
        headline: tuple[str, ...],
        detail: str,
        badge: str,
        y: int = 116,
    ) -> None:
        if opacity <= 0:
            return
        layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(layer)
        draw.rounded_rectangle(
            (112, y - 20, 1000, y + 232 + 104 * (len(headline) - 1)),
            radius=18,
            fill=rgba(INK, 154, opacity),
            outline=rgba(TEXT, 30, opacity),
            width=1,
        )
        draw_tracked(draw, (148, y + 10), eyebrow, FONTS["label"], rgba(GOLD, 245, opacity), 3.5)
        text_y = y + 52
        for line in headline:
            draw_glow_text(
                layer,
                (144, text_y),
                line,
                FONTS["headline_small"],
                TEXT if line != headline[-1] else GOLD,
                opacity,
                -0.3,
                blur=14,
            )
            text_y += 100
        draw_tracked(draw, (148, text_y + 10), detail, FONTS["label_small"], rgba(MUTED, 230, opacity), 1.2)
        badge_width = tracked_width(badge, FONTS["label_small"], 1.3) + 44
        draw.rounded_rectangle(
            (WIDTH - badge_width - 58, 56, WIDTH - 58, 104),
            radius=24,
            fill=rgba(INK, 178, opacity),
            outline=rgba(GOLD if "CONCEPT" in badge else GREEN, 130, opacity),
            width=1,
        )
        draw_tracked(
            draw,
            (WIDTH - badge_width / 2 - 58, 69),
            badge,
            FONTS["label_small"],
            rgba(GOLD if "CONCEPT" in badge else GREEN, 245, opacity),
            1.3,
            "center",
        )
        frame.alpha_composite(layer)

    def scene_transit(self, frame: Image.Image, t: float) -> None:
        opacity = self.composite_scene(
            frame, self.gateway, t, 0.0, 0.22, 7.55, 8.15, 1.0, 1.115, 1.0, 38
        )
        if opacity <= 0:
            return
        title_opacity = opacity * scene_opacity(t, 0.0, 0.45, 2.75, 3.35)
        layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
        if title_opacity > 0:
            draw_tracked(
                ImageDraw.Draw(layer),
                (124, 240),
                "A PERSISTENT GALACTIC STRATEGY GAME",
                FONTS["label_small"],
                rgba(GOLD, 245, title_opacity),
                3.0,
            )
            draw_glow_text(layer, (116, 290), "CYCLES", FONTS["hero"], TEXT, title_opacity, -4.0, blur=24)
            draw_tracked(
                ImageDraw.Draw(layer),
                (124, 596),
                "COMMAND THE PRESENT.",
                FONTS["subhead"],
                rgba(PAPER, 250, title_opacity),
                1.2,
            )
        frame.alpha_composite(layer)
        movement_opacity = opacity * scene_opacity(t, 3.0, 0.5, 7.45, 8.05)
        self.scene_copy(
            frame,
            movement_opacity,
            "01 / MOVEMENT",
            ("CROSS REAL", "DISTANCE."),
            "Orders become routes, arrivals and exposure.",
            "CONCEPT DRAMATISATION",
            132,
        )

    def scene_command(self, frame: Image.Image, t: float) -> None:
        opacity = self.composite_scene(
            frame, self.command, t, 7.45, 0.5, 10.95, 11.55, 1.0, 1.045, -0.45, 46
        )
        self.scene_copy(
            frame,
            opacity,
            "02 / COMMAND",
            ("COMMIT YOUR", "INTENT."),
            "Set priorities. Issue orders. Advance the turn.",
            "CURRENT BUILD",
            108,
        )

    def scene_galaxy(self, frame: Image.Image, t: float) -> None:
        opacity = self.composite_scene(
            frame, self.galaxy, t, 10.95, 0.5, 14.45, 15.05, 1.0, 1.035, 0.35, 26
        )
        self.scene_copy(
            frame,
            opacity,
            "03 / GALAXY RANGE",
            ("READ THE", "WHOLE GALAXY."),
            "8 sectors  /  64 systems  /  91 live routes",
            "CURRENT BUILD",
            570,
        )

    def scene_sector(self, frame: Image.Image, t: float) -> None:
        opacity = self.composite_scene(
            frame, self.sector, t, 14.45, 0.5, 17.65, 18.25, 1.0, 1.105, -0.65, 52
        )
        self.scene_copy(
            frame,
            opacity,
            "04 / SECTOR RANGE",
            ("FIND THE", "CHOKEPOINT."),
            "Eight local systems. Two gateways. Every route matters.",
            "CURRENT BUILD · AUTHORED ATLAS",
            120,
        )

    def scene_battle(self, frame: Image.Image, t: float) -> None:
        opacity = self.composite_scene(
            frame, self.battle, t, 17.65, 0.52, 21.85, 22.45, 1.0, 1.12, 0.8, 30
        )
        self.scene_copy(
            frame,
            opacity,
            "05 / AUTHORITATIVE TICK",
            ("EVERY ORDER", "BECOMES A FACT."),
            "Movement  /  growth  /  combat  /  consequence",
            "CONCEPT DRAMATISATION",
            126,
        )

    def scene_legacy(self, frame: Image.Image, t: float) -> None:
        opacity = self.composite_scene(
            frame, self.legacy, t, 21.85, 0.52, 25.65, 26.25, 1.0, 1.1, -0.75, 34
        )
        self.scene_copy(
            frame,
            opacity,
            "06 / THE CHRONICLE",
            ("THE GALAXY RESETS.", "HISTORY REMAINS."),
            "The simulation writes the facts. The next Cycle inherits the record.",
            "CONCEPT DRAMATISATION",
            116,
        )

    def scene_final(self, frame: Image.Image, t: float) -> None:
        opacity = self.composite_scene(
            frame, self.legacy, t, 25.65, 0.55, DURATION, DURATION, 1.08, 1.14, 0.0, 188
        )
        if opacity <= 0:
            return
        layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
        local = ease_out((t - 25.75) / 0.7)
        draw_glow_text(layer, (WIDTH / 2, 226), "CYCLES", FONTS["hero_small"], TEXT, opacity * local, -2.5, "center", 22)
        draw_tracked(
            ImageDraw.Draw(layer),
            (WIDTH / 2, 470),
            "COMMAND THE PRESENT.",
            FONTS["subhead"],
            rgba(PAPER, 250, opacity * smooth((t - 26.35) / 0.55)),
            1.2,
            "center",
        )
        draw_tracked(
            ImageDraw.Draw(layer),
            (WIDTH / 2, 544),
            "BECOME PART OF THE PAST.",
            FONTS["subhead"],
            rgba(GOLD, 250, opacity * smooth((t - 26.75) / 0.55)),
            1.2,
            "center",
        )
        cta_alpha = opacity * smooth((t - 27.25) / 0.55)
        draw = ImageDraw.Draw(layer)
        draw.rounded_rectangle((765, 688, 1155, 772), radius=8, fill=rgba(PAPER, 245, cta_alpha))
        draw_tracked(draw, (960, 711), "ENTER THE BUILD", FONTS["label"], rgba(INK, 255, cta_alpha), 2.2, "center")
        draw_tracked(
            draw,
            (WIDTH / 2, 830),
            "PLAYABLE DEVELOPMENT BUILD",
            FONTS["label_small"],
            rgba(MUTED, 225, cta_alpha),
            3.0,
            "center",
        )
        draw_frame_corners(layer, opacity)
        frame.alpha_composite(layer)

    def render(self, t: float, frame_index: int) -> Image.Image:
        frame = Image.new("RGBA", (WIDTH, HEIGHT), rgba(INK))
        self.scene_transit(frame, t)
        self.scene_command(frame, t)
        self.scene_galaxy(frame, t)
        self.scene_sector(frame, t)
        self.scene_battle(frame, t)
        self.scene_legacy(frame, t)
        self.scene_final(frame, t)
        frame.alpha_composite(self.vignette)
        frame.alpha_composite(self.grain[frame_index % len(self.grain)])
        return frame.convert("RGB")


def add_tone(
    audio: np.ndarray,
    start: float,
    duration: float,
    frequency: float,
    amplitude: float,
    pan: float = 0.0,
    attack: float = 0.08,
    release: float = 0.2,
) -> None:
    first = int(start * SAMPLE_RATE)
    count = min(int(duration * SAMPLE_RATE), len(audio) - first)
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    envelope = np.ones(count, dtype=np.float64)
    attack_count = max(1, int(attack * SAMPLE_RATE))
    release_count = max(1, int(release * SAMPLE_RATE))
    envelope[: min(attack_count, count)] *= np.linspace(0, 1, min(attack_count, count)) ** 2
    envelope[-min(release_count, count) :] *= np.linspace(1, 0, min(release_count, count)) ** 2
    wave_data = np.sin(math.tau * frequency * time) * amplitude * envelope
    left = math.sqrt((1 - clamp((pan + 1) / 2)) * 2) / math.sqrt(2)
    right = math.sqrt(clamp((pan + 1) / 2) * 2) / math.sqrt(2)
    audio[first : first + count, 0] += wave_data * left
    audio[first : first + count, 1] += wave_data * right


def add_chirp(
    audio: np.ndarray,
    start: float,
    duration: float,
    low: float,
    high: float,
    amplitude: float,
    pan: float,
) -> None:
    first = int(start * SAMPLE_RATE)
    count = min(int(duration * SAMPLE_RATE), len(audio) - first)
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    rate = (high - low) / duration
    phase = math.tau * (low * time + 0.5 * rate * time * time)
    envelope = np.sin(np.linspace(0, math.pi, count)) ** 2
    wave_data = np.sin(phase) * amplitude * envelope
    left = math.sqrt((1 - clamp((pan + 1) / 2)) * 2) / math.sqrt(2)
    right = math.sqrt(clamp((pan + 1) / 2) * 2) / math.sqrt(2)
    audio[first : first + count, 0] += wave_data * left
    audio[first : first + count, 1] += wave_data * right


def add_whoosh(
    audio: np.ndarray,
    rng: np.random.Generator,
    start: float,
    duration: float,
    amplitude: float,
    pan_from: float,
    pan_to: float,
) -> None:
    first = int(start * SAMPLE_RATE)
    count = min(int(duration * SAMPLE_RATE), len(audio) - first)
    if count <= 0:
        return
    noise = rng.normal(0, 1, count)
    kernel = np.ones(41) / 41
    low = np.convolve(noise, kernel, mode="same")
    textured = noise * 0.28 + low * 2.4
    envelope = np.sin(np.linspace(0, math.pi, count)) ** 1.7
    textured *= envelope * amplitude
    pan = np.linspace(pan_from, pan_to, count)
    left = np.sqrt((1 - (pan + 1) / 2) * 2) / math.sqrt(2)
    right = np.sqrt(((pan + 1) / 2) * 2) / math.sqrt(2)
    audio[first : first + count, 0] += textured * left
    audio[first : first + count, 1] += textured * right


def add_impact(audio: np.ndarray, rng: np.random.Generator, start: float, amplitude: float) -> None:
    first = int(start * SAMPLE_RATE)
    count = min(int(1.35 * SAMPLE_RATE), len(audio) - first)
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    low = np.sin(math.tau * (58 * time - 11 * time * time)) * np.exp(-time * 4.2)
    sub = np.sin(math.tau * 37 * time) * np.exp(-time * 3.2)
    noise = rng.normal(0, 1, count)
    crack = noise * np.exp(-time * 18.0)
    signal = (low * 0.78 + sub * 0.44 + crack * 0.13) * amplitude
    audio[first : first + count, 0] += signal * 0.96
    audio[first : first + count, 1] += signal


def add_pad_chord(
    audio: np.ndarray,
    start: float,
    duration: float,
    frequencies: tuple[float, ...],
    amplitude: float,
) -> None:
    """Add a wide, slowly breathing analogue-style chord."""
    pans = np.linspace(-0.58, 0.58, len(frequencies))
    for index, (frequency, pan) in enumerate(zip(frequencies, pans, strict=True)):
        level = amplitude * (0.9 if index == 0 else 0.68)
        add_tone(audio, start, duration, frequency, level, float(pan), 0.52, 0.72)
        add_tone(audio, start, duration, frequency * 1.0045, level * 0.34, float(-pan), 0.62, 0.82)
        add_tone(audio, start, duration, frequency * 2.0, level * 0.11, float(pan * 0.65), 0.7, 0.9)


def add_pluck(
    audio: np.ndarray,
    start: float,
    frequency: float,
    amplitude: float,
    pan: float,
) -> None:
    first = int(start * SAMPLE_RATE)
    count = min(int(0.48 * SAMPLE_RATE), len(audio) - first)
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    envelope = (1 - np.exp(-time * 110.0)) * np.exp(-time * 8.6)
    shimmer = (
        np.sin(math.tau * frequency * time)
        + np.sin(math.tau * frequency * 2.005 * time + 0.6) * 0.31
        + np.sin(math.tau * frequency * 3.0 * time + 1.1) * 0.12
    )
    signal = shimmer * envelope * amplitude
    left = math.sqrt((1 - clamp((pan + 1) / 2)) * 2) / math.sqrt(2)
    right = math.sqrt(clamp((pan + 1) / 2) * 2) / math.sqrt(2)
    audio[first : first + count, 0] += signal * left
    audio[first : first + count, 1] += signal * right


def add_kick(audio: np.ndarray, start: float, amplitude: float) -> None:
    first = int(start * SAMPLE_RATE)
    count = min(int(0.52 * SAMPLE_RATE), len(audio) - first)
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    phase = math.tau * (49.0 * time + 34.0 * (1 - np.exp(-time * 24.0)))
    signal = np.sin(phase) * np.exp(-time * 10.5) * amplitude
    audio[first : first + count, 0] += signal * 0.97
    audio[first : first + count, 1] += signal


def add_hat(
    audio: np.ndarray,
    rng: np.random.Generator,
    start: float,
    amplitude: float,
    pan: float,
) -> None:
    first = int(start * SAMPLE_RATE)
    count = min(int(0.16 * SAMPLE_RATE), len(audio) - first)
    if count <= 0:
        return
    noise = rng.normal(0, 1, count)
    smoothed = np.convolve(noise, np.ones(17) / 17, mode="same")
    signal = (noise - smoothed) * np.exp(-np.arange(count) / SAMPLE_RATE * 32.0) * amplitude
    left = math.sqrt((1 - clamp((pan + 1) / 2)) * 2) / math.sqrt(2)
    right = math.sqrt(clamp((pan + 1) / 2) * 2) / math.sqrt(2)
    audio[first : first + count, 0] += signal * left
    audio[first : first + count, 1] += signal * right


def midi_frequency(note: int) -> float:
    return 440.0 * (2.0 ** ((note - 69) / 12.0))


def mix_mono(audio: np.ndarray, start: float, signal: np.ndarray, pan: float = 0.0) -> None:
    first = int(start * SAMPLE_RATE)
    count = min(len(signal), len(audio) - first)
    if count <= 0:
        return
    left = math.sqrt((1 - clamp((pan + 1) / 2)) * 2) / math.sqrt(2)
    right = math.sqrt(clamp((pan + 1) / 2) * 2) / math.sqrt(2)
    audio[first : first + count, 0] += signal[:count] * left
    audio[first : first + count, 1] += signal[:count] * right


def envelope(
    count: int,
    attack: float,
    decay: float,
    sustain: float,
    release: float,
) -> np.ndarray:
    result = np.full(count, sustain, dtype=np.float64)
    attack_count = min(count, max(1, int(attack * SAMPLE_RATE)))
    decay_count = min(max(0, count - attack_count), max(1, int(decay * SAMPLE_RATE)))
    release_count = min(count, max(1, int(release * SAMPLE_RATE)))
    result[:attack_count] = np.linspace(0, 1, attack_count) ** 1.6
    if decay_count:
        result[attack_count : attack_count + decay_count] = np.linspace(1, sustain, decay_count)
    result[-release_count:] *= np.linspace(1, 0, release_count) ** 1.4
    return result


def harmonic_wave(
    frequency: float,
    time: np.ndarray,
    harmonics: int,
    rolloff: float,
    vibrato_depth: float = 0.0,
    vibrato_rate: float = 5.1,
) -> np.ndarray:
    phase = math.tau * frequency * time
    if vibrato_depth:
        phase += vibrato_depth * np.sin(math.tau * vibrato_rate * time)
    result = np.zeros_like(time)
    normaliser = 0.0
    for harmonic in range(1, harmonics + 1):
        weight = 1.0 / (harmonic**rolloff)
        result += np.sin(phase * harmonic + harmonic * 0.07) * weight
        normaliser += weight
    return result / normaliser


def add_brass_note(
    audio: np.ndarray,
    start: float,
    duration: float,
    midi_note: int,
    amplitude: float,
    pan: float = 0.0,
) -> None:
    count = min(int(duration * SAMPLE_RATE), len(audio) - int(start * SAMPLE_RATE))
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    frequency = midi_frequency(midi_note)
    body = harmonic_wave(frequency, time, 10, 0.86, 0.09, 5.3)
    lower = harmonic_wave(frequency / 2, time, 7, 1.05, 0.04, 4.8) * 0.26
    signal = np.tanh((body + lower) * 1.3)
    signal *= envelope(count, 0.035, 0.15, 0.82, min(0.22, duration * 0.32)) * amplitude
    mix_mono(audio, start, signal, pan)


def add_string_staccato(
    audio: np.ndarray,
    start: float,
    duration: float,
    midi_note: int,
    amplitude: float,
    pan: float,
) -> None:
    count = min(int(duration * SAMPLE_RATE), len(audio) - int(start * SAMPLE_RATE))
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    tone = harmonic_wave(midi_frequency(midi_note), time, 7, 1.06, 0.035, 6.1)
    signal = tone * envelope(count, 0.009, 0.055, 0.42, min(0.1, duration * 0.4)) * amplitude
    mix_mono(audio, start, signal, pan)


def add_power_chord(
    audio: np.ndarray,
    start: float,
    duration: float,
    root_midi: int,
    amplitude: float,
) -> None:
    count = min(int(duration * SAMPLE_RATE), len(audio) - int(start * SAMPLE_RATE))
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    signal = np.zeros(count, dtype=np.float64)
    for note, level in ((root_midi, 1.0), (root_midi + 7, 0.78), (root_midi + 12, 0.52)):
        signal += harmonic_wave(midi_frequency(note), time, 9, 0.92) * level
    signal = np.tanh(signal * 1.72)
    signal *= envelope(count, 0.014, 0.08, 0.68, min(0.19, duration * 0.42)) * amplitude
    mix_mono(audio, start, signal, -0.08)
    mix_mono(audio, start + 0.009, signal * 0.74, 0.22)


def add_bass_note(
    audio: np.ndarray,
    start: float,
    duration: float,
    midi_note: int,
    amplitude: float,
) -> None:
    count = min(int(duration * SAMPLE_RATE), len(audio) - int(start * SAMPLE_RATE))
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    frequency = midi_frequency(midi_note)
    signal = (
        np.sin(math.tau * frequency * time) * 0.9
        + harmonic_wave(frequency, time, 5, 1.18) * 0.42
        + np.sin(math.tau * frequency / 2 * time) * 0.26
    )
    signal = np.tanh(signal * 1.45)
    signal *= envelope(count, 0.012, 0.07, 0.72, min(0.13, duration * 0.4)) * amplitude
    mix_mono(audio, start, signal, 0.0)


def add_snare(
    audio: np.ndarray,
    rng: np.random.Generator,
    start: float,
    amplitude: float,
) -> None:
    count = min(int(0.34 * SAMPLE_RATE), len(audio) - int(start * SAMPLE_RATE))
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    noise = rng.normal(0, 1, count)
    high = noise - np.convolve(noise, np.ones(23) / 23, mode="same")
    body = np.sin(math.tau * 188 * time) * np.exp(-time * 17.0)
    crack = high * np.exp(-time * 24.0)
    signal = (body * 0.42 + crack * 0.58) * amplitude
    mix_mono(audio, start, signal, -0.08)
    mix_mono(audio, start + 0.012, signal * 0.52, 0.22)


def add_taiko(
    audio: np.ndarray,
    rng: np.random.Generator,
    start: float,
    amplitude: float,
    pan: float = 0.0,
) -> None:
    count = min(int(1.25 * SAMPLE_RATE), len(audio) - int(start * SAMPLE_RATE))
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    phase = math.tau * (45.0 * time + 3.75 * (1 - np.exp(-time * 8.0)))
    skin = np.sin(phase) * np.exp(-time * 3.4)
    sub = np.sin(math.tau * 37.0 * time + 0.3) * np.exp(-time * 2.7)
    strike = rng.normal(0, 1, count) * np.exp(-time * 28.0)
    signal = (skin * 0.78 + sub * 0.42 + strike * 0.13) * amplitude
    mix_mono(audio, start, signal, pan)


def add_crash(
    audio: np.ndarray,
    rng: np.random.Generator,
    start: float,
    amplitude: float,
) -> None:
    count = min(int(2.0 * SAMPLE_RATE), len(audio) - int(start * SAMPLE_RATE))
    if count <= 0:
        return
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    noise = rng.normal(0, 1, count)
    high = noise - np.convolve(noise, np.ones(37) / 37, mode="same")
    shimmer = np.sin(math.tau * 6810 * time + 0.18 * np.sin(math.tau * 7.2 * time))
    signal = (high * 0.82 + shimmer * 0.18) * np.exp(-time * 1.75) * amplitude
    mix_mono(audio, start, signal, -0.18)
    mix_mono(audio, start + 0.017, signal * 0.76, 0.26)


def multitap_reverb(audio: np.ndarray, amount: float) -> np.ndarray:
    result = audio.copy()
    for delay, gain, crossfeed in ((0.075, 0.20, 0.08), (0.147, 0.15, 0.12), (0.263, 0.105, 0.18), (0.421, 0.07, 0.24)):
        offset = int(delay * SAMPLE_RATE)
        result[offset:, 0] += audio[:-offset, 0] * gain * amount + audio[:-offset, 1] * gain * amount * crossfeed
        result[offset:, 1] += audio[:-offset, 1] * gain * amount + audio[:-offset, 0] * gain * amount * crossfeed
    return result


def create_soundtrack(path: Path) -> None:
    rng = np.random.default_rng(71421)
    count = int(DURATION * SAMPLE_RATE)
    orchestra = np.zeros((count, 2), dtype=np.float64)
    rock = np.zeros((count, 2), dtype=np.float64)
    drums = np.zeros((count, 2), dtype=np.float64)
    effects = np.zeros((count, 2), dtype=np.float64)
    time = np.arange(count, dtype=np.float64) / SAMPLE_RATE

    # Fifteen exact bars at 120 BPM: heroic orchestral colour over a swaggering rock/mecha groove.
    breathing = 0.62 + 0.38 * np.sin(math.tau * 0.043 * time - 0.8) ** 2
    drone = (
        np.sin(math.tau * 41.20 * time) * 0.025
        + np.sin(math.tau * 61.74 * time + 0.7) * 0.015
        + np.sin(math.tau * 82.41 * time + 1.2) * 0.011
    ) * breathing
    effects[:, 0] += drone * 0.93
    effects[:, 1] += drone
    ambience = rng.normal(0, 1, count)
    window = 181
    cumulative = np.cumsum(np.insert(ambience, 0, 0.0))
    smoothed = (cumulative[window:] - cumulative[:-window]) / window
    ambience = np.pad(smoothed, (window // 2, count - len(smoothed) - window // 2), mode="edge") * 0.012
    effects[:, 0] += ambience
    effects[:, 1] += np.roll(ambience, 173)

    beat = 0.5
    bar = 2.0
    # root MIDI, chord MIDI. E Dorian keeps the heroic lift without borrowing any existing theme.
    progression = (
        (40, (52, 55, 59)),       # E pedal / Em
        (40, (48, 52, 55, 59)),   # Cmaj7 over E
        (40, (52, 55, 59, 66)),   # Em9
        (38, (55, 59, 62, 64)),   # G6 over D
        (37, (57, 61, 62, 64)),   # A(add4) over C-sharp
        (40, (52, 55, 59)),       # Em
        (36, (48, 52, 55, 59)),   # Cmaj7
        (40, (57, 64, 71)),       # A5 over E
        (40, (52, 55, 59)),       # Em(no3 colour arrives in motif)
        (40, (48, 52, 55, 59)),   # C over E
        (43, (52, 55, 59)),       # Em over G
        (36, (48, 52, 55, 59)),   # Cmaj9 colour
        (33, (45, 48, 52, 59)),   # Am(add9)
        (40, (52, 55, 59, 66)),   # Em(add9)
        (40, (52, 59, 66)),       # E5(add9) final
    )

    for bar_index, (root_midi, chord_midi) in enumerate(progression):
        start = bar_index * bar
        chord_frequencies = tuple(midi_frequency(note) for note in chord_midi)
        pad_level = 0.027 if bar_index < 2 else 0.038
        if 10 <= bar_index <= 12:
            pad_level = 0.045
        add_pad_chord(orchestra, start, bar + 0.32, chord_frequencies, pad_level)

        full_groove = 2 <= bar_index <= 9 or bar_index >= 13
        half_time = 10 <= bar_index <= 12
        if full_groove:
            bass_pattern = (root_midi, root_midi, root_midi + 12, root_midi + 7, root_midi, root_midi + 12, root_midi + 7, root_midi + 12)
            for step, note in enumerate(bass_pattern):
                add_bass_note(rock, start + step * 0.25, 0.235, note, 0.105 if bar_index < 8 else 0.12)
            chord_hits = ((0.0, 0.62), (0.75, 0.22), (1.0, 0.42), (1.5, 0.44), (1.82, 0.16))
            for offset, duration in chord_hits:
                add_power_chord(rock, start + offset, duration, chord_midi[0], 0.074 if bar_index < 8 else 0.088)

            string_steps = 16 if bar_index in (8, 9, 13) else 8
            step_length = bar / string_steps
            string_pattern = tuple(note + 12 for note in (chord_midi[0], chord_midi[-1], chord_midi[1], chord_midi[-1]))
            for step in range(string_steps):
                pan = -0.62 if step % 2 == 0 else 0.62
                add_string_staccato(
                    orchestra,
                    start + step * step_length,
                    step_length * 0.78,
                    string_pattern[step % len(string_pattern)],
                    0.049 if string_steps == 8 else 0.035,
                    pan,
                )

            kick_offsets = (0.0, 0.75, 1.0, 1.75)
            if bar_index in (8, 9, 13):
                kick_offsets = (0.0, 0.5, 0.75, 1.0, 1.25, 1.75)
            for offset in kick_offsets:
                add_kick(drums, start + offset, 0.145)
            for offset in (0.5, 1.5):
                add_snare(drums, rng, start + offset, 0.18)
            for step in range(8):
                add_hat(drums, rng, start + step * 0.25, 0.026 if step % 2 == 0 else 0.019, -0.4 if step % 2 == 0 else 0.4)

        elif half_time:
            add_bass_note(rock, start, 0.95, root_midi, 0.10)
            add_bass_note(rock, start + 1.0, 0.88, root_midi + 7, 0.085)
            add_power_chord(rock, start, 0.82, chord_midi[0], 0.065)
            add_power_chord(rock, start + 1.0, 0.72, chord_midi[0], 0.055)
            add_kick(drums, start, 0.15)
            add_kick(drums, start + 1.5, 0.11)
            add_snare(drums, rng, start + 1.0, 0.20)
            for step in range(4):
                add_hat(drums, rng, start + step * 0.5, 0.017, -0.28 if step % 2 == 0 else 0.28)

        if bar_index in (2, 4, 8, 10, 13, 14):
            add_taiko(drums, rng, start, 0.24 if bar_index not in (8, 13, 14) else 0.32)
            add_crash(drums, rng, start, 0.075 if bar_index < 8 else 0.10)

    # Original heroic two-bar motif. Its quartal leap and Dorian colour avoid familiar fanfare shapes.
    motif = (
        (0.0, 64, 0.42),
        (0.5, 69, 0.22),
        (0.75, 67, 0.22),
        (1.0, 66, 0.44),
        (1.5, 73, 0.44),
        (2.0, 71, 0.68),
        (2.75, 69, 0.22),
        (3.0, 66, 0.44),
        (3.5, 67, 0.22),
        (3.75, 64, 0.22),
    )
    add_brass_note(orchestra, 3.75, 0.22, 59, 0.10, -0.18)
    for motif_start, lift, level in ((4.0, 0, 0.13), (16.0, 0, 0.15), (26.0, 12, 0.16)):
        for offset, note, duration in motif:
            add_brass_note(orchestra, motif_start + offset, duration, note + lift, level, -0.22 + (offset / 4.0) * 0.44)
            if motif_start >= 16.0:
                add_brass_note(orchestra, motif_start + offset, duration, note + lift - 12, level * 0.48, 0.16)

    # Broad noble statement under the Chronicle before the final chorus.
    for offset, note, duration in ((0.0, 64, 0.85), (1.0, 67, 0.42), (1.5, 69, 0.42), (2.0, 71, 0.85), (3.0, 69, 0.42), (3.5, 66, 0.42)):
        add_brass_note(orchestra, 22.0 + offset, duration, note, 0.095, -0.12 if offset < 2 else 0.16)

    # Scene-synchronised industrial/orchestral punctuation.
    add_whoosh(effects, rng, 0.0, 1.45, 0.048, -0.9, 0.4)
    add_taiko(drums, rng, 0.32, 0.34)
    add_crash(drums, rng, 0.32, 0.085)
    add_taiko(drums, rng, 2.0, 0.18, -0.25)
    add_taiko(drums, rng, 3.0, 0.20, 0.25)
    add_whoosh(effects, rng, 3.1, 1.05, 0.07, -0.8, 0.8)
    add_chirp(effects, 3.45, 0.62, 260, 1260, 0.065, 0.15)
    add_taiko(drums, rng, 4.0, 0.38)
    add_crash(drums, rng, 4.0, 0.12)
    for when, pan in ((9.35, -0.32), (10.25, -0.45), (12.10, 0.0), (13.95, 0.45)):
        add_taiko(drums, rng, when, 0.23, pan)
        add_crash(drums, rng, when, 0.042)
    for when, pan in ((15.45, -0.5), (15.67, -0.2), (15.82, 0.15), (15.94, 0.5)):
        add_taiko(drums, rng, when, 0.17, pan)
    add_whoosh(effects, rng, 15.1, 1.0, 0.085, -1.0, 1.0)
    add_taiko(drums, rng, 16.0, 0.46)
    add_crash(drums, rng, 16.0, 0.15)
    for when, pan in ((17.05, -0.3), (17.75, 0.3)):
        add_taiko(drums, rng, when, 0.24, pan)
    add_whoosh(effects, rng, 18.0, 1.18, 0.095, -0.7, 0.7)
    add_chirp(effects, 18.05, 1.0, 180, 1480, 0.055, 0.0)
    add_taiko(drums, rng, 19.18, 0.62)
    add_crash(drums, rng, 19.18, 0.19)
    add_impact(effects, rng, 19.18, 0.50)
    add_taiko(drums, rng, 19.42, 0.25, 0.25)
    add_whoosh(effects, rng, 20.45, 0.62, 0.072, 0.55, -0.55)
    add_taiko(drums, rng, 20.95, 0.35)
    add_crash(drums, rng, 20.95, 0.10)
    add_taiko(drums, rng, 25.48, 0.44)
    add_crash(drums, rng, 25.48, 0.13)
    for when, pan in ((26.15, -0.32), (26.65, 0.28), (27.50, 0.0)):
        add_taiko(drums, rng, when, 0.26, pan)
    add_crash(drums, rng, 28.0, 0.15)

    # Resolve the exact fifteen-bar edit on a new downbeat instead of padding its tail.
    # The final E5(add9) impact carries the CTA through a complete, authored decay.
    add_pad_chord(
        orchestra,
        FINAL_HIT_TIME,
        1.9,
        tuple(midi_frequency(note) for note in (52, 59, 66, 71)),
        0.085,
    )
    for note, pan in ((52, -0.18), (59, 0.0), (66, 0.18)):
        add_brass_note(orchestra, FINAL_HIT_TIME, 1.55, note, 0.14, pan)
    add_bass_note(rock, FINAL_HIT_TIME, 1.45, 40, 0.16)
    add_power_chord(rock, FINAL_HIT_TIME, 1.35, 52, 0.12)
    add_taiko(drums, rng, FINAL_HIT_TIME, 0.48)
    add_crash(drums, rng, FINAL_HIT_TIME, 0.17)
    add_impact(effects, rng, FINAL_HIT_TIME, 0.44)

    orchestra = multitap_reverb(orchestra, 0.95)
    effects = multitap_reverb(effects, 0.58)

    # Mechanical pre-hit vacuums make the two largest slams feel physically bigger.
    duck = np.ones(count, dtype=np.float64)
    for start, end, floor in ((15.72, 16.0, 0.035), (19.03, 19.18, 0.025)):
        first = int(start * SAMPLE_RATE)
        last = int(end * SAMPLE_RATE)
        duck[first:last] = np.linspace(1.0, floor, last - first) ** 1.5
    orchestra *= duck[:, None]
    rock *= duck[:, None]
    drums *= duck[:, None]

    audio = orchestra * 0.96 + rock * 1.04 + drums * 1.06 + effects * 0.78
    fade_in = np.linspace(0, 1, int(0.08 * SAMPLE_RATE)) ** 2
    fade_out_start = int(AUDIO_FADE_START * SAMPLE_RATE)
    fade_out_end = count
    fade_out = np.linspace(1, 0, fade_out_end - fade_out_start) ** 2
    audio[: len(fade_in)] *= fade_in[:, None]
    audio[fade_out_start:fade_out_end] *= fade_out[:, None]
    audio[fade_out_end:] = 0
    audio = np.tanh(audio * 2.15)
    peak = np.max(np.abs(audio))
    if peak > 0:
        audio *= 0.94 / peak
    pcm = (audio * 32767).clip(-32768, 32767).astype("<i2")
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        wav.writeframes(pcm.tobytes())


def render_video(
    renderer: PromoRenderer,
    ffmpeg_path: Path,
    output_path: Path,
    audio_path: Path,
    poster_path: Path,
) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    command = [
        str(ffmpeg_path),
        "-hide_banner",
        "-loglevel",
        "warning",
        "-y",
        "-f",
        "rawvideo",
        "-pix_fmt",
        "rgb24",
        "-s:v",
        f"{WIDTH}x{HEIGHT}",
        "-r",
        str(FPS),
        "-i",
        "pipe:0",
        "-i",
        str(audio_path),
        "-map",
        "0:v:0",
        "-map",
        "1:a:0",
        "-frames:v",
        str(FRAME_COUNT),
        "-c:v",
        "libx264",
        "-preset",
        "medium",
        "-crf",
        "16",
        "-pix_fmt",
        "yuv420p",
        "-profile:v",
        "high",
        "-level",
        "4.1",
        "-color_primaries",
        "bt709",
        "-color_trc",
        "bt709",
        "-colorspace",
        "bt709",
        "-c:a",
        "aac",
        "-af",
        "loudnorm=I=-13.5:TP=-2.0:LRA=6",
        "-b:a",
        "256k",
        "-ar",
        str(SAMPLE_RATE),
        "-movflags",
        "+faststart",
        "-t",
        f"{DURATION:.3f}",
        str(output_path),
    ]
    process = subprocess.Popen(command, stdin=subprocess.PIPE)
    if process.stdin is None:
        raise RuntimeError("FFmpeg did not expose a stdin pipe.")
    try:
        for index in range(FRAME_COUNT):
            t = index / FPS
            frame = renderer.render(t, index)
            if index == int(27.8 * FPS):
                frame.save(poster_path, quality=95)
            process.stdin.write(frame.tobytes())
            if index % FPS == 0:
                print(f"Rendered {index // FPS:02d}/{int(DURATION):02d}s", flush=True)
        process.stdin.close()
        return_code = process.wait()
    except Exception:
        process.kill()
        raise
    if return_code != 0:
        raise RuntimeError(f"FFmpeg exited with code {return_code}.")


def transcode_web_video(ffmpeg_path: Path, master_path: Path, web_path: Path) -> None:
    web_path.parent.mkdir(parents=True, exist_ok=True)
    command = [
        str(ffmpeg_path),
        "-hide_banner",
        "-loglevel",
        "warning",
        "-y",
        "-i",
        str(master_path),
        "-map",
        "0:v:0",
        "-map",
        "0:a:0",
        "-map_metadata",
        "-1",
        "-frames:v",
        str(FRAME_COUNT),
        "-c:v",
        "libx264",
        "-preset",
        "medium",
        "-crf",
        "22",
        "-pix_fmt",
        "yuv420p",
        "-profile:v",
        "high",
        "-level",
        "4.1",
        "-color_primaries",
        "bt709",
        "-color_trc",
        "bt709",
        "-colorspace",
        "bt709",
        "-c:a",
        "aac",
        "-af",
        "afade=t=out:st=29.500:d=0.500",
        "-b:a",
        "128k",
        "-ar",
        str(SAMPLE_RATE),
        "-movflags",
        "+faststart",
        "-t",
        f"{DURATION:.3f}",
        str(web_path),
    ]
    subprocess.run(command, check=True)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gateway", type=Path, required=True)
    parser.add_argument("--command", type=Path, required=True)
    parser.add_argument("--galaxy", type=Path, required=True)
    parser.add_argument("--sector", type=Path, required=True)
    parser.add_argument("--battle", type=Path, required=True)
    parser.add_argument("--legacy", type=Path, required=True)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--web-out", type=Path, required=True)
    parser.add_argument("--poster", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    required_paths = (
        (args.gateway, "gateway concept frame"),
        (args.command, "command gameplay frame"),
        (args.galaxy, "galaxy gameplay frame"),
        (args.sector, "sector atlas frame"),
        (args.battle, "battle concept frame"),
        (args.legacy, "legacy concept frame"),
        (args.ffmpeg, "FFmpeg"),
    )
    for path, label in required_paths:
        if not path.exists():
            raise FileNotFoundError(f"Missing {label}: {path}")
    if args.out.resolve() == args.web_out.resolve():
        raise ValueError("--out and --web-out must identify different files.")
    renderer = PromoRenderer(
        args.gateway,
        args.command,
        args.galaxy,
        args.sector,
        args.battle,
        args.legacy,
    )
    audio_path = args.out.with_suffix(".render-audio.wav")
    print("Creating original soundtrack", flush=True)
    create_soundtrack(audio_path)
    print("Rendering 1080p master", flush=True)
    render_video(renderer, args.ffmpeg, args.out, audio_path, args.poster)
    audio_path.unlink(missing_ok=True)
    print("Encoding 1080p web derivative", flush=True)
    transcode_web_video(args.ffmpeg, args.out, args.web_out)
    print(f"Created {args.out}", flush=True)
    print(f"Created {args.web_out}", flush=True)
    print(f"Created {args.poster}", flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except BrokenPipeError:
        print("FFmpeg closed the render pipe unexpectedly.", file=sys.stderr)
        raise SystemExit(1)
