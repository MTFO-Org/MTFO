# MTFO - Move The F*** Over
*Formally Opticom by Rohan*
## Description

This plugin for LSPDFR on GTA V is designed to enhance the traffic control and behavior when you are responding to a call with your lights and siren active. MTFO aims to create a more realistic and immersive experience by making the AI drivers react in a more intelligent and 'human-like' way to your presence.

All credit goes to **Rohan** for the original "Opticom" functionality, which has been integrated and adapted into this plugin.

## Features

MTFO is a complete overhaul of AI behavior while sirens are active:

* **Opticom Intersection Control**: As you approach intersections with traffic lights, this feature will automatically turn the light green in your direction. This can be disabled via the `EnableOpticom` setting in the configuration file.
* **Intelligent Yielding**: Vehicles traveling in the same direction as you will move to the side to clear a path for you. They will attempt to find a safe space to pull over, either to the right or left, using their turn signals. This feature can be turned off with the `EnableSameSideYield` setting.
* **Oncoming Traffic Braking**: Vehicles in the oncoming lane will slow down or come to a stop as pass them. This is controlled by the `EnableOncomingBraking` setting.
* **Intersection Creep**: When traffic is stopped at an intersection you are approaching, vehicles will "creep" forward and to the side to create a path for you to maneuver through the backed-up traffic. You can toggle this with `EnableIntersectionCreep`.
* **Dynamic Intersection Clearing**: When you are approaching a major intersection (with traffic lights or stop signs), cross-traffic will be instructed to stop, clearing the way for you to proceed without having to navigate around other vehicles. The `EnableIntersectionControl` setting manages this feature.
* **Around Player Overtaking**: When you are stopped, vehicles behind you will attempt to safely overtake your vehicle rather than getting stuck behind you. This is particularly useful during traffic stops or when you are stationary on a roadway for any reason. This is managed by the `EnableAroundPlayerLogic` setting.

## Configuration

Every feature listed above can be individually enabled or disabled to your liking. Furthermore, you can fine-tune the behavior of each feature through the `MTFO.ini` file located in `Plugins/LSPDFR/`.

**Reloading the Configuration:** Any changes you make to the `MTFO.ini` file will be automatically loaded every time you go on duty in LSPDFR. This allows you to easily tweak the settings without needing to restart the game.

You can adjust parameters such as:
* Detection ranges and widths for vehicle scanning.
* The duration of a green light from the Opticom.
* How far vehicles will move to the side.
* Timeouts for various tasks to prevent vehicles from being stuck.
* And many more.