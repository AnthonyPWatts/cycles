# Cycles Promo Film Production Notes

## Current Master

`cycles-promo-30s.mp4` is the current public-site film. It is a 30-second, 1920×1080, 30 fps H.264/AAC master with 48 kHz stereo audio. `cycles-promo-poster.jpg` is taken from the final title card.

The film labels its two kinds of imagery on screen:

- **Current build** identifies captured dashboard UI and the current authored atlas.
- **Concept dramatisation** identifies generated cinematic imagery used to express gateway transit, battle, and continuity between Cycles.

Concept frames establish intended tone and scale. They are not screenshots, simulation output, or a promise of an implemented cinematic battle renderer.

## Picture Sources

| Sequence | Source | Treatment |
| --- | --- | --- |
| Gateway transit | `promo/concept-gateway-transit.png` | Generated concept dramatisation with trailer typography and camera movement. |
| Command | `promo/gameplay-command-guide.png` | Current local build captured at 1600×900 against a fresh canonical Day One SQL seed. |
| Galaxy | `promo/gameplay-galaxy.png` | Current local build showing the authored 8-sector, 64-system chart and live route overlay. |
| Sector | `../assets/galaxy/sector-aster-reach.png` | Current route-free Aster Reach atlas; the film identifies it as current-build authored art. |
| Treaty Gate battle | `promo/concept-treaty-gate-battle.png` | Generated concept dramatisation. |
| Cycle legacy | `promo/concept-cycle-legacy.png` | Generated concept dramatisation. |

The generated concept frames were created with OpenAI's built-in image generation using these prompts:

1. **Gateway transit:** Use the supplied Cycles fleet and galaxy-atlas images only as visual-language references. Create a cinematic 16:9 trailer keyframe for a stylised concept dramatisation: an Aurelian fleet of dark, elegant warships crossing a luminous two-tick inter-sector gateway between a blue-violet sector and an amber sector. The gateway should read as a vast spatial corridor, with teal engine light, restrained gold navigation geometry, and deep star fields. Match Cycles' dark baroque science-fiction tone and clean strategic composition. No user interface, text, captions, logos, watermark, or recognisable franchise designs.
2. **Treaty Gate battle:** Create a cinematic 16:9 trailer keyframe for a stylised concept dramatisation of the Battle of Treaty Gate. Two disciplined fleets converge around a monumental ancient gateway: Aurelian ships carry teal engine light and restrained gold accents; the opposing Khepri force carries muted crimson signals. Show one decisive gold-white impact and several smaller exchanges while preserving readable fleet silhouettes and strategic geography. Use a dark baroque science-fiction tone with blue-violet space, amber energy, controlled scale, and no gore. No user interface, text, captions, logos, watermark, or recognisable franchise designs.
3. **Cycle legacy:** Create a cinematic 16:9 trailer keyframe for a stylised concept dramatisation of the end of a Cycle. A monumental galactic archive stands in darkness: the old galaxy dissolves into gold particulate on the left, a new eight-sector blue-violet atlas forms on the right, and a preserved battle memory glows within the central archive. Convey continuity, consequence, and history surviving a reset. Use the same dark baroque science-fiction visual language, restrained gold and teal light, clear cinematic depth, and no characters in close-up. No user interface, text, captions, logos, watermark, or recognisable franchise designs.

## Edit And Audio

The structure is: premise and movement; current Command intent; current Galaxy and Sector geography; concept battle; concept legacy; final call to action. Crossfades briefly overlap those chapters, so current-build and concept labels remain attached to their respective shots.

`tools/render_cycles_promo.py` creates every frame, the original score, the sound design, the encoded master, and the poster. No third-party audio is used in the current film.

Render with the repository's Python dependencies and any FFmpeg 7-compatible executable:

```powershell
python tools\render_cycles_promo.py `
  --gateway src\Cycles.Api\wwwroot\media\promo\concept-gateway-transit.png `
  --command src\Cycles.Api\wwwroot\media\promo\gameplay-command-guide.png `
  --galaxy src\Cycles.Api\wwwroot\media\promo\gameplay-galaxy.png `
  --sector src\Cycles.Api\wwwroot\assets\galaxy\sector-aster-reach.png `
  --battle src\Cycles.Api\wwwroot\media\promo\concept-treaty-gate-battle.png `
  --legacy src\Cycles.Api\wwwroot\media\promo\concept-cycle-legacy.png `
  --ffmpeg C:\path\to\ffmpeg.exe `
  --out src\Cycles.Api\wwwroot\media\cycles-promo-30s.mp4 `
  --poster src\Cycles.Api\wwwroot\media\cycles-promo-poster.jpg
```

Verify the encoded duration, dimensions, frame count, audio format, full decode, final-title luminance, and audio decay:

```powershell
python tools\verify_cycles_promo.py --ffmpeg C:\path\to\ffmpeg.exe
```
