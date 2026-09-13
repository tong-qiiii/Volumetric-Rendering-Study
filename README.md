# Volumetric Rendering Study

A real-time volumetric fog and cloud rendering study implemented with Unity URP and HLSL.

## Current Progress

- Camera ray generation
- Ray marching inside a bounded volume
- Beer-Lambert transmittance
- Spatial density field
- Procedural 3D value noise
- FBM density field
- Animated volumetric noise

## Tech Stack

- Unity URP
- HLSL / ShaderLab
- Git

## Current Implementation

The current prototype casts a camera ray through a bounded volume and performs ray marching inside the volume.

At each sample point, the shader evaluates a procedural 3D density field constructed from FBM noise. Beer-Lambert transmittance is accumulated along the viewing ray to produce the final volumetric opacity.

## Next Steps

- Directional sunlight
- Single scattering
- Self-shadowing
- Scene depth composition
- Sampling quality / GPU performance experiments
