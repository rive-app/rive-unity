# Rive Unity WebGL

Rive's WebGL Renderer has two modes:

## PLS

Uses [an extension](https://registry.khronos.org/webgl/extensions/WEBGL_shader_pixel_local_storage/) we helped pioneer and implement into modern browsers. If the user has this enabled we'll prioritize using this extension for best performance and quality. Enable [WebGL Draft Extensions](https://www.wikihow.tech/Enable-WebGL-Draft-Extensions-in-Google-Chrome) to use this today!

This is our best-in-class solution for both quality and performance, supporting all Rive features including advanced blend modes.

## MSAA

A fallback that will work on all modern browsers supporting WebGL 2. Anti-aliasing quality and playback performance won't be quite as high as PLS but will still be very good.

All Rive features are supported, however performance will be impacted when using advanced blend modes.

# ⚠️ Patching Emscripten

Rive's shaders use features Unity's WebGL shader pre-processor doesn't handle. We provide a patch that must be applied once to your local Unity install in order for it to bypass the shader preprocessor when loading Rive shaders.

## Locate Unity's Emscripten

Unity's emscripten installation is based on the location of the installed Unity Engine. For example, on Mac version 2022.3.10f1 will be located here:

```
/Applications/Unity/Hub/Editor/2022.3.10f1/PlaybackEngines/WebGLSupport/BuildTools/Emscripten
```

## Patching library_c_processor.js

Apply the patch to library_c_processor.js

```
patch -u -b /Applications/Unity/Hub/Editor/2022.3.10f1/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/emscripten/src/library_c_preprocessor.js -i ./rive_unity_webgl_patch.diff
```

## Manually patching library_c_processor.js

The patch may not be compatible with your version of Unity, in this case you can manually make the change.

- Find library_c_processor.js in the paths provided above and open it in your code/text editor.

- Find where preprocess_c_code is defined: `$preprocess_c_code: function(code, defs = {}) {`

- Make it early out if it detects the shader is a Rive shader (it'll include GL_ANGLE_shader_pixel_local_storage) by returning the un-altered shader source:

```
  $preprocess_c_code: function(code, defs = {}) {
    if(code.indexOf('GL_ANGLE_shader_pixel_local_storage') != -1) {
      return code;
    }
    // ...
```
