# VTT
*Dream. Create. Inspire.*
## Introduction

**VTT is a multiplayer environment for you and your friends to host and play tabletop roleplaying games. Create magnificent 3D environments or more traditional 2D ones.**

![3d](https://i.postimg.cc/KcscCkLx/highlight-3d.jpg)

**Fully functional shadows with many light sources all casting them at the same time, in real time. Make your players' dungeon experience a completely new one**

[![highlight-shadows.jpg](https://i.postimg.cc/HWP53Lk7/highlight-shadows.jpg)](https://postimg.cc/V5ndkmMY)

**Create worlds at any scale, from small conflict maps to large overworld ones. No restrictions. No limits.**

[![highlight-world.jpg](https://i.postimg.cc/Jn3800YY/highlight-world.jpg)](https://postimg.cc/G8pVSLCv)

**An advanced chat system supporting cryptographically secure dice rolls in various combinations, complex expressions, animated images and much more!**

[![highlight-chat.jpg](https://i.postimg.cc/C5PxMdzT/highlight-chat.jpg)](https://postimg.cc/JHXLPrNP)

**And much, much more, hundreds of features intertwined together to let you create the worlds of your imagination and truly wow your players!**

---

## Feature List
* Displays 3d models or 2d sprites in complex environments of any scale.
* 3D and 2D square grids with customizable grid sizes and grid snapping, extending to infinity.
* A sky system, with the sun positioned according to pitch/yaw controls.
* Fully dynamic real-time 3d shadows for both the sun and up to 16 light sources on the screen (though the scenes themselves support any amounts of light sources)
* A very rich property editor for both objects and maps allows for fine control over any value or property to create truly any environment imaginable. Many of the finer controls have tooltips, explaining exactly what a given property controls in great detail.
* A dynamic fog of war system for both 3D and 2D environments, with all the scalability and fine controls you'd ever need.
* Easy to setup multiplayer - just click host, enter the desired port and let your players connect.
* An advanced distance measuring system that allows you to measure lines, circles, spheres, squares, cubes and cones, highlighting which objects fall into the measured shape, with the ability to leave those measurements in the scene with custom tooltips.
* A fully fledged turn order tracker, reminiscent of those in turn-based games, with portraits and highlights.
* Full controls for fine object positioning, rotating and scaling, similar to those in professional 3d game engines such as unity.
* A powerful particle system editor which allows for visual creation of complex particle systems which evolve according to complex rules, all setup with simple interface controls.
* Thousands of icons for status effects, with a built-in search bar.
* Custom health/mana/armour/anything bars for objects, with automatic value calculation for mathematical inputs for ease of use.
* Journals for storing arbitrary text information.
* A rich chat system that uses cryptography for random dice rolls, allows up to 10 million rolls (in a single chat message) that are all rolled and delivered within milliseconds, that supports advanced templates (such as attacks, spells, fancy dice rolls and more) and images (including animated gifs)
* An advanced asset system that supports .glb models and image (.png, .jpeg, .tiff, .gif and similar) sprites. If the required libraries are present on the uploader's side, supports animated images (.webm) too!
* Good performance - both the networking and rendering parts of the application offer many optimizations and are built for older hardware, allowing the rendering of very complex scenes with millions upon millions of triangles, dynamic shadows, particles and high-resolution textures, that get delivered to the clients within seconds, all that with a consistently high framerate.

---

## Download and Installation
You can download the application in the releases section. VTT is still in its beta stage and has bugs that are regularly patched, so be sure to check for new releases frequently.

An automatic updater companion app is built-in. When it detects a new version being released it will notify you in the main menu. Updating is as simple as pressing the "Update" button there.

**Windows only, but since the application is built with .net core and uses opengl 3.3 it should be relatively simple to run on linux.**


### Requires .NET 6, msvcr 120 and msvcr 140.
There is no installation process, simply unpack the application into any directory and run the VTT.exe executable.

---

## Troubleshooting
- **Application doesn't start-up**: Make sure that you have the required components installed and they match the architecture of your os.
- **Your players can't connect**: Make sure that they are using the right ip address and port, and you have port-forwarded the port you are using for the application.
- **Application crashes**: Whenever VTT encounters an unrecoverable problem it will shutdown, generating a crash report file in the main application directory. Please submit a bug report in the issues section here, including this file and other relevant information (such as log files).

---

## GLB 3D models and importing them
You can import any 3d model that is a .glb _embedded_ file format. However to display them properly a few things must be present in the model structure itself:
* The model MUST be an embedded glTF 2.0 binary. All textures must be contained within the same file as the model itself.
* At least 1 camera must be present in the scene, for preview and portrait generation. If you don't have a camera present your application will crash.
* At least 1 light should be present, otherwise the preview and portrait will fail to properly render with lighting and will be pitch black.
* The model _must use the Z axis as the upwards axis_. Many model editors will by default export .glb models with Y axis as the upwards one. Make sure you specify Z as upwards.
* Multiple material slots per object are not supported. Please separate your objects into distinct ones, with 1 material per object.
* The albedo texture must be present for all materials, even if it is a 1x1 white square. Other textures are optional.

### Some additional tips on 3d models
* If your modelling software allows, it is recommended to export tangents and bitangents with the model, as automatically generated ones may differ from those of the software and cause rendering differences.
* While animations are not currently supported the support is planned and they will simply be ignored, so you can export animated glb models.
* If multiple cameras are used the first one encountered in the file structure will be used as the preview one. If you want a custom camera for the portrait (turn tracker, inspect menu, etc) you can name the camera object node exactly **portrait_camera**.
* VTT uses raycasting for object picking in the editor. If your model is very complex it is recommended to simplify the mesh for the raycasting process. You can name a mesh object node exactly **simplified_raycast**, and if such node is encountered it will not be drawn, but will be used for raycasting purposes.
* It is recommended that you don't export translated, rotated or scaled nodes. Please apply the transformations to the mesh instead, and make the node's origin at 0,0,0. While node transformations are rendered correctly they are not applied when calculating the bounding box for raycasting purposes and when raycasting for performance reasons. Camera and light nodes are exceptions and are allowed transformations.
* All point and directional lights will be exported with the model, and this is in fact how you create lights in your scenes. However due to rendering differences (notably the lack of reflections) the lights may appear dimmer in your modelling editor than they are in VTT.
* It is recommended that you include all required textures with the material (albedo, metallic, roughness, normal and ambient), and that their dimensions match.

---

## Custom Shaders
VTT now supports custom material shaders with nodegraph editor!
[![shadergraph.jpg](https://i.postimg.cc/nL3HgRC6/shadergraph.jpg)](https://postimg.cc/kVt3tyNc)

A custom shader allows you to finely control the output parameters for your material and make them as dynamic as you want.
If you are familiar with nodegraph editors then this system should be intuitive.

Hold left click and move the cursor to move the graph. Hold left click over a header of a node to move that specific node. Press the X in the node header to delete it and all its connections. Right-click anywhere that isn't a node to add a new one.
To connect inputs and outputs hold left click on either and drag the line to the connection you want to make. Inputs can only have one connection to them, but a single output can connect to different inputs.
If the input is not connected you can manualy edit the values of said input, making it a constant.

The main node is the PBR Output. It can't be deleted and there is only one per material. It has the following inputs:

* Albedo - the color of the pixel, before light calculations.
* Normal - the normal vector of the pixel, used for calculating reflections and specular highlights.
* Emission - the additive color of the pixel **after** light calculation.
* Alpha - the transparency of the pixel.
* Ambient Occlusion - the multiplier to ambient lighting for the pixel.
* Metallic - a value indicating whether a given pixel is metal or not. Metals have different reflective properties to non-metals.
* Roughness - a surface roughness indicator for a given pixel, controling specular highlight.

You can read more about PBR [here](https://substance3d.adobe.com/tutorials/courses/the-pbr-guide-part-1) (though this guide is very technical).

### Node In/Out colors, and on conversions
Certain inputs expect a certain value - a pixel albedo value is a combination of 3 float values, each controling the Red, Green and Blue channels. To help distinguish different required values VTT uses colors.

* <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQI12NIO7vqPwAGVwLdh0OFRAAAAABJRU5ErkJggg==" width="12"> A boolean value
* <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQI12Ow2Vz4HwAE7wJgOnTs/wAAAABJRU5ErkJggg==" width="12"> An integer value
* <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQI12Ng+DXrPwAFJgKU8sMxDAAAAABJRU5ErkJggg==" width="12"> An unsigned integer value
* <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQI12MwOmv0HwAElwIx0ewwSgAAAABJRU5ErkJggg==" width="12"> A float (number with a decimal point) value
* <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQI12P48KznPwAIjgNiBMwsygAAAABJRU5ErkJggg==" width="12"> A 2D vector (2 floats)
* <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQI12P4f53hPwAHhQLWFrY87gAAAABJRU5ErkJggg==" width="12"> A 3D vector (3 floats)
* <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQI12O4tVThPwAGmwKfYQj+rwAAAABJRU5ErkJggg==" width="12"> A 4D vector (4 floats)

If a wrong output type is connected to the input it will automatically convert. Note that some data may be lost (a 4d vector converting to a 3d one drops the 4th component entirely). This will also show a warning at the bottom-left of the node graph editor.

### On performance
Custom shaders are by their nature not friendly to performance. They will require the application to switch between shaders on the fly, which is costly. If UBOs are enabled in the setting (on by default) the switch is much cheaper than with them disabled. Try to avoid having many different custom shaders in a given scene.

---

## Setting up animated sprites
VTT supports webm animated sprites for 2D images (and technically 3D model textures). However before such sprite may be used 3rd party libraries must be installed.
* Download full [FFmpeg](https://ffmpeg.org/) binaries. The ones that include both the .exe and .dll files are required (specifically, the following files must be present:)
   * avcodec-**.dll
   * avdevice-**.dll
   * avfilter-*.dll
   * avformat-**.dll
   * avutil-**.dll
   * postproc-**.dll
   * swresample-*.dll
   * swscale-*.dll
   * ffmpeg.exe
   * ffplay.exe
   * ffprobe.exe
* Navigate to VTT data directory. On windows it is SystemDrive/Users/UserName/AppData/Local/VTT. Note that VTT must have been started at least once for the directory to exist.
* Navigate to the Client folder in the Data directory.
* Navigate to the FFmpeg folder in the Client directory. If the folder doesn't exist - create it with the exact name.
* Move all required FFmpeg files listed above into the directory.

After you've done this start up VTT. It should now be able to handle .webm files as animated sprites.
* Please note, that parsing all frames of a video file (which .webm is), baking them onto a single texture and uploading that to the GPU takes quite a while. Animated textures take a considerable amount of time to upload and download, and may take upwards of minutes to do so.
* Only the uploader (server host) needs FFmpeg installed for animated textures to work. VTT will convert the video file into an image sequence that other clients will download and use.
* Please make sure your video file uses a reasonable resolution, length and framerate. VTT for performance reasons will use a single texture for the entire animation, and if there is too much data, all extra frames will be lost. The maximum texture size is limited by the GPU, and is typically 32k x 32k pixels at most on the hardware VTT is built for. If you import a 4k 166fps hour long video there is only so much data that can be packed onto that texture.

---
## Manual file manipulation
In case it is necessary all VTT data can be found at SystemDrive/Users/UserName/AppData/Local/VTT and is stored in one of several formats:
.json is editable text data.
.ab is raw asset binary.
.ued is a custom packed data format.
.png is an image (asset preview)

You can edit, move and delete files. An asset file can't be loaded if either .ab or .json are not present, however it is recommended that you delete both if you want to delete either. Please note that .ab is not necessarily the .glb or .png that you've uploaded, and your modelling software may be unable to open them.

Never edit these files while the application is running.

Please do not edit the map fog of war .png files manually. They may be stored as .png but they don't contain easily editable data for any image editing software.

---
## VSCC Integration
VTT supports partial VSCC integration.

Start the "Roll20" server in VSCC, then in VTT press the "Connect VSCC" button in the escape menu. If it glows green, then the connection is established and you may use VSCC integration as you would with Roll20.

The following options are not implemented:
* Custom scripts that send raw R20 messages, such as the initiative script.
* The "Poll Roll Result" macro action

---
## Known issues, suggestions and missing features tracker
You can see all the currently known issues in the issues section on github.
These are the features currently missing that are planned to be implemented:
- [x] Correct bounding box + raycast position calculation for rotated .glb nodes
- [x] More graceful bad data rejection instead of shutting down
- [ ] Automated reconnect attempts with a finer timeout control for clients
- [ ] A non-debug interface for connected client information + ability to ban clients.
- [ ] Allow for multiple administrators and observers
- [ ] Improvements on shadow maps, maybe cascade shadow maps implementation
- [ ] Path curves and path following for particles
- [ ] Animations for 3D models
- [ ] Automatic decimation for shadow meshes
- [ ] In app asset deletion
- [ ] Better asset management interface
- [ ] Sounds (in app and assets)
- [x] Custom material shaders
- [ ] Moving away from imgui to a custom interface library

If you have any suggestions on what you would like to see implemented, please leave them in the issues tracker here.
### The following features will probably never be implemented:
- **Voice chat client**: There are enough dedicated VC clients like Discord that have already perfected VC. A dedicated VC would take a lot of effort to develop and maintain, while adding very little to the overall application.
- **Webcam feed**: Similar to the VC it would take a lot of effort, reduce the portability of the app, and significantly load the network for relatively little gain.
- **Fully-fledged document editor**: While journals are intended to store text data, they are not intended to be a fully-fledged document editor. There are great online document editors such as google drive that are intended for this exact purpose.
- **Character sheets**: VTT is intended to be a general-purpose application, that can be used for virtually any system. Character sheets are usually very specific to their system (dnd, pathfinder, vtm, etc) and introducing them would break the general-purpose nature of VTT.
- **Dedicated servers**: VTT is currently self-hosted by users who press the Host button. It runs a dedicated server in the background to which all (including the host) connect. While VTT is able to run in a host-less environment (with the -server console parameter) setting up and maintaining dedicated servers is too costly for me at the moment. In addition, VTT was designed with the concept of being self-hosted. At most the dedicated server could serve as a hub for players to see each other's games and connect to them.

---
## Building from sources
Simply clone the repo, open it with Visual Studio and build. Nuget is used to fetch all dependencies and the application is self-contained.
In case you don't wish to use Visual Studio the following nuget packages are used:
* CoreCLR-NCalc
* FFmpeg.AutoGen
* glTF2Loader
* ImGui .NET
* NetCoreServer
* Newtonsoft.Json
* OpenTK
* SixLabours.ImageSharp

Everything in the Embed folder is a manifest resource of the resulting VTT.dll

---
## Command Line Arguments
The following command line arguments are available (do not include the square brackets):
```
-debug [true/false]: Enables/disables debug mode information and hooks without the debugger attached.

-server [port]: Launches VTT without the graphical shell, terminal only, and starts a server on the specified port.

-quick [true/false]: Launches VTT, hosts a server on the default port and connects to that server, bypassing the main menu.

-connect [ip:port]: Launches VTT and tries to connect to the address specified, bypassing the main menu.

-loglevel [Off/Debug/Info/Warn/Error/Fatal]: Specifies the logging level for the internal logger.

-gldebug [true/false]: Launches the app with the OpenGL Debug flag set and sets up the necessary hooks.

-timeout [number][postfix]: Sets the timeout bound for both the client and the server to a specified value. If there was no client/server communication over this period of time the connection closes. Number is any positive number. Postfix indicates the rank, and can be ms(millisecond), s(seconds), m(minutes) or h(hours)

-nocache [true]: If this argument is encountered the server will startup with the asset caching system disabled, regardless of the -servercache parameter.

-servercache [number][postfix]: Sets the maximum server cache buffer before it is trimmed. Number is any positive number. Postfix indicates the rank, and can be b(bytes), kb(kilobytes), mb(megabytes) or gb(gigabytes)
```