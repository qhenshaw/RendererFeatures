# Renderer Features
[![Unity 2021.3+](https://img.shields.io/badge/unity-2020.1%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](LICENSE.md)

Renderer Features is a collection of common post-processing effects added as URP Renderer Feature scripts/shaders.

## System Requirements
Unity 2021.3+. Will likely work on earlier versions but this is the version I tested with.  
`Requires URP - DOES NOT support built-in/HDRP.`

## Installation
Use the Package Manager and use Add package from git URL, using the following: 
```
https://github.com/qhenshaw/RendererFeatures.git
```

## Usage
Install the package and add any of the included renderer features to your Universal Renderer Data asset.
Included features:
- Sobel Outline (depth based outlines)
- Kawase Blur (screen blur, can be passed into texture target for use in shaders)
- Depth Fog
- Sharpen (increases edge contrast)
- God Rays (only works with a single light)
- Draw Fullscreen
  - This feature allows shaders created via Shader Graph or HLSL to be rendered as custom post-processing
  - Shader must have a _MainTex texture input to receive screen blit
  - Feature can output directly to camera or to texture target for use in shaders
