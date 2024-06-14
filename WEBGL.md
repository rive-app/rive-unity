# Rive Unity WebGL

Rive's WebGL Renderer has two modes:

## PLS

Uses [an extension](https://registry.khronos.org/webgl/extensions/WEBGL_shader_pixel_local_storage/) we helped pioneer and implement into modern browsers. If the user has this enabled we'll prioritize using this extension for best performance and quality. Enable [WebGL Draft Extensions](https://www.wikihow.tech/Enable-WebGL-Draft-Extensions-in-Google-Chrome) to use this today!

This is our best-in-class solution for both quality and performance, supporting all Rive features including advanced blend modes.

## MSAA

A fallback that will work on all modern browsers supporting WebGL 2. Anti-aliasing quality and playback performance won't be quite as high as PLS but will still be very good.

All Rive features are supported, however performance will be impacted when using advanced blend modes.
