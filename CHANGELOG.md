# Changelog

[2.0.0] - 2022-11-27
- Renamed SobelOutlineFeature, removing references to now unsued Sobel technique
- Fixed visual artifacts (glancing angle issues) in Outline renderer
- Removed unused DepthNormalsFeature

[1.2.0] - 2022-11-25
- Reworked outline feature to use a combination of depth and normals

[1.1.0] - 2022-11-22
- Added editor script to disable Renderer Features during light bakes, avoiding a bug that causes reflection probes to be black when using custom blit features

[1.0.0] - 2022-11-02
- This is the first release of Renderer Features
