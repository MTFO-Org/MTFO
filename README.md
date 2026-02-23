# MTFO - Move The F\*\*\* Over (v4)

_Formally Opticom by Rohan_

## Description

This plugin for LSPDFR on GTA V is designed to completely overhaul and enhance traffic control and AI behavior when you are responding to a call with your lights and siren active. MTFO aims to create a more realistic and immersive experience by making the AI drivers react in a more intelligent and 'human-like' way to your presence.

_Credit goes to **Rohan** for the original "Opticom" functionality, which has been modified and adapted into this plugin._

## Features

MTFO is a complete overhaul of AI behavior while sirens are active:

- **Intelligent Lane Yielding**: When driving with sirens active, AI vehicles traveling ahead of you will actively seek out valid, physical road boundaries to pull over to the right. If they cannot safely pull over, they will attempt to change lanes out of your way instead of randomly slamming on their brakes in the middle of the road.
- **Dynamic Intersection Clearing**: As you approach a major intersection or stop sign, cross-traffic will be automatically instructed to stop. This clears the intersection, allowing you to proceed safely without weaving through unpredictable cross-traffic. (_Toggleable via `EnableIntersectionControl`_)
- **Opticom Intersection Control**: Approaching an intersection with traffic lights will automatically force the light green in your direction. It also includes an optional feature to trigger a yellow warning flash sequence before turning green to alert cross-traffic. (_Toggleable via `EnableOpticom`_)
- **Oncoming Traffic Braking**: Vehicles traveling in the oncoming lanes will recognize your emergency vehicle and slow down or come to a complete stop as you pass them, preventing head-on collisions. (_Toggleable via `EnableOncomingBraking`_)
- **Around Player Overtaking**: When you are stationary or moving very slowly (such as during a traffic stop or blocking a lane), vehicles stuck behind you will dynamically pathfind and overtake your vehicle. This prevents massive traffic jams behind your cruiser. This feature works whether you are inside or outside of your vehicle. (_Toggleable via `EnableAroundPlayerLogic` and `AroundPlayerLogicOnlyInVehicle`_)

## Configuration

You can fine-tune the behavior of every feature through the `MTFO.ini` file located in your `Plugins/LSPDFR/` directory.

**Reloading the Configuration:** Any changes made to the `MTFO.ini` file are automatically loaded every time you go On Duty in LSPDFR. You can easily tweak settings without needing to restart your game.

### Key Settings Available:

- **Debug Settings**: Enable visual 3D debug lines, road bounds, and on-screen task tracking to see exactly what the AI is thinking.
- **Opticom Timings**: Adjust how long the light stays green, the number of yellow warning flashes, and the interval between flashes.
- **Detection Ranges**: Customize the forward, lateral, and backward search distances to dictate exactly when the AI should start reacting to your sirens.
- **Intersection Logic**: Modify how far ahead the plugin searches for stop signs and traffic lights, and how wide the net is cast for stopping cross-traffic.
- **Feature Toggles**: Individually enable or disable Opticom, Intersection Control, Oncoming Braking, and the Around Player logic to suit your playstyle.
