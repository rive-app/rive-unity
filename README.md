![Discord badge](https://img.shields.io/discord/532365473602600965)
![Twitter handle](https://img.shields.io/twitter/follow/rive_app.svg?style=social&label=Follow)

# Rive Unity

![Rive hero image](https://cdn.rive.app/rive_logo_dark_bg.png)

The Unity runtime for [Rive](https://rive.app). It uses the [Rive Renderer](https://rive.app/renderer) and stays up to date with the latest Rive C++ runtime. Install it from the [Unity Asset Store](https://assetstore.unity.com/packages/tools/gui/rive-350858) or from this repo.

## Unity version support

The package supports Unity LTS versions from 2021 upwards (including Unity 6).

### Rendering support

- [WebGL](WEBGL.md)
- Metal on Mac
- Metal on iOS
- D3D11 on Windows
- D3D12 on Windows
- OpenGL on Windows
- OpenGL on Android
- Vulkan on Windows
- Vulkan on Android
- Vulkan on Ubuntu 24.04+ (x86_64)

Planned support for:

- Consoles

### Feature support

See [Feature Support](https://rive.app/docs/feature-support) for which Rive features each runtime supports, including Unity. The Unity docs have pages for [Data Binding](https://rive.app/docs/game-runtimes/unity/data-binding), [State Machines](https://rive.app/docs/game-runtimes/unity/state-machines), [Listeners](https://rive.app/docs/game-runtimes/unity/listeners), [Layouts](https://rive.app/docs/game-runtimes/unity/layouts), [Loading Assets](https://rive.app/docs/game-runtimes/unity/loading-assets), [Audio](https://rive.app/docs/game-runtimes/unity/audio), [Procedural Rendering](https://rive.app/docs/game-runtimes/unity/procedural-rendering), [Runtime Asset Swapping](https://rive.app/docs/game-runtimes/unity/runtime-asset-swapping), and, for older files, [Inputs](https://rive.app/docs/game-runtimes/unity/inputs), [Text](https://rive.app/docs/game-runtimes/unity/text), and [Rive Events](https://rive.app/docs/game-runtimes/unity/rive-events).

## Table of contents

- ⭐️ [Rive Overview](#rive-overview)
- 🤖 [Build with your agent](#build-with-your-agent)
- 🚀 [Getting Started](#getting-started)
- 👨‍💻 [Contributing](#contributing)
- ❓ [Issues](#issues)

## Rive overview

[Rive](https://rive.app) is an agent-native 2D and 3D engine for games, apps, and interactive UI. Use it as your whole engine, or add it to the products you already have. Build with your coding agent through the [Rive CLI](https://rive.app/agents), or draw, rig, and animate by hand with the professional design and animation tools in the [Rive Editor](https://rive.app/editor). Both work on the same files. Open-source runtimes, no royalties or runtime fees. Rive files (`.riv`) play natively on web, mobile, desktop, game engines, consoles, smart TVs, and cars.

🏡 [Homepage](https://rive.app/)

📘 [Rive docs](https://rive.app/docs/) · [Rive Unity docs](https://rive.app/docs/game-runtimes/unity/unity)

🛠 [Learn Rive](https://rive.app/docs/tutorials/learn-rive)

## Build with your agent

Paste this into Claude Code, Codex, Cursor, or any agent that runs commands on your computer:

```text
Install the Rive CLI from rive.app/cli. Build a game menu and HUD as a Rive file and add them to my Unity project with the Rive runtime.
```

Your agent installs the [Rive CLI](https://rive.app/agents), writes the scene, and opens a preview window that rebuilds every time it saves. Keep giving it notes, or open the same file in the [Rive Editor](https://editor.rive.app/) and shape it by hand. Trying it needs no account. Writing the `.riv` that ships in your game needs a free Rive account. Free exports play a short Rive splash screen; [paid plans](https://rive.app/pricing) export without it. See [Getting started with the Rive CLI](https://rive.app/docs/cli/getting-started).

## Getting started

See [Getting Started](https://rive.app/docs/game-runtimes/unity/getting-started) in the Rive Unity docs. To try Rive in Unity quickly, run one of the [example projects](https://github.com/rive-app/rive-unity-examples).

Install the package from the [Unity Asset Store](https://assetstore.unity.com/packages/tools/gui/rive-350858) (stable releases), or from GitHub to also get canary tags with [Early Access features](https://rive.app/docs/feature-support).

From GitHub, open **Window -> Package Manager**, choose **Add package from git URL...**, and paste this URL (replace `0.0.0` with the [latest release](https://github.com/rive-app/rive-unity/releases)):

```
https://github.com/rive-app/rive-unity.git?path=package#v0.0.0
```

Or add it to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "app.rive.rive-unity": "https://github.com/rive-app/rive-unity.git?path=package#v0.0.0"
  }
}
```

Unity can crash when it upgrades the package while the Editor is running. If that happens, close Unity, update the version in `Packages/manifest.json`, then reopen the project.

Once you have a `.riv` file, drag it into the Unity **Project** window. Unity imports it as a **Rive Asset** you can reference from components and scripts.

### Awesome Rive

For even more examples and resources on using Rive at runtime or in other tools, checkout the [awesome-rive](https://github.com/rive-app/awesome-rive) repo.

## Contributing

We love contributions! Check out our [contributing docs](./CONTRIBUTING.md) to get more details into how to run this project, the examples, and more all locally.

## Issues

Have an issue with using the runtime, or want to suggest a feature/API to help make your development
life better? Log an issue in our [issues](https://github.com/rive-app/rive-unity/issues) tab! You
can also browse older issues and discussion threads there to see solutions that may have worked for
common problems.

For crashes, attach the **Editor.log** from right after the crash. The [Unity docs](https://docs.unity3d.com/Manual/LogFiles.html) say where to find it.
