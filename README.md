![Discord badge](https://img.shields.io/discord/532365473602600965)
![Twitter handle](https://img.shields.io/twitter/follow/rive_app.svg?style=social&label=Follow)

# Rive Unity

![rive x unity image](https://github.com/rive-app/rive/assets/13705472/65130bf0-dff8-49cd-ae3a-9abe159c4b20)

A Unity runtime library for [Rive](https://rive.app). This is currently a **technical preview** for Mac and Windows installs of Unity. We're hoping to gather feedback about the API and feature-set as we expand platform support.

### Rendering Support

Currently supported platforms and backends include:

- [WebGL](WEBGL.md)
- Metal on Mac
- Metal on iOS
- D3D11 on Windows
- OpenGL on Windows
- OpenGL on Android

Planned support for:

- D3D12
- Vulkan

### Feature Support

The rive-unity runtime utilizes the latest Rive C++ runtime. All Rive features are supported for playback. Work is in progress to add runtime configuration support to the Unity package for Rive-specific features. For additional information, see here: https://help.rive.app/game-runtimes/unity#feature-support

## Table of contents

- ⭐️ [Rive Overview](#rive-overview)
- 🚀 [Getting Started](#getting-started)
- 👨‍💻 [Contributing](#contributing)
- ❓ [Issues](#issues)

## Rive Overview

[Rive](https://rive.app) is a real-time interactive design and animation tool that helps teams
create and run interactive animations anywhere. Designers and developers use our collaborative
editor to create motion graphics that respond to different states and user inputs. Our lightweight
open-source runtime libraries allow them to load their animations into apps, games, and websites.

🏡 [Homepage](https://rive.app/)

📘 [General help docs](https://help.rive.app/) · [Rive Unity docs](https://help.rive.app/game-runtimes/unity)

🛠 [Learning Rive](https://rive.app/learn-rive/)

## Getting Started

See the official examples repository to easily run a project locally: https://github.com/rive-app/rive-unity-examples

See the [Rive Unity docs](https://help.rive.app/game-runtimes/unity) for more information.

You will need a Unity editor that supports OpenGL or D3D11 for Windows, or a Mac with ARM64 (M1, M2, etc) architecture.

Select either D3D11/OpenGL for Windows, or Metal for Mac/iOS as the Graphics API under Project Settings -> Player in Unity.

You can install the Rive package for Unity by opening the Package Manager (Window -> Package Manager) and adding the [latest tag](https://github.com/rive-app/rive-unity/tags) as a git dependency:

```
git@github.com:rive-app/rive-unity.git?path=package#v0.1.69
```

Or through HTTP:

```
https://github.com/rive-app/rive-unity.git?path=package#v0.1.69
```

You can also add it manually to your projects `Packages/manifest.json` file:

```json
{
  "dependencies": {
    "app.rive.rive-unity": "git@github.com:rive-app/rive-unity.git?path=package#v0.1.69"
  }
}
```

### Awesome Rive

For even more examples and resources on using Rive at runtime or in other tools, checkout the [awesome-rive](https://github.com/rive-app/awesome-rive) repo.

## Contributing

We love contributions! Check out our [contributing docs](./CONTRIBUTING.md) to get more details into how to run this project, the examples, and more all locally.

## Issues

Have an issue with using the runtime, or want to suggest a feature/API to help make your development
life better? Log an issue in our [issues](https://github.com/rive-app/rive-unity/issues) tab! You
can also browse older issues and discussion threads there to see solutions that may have worked for
common problems.
