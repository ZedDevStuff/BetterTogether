# Installation

## For regular .NET applications

When using the library in a regular .NET application, you can install the library via NuGet. The package is available on the NuGet gallery as [**`BetterTogether`**](https://www.nuget.org/packages/BetterTogether/).

```bash
dotnet add package BetterTogether
```

## For Unity applications

When using the library in a Unity application, you can install the library via 2 different methods.

### Method 1: Using the Unity Package Manager

Use this git url `https://github.com/ZedDevStuff/BetterTogether.git#unity` in the Unity Package Manager

!!! warning
    This might not work on older Unity versions since this url contains a branch, which wasn't always supported.

### Method 2: Using NuGetForUnity

This is my recommended way of installing the library in Unity. It's a bit more work to setup, but it's worth it in the long run if you're planning on using more libraries in your project or need to update a single dependency of the library.

1. Install [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity?tab=readme-ov-file#how-do-i-install-nugetforunity) in your project.

2. Open the NuGetForUnity window in Unity. (Window -> NuGet -> Manage NuGet Packages)

3. Search for the BetterTogether package and install it.
