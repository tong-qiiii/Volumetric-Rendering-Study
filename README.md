# Volumetric Rendering Study

A computer graphics course project for real-time volumetric clouds in Unity 6000.6.0f1, URP, and HLSL.

## Milestones

| Week | Implementation |
| --- | --- |
| W6 | Ray–box intersection in object space, midpoint integration over the visible interval, Beer–Lambert transmittance, premultiplied composition |
| W9 | Perlin FBM base density, Worley edge erosion, height and coverage controls, animated local wind |
| W12 | Henyey–Greenstein directional scattering, a short secondary march for cloud self-shadowing, and opaque scene depth clipping |

## Running the demo

The current close-range showcase is **`Assets/Scenes/MistForest.unity`**. Enter Play mode to see the drifting forest mist, moss path, ancient trees, lanterns, and ruined gate. This is an original stylized forest inspired by the atmosphere of Breath of the Wild; the meshes and textures are generated within this project. The previous open valley remains available as `CloudValley.unity`.

[Forest screenshot](Docs/MistForest/01-forest-final.png) · [Mist on/off](Docs/MistForest/07-fog-comparison.png) · [Rewritten midterm report](Docs/MistForest/MidtermReport.md) · [PDF report](Docs/MistForest/实时体积云渲染中期报告.pdf)

Select **Forest volume - select material to tune fog** to adjust the `Forest mist` material. **Forest Height Fog** selects a continuous near-ground density field with exponential height falloff, Perlin pockets and Worley detail. The entrance has a fixed, thinner spatial envelope. **Receive Tree Shadows** samples the main-light shadow atlas along the view ray; **Sky Fill Strength** controls the upward ambient contribution. Unity's built-in global fog is disabled. Lantern point lights illuminate surfaces, but are not yet included in the volume scattering calculation.

Use **Cloud Study > Capture Mist Forest Comparisons** to render the saved forest in an isolated preview scene at 1280 × 720, with wind frozen. It writes the fog, tree-shadow, sky-fill and depth-clipping comparisons to `Docs/MistForest`. The diagnostic depth keyword is enabled only on a temporary material. **Cloud Study > Build Mist Forest** regenerates the procedural assets and forest scene; save scene edits first, and note that rebuilding replaces generated scene content. The baked scene itself does not require the editor generator to run.

The original minimal algorithm test is still available:

Open `Assets/Scenes/MainScene.unity` and enter Play mode. Select the `FogVolume` cube and edit its material in the Inspector. The shader expects a centered unit cube mesh and supports scaling through the cube transform. Both included URP quality assets enable Depth Texture. If using a different camera or URP asset, keep Depth Texture enabled.

- Set **Coverage** to 0 to hide the cloud. Increase it to admit more of the base noise field. Keep **Cloud Top** above **Cloud Base** to control the vertical envelope.
- Compare **Edge Erosion** at 0 and 0.45 with **Noise Speed** set to 0. Restore the speed and change **Local Wind Direction** to see the field move.
- Rotate **Directional Light** to compare front and back lighting. Compare **Light Steps** at 0 and 4 to isolate cloud self-shadowing. Set **Scattering Anisotropy** to 0 for an isotropic phase function.
- With the camera and cloud at their saved transforms, move **Sphere** along Z between 0, 3, and 6 to inspect opaque geometry in front of, inside, and behind the cloud. The cloud clips its ray interval at the opaque surface.

The light march samples the base cloud density, omitting Worley erosion on that secondary ray. The view march still samples the detailed density. Scene depth comes from opaque geometry. The W12 milestone does not include GPU timing or quality benchmarks.

## CloudValley weather map

Open `Assets/Scenes/CloudValley.unity` to see three cloud groups. The `ValleyClouds` material uses `ValleyWeatherMap.png` as linear data in the volume's local XZ plane. Red controls local coverage, green the normalized cloud top, and blue the normalized cloud base within the Cloud Base–Cloud Top range. Black coverage leaves the sky clear. The weather map fixes the cloud group layout while wind moves the 3D noise inside the groups.

Disable **Use Weather Map** to restore the procedural cloud layout, or set **Edge Erosion** to 0 to inspect the weather map silhouette without Worley detail.

[Original](Docs/CloudValleyWeather/01-original.png) · [Weather map](Docs/CloudValleyWeather/02-weather-map.png) · [Weather map with erosion](Docs/CloudValleyWeather/03-weather-map-erosion.png)

## CloudValley lighting

`ValleyClouds` blends a forward and a broader backward Henyey–Greenstein lobe. **Secondary Phase Weight** controls the blend. **Sky Fill Color** and **Sky Fill Strength** control the cool ambient contribution. The shader estimates sky occlusion with three density samples in the world-up direction and keeps the existing sun trace for direct lighting.

The camera, cloud geometry, and wind are fixed for each before/after pair. [Front light](Docs/CloudValleyLighting/01-front-comparison.png) · [Side light](Docs/CloudValleyLighting/02-side-comparison.png) · [Back light](Docs/CloudValleyLighting/03-back-comparison.png) · [Sky fill on/off](Docs/CloudValleyLighting/04-sky-fill-isolation.png)

The refined preset uses stronger direct-light attenuation and cooler sky fill. [Front close-up](Docs/CloudValleyLighting/Refined/01-front-refined.png) · [Side close-up](Docs/CloudValleyLighting/Refined/02-side-refined.png) · [Back close-up](Docs/CloudValleyLighting/Refined/03-back-refined.png) · [Sky fill on/off](Docs/CloudValleyLighting/Refined/04-back-sky-fill.png)

## Next milestone

W14 will compare GPU time and visual quality across view step lengths, light step counts, and render resolutions.
