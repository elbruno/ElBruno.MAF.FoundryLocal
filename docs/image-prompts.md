# Image Generation Prompts (t2i)

This document records the exact prompts used for repository visual assets.

## 1) Repository icon prompt

```text
Square repository icon for a .NET 10 local AI adapter project. Minimal modern flat vector style. Color palette: deep blue, cyan, and white. Include subtle symbols suggesting AI chat, adapter bridge, and local/on-device inference. Clean geometric mark, no text, no watermark, transparent-friendly look, high contrast, professional open-source branding.
```

Attempted command:

```bash
t2i "Square repository icon for a .NET 10 local AI adapter project. Minimal modern flat vector style. Color palette: deep blue, cyan, and white. Include subtle symbols suggesting AI chat, adapter bridge, and local/on-device inference. Clean geometric mark, no text, no watermark, transparent-friendly look, high contrast, professional open-source branding." --out docs\images\repo-icon.png --width 1024 --height 1024
```

## 2) README main image prompt

```text
Wide hero image for README of a .NET 10 console project integrating Microsoft Agent Framework with Foundry Local through an IChatClient adapter. Show a clean developer workstation scene with abstract data flow: Console App -> Agent Framework -> IChatClient Adapter -> Foundry Local. Futuristic but readable, Microsoft-style professional palette (blue/cyan/neutral), no logos, no text overlays, no watermark, cinematic lighting, high detail.
```

Attempted command:

```bash
t2i "Wide hero image for README of a .NET 10 console project integrating Microsoft Agent Framework with Foundry Local through an IChatClient adapter. Show a clean developer workstation scene with abstract data flow: Console App -> Agent Framework -> IChatClient Adapter -> Foundry Local. Futuristic but readable, Microsoft-style professional palette (blue/cyan/neutral), no logos, no text overlays, no watermark, cinematic lighting, high detail." --out docs\images\readme-main.png --width 1792 --height 1024
```

## Current status

Generation is currently blocked because `t2i` has no configured provider credentials in this environment (missing endpoint/model/api key).  
Once configured, re-run the commands above to produce:

- `docs\images\repo-icon.png`
- `docs\images\readme-main.png`
