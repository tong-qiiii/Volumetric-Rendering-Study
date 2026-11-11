# Volumetric Rendering Study

A computer graphics course project for real-time volumetric clouds in Unity 6000.6.0f1, URP, and HLSL.

## Milestones

| Week | Implementation |
| --- | --- |
| W6 | Ray–box intersection in object space, midpoint integration over the visible interval, Beer–Lambert transmittance, premultiplied composition |
| W9 | Perlin FBM base density, Worley edge erosion, height and coverage controls, animated local wind |
| W12 | Henyey–Greenstein directional scattering, a short secondary march for cloud self-shadowing, and opaque scene depth clipping |

## Running the demo

Open `Assets/Scenes/MainScene.unity` and enter Play mode. Select the `FogVolume` cube and edit its material in the Inspector. The shader expects a centered unit cube mesh and supports scaling through the cube transform. Both included URP quality assets enable Depth Texture. If using a different camera or URP asset, keep Depth Texture enabled.

- Set **Coverage** to 0 to hide the cloud. Increase it to admit more of the base noise field. Keep **Cloud Top** above **Cloud Base** to control the vertical envelope.
- Compare **Edge Erosion** at 0 and 0.45 with **Noise Speed** set to 0. Restore the speed and change **Local Wind Direction** to see the field move.
- Rotate **Directional Light** to compare front and back lighting. Compare **Light Steps** at 0 and 4 to isolate cloud self-shadowing. Set **Scattering Anisotropy** to 0 for an isotropic phase function.
- With the camera and cloud at their saved transforms, move **Sphere** along Z between 0, 3, and 6 to inspect opaque geometry in front of, inside, and behind the cloud. The cloud clips its ray interval at the opaque surface.

The light march samples the base cloud density, omitting Worley erosion on that secondary ray. The view march still samples the detailed density. Scene depth comes from opaque geometry. The W12 milestone does not include GPU timing or quality benchmarks.

## Next milestone

W14 will compare GPU time and visual quality across view step lengths, light step counts, and render resolutions.
