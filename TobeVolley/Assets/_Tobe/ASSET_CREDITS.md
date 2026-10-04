# Asset Credits

## Characters (Assets/StreamingAssets/Characters/)
Source repo for VRoid/Orion: https://github.com/madjin/vrm-samples (mirror of VRoid sample models; its README states the VRoid beta samples are CC0; each file's embedded VRM meta says licenseName "CC0", all usage flags Allow).
| File | Model | Author | License | Attribution |
|---|---|---|---|---|
| hair_male.vrm | HairSample_Male (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| sakurada.vrm | Sakurada Fumiriya (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| shibu.vrm | Sendagaya Shibu (vroid/beta/) | pixiv Inc. / VRoid Project | CC0 1.0 (embedded meta) | none required |
| orion.vrm | Orion (Avatar_Orion.vrm) | Polygonal Mind (www.PolygonalMind.com) | CC0 1.0 (embedded meta) | none required |
| seedsan.vrm | Seed-san | VirtualCast, Inc. | VRM Public License 1.0 (https://vrm.dev/licenses/1.0/); redistribution + modification allowed, credit required | "Seed-san model by VirtualCast, Inc." (https://github.com/vrm-c/vrm-specification/tree/master/samples/Seed-san) |

## Sound effects (Assets/_Tobe/Resources/Audio/)
Kenney Starter Kits, MIT License, Copyright (c) Kenney (https://kenney.nl). Credit optional but appreciated: "Sounds by Kenney (kenney.nl)". MIT notice must accompany copies.
| File | Original | URL |
|---|---|---|
| spike.ogg | audio/impact.ogg | https://github.com/KenneyNL/Starter-Kit-Racing |
| bump.ogg | sounds/placement-b.ogg | https://github.com/KenneyNL/Starter-Kit-City-Builder |
| click.ogg | sounds/toggle.ogg | https://github.com/KenneyNL/Starter-Kit-City-Builder |
| set.ogg | sounds/tile-swap.ogg | https://github.com/KenneyNL/Starter-Kit-Match-3 |
| floor.ogg | sounds/tile-land.ogg | https://github.com/KenneyNL/Starter-Kit-Match-3 |
| net.ogg | sounds/enemy_hurt.ogg | https://github.com/KenneyNL/Starter-Kit-FPS |

These are generic placeholder-quality picks (not volleyball-specific); replace when better sfx are available.

## Generated (original, released CC0 by the project)
whistle.wav, cheer.wav, crowd_loop.wav: synthesized procedurally (sine trill / filtered noise). Placeholders; no attribution needed.


## Generated additions (original, released CC0 by the project)
squeak.wav, drum.wav, ui_whoosh.wav, crowd_gasp.wav: synthesized procedurally (numpy). Placeholders; no attribution needed.

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
