# Asset Credits

## Characters (Assets/StreamingAssets/Characters/)
Source repo for VRoid/Orion: https://github.com/madjin/vrm-samples (mirror of VRoid sample models; its README states the VRoid beta samples are CC0; each file's embedded VRM meta says licenseName "CC0", all usage flags Allow).
| File | Model | Author | License | Attribution |
|---|---|---|---|---|
| hair_male.vrm | HairSample_Male (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| shino.vrm | Sendagaya_Shino (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| vita.vrm | Vita (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| vivi.vrm | Vivi (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| victoria.vrm | Victoria_Rubin (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| hair_female.vrm | HairSample_Female (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| darkness_shibu.vrm | Darkness_Shibu (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| sakurada.vrm | Sakurada Fumiriya (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| shibu.vrm | Sendagaya Shibu (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |

## Звуки (Assets/_Tobe/Resources/Audio/)
Записи скачал владелец проекта (Rutatata) 04.10.2026 с Pixabay Sound Effects / Freesound, фильтр CC0 (свободное использование, указание автора не требуется):
whistle.wav, crowd_loop.wav, cheer.wav, spike.mp3, bump.wav, set.mp3, net.wav, floor.mp3, squeak.mp3.
Игра при загрузке сама срезает тишину в начале, оставляет у коротких звуков только первый удар и склеивает петлю трибун (AudioManager).

click.ogg: Kenney Starter Kit City Builder, sounds/toggle.ogg (https://github.com/KenneyNL/Starter-Kit-City-Builder), MIT License, Copyright (c) Kenney (https://kenney.nl).
ui_whoosh.wav: сгенерирован процедурно (numpy) самим проектом, CC0.

Модели orion и seedsan (роботы) убраны из игры 05.10.2026, вместо них шесть аниме-моделей VRoid с лицензией CC0 из того же репозитория.

## Motion capture (Assets/StreamingAssets/Motions/)
"The data used in this project was obtained from mocap.cs.cmu.edu. The database was created with funding from NSF EIA-0196217."
CMU Graphics Lab Motion Capture Database, terms: "free for all uses" (research and commercial). BVH conversion by Bruce Hahne (cgspeed.com, 2010 Motionbuilder-friendly release; READMEFIRST.txt confirms free use incl. commercial), obtained from the GitHub mirror https://github.com/una-dinosauria/cmu-mocap.
Processing: trimmed, heading normalised to +Z, decimated 120 -> 30 fps, finger joints removed, loops cut at best-matching frame with a short end crossfade. Units: 1 BVH unit = 0.0564 m (CMU 0.45 inch-scale).
| Clip | Source (CMU subject_trial) | CMU description |
|---|---|---|
| idle | 77_02 | standing |
| ready | 77_03 | ready stance |
| walk | 35_01 | walk |
| run | 35_18 | run/jog |
| sprint | 16_45 | run/jog |
| sidestep | 113_17 | walk sideways |
| jump_vertical, land | 16_03 | high jump |
| jump_approach | 124_06 | basketball lay up |
| jump_block | 124_05 | basketball jump shot |
| jump_run | 127_21 | run jump stop run |
| dive | 127_23 | run dive over roll run |
| celebrate | 142_09 | joy |
| sad | 79_71 | sad |
| throw_overhead | 124_01 | baseball pitch |
| high_five | 20_11 | high-five, walk (subject A) |
Rejected: Bandai Namco motion dataset (CC BY-NC-ND).
