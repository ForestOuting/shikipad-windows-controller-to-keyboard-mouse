# ShikiPad Terminal Startup Designs

These are terminal-only layouts for Windows Terminal, CMD, and PowerShell. They are designed for dark backgrounds and ANSI 24-bit color.

Canonical palette order:

- Spring mint: `#5EFFC5`
- Summer aqua: `#5BE2FF`
- Solar gold bridge: `#FFD75C`
- Autumn ember: `#FF9452`
- Winter frost: `#EBFAFF`

The ShikiPad logo uses this left-to-right order, matching the season rail while placing the gold bridge slightly right of center. The live implementation uses filled block glyphs with a right/down extruded shadow rather than a hollow outline, plus embossed shadows on the surrounding terminal text and symbols.

## Final Recommended Version

```text
✦── SHIKIPAD BOOT SEQUENCE ───────────────────────────────────────────────✦
╭──────────────────────────────────────────────────────────────────────────╮
│      ✿ Spring          ◇ Summer          ◈ Autumn          ❄ Winter      │
╰──────────────────────────────────────────────────────────────────────────╯
   ⋆           (◕‿◕)            ✿             ◇          (｡･ω･｡)       ✧
                           CONTROL SURFACE READY
✧ ·˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙··˙· ✧
       _____ _     _ _    _ _____          _
      / ____| |   (_) |  (_)  __ \        | |
     | (___ | |__  _| | ___| |__) |_ _  __| |
      \___ \| '_ \| | |/ / |  ___/ _` |/ _` |
      ____) | | | | |   <| | |  | (_| | (_| |
     |_____/|_| |_|_|_|\_\_|_|   \__,_|\__,_|
✧ ·⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆··⋆· ✧
                  ▣ seasonal input mapper  ▸  physical keys / mouse / touch charge
   ✦              ✿           (˘ω˘)           ◈             ❄        (☆▽☆)
╭──────────────────────────────────────────────────────────────────────────╮
│                             ◇ SYSTEM STATUS ◇                            │
│┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄│
│▸ Controller awake  | READY       ┆  ▸ Keyboard and mouse ready  | READY  │
│  Season cycle                  ┆ Spring / Summer / Autumn / Winter       │
│  Input safety                  ┆ Releases held inputs when app closes    │
╰──────────────────────────────────────────────────────────────────────────╯
─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅
      ✿  Spring memory   ◇  Summer signal   ◈  Autumn keylight   ❄  Winter layer online
╭─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅╮
│                                ◆ Live session                           │
╰─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅─⋅╯
```

## ANSI Color Version

Use `\x1b[38;2;R;G;Bm` for each color and reset with `\x1b[0m`. The implemented program applies gradients per character, but this static sample shows the palette.

```text
\x1b[38;2;91;246;255m✦── SHIKIPAD BOOT SEQUENCE ──\x1b[38;2;255;109;191m────────────────────\x1b[38;2;255;224;112m✦\x1b[0m
\x1b[38;2;61;78;91m╭──────────────────────────────────────────────────────────────────────────╮\x1b[0m
\x1b[38;2;61;78;91m│\x1b[38;2;115;255;190m      ✿ Spring\x1b[38;2;255;218;113m          ◇ Summer\x1b[38;2;255;153;90m          ◈ Autumn\x1b[38;2;246;251;255m          ❄ Winter      \x1b[38;2;61;78;91m│\x1b[0m
\x1b[38;2;61;78;91m╰──────────────────────────────────────────────────────────────────────────╯\x1b[0m
\x1b[38;2;206;225;232m                           CONTROL SURFACE READY\x1b[0m
\x1b[38;2;91;246;255m       _____ _     _ _    _ _____          _\x1b[0m
\x1b[38;2;83;183;255m      / ____| |   (_) |  (_)  __ \        | |\x1b[0m
\x1b[38;2;174;120;255m     | (___ | |__  _| | ___| |__) |_ _  __| |\x1b[0m
\x1b[38;2;255;109;191m      \___ \| '_ \| | |/ / |  ___/ _` |/ _` |\x1b[0m
\x1b[38;2;255;169;85m      ____) | | | | |   <| | |  | (_| | (_| |\x1b[0m
\x1b[38;2;255;255;255m     |_____/|_| |_|_|_|\_\_|_|   \__,_|\__,_|\x1b[0m
```

## Program String Form

```csharp
string[] logo = {
    @"     _____ _     _ _    _ _____          _     ",
    @"    / ____| |   (_) |  (_)  __ \        | |    ",
    @"   | (___ | |__  _| | ___| |__) |_ _  __| |    ",
    @"    \___ \| '_ \| | |/ / |  ___/ _` |/ _` |    ",
    @"    ____) | | | | |   <| | |  | (_| | (_| |    ",
    @"   |_____/|_| |_|_|_|\_\_|_|   \__,_|\__,_|    "
};
```

## Variant A: Dream Starfield

```text
✦── SHIKIPAD STARRY WAKE ───────────────────────────────────────────────✦
╭──────────────────────────────────────────────────────────────────────╮
│   ✿ Spring        ◇ Summer        ◈ Autumn        ❄ Winter           │
╰──────────────────────────────────────────────────────────────────────╯
        ⋆       ✧        (◕‿◕)        ˚        ✦        (˘ω˘)
                         CONTROL SURFACE READY
      _____ _     _ _    _ _____          _
     / ____| |   (_) |  (_)  __ \        | |
    | (___ | |__  _| | ___| |__) |_ _  __| |
     \___ \| '_ \| | |/ / |  ___/ _` |/ _` |
     ____) | | | | |   <| | |  | (_| | (_| |
    |_____/|_| |_|_|_|\_\_|_|   \__,_|\__,_|
╭────────────────────────── SYSTEM STATUS ─────────────────────────────╮
│ Controller awake          | READY                                     │
│ Keyboard and mouse ready  | READY                                     │
│ Season cycle              | Spring / Summer / Autumn / Winter         │
│ Input safety              | Releases held inputs when app closes      │
╰──────────────────────────────────────────────────────────────────────╯
◆ Live session
```

## Variant B: Cute Pixel

```text
▣ SHIKIPAD PIXEL BOOT ▣
┌──────────────────────────────────────────────────────────────────────┐
│ [Spring ✿]    [Summer ◇]    [Autumn ◈]    [Winter ❄]                 │
└──────────────────────────────────────────────────────────────────────┘
  +--+      (｡･ω･｡)        *        (☆▽☆)        +--+
      _____ _     _ _    _ _____          _
     / ____| |   (_) |  (_)  __ \        | |
    | (___ | |__  _| | ___| |__) |_ _  __| |
     \___ \| '_ \| | |/ / |  ___/ _` |/ _` |
     ____) | | | | |   <| | |  | (_| | (_| |
    |_____/|_| |_|_|_|\_\_|_|   \__,_|\__,_|
┌─ STATUS ─────────────────────────────────────────────────────────────┐
│ Controller awake        : READY                                      │
│ Keyboard and mouse ready: READY                                      │
│ Season cycle            : Spring / Summer / Autumn / Winter          │
│ Input safety            : Releases held inputs when app closes       │
└──────────────────────────────────────────────────────────────────────┘
◆ Live session
```

## Variant C: Cyber Neon

```text
◇═ SHIKIPAD NEON LINK ═════════════════════════════════════════════════◇
╭──────────────────────────────────────────────────────────────────────╮
│ SPRING ✿  ┆  SUMMER ◇  ┆  AUTUMN ◈  ┆  WINTER ❄                     │
╰──────────────────────────────────────────────────────────────────────╯
               CONTROL SURFACE READY  // INPUT BUS ONLINE
      _____ _     _ _    _ _____          _
     / ____| |   (_) |  (_)  __ \        | |
    | (___ | |__  _| | ___| |__) |_ _  __| |
     \___ \| '_ \| | |/ / |  ___/ _` |/ _` |
     ____) | | | | |   <| | |  | (_| | (_| |
    |_____/|_| |_|_|_|\_\_|_|   \__,_|\__,_|
╭─ SIGNAL MATRIX ──────────────────────────────────────────────────────╮
│ Controller awake          ┆ READY                                     │
│ Keyboard and mouse ready  ┆ READY                                     │
│ Season cycle              ┆ Spring / Summer / Autumn / Winter         │
│ Input safety              ┆ Releases held inputs when app closes      │
╰──────────────────────────────────────────────────────────────────────╯
◆ Live session
```
